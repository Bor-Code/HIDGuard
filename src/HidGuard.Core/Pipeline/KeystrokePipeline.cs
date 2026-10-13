using System.Diagnostics;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using HidGuard.Core.Countermeasures;
using HidGuard.Core.Domain;
using HidGuard.Core.Events;
using HidGuard.Core.Incidents;
using HidGuard.Core.Input;
using HidGuard.Core.Policy;
using HidGuard.Core.Timing;
using HidGuard.Core.Trust;

namespace HidGuard.Core.Pipeline;

public sealed class KeystrokePipeline : IKeystrokePipeline
{
    private static readonly Action<ILogger, Exception?> LogPipelineStartedDelegate =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(1, nameof(KeystrokePipeline)),
            "Keystroke pipeline started successfully.");

    private static readonly Action<ILogger, Exception?> LogPipelineStoppedDelegate =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(2, nameof(KeystrokePipeline)),
            "Keystroke pipeline stopped.");

    private static readonly Action<ILogger, string, Exception?> LogAttackDetectedDelegate =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(3, nameof(KeystrokePipeline)),
            "Keystroke injection attack detected from device {DeviceId}");

    private readonly IKeystrokeSource keystrokeSource;
    private readonly ITimingAnalysisEngine timingEngine;
    private readonly IPolicyEngine policyEngine;
    private readonly ICountermeasureService countermeasureService;
    private readonly ITrustStore trustStore;
    private readonly IKeystrokeInterceptor interceptor;
    private readonly IPipelineEventAggregator eventAggregator;
    private readonly IIncidentStore? incidentStore;
    private readonly PolicyOptions policyOptions;
    private readonly ILogger<KeystrokePipeline> logger;

    private readonly Channel<KeystrokeEvent> channel;
    private CancellationTokenSource? pipelineCts;
    private Task? processingTask;
    private bool isRunning;
    private bool disposed;

    private long totalKeystrokesProcessed;
    private long totalKeystrokesSuppressed;
    private long totalAttacksDetected;
    private long totalAttacksNeutralized;
    private DateTimeOffset startedAt;

    public bool IsRunning => isRunning;

    public KeystrokePipeline(
        IKeystrokeSource keystrokeSource,
        ITimingAnalysisEngine timingEngine,
        IPolicyEngine policyEngine,
        ICountermeasureService countermeasureService,
        ITrustStore trustStore,
        IKeystrokeInterceptor interceptor,
        IPipelineEventAggregator eventAggregator,
        PolicyOptions? policyOptions = null,
        IIncidentStore? incidentStore = null,
        ILogger<KeystrokePipeline>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(keystrokeSource);
        ArgumentNullException.ThrowIfNull(timingEngine);
        ArgumentNullException.ThrowIfNull(policyEngine);
        ArgumentNullException.ThrowIfNull(countermeasureService);
        ArgumentNullException.ThrowIfNull(trustStore);
        ArgumentNullException.ThrowIfNull(interceptor);
        ArgumentNullException.ThrowIfNull(eventAggregator);

        this.keystrokeSource = keystrokeSource;
        this.timingEngine = timingEngine;
        this.policyEngine = policyEngine;
        this.countermeasureService = countermeasureService;
        this.trustStore = trustStore;
        this.interceptor = interceptor;
        this.eventAggregator = eventAggregator;
        this.policyOptions = policyOptions ?? new PolicyOptions();
        this.incidentStore = incidentStore;
        this.logger = logger ?? NullLogger<KeystrokePipeline>.Instance;

        var options = new BoundedChannelOptions(10_000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        };
        channel = Channel.CreateBounded<KeystrokeEvent>(options);
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (isRunning)
        {
            return;
        }

        pipelineCts = new CancellationTokenSource();
        startedAt = DateTimeOffset.UtcNow;

        await trustStore.InitializeAsync(cancellationToken).ConfigureAwait(false);

        if (policyOptions.AutoEnrollFirstRun)
        {
            var isEmpty = await trustStore.IsEmptyAsync(cancellationToken).ConfigureAwait(false);
            if (isEmpty)
            {
                var connected = keystrokeSource.GetConnectedKeyboards();
                await trustStore.EnrollInitialDevicesAsync(connected, cancellationToken).ConfigureAwait(false);
            }
        }

        interceptor.StartInterception();
        keystrokeSource.StartListening();

        keystrokeSource.KeystrokeReceived += OnKeystrokeReceived;
        keystrokeSource.DeviceConnected += OnDeviceConnected;
        keystrokeSource.DeviceDisconnected += OnDeviceDisconnected;
        interceptor.KeySuppressed += OnKeySuppressed;

        processingTask = Task.Run(ProcessQueueAsync, CancellationToken.None);
        isRunning = true;

        LogPipelineStartedDelegate(logger, null);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!isRunning)
        {
            return;
        }

        isRunning = false;

        keystrokeSource.KeystrokeReceived -= OnKeystrokeReceived;
        keystrokeSource.DeviceConnected -= OnDeviceConnected;
        keystrokeSource.DeviceDisconnected -= OnDeviceDisconnected;
        interceptor.KeySuppressed -= OnKeySuppressed;

        channel.Writer.TryComplete();

        if (pipelineCts != null)
        {
            await pipelineCts.CancelAsync().ConfigureAwait(false);
        }

        if (processingTask != null)
        {
            try
            {
                await processingTask.WaitAsync(TimeSpan.FromSeconds(3), cancellationToken).ConfigureAwait(false);
            }
            catch (Exception)
            {
            }
        }

        keystrokeSource.StopListening();
        interceptor.StopInterception();

        pipelineCts?.Dispose();
        pipelineCts = null;

        LogPipelineStoppedDelegate(logger, null);
    }

    public PipelineStatistics GetStatistics()
    {
        return new PipelineStatistics
        {
            TotalKeystrokesProcessed = Interlocked.Read(ref totalKeystrokesProcessed),
            TotalKeystrokesSuppressed = Interlocked.Read(ref totalKeystrokesSuppressed),
            TotalAttacksDetected = Interlocked.Read(ref totalAttacksDetected),
            TotalAttacksNeutralized = Interlocked.Read(ref totalAttacksNeutralized),
            ActiveTrackedDevices = timingEngine.TrackedDevices.Count,
            StartedAt = startedAt
        };
    }

    private void OnKeystrokeReceived(KeystrokeEvent keystroke)
    {
        channel.Writer.TryWrite(keystroke);
    }

    private void OnDeviceConnected(DeviceIdentifier device)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                var trustLevel = await trustStore.GetTrustLevelAsync(device).ConfigureAwait(false);
                eventAggregator.PublishDeviceLifecycle(new DeviceLifecycleEvent(
                    device,
                    DeviceLifecycleEventType.Arrived,
                    DateTimeOffset.UtcNow,
                    trustLevel.ToString()));

                if (trustLevel == DeviceTrustLevel.Blocked)
                {
                    var count = keystrokeSource.GetConnectedKeyboards().Count;
                    var decision = await policyEngine.EvaluateAsync(device, null, count).ConfigureAwait(false);
                    await countermeasureService.ExecuteAsync(decision, count).ConfigureAwait(false);
                }
            }
            catch (Exception)
            {
            }
        }, CancellationToken.None);
    }

    private void OnDeviceDisconnected(DeviceIdentifier device)
    {
        timingEngine.RemoveDevice(device);
        eventAggregator.PublishDeviceLifecycle(new DeviceLifecycleEvent(
            device,
            DeviceLifecycleEventType.Removed,
            DateTimeOffset.UtcNow));
    }

    private void OnKeySuppressed(object? sender, int vkCode)
    {
        Interlocked.Increment(ref totalKeystrokesSuppressed);
    }

    private async Task ProcessQueueAsync()
    {
        var token = pipelineCts?.Token ?? CancellationToken.None;

        try
        {
            while (await channel.Reader.WaitToReadAsync(token).ConfigureAwait(false))
            {
                while (channel.Reader.TryRead(out var keystroke))
                {
                    var startTicks = Stopwatch.GetTimestamp();
                    var anomaly = timingEngine.ProcessKeystroke(keystroke);
                    PolicyDecision? decision = null;
                    CountermeasureExecutionResult? cmResult = null;

                    if (anomaly.IsSuspicious)
                    {
                        Interlocked.Increment(ref totalAttacksDetected);
                        LogAttackDetectedDelegate(logger, keystroke.DeviceId.FormattedHardwareId, null);

                        var count = keystrokeSource.GetConnectedKeyboards().Count;
                        if (count == 0)
                        {
                            count = 1;
                        }

                        decision = await policyEngine.EvaluateAsync(keystroke.DeviceId, anomaly, count, token).ConfigureAwait(false);
                        cmResult = await countermeasureService.ExecuteAsync(decision, count, token).ConfigureAwait(false);

                        if (cmResult.WorkstationLockAttempted || cmResult.DeviceDisableAttempted || cmResult.KeystrokeSuppressionActive)
                        {
                            Interlocked.Increment(ref totalAttacksNeutralized);
                        }

                        if (incidentStore != null)
                        {
                            var deviceHwId = keystroke.DeviceId.FormattedHardwareId;
                            var ruleText = anomaly.TriggeredSignals.Count > 0
                                ? string.Join(", ", anomaly.TriggeredSignals)
                                : "TimingAnomaly";
                            var actionText = decision.Action.ToString();
                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    await incidentStore.RecordIncidentAsync(new CreateIncidentRequest
                                    {
                                        DeviceId = deviceHwId,
                                        AnomalyScore = anomaly.Score,
                                        TriggeredRule = ruleText,
                                        CountermeasureAction = actionText,
                                        Metadata = new Dictionary<string, string>
                                        {
                                            ["DeviceId"] = deviceHwId,
                                            ["Path"] = keystroke.DeviceId.Path
                                        }
                                    }, token).ConfigureAwait(false);
                                }
                                catch (Exception)
                                {
                                }
                            }, CancellationToken.None);
                        }
                    }

                    var elapsed = Stopwatch.GetElapsedTime(startTicks);
                    Interlocked.Increment(ref totalKeystrokesProcessed);

                    eventAggregator.PublishKeystrokeProcessed(new PipelineKeystrokeProcessedEvent
                    {
                        Keystroke = keystroke,
                        AnomalyResult = anomaly,
                        Decision = decision,
                        CountermeasureResult = cmResult,
                        Latency = elapsed
                    });
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        await StopAsync().ConfigureAwait(false);
        disposed = true;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        StopAsync().GetAwaiter().GetResult();
        disposed = true;
    }
}
