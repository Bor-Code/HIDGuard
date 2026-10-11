namespace HidGuard.Core.Domain;

public readonly record struct KeystrokeInterval
{
    public TimeSpan Duration { get; }
    public double Milliseconds => Duration.TotalMilliseconds;

    public KeystrokeInterval(TimeSpan duration)
    {
        Duration = duration < TimeSpan.Zero ? TimeSpan.Zero : duration;
    }

    public static KeystrokeInterval FromMilliseconds(double milliseconds)
    {
        return new KeystrokeInterval(TimeSpan.FromMilliseconds(Math.Max(0.0, milliseconds)));
    }
}
