namespace HidGuard.Core.Configuration;

public sealed record IncidentStoreConfiguration
{
    public bool EnableOptInPayloadCapture { get; init; }
    public int MaxIncidentCount { get; init; } = 1000;
    public int RetentionDays { get; init; } = 30;
    public int MaxPayloadBytes { get; init; } = 4096;
    public string StorageDirectory { get; init; } = string.Empty;

    public void Validate()
    {
        if (MaxIncidentCount <= 0)
        {
            throw new InvalidOperationException("MaxIncidentCount must be greater than zero.");
        }

        if (RetentionDays <= 0)
        {
            throw new InvalidOperationException("RetentionDays must be greater than zero.");
        }

        if (MaxPayloadBytes <= 0 || MaxPayloadBytes > 65536)
        {
            throw new InvalidOperationException("MaxPayloadBytes must be between 1 and 65536 bytes.");
        }
    }
}
