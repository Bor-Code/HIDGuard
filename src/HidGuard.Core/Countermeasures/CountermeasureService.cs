using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using HidGuard.Core.Audit;
using HidGuard.Core.Policy;
using HidGuard.Core.Trust;

namespace HidGuard.Core.Countermeasures;

public sealed class CountermeasureService : ICountermeasureService
{
    private static readonly Action<ILogger, PolicyAction, string, Exception?> LogPolicyActionExecutedDelegate =
        LoggerMessage.Define<PolicyAction, string>(
            LogLevel.Information,
            new EventId(1, nameof(CountermeasureService)),
            "Executing countermeasure action {Action} for device {DeviceId}");

    private static readonly Action<ILogger, string, Exception?> LogSoleKeyboardProtectedDelegate =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(2, nameof(CountermeasureService)),
            "Sole attached keyboard {DeviceId} protected from hardware disabling.");

    private readonly IWorkstationLocker locker;
    private readonly IDeviceDisabler deviceDisabler;
    private readonly IKeystrokeInterceptor interceptor;
    private readonly ITrustStore trustStore;
    private readonly IWindowsEventLogService? eventLogService;
    private readonly IDevicePromptService? promptService;
    private readonly ILogger<CountermeasureService> logger;

    public event EventHandler<CountermeasureExecutionResult>? CountermeasureExecuted;

    public CountermeasureService(
        IWorkstationLocker locker,
        IDeviceDisabler deviceDisabler,
        IKeystrokeInterceptor interceptor,
        ITrustStore trustStore,
        ILogger<CountermeasureService>? logger = null)
        : this(locker, deviceDisabler, interceptor, trustStore, null, null, logger)
    {
    }

    public CountermeasureService(
        IWorkstationLocker locker,
        IDeviceDisabler deviceDisabler,
        IKeystrokeInterceptor interceptor,
        ITrustStore trustStore,
        IWindowsEventLogService? eventLogService,
        ILogger<CountermeasureService>? logger = null)
        : this(locker, deviceDisabler, interceptor, trustStore, eventLogService, null, logger)
    {
    }

    public CountermeasureService(
        IWorkstationLocker locker,
        IDeviceDisabler deviceDisabler,
        IKeystrokeInterceptor interceptor,
        ITrustStore trustStore,
        IWindowsEventLogService? eventLogService,
        IDevicePromptService? promptService,
        ILogger<CountermeasureService>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(locker);
        ArgumentNullException.ThrowIfNull(deviceDisabler);
        ArgumentNullException.ThrowIfNull(interceptor);
        ArgumentNullException.ThrowIfNull(trustStore);

        this.locker = locker;
        this.deviceDisabler = deviceDisabler;
        this.interceptor = interceptor;
        this.trustStore = trustStore;
        this.eventLogService = eventLogService;
        this.promptService = promptService;
        this.logger = logger ?? NullLogger<CountermeasureService>.Instance;
    }

    public async Task<CountermeasureExecutionResult> ExecuteAsync(
        PolicyDecision decision,
        int totalKeyboardsAttached,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(decision);

        LogPolicyActionExecutedDelegate(logger, decision.Action, decision.DeviceId.FormattedHardwareId, null);

        var result = decision.Action switch
        {
            PolicyAction.Allow => HandleAllow(decision),
            PolicyAction.AlertOnly => HandleAlertOnly(decision),
            PolicyAction.PromptUser => await HandlePromptUserAsync(decision, cancellationToken).ConfigureAwait(false),
            PolicyAction.LockWorkstation => HandleLockWorkstation(decision),
            PolicyAction.BlockDevice => await HandleBlockDeviceAsync(decision, totalKeyboardsAttached, cancellationToken).ConfigureAwait(false),
            PolicyAction.LockAndBlock => await HandleLockAndBlockAsync(decision, totalKeyboardsAttached, cancellationToken).ConfigureAwait(false),
            _ => HandleAllow(decision)
        };

        CountermeasureExecuted?.Invoke(this, result);
        LogToWindowsEventLog(result);
        return result;
    }

    private CountermeasureExecutionResult HandleAllow(PolicyDecision decision)
    {
        return new CountermeasureExecutionResult
        {
            Decision = decision,
            WorkstationLockAttempted = false,
            WorkstationLockSucceeded = false,
            DeviceDisableAttempted = false,
            DeviceDisableSucceeded = false,
            KeystrokeSuppressionActive = interceptor.IsSuppressionActive,
            ExecutedAt = DateTimeOffset.UtcNow,
            Message = "Allowed without countermeasures."
        };
    }

    private CountermeasureExecutionResult HandleAlertOnly(PolicyDecision decision)
    {
        return new CountermeasureExecutionResult
        {
            Decision = decision,
            WorkstationLockAttempted = false,
            WorkstationLockSucceeded = false,
            DeviceDisableAttempted = false,
            DeviceDisableSucceeded = false,
            KeystrokeSuppressionActive = interceptor.IsSuppressionActive,
            ExecutedAt = DateTimeOffset.UtcNow,
            Message = "Alert triggered; no blocking countermeasures executed."
        };
    }

    private async Task<CountermeasureExecutionResult> HandlePromptUserAsync(
        PolicyDecision decision,
        CancellationToken cancellationToken)
    {
        if (promptService != null)
        {
            var choice = await promptService.PromptDeviceTrustAsync(decision.DeviceId, null, cancellationToken).ConfigureAwait(false);
            if (choice == DeviceTrustLevel.Trusted)
            {
                await trustStore.SetTrustLevelAsync(decision.DeviceId, DeviceTrustLevel.Trusted, "UserPrompt", cancellationToken: cancellationToken).ConfigureAwait(false);
                eventLogService?.WriteSecurityEvent(
                    SecurityEventIds.DeviceReEnabled,
                    SecurityEventSeverity.Information,
                    $"HIDGuard device {decision.DeviceId.FormattedHardwareId} trusted via user prompt.");

                return new CountermeasureExecutionResult
                {
                    Decision = decision,
                    WorkstationLockAttempted = false,
                    WorkstationLockSucceeded = false,
                    DeviceDisableAttempted = false,
                    DeviceDisableSucceeded = false,
                    KeystrokeSuppressionActive = interceptor.IsSuppressionActive,
                    ExecutedAt = DateTimeOffset.UtcNow,
                    Message = "User prompt: Device trusted."
                };
            }

            if (choice == DeviceTrustLevel.Blocked)
            {
                await trustStore.SetTrustLevelAsync(decision.DeviceId, DeviceTrustLevel.Blocked, "UserPrompt", cancellationToken: cancellationToken).ConfigureAwait(false);
                var disableSucceeded = await deviceDisabler.DisableDeviceAsync(decision.DeviceId, cancellationToken).ConfigureAwait(false);
                eventLogService?.WriteSecurityEvent(
                    SecurityEventIds.DeviceDisabled,
                    disableSucceeded ? SecurityEventSeverity.Warning : SecurityEventSeverity.Error,
                    $"HIDGuard device {decision.DeviceId.FormattedHardwareId} blocked via user prompt.");

                return new CountermeasureExecutionResult
                {
                    Decision = decision,
                    WorkstationLockAttempted = false,
                    WorkstationLockSucceeded = false,
                    DeviceDisableAttempted = true,
                    DeviceDisableSucceeded = disableSucceeded,
                    KeystrokeSuppressionActive = interceptor.IsSuppressionActive,
                    ExecutedAt = DateTimeOffset.UtcNow,
                    Message = "User prompt: Device blocked and disabled."
                };
            }
        }

        return new CountermeasureExecutionResult
        {
            Decision = decision,
            WorkstationLockAttempted = false,
            WorkstationLockSucceeded = false,
            DeviceDisableAttempted = false,
            DeviceDisableSucceeded = false,
            KeystrokeSuppressionActive = interceptor.IsSuppressionActive,
            ExecutedAt = DateTimeOffset.UtcNow,
            Message = "User prompt requested."
        };
    }

    private CountermeasureExecutionResult HandleLockWorkstation(PolicyDecision decision)
    {
        interceptor.IsSuppressionActive = true;
        var lockSucceeded = locker.LockWorkstation();

        return new CountermeasureExecutionResult
        {
            Decision = decision,
            WorkstationLockAttempted = true,
            WorkstationLockSucceeded = lockSucceeded,
            DeviceDisableAttempted = false,
            DeviceDisableSucceeded = false,
            KeystrokeSuppressionActive = true,
            ExecutedAt = DateTimeOffset.UtcNow,
            Message = "Workstation locked to neutralize keystroke injection."
        };
    }

    private async Task<CountermeasureExecutionResult> HandleBlockDeviceAsync(
        PolicyDecision decision,
        int totalKeyboardsAttached,
        CancellationToken cancellationToken)
    {
        interceptor.IsSuppressionActive = true;

        if (totalKeyboardsAttached <= 1)
        {
            LogSoleKeyboardProtectedDelegate(logger, decision.DeviceId.FormattedHardwareId, null);
            var lockSucceeded = locker.LockWorkstation();

            return new CountermeasureExecutionResult
            {
                Decision = decision,
                WorkstationLockAttempted = true,
                WorkstationLockSucceeded = lockSucceeded,
                DeviceDisableAttempted = false,
                DeviceDisableSucceeded = false,
                KeystrokeSuppressionActive = true,
                ExecutedAt = DateTimeOffset.UtcNow,
                Message = "Device disable skipped to prevent sole keyboard lockout. Workstation locked instead."
            };
        }

        var disableSucceeded = await deviceDisabler.DisableDeviceAsync(decision.DeviceId, cancellationToken).ConfigureAwait(false);
        await trustStore.SetTrustLevelAsync(decision.DeviceId, DeviceTrustLevel.Blocked, "CountermeasureEngine", cancellationToken: cancellationToken).ConfigureAwait(false);

        return new CountermeasureExecutionResult
        {
            Decision = decision,
            WorkstationLockAttempted = false,
            WorkstationLockSucceeded = false,
            DeviceDisableAttempted = true,
            DeviceDisableSucceeded = disableSucceeded,
            KeystrokeSuppressionActive = true,
            ExecutedAt = DateTimeOffset.UtcNow,
            Message = disableSucceeded ? "Device disabled at PnP level and marked blocked." : "Failed to disable device at PnP level."
        };
    }

    private async Task<CountermeasureExecutionResult> HandleLockAndBlockAsync(
        PolicyDecision decision,
        int totalKeyboardsAttached,
        CancellationToken cancellationToken)
    {
        interceptor.IsSuppressionActive = true;
        var lockSucceeded = locker.LockWorkstation();

        if (totalKeyboardsAttached <= 1)
        {
            LogSoleKeyboardProtectedDelegate(logger, decision.DeviceId.FormattedHardwareId, null);

            return new CountermeasureExecutionResult
            {
                Decision = decision,
                WorkstationLockAttempted = true,
                WorkstationLockSucceeded = lockSucceeded,
                DeviceDisableAttempted = false,
                DeviceDisableSucceeded = false,
                KeystrokeSuppressionActive = true,
                ExecutedAt = DateTimeOffset.UtcNow,
                Message = "Workstation locked. Device disable skipped for sole attached keyboard."
            };
        }

        var disableSucceeded = await deviceDisabler.DisableDeviceAsync(decision.DeviceId, cancellationToken).ConfigureAwait(false);
        await trustStore.SetTrustLevelAsync(decision.DeviceId, DeviceTrustLevel.Blocked, "CountermeasureEngine", cancellationToken: cancellationToken).ConfigureAwait(false);

        return new CountermeasureExecutionResult
        {
            Decision = decision,
            WorkstationLockAttempted = true,
            WorkstationLockSucceeded = lockSucceeded,
            DeviceDisableAttempted = true,
            DeviceDisableSucceeded = disableSucceeded,
            KeystrokeSuppressionActive = true,
            ExecutedAt = DateTimeOffset.UtcNow,
            Message = "Workstation locked and device disabled at PnP level."
        };
    }

    private void LogToWindowsEventLog(CountermeasureExecutionResult result)
    {
        if (eventLogService == null)
        {
            return;
        }

        if (result.Decision.Action is PolicyAction.Allow or PolicyAction.AlertOnly or PolicyAction.PromptUser)
        {
            return;
        }

        var deviceId = result.Decision.DeviceId.FormattedHardwareId;

        if (result.DeviceDisableAttempted)
        {
            var status = result.DeviceDisableSucceeded ? "succeeded" : "failed";
            eventLogService.WriteSecurityEvent(
                SecurityEventIds.DeviceDisabled,
                result.DeviceDisableSucceeded ? SecurityEventSeverity.Warning : SecurityEventSeverity.Error,
                $"HIDGuard disabled device {deviceId} at PnP level (status: {status}). Decision: {result.Decision.Action}. Reason: {result.Decision.Reason}.");
        }
        else if (result.WorkstationLockAttempted && result.Message.Contains("sole keyboard", StringComparison.OrdinalIgnoreCase))
        {
            eventLogService.WriteSecurityEvent(
                SecurityEventIds.AntiLockoutTriggered,
                SecurityEventSeverity.Warning,
                $"HIDGuard anti-lockout protection triggered for sole keyboard {deviceId}. Hardware disable skipped; workstation locked instead. Reason: {result.Decision.Reason}.");
        }
        else if (result.WorkstationLockAttempted)
        {
            eventLogService.WriteSecurityEvent(
                SecurityEventIds.AttackDetected,
                SecurityEventSeverity.Warning,
                $"HIDGuard detected keystroke injection attack from device {deviceId}. Workstation locked. Reason: {result.Decision.Reason}.");
        }
    }
}
