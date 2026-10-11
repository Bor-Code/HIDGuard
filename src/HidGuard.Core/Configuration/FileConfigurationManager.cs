using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HidGuard.Core.Configuration;

public sealed class FileConfigurationManager : IConfigurationManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private static readonly Action<ILogger, Exception?> LogConfigLoadedDelegate =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(1, nameof(FileConfigurationManager)),
            "Configuration successfully loaded.");

    private static readonly Action<ILogger, Exception?> LogConfigCorruptedDelegate =
        LoggerMessage.Define(
            LogLevel.Warning,
            new EventId(2, nameof(FileConfigurationManager)),
            "Configuration file was invalid or corrupted. Reverted to default settings.");

    private static readonly Action<ILogger, Exception?> LogConfigReloadedDelegate =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(3, nameof(FileConfigurationManager)),
            "Configuration file modified on disk. Hot-reload completed.");

    private readonly string filePath;
    private readonly string tempPath;
    private readonly string backupPath;
    private readonly ILogger<FileConfigurationManager> logger;
    private readonly SemaphoreSlim syncGate = new(1, 1);
    private FileSystemWatcher? watcher;
    private DateTime lastReloadTime = DateTime.MinValue;
    private bool disposed;

    public HidGuardConfiguration CurrentConfiguration { get; private set; } = new();

    public event EventHandler<HidGuardConfiguration>? ConfigurationChanged;

    public FileConfigurationManager(string filePath, ILogger<FileConfigurationManager>? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        this.filePath = filePath;
        tempPath = $"{filePath}.tmp";
        backupPath = $"{filePath}.bak";
        this.logger = logger ?? NullLogger<FileConfigurationManager>.Instance;

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        SetupFileWatcher();
    }

    public async Task<HidGuardConfiguration> LoadAsync(CancellationToken cancellationToken = default)
    {
        await syncGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (File.Exists(filePath))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
                    var config = JsonSerializer.Deserialize<HidGuardConfiguration>(json, JsonOptions);
                    if (config != null)
                    {
                        config.Validate();
                        CurrentConfiguration = config;
                        LogConfigLoadedDelegate(logger, null);
                        return CurrentConfiguration;
                    }
                }
                catch (Exception ex) when (ex is JsonException or ArgumentException or InvalidOperationException)
                {
                    LogConfigCorruptedDelegate(logger, ex);
                }
            }

            if (File.Exists(backupPath))
            {
                try
                {
                    var backupJson = await File.ReadAllTextAsync(backupPath, cancellationToken).ConfigureAwait(false);
                    var backupConfig = JsonSerializer.Deserialize<HidGuardConfiguration>(backupJson, JsonOptions);
                    if (backupConfig != null)
                    {
                        backupConfig.Validate();
                        CurrentConfiguration = backupConfig;
                        return CurrentConfiguration;
                    }
                }
                catch (Exception)
                {
                }
            }

            CurrentConfiguration = new HidGuardConfiguration();
            await SaveInternalAsync(CurrentConfiguration, cancellationToken).ConfigureAwait(false);
            return CurrentConfiguration;
        }
        finally
        {
            syncGate.Release();
        }
    }

    public async Task SaveAsync(HidGuardConfiguration configuration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        configuration.Validate();

        await syncGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await SaveInternalAsync(configuration, cancellationToken).ConfigureAwait(false);
            CurrentConfiguration = configuration;
            ConfigurationChanged?.Invoke(this, CurrentConfiguration);
        }
        finally
        {
            syncGate.Release();
        }
    }

    private async Task SaveInternalAsync(HidGuardConfiguration configuration, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(configuration, JsonOptions);
        await File.WriteAllTextAsync(tempPath, json, cancellationToken).ConfigureAwait(false);

        if (File.Exists(filePath))
        {
            File.Copy(filePath, backupPath, overwrite: true);
        }

        File.Move(tempPath, filePath, overwrite: true);
    }

    private void SetupFileWatcher()
    {
        var directory = Path.GetDirectoryName(filePath);
        var fileName = Path.GetFileName(filePath);

        if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileName))
        {
            return;
        }

        try
        {
            watcher = new FileSystemWatcher(directory, fileName)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                EnableRaisingEvents = true
            };

            watcher.Changed += OnFileChanged;
        }
        catch (Exception)
        {
        }
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        var now = DateTime.UtcNow;
        if ((now - lastReloadTime).TotalMilliseconds < 500)
        {
            return;
        }
        lastReloadTime = now;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(200).ConfigureAwait(false);
                var reloaded = await LoadAsync().ConfigureAwait(false);
                LogConfigReloadedDelegate(logger, null);
                ConfigurationChanged?.Invoke(this, reloaded);
            }
            catch (Exception)
            {
            }
        }, CancellationToken.None);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        if (watcher != null)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Changed -= OnFileChanged;
            watcher.Dispose();
            watcher = null;
        }

        syncGate.Dispose();
        disposed = true;
    }
}
