namespace HidGuard.Core.Incidents;

public sealed record IncidentRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public string DeviceId { get; init; } = string.Empty;
    public double AnomalyScore { get; init; }
    public string TriggeredRule { get; init; } = string.Empty;
    public string CountermeasureAction { get; init; } = string.Empty;
    public bool HasEncryptedPayload { get; init; }
    public byte[]? EncryptedPayload { get; init; }
    public int? UnencryptedPayloadLength { get; init; }
    public DateTimeOffset RetentionExpiresAt { get; init; }
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();
}
