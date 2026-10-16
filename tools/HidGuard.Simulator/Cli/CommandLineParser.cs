using HidGuard.Simulator.Profiles;

namespace HidGuard.Simulator.Cli;

public static class CommandLineParser
{
    public static CommandLineOptions Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        AttackProfileType? mode = null;
        var verify = false;
        var dryRun = false;
        string? scriptFile = null;
        string? text = null;
        var count = 50;
        var intervalMs = 4;
        var showHelp = false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if (arg is "--help" or "-h" or "/?")
            {
                showHelp = true;
            }
            else if (arg is "--verify" or "-v")
            {
                verify = true;
            }
            else if (arg is "--dry-run" or "-d")
            {
                dryRun = true;
            }
            else if (arg is "--mode" or "-m" && i + 1 < args.Length)
            {
                var modeStr = args[++i];
                mode = modeStr.ToLowerInvariant() switch
                {
                    "burst" => AttackProfileType.Burst,
                    "jitter" => AttackProfileType.Jitter,
                    "human" => AttackProfileType.HumanTyping,
                    "ducky" => AttackProfileType.DuckyScript,
                    "stealth" or "stealth-micro-burst" or "microburst" => AttackProfileType.StealthMicroBurst,
                    "staged" or "staged-payload" or "multi-stage" => AttackProfileType.StagedPayload,
                    _ => null
                };
            }
            else if (arg is "--file" or "-f" && i + 1 < args.Length)
            {
                scriptFile = args[++i];
            }
            else if (arg is "--text" or "-t" && i + 1 < args.Length)
            {
                text = args[++i];
            }
            else if (arg is "--count" or "-c" && i + 1 < args.Length)
            {
                if (int.TryParse(args[++i], out var parsedCount) && parsedCount > 0)
                {
                    count = parsedCount;
                }
            }
            else if (arg is "--interval" or "-i" && i + 1 < args.Length)
            {
                if (int.TryParse(args[++i], out var parsedInterval) && parsedInterval > 0)
                {
                    intervalMs = parsedInterval;
                }
            }
        }

        return new CommandLineOptions
        {
            Mode = mode,
            Verify = verify,
            DryRun = dryRun,
            ScriptFile = scriptFile,
            Text = text,
            Count = count,
            IntervalMs = intervalMs,
            ShowHelp = showHelp
        };
    }

    public static string GetHelpText()
    {
        return
            "HIDGuard Attack Simulator and End-to-End Verification Tool\n" +
            "Usage: HidGuard.Simulator [options]\n\n" +
            "Options:\n" +
            "  --mode, -m <burst|jitter|human|ducky|stealth|staged>  Select attack profile to execute\n" +
            "  --verify, -v                                           Run automated end-to-end verification suite\n" +
            "  --dry-run, -d                                          Execute simulated injection without Win32 SendInput\n" +
            "  --file, -f <path>                                      DuckyScript file path for ducky mode\n" +
            "  --text, -t <string>                                    Custom text payload for burst, human, stealth, or staged mode\n" +
            "  --count, -c <number>                                   Number of keystrokes to inject (default: 50)\n" +
            "  --interval, -i <ms>                                    Inter-keystroke interval in milliseconds (default: 4)\n" +
            "  --help, -h                                             Show this help message\n";
    }
}
