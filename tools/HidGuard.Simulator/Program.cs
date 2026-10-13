using HidGuard.Simulator.Cli;
using HidGuard.Simulator.Injection;
using HidGuard.Simulator.Profiles;
using HidGuard.Simulator.Verification;

namespace HidGuard.Simulator;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var options = CommandLineParser.Parse(args);

        if (options.ShowHelp)
        {
            Console.WriteLine(CommandLineParser.GetHelpText());
            return 0;
        }

        if (options.Verify)
        {
            Console.WriteLine("Executing automated end-to-end verification suite...");
            var runner = new VerificationRunner();
            var report = await runner.RunAllScenariosAsync().ConfigureAwait(false);
            Console.WriteLine(report.FormatReport());
            return report.AllPassed ? 0 : 1;
        }

        if (options.Mode.HasValue)
        {
            return await ExecuteCliModeAsync(options).ConfigureAwait(false);
        }

        return await RunInteractiveMenuAsync().ConfigureAwait(false);
    }

    private static async Task<int> ExecuteCliModeAsync(CommandLineOptions options)
    {
        IKeystrokeInjector injector = options.DryRun
            ? new SimulatedKeystrokeInjector(executeRealDelays: true)
            : new Win32KeystrokeInjector();

        var profile = CreateProfile(options);
        if (profile == null)
        {
            Console.Error.WriteLine("Error: Unable to configure attack profile.");
            return 1;
        }

        var executionTarget = options.DryRun ? "Simulated Dry-Run" : "Live Windows Desktop (Win32 SendInput)";
        Console.WriteLine($"Starting attack profile: {profile.Name} [{executionTarget}]");

        if (!options.DryRun)
        {
            Console.WriteLine("Warning: Keystrokes will be dispatched to the active window in 3 seconds. Press Ctrl+C to cancel.");
            await Task.Delay(3000).ConfigureAwait(false);
        }

        await profile.ExecuteAsync(injector).ConfigureAwait(false);
        Console.WriteLine($"Execution completed: {profile.Name}");
        return 0;
    }

    private static IAttackProfile? CreateProfile(CommandLineOptions options)
    {
        return options.Mode switch
        {
            AttackProfileType.Burst => new BurstAttackProfile(
                options.Count,
                TimeSpan.FromMilliseconds(options.IntervalMs),
                options.Text),

            AttackProfileType.Jitter => new JitterAttackProfile(
                options.Count,
                TimeSpan.FromMilliseconds(options.IntervalMs),
                0.5,
                options.Text),

            AttackProfileType.HumanTyping => new HumanTypingProfile(
                options.Text),

            AttackProfileType.DuckyScript => LoadDuckyProfile(options.ScriptFile),

            _ => null
        };
    }

    private static DuckyScriptAttackProfile LoadDuckyProfile(string? filePath)
    {
        if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
        {
            var content = File.ReadAllText(filePath);
            return new DuckyScriptAttackProfile(content);
        }

        var demoScript = "STRING powershell -NoP -NonI -W Hidden -Exec Bypass \"Write-Host HIDGuard_Attack_Simulated\"\nENTER";
        return new DuckyScriptAttackProfile(demoScript);
    }

    private static async Task<int> RunInteractiveMenuAsync()
    {
        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("================================================================================");
            Console.WriteLine("HIDGuard Security Research & Attack Simulator");
            Console.WriteLine("================================================================================");
            Console.WriteLine("[1] Run Burst Keystroke Attack (<10ms inter-keystroke interval)");
            Console.WriteLine("[2] Run Low-Jitter Microcontroller Loop Attack (Uniform interval)");
            Console.WriteLine("[3] Run Realistic Human Typing Emulation (Gaussian variance)");
            Console.WriteLine("[4] Execute DuckyScript Payload (Sample payload)");
            Console.WriteLine("[5] Run Full Automated End-to-End Verification Suite");
            Console.WriteLine("[0] Exit");
            Console.WriteLine("--------------------------------------------------------------------------------");
            Console.Write("Select an option [0-5]: ");

            var input = Console.ReadLine()?.Trim();
            Console.WriteLine();

            if (input == "0")
            {
                return 0;
            }

            if (input == "5")
            {
                var runner = new VerificationRunner();
                var report = await runner.RunAllScenariosAsync().ConfigureAwait(false);
                Console.WriteLine(report.FormatReport());
                continue;
            }

            IAttackProfile? profile = input switch
            {
                "1" => new BurstAttackProfile(keystrokeCount: 50, interval: TimeSpan.FromMilliseconds(4)),
                "2" => new JitterAttackProfile(keystrokeCount: 50, baseInterval: TimeSpan.FromMilliseconds(15), maxJitterMilliseconds: 0.2),
                "3" => new HumanTypingProfile(),
                "4" => LoadDuckyProfile(null),
                _ => null
            };

            if (profile == null)
            {
                Console.WriteLine("Invalid selection. Please choose an option between 0 and 5.");
                continue;
            }

            Console.Write("Execute in dry-run mode (Y/n)? ");
            var dryRunInput = Console.ReadLine()?.Trim().ToLowerInvariant();
            var dryRun = dryRunInput != "n" && dryRunInput != "no";

            IKeystrokeInjector injector = dryRun
                ? new SimulatedKeystrokeInjector(executeRealDelays: true)
                : new Win32KeystrokeInjector();

            var modeName = dryRun ? "Simulated Dry-Run" : "Live Windows Desktop (Win32 SendInput)";
            Console.WriteLine($"Executing {profile.Name} [{modeName}]...");

            if (!dryRun)
            {
                Console.WriteLine("Warning: Injection starts in 3 seconds. Focus desired test application.");
                await Task.Delay(3000).ConfigureAwait(false);
            }

            await profile.ExecuteAsync(injector).ConfigureAwait(false);
            Console.WriteLine("Execution finished successfully.");
        }
    }
}
