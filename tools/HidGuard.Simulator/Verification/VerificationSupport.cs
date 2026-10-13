using HidGuard.Core.Audit;
using HidGuard.Core.Countermeasures;
using HidGuard.Core.Domain;
using HidGuard.Core.Input;
using HidGuard.Core.Trust;

namespace HidGuard.Simulator.Verification;

internal sealed class VerificationKeystrokeSource : IKeystrokeSource
{
    private readonly List<DeviceIdentifier> connectedKeyboards = new();

    public event Action<KeystrokeEvent>? KeystrokeReceived;
    public event Action<DeviceIdentifier>? DeviceConnected;
    public event Action<DeviceIdentifier>? DeviceDisconnected;

    public bool IsRunning { get; private set; }

    public void StartListening() => IsRunning = true;
    public void StopListening() => IsRunning = false;

    public IReadOnlyList<DeviceIdentifier> GetConnectedKeyboards()
    {
        lock (connectedKeyboards)
        {
            return connectedKeyboards.ToList();
        }
    }

    public void SetConnectedKeyboards(IEnumerable<DeviceIdentifier> keyboards)
    {
        lock (connectedKeyboards)
        {
            connectedKeyboards.Clear();
            connectedKeyboards.AddRange(keyboards);
        }
    }

    public void EmitKeystroke(KeystrokeEvent keystroke) => KeystrokeReceived?.Invoke(keystroke);
    public void EmitDeviceConnected(DeviceIdentifier device) => DeviceConnected?.Invoke(device);
    public void EmitDeviceDisconnected(DeviceIdentifier device) => DeviceDisconnected?.Invoke(device);

    public void Dispose()
    {
    }
}

internal sealed class VerificationTrustStore : ITrustStore
{
    private readonly Dictionary<string, DeviceTrustLevel> store = new(StringComparer.OrdinalIgnoreCase);

    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<DeviceTrustLevel> GetTrustLevelAsync(DeviceIdentifier deviceId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(deviceId);
        store.TryGetValue(deviceId.FormattedHardwareId, out var level);
        return Task.FromResult(level);
    }

    public Task SetTrustLevelAsync(
        DeviceIdentifier deviceId,
        DeviceTrustLevel level,
        string enrolledBy,
        string? friendlyName = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(deviceId);
        store[deviceId.FormattedHardwareId] = level;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<TrustRecord>> GetAllRecordsAsync(CancellationToken cancellationToken = default)
    {
        var records = store.Select(kv => new TrustRecord(
            new DeviceIdentifier(kv.Key, 0, 0),
            kv.Value,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            "VerificationSuite"
        )).ToList();
        return Task.FromResult<IReadOnlyList<TrustRecord>>(records);
    }

    public Task<int> EnrollInitialDevicesAsync(IEnumerable<DeviceIdentifier> devices, CancellationToken cancellationToken = default)
    {
        var count = 0;
        foreach (var device in devices)
        {
            if (!store.ContainsKey(device.FormattedHardwareId))
            {
                store[device.FormattedHardwareId] = DeviceTrustLevel.Trusted;
                count++;
            }
        }
        return Task.FromResult(count);
    }

    public Task<bool> IsEmptyAsync(CancellationToken cancellationToken = default) => Task.FromResult(store.Count == 0);

    public void Dispose()
    {
    }
}

internal sealed class VerificationWorkstationLocker : IWorkstationLocker
{
    public int LockCallCount { get; private set; }

    public bool LockWorkstation()
    {
        LockCallCount++;
        return true;
    }
}

internal sealed class VerificationDeviceDisabler : IDeviceDisabler
{
    public List<DeviceIdentifier> DisabledDevices { get; } = new();

    public Task<bool> DisableDeviceAsync(DeviceIdentifier deviceId, CancellationToken cancellationToken = default)
    {
        lock (DisabledDevices)
        {
            DisabledDevices.Add(deviceId);
        }
        return Task.FromResult(true);
    }

    public Task<bool> EnableDeviceAsync(DeviceIdentifier deviceId, CancellationToken cancellationToken = default) => Task.FromResult(true);
}

internal sealed class VerificationKeystrokeInterceptor : IKeystrokeInterceptor
{
    public bool IsSuppressionActive { get; set; }
    public int SuppressedKeyCount { get; private set; }
    public event EventHandler<int>? KeySuppressed;

    public void StartInterception()
    {
    }

    public void StopInterception()
    {
    }

    public void SuppressTemporarily(TimeSpan duration) => IsSuppressionActive = true;

    public void SimulateKeySuppression(int vkCode)
    {
        SuppressedKeyCount++;
        KeySuppressed?.Invoke(this, vkCode);
    }

    public void Dispose()
    {
    }
}

internal sealed class VerificationAuditTrailService : IAuditTrailService
{
    private readonly List<AuditEvent> events = new();

    public event EventHandler<AuditEvent>? AuditEventRecorded;

    public IReadOnlyList<AuditEvent> RecordedEvents
    {
        get
        {
            lock (events)
            {
                return events.ToList();
            }
        }
    }

    public Task RecordEventAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        lock (events)
        {
            events.Add(auditEvent);
        }
        AuditEventRecorded?.Invoke(this, auditEvent);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AuditEvent>> QueryRecentEventsAsync(int limit = 100, CancellationToken cancellationToken = default)
    {
        lock (events)
        {
            var result = events.OrderByDescending(e => e.Timestamp).Take(limit).ToList();
            return Task.FromResult<IReadOnlyList<AuditEvent>>(result);
        }
    }

    public void Dispose()
    {
    }
}
