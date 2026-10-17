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

        var openItem = new ToolStripMenuItem("Kontrol Panelini Aç", null, (_, _) => onOpenRequested())
        {
            Font = new Font(contextMenu.Font, FontStyle.Bold)
        };

        statusMenuItem = new ToolStripMenuItem("Koruma: Aktif", null, (_, _) => onToggleProtectionRequested());
        var settingsItem = new ToolStripMenuItem("Ayarlar", null, (_, _) => onSettingsRequested());
        var auditItem = new ToolStripMenuItem("Olay Günlüğü", null, (_, _) => onAuditRequested());
        var exitItem = new ToolStripMenuItem("Çıkış", null, (_, _) => onExitRequested());

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
            Text = "HIDGuard - Donanım Güvenliği ve Tuş Enjeksiyonu Koruması",
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

        statusMenuItem.Text = isProtected ? "Koruma: Aktif (Duraklatmak için tıklayın)" : "Koruma: Duraklatıldı (Sürdürmek için tıklayın)";
        notifyIcon.Text = isProtected ? "HIDGuard - Koruma Aktif" : "HIDGuard - Koruma Duraklatıldı";
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
