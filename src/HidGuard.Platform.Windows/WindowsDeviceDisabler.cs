using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Windows.Win32;
using Windows.Win32.Foundation;
using HidGuard.Core.Countermeasures;
using HidGuard.Core.Domain;

namespace HidGuard.Platform.Windows;

public sealed class WindowsDeviceDisabler : IDeviceDisabler
{
    private static readonly Action<ILogger, string, Exception?> LogDeviceDisabledDelegate =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(1, nameof(WindowsDeviceDisabler)),
            "Successfully disabled device {DeviceId} via PnP manager.");

    private static readonly Action<ILogger, string, uint, Exception?> LogDeviceDisableFailedDelegate =
        LoggerMessage.Define<string, uint>(
            LogLevel.Warning,
            new EventId(2, nameof(WindowsDeviceDisabler)),
            "Failed to disable device {DeviceId} via PnP manager. Return code: {Code}");

    private static readonly Action<ILogger, string, Exception?> LogDeviceEnabledDelegate =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(3, nameof(WindowsDeviceDisabler)),
            "Successfully enabled device {DeviceId} via PnP manager.");

    private readonly ILogger<WindowsDeviceDisabler> logger;

    public WindowsDeviceDisabler(ILogger<WindowsDeviceDisabler>? logger = null)
    {
        this.logger = logger ?? NullLogger<WindowsDeviceDisabler>.Instance;
    }

    public async Task<bool> DisableDeviceAsync(DeviceIdentifier deviceId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(deviceId);

        if (deviceId == DeviceIdentifier.Synthetic)
        {
            return false;
        }

        var instanceId = ExtractDeviceInstanceId(deviceId.Path);
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return false;
        }

        var result = DisableViaCfgMgr(instanceId);
        if (result)
        {
            LogDeviceDisabledDelegate(logger, deviceId.FormattedHardwareId, null);
            return true;
        }

        var fallbackResult = await DisableViaProcessAsync(instanceId, cancellationToken).ConfigureAwait(false);
        if (fallbackResult)
        {
            LogDeviceDisabledDelegate(logger, deviceId.FormattedHardwareId, null);
            return true;
        }

        LogDeviceDisableFailedDelegate(logger, deviceId.FormattedHardwareId, 1, null);
        return false;
    }

    public Task<bool> EnableDeviceAsync(DeviceIdentifier deviceId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(deviceId);

        if (deviceId == DeviceIdentifier.Synthetic)
        {
            return Task.FromResult(false);
        }

        var instanceId = ExtractDeviceInstanceId(deviceId.Path);
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return Task.FromResult(false);
        }

        var result = EnableViaCfgMgr(instanceId);
        if (result)
        {
            LogDeviceEnabledDelegate(logger, deviceId.FormattedHardwareId, null);
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    public static string? ExtractDeviceInstanceId(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var clean = path;
        if (clean.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase) ||
            clean.StartsWith(@"\\.\", StringComparison.OrdinalIgnoreCase))
        {
            clean = clean[4..];
        }

        var parts = clean.Split('#', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 3)
        {
            return $"{parts[0]}\\{parts[1]}\\{parts[2]}";
        }

        return null;
    }

    private static bool DisableViaCfgMgr(string instanceId)
    {
        try
        {
            unsafe
            {
                fixed (char* pId = instanceId)
                {
                    var locateResult = PInvoke.CM_Locate_DevNode(out var devInst, pId, 0);
                    if (locateResult == 0)
                    {
                        var disableResult = PInvoke.CM_Disable_DevNode(devInst, 0);
                        return disableResult == 0;
                    }
                }
            }
        }
        catch
        {
        }

        return false;
    }

    private static bool EnableViaCfgMgr(string instanceId)
    {
        try
        {
            unsafe
            {
                fixed (char* pId = instanceId)
                {
                    var locateResult = PInvoke.CM_Locate_DevNode(out var devInst, pId, 0);
                    if (locateResult == 0)
                    {
                        var enableResult = PInvoke.CM_Enable_DevNode(devInst, 0);
                        return enableResult == 0;
                    }
                }
            }
        }
        catch
        {
        }

        return false;
    }

    private static async Task<bool> DisableViaProcessAsync(string instanceId, CancellationToken cancellationToken)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -NonInteractive -Command \"Disable-PnpDevice -InstanceId '{instanceId}' -Confirm:$false\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(psi);
            if (process == null)
            {
                return false;
            }

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
