namespace HidGuard.Core.Trust;

public sealed record TrustStoreData
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public DateTimeOffset LastUpdated { get; init; } = DateTimeOffset.UtcNow;
    public IReadOnlyList<TrustRecord> Records { get; init; } = Array.Empty<TrustRecord>();
}
