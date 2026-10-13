namespace HidGuard.Simulator.Verification;

public sealed record VerificationScenarioResult
{
    public required string ScenarioName { get; init; }
    public required bool Success { get; init; }
    public required string Description { get; init; }
    public TimeSpan Duration { get; init; }
    public string? FailureReason { get; init; }
    public IReadOnlyDictionary<string, string> Metrics { get; init; } = new Dictionary<string, string>();
}
