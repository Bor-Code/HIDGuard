namespace HidGuard.Core.Configuration;

public sealed record UiConfiguration
{
    private static readonly HashSet<string> ValidThemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Dark", "Light", "System", "RedBlack", "GreenBlack", "BlueBlack"
    };

    public bool StartWithWindows { get; init; }
    public bool StartMinimizedToTray { get; init; }
    public bool NotifyOnAllowedDevices { get; init; }
    public bool NotifyOnBlockedDevices { get; init; } = true;
    public string Theme { get; init; } = "RedBlack";

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Theme) || !ValidThemes.Contains(Theme))
        {
            throw new ArgumentException("Theme must be one of Dark, Light, System, RedBlack, GreenBlack, BlueBlack.", nameof(Theme));
        }
    }
}
