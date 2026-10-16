namespace HidGuard.Core.Configuration;

public interface IStartupService
{
    bool IsStartWithWindowsEnabled();
    bool SetStartWithWindows(bool enabled);
}
