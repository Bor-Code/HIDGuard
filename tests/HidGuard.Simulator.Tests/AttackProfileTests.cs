using Shouldly;
using Xunit;
using HidGuard.Simulator.DuckyScript;
using HidGuard.Simulator.Injection;
using HidGuard.Simulator.Profiles;

namespace HidGuard.Simulator.Tests;

public sealed class AttackProfileTests
{
    [Fact]
    public async Task BurstAttackProfileShouldEmitExpectedCountAndIntervals()
    {
        var injector = new SimulatedKeystrokeInjector(executeRealDelays: false);
        var delays = new List<TimeSpan>();
        injector.Delayed += d => delays.Add(d);

        var profile = new BurstAttackProfile(keystrokeCount: 20, interval: TimeSpan.FromMilliseconds(4));
        await profile.ExecuteAsync(injector);

        injector.InjectedRecords.Count.ShouldBe(40);
        delays.Count(d => d == TimeSpan.FromMilliseconds(4)).ShouldBe(20);
    }

    [Fact]
    public async Task BurstAttackProfileWithCustomTextShouldTypeCharacters()
    {
        var injector = new SimulatedKeystrokeInjector(executeRealDelays: false);
        var profile = new BurstAttackProfile(payloadText: "ABC");
        await profile.ExecuteAsync(injector);

        injector.InjectedRecords.Count.ShouldBe(12);
    }

    [Fact]
    public async Task JitterAttackProfileShouldEmitUniformDelays()
    {
        var injector = new SimulatedKeystrokeInjector(executeRealDelays: false);
        var delays = new List<TimeSpan>();
        injector.Delayed += d => delays.Add(d);

        var profile = new JitterAttackProfile(
            keystrokeCount: 30,
            baseInterval: TimeSpan.FromMilliseconds(15),
            maxJitterMilliseconds: 0.2);

        await profile.ExecuteAsync(injector);

        var interKeyDelays = delays.Where(d => d.TotalMilliseconds >= 10.0).ToList();
        interKeyDelays.Count.ShouldBe(30);

        var msValues = interKeyDelays.Select(d => d.TotalMilliseconds).ToList();
        var mean = msValues.Average();
        var variance = msValues.Select(v => Math.Pow(v - mean, 2)).Average();
        var stdDev = Math.Sqrt(variance);

        mean.ShouldBeInRange(14.8, 15.2);
        stdDev.ShouldBeLessThan(0.3);
    }

    [Fact]
    public async Task HumanTypingProfileShouldEmitVariableDelaysWithinBounds()
    {
        var injector = new SimulatedKeystrokeInjector(executeRealDelays: false);
        var delays = new List<TimeSpan>();
        injector.Delayed += d => delays.Add(d);

        var profile = new HumanTypingProfile(
            payloadText: "Testing realistic human typing intervals with variable pauses.",
            meanInterval: TimeSpan.FromMilliseconds(130),
            standardDeviation: TimeSpan.FromMilliseconds(35));

        await profile.ExecuteAsync(injector);

        var interKeyDelays = delays.Where(d => d.TotalMilliseconds >= 60.0).ToList();
        interKeyDelays.Count.ShouldBeGreaterThan(30);

        var msValues = interKeyDelays.Select(d => d.TotalMilliseconds).ToList();
        var min = msValues.Min();
        var max = msValues.Max();

        min.ShouldBeGreaterThanOrEqualTo(60.0);
        max.ShouldBeLessThanOrEqualTo(500.0);

        var mean = msValues.Average();
        var variance = msValues.Select(v => Math.Pow(v - mean, 2)).Average();
        var stdDev = Math.Sqrt(variance);

        mean.ShouldBeGreaterThan(100.0);
        stdDev.ShouldBeGreaterThan(15.0);
    }

    [Fact]
    public async Task DuckyScriptAttackProfileShouldExecuteParsedInstructions()
    {
        var injector = new SimulatedKeystrokeInjector(executeRealDelays: false);
        var delays = new List<TimeSpan>();
        injector.Delayed += d => delays.Add(d);

        var script = "DELAY 100\nGUI r\nDELAY 50\nSTRING cmd\nENTER";
        var profile = new DuckyScriptAttackProfile(script);

        await profile.ExecuteAsync(injector);

        injector.InjectedRecords.Count.ShouldBeGreaterThan(10);
        delays.ShouldContain(TimeSpan.FromMilliseconds(100));
        delays.ShouldContain(TimeSpan.FromMilliseconds(50));
    }

    [Fact]
    public async Task SimulatedKeystrokeInjectorShouldRecordKeysAndAdvanceClock()
    {
        var initial = DateTimeOffset.UtcNow;
        var injector = new SimulatedKeystrokeInjector(executeRealDelays: false, initialTime: initial);

        await injector.SendKeyDownAsync(0x41);
        await injector.DelayAsync(TimeSpan.FromMilliseconds(50));
        await injector.SendKeyUpAsync(0x41);

        injector.InjectedRecords.Count.ShouldBe(2);
        injector.InjectedRecords[0].VirtualKey.ShouldBe((ushort)0x41);
        injector.InjectedRecords[0].IsKeyUp.ShouldBeFalse();
        injector.InjectedRecords[1].IsKeyUp.ShouldBeTrue();

        var elapsed = injector.InjectedRecords[1].Timestamp - injector.InjectedRecords[0].Timestamp;
        elapsed.ShouldBe(TimeSpan.FromMilliseconds(50));
    }
}
