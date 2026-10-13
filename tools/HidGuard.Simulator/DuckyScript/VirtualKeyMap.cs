namespace HidGuard.Simulator.DuckyScript;

public static class VirtualKeyMap
{
    public const ushort VkBack = 0x08;
    public const ushort VkTab = 0x09;
    public const ushort VkReturn = 0x0D;
    public const ushort VkShift = 0x10;
    public const ushort VkControl = 0x11;
    public const ushort VkMenu = 0x12;
    public const ushort VkPause = 0x13;
    public const ushort VkCapital = 0x14;
    public const ushort VkEscape = 0x1B;
    public const ushort VkSpace = 0x20;
    public const ushort VkPrior = 0x21;
    public const ushort VkNext = 0x22;
    public const ushort VkEnd = 0x23;
    public const ushort VkHome = 0x24;
    public const ushort VkLeft = 0x25;
    public const ushort VkUp = 0x26;
    public const ushort VkRight = 0x27;
    public const ushort VkDown = 0x28;
    public const ushort VkSnapshot = 0x2C;
    public const ushort VkInsert = 0x2D;
    public const ushort VkDelete = 0x2E;
    public const ushort VkLWin = 0x5B;
    public const ushort VkRWin = 0x5C;
    public const ushort VkApps = 0x5D;

    public const ushort VkOem1 = 0xBA;
    public const ushort VkOemPlus = 0xBB;
    public const ushort VkOemComma = 0xBC;
    public const ushort VkOemMinus = 0xBD;
    public const ushort VkOemPeriod = 0xBE;
    public const ushort VkOem2 = 0xBF;
    public const ushort VkOem3 = 0xC0;
    public const ushort VkOem4 = 0xDB;
    public const ushort VkOem5 = 0xDC;
    public const ushort VkOem6 = 0xDD;
    public const ushort VkOem7 = 0xDE;

    private static readonly Dictionary<string, ushort> NamedKeyMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ENTER"] = VkReturn,
        ["RETURN"] = VkReturn,
        ["ESCAPE"] = VkEscape,
        ["ESC"] = VkEscape,
        ["BACKSPACE"] = VkBack,
        ["TAB"] = VkTab,
        ["SPACE"] = VkSpace,
        ["CAPSLOCK"] = VkCapital,
        ["CAPS"] = VkCapital,
        ["PAGEUP"] = VkPrior,
        ["PAGEDOWN"] = VkNext,
        ["END"] = VkEnd,
        ["HOME"] = VkHome,
        ["LEFT"] = VkLeft,
        ["LEFTARROW"] = VkLeft,
        ["UP"] = VkUp,
        ["UPARROW"] = VkUp,
        ["RIGHT"] = VkRight,
        ["RIGHTARROW"] = VkRight,
        ["DOWN"] = VkDown,
        ["DOWNARROW"] = VkDown,
        ["PRINTSCREEN"] = VkSnapshot,
        ["INSERT"] = VkInsert,
        ["DELETE"] = VkDelete,
        ["DEL"] = VkDelete,
        ["GUI"] = VkLWin,
        ["WINDOWS"] = VkLWin,
        ["WIN"] = VkLWin,
        ["CTRL"] = VkControl,
        ["CONTROL"] = VkControl,
        ["ALT"] = VkMenu,
        ["SHIFT"] = VkShift,
        ["MENU"] = VkApps,
        ["APP"] = VkApps,
        ["PAUSE"] = VkPause,
        ["BREAK"] = VkPause
    };

    static VirtualKeyMap()
    {
        for (var i = 1; i <= 24; i++)
        {
            NamedKeyMap[$"F{i}"] = (ushort)(0x6F + i);
        }
    }

    public static bool TryResolveKeyName(string token, out ushort virtualKey)
    {
        ArgumentNullException.ThrowIfNull(token);

        if (NamedKeyMap.TryGetValue(token, out virtualKey))
        {
            return true;
        }

        if (token.Length == 1)
        {
            var (vk, _) = CharacterToVirtualKey(token[0]);
            virtualKey = vk;
            return true;
        }

        virtualKey = 0;
        return false;
    }

    public static (ushort VirtualKey, bool Shift) CharacterToVirtualKey(char character)
    {
        if (character is >= 'a' and <= 'z')
        {
            return ((ushort)(0x41 + (character - 'a')), false);
        }

        if (character is >= 'A' and <= 'Z')
        {
            return ((ushort)(0x41 + (character - 'A')), true);
        }

        if (character is >= '0' and <= '9')
        {
            return ((ushort)(0x30 + (character - '0')), false);
        }

        return character switch
        {
            ' ' => (VkSpace, false),
            '\t' => (VkTab, false),
            '\r' or '\n' => (VkReturn, false),
            '!' => (0x31, true),
            '@' => (0x32, true),
            '#' => (0x33, true),
            '$' => (0x34, true),
            '%' => (0x35, true),
            '^' => (0x36, true),
            '&' => (0x37, true),
            '*' => (0x38, true),
            '(' => (0x39, true),
            ')' => (0x30, true),
            '-' => (VkOemMinus, false),
            '_' => (VkOemMinus, true),
            '=' => (VkOemPlus, false),
            '+' => (VkOemPlus, true),
            '[' => (VkOem4, false),
            '{' => (VkOem4, true),
            ']' => (VkOem6, false),
            '}' => (VkOem6, true),
            '\\' => (VkOem5, false),
            '|' => (VkOem5, true),
            ';' => (VkOem1, false),
            ':' => (VkOem1, true),
            '\'' => (VkOem7, false),
            '"' => (VkOem7, true),
            ',' => (VkOemComma, false),
            '<' => (VkOemComma, true),
            '.' => (VkOemPeriod, false),
            '>' => (VkOemPeriod, true),
            '/' => (VkOem2, false),
            '?' => (VkOem2, true),
            '`' => (VkOem3, false),
            '~' => (VkOem3, true),
            _ => (0, false)
        };
    }
}
