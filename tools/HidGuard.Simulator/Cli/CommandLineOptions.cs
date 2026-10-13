using HidGuard.Simulator.Profiles;

namespace HidGuard.Simulator.Cli;

public sealed record CommandLineOptions
{
    public AttackProfileType? Mode { get; init; }
    public bool Verify { get; init; }
    public bool DryRun { get; init; }
    public string? ScriptFile { get; init; }
    public string? Text { get; init; }
    public int Count { get; init; } = 50;
    public int IntervalMs { get; init; } = 4;
    public bool ShowHelp { get; init; }
}
