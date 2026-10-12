using Shouldly;
using Xunit;
using HidGuard.App.ViewModels;
using HidGuard.Core.Audit;
using HidGuard.Core.Domain;
using HidGuard.Core.Events;
using HidGuard.Core.Pipeline;
using HidGuard.Core.Scoring;
using HidGuard.Core.Timing;

namespace HidGuard.App.Tests;

public sealed class OverviewViewModelTests
{
    private sealed class StubKeystrokePipeline : IKeystrokePipeline
    {
        public bool IsRunning => true;

        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public PipelineStatistics GetStatistics() => new()
        {
            TotalKeystrokesProcessed = 150,
            TotalKeystrokesSuppressed = 12,
            TotalAttacksDetected = 2,
            TotalAttacksNeutralized = 2,
            ActiveTrackedDevices = 1
        };

        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class StubAuditTrailService : IAuditTrailService
    {
        public event EventHandler<AuditEvent>? AuditEventRecorded;

        public Task RecordEventAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
        {
            AuditEventRecorded?.Invoke(this, auditEvent);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<AuditEvent>> QueryRecentEventsAsync(int limit = 100, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<AuditEvent>>(Array.Empty<AuditEvent>());
        }

        public void Dispose() { }
    }

    [Fact]
    public void OverviewViewModelShouldInitializeFromPipelineStats()
    {
        var pipeline = new StubKeystrokePipeline();
        var audit = new StubAuditTrailService();

        var vm = new OverviewViewModel(pipeline, audit);

        vm.TotalKeystrokes.ShouldBe(150);
        vm.SuppressedKeystrokes.ShouldBe(12);
        vm.AttacksDetected.ShouldBe(2);
        vm.ThreatsNeutralized.ShouldBe(2);
        vm.IsProtectionActive.ShouldBeTrue();
        vm.StatusText.ShouldBe("Active Defense Engaged");
    }

    [Fact]
    public void ToggleProtectionShouldUpdateStateAndStatus()
    {
        var pipeline = new StubKeystrokePipeline();
        var audit = new StubAuditTrailService();

        var vm = new OverviewViewModel(pipeline, audit);

        vm.ToggleProtectionCommand.Execute(null);

        vm.IsProtectionActive.ShouldBeFalse();
        vm.StatusText.ShouldContain("Paused");

        vm.ToggleProtectionCommand.Execute(null);

        vm.IsProtectionActive.ShouldBeTrue();
        vm.StatusText.ShouldContain("Active");
    }

    [Fact]
    public void RecordLiveKeystrokeShouldUpdateMetrics()
    {
        var pipeline = new StubKeystrokePipeline();
        var audit = new StubAuditTrailService();

        var vm = new OverviewViewModel(pipeline, audit);
        var dev = new DeviceIdentifier("TestDevice", 0x1234, 0x5678);
        var key = new KeystrokeEvent(dev, DateTimeOffset.UtcNow, KeyTransitionState.KeyDown, 65);

        var stats = new SlidingWindowStatistics
        {
            SampleCount = 10,
            MeanIntervalMs = 20.0,
            StandardDeviationMs = 2.0
        };

        var scoreResult = new AnomalyScoreResult
        {
            Score = 92.5,
            IsSuspicious = true,
            Statistics = stats,
            EvaluatedAt = DateTimeOffset.UtcNow
        };

        var evt = new PipelineKeystrokeProcessedEvent
        {
            Keystroke = key,
            AnomalyResult = scoreResult,
            Decision = new HidGuard.Core.Policy.PolicyDecision(
                HidGuard.Core.Policy.PolicyAction.LockWorkstation,
                dev,
                HidGuard.Core.Trust.DeviceTrustLevel.Unknown,
                "Threat detected",
                scoreResult),
            Latency = TimeSpan.FromMilliseconds(1)
        };

        vm.RecordLiveKeystroke(evt);

        vm.LatestAnomalyScore.ShouldBe(92.5);
        vm.CurrentWpm.ShouldBeGreaterThan(0);
        vm.StatusText.ShouldContain("Attack Detected");
    }
}
