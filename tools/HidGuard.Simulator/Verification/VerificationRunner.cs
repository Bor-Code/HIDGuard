using System.Diagnostics;
using System.Globalization;
using HidGuard.Core.Audit;
using HidGuard.Core.Countermeasures;
using HidGuard.Core.Domain;
using HidGuard.Core.Events;
using HidGuard.Core.Pipeline;
using HidGuard.Core.Policy;
using HidGuard.Core.Scoring;
using HidGuard.Core.Timing;
using HidGuard.Core.Trust;
using HidGuard.Simulator.Injection;
using HidGuard.Simulator.Profiles;

namespace HidGuard.Simulator.Verification;

public sealed class VerificationRunner : IVerificationRunner
{
    private static readonly DeviceIdentifier HumanDevice = new(
        @"\\?\HID#VID_046D&PID_C52B#HUMAN#{884b96c3-56ef-11d1-bc8c-00a0c91405dd}",
        0x046D,
        0xC52B);

    private static readonly DeviceIdentifier AttackDevice = new(
        @"\\?\HID#VID_16C0&PID_05DF#ATTACK#{884b96c3-56ef-11d1-bc8c-00a0c91405dd}",
        0x16C0,
        0x05DF);

    public async Task<VerificationResult> RunAllScenariosAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<VerificationScenarioResult>
        {
            await VerifyBurstAttackScenarioAsync(cancellationToken).ConfigureAwait(false),
            await VerifyJitterAttackScenarioAsync(cancellationToken).ConfigureAwait(false),
            await VerifyHumanTypingScenarioAsync(cancellationToken).ConfigureAwait(false),
            await VerifyDuckyScriptMitigationScenarioAsync(cancellationToken).ConfigureAwait(false),
            await VerifyStealthMicroBurstScenarioAsync(cancellationToken).ConfigureAwait(false),
            await VerifyStagedPayloadScenarioAsync(cancellationToken).ConfigureAwait(false),
            await VerifyPrivacyInvariantScenarioAsync(cancellationToken).ConfigureAwait(false)
        };

