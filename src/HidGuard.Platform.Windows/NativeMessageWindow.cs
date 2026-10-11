using System.ComponentModel;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace HidGuard.Platform.Windows;

internal sealed class NativeMessageWindow : IDisposable
{
    private readonly string className;
    private readonly Thread thread;
    private readonly ManualResetEventSlim readyEvent = new(false);
    private readonly WNDPROC wndProcDelegate;
    private HWND hwnd;
    private bool disposed;
    private Exception? initializationException;

    public HWND Handle => hwnd;
    public Func<HWND, uint, WPARAM, LPARAM, LRESULT?>? MessageHandler { get; set; }

    public NativeMessageWindow()
    {
        className = $"HidGuard_MsgWin_{Guid.NewGuid():N}";
        wndProcDelegate = WindowProcedure;

        thread = new Thread(RunMessageLoop)
        {
            IsBackground = true,
            Name = "HidGuard.RawInput.MessageLoop"
        };
        thread.Start();

        readyEvent.Wait();

        if (initializationException != null)
        {
            throw new WindowsRawInputException("Failed to initialize message window.", initializationException);
        }
    }

    private unsafe void RunMessageLoop()
    {
        try
        {
            fixed (char* pClassName = className)
            {
                var wndClass = new WNDCLASSEXW
                {
                    cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
                    lpfnWndProc = wndProcDelegate,
                    lpszClassName = (char*)pClassName
                };

                var atom = PInvoke.RegisterClassEx(wndClass);
                if (atom == 0)
                {
                    var lastError = Marshal.GetLastWin32Error();
                    initializationException = new Win32Exception(lastError);
                    readyEvent.Set();
                    return;
                }

                hwnd = PInvoke.CreateWindowEx(
                    0,
                    className,
                    "HIDGuardMessageHost",
                    0,
                    0,
                    0,
                    0,
                    0,
                    new HWND(new nint(-3)),
                    default,
                    default,
                    null);

                if (hwnd.IsNull)
                {
                    var lastError = Marshal.GetLastWin32Error();
                    initializationException = new Win32Exception(lastError);
                    readyEvent.Set();
                    return;
                }
            }

            readyEvent.Set();

            MSG msg;
            while (PInvoke.GetMessage(&msg, HWND.Null, 0, 0))
            {
                _ = PInvoke.TranslateMessage(&msg);
                _ = PInvoke.DispatchMessage(&msg);
            }
        }
        catch (Exception ex)
        {
            initializationException = ex;
            readyEvent.Set();
        }
        finally
        {
            if (!hwnd.IsNull)
            {
                _ = PInvoke.DestroyWindow(hwnd);
                hwnd = HWND.Null;
            }

            _ = PInvoke.UnregisterClass(className, default);
        }
    }

    private LRESULT WindowProcedure(HWND window, uint message, WPARAM wParam, LPARAM lParam)
    {
        if (MessageHandler != null)
        {
            var customResult = MessageHandler(window, message, wParam, lParam);
            if (customResult.HasValue)
            {
                return customResult.Value;
            }
        }

        return PInvoke.DefWindowProc(window, message, wParam, lParam);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;

        if (!hwnd.IsNull)
        {
            _ = PInvoke.PostMessage(hwnd, PInvoke.WM_QUIT, default, default);
        }

        if (thread.IsAlive)
        {
            thread.Join(TimeSpan.FromSeconds(2));
        }

        readyEvent.Dispose();
    }
}
