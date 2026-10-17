using System;
using System.Windows;
using System.Windows.Media;
using WpfApplication = System.Windows.Application;
using WpfColor = System.Windows.Media.Color;
using WpfColorConverter = System.Windows.Media.ColorConverter;

namespace HidGuard.App.Services;

public sealed class ThemeService : IThemeService
{
    private AppTheme currentTheme = AppTheme.RedBlack;

    public AppTheme CurrentTheme => currentTheme;

    public string CurrentThemeName => currentTheme switch
    {
        AppTheme.GreenBlack => "GreenBlack",
        AppTheme.BlueBlack => "BlueBlack",
        _ => "RedBlack"
    };

    public event EventHandler<AppTheme>? ThemeChanged;

    public void ApplyTheme(string themeName)
    {
        var theme = themeName?.ToUpperInvariant() switch
        {
            "GREENBLACK" or "GREEN" => AppTheme.GreenBlack,
            "BLUEBLACK" or "BLUE" => AppTheme.BlueBlack,
            _ => AppTheme.RedBlack
        };
        ApplyTheme(theme);
    }

    public void ApplyTheme(AppTheme theme)
    {
        currentTheme = theme;
        var app = WpfApplication.Current;
        if (app == null)
        {
            return;
        }

        var dispatcher = app.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(() => ApplyThemeInternal(theme));
        }
        else
        {
            ApplyThemeInternal(theme);
        }

