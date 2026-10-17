using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows.Input;
using HidGuard.App.Mvvm;
using HidGuard.Simulator.DuckyScript;
using HidGuard.Simulator.Injection;
using HidGuard.Simulator.Profiles;
using HidGuard.Simulator.Verification;

namespace HidGuard.App.ViewModels;

public sealed class SimulatorProfileItem
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string BadgeColor { get; init; } = "#EF4444";
}

public sealed class VerificationScenarioItem : ObservableObject
{
    private string status = "Pending";
    private string statusColor = "#9CA3AF";
    private string duration = "-";
    private string details = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Status
    {
        get => status;
        set => SetProperty(ref status, value);
    }

    public string StatusColor
    {
        get => statusColor;
        set => SetProperty(ref statusColor, value);
    }

    public string Duration
    {
        get => duration;
        set => SetProperty(ref duration, value);
    }

    public string Details
    {
        get => details;
        set => SetProperty(ref details, value);
    }
}

public sealed class SimulatorViewModel : ObservableObject, IDisposable
{
    private static readonly SimulatorProfileItem[] Profiles =
    [
        new SimulatorProfileItem
        {
            Id = "burst",
            Title = "Rapid Burst Injection",
            Description = "High-velocity keystroke stream at sub-10ms intervals mimicking Hak5 Rubber Ducky and BadUSB hardware.",
            Category = "High Velocity",
            BadgeColor = "#EF4444"
        },
        new SimulatorProfileItem
        {
            Id = "jitter",
            Title = "Low-Jitter Uniformity Loop",
            Description = "Sub-15ms keystroke injection with ultra-low microsecond variance simulating Arduino/Digispark firmware loops.",
            Category = "Microcontroller",
            BadgeColor = "#EF4444"
        },
        new SimulatorProfileItem
        {
            Id = "human",
            Title = "Realistic Human Typing",
            Description = "Gaussian distributed timing (mean 130ms, stddev 35ms) used as a baseline to verify benign typing passthrough.",
            Category = "Benign Baseline",
            BadgeColor = "#10B981"
        },
        new SimulatorProfileItem
        {
            Id = "stealth",
            Title = "Stealth Mixed-Cadence Micro-Burst",
            Description = "Advanced evasion profile interleaving benign human-rate decoy typing with sudden high-velocity micro-bursts.",
            Category = "Stealth Evasion",
            BadgeColor = "#F59E0B"
        },
        new SimulatorProfileItem
        {
            Id = "staged",
            Title = "Multi-Stage Exploit Pipeline",
            Description = "Simulates real-world exploit delivery: Run dialog invocation (Win+R), delay pacing, and staged command injection.",
            Category = "Complex Exploit",
            BadgeColor = "#F59E0B"
        },
        new SimulatorProfileItem
        {
            Id = "ducky",
            Title = "Custom DuckyScript Payload",
            Description = "Execute custom Hak5 DuckyScript 1.0/2.0 payloads with syntax support for delays, combinations, and strings.",
            Category = "Scripting",
            BadgeColor = "#3B82F6"
        },
        new SimulatorProfileItem
        {
            Id = "verify",
            Title = "Automated E2E Verification Suite",
            Description = "Run the full 7-scenario automated verification suite testing timing, suppression, locking, and privacy invariants.",
            Category = "Compliance",
            BadgeColor = "#06B6D4"
        }
    ];

    private static readonly string DefaultScript =
        "REM Hak5 DuckyScript Sample Payload" + Environment.NewLine +
        "DELAY 400" + Environment.NewLine +
        "GUI r" + Environment.NewLine +
        "DELAY 250" + Environment.NewLine +
        "STRING notepad.exe" + Environment.NewLine +
        "ENTER" + Environment.NewLine +
        "DELAY 600" + Environment.NewLine +
        "STRING HIDGuard Security Verification Active." + Environment.NewLine +
        "ENTER";

    private readonly IVerificationRunner verificationRunner;
    private CancellationTokenSource? activeExecutionCts;

    private SimulatorProfileItem selectedProfile;
    private bool isDryRun = true;
    private int countdownSeconds = 3;
    private int keystrokeCount = 50;
    private int intervalMs = 4;
    private string duckyScriptText = DefaultScript;
    private bool isExecuting;
    private string statusText = "Ready";
    private string statusColor = "#10B981";
    private string outputConsole = "HIDGuard Security Research & Attack Simulator initialized." + Environment.NewLine;
    private bool hasVerificationResults;

    public IReadOnlyList<SimulatorProfileItem> AvailableProfiles { get; } = Profiles;

    public SimulatorProfileItem SelectedProfile
    {
        get => selectedProfile;
        set
        {
            if (SetProperty(ref selectedProfile, value))
            {
                OnPropertyChanged(nameof(IsDuckyScriptSelected));
                OnPropertyChanged(nameof(IsVerificationSelected));
                OnPropertyChanged(nameof(IsStandardProfileSelected));
            }
        }
    }

    public bool IsDryRun
    {
        get => isDryRun;
        set => SetProperty(ref isDryRun, value);
    }

