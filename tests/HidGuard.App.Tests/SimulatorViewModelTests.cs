using Shouldly;
using Xunit;
using HidGuard.App.ViewModels;
using HidGuard.Simulator.Verification;

namespace HidGuard.App.Tests;

public sealed class SimulatorViewModelTests
{
    private sealed class StubVerificationRunner : IVerificationRunner
    {
        public bool RunAllScenariosCalled { get; private set; }

        public Task<VerificationResult> RunAllScenariosAsync(CancellationToken cancellationToken = default)
        {
            RunAllScenariosCalled = true;
            var scenarios = new List<VerificationScenarioResult>
            {
                new() { ScenarioName = "Burst Attack", Success = true, Description = "Test", Duration = TimeSpan.FromMilliseconds(45.2) },
                new() { ScenarioName = "Low-Jitter Uniformity", Success = true, Description = "Test", Duration = TimeSpan.FromMilliseconds(18.4) },
                new() { ScenarioName = "Human Typing", Success = true, Description = "Test", Duration = TimeSpan.FromMilliseconds(22.1) },
                new() { ScenarioName = "DuckyScript Payload", Success = true, Description = "Test", Duration = TimeSpan.FromMilliseconds(31.0) },
                new() { ScenarioName = "Stealth Micro-Burst", Success = true, Description = "Test", Duration = TimeSpan.FromMilliseconds(28.7) },
                new() { ScenarioName = "Multi-Stage Exploit", Success = true, Description = "Test", Duration = TimeSpan.FromMilliseconds(25.9) },
                new() { ScenarioName = "Privacy Invariant", Success = true, Description = "Test", Duration = TimeSpan.FromMilliseconds(12.3) }
            };

            return Task.FromResult(new VerificationResult { Scenarios = scenarios });
        }

        public Task<VerificationScenarioResult> VerifyBurstAttackScenarioAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new VerificationScenarioResult { ScenarioName = "Burst", Success = true, Description = "Test" });

        public Task<VerificationScenarioResult> VerifyJitterAttackScenarioAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new VerificationScenarioResult { ScenarioName = "Jitter", Success = true, Description = "Test" });

        public Task<VerificationScenarioResult> VerifyHumanTypingScenarioAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new VerificationScenarioResult { ScenarioName = "Human", Success = true, Description = "Test" });

        public Task<VerificationScenarioResult> VerifyDuckyScriptMitigationScenarioAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new VerificationScenarioResult { ScenarioName = "Ducky", Success = true, Description = "Test" });

        public Task<VerificationScenarioResult> VerifyStealthMicroBurstScenarioAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new VerificationScenarioResult { ScenarioName = "Stealth", Success = true, Description = "Test" });

        public Task<VerificationScenarioResult> VerifyStagedPayloadScenarioAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new VerificationScenarioResult { ScenarioName = "Staged", Success = true, Description = "Test" });

        public Task<VerificationScenarioResult> VerifyPrivacyInvariantScenarioAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new VerificationScenarioResult { ScenarioName = "Privacy", Success = true, Description = "Test" });
    }

    [Fact]
    public void InitialStateShouldBeProperlyConfigured()
    {
        using var vm = new SimulatorViewModel();

        vm.AvailableProfiles.Count.ShouldBe(7);
        vm.SelectedProfile.ShouldNotBeNull();
        vm.SelectedProfile.Id.ShouldBe("burst");
        vm.IsDryRun.ShouldBeTrue();
        vm.CountdownSeconds.ShouldBe(3);
        vm.KeystrokeCount.ShouldBe(50);
        vm.IntervalMs.ShouldBe(4);
        vm.StatusText.ShouldBe("Ready");
        vm.CanExecute.ShouldBeTrue();
        vm.CanCancel.ShouldBeFalse();
        vm.HasVerificationResults.ShouldBeFalse();
    }

    [Fact]
    public void ProfileSelectionShouldUpdateFlags()
    {
        using var vm = new SimulatorViewModel();

        var duckyProfile = vm.AvailableProfiles.First(p => p.Id == "ducky");
        vm.SelectedProfile = duckyProfile;
        vm.IsDuckyScriptSelected.ShouldBeTrue();
        vm.IsVerificationSelected.ShouldBeFalse();
        vm.IsStandardProfileSelected.ShouldBeFalse();

        var verifyProfile = vm.AvailableProfiles.First(p => p.Id == "verify");
        vm.SelectedProfile = verifyProfile;
        vm.IsDuckyScriptSelected.ShouldBeFalse();
        vm.IsVerificationSelected.ShouldBeTrue();
        vm.IsStandardProfileSelected.ShouldBeFalse();

        var jitterProfile = vm.AvailableProfiles.First(p => p.Id == "jitter");
        vm.SelectedProfile = jitterProfile;
        vm.IsDuckyScriptSelected.ShouldBeFalse();
        vm.IsVerificationSelected.ShouldBeFalse();
        vm.IsStandardProfileSelected.ShouldBeTrue();
    }

    [Fact]
    public void LoadPresetCommandShouldPopulateScript()
    {
        using var vm = new SimulatorViewModel();

        vm.LoadPresetCommand.Execute("Hello");
        vm.DuckyScriptText.ShouldContain("Hello from HIDGuard");

        vm.LoadPresetCommand.Execute("RunCmd");
        vm.DuckyScriptText.ShouldContain("cmd.exe");

        vm.LoadPresetCommand.Execute("PowerShell");
        vm.DuckyScriptText.ShouldContain("powershell");

        vm.LoadPresetCommand.Execute("Recon");
        vm.DuckyScriptText.ShouldContain("whoami && ipconfig");
    }

    [Fact]
    public void ClearConsoleCommandShouldEmptyOutput()
    {
        using var vm = new SimulatorViewModel();
        vm.OutputConsole.ShouldNotBeEmpty();

        vm.ClearConsoleCommand.Execute(null);
        vm.OutputConsole.ShouldBeEmpty();
    }

    [Fact]
    public async Task ExecuteSimulationAsyncInDryRunModeShouldSucceed()
    {
        using var vm = new SimulatorViewModel
        {
            IsDryRun = true,
            KeystrokeCount = 10,
            IntervalMs = 1
        };

        await vm.ExecuteSimulationAsync();

        vm.StatusText.ShouldBe("Completed Successfully");
        vm.StatusColor.ShouldBe("#10B981");
        vm.OutputConsole.ShouldContain("Rapid Burst Injection");
        vm.OutputConsole.ShouldContain("Finished execution in");
    }

    [Fact]
    public async Task ExecuteVerificationSuiteShouldPopulateResults()
    {
        var stubRunner = new StubVerificationRunner();
        using var vm = new SimulatorViewModel(stubRunner)
        {
            SelectedProfile = new SimulatorProfileItem { Id = "verify", Title = "Automated E2E Verification Suite" }
        };

        await vm.ExecuteSimulationAsync();

        stubRunner.RunAllScenariosCalled.ShouldBeTrue();
        vm.HasVerificationResults.ShouldBeTrue();
        vm.VerificationScenarios.Count.ShouldBe(7);
        vm.VerificationScenarios.All(s => s.Status == "PASS").ShouldBeTrue();
        vm.StatusText.ShouldBe("Completed Successfully");
    }

    [Fact]
    public void CancelExecutionShouldSetRequestedFlag()
    {
        using var vm = new SimulatorViewModel();
        vm.CancelExecution();
        vm.StatusText.ShouldBe("Ready");
    }
}