        ThemeChanged?.Invoke(this, theme);
    }

    private static void ApplyThemeInternal(AppTheme theme)
    {
        var res = WpfApplication.Current.Resources;

        switch (theme)
        {
            case AppTheme.GreenBlack:
                SetBrush(res, "PageBrush", "#070F0B");
                SetBrush(res, "SurfaceBrush", "#0F1A14");
                SetBrush(res, "SurfaceAltBrush", "#15261E");
                SetBrush(res, "SurfaceHoverBrush", "#1C3328");
                SetBrush(res, "BorderBrush", "#1F382B");
                SetBrush(res, "BorderStrongBrush", "#2E5240");
                SetBrush(res, "TrackBrush", "#15261E");
                SetBrush(res, "TextPrimaryBrush", "#F0FDF4");
                SetBrush(res, "TextSecondaryBrush", "#86EFAC");
                SetBrush(res, "TextMutedBrush", "#52796F");
                SetBrush(res, "AccentBrush", "#10B981");
                SetBrush(res, "AccentHoverBrush", "#34D399");
                SetBrush(res, "AccentPressedBrush", "#059669");
                SetBrush(res, "AccentSoftBrush", "#0D2B1E");
                SetBrush(res, "SuccessBrush", "#10B981");
                SetBrush(res, "SuccessSoftBrush", "#0D2B1E");
                SetBrush(res, "SuccessBorderBrush", "#059669");
                SetBrush(res, "WarningBrush", "#F59E0B");
                SetBrush(res, "WarningSoftBrush", "#291E0C");
                SetBrush(res, "WarningBorderBrush", "#D97706");
                SetBrush(res, "DangerBrush", "#EF4444");
                SetBrush(res, "DangerSoftBrush", "#2A1316");
                SetBrush(res, "DangerBorderBrush", "#DC2626");
                SetBrush(res, "SidebarBrush", "#040906");
                SetBrush(res, "SidebarHoverBrush", "#0F1A14");
                SetBrush(res, "SidebarActiveBrush", "#15261E");
                SetBrush(res, "SidebarDividerBrush", "#15261E");
                SetBrush(res, "SidebarTextBrush", "#86EFAC");
                SetBrush(res, "SidebarMutedBrush", "#52796F");
                SetBrush(res, "SidebarIndicatorBrush", "#10B981");
                SetBrush(res, "BackgroundBrush", "#070F0B");
                SetBrush(res, "CardBrush", "#0F1A14");
                SetBrush(res, "CardHoverBrush", "#15261E");
                SetBrush(res, "BorderLightBrush", "#1F382B");
                SetBrush(res, "AccentCyanBrush", "#10B981");
                SetBrush(res, "AccentRedBrush", "#EF4444");
                SetBrush(res, "AccentAmberBrush", "#F59E0B");
                SetBrush(res, "AccentBlueBrush", "#10B981");
                break;

            case AppTheme.BlueBlack:
                SetBrush(res, "PageBrush", "#080C14");
                SetBrush(res, "SurfaceBrush", "#0F1726");
                SetBrush(res, "SurfaceAltBrush", "#162238");
                SetBrush(res, "SurfaceHoverBrush", "#1E2D4A");
                SetBrush(res, "BorderBrush", "#22324F");
                SetBrush(res, "BorderStrongBrush", "#334B75");
                SetBrush(res, "TrackBrush", "#162238");
                SetBrush(res, "TextPrimaryBrush", "#F8FAFC");
                SetBrush(res, "TextSecondaryBrush", "#93C5FD");
                SetBrush(res, "TextMutedBrush", "#64748B");
                SetBrush(res, "AccentBrush", "#2563EB");
                SetBrush(res, "AccentHoverBrush", "#3B82F6");
                SetBrush(res, "AccentPressedBrush", "#1D4ED8");
                SetBrush(res, "AccentSoftBrush", "#0F2042");
                SetBrush(res, "SuccessBrush", "#10B981");
                SetBrush(res, "SuccessSoftBrush", "#0E261B");
                SetBrush(res, "SuccessBorderBrush", "#059669");
                SetBrush(res, "WarningBrush", "#F59E0B");
                SetBrush(res, "WarningSoftBrush", "#291E0C");
                SetBrush(res, "WarningBorderBrush", "#D97706");
                SetBrush(res, "DangerBrush", "#EF4444");
                SetBrush(res, "DangerSoftBrush", "#2A1316");
                SetBrush(res, "DangerBorderBrush", "#DC2626");
                SetBrush(res, "SidebarBrush", "#05080E");
                SetBrush(res, "SidebarHoverBrush", "#0F1726");
                SetBrush(res, "SidebarActiveBrush", "#162238");
                SetBrush(res, "SidebarDividerBrush", "#162238");
                SetBrush(res, "SidebarTextBrush", "#94A3B8");
                SetBrush(res, "SidebarMutedBrush", "#64748B");
                SetBrush(res, "SidebarIndicatorBrush", "#3B82F6");
                SetBrush(res, "BackgroundBrush", "#080C14");
                SetBrush(res, "CardBrush", "#0F1726");
                SetBrush(res, "CardHoverBrush", "#162238");
                SetBrush(res, "BorderLightBrush", "#22324F");
                SetBrush(res, "AccentCyanBrush", "#2563EB");
                SetBrush(res, "AccentRedBrush", "#EF4444");
                SetBrush(res, "AccentAmberBrush", "#F59E0B");
                SetBrush(res, "AccentBlueBrush", "#3B82F6");
                break;

            default:
                SetBrush(res, "PageBrush", "#0A0D14");
                SetBrush(res, "SurfaceBrush", "#121722");
                SetBrush(res, "SurfaceAltBrush", "#19202E");
                SetBrush(res, "SurfaceHoverBrush", "#222B3D");
                SetBrush(res, "BorderBrush", "#253043");
                SetBrush(res, "BorderStrongBrush", "#374660");
                SetBrush(res, "TrackBrush", "#19202E");
                SetBrush(res, "TextPrimaryBrush", "#F8FAFC");
                SetBrush(res, "TextSecondaryBrush", "#94A3B8");
                SetBrush(res, "TextMutedBrush", "#64748B");
                SetBrush(res, "AccentBrush", "#DC2626");
                SetBrush(res, "AccentHoverBrush", "#EF4444");
                SetBrush(res, "AccentPressedBrush", "#B91C1C");
                SetBrush(res, "AccentSoftBrush", "#2A1316");
                SetBrush(res, "SuccessBrush", "#10B981");
                SetBrush(res, "SuccessSoftBrush", "#0E261B");
                SetBrush(res, "SuccessBorderBrush", "#059669");
                SetBrush(res, "WarningBrush", "#F59E0B");
                SetBrush(res, "WarningSoftBrush", "#291E0C");
                SetBrush(res, "WarningBorderBrush", "#D97706");
                SetBrush(res, "DangerBrush", "#EF4444");
                SetBrush(res, "DangerSoftBrush", "#2A1316");
                SetBrush(res, "DangerBorderBrush", "#DC2626");
                SetBrush(res, "SidebarBrush", "#06080E");
                SetBrush(res, "SidebarHoverBrush", "#121722");
                SetBrush(res, "SidebarActiveBrush", "#19202E");
                SetBrush(res, "SidebarDividerBrush", "#19202E");
                SetBrush(res, "SidebarTextBrush", "#94A3B8");
                SetBrush(res, "SidebarMutedBrush", "#64748B");
                SetBrush(res, "SidebarIndicatorBrush", "#EF4444");
                SetBrush(res, "BackgroundBrush", "#0A0D14");
                SetBrush(res, "CardBrush", "#121722");
                SetBrush(res, "CardHoverBrush", "#19202E");
                SetBrush(res, "BorderLightBrush", "#253043");
                SetBrush(res, "AccentCyanBrush", "#DC2626");
                SetBrush(res, "AccentRedBrush", "#EF4444");
                SetBrush(res, "AccentAmberBrush", "#F59E0B");
                SetBrush(res, "AccentBlueBrush", "#DC2626");
                break;
        }
    }

    private static void SetBrush(ResourceDictionary res, string key, string hexColor)
    {
        var color = (WpfColor)WpfColorConverter.ConvertFromString(hexColor);
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        res[key] = brush;
    }
}
