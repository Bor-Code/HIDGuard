using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Win32;
using HidGuard.Core.Configuration;

namespace HidGuard.Platform.Windows;

public sealed class WindowsStartupService : IStartupService
{
    private const string RunRegistrySubKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppValueName = "HIDGuard";

    private static readonly Action<ILogger, Exception?> LogRegistryReadFailedDelegate =
        LoggerMessage.Define(
            LogLevel.Warning,
            new EventId(1, nameof(WindowsStartupService)),
            "Failed to read HKCU Run registry value.");

    private static readonly Action<ILogger, Exception?> LogEmptyExecutablePathDelegate =
        LoggerMessage.Define(
            LogLevel.Warning,
            new EventId(2, nameof(WindowsStartupService)),
            "Cannot enable start with Windows because executable path is empty.");

    private static readonly Action<ILogger, Exception?> LogRegistryUpdateFailedDelegate =
        LoggerMessage.Define(
            LogLevel.Warning,
            new EventId(3, nameof(WindowsStartupService)),
            "Failed to update HKCU Run registry value.");

    private readonly IRegistryKeyAccessor registryAccessor;
    private readonly string executablePath;
    private readonly ILogger<WindowsStartupService> logger;

    public WindowsStartupService(ILogger<WindowsStartupService>? logger = null)
        : this(null, null, logger)
    {
    }

    internal WindowsStartupService(
        IRegistryKeyAccessor? registryAccessor = null,
        string? executablePath = null,
        ILogger<WindowsStartupService>? logger = null)
    {
        this.registryAccessor = registryAccessor ?? new CurrentUserRunRegistryAccessor();
        this.executablePath = executablePath ?? Environment.ProcessPath ?? string.Empty;
        this.logger = logger ?? NullLogger<WindowsStartupService>.Instance;
    }

    public bool IsStartWithWindowsEnabled()
    {
        try
        {
            var value = registryAccessor.GetValue(RunRegistrySubKey, AppValueName);
            return !string.IsNullOrWhiteSpace(value);
        }
        catch (Exception ex)
        {
            LogRegistryReadFailedDelegate(logger, ex);
            return false;
        }
    }

    public bool SetStartWithWindows(bool enabled)
    {
        try
        {
            if (enabled)
            {
                if (string.IsNullOrWhiteSpace(executablePath))
                {
                    LogEmptyExecutablePathDelegate(logger, null);
                    return false;
                }

                var command = $"\"{executablePath}\" --minimized";
                registryAccessor.SetValue(RunRegistrySubKey, AppValueName, command);
            }
            else
            {
                registryAccessor.DeleteValue(RunRegistrySubKey, AppValueName);
            }

            return true;
        }
        catch (Exception ex)
        {
            LogRegistryUpdateFailedDelegate(logger, ex);
            return false;
        }
    }
}

internal interface IRegistryKeyAccessor
{
    string? GetValue(string subKey, string valueName);
    void SetValue(string subKey, string valueName, string value);
    void DeleteValue(string subKey, string valueName);
}

internal sealed class CurrentUserRunRegistryAccessor : IRegistryKeyAccessor
{
    public string? GetValue(string subKey, string valueName)
    {
        using var key = Registry.CurrentUser.OpenSubKey(subKey, false);
        return key?.GetValue(valueName) as string;
    }

    public void SetValue(string subKey, string valueName, string value)
    {
        using var key = Registry.CurrentUser.CreateSubKey(subKey, true);
        key?.SetValue(valueName, value, RegistryValueKind.String);
    }

    public void DeleteValue(string subKey, string valueName)
    {
        using var key = Registry.CurrentUser.OpenSubKey(subKey, true);
        key?.DeleteValue(valueName, false);
    }
}
