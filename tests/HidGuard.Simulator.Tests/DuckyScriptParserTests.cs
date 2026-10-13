using Shouldly;
using Xunit;
using HidGuard.Simulator.DuckyScript;

namespace HidGuard.Simulator.Tests;

public sealed class DuckyScriptParserTests
{
    [Fact]
    public void ParseStringCommandShouldCreateStringInstruction()
    {
        var script = "STRING Hello World!";
        var instructions = DuckyScriptParser.Parse(script);

        instructions.Count.ShouldBe(1);
        instructions[0].Type.ShouldBe(DuckyScriptCommandType.TextPayload);
        instructions[0].Text.ShouldBe("Hello World!");
    }

    [Fact]
    public void ParseStringLnCommandShouldCreateStringLineInstruction()
    {
        var script = "STRINGLN dir C:\\";
        var instructions = DuckyScriptParser.Parse(script);

        instructions.Count.ShouldBe(1);
        instructions[0].Type.ShouldBe(DuckyScriptCommandType.TextLinePayload);
        instructions[0].Text.ShouldBe("dir C:\\");
    }

    [Fact]
    public void ParseDelayCommandShouldCreateDelayInstruction()
    {
        var script = "DELAY 500";
        var instructions = DuckyScriptParser.Parse(script);

        instructions.Count.ShouldBe(1);
        instructions[0].Type.ShouldBe(DuckyScriptCommandType.Delay);
        instructions[0].Duration.ShouldBe(TimeSpan.FromMilliseconds(500));
    }

    [Fact]
    public void ParseDefaultDelayCommandShouldCreateDefaultDelayInstruction()
    {
        var script = "DEFAULT_DELAY 25\nDEFAULTDELAY 50";
        var instructions = DuckyScriptParser.Parse(script);

        instructions.Count.ShouldBe(2);
        instructions[0].Type.ShouldBe(DuckyScriptCommandType.DefaultDelay);
        instructions[0].Duration.ShouldBe(TimeSpan.FromMilliseconds(25));
        instructions[1].Type.ShouldBe(DuckyScriptCommandType.DefaultDelay);
        instructions[1].Duration.ShouldBe(TimeSpan.FromMilliseconds(50));
    }

    [Fact]
    public void ParseSingleKeysShouldCreateKeyPressInstruction()
    {
        var script = "ENTER\nESCAPE\nTAB\nSPACE\nF5\nCAPSLOCK";
        var instructions = DuckyScriptParser.Parse(script);

        instructions.Count.ShouldBe(6);
        instructions[0].VirtualKey.ShouldBe(VirtualKeyMap.VkReturn);
        instructions[1].VirtualKey.ShouldBe(VirtualKeyMap.VkEscape);
        instructions[2].VirtualKey.ShouldBe(VirtualKeyMap.VkTab);
        instructions[3].VirtualKey.ShouldBe(VirtualKeyMap.VkSpace);
        instructions[4].VirtualKey.ShouldBe((ushort)0x74);
        instructions[5].VirtualKey.ShouldBe(VirtualKeyMap.VkCapital);
    }

    [Fact]
    public void ParseModifierCombinationsShouldCreateKeyCombinationInstruction()
    {
        var script = "GUI r\nCTRL ALT DELETE\nALT F4";
        var instructions = DuckyScriptParser.Parse(script);

        instructions.Count.ShouldBe(3);
        instructions[0].Type.ShouldBe(DuckyScriptCommandType.KeyCombination);
        instructions[0].CombinationKeys.Count.ShouldBe(2);
        instructions[0].CombinationKeys[0].ShouldBe(VirtualKeyMap.VkLWin);
        instructions[0].CombinationKeys[1].ShouldBe((ushort)0x52);

        instructions[1].Type.ShouldBe(DuckyScriptCommandType.KeyCombination);
        instructions[1].CombinationKeys.Count.ShouldBe(3);
        instructions[1].CombinationKeys[0].ShouldBe(VirtualKeyMap.VkControl);
        instructions[1].CombinationKeys[1].ShouldBe(VirtualKeyMap.VkMenu);
        instructions[1].CombinationKeys[2].ShouldBe(VirtualKeyMap.VkDelete);

        instructions[2].Type.ShouldBe(DuckyScriptCommandType.KeyCombination);
        instructions[2].CombinationKeys.Count.ShouldBe(2);
        instructions[2].CombinationKeys[0].ShouldBe(VirtualKeyMap.VkMenu);
        instructions[2].CombinationKeys[1].ShouldBe((ushort)0x73);
    }

    [Fact]
    public void ParseRepeatCommandShouldDuplicateLastInstruction()
    {
        var script = "ENTER\nREPEAT 3";
        var instructions = DuckyScriptParser.Parse(script);

        instructions.Count.ShouldBe(4);
        instructions.All(i => i.VirtualKey == VirtualKeyMap.VkReturn).ShouldBeTrue();
    }

    [Fact]
    public void ParseRemLinesAndEmptyLinesShouldBeIgnored()
    {
        var script = "REM This is a comment\n\n   \nREM Another comment\nSTRING Payload";
        var instructions = DuckyScriptParser.Parse(script);

        instructions.Count.ShouldBe(1);
        instructions[0].Type.ShouldBe(DuckyScriptCommandType.TextPayload);
        instructions[0].Text.ShouldBe("Payload");
    }

    [Fact]
    public void ParseInvalidDelayShouldThrowFormatException()
    {
        Should.Throw<FormatException>(() => DuckyScriptParser.Parse("DELAY invalid"));
    }

    [Fact]
    public void ParseUnknownKeyShouldThrowInvalidOperationException()
    {
        Should.Throw<InvalidOperationException>(() => DuckyScriptParser.Parse("UNKNOWN_KEY_TOKEN"));
    }

    [Theory]
    [InlineData('a', 0x41, false)]
    [InlineData('Z', 0x5A, true)]
    [InlineData('1', 0x31, false)]
    [InlineData(' ', VirtualKeyMap.VkSpace, false)]
    [InlineData('!', 0x31, true)]
    [InlineData('.', VirtualKeyMap.VkOemPeriod, false)]
    public void CharacterToVirtualKeyShouldMapCorrectly(char ch, ushort expectedVk, bool expectedShift)
    {
        var (vk, shift) = VirtualKeyMap.CharacterToVirtualKey(ch);
        vk.ShouldBe(expectedVk);
        shift.ShouldBe(expectedShift);
    }
}
