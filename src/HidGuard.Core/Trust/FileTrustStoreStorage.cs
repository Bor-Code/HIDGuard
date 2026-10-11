namespace HidGuard.Core.Trust;

public sealed class FileTrustStoreStorage : ITrustStoreStorage
{
    private readonly string filePath;
    private readonly string backupPath;
    private readonly string tempPath;

    public FileTrustStoreStorage(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        this.filePath = filePath;
        backupPath = $"{filePath}.bak";
        tempPath = $"{filePath}.tmp";

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    public async Task<string?> ReadRawAsync(CancellationToken cancellationToken = default)
    {
        if (File.Exists(filePath))
        {
            try
            {
                return await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
            }
            catch (IOException)
            {
                if (File.Exists(backupPath))
                {
                    return await File.ReadAllTextAsync(backupPath, cancellationToken).ConfigureAwait(false);
                }
                throw;
            }
        }

        if (File.Exists(backupPath))
        {
            return await File.ReadAllTextAsync(backupPath, cancellationToken).ConfigureAwait(false);
        }

        return null;
    }

    public async Task WriteRawAsync(string content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        await File.WriteAllTextAsync(tempPath, content, cancellationToken).ConfigureAwait(false);

        if (File.Exists(filePath))
        {
            File.Copy(filePath, backupPath, overwrite: true);
        }

        File.Move(tempPath, filePath, overwrite: true);
    }
}
