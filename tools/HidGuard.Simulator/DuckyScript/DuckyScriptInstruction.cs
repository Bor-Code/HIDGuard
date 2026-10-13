namespace HidGuard.Simulator.DuckyScript;

public sealed record DuckyScriptInstruction
{
    public DuckyScriptCommandType Type { get; init; }
    public TimeSpan Duration { get; init; }
    public string Text { get; init; } = string.Empty;
    public ushort VirtualKey { get; init; }
    public bool Shift { get; init; }
    public IReadOnlyList<ushort> CombinationKeys { get; init; } = Array.Empty<ushort>();
    public int RepeatCount { get; init; }

    public static DuckyScriptInstruction CreateDelay(TimeSpan duration) => new()
    {
        Type = DuckyScriptCommandType.Delay,
        Duration = duration
    };

    public static DuckyScriptInstruction CreateDefaultDelay(TimeSpan duration) => new()
    {
        Type = DuckyScriptCommandType.DefaultDelay,
        Duration = duration
    };

    public static DuckyScriptInstruction CreateString(string text) => new()
    {
        Type = DuckyScriptCommandType.TextPayload,
        Text = text ?? string.Empty
    };

    public static DuckyScriptInstruction CreateStringLine(string text) => new()
    {
        Type = DuckyScriptCommandType.TextLinePayload,
        Text = text ?? string.Empty
    };

    public static DuckyScriptInstruction CreateKeyPress(ushort virtualKey, bool shift = false) => new()
    {
        Type = DuckyScriptCommandType.KeyPress,
        VirtualKey = virtualKey,
        Shift = shift
    };

    public static DuckyScriptInstruction CreateKeyCombination(IReadOnlyList<ushort> keys) => new()
    {
        Type = DuckyScriptCommandType.KeyCombination,
        CombinationKeys = keys ?? Array.Empty<ushort>()
    };

    public static DuckyScriptInstruction CreateRepeat(int count) => new()
    {
        Type = DuckyScriptCommandType.Repeat,
        RepeatCount = Math.Max(1, count)
    };
}
