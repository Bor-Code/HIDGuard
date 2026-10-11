namespace HidGuard.Core.Timing;

public sealed record TimingThresholds
{
    public int WindowSize { get; init; } = 30;
    public double HumanFeasibilityFloorMs { get; init; } = 25.0;
    public double SubHumanRatioThreshold { get; init; } = 0.35;
    public double CoefficientOfVariationThreshold { get; init; } = 0.18;
    public double MinimumIntervalThresholdMs { get; init; } = 8.0;
    public int BurstLengthThreshold { get; init; } = 15;
    public double BurstPauseFloorMs { get; init; } = 70.0;
    public int ObservationWindowKeyCount { get; init; } = 60;
    public double ObservationWindowScoreMultiplier { get; init; } = 1.25;
    public double SuspicionScoreThreshold { get; init; } = 65.0;

    public void Validate()
    {
        if (WindowSize < 5 || WindowSize > 500)
        {
            throw new ArgumentOutOfRangeException(nameof(WindowSize), "WindowSize must be between 5 and 500.");
        }

        if (HumanFeasibilityFloorMs <= 0.0 || HumanFeasibilityFloorMs > 200.0)
        {
            throw new ArgumentOutOfRangeException(nameof(HumanFeasibilityFloorMs), "HumanFeasibilityFloorMs must be between 0 and 200.");
        }

        if (SubHumanRatioThreshold <= 0.0 || SubHumanRatioThreshold > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(SubHumanRatioThreshold), "SubHumanRatioThreshold must be between 0 and 1.");
        }

        if (CoefficientOfVariationThreshold <= 0.0 || CoefficientOfVariationThreshold > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(CoefficientOfVariationThreshold), "CoefficientOfVariationThreshold must be between 0 and 1.");
        }

        if (MinimumIntervalThresholdMs <= 0.0 || MinimumIntervalThresholdMs > HumanFeasibilityFloorMs)
        {
            throw new ArgumentOutOfRangeException(nameof(MinimumIntervalThresholdMs), "MinimumIntervalThresholdMs must be positive and less than or equal to HumanFeasibilityFloorMs.");
        }

        if (BurstLengthThreshold < 3 || BurstLengthThreshold > 200)
        {
            throw new ArgumentOutOfRangeException(nameof(BurstLengthThreshold), "BurstLengthThreshold must be between 3 and 200.");
        }

        if (BurstPauseFloorMs <= HumanFeasibilityFloorMs || BurstPauseFloorMs > 500.0)
        {
            throw new ArgumentOutOfRangeException(nameof(BurstPauseFloorMs), "BurstPauseFloorMs must be greater than HumanFeasibilityFloorMs and at most 500.");
        }

        if (ObservationWindowKeyCount < 10 || ObservationWindowKeyCount > 1000)
        {
            throw new ArgumentOutOfRangeException(nameof(ObservationWindowKeyCount), "ObservationWindowKeyCount must be between 10 and 1000.");
        }

        if (ObservationWindowScoreMultiplier < 1.0 || ObservationWindowScoreMultiplier > 3.0)
        {
            throw new ArgumentOutOfRangeException(nameof(ObservationWindowScoreMultiplier), "ObservationWindowScoreMultiplier must be between 1.0 and 3.0.");
        }

        if (SuspicionScoreThreshold <= 0.0 || SuspicionScoreThreshold > 100.0)
        {
            throw new ArgumentOutOfRangeException(nameof(SuspicionScoreThreshold), "SuspicionScoreThreshold must be between 0 and 100.");
        }
    }
}
