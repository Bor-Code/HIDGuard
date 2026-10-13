using Shouldly;
using Xunit;
using HidGuard.Simulator.Verification;

namespace HidGuard.Simulator.Tests;

public sealed class VerificationRunnerTests
{
    private readonly VerificationRunner runner = new();

    [Fact]
    public async Task VerifyBurstAttackScenarioShouldPass()
    {
        var result = await runner.VerifyBurstAttackScenarioAsync();

        result.Success.ShouldBeTrue(result.FailureReason);
        result.Metrics.ContainsKey("AttacksDetected").ShouldBeTrue();
        result.Metrics["FinalDeviceTrust"].ShouldBe("Blocked");
    }

    [Fact]
    public async Task VerifyJitterAttackScenarioShouldPass()
    {
        var result = await runner.VerifyJitterAttackScenarioAsync();

        result.Success.ShouldBeTrue(result.FailureReason);
        result.Metrics["AttacksDetected"].ShouldNotBe("0");
    }

    [Fact]
    public async Task VerifyHumanTypingScenarioShouldPass()
    {
        var result = await runner.VerifyHumanTypingScenarioAsync();

        result.Success.ShouldBeTrue(result.FailureReason);
        result.Metrics["AttacksDetected"].ShouldBe("0");
        result.Metrics["SuppressionActive"].ShouldBe("False");
        result.Metrics["WorkstationLockCalls"].ShouldBe("0");
    }

    [Fact]
    public async Task VerifyDuckyScriptMitigationScenarioShouldPass()
    {
        var result = await runner.VerifyDuckyScriptMitigationScenarioAsync();

        result.Success.ShouldBeTrue(result.FailureReason);
        result.Metrics["FinalDeviceTrust"].ShouldBe("Blocked");
    }

    [Fact]
    public async Task VerifyPrivacyInvariantScenarioShouldPass()
    {
        var result = await runner.VerifyPrivacyInvariantScenarioAsync();

        result.Success.ShouldBeTrue(result.FailureReason);
        result.Metrics["ForbiddenFieldsFound"].ShouldBe("0");
    }

    [Fact]
    public async Task RunAllScenariosShouldPassAndProduceValidReport()
    {
        var report = await runner.RunAllScenariosAsync();

        report.AllPassed.ShouldBeTrue();
        report.TotalCount.ShouldBe(5);
        report.PassedCount.ShouldBe(5);
        report.FailedCount.ShouldBe(0);

        var reportText = report.FormatReport();
        reportText.ShouldContain("HIDGUARD END-TO-END AUTOMATED VERIFICATION REPORT");
        reportText.ShouldContain("5 / 5 Scenarios Passed");
        reportText.ShouldContain("PASS");
        reportText.ShouldNotContain("FAIL");
    }
}
