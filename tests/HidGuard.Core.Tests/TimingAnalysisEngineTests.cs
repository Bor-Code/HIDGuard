using Shouldly;
using Xunit;
using HidGuard.Core.Domain;
using HidGuard.Core.Scoring;
using HidGuard.Core.Timing;

namespace HidGuard.Core.Tests;

public sealed class TimingAnalysisEngineTests
{
    private static readonly DeviceIdentifier HumanDevice = new("HUMAN_KEYBOARD_PATH", 0x046D, 0xC52B);
    private static readonly DeviceIdentifier AttackDevice = new("BADUSB_ATTACK_PATH", 0x16C0, 0x05DF);

    [Fact]
    public void KeyUpEventsShouldNotTriggerIntervalAnalysis()
    {
        var engine = new TimingAnalysisEngine(new TimingThresholds(), new AnomalyScorer());
        var keyUp = new KeystrokeEvent(HumanDevice, DateTimeOffset.UtcNow, KeyTransitionState.KeyUp, 0x41);

        var result = engine.ProcessKeystroke(keyUp);

        result.Score.ShouldBe(0.0);
        result.IsSuspicious.ShouldBeFalse();
        result.Statistics.SampleCount.ShouldBe(0);
    }

    [Fact]
    public void MultiDeviceIsolationShouldEnsureAttackDoesNotAffectHumanDevice()
    {
        var engine = new TimingAnalysisEngine(new TimingThresholds(), new AnomalyScorer());
        var baseTime = DateTimeOffset.UtcNow;

        for (var i = 0; i < 20; i++)
        {
            var humanStroke = new KeystrokeEvent(
                HumanDevice,
                baseTime.AddMilliseconds(i * 150),
                KeyTransitionState.KeyDown,
                0x41);
            engine.ProcessKeystroke(humanStroke);
        }

        var humanStatsBeforeAttack = engine.GetStatistics(HumanDevice);
        humanStatsBeforeAttack.ShouldNotBeNull();
        humanStatsBeforeAttack.MeanIntervalMs.ShouldBe(150.0);

        for (var i = 0; i < 30; i++)
        {
            var attackStroke = new KeystrokeEvent(
                AttackDevice,
                baseTime.AddMilliseconds(i * 5),
                KeyTransitionState.KeyDown,
                0x42);
            var attackResult = engine.ProcessKeystroke(attackStroke);

            if (i >= 15)
            {
                attackResult.IsSuspicious.ShouldBeTrue();
            }
        }

        var humanStatsAfterAttack = engine.GetStatistics(HumanDevice);
        humanStatsAfterAttack.ShouldNotBeNull();
        humanStatsAfterAttack.MeanIntervalMs.ShouldBe(150.0);
        humanStatsAfterAttack.SampleCount.ShouldBe(19);

        var attackStats = engine.GetStatistics(AttackDevice);
        attackStats.ShouldNotBeNull();
        attackStats.MeanIntervalMs.ShouldBe(5.0);
        attackStats.SubHumanIntervalRatio.ShouldBe(1.0);
    }

    [Fact]
    public void ResetDeviceShouldClearDeviceHistory()
    {
        var engine = new TimingAnalysisEngine(new TimingThresholds(), new AnomalyScorer());
        var baseTime = DateTimeOffset.UtcNow;

        for (var i = 0; i < 5; i++)
        {
            engine.ProcessKeystroke(new KeystrokeEvent(
                HumanDevice,
                baseTime.AddMilliseconds(i * 50),
                KeyTransitionState.KeyDown,
                0x41));
        }

        engine.GetStatistics(HumanDevice)!.SampleCount.ShouldBe(4);

        engine.ResetDevice(HumanDevice);

        engine.GetStatistics(HumanDevice)!.SampleCount.ShouldBe(0);
    }

    [Fact]
    public void RemoveDeviceShouldRemoveTrackerFromEngine()
    {
        var engine = new TimingAnalysisEngine(new TimingThresholds(), new AnomalyScorer());
        engine.ProcessKeystroke(new KeystrokeEvent(
            HumanDevice,
            DateTimeOffset.UtcNow,
            KeyTransitionState.KeyDown,
            0x41));

        engine.TrackedDevices.ShouldContain(HumanDevice);

        engine.RemoveDevice(HumanDevice);

        engine.TrackedDevices.ShouldNotContain(HumanDevice);
        engine.GetStatistics(HumanDevice).ShouldBeNull();
    }

    [Fact]
    public void PruneInactiveDevicesShouldRemoveOldDevices()
    {
        var engine = new TimingAnalysisEngine(new TimingThresholds(), new AnomalyScorer());
        var pastTime = DateTimeOffset.UtcNow.AddHours(-2);

        engine.ProcessKeystroke(new KeystrokeEvent(
            HumanDevice,
            pastTime,
            KeyTransitionState.KeyDown,
            0x41));

        engine.TrackedDevices.ShouldContain(HumanDevice);

        engine.PruneInactiveDevices(TimeSpan.FromHours(1));

        engine.TrackedDevices.ShouldNotContain(HumanDevice);
    }
}
