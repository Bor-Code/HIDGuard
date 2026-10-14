namespace HidGuard.Core.Configuration;

public sealed record IncidentStoreConfiguration
{
    public const int MaxCaptureWindowKeystrokes = 1024;

    public bool EnableOptInPayloadCapture { get; init; }
    public int CaptureWindowKeystrokes { get; init; } = 64;
    public int MaxIncidentCount { get; init; } = 1000;
    public int RetentionDays { get; init; } = 30;
    public int MaxPayloadBytes { get; init; } = 4096;
    public int PurgeIntervalMinutes { get; init; } = 60;
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

        if (CaptureWindowKeystrokes <= 0 || CaptureWindowKeystrokes > MaxCaptureWindowKeystrokes)
        {
            throw new InvalidOperationException($"CaptureWindowKeystrokes must be between 1 and {MaxCaptureWindowKeystrokes}.");
        }

        if (PurgeIntervalMinutes <= 0)
        {
            throw new InvalidOperationException("PurgeIntervalMinutes must be greater than zero.");
        }
    }
}
