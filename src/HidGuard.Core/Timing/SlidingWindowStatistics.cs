namespace HidGuard.Core.Timing;

public sealed record SlidingWindowStatistics
{
    public int SampleCount { get; init; }
    public double MeanIntervalMs { get; init; }
    public double MedianIntervalMs { get; init; }
    public double VarianceMs { get; init; }
    public double StandardDeviationMs { get; init; }
    public double CoefficientOfVariation { get; init; }
    public double MinimumIntervalMs { get; init; }
    public int BurstLength { get; init; }
    public double SubHumanIntervalRatio { get; init; }

    public static SlidingWindowStatistics Empty => new()
    {
        SampleCount = 0,
        MeanIntervalMs = 0.0,
        MedianIntervalMs = 0.0,
        VarianceMs = 0.0,
        StandardDeviationMs = 0.0,
        CoefficientOfVariation = 0.0,
        MinimumIntervalMs = 0.0,
        BurstLength = 0,
        SubHumanIntervalRatio = 0.0
    };
}