    public int CountdownSeconds
    {
        get => countdownSeconds;
        set => SetProperty(ref countdownSeconds, Math.Max(1, Math.Min(10, value)));
    }

    public int KeystrokeCount
    {
        get => keystrokeCount;
        set => SetProperty(ref keystrokeCount, Math.Max(1, Math.Min(1000, value)));
    }

    public int IntervalMs
    {
        get => intervalMs;
        set => SetProperty(ref intervalMs, Math.Max(1, Math.Min(500, value)));
    }

    public string DuckyScriptText
    {
        get => duckyScriptText;
        set => SetProperty(ref duckyScriptText, value);
    }

    public bool IsDuckyScriptSelected => SelectedProfile?.Id == "ducky";
    public bool IsVerificationSelected => SelectedProfile?.Id == "verify";
    public bool IsStandardProfileSelected => SelectedProfile?.Id is "burst" or "jitter" or "human";

    public bool IsExecuting
    {
        get => isExecuting;
        set
        {
            if (SetProperty(ref isExecuting, value))
            {
                OnPropertyChanged(nameof(CanExecute));
                OnPropertyChanged(nameof(CanCancel));
            }
        }
    }

    public bool CanExecute => !IsExecuting;
    public bool CanCancel => IsExecuting;

    public string StatusText
    {
        get => statusText;
        set => SetProperty(ref statusText, value);
    }

    public string StatusColor
    {
        get => statusColor;
        set => SetProperty(ref statusColor, value);
    }

    public string OutputConsole
    {
        get => outputConsole;
        set => SetProperty(ref outputConsole, value);
    }

    public bool HasVerificationResults
    {
        get => hasVerificationResults;
        set => SetProperty(ref hasVerificationResults, value);
    }

    public ObservableCollection<VerificationScenarioItem> VerificationScenarios { get; } = [];

    public ICommand ExecuteCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand ClearConsoleCommand { get; }
    public ICommand LoadPresetCommand { get; }

    public SimulatorViewModel(IVerificationRunner? verificationRunner = null)
    {
        this.verificationRunner = verificationRunner ?? new VerificationRunner();
        selectedProfile = Profiles[0];

        ExecuteCommand = new RelayCommand(async () => await ExecuteSimulationAsync().ConfigureAwait(true), () => CanExecute);
        CancelCommand = new RelayCommand(CancelExecution, () => CanCancel);
        ClearConsoleCommand = new RelayCommand(ClearConsole);
        LoadPresetCommand = new RelayCommand<string>(LoadPreset);
    }

    public void LoadPreset(string? presetName)
    {
        switch (presetName)
        {
            case "Hello":
                DuckyScriptText =
                    "REM Simple typing demonstration" + Environment.NewLine +
                    "DELAY 500" + Environment.NewLine +
                    "STRING Hello from HIDGuard Attack Simulator!" + Environment.NewLine +
                    "ENTER";
                break;
            case "RunCmd":
                DuckyScriptText =
                    "REM Automated Command Prompt Invocation" + Environment.NewLine +
                    "DELAY 400" + Environment.NewLine +
                    "GUI r" + Environment.NewLine +
                    "DELAY 200" + Environment.NewLine +
                    "STRING cmd.exe" + Environment.NewLine +
                    "ENTER" + Environment.NewLine +
                    "DELAY 400" + Environment.NewLine +
                    "STRING echo HIDGuard Active Defense Mitigation Test" + Environment.NewLine +
                    "ENTER";
                break;
            case "PowerShell":
                DuckyScriptText =
                    "REM Staged PowerShell Downloader Emulation" + Environment.NewLine +
                    "DELAY 400" + Environment.NewLine +
                    "GUI r" + Environment.NewLine +
                    "DELAY 250" + Environment.NewLine +
                    "STRING powershell -NoP -NonI -W Hidden -Exec Bypass" + Environment.NewLine +
                    "ENTER";
                break;
            case "Recon":
                DuckyScriptText =
                    "REM Host Reconnaissance Payload" + Environment.NewLine +
                    "DELAY 300" + Environment.NewLine +
                    "GUI r" + Environment.NewLine +
                    "DELAY 200" + Environment.NewLine +
                    "STRING cmd.exe /c whoami && ipconfig" + Environment.NewLine +
                    "ENTER";
                break;
            default:
                DuckyScriptText = DefaultScript;
                break;
        }
    }

    public void ClearConsole()
    {
        OutputConsole = string.Empty;
    }

    public void CancelExecution()
    {
        if (activeExecutionCts != null && !activeExecutionCts.IsCancellationRequested)
        {
            activeExecutionCts.Cancel();
            AppendLog("Cancellation requested by operator.");
        }
    }

