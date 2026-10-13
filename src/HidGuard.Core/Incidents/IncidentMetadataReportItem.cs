namespace HidGuard.Core.Incidents;

public sealed record IncidentMetadataReportItem
{
    public Guid IncidentId { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public string DeviceId { get; init; } = string.Empty;
    public double AnomalyScore { get; init; }
    public string TriggeredRule { get; init; } = string.Empty;
    public string CountermeasureAction { get; init; } = string.Empty;
    public bool HasEncryptedPayload { get; init; }
    public int EncryptedPayloadSizeBytes { get; init; }
    public DateTimeOffset RetentionExpiresAt { get; init; }
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();
}
