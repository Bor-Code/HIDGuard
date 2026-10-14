using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using HidGuard.Core.Configuration;

namespace HidGuard.Core.Incidents;

public sealed class IncidentMaintenanceService : IAsyncDisposable
{
    private static readonly Action<ILogger, Exception?> LogConfigurationApplyFailedDelegate =
        LoggerMessage.Define(
            LogLevel.Warning,
            new EventId(1, nameof(IncidentMaintenanceService)),
            "Failed to apply updated incident store configuration.");

    private static readonly Action<ILogger, Exception?> LogPurgeFailedDelegate =
        LoggerMessage.Define(
            LogLevel.Warning,
            new EventId(2, nameof(IncidentMaintenanceService)),
            "Scheduled purge of expired incidents failed.");

    private readonly IIncidentStore incidentStore;
    private readonly IncidentPayloadBuffer payloadBuffer;
    private readonly IConfigurationManager configurationManager;
    private readonly TimeSpan? purgeIntervalOverride;
    private readonly ILogger<IncidentMaintenanceService> logger;
    private readonly object lifecycleGate = new();
    private CancellationTokenSource? loopCts;
    private Task? loopTask;
    private bool disposed;

    public IncidentMaintenanceService(
        IIncidentStore incidentStore,
        IncidentPayloadBuffer payloadBuffer,
        IConfigurationManager configurationManager,
        ILogger<IncidentMaintenanceService>? logger = null,
        TimeSpan? purgeIntervalOverride = null)
    {
        ArgumentNullException.ThrowIfNull(incidentStore);
        ArgumentNullException.ThrowIfNull(payloadBuffer);
        ArgumentNullException.ThrowIfNull(configurationManager);

        if (purgeIntervalOverride.HasValue && purgeIntervalOverride.Value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(purgeIntervalOverride), "Purge interval must be positive.");
        }

        this.incidentStore = incidentStore;
        this.payloadBuffer = payloadBuffer;
        this.configurationManager = configurationManager;
        this.purgeIntervalOverride = purgeIntervalOverride;
        this.logger = logger ?? NullLogger<IncidentMaintenanceService>.Instance;
    }

    public bool IsRunning
    {
        get
        {
            lock (lifecycleGate)
            {
                return loopTask != null;
            }
        }
    }

    public void Start()
    {
        lock (lifecycleGate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);

            if (loopTask != null)
            {
                return;
            }

            ApplyConfiguration(configurationManager.CurrentConfiguration);
            configurationManager.ConfigurationChanged += OnConfigurationChanged;

            loopCts = new CancellationTokenSource();
            var token = loopCts.Token;
            loopTask = Task.Run(() => RunPurgeLoopAsync(token), CancellationToken.None);
        }
    }

    public async Task StopAsync()
    {
        Task? taskToAwait;
        CancellationTokenSource? ctsToDispose;

        lock (lifecycleGate)
        {
            if (loopTask == null)
            {
                return;
            }

            configurationManager.ConfigurationChanged -= OnConfigurationChanged;
            taskToAwait = loopTask;
            ctsToDispose = loopCts;
            loopTask = null;
            loopCts = null;
        }

        if (ctsToDispose != null)
        {
            await ctsToDispose.CancelAsync().ConfigureAwait(false);
        }

        try
        {
            await taskToAwait.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            ctsToDispose?.Dispose();
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

    private void OnConfigurationChanged(object? sender, HidGuardConfiguration configuration)
    {
        ApplyConfiguration(configuration);
    }

    private void ApplyConfiguration(HidGuardConfiguration configuration)
    {
        try
        {
            incidentStore.UpdateConfiguration(configuration.Incidents);
            payloadBuffer.Configure(configuration.Incidents);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            LogConfigurationApplyFailedDelegate(logger, ex);
        }
    }

    private TimeSpan CurrentPurgeInterval()
    {
        return purgeIntervalOverride
            ?? TimeSpan.FromMinutes(configurationManager.CurrentConfiguration.Incidents.PurgeIntervalMinutes);
    }

    private async Task RunPurgeLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await incidentStore.PurgeExpiredIncidentsAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                LogPurgeFailedDelegate(logger, ex);
            }

            try
            {
                await Task.Delay(CurrentPurgeInterval(), token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
