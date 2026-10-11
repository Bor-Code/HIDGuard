namespace HidGuard.Core.Scoring;

public enum DetectionSignalType
{
    SubHumanIntervalRate,
    LowCoefficientOfVariation,
    ExtremeMinimumInterval,
    SustainedHighSpeedBurst,
    ObservationWindowMultiplierApplied
}
