using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Windows.Win32;
using HidGuard.Core.Countermeasures;

namespace HidGuard.Platform.Windows;

public sealed class WindowsWorkstationLocker : IWorkstationLocker
{
    private static readonly Action<ILogger, Exception?> LogLockSuccessDelegate =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(1, nameof(WindowsWorkstationLocker)),
            "Workstation lock successfully initiated.");

    private static readonly Action<ILogger, Exception?> LogLockFailedDelegate =
        LoggerMessage.Define(
            LogLevel.Warning,
            new EventId(2, nameof(WindowsWorkstationLocker)),
            "LockWorkStation call failed or returned false.");

    private readonly ILogger<WindowsWorkstationLocker> logger;

    public WindowsWorkstationLocker(ILogger<WindowsWorkstationLocker>? logger = null)
    {
        this.logger = logger ?? NullLogger<WindowsWorkstationLocker>.Instance;
    }

    public bool LockWorkstation()
    {
        try
        {
            var result = PInvoke.LockWorkStation();
            if (result)
            {
                LogLockSuccessDelegate(logger, null);
                return true;
            }

            LogLockFailedDelegate(logger, new Win32Exception());
            return false;
        }
        catch (Exception ex)
        {
            LogLockFailedDelegate(logger, ex);
            return false;
        }
    }
}
