using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using HidGuard.Core.Domain;

namespace HidGuard.Core.Trust;

public sealed class TrustStore : ITrustStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private static readonly Action<ILogger, Exception?> LogTrustStoreCorruptedAction =
        LoggerMessage.Define(LogLevel.Warning, new EventId(1, nameof(TrustStore)), "Trust store corrupted. Initializing fresh trust store.");

    private readonly SemaphoreSlim syncGate = new(1, 1);
    private readonly ITrustStoreStorage storage;
    private readonly ILogger<TrustStore> logger;
    private readonly Dictionary<string, TrustRecord> records = new(StringComparer.OrdinalIgnoreCase);
    private bool initialized;

    public TrustStore(ITrustStoreStorage storage, ILogger<TrustStore>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(storage);
        this.storage = storage;
        this.logger = logger ?? NullLogger<TrustStore>.Instance;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await syncGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (initialized)
            {
                return;
            }

            records.Clear();
            var rawJson = await storage.ReadRawAsync(cancellationToken).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(rawJson))
            {
                try
                {
                    var data = JsonSerializer.Deserialize<TrustStoreData>(rawJson, JsonOptions);
                    if (data?.Records != null)
                    {
                        foreach (var record in data.Records)
                        {
                            var key = GetLookupKey(record.DeviceId);
                            records[key] = record;
                        }
                    }
                }
                catch (JsonException ex)
                {
                    LogTrustStoreCorruptedAction(logger, ex);
                    await PersistInternalAsync(cancellationToken).ConfigureAwait(false);
                }
            }

            initialized = true;
        }
        finally
        {
            syncGate.Release();
        }
    }

    public async Task<DeviceTrustLevel> GetTrustLevelAsync(DeviceIdentifier deviceId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(deviceId);

        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        await syncGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var key = GetLookupKey(deviceId);
            if (records.TryGetValue(key, out var record))
            {
                return record.TrustLevel;
            }

            return DeviceTrustLevel.Unknown;
        }
        finally
        {
            syncGate.Release();
        }
    }

    public async Task SetTrustLevelAsync(
        DeviceIdentifier deviceId,
        DeviceTrustLevel level,
        string enrolledBy,
        string? friendlyName = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(deviceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(enrolledBy);

        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        await syncGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var key = GetLookupKey(deviceId);
            var now = DateTimeOffset.UtcNow;

            if (records.TryGetValue(key, out var existing))
            {
                records[key] = new TrustRecord(
                    deviceId,
                    level,
                    existing.FirstSeenAt,
                    now,
                    enrolledBy,
                    friendlyName ?? existing.FriendlyName);
            }
            else
            {
                records[key] = new TrustRecord(
                    deviceId,
                    level,
                    now,
                    now,
                    enrolledBy,
                    friendlyName);
            }

            await PersistInternalAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            syncGate.Release();
        }
    }

    public async Task<IReadOnlyList<TrustRecord>> GetAllRecordsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        await syncGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return records.Values.ToList();
        }
        finally
        {
            syncGate.Release();
        }
    }

    public async Task<int> EnrollInitialDevicesAsync(IEnumerable<DeviceIdentifier> devices, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(devices);

        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        await syncGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (records.Count > 0)
            {
                return 0;
            }

            var enrolledCount = 0;
            var now = DateTimeOffset.UtcNow;

            foreach (var device in devices)
            {
                if (device == DeviceIdentifier.Synthetic)
                {
                    continue;
                }

                var key = GetLookupKey(device);
                if (!records.ContainsKey(key))
                {
                    records[key] = new TrustRecord(
                        device,
                        DeviceTrustLevel.Trusted,
                        now,
                        now,
                        "InitialRunEnrollment");
                    enrolledCount++;
                }
            }

            if (enrolledCount > 0)
            {
                await PersistInternalAsync(cancellationToken).ConfigureAwait(false);
            }

            return enrolledCount;
        }
        finally
        {
            syncGate.Release();
        }
    }

    public async Task<bool> IsEmptyAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        await syncGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return records.Count == 0;
        }
        finally
        {
            syncGate.Release();
        }
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (!initialized)
        {
            await InitializeAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task PersistInternalAsync(CancellationToken cancellationToken)
    {
        var data = new TrustStoreData
        {
            SchemaVersion = TrustStoreData.CurrentSchemaVersion,
            LastUpdated = DateTimeOffset.UtcNow,
            Records = records.Values.ToList()
        };

        var json = JsonSerializer.Serialize(data, JsonOptions);
        await storage.WriteRawAsync(json, cancellationToken).ConfigureAwait(false);
    }

    private static string GetLookupKey(DeviceIdentifier deviceId)
    {
        if (deviceId.VendorId != 0 || deviceId.ProductId != 0)
        {
            return deviceId.FormattedHardwareId;
        }

        return deviceId.Path;
    }

    public void Dispose()
    {
        syncGate.Dispose();
    }
}
