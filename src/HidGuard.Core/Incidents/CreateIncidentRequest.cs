namespace HidGuard.Core.Incidents;

public sealed record CreateIncidentRequest
{
    public string DeviceId { get; init; } = string.Empty;
    public double AnomalyScore { get; init; }
    public string TriggeredRule { get; init; } = string.Empty;
    public string CountermeasureAction { get; init; } = string.Empty;
    public byte[]? RawPayload { get; init; }
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}
