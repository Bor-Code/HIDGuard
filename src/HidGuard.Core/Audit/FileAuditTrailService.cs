using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HidGuard.Core.Audit;

public sealed class FileAuditTrailService : IAuditTrailService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string auditDirectory;
    private readonly SemaphoreSlim syncGate = new(1, 1);
    private bool disposed;

    public event EventHandler<AuditEvent>? AuditEventRecorded;

    public FileAuditTrailService(string auditDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(auditDirectory);
        this.auditDirectory = auditDirectory;

        if (!Directory.Exists(auditDirectory))
        {
            Directory.CreateDirectory(auditDirectory);
        }
    }

    public async Task RecordEventAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        var dateString = auditEvent.Timestamp.UtcDateTime.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var filePath = Path.Combine(auditDirectory, $"audit-{dateString}.jsonl");
        var line = JsonSerializer.Serialize(auditEvent, JsonOptions) + Environment.NewLine;

        await syncGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await File.AppendAllTextAsync(filePath, line, cancellationToken).ConfigureAwait(false);
            AuditEventRecorded?.Invoke(this, auditEvent);
        }
        finally
        {
            syncGate.Release();
        }
    }

    public async Task<IReadOnlyList<AuditEvent>> QueryRecentEventsAsync(int limit = 100, CancellationToken cancellationToken = default)
    {
        if (limit <= 0)
        {
            return Array.Empty<AuditEvent>();
        }

        await syncGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!Directory.Exists(auditDirectory))
            {
                return Array.Empty<AuditEvent>();
            }

            var files = Directory.GetFiles(auditDirectory, "audit-*.jsonl")
                .OrderByDescending(f => f)
                .ToList();

            var results = new List<AuditEvent>();

            foreach (var file in files)
            {
                var lines = await File.ReadAllLinesAsync(file, cancellationToken).ConfigureAwait(false);
                for (var i = lines.Length - 1; i >= 0; i--)
                {
                    var line = lines[i];
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    try
                    {
                        var parsed = JsonSerializer.Deserialize<AuditEvent>(line, JsonOptions);
                        if (parsed != null)
                        {
                            results.Add(parsed);
                            if (results.Count >= limit)
                            {
                                return results;
                            }
                        }
                    }
                    catch (JsonException)
                    {
                    }
                }
            }

            return results;
        }
        finally
        {
            syncGate.Release();
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
}