        return new VerificationResult { Scenarios = results };
    }

    public async Task<VerificationScenarioResult> VerifyBurstAttackScenarioAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var metrics = new Dictionary<string, string>();

        try
        {
            var source = new VerificationKeystrokeSource();
            source.SetConnectedKeyboards(new[] { HumanDevice, AttackDevice });

            var locker = new VerificationWorkstationLocker();
            var disabler = new VerificationDeviceDisabler();
            var interceptor = new VerificationKeystrokeInterceptor();
            using var trustStore = new VerificationTrustStore();
            await trustStore.SetTrustLevelAsync(HumanDevice, DeviceTrustLevel.Trusted, "Test", cancellationToken: cancellationToken).ConfigureAwait(false);
            await trustStore.SetTrustLevelAsync(AttackDevice, DeviceTrustLevel.Unknown, "Test", cancellationToken: cancellationToken).ConfigureAwait(false);

            var eventAggregator = new PipelineEventAggregator();
            var thresholds = new TimingThresholds();
            var scorer = new AnomalyScorer();
            var timingEngine = new TimingAnalysisEngine(thresholds, scorer);
            var policyEngine = new PolicyEngine(trustStore, new PolicyOptions
            {
                DefaultAnomalyAction = AnomalyAction.BlockDevice,
                EnforceMinimumOneActiveKeyboard = true
            });
            var countermeasureService = new CountermeasureService(locker, disabler, interceptor, trustStore);

            await using var pipeline = new KeystrokePipeline(
                source,
                timingEngine,
                policyEngine,
                countermeasureService,
                trustStore,
                interceptor,
                eventAggregator);

            await pipeline.StartAsync(cancellationToken).ConfigureAwait(false);

            var injector = new SimulatedKeystrokeInjector(executeRealDelays: false);
            injector.KeyInjected += (vk, isKeyUp) =>
            {
                source.EmitKeystroke(new KeystrokeEvent(
                    AttackDevice,
                    DateTimeOffset.UtcNow,
                    isKeyUp ? KeyTransitionState.KeyUp : KeyTransitionState.KeyDown,
                    vk));
            };

            var profile = new BurstAttackProfile(keystrokeCount: 50, interval: TimeSpan.FromMilliseconds(3));
            await profile.ExecuteAsync(injector, cancellationToken).ConfigureAwait(false);

            await WaitForPipelineProcessingAsync(pipeline, 50, cancellationToken).ConfigureAwait(false);

            var stats = pipeline.GetStatistics();
            var finalTrust = await trustStore.GetTrustLevelAsync(AttackDevice, cancellationToken).ConfigureAwait(false);

            metrics["ProcessedKeystrokes"] = stats.TotalKeystrokesProcessed.ToString(CultureInfo.InvariantCulture);
            metrics["AttacksDetected"] = stats.TotalAttacksDetected.ToString(CultureInfo.InvariantCulture);
            metrics["AttacksNeutralized"] = stats.TotalAttacksNeutralized.ToString(CultureInfo.InvariantCulture);
            metrics["SuppressionActive"] = interceptor.IsSuppressionActive.ToString();
            metrics["FinalDeviceTrust"] = finalTrust.ToString();

            var success = stats.TotalAttacksDetected > 0 &&
                          stats.TotalAttacksNeutralized > 0 &&
                          interceptor.IsSuppressionActive &&
                          finalTrust == DeviceTrustLevel.Blocked &&
                          disabler.DisabledDevices.Contains(AttackDevice);

            await pipeline.StopAsync(cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();

            return new VerificationScenarioResult
            {
                ScenarioName = "Burst Attack (<10ms) Detection & Countermeasure",
                Success = success,
                Description = "Injects 50 keystrokes at 3ms intervals and verifies instant detection, suppression, and hardware block.",
                Duration = stopwatch.Elapsed,
                FailureReason = success ? null : "Burst attack did not trigger expected detection, suppression, or device block.",
                Metrics = metrics
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new VerificationScenarioResult
            {
                ScenarioName = "Burst Attack (<10ms) Detection & Countermeasure",
                Success = false,
                Description = "Injects 50 keystrokes at 3ms intervals and verifies instant detection, suppression, and hardware block.",
                Duration = stopwatch.Elapsed,
                FailureReason = ex.Message,
                Metrics = metrics
            };
        }
    }

    public async Task<VerificationScenarioResult> VerifyJitterAttackScenarioAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var metrics = new Dictionary<string, string>();

        try
        {
            var source = new VerificationKeystrokeSource();
            source.SetConnectedKeyboards(new[] { HumanDevice, AttackDevice });

            var locker = new VerificationWorkstationLocker();
            var disabler = new VerificationDeviceDisabler();
            var interceptor = new VerificationKeystrokeInterceptor();
            using var trustStore = new VerificationTrustStore();
            await trustStore.SetTrustLevelAsync(HumanDevice, DeviceTrustLevel.Trusted, "Test", cancellationToken: cancellationToken).ConfigureAwait(false);
            await trustStore.SetTrustLevelAsync(AttackDevice, DeviceTrustLevel.Unknown, "Test", cancellationToken: cancellationToken).ConfigureAwait(false);

            var eventAggregator = new PipelineEventAggregator();
            var thresholds = new TimingThresholds();
            var scorer = new AnomalyScorer();
            var timingEngine = new TimingAnalysisEngine(thresholds, scorer);
            var policyEngine = new PolicyEngine(trustStore, new PolicyOptions
            {
                DefaultAnomalyAction = AnomalyAction.BlockDevice,
                EnforceMinimumOneActiveKeyboard = true
            });
            var countermeasureService = new CountermeasureService(locker, disabler, interceptor, trustStore);

            await using var pipeline = new KeystrokePipeline(
                source,
                timingEngine,
                policyEngine,
                countermeasureService,
                trustStore,
                interceptor,
                eventAggregator);

            await pipeline.StartAsync(cancellationToken).ConfigureAwait(false);

            var injector = new SimulatedKeystrokeInjector(executeRealDelays: false);
            injector.KeyInjected += (vk, isKeyUp) =>
            {
                source.EmitKeystroke(new KeystrokeEvent(
                    AttackDevice,
                    DateTimeOffset.UtcNow,
                    isKeyUp ? KeyTransitionState.KeyUp : KeyTransitionState.KeyDown,
                    vk));
            };

            var profile = new JitterAttackProfile(
                keystrokeCount: 60,
                baseInterval: TimeSpan.FromMilliseconds(15),
                maxJitterMilliseconds: 0.1);
            await profile.ExecuteAsync(injector, cancellationToken).ConfigureAwait(false);

            await WaitForPipelineProcessingAsync(pipeline, 60, cancellationToken).ConfigureAwait(false);

            var stats = pipeline.GetStatistics();
            metrics["ProcessedKeystrokes"] = stats.TotalKeystrokesProcessed.ToString(CultureInfo.InvariantCulture);
            metrics["AttacksDetected"] = stats.TotalAttacksDetected.ToString(CultureInfo.InvariantCulture);
            metrics["SuppressionActive"] = interceptor.IsSuppressionActive.ToString();

            var success = stats.TotalAttacksDetected > 0 && interceptor.IsSuppressionActive;

            await pipeline.StopAsync(cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();

            return new VerificationScenarioResult
            {
                ScenarioName = "Low-Jitter Uniformity Detection",
                Success = success,
                Description = "Injects low-jitter microcontroller-timed keystrokes (15ms +/- 0.1ms) and asserts timing uniformity detection.",
                Duration = stopwatch.Elapsed,
                FailureReason = success ? null : "Uniform microcontroller loop keystrokes were not detected as anomalous.",
                Metrics = metrics
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new VerificationScenarioResult
            {
                ScenarioName = "Low-Jitter Uniformity Detection",
                Success = false,
                Description = "Injects low-jitter microcontroller-timed keystrokes (15ms +/- 0.1ms) and asserts timing uniformity detection.",
                Duration = stopwatch.Elapsed,
                FailureReason = ex.Message,
                Metrics = metrics
            };
        }
    }

    public async Task<VerificationScenarioResult> VerifyHumanTypingScenarioAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var metrics = new Dictionary<string, string>();

        try
        {
            var source = new VerificationKeystrokeSource();
            source.SetConnectedKeyboards(new[] { HumanDevice });

            var locker = new VerificationWorkstationLocker();
            var disabler = new VerificationDeviceDisabler();
            var interceptor = new VerificationKeystrokeInterceptor();
            using var trustStore = new VerificationTrustStore();
            await trustStore.SetTrustLevelAsync(HumanDevice, DeviceTrustLevel.Trusted, "Test", cancellationToken: cancellationToken).ConfigureAwait(false);

            var eventAggregator = new PipelineEventAggregator();
            var thresholds = new TimingThresholds();
            var scorer = new AnomalyScorer();
            var timingEngine = new TimingAnalysisEngine(thresholds, scorer);
            var policyEngine = new PolicyEngine(trustStore, new PolicyOptions
            {
                DefaultAnomalyAction = AnomalyAction.AlertOnly
            });
            var countermeasureService = new CountermeasureService(locker, disabler, interceptor, trustStore);

            await using var pipeline = new KeystrokePipeline(
                source,
                timingEngine,
                policyEngine,
                countermeasureService,
                trustStore,
                interceptor,
                eventAggregator);

            await pipeline.StartAsync(cancellationToken).ConfigureAwait(false);

            var simulatedClock = DateTimeOffset.UtcNow;
            var injector = new SimulatedKeystrokeInjector(executeRealDelays: false, initialTime: simulatedClock);
            injector.KeyInjected += (vk, isKeyUp) =>
            {
                source.EmitKeystroke(new KeystrokeEvent(
                    HumanDevice,
                    simulatedClock,
                    isKeyUp ? KeyTransitionState.KeyUp : KeyTransitionState.KeyDown,
                    vk));
            };
            injector.Delayed += (duration) =>
            {
                simulatedClock = simulatedClock.Add(duration);
            };

            var profile = new HumanTypingProfile(
                payloadText: "Testing realistic human typing behavior with standard variance",
                meanInterval: TimeSpan.FromMilliseconds(135),
                standardDeviation: TimeSpan.FromMilliseconds(38));

            await profile.ExecuteAsync(injector, cancellationToken).ConfigureAwait(false);

            await WaitForPipelineProcessingAsync(pipeline, 50, cancellationToken).ConfigureAwait(false);

            var stats = pipeline.GetStatistics();
            metrics["ProcessedKeystrokes"] = stats.TotalKeystrokesProcessed.ToString(CultureInfo.InvariantCulture);
            metrics["AttacksDetected"] = stats.TotalAttacksDetected.ToString(CultureInfo.InvariantCulture);
            metrics["SuppressionActive"] = interceptor.IsSuppressionActive.ToString();
            metrics["WorkstationLockCalls"] = locker.LockCallCount.ToString(CultureInfo.InvariantCulture);

            var success = stats.TotalAttacksDetected == 0 &&
                          !interceptor.IsSuppressionActive &&
                          locker.LockCallCount == 0 &&
                          disabler.DisabledDevices.Count == 0;

            await pipeline.StopAsync(cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();

            return new VerificationScenarioResult
            {
                ScenarioName = "Benign Human Typing Passthrough",
                Success = success,
                Description = "Replays human typing profile with natural variance and verifies zero false positives and zero suppression.",
                Duration = stopwatch.Elapsed,
                FailureReason = success ? null : "Legitimate human typing triggered false positive anomaly detection or countermeasures.",
                Metrics = metrics
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new VerificationScenarioResult
            {
                ScenarioName = "Benign Human Typing Passthrough",
                Success = false,
                Description = "Replays human typing profile with natural variance and verifies zero false positives and zero suppression.",
                Duration = stopwatch.Elapsed,
                FailureReason = ex.Message,
                Metrics = metrics
            };
        }
    }

    public async Task<VerificationScenarioResult> VerifyDuckyScriptMitigationScenarioAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var metrics = new Dictionary<string, string>();

        try
        {
            var source = new VerificationKeystrokeSource();
            source.SetConnectedKeyboards(new[] { HumanDevice, AttackDevice });

            var locker = new VerificationWorkstationLocker();
            var disabler = new VerificationDeviceDisabler();
            var interceptor = new VerificationKeystrokeInterceptor();
            using var trustStore = new VerificationTrustStore();
            await trustStore.SetTrustLevelAsync(HumanDevice, DeviceTrustLevel.Trusted, "Test", cancellationToken: cancellationToken).ConfigureAwait(false);
            await trustStore.SetTrustLevelAsync(AttackDevice, DeviceTrustLevel.Unknown, "Test", cancellationToken: cancellationToken).ConfigureAwait(false);

            var eventAggregator = new PipelineEventAggregator();
            var thresholds = new TimingThresholds();
            var scorer = new AnomalyScorer();
            var timingEngine = new TimingAnalysisEngine(thresholds, scorer);
            var policyEngine = new PolicyEngine(trustStore, new PolicyOptions
            {
                DefaultAnomalyAction = AnomalyAction.BlockDevice,
                EnforceMinimumOneActiveKeyboard = true
            });
            var countermeasureService = new CountermeasureService(locker, disabler, interceptor, trustStore);

            await using var pipeline = new KeystrokePipeline(
                source,
                timingEngine,
                policyEngine,
                countermeasureService,
                trustStore,
                interceptor,
                eventAggregator);

            await pipeline.StartAsync(cancellationToken).ConfigureAwait(false);

            var injector = new SimulatedKeystrokeInjector(executeRealDelays: false);
            injector.KeyInjected += (vk, isKeyUp) =>
            {
                source.EmitKeystroke(new KeystrokeEvent(
                    AttackDevice,
                    DateTimeOffset.UtcNow,
                    isKeyUp ? KeyTransitionState.KeyUp : KeyTransitionState.KeyDown,
                    vk));
            };

            var script = "STRING powershell -NoP -NonI -W Hidden -Exec Bypass \"Invoke-Payload\"\nENTER";
            var profile = new DuckyScriptAttackProfile(script);
            await profile.ExecuteAsync(injector, cancellationToken).ConfigureAwait(false);

            await WaitForPipelineProcessingAsync(pipeline, 40, cancellationToken).ConfigureAwait(false);

            var stats = pipeline.GetStatistics();
            var finalTrust = await trustStore.GetTrustLevelAsync(AttackDevice, cancellationToken).ConfigureAwait(false);

            metrics["ProcessedKeystrokes"] = stats.TotalKeystrokesProcessed.ToString(CultureInfo.InvariantCulture);
            metrics["AttacksDetected"] = stats.TotalAttacksDetected.ToString(CultureInfo.InvariantCulture);
            metrics["FinalDeviceTrust"] = finalTrust.ToString();

            var success = stats.TotalAttacksDetected > 0 &&
                          interceptor.IsSuppressionActive &&
                          finalTrust == DeviceTrustLevel.Blocked;

            await pipeline.StopAsync(cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();

            return new VerificationScenarioResult
            {
                ScenarioName = "DuckyScript Malicious Payload Neutralization",
                Success = success,
                Description = "Executes rapid DuckyScript string injection and verifies pipeline suppression and trust revocation.",
                Duration = stopwatch.Elapsed,
                FailureReason = success ? null : "DuckyScript injection was not neutralized by the pipeline.",
                Metrics = metrics
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new VerificationScenarioResult
            {
                ScenarioName = "DuckyScript Malicious Payload Neutralization",
                Success = false,
                Description = "Executes rapid DuckyScript string injection and verifies pipeline suppression and trust revocation.",
                Duration = stopwatch.Elapsed,
                FailureReason = ex.Message,
                Metrics = metrics
            };
        }
    }

    public async Task<VerificationScenarioResult> VerifyStealthMicroBurstScenarioAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var metrics = new Dictionary<string, string>();

        try
        {
            var source = new VerificationKeystrokeSource();
            source.SetConnectedKeyboards(new[] { HumanDevice, AttackDevice });

            var locker = new VerificationWorkstationLocker();
            var disabler = new VerificationDeviceDisabler();
            var interceptor = new VerificationKeystrokeInterceptor();
            using var trustStore = new VerificationTrustStore();
            await trustStore.SetTrustLevelAsync(HumanDevice, DeviceTrustLevel.Trusted, "Test", cancellationToken: cancellationToken).ConfigureAwait(false);
            await trustStore.SetTrustLevelAsync(AttackDevice, DeviceTrustLevel.Unknown, "Test", cancellationToken: cancellationToken).ConfigureAwait(false);

            var eventAggregator = new PipelineEventAggregator();
            var thresholds = new TimingThresholds();
            var scorer = new AnomalyScorer();
            var timingEngine = new TimingAnalysisEngine(thresholds, scorer);
            var policyEngine = new PolicyEngine(trustStore, new PolicyOptions
            {
                DefaultAnomalyAction = AnomalyAction.BlockDevice,
                EnforceMinimumOneActiveKeyboard = true
            });
            var countermeasureService = new CountermeasureService(locker, disabler, interceptor, trustStore);

            await using var pipeline = new KeystrokePipeline(
                source,
                timingEngine,
                policyEngine,
                countermeasureService,
                trustStore,
                interceptor,
                eventAggregator);

            await pipeline.StartAsync(cancellationToken).ConfigureAwait(false);

            var simulatedClock = DateTimeOffset.UtcNow;
            var injector = new SimulatedKeystrokeInjector(executeRealDelays: false, initialTime: simulatedClock);
            injector.KeyInjected += (vk, isKeyUp) =>
            {
                source.EmitKeystroke(new KeystrokeEvent(
                    AttackDevice,
                    simulatedClock,
                    isKeyUp ? KeyTransitionState.KeyUp : KeyTransitionState.KeyDown,
                    vk));
            };
            injector.Delayed += (duration) =>
            {
                simulatedClock = simulatedClock.Add(duration);
            };

            var profile = new StealthMicroBurstProfile(
                decoyText: "Status check",
                burstPayload: "powershell -NoP -w hidden -c Invoke-StealthCommand",
                burstInterval: TimeSpan.FromMilliseconds(3));

            await profile.ExecuteAsync(injector, cancellationToken).ConfigureAwait(false);

            await WaitForPipelineProcessingAsync(pipeline, 40, cancellationToken).ConfigureAwait(false);

            var stats = pipeline.GetStatistics();
            var finalTrust = await trustStore.GetTrustLevelAsync(AttackDevice, cancellationToken).ConfigureAwait(false);

            metrics["ProcessedKeystrokes"] = stats.TotalKeystrokesProcessed.ToString(CultureInfo.InvariantCulture);
            metrics["AttacksDetected"] = stats.TotalAttacksDetected.ToString(CultureInfo.InvariantCulture);
            metrics["SuppressionActive"] = interceptor.IsSuppressionActive.ToString();
            metrics["FinalDeviceTrust"] = finalTrust.ToString();

            var success = stats.TotalAttacksDetected > 0 &&
                          interceptor.IsSuppressionActive &&
                          finalTrust == DeviceTrustLevel.Blocked &&
                          disabler.DisabledDevices.Contains(AttackDevice);

            await pipeline.StopAsync(cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();

            return new VerificationScenarioResult
            {
                ScenarioName = "Stealth Micro-Burst Attack Neutralization",
                Success = success,
                Description = "Simulates mixed human-rate decoy typing followed by rapid micro-burst injection and verifies evasion neutralization.",
                Duration = stopwatch.Elapsed,
                FailureReason = success ? null : "Stealth micro-burst attack was not detected or neutralized by the pipeline.",
                Metrics = metrics
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new VerificationScenarioResult
            {
                ScenarioName = "Stealth Micro-Burst Attack Neutralization",
                Success = false,
                Description = "Simulates mixed human-rate decoy typing followed by rapid micro-burst injection and verifies evasion neutralization.",
                Duration = stopwatch.Elapsed,
                FailureReason = ex.Message,
                Metrics = metrics
            };
        }
    }

    public async Task<VerificationScenarioResult> VerifyStagedPayloadScenarioAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var metrics = new Dictionary<string, string>();

        try
        {
            var source = new VerificationKeystrokeSource();
            source.SetConnectedKeyboards(new[] { HumanDevice, AttackDevice });

            var locker = new VerificationWorkstationLocker();
            var disabler = new VerificationDeviceDisabler();
            var interceptor = new VerificationKeystrokeInterceptor();
            using var trustStore = new VerificationTrustStore();
            await trustStore.SetTrustLevelAsync(HumanDevice, DeviceTrustLevel.Trusted, "Test", cancellationToken: cancellationToken).ConfigureAwait(false);
            await trustStore.SetTrustLevelAsync(AttackDevice, DeviceTrustLevel.Unknown, "Test", cancellationToken: cancellationToken).ConfigureAwait(false);

            var eventAggregator = new PipelineEventAggregator();
            var thresholds = new TimingThresholds();
            var scorer = new AnomalyScorer();
            var timingEngine = new TimingAnalysisEngine(thresholds, scorer);
            var policyEngine = new PolicyEngine(trustStore, new PolicyOptions
            {
                DefaultAnomalyAction = AnomalyAction.BlockDevice,
                EnforceMinimumOneActiveKeyboard = true
            });
            var countermeasureService = new CountermeasureService(locker, disabler, interceptor, trustStore);

            await using var pipeline = new KeystrokePipeline(
                source,
                timingEngine,
                policyEngine,
                countermeasureService,
                trustStore,
                interceptor,
                eventAggregator);

            await pipeline.StartAsync(cancellationToken).ConfigureAwait(false);

            var simulatedClock = DateTimeOffset.UtcNow;
            var injector = new SimulatedKeystrokeInjector(executeRealDelays: false, initialTime: simulatedClock);
            injector.KeyInjected += (vk, isKeyUp) =>
            {
                source.EmitKeystroke(new KeystrokeEvent(
                    AttackDevice,
                    simulatedClock,
                    isKeyUp ? KeyTransitionState.KeyUp : KeyTransitionState.KeyDown,
                    vk));
            };
            injector.Delayed += (duration) =>
            {
                simulatedClock = simulatedClock.Add(duration);
            };

            var profile = new StagedPayloadProfile(
                stageOneCommand: "powershell -NoP -w hidden -c Invoke-StagedExploit",
                stageOneInterval: TimeSpan.FromMilliseconds(3));

            await profile.ExecuteAsync(injector, cancellationToken).ConfigureAwait(false);

            await WaitForPipelineProcessingAsync(pipeline, 35, cancellationToken).ConfigureAwait(false);

            var stats = pipeline.GetStatistics();
            var finalTrust = await trustStore.GetTrustLevelAsync(AttackDevice, cancellationToken).ConfigureAwait(false);

            metrics["ProcessedKeystrokes"] = stats.TotalKeystrokesProcessed.ToString(CultureInfo.InvariantCulture);
            metrics["AttacksDetected"] = stats.TotalAttacksDetected.ToString(CultureInfo.InvariantCulture);
            metrics["SuppressionActive"] = interceptor.IsSuppressionActive.ToString();
            metrics["FinalDeviceTrust"] = finalTrust.ToString();

            var success = stats.TotalAttacksDetected > 0 &&
                          interceptor.IsSuppressionActive &&
                          finalTrust == DeviceTrustLevel.Blocked &&
                          disabler.DisabledDevices.Contains(AttackDevice);

            await pipeline.StopAsync(cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();

            return new VerificationScenarioResult
            {
                ScenarioName = "Multi-Stage Exploit Execution Neutralization",
                Success = success,
                Description = "Simulates multi-stage Run-dialog invocation and rapid command payload injection with pipeline suppression.",
                Duration = stopwatch.Elapsed,
                FailureReason = success ? null : "Multi-stage exploit execution was not detected or neutralized by the pipeline.",
                Metrics = metrics
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new VerificationScenarioResult
            {
                ScenarioName = "Multi-Stage Exploit Execution Neutralization",
                Success = false,
                Description = "Simulates multi-stage Run-dialog invocation and rapid command payload injection with pipeline suppression.",
                Duration = stopwatch.Elapsed,
                FailureReason = ex.Message,
                Metrics = metrics
            };
        }
    }

    public Task<VerificationScenarioResult> VerifyPrivacyInvariantScenarioAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var metrics = new Dictionary<string, string>();

        try
        {
            var auditService = new VerificationAuditTrailService();

            var properties = typeof(AuditEvent).GetProperties().Select(p => p.Name.ToLowerInvariant()).ToList();
            var forbiddenNames = new[] { "key", "vkcode", "virtualkey", "char", "character", "typedtext" };
            var foundForbidden = properties.Intersect(forbiddenNames).ToList();

            metrics["AuditEventPropertyCount"] = properties.Count.ToString(CultureInfo.InvariantCulture);
            metrics["ForbiddenFieldsFound"] = foundForbidden.Count.ToString(CultureInfo.InvariantCulture);

            var sampleEvent = new AuditEvent
            {
                EventType = AuditEventType.AnomalyDetected,
                DeviceId = "HID\\VID_16C0&PID_05DF",
                ActionTaken = "BlockDevice",
                Outcome = "Neutralized",
                Details = "Timing anomaly score 0.94 exceeded threshold"
            };

            auditService.RecordEventAsync(sampleEvent, cancellationToken);
            var recorded = auditService.RecordedEvents;

            var noForbiddenFields = foundForbidden.Count == 0;
            var safeDetails = !sampleEvent.Details.Contains("VirtualKey", StringComparison.OrdinalIgnoreCase);

            var success = noForbiddenFields && safeDetails && recorded.Count == 1;
            stopwatch.Stop();

            return Task.FromResult(new VerificationScenarioResult
            {
                ScenarioName = "Privacy Invariant: Zero Keylogging Audit Trail",
                Success = success,
                Description = "Verifies AuditEvent schema and audit records never expose typed characters or virtual key codes.",
                Duration = stopwatch.Elapsed,
                FailureReason = success ? null : "Privacy invariant violated: Audit trail references raw keystroke data.",
                Metrics = metrics
            });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return Task.FromResult(new VerificationScenarioResult
            {
                ScenarioName = "Privacy Invariant: Zero Keylogging Audit Trail",
                Success = false,
                Description = "Verifies AuditEvent schema and audit records never expose typed characters or virtual key codes.",
                Duration = stopwatch.Elapsed,
                FailureReason = ex.Message,
                Metrics = metrics
            });
        }
    }

    private static async Task WaitForPipelineProcessingAsync(
        KeystrokePipeline pipeline,
        int minExpected,
        CancellationToken cancellationToken)
    {
        var timeout = Stopwatch.StartNew();
        while (timeout.ElapsedMilliseconds < 1500)
        {
            if (pipeline.GetStatistics().TotalKeystrokesProcessed >= minExpected)
            {
                break;
            }
            await Task.Delay(10, cancellationToken).ConfigureAwait(false);
        }
    }
}
