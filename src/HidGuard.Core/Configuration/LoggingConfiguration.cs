namespace HidGuard.Core.Configuration;

public sealed record LoggingConfiguration
{
    private static readonly HashSet<string> ValidLevels = new(StringComparer.OrdinalIgnoreCase)
    {
        "Verbose", "Debug", "Information", "Warning", "Error", "Fatal"
    };

    public string MinimumLevel { get; init; } = "Information";
    public string LogDirectory { get; init; } = string.Empty;
    public int RetainedFileCountLimit { get; init; } = 30;
    public long FileSizeLimitBytes { get; init; } = 52_428_800;
    public bool EnableConsoleLogging { get; init; } = true;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(MinimumLevel) || !ValidLevels.Contains(MinimumLevel))
        {
            throw new ArgumentException("MinimumLevel must be one of Verbose, Debug, Information, Warning, Error, Fatal.", nameof(MinimumLevel));
        }

        if (RetainedFileCountLimit is < 1 or > 365)
        {
            throw new ArgumentOutOfRangeException(nameof(RetainedFileCountLimit), "RetainedFileCountLimit must be between 1 and 365.");
        }

        if (FileSizeLimitBytes is < 1024 or > 1_073_741_824)
        {
            throw new ArgumentOutOfRangeException(nameof(FileSizeLimitBytes), "FileSizeLimitBytes must be between 1KB and 1GB.");
        }
    }
}
