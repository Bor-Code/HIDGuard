namespace HidGuard.App.ViewModels;

public sealed record LiveIntervalSample
{
    public double IntervalMs { get; init; }
    public double BarHeight { get; init; }
    public string Color { get; init; } = "#06B6D4";
    public bool IsAnomaly { get; init; }
}
