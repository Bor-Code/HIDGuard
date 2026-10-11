namespace HidGuard.Core.Scoring;

public sealed record DetectionSignal(
    DetectionSignalType Type,
    double ObservedValue,
    double ThresholdValue,
    double ScoreContribution,
    string Description);
