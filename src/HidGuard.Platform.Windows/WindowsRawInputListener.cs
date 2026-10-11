using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input;
using Windows.Win32.UI.WindowsAndMessaging;
using HidGuard.Core.Domain;
using HidGuard.Core.Input;

namespace HidGuard.Platform.Windows;

public sealed class WindowsRawInputListener : IKeystrokeSource
{
    private readonly object syncLock = new();
    private readonly RawInputDeviceResolver resolver;
    private NativeMessageWindow? messageWindow;
    private bool isRunning;
    private bool disposed;

    public event Action<KeystrokeEvent>? KeystrokeReceived;
    public event Action<DeviceIdentifier>? DeviceConnected;
    public event Action<DeviceIdentifier>? DeviceDisconnected;

    public bool IsRunning
    {
        get
        {
            lock (syncLock)
            {
                return isRunning;
            }
        }
    }

    public WindowsRawInputListener()
    {
        resolver = new RawInputDeviceResolver();
    }

    internal WindowsRawInputListener(RawInputDeviceResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        this.resolver = resolver;
    }

    public IReadOnlyList<DeviceIdentifier> GetConnectedKeyboards()
    {
        return resolver.EnumerateKeyboards();
    }

    public unsafe void StartListening()
    {
        lock (syncLock)
        {
            ObjectDisposedException.ThrowIf(disposed, this);

            if (isRunning)
            {
                return;
            }

            messageWindow = new NativeMessageWindow();
            messageWindow.MessageHandler = HandleWindowMessage;

            var rawInputDevice = new RAWINPUTDEVICE
            {
                usUsagePage = 0x01,
                usUsage = 0x06,
                dwFlags = RAWINPUTDEVICE_FLAGS.RIDEV_INPUTSINK | RAWINPUTDEVICE_FLAGS.RIDEV_DEVNOTIFY,
                hwndTarget = messageWindow.Handle
            };

            var registered = PInvoke.RegisterRawInputDevices(&rawInputDevice, 1, (uint)sizeof(RAWINPUTDEVICE));
            if (!registered)
            {
                var lastError = Marshal.GetLastWin32Error();
                messageWindow.Dispose();
                messageWindow = null;
                throw new WindowsRawInputException("Failed to register Raw Input devices for keyboard.", lastError);
            }

            isRunning = true;
        }
    }

    public unsafe void StopListening()
    {
        lock (syncLock)
        {
            if (!isRunning)
            {
                return;
            }

            var unregisterDevice = new RAWINPUTDEVICE
            {
                usUsagePage = 0x01,
                usUsage = 0x06,
                dwFlags = RAWINPUTDEVICE_FLAGS.RIDEV_REMOVE,
                hwndTarget = HWND.Null
            };

            _ = PInvoke.RegisterRawInputDevices(&unregisterDevice, 1, (uint)sizeof(RAWINPUTDEVICE));

            messageWindow?.Dispose();
            messageWindow = null;
            isRunning = false;
        }
    }

    private unsafe LRESULT? HandleWindowMessage(HWND window, uint message, WPARAM wParam, LPARAM lParam)
    {
        if (message == PInvoke.WM_INPUT)
        {
            var rawInputHandle = new HRAWINPUT(lParam.Value);
            var size = 0u;

            _ = PInvoke.GetRawInputData(
                rawInputHandle,
                RAW_INPUT_DATA_COMMAND_FLAGS.RID_INPUT,
                null,
                &size,
                (uint)sizeof(RAWINPUTHEADER));

            if (size > 0)
            {
                var buffer = new byte[size];
                fixed (byte* pBuffer = buffer)
                {
                    var bytesRead = PInvoke.GetRawInputData(
                        rawInputHandle,
                        RAW_INPUT_DATA_COMMAND_FLAGS.RID_INPUT,
                        pBuffer,
                        &size,
                        (uint)sizeof(RAWINPUTHEADER));

                    if (bytesRead == size)
                    {
                        var rawInput = (RAWINPUT*)pBuffer;
                        if (rawInput->header.dwType == (uint)RID_DEVICE_INFO_TYPE.RIM_TYPEKEYBOARD)
                        {
                            var keyboard = rawInput->data.keyboard;
                            var vKey = (uint)keyboard.VKey;
                            var isKeyUp = (keyboard.Flags & PInvoke.RI_KEY_BREAK) != 0;
                            var state = isKeyUp ? KeyTransitionState.KeyUp : KeyTransitionState.KeyDown;
                            var hDevice = rawInput->header.hDevice;
                            var isSynthetic = (nint)hDevice.Value == 0;
                            var device = resolver.Resolve(hDevice);

                            var keystrokeEvent = new KeystrokeEvent(
                                device,
                                DateTimeOffset.UtcNow,
                                state,
                                vKey,
                                isSynthetic);

                            KeystrokeReceived?.Invoke(keystrokeEvent);
                        }
                    }
                }
            }

            return null;
        }

        if (message == PInvoke.WM_INPUT_DEVICE_CHANGE)
        {
            var changeType = (uint)wParam.Value;
            var hDevice = new HANDLE(lParam.Value);

            if (changeType == (uint)PInvoke.GIDC_ARRIVAL)
            {
                var device = resolver.Resolve(hDevice);
                if (device != DeviceIdentifier.Synthetic)
                {
                    DeviceConnected?.Invoke(device);
                }
            }
            else if (changeType == (uint)PInvoke.GIDC_REMOVAL)
            {
                var device = resolver.Resolve(hDevice);
                resolver.Invalidate(hDevice);
                if (device != DeviceIdentifier.Synthetic)
                {
                    DeviceDisconnected?.Invoke(device);
                }
            }

            return new LRESULT(0);
        }

        return null;
    }

    public void Dispose()
    {
        lock (syncLock)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            StopListening();
        }
    }
}
