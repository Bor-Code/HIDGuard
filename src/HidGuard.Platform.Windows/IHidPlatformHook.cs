namespace HidGuard.Platform.Windows;

public interface IHidPlatformHook
{
    bool IsActive { get; }
}
