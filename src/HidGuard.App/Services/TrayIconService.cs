using System.Drawing;
using System.Windows.Forms;

namespace HidGuard.App.Services;

public sealed class TrayIconService : ITrayIconService
{
    private NotifyIcon? notifyIcon;
    private ToolStripMenuItem? statusMenuItem;
    private bool disposed;

    public void Initialize(
        Action onOpenRequested,
        Action onToggleProtectionRequested,
        Action onSettingsRequested,
        Action onAuditRequested,
        Action onExitRequested)
    {
        ArgumentNullException.ThrowIfNull(onOpenRequested);
        ArgumentNullException.ThrowIfNull(onToggleProtectionRequested);
        ArgumentNullException.ThrowIfNull(onSettingsRequested);
        ArgumentNullException.ThrowIfNull(onAuditRequested);
        ArgumentNullException.ThrowIfNull(onExitRequested);

        var contextMenu = new ContextMenuStrip();

        var openItem = new ToolStripMenuItem("Open Dashboard", null, (_, _) => onOpenRequested())
        {
            Font = new Font(contextMenu.Font, FontStyle.Bold)
        };

        statusMenuItem = new ToolStripMenuItem("Protection: Active", null, (_, _) => onToggleProtectionRequested());
        var settingsItem = new ToolStripMenuItem("Settings", null, (_, _) => onSettingsRequested());
        var auditItem = new ToolStripMenuItem("Audit Log", null, (_, _) => onAuditRequested());
        var exitItem = new ToolStripMenuItem("Exit", null, (_, _) => onExitRequested());

        contextMenu.Items.Add(openItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(statusMenuItem);
        contextMenu.Items.Add(settingsItem);
        contextMenu.Items.Add(auditItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(exitItem);

        Icon appIcon;
        try
        {
            var stream = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Assets/app.ico"))?.Stream;
            appIcon = stream != null ? new Icon(stream) : SystemIcons.Shield;
        }
        catch
        {
            appIcon = SystemIcons.Shield;
        }

        notifyIcon = new NotifyIcon
        {
            Icon = appIcon,
            Text = "HIDGuard - Hardware Security & BadUSB Mitigation",
            ContextMenuStrip = contextMenu,
            Visible = true
        };

        notifyIcon.DoubleClick += (_, _) => onOpenRequested();
    }

    public void UpdateProtectionState(bool isProtected)
    {
        if (notifyIcon == null || statusMenuItem == null)
        {
            return;
        }

        statusMenuItem.Text = isProtected ? "Protection: Active (Click to pause)" : "Protection: Paused (Click to resume)";
        notifyIcon.Text = isProtected ? "HIDGuard - Protection Active" : "HIDGuard - Protection Paused";
    }

    public void ShowNotification(string title, string message, ToolTipIcon icon = ToolTipIcon.Info)
    {
        notifyIcon?.ShowBalloonTip(3000, title, message, icon);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        if (notifyIcon != null)
        {
            notifyIcon.Visible = false;
            notifyIcon.Dispose();
            notifyIcon = null;
        }

        disposed = true;
    }
}
