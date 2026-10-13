using HidGuard.Simulator.DuckyScript;
using HidGuard.Simulator.Injection;

namespace HidGuard.Simulator.Profiles;

public sealed class DuckyScriptAttackProfile : IAttackProfile
{
    private readonly IReadOnlyList<DuckyScriptInstruction> instructions;
    private TimeSpan defaultDelay = TimeSpan.FromMilliseconds(5);

    public AttackProfileType ProfileType => AttackProfileType.DuckyScript;
    public string Name => "DuckyScript Payload Execution";
    public IReadOnlyList<DuckyScriptInstruction> Instructions => instructions;

    public DuckyScriptAttackProfile(IReadOnlyList<DuckyScriptInstruction> instructions)
    {
        ArgumentNullException.ThrowIfNull(instructions);
        this.instructions = instructions;
    }

    public DuckyScriptAttackProfile(string scriptText)
    {
        ArgumentNullException.ThrowIfNull(scriptText);
        instructions = DuckyScriptParser.Parse(scriptText);
    }

    public async Task ExecuteAsync(IKeystrokeInjector injector, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(injector);
        cancellationToken.ThrowIfCancellationRequested();

        foreach (var instruction in instructions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            switch (instruction.Type)
            {
                case DuckyScriptCommandType.DefaultDelay:
                    defaultDelay = instruction.Duration;
                    break;

                case DuckyScriptCommandType.Delay:
                    await injector.DelayAsync(instruction.Duration, cancellationToken).ConfigureAwait(false);
                    break;

                case DuckyScriptCommandType.KeyPress:
                    await injector.SendKeyPressAsync(instruction.VirtualKey, instruction.Shift, TimeSpan.FromMilliseconds(2), cancellationToken).ConfigureAwait(false);
                    await injector.DelayAsync(defaultDelay, cancellationToken).ConfigureAwait(false);
                    break;

                case DuckyScriptCommandType.KeyCombination:
                    await injector.SendKeyCombinationAsync(instruction.CombinationKeys, TimeSpan.FromMilliseconds(5), cancellationToken).ConfigureAwait(false);
                    await injector.DelayAsync(defaultDelay, cancellationToken).ConfigureAwait(false);
                    break;

                case DuckyScriptCommandType.TextPayload:
                    await TypeStringAsync(injector, instruction.Text, cancellationToken).ConfigureAwait(false);
                    break;

                case DuckyScriptCommandType.TextLinePayload:
                    await TypeStringAsync(injector, instruction.Text, cancellationToken).ConfigureAwait(false);
                    await injector.SendKeyPressAsync(VirtualKeyMap.VkReturn, false, TimeSpan.FromMilliseconds(2), cancellationToken).ConfigureAwait(false);
                    await injector.DelayAsync(defaultDelay, cancellationToken).ConfigureAwait(false);
                    break;

                case DuckyScriptCommandType.Repeat:
                    break;
            }
        }
    }

    private async Task TypeStringAsync(IKeystrokeInjector injector, string text, CancellationToken cancellationToken)
    {
        foreach (var ch in text)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (vk, shift) = VirtualKeyMap.CharacterToVirtualKey(ch);
            if (vk != 0)
            {
                await injector.SendKeyPressAsync(vk, shift, TimeSpan.FromMilliseconds(1), cancellationToken).ConfigureAwait(false);
                await injector.DelayAsync(defaultDelay, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