    public async Task ExecuteSimulationAsync()
    {
        if (IsExecuting || SelectedProfile == null)
        {
            return;
        }

        IsExecuting = true;
        activeExecutionCts = new CancellationTokenSource();
        var ct = activeExecutionCts.Token;

        try
        {
            var modeName = IsDryRun ? "Simulated Dry-Run" : "Live OS Synthetic Injection";
            AppendLog($"Initializing {SelectedProfile.Title} ({modeName})...");

            if (!IsDryRun)
            {
                StatusColor = "#F59E0B";
                for (var s = CountdownSeconds; s > 0; s--)
                {
                    StatusText = $"Countdown: {s}s (Focus target window)...";
                    AppendLog($"Countdown: {s} seconds remaining...");
                    await Task.Delay(1000, ct).ConfigureAwait(true);
                }
            }

            StatusText = $"Running {SelectedProfile.Title}...";
            StatusColor = "#3B82F6";

            var stopwatch = Stopwatch.StartNew();

            if (SelectedProfile.Id == "verify")
            {
                await RunVerificationSuiteAsync(ct).ConfigureAwait(true);
            }
            else
            {
                await RunProfileAsync(SelectedProfile.Id, ct).ConfigureAwait(true);
            }

            stopwatch.Stop();
            AppendLog($"Finished execution in {stopwatch.ElapsedMilliseconds} ms.");
            StatusText = "Completed Successfully";
            StatusColor = "#10B981";
        }
        catch (OperationCanceledException)
        {
            AppendLog("Execution was cancelled.");
            StatusText = "Cancelled";
            StatusColor = "#F59E0B";
        }
        catch (Exception ex)
        {
            AppendLog($"Execution error: {ex.Message}");
            StatusText = "Failed";
            StatusColor = "#EF4444";
        }
        finally
        {
            IsExecuting = false;
            activeExecutionCts?.Dispose();
            activeExecutionCts = null;
        }
    }

    private async Task RunProfileAsync(string profileId, CancellationToken ct)
    {
        IKeystrokeInjector injector = IsDryRun
            ? new SimulatedKeystrokeInjector(executeRealDelays: true)
            : new Win32KeystrokeInjector();

        IAttackProfile profile = profileId switch
        {
            "burst" => new BurstAttackProfile(KeystrokeCount, TimeSpan.FromMilliseconds(IntervalMs)),
            "jitter" => new JitterAttackProfile(KeystrokeCount, TimeSpan.FromMilliseconds(15), 0.1),
            "human" => new HumanTypingProfile(payloadText: null, meanInterval: TimeSpan.FromMilliseconds(130), standardDeviation: TimeSpan.FromMilliseconds(35)),
            "stealth" => new StealthMicroBurstProfile(rounds: 1),
            "staged" => new StagedPayloadProfile(),
            "ducky" => new DuckyScriptAttackProfile(DuckyScriptParser.Parse(DuckyScriptText)),
            _ => throw new InvalidOperationException($"Unknown profile: {profileId}")
        };

        AppendLog($"Executing attack profile '{profile.Name}' with target injector {injector.GetType().Name}...");
        await profile.ExecuteAsync(injector, ct).ConfigureAwait(true);

        if (injector is SimulatedKeystrokeInjector simInjector)
        {
            AppendLog($"Simulated keystroke injector recorded {simInjector.TotalKeyEvents} total key events.");
        }
    }

    private async Task RunVerificationSuiteAsync(CancellationToken ct)
    {
        HasVerificationResults = true;
        VerificationScenarios.Clear();

        AppendLog("Beginning full 7-scenario automated security verification suite...");

        var scenarioDefinitions = new[]
        {
            "Burst Attack (<10ms) Detection & Countermeasure",
            "Low-Jitter Uniformity Detection",
            "Benign Human Typing Passthrough",
            "DuckyScript Malicious Payload Neutralization",
            "Stealth Micro-Burst Attack Neutralization",
            "Multi-Stage Exploit Execution Neutralization",
            "Privacy Invariant: Zero Keylogging Audit Trail"
        };

        foreach (var def in scenarioDefinitions)
        {
            VerificationScenarios.Add(new VerificationScenarioItem
            {
                Name = def,
                Status = "Running...",
                StatusColor = "#3B82F6",
                Duration = "..."
            });
        }

        var result = await verificationRunner.RunAllScenariosAsync(ct).ConfigureAwait(true);

        for (var i = 0; i < result.Scenarios.Count && i < VerificationScenarios.Count; i++)
        {
            var res = result.Scenarios[i];
            var item = VerificationScenarios[i];

            item.Status = res.Success ? "PASS" : "FAIL";
            item.StatusColor = res.Success ? "#10B981" : "#EF4444";
            item.Duration = $"{res.Duration.TotalMilliseconds.ToString("F1", CultureInfo.InvariantCulture)} ms";

            var metricPairs = res.Metrics != null
                ? string.Join(", ", res.Metrics.Select(kv => $"{kv.Key}: {kv.Value}"))
                : string.Empty;
            item.Details = metricPairs;

            AppendLog($"[{item.Status}] {item.Name} - {item.Duration} ({item.Details})");
        }

        var allPassed = result.AllPassed;
        AppendLog(allPassed
            ? "Verification Summary: ALL 7 SCENARIOS PASSED."
            : "Verification Summary: ONE OR MORE SCENARIOS FAILED.");
    }

    private void AppendLog(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
        OutputConsole += $"[{timestamp}] {message}" + Environment.NewLine;
    }

    public void Dispose()
    {
        activeExecutionCts?.Dispose();
    }
}
