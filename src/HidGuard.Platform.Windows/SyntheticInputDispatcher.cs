using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace HidGuard.Platform.Windows;

internal static class SyntheticInputDispatcher
{
    public static unsafe uint SendKey(ushort virtualKey, bool isKeyUp)
    {
        var flags = isKeyUp ? KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP : (KEYBD_EVENT_FLAGS)0;
        var input = new INPUT
        {
            type = INPUT_TYPE.INPUT_KEYBOARD,
            Anonymous = new INPUT._Anonymous_e__Union
            {
                ki = new KEYBDINPUT
                {
                    wVk = (VIRTUAL_KEY)virtualKey,
                    wScan = 0,
                    dwFlags = flags,
                    time = 0,
                    dwExtraInfo = 0
                }
            }
        };

        return PInvoke.SendInput(1, &input, sizeof(INPUT));
    }
}
