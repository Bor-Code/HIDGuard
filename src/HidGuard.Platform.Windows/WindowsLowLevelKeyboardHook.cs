using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using HidGuard.Core.Countermeasures;

namespace HidGuard.Platform.Windows;

public sealed class WindowsLowLevelKeyboardHook : IKeystrokeInterceptor
{
    private static readonly Action<ILogger, Exception?> LogHookInstalledDelegate =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(1, nameof(WindowsLowLevelKeyboardHook)),
            "Low-level keyboard hook successfully installed.");

    private static readonly Action<ILogger, Exception?> LogHookRemovedDelegate =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(2, nameof(WindowsLowLevelKeyboardHook)),
            "Low-level keyboard hook successfully unhooked.");

    private static readonly Action<ILogger, int, Exception?> LogHookInstallFailedDelegate =
        LoggerMessage.Define<int>(
            LogLevel.Error,
            new EventId(3, nameof(WindowsLowLevelKeyboardHook)),
            "Failed to install low-level keyboard hook. Error code: {ErrorCode}");

    private readonly ILogger<WindowsLowLevelKeyboardHook> logger;
    private readonly HOOKPROC hookProcDelegate;
    private readonly object syncRoot = new();

    private Thread? hookThread;
    private uint hookThreadId;
    private UnhookWindowsHookExSafeHandle? hookHandle;
    private volatile bool isSuppressionActive;
    private long suppressionExpiryTicks;
    private bool isRunning;
    private bool disposed;

    public event EventHandler<int>? KeySuppressed;

    public bool IsSuppressionActive
    {
        get => isSuppressionActive || IsTemporarySuppressionActive();
        set => isSuppressionActive = value;
    }

    public WindowsLowLevelKeyboardHook(ILogger<WindowsLowLevelKeyboardHook>? logger = null)
    {
        this.logger = logger ?? NullLogger<WindowsLowLevelKeyboardHook>.Instance;
        hookProcDelegate = LowLevelKeyboardCallback;
    }

    public void StartInterception()
    {
        lock (syncRoot)
        {
            if (isRunning)
            {
                return;
            }

            using var initializedEvent = new ManualResetEventSlim(false);
            Exception? threadException = null;

            hookThread = new Thread(() =>
            {
                try
                {
                    hookThreadId = PInvoke.GetCurrentThreadId();
                    var moduleHandle = PInvoke.GetModuleHandle((string?)null);

                    hookHandle = PInvoke.SetWindowsHookEx(
                        WINDOWS_HOOK_ID.WH_KEYBOARD_LL,
                        hookProcDelegate,
                        moduleHandle,
                        0);

                    if (hookHandle == null || hookHandle.IsInvalid)
                    {
                        var lastError = Marshal.GetLastWin32Error();
                        LogHookInstallFailedDelegate(logger, lastError, new Win32Exception(lastError));
                        threadException = new Win32Exception(lastError, "Failed to install WH_KEYBOARD_LL hook.");
                        return;
                    }

                    LogHookInstalledDelegate(logger, null);
                    isRunning = true;
                    initializedEvent.Set();

                    unsafe
                    {
                        MSG msg;
                        while (PInvoke.GetMessage(&msg, HWND.Null, 0, 0))
                        {
                            _ = PInvoke.TranslateMessage(&msg);
                            _ = PInvoke.DispatchMessage(&msg);
                        }
                    }
                }
                finally
                {
                    if (hookHandle != null && !hookHandle.IsInvalid)
                    {
                        hookHandle.Dispose();
                        hookHandle = null;
                        LogHookRemovedDelegate(logger, null);
                    }

                    isRunning = false;
                    initializedEvent.Set();
                }
            })
            {
                IsBackground = true,
                Name = "HidGuard.LowLevelKeyboardHook"
            };

            hookThread.Start();
            initializedEvent.Wait();

            if (threadException != null)
            {
                throw threadException;
            }
        }
    }

    public void StopInterception()
    {
        lock (syncRoot)
        {
            if (!isRunning && hookThread == null)
            {
                return;
            }

            if (hookThreadId != 0)
            {
                PInvoke.PostThreadMessage(hookThreadId, PInvoke.WM_QUIT, default, default);
            }

            if (hookThread != null && hookThread.IsAlive)
            {
                hookThread.Join(TimeSpan.FromSeconds(2));
                hookThread = null;
            }

            isRunning = false;
            hookThreadId = 0;
        }
    }

    public void SuppressTemporarily(TimeSpan duration)
    {
        var expiry = DateTimeOffset.UtcNow.Add(duration).Ticks;
        Interlocked.Exchange(ref suppressionExpiryTicks, expiry);
    }

    private bool IsTemporarySuppressionActive()
    {
        var expiry = Interlocked.Read(ref suppressionExpiryTicks);
        return expiry > 0 && DateTimeOffset.UtcNow.Ticks < expiry;
    }

    private LRESULT LowLevelKeyboardCallback(int nCode, WPARAM wParam, LPARAM lParam)
    {
        if (nCode >= 0)
        {
            if (IsSuppressionActive)
            {
                var vkCode = 0;
                unsafe
                {
                    if (lParam.Value != 0)
                    {
                        var pKbd = (KBDLLHOOKSTRUCT*)lParam.Value;
                        vkCode = (int)pKbd->vkCode;
                    }
                }

                if (vkCode != 0)
                {
                    ThreadPool.QueueUserWorkItem(_ =>
                    {
                        KeySuppressed?.Invoke(this, vkCode);
                    });
                }

                return new LRESULT(1);
            }
        }

        var hhook = hookHandle != null && !hookHandle.IsInvalid ? new HHOOK(hookHandle.DangerousGetHandle()) : default;
        return PInvoke.CallNextHookEx(hhook, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        StopInterception();
        disposed = true;
    }
}
