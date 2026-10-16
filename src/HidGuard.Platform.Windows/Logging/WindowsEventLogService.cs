using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using HidGuard.Core.Audit;

namespace HidGuard.Platform.Windows.Logging;

[SupportedOSPlatform("windows")]
public sealed class WindowsEventLogService : IWindowsEventLogService
{
    private static readonly Action<ILogger, int, string, Exception?> LogEventWrittenDelegate =
        LoggerMessage.Define<int, string>(
            LogLevel.Debug,
            new EventId(1, nameof(WindowsEventLogService)),
            "Security event {EventId} written to Windows Event Log: {Message}");

    private static readonly Action<ILogger, int, string, Exception?> LogEventFailedDelegate =
        LoggerMessage.Define<int, string>(
            LogLevel.Warning,
            new EventId(2, nameof(WindowsEventLogService)),
            "Failed to write security event {EventId} to Windows Event Log: {Message}");

    private const string DefaultSourceName = "HIDGuard";
    private const string DefaultLogName = "Application";

    private readonly string sourceName;
    private readonly string logName;
    private readonly ILogger<WindowsEventLogService> logger;
    private bool isSourceRegistered;

    public WindowsEventLogService(
        string sourceName = DefaultSourceName,
        string logName = DefaultLogName,
        ILogger<WindowsEventLogService>? logger = null)
    {
        this.sourceName = string.IsNullOrWhiteSpace(sourceName) ? DefaultSourceName : sourceName;
        this.logName = string.IsNullOrWhiteSpace(logName) ? DefaultLogName : logName;
        this.logger = logger ?? NullLogger<WindowsEventLogService>.Instance;

        EnsureSourceRegistered();
    }

    public bool WriteSecurityEvent(int eventId, SecurityEventSeverity severity, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        try
        {
            var entryType = severity switch
            {
                SecurityEventSeverity.Information => EventLogEntryType.Information,
                SecurityEventSeverity.Warning => EventLogEntryType.Warning,
                SecurityEventSeverity.Error => EventLogEntryType.Error,
                _ => EventLogEntryType.Information
            };

            var targetSource = isSourceRegistered ? sourceName : DefaultLogName;
            EventLog.WriteEntry(targetSource, message, entryType, eventId);
            LogEventWrittenDelegate(logger, eventId, message, null);
            return true;
        }
        catch (Exception ex)
        {
            LogEventFailedDelegate(logger, eventId, ex.Message, ex);
            return false;
        }
    }

    private void EnsureSourceRegistered()
    {
        try
        {
            if (!EventLog.SourceExists(sourceName))
            {
                EventLog.CreateEventSource(sourceName, logName);
            }

            isSourceRegistered = true;
        }
        catch
        {
            isSourceRegistered = false;
        }
    }
}
