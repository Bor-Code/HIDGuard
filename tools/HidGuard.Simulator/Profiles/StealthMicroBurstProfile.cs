using HidGuard.Simulator.DuckyScript;
using HidGuard.Simulator.Injection;

namespace HidGuard.Simulator.Profiles;

public sealed class StealthMicroBurstProfile : IAttackProfile
{
    private readonly Random random = new(42);

    public AttackProfileType ProfileType => AttackProfileType.StealthMicroBurst;
    public string Name => "Stealth Mixed-Cadence Micro-Burst Attack";

    public string DecoyText { get; }
    public string BurstPayload { get; }
    public TimeSpan DecoyInterval { get; }
    public TimeSpan BurstInterval { get; }
    public int Rounds { get; }

    public StealthMicroBurstProfile(
        string? decoyText = null,
        string? burstPayload = null,
        TimeSpan? decoyInterval = null,
        TimeSpan? burstInterval = null,
        int rounds = 1)
    {
        DecoyText = !string.IsNullOrEmpty(decoyText) ? decoyText : "Searching document for project status";
        BurstPayload = !string.IsNullOrEmpty(burstPayload) ? burstPayload : "powershell -NoP -NonI -W Hidden -Exec Bypass -Command \"Invoke-StealthExploit\"";
        DecoyInterval = decoyInterval ?? TimeSpan.FromMilliseconds(140);
        BurstInterval = burstInterval ?? TimeSpan.FromMilliseconds(4);
        Rounds = Math.Max(1, rounds);
    }

    public async Task ExecuteAsync(IKeystrokeInjector injector, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(injector);
        cancellationToken.ThrowIfCancellationRequested();

        for (var round = 0; round < Rounds; round++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var ch in DecoyText)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var (vk, shift) = VirtualKeyMap.CharacterToVirtualKey(ch);
                if (vk != 0)
                {
                    var holdMs = Math.Clamp(50.0 + (random.NextDouble() * 30.0), 30.0, 90.0);
                    await injector.SendKeyPressAsync(vk, shift, TimeSpan.FromMilliseconds(holdMs), cancellationToken).ConfigureAwait(false);
                    var jitterMs = (random.NextDouble() * 40.0) - 20.0;
                    var interval = TimeSpan.FromMilliseconds(Math.Max(60.0, DecoyInterval.TotalMilliseconds + jitterMs));
                    await injector.DelayAsync(interval, cancellationToken).ConfigureAwait(false);
                }
            }

            foreach (var ch in BurstPayload)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var (vk, shift) = VirtualKeyMap.CharacterToVirtualKey(ch);
                if (vk != 0)
                {
                    await injector.SendKeyPressAsync(vk, shift, TimeSpan.FromMilliseconds(1), cancellationToken).ConfigureAwait(false);
                    await injector.DelayAsync(BurstInterval, cancellationToken).ConfigureAwait(false);
                }
            }

            await injector.SendKeyPressAsync(VirtualKeyMap.VkReturn, false, TimeSpan.FromMilliseconds(1), cancellationToken).ConfigureAwait(false);
            await injector.DelayAsync(BurstInterval, cancellationToken).ConfigureAwait(false);
        }
    }
}
