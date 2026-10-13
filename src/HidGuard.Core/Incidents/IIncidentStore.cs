namespace HidGuard.Core.Incidents;

public interface IIncidentStore : IDisposable
{
    Task<IncidentRecord> RecordIncidentAsync(CreateIncidentRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IncidentRecord>> GetIncidentsAsync(int limit = 100, CancellationToken cancellationToken = default);
    Task<IncidentRecord?> GetIncidentByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<int> PurgeExpiredIncidentsAsync(CancellationToken cancellationToken = default);
    Task ExportMetadataReportAsync(Stream outputStream, IncidentExportFormat format = IncidentExportFormat.Json, CancellationToken cancellationToken = default);
    Task<byte[]?> DecryptPayloadAsync(Guid incidentId, CancellationToken cancellationToken = default);
    event EventHandler<IncidentRecord>? IncidentRecorded;
}
