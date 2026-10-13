namespace HidGuard.Simulator.DuckyScript;

public sealed class DuckyScriptParser
{
    private static readonly string[] LineSeparators = ["\r\n", "\r", "\n"];

    public static IReadOnlyList<DuckyScriptInstruction> Parse(string scriptText)
    {
        ArgumentNullException.ThrowIfNull(scriptText);

        var lines = scriptText.Split(LineSeparators, StringSplitOptions.None);
        return ParseLines(lines);
    }

    public static IReadOnlyList<DuckyScriptInstruction> ParseLines(IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var instructions = new List<DuckyScriptInstruction>();
        DuckyScriptInstruction? lastInstruction = null;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (line.StartsWith("REM", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var instruction = ParseLine(line, lastInstruction);
            if (instruction != null)
            {
                if (instruction.Type == DuckyScriptCommandType.Repeat && lastInstruction != null)
                {
                    for (var r = 0; r < instruction.RepeatCount; r++)
                    {
                        instructions.Add(lastInstruction);
                    }
                }
                else
                {
                    instructions.Add(instruction);
                    lastInstruction = instruction;
                }
            }
        }

        return instructions;
    }

    private static DuckyScriptInstruction? ParseLine(string line, DuckyScriptInstruction? lastInstruction)
    {
        var firstSpaceIndex = line.IndexOf(' ', StringComparison.Ordinal);
        var commandToken = firstSpaceIndex > 0 ? line[..firstSpaceIndex].Trim() : line;
        var argument = firstSpaceIndex > 0 ? line[(firstSpaceIndex + 1)..].Trim() : string.Empty;

        if (commandToken.Equals("STRING", StringComparison.OrdinalIgnoreCase))
        {
            var payload = line.Length > 6 ? line[7..] : string.Empty;
            return DuckyScriptInstruction.CreateString(payload);
        }

        if (commandToken.Equals("STRINGLN", StringComparison.OrdinalIgnoreCase))
        {
            var payload = line.Length > 8 ? line[9..] : string.Empty;
            return DuckyScriptInstruction.CreateStringLine(payload);
        }

        if (commandToken.Equals("DELAY", StringComparison.OrdinalIgnoreCase))
        {
            if (int.TryParse(argument, out var ms) && ms >= 0)
            {
                return DuckyScriptInstruction.CreateDelay(TimeSpan.FromMilliseconds(ms));
            }
            throw new FormatException($"Invalid DELAY argument: '{argument}'");
        }

        if (commandToken.Equals("DEFAULT_DELAY", StringComparison.OrdinalIgnoreCase) ||
            commandToken.Equals("DEFAULTDELAY", StringComparison.OrdinalIgnoreCase))
        {
            if (int.TryParse(argument, out var ms) && ms >= 0)
            {
                return DuckyScriptInstruction.CreateDefaultDelay(TimeSpan.FromMilliseconds(ms));
            }
            throw new FormatException($"Invalid DEFAULT_DELAY argument: '{argument}'");
        }

        if (commandToken.Equals("REPEAT", StringComparison.OrdinalIgnoreCase))
        {
            if (int.TryParse(argument, out var count) && count > 0)
            {
                return DuckyScriptInstruction.CreateRepeat(count);
            }
            return DuckyScriptInstruction.CreateRepeat(1);
        }

        var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 1)
        {
            if (VirtualKeyMap.TryResolveKeyName(tokens[0], out var singleKey))
            {
                return DuckyScriptInstruction.CreateKeyPress(singleKey);
            }
            throw new InvalidOperationException($"Unrecognized DuckyScript command or key: '{tokens[0]}'");
        }

        var comboKeys = new List<ushort>();
        foreach (var token in tokens)
        {
            if (VirtualKeyMap.TryResolveKeyName(token, out var resolvedKey))
            {
                comboKeys.Add(resolvedKey);
            }
            else
            {
                throw new InvalidOperationException($"Unrecognized key in combination: '{token}'");
            }
        }

        return DuckyScriptInstruction.CreateKeyCombination(comboKeys);
    }
}
