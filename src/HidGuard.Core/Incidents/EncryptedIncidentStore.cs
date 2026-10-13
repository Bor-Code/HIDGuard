using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using HidGuard.Core.Configuration;
using HidGuard.Core.Security;

namespace HidGuard.Core.Incidents;

public sealed class EncryptedIncidentStore : IIncidentStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly Action<ILogger, Guid, Exception?> LogIncidentRecordedDelegate =
        LoggerMessage.Define<Guid>(
            LogLevel.Information,
            new EventId(1, nameof(EncryptedIncidentStore)),
            "Incident {IncidentId} successfully recorded.");

    private static readonly Action<ILogger, int, Exception?> LogIncidentsPurgedDelegate =
        LoggerMessage.Define<int>(
            LogLevel.Information,
            new EventId(2, nameof(EncryptedIncidentStore)),
            "Purged {PurgedCount} expired incidents.");

    private readonly string storageDirectory;
    private readonly IncidentStoreConfiguration configuration;
    private readonly IDataProtector dataProtector;
    private readonly ILogger<EncryptedIncidentStore> logger;
    private readonly SemaphoreSlim syncGate = new(1, 1);
    private bool disposed;

    public event EventHandler<IncidentRecord>? IncidentRecorded;

    public EncryptedIncidentStore(
        string storageDirectory,
        IncidentStoreConfiguration? configuration = null,
        IDataProtector? dataProtector = null,
        ILogger<EncryptedIncidentStore>? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageDirectory);
        this.storageDirectory = storageDirectory;
        this.configuration = configuration ?? new IncidentStoreConfiguration();
        this.configuration.Validate();
        this.dataProtector = dataProtector ?? new NoOpDataProtector();
        this.logger = logger ?? NullLogger<EncryptedIncidentStore>.Instance;

        if (!Directory.Exists(this.storageDirectory))
        {
            Directory.CreateDirectory(this.storageDirectory);
        }
    }

    public async Task<IncidentRecord> RecordIncidentAsync(CreateIncidentRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        byte[]? encryptedPayload = null;
        int? unencryptedLength = null;
        var hasPayload = false;

        if (configuration.EnableOptInPayloadCapture && request.RawPayload != null && request.RawPayload.Length > 0)
        {
            var length = Math.Min(request.RawPayload.Length, configuration.MaxPayloadBytes);
            var payloadToProtect = new byte[length];
            Array.Copy(request.RawPayload, payloadToProtect, length);

            encryptedPayload = dataProtector.Protect(payloadToProtect);
            hasPayload = true;
            unencryptedLength = length;
        }

        var record = new IncidentRecord
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            DeviceId = request.DeviceId,
            AnomalyScore = request.AnomalyScore,
            TriggeredRule = request.TriggeredRule,
            CountermeasureAction = request.CountermeasureAction,
            HasEncryptedPayload = hasPayload,
            EncryptedPayload = encryptedPayload,
            UnencryptedPayloadLength = unencryptedLength,
            RetentionExpiresAt = DateTimeOffset.UtcNow.AddDays(configuration.RetentionDays),
            Metadata = request.Metadata ?? new Dictionary<string, string>()
        };

        var filePath = GetIncidentFilePath(record.Id);
        var tempFilePath = $"{filePath}.tmp";

        await syncGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var json = JsonSerializer.Serialize(record, JsonOptions);
            await File.WriteAllTextAsync(tempFilePath, json, cancellationToken).ConfigureAwait(false);
            File.Move(tempFilePath, filePath, overwrite: true);

            EnforceCapacityLimit();
        }
        finally
        {
            syncGate.Release();
        }

        LogIncidentRecordedDelegate(logger, record.Id, null);
        IncidentRecorded?.Invoke(this, record);
        return record;
    }

    public async Task<IReadOnlyList<IncidentRecord>> GetIncidentsAsync(int limit = 100, CancellationToken cancellationToken = default)
    {
        if (limit <= 0)
        {
            return Array.Empty<IncidentRecord>();
        }

        await syncGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!Directory.Exists(storageDirectory))
            {
                return Array.Empty<IncidentRecord>();
            }

            var files = Directory.GetFiles(storageDirectory, "incident_*.json")
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .ToList();

            var records = new List<IncidentRecord>();

            foreach (var file in files)
            {
                if (records.Count >= limit)
                {
                    break;
                }

                try
                {
                    var json = await File.ReadAllTextAsync(file.FullName, cancellationToken).ConfigureAwait(false);
                    var record = JsonSerializer.Deserialize<IncidentRecord>(json, JsonOptions);
                    if (record != null)
                    {
                        records.Add(record);
                    }
                }
                catch (JsonException)
                {
                }
            }

            return records;
        }
        finally
        {
            syncGate.Release();
        }
    }

    public async Task<IncidentRecord?> GetIncidentByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var filePath = GetIncidentFilePath(id);

        await syncGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(filePath))
            {
                return null;
            }

            var json = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
            return JsonSerializer.Deserialize<IncidentRecord>(json, JsonOptions);
        }
        finally
        {
            syncGate.Release();
        }
    }

    public async Task<int> PurgeExpiredIncidentsAsync(CancellationToken cancellationToken = default)
    {
        await syncGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!Directory.Exists(storageDirectory))
            {
                return 0;
            }

            var files = Directory.GetFiles(storageDirectory, "incident_*.json");
            var purgedCount = 0;
            var now = DateTimeOffset.UtcNow;

            foreach (var filePath in files)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
                    var record = JsonSerializer.Deserialize<IncidentRecord>(json, JsonOptions);
                    if (record != null && record.RetentionExpiresAt <= now)
                    {
                        File.Delete(filePath);
                        purgedCount++;
                    }
                }
                catch (Exception)
                {
                }
            }

            if (purgedCount > 0)
            {
                LogIncidentsPurgedDelegate(logger, purgedCount, null);
            }

            return purgedCount;
        }
        finally
        {
            syncGate.Release();
        }
    }

    public async Task<byte[]?> DecryptPayloadAsync(Guid incidentId, CancellationToken cancellationToken = default)
    {
        var record = await GetIncidentByIdAsync(incidentId, cancellationToken).ConfigureAwait(false);
        if (record == null || record.EncryptedPayload == null || record.EncryptedPayload.Length == 0)
        {
            return null;
        }

        return dataProtector.Unprotect(record.EncryptedPayload);
    }

    public async Task ExportMetadataReportAsync(
        Stream outputStream,
        IncidentExportFormat format = IncidentExportFormat.Json,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(outputStream);

        var incidents = await GetIncidentsAsync(int.MaxValue, cancellationToken).ConfigureAwait(false);

        var reportItems = incidents.Select(i => new IncidentMetadataReportItem
        {
            IncidentId = i.Id,
            Timestamp = i.Timestamp,
            DeviceId = i.DeviceId,
            AnomalyScore = i.AnomalyScore,
            TriggeredRule = i.TriggeredRule,
            CountermeasureAction = i.CountermeasureAction,
            HasEncryptedPayload = i.HasEncryptedPayload,
            EncryptedPayloadSizeBytes = i.EncryptedPayload?.Length ?? 0,
            RetentionExpiresAt = i.RetentionExpiresAt,
            Metadata = i.Metadata
        }).ToList();

        if (format == IncidentExportFormat.Json)
        {
            await JsonSerializer.SerializeAsync(outputStream, reportItems, JsonOptions, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            using var writer = new StreamWriter(outputStream, Encoding.UTF8, bufferSize: 1024, leaveOpen: true);
            await writer.WriteLineAsync(
                "IncidentId,Timestamp,DeviceId,AnomalyScore,TriggeredRule,CountermeasureAction,HasEncryptedPayload,EncryptedPayloadSizeBytes,RetentionExpiresAt")
                .ConfigureAwait(false);

            foreach (var item in reportItems)
            {
                var line = string.Format(
                    CultureInfo.InvariantCulture,
                    "\"{0}\",\"{1:O}\",\"{2}\",{3:F2},\"{4}\",\"{5}\",{6},{7},\"{8:O}\"",
                    item.IncidentId,
                    item.Timestamp,
                    EscapeCsv(item.DeviceId),
                    item.AnomalyScore,
                    EscapeCsv(item.TriggeredRule),
                    EscapeCsv(item.CountermeasureAction),
                    item.HasEncryptedPayload,
                    item.EncryptedPayloadSizeBytes,
                    item.RetentionExpiresAt);

                await writer.WriteLineAsync(line).ConfigureAwait(false);
            }

            await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        syncGate.Dispose();
        disposed = true;
    }

    private string GetIncidentFilePath(Guid id)
    {
        return Path.Combine(storageDirectory, $"incident_{id:N}.json");
    }

    private void EnforceCapacityLimit()
    {
        if (!Directory.Exists(storageDirectory))
        {
            return;
        }

        var files = Directory.GetFiles(storageDirectory, "incident_*.json")
            .Select(f => new FileInfo(f))
            .OrderBy(f => f.LastWriteTimeUtc)
            .ToList();

        if (files.Count <= configuration.MaxIncidentCount)
        {
            return;
        }

        var excessCount = files.Count - configuration.MaxIncidentCount;
        for (var i = 0; i < excessCount; i++)
        {
            try
            {
                files[i].Delete();
            }
            catch (IOException)
            {
            }
        }
    }

    private static string EscapeCsv(string value)
    {
        return value.Replace("\"", "\"\"", StringComparison.Ordinal);
    }

    private sealed class NoOpDataProtector : IDataProtector
    {
        public byte[] Protect(byte[] userData)
        {
            ArgumentNullException.ThrowIfNull(userData);
            var copy = new byte[userData.Length];
            Array.Copy(userData, copy, userData.Length);
            return copy;
        }

        public byte[] Unprotect(byte[] encryptedData)
        {
            ArgumentNullException.ThrowIfNull(encryptedData);
            var copy = new byte[encryptedData.Length];
            Array.Copy(encryptedData, copy, encryptedData.Length);
            return copy;
        }
    }
}
