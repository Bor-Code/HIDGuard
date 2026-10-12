using System.Windows.Forms;

namespace HidGuard.App.Services;

public interface ITrayIconService : IDisposable
{
    void Initialize(
        Action onOpenRequested,
        Action onToggleProtectionRequested,
        Action onSettingsRequested,
        Action onAuditRequested,
        Action onExitRequested);

    void UpdateProtectionState(bool isProtected);

    void ShowNotification(string title, string message, ToolTipIcon icon = ToolTipIcon.Info);
}
