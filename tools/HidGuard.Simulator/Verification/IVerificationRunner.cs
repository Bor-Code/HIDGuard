namespace HidGuard.Simulator.Verification;

public interface IVerificationRunner
{
    Task<VerificationResult> RunAllScenariosAsync(CancellationToken cancellationToken = default);
    Task<VerificationScenarioResult> VerifyBurstAttackScenarioAsync(CancellationToken cancellationToken = default);
    Task<VerificationScenarioResult> VerifyJitterAttackScenarioAsync(CancellationToken cancellationToken = default);
    Task<VerificationScenarioResult> VerifyHumanTypingScenarioAsync(CancellationToken cancellationToken = default);
    Task<VerificationScenarioResult> VerifyDuckyScriptMitigationScenarioAsync(CancellationToken cancellationToken = default);
    Task<VerificationScenarioResult> VerifyPrivacyInvariantScenarioAsync(CancellationToken cancellationToken = default);
}
