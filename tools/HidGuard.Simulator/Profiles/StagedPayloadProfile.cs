using HidGuard.Simulator.DuckyScript;
using HidGuard.Simulator.Injection;

namespace HidGuard.Simulator.Profiles;

public sealed class StagedPayloadProfile : IAttackProfile
{
    public AttackProfileType ProfileType => AttackProfileType.StagedPayload;
    public string Name => "Multi-Stage Exploit Execution Profile";

    public IReadOnlyList<ushort> ShortcutKeys { get; }
    public TimeSpan ShortcutWaitDelay { get; }
    public string StageOneCommand { get; }
    public TimeSpan StageOneInterval { get; }
    public TimeSpan InterStageDelay { get; }
    public string? StageTwoCommand { get; }
    public TimeSpan StageTwoInterval { get; }

    public StagedPayloadProfile(
        IReadOnlyList<ushort>? shortcutKeys = null,
        TimeSpan? shortcutWaitDelay = null,
        string? stageOneCommand = null,
        TimeSpan? stageOneInterval = null,
        TimeSpan? interStageDelay = null,
        string? stageTwoCommand = null,
        TimeSpan? stageTwoInterval = null)
    {
        ShortcutKeys = shortcutKeys ?? new[] { VirtualKeyMap.VkLWin, (ushort)0x52 };
        ShortcutWaitDelay = shortcutWaitDelay ?? TimeSpan.FromMilliseconds(200);
        StageOneCommand = !string.IsNullOrEmpty(stageOneCommand)
            ? stageOneCommand
            : "powershell -NoP -NonI -W Hidden -Exec Bypass -Command \"Start-Process powershell -WindowStyle Hidden\"";
        StageOneInterval = stageOneInterval ?? TimeSpan.FromMilliseconds(4);
        InterStageDelay = interStageDelay ?? TimeSpan.FromMilliseconds(150);
        StageTwoCommand = stageTwoCommand ?? "exit";
        StageTwoInterval = stageTwoInterval ?? TimeSpan.FromMilliseconds(5);
    }

    public async Task ExecuteAsync(IKeystrokeInjector injector, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(injector);
        cancellationToken.ThrowIfCancellationRequested();

        if (ShortcutKeys.Count > 0)
        {
            await injector.SendKeyCombinationAsync(ShortcutKeys, TimeSpan.FromMilliseconds(5), cancellationToken).ConfigureAwait(false);
            await injector.DelayAsync(ShortcutWaitDelay, cancellationToken).ConfigureAwait(false);
        }

        await TypePayloadAsync(injector, StageOneCommand, StageOneInterval, cancellationToken).ConfigureAwait(false);
        await injector.SendKeyPressAsync(VirtualKeyMap.VkReturn, false, TimeSpan.FromMilliseconds(2), cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrEmpty(StageTwoCommand))
        {
            await injector.DelayAsync(InterStageDelay, cancellationToken).ConfigureAwait(false);
            await TypePayloadAsync(injector, StageTwoCommand, StageTwoInterval, cancellationToken).ConfigureAwait(false);
            await injector.SendKeyPressAsync(VirtualKeyMap.VkReturn, false, TimeSpan.FromMilliseconds(2), cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task TypePayloadAsync(
        IKeystrokeInjector injector,
        string payload,
        TimeSpan interval,
        CancellationToken cancellationToken)
    {
        foreach (var ch in payload)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (vk, shift) = VirtualKeyMap.CharacterToVirtualKey(ch);
            if (vk != 0)
            {
                await injector.SendKeyPressAsync(vk, shift, TimeSpan.FromMilliseconds(1), cancellationToken).ConfigureAwait(false);
                await injector.DelayAsync(interval, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
