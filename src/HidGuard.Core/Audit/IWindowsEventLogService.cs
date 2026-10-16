namespace HidGuard.Core.Audit;

public interface IWindowsEventLogService
{
    bool WriteSecurityEvent(int eventId, SecurityEventSeverity severity, string message);
}
