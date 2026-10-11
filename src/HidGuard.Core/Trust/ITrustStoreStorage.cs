namespace HidGuard.Core.Trust;

public interface ITrustStoreStorage
{
    Task<string?> ReadRawAsync(CancellationToken cancellationToken = default);
    Task WriteRawAsync(string content, CancellationToken cancellationToken = default);
}
