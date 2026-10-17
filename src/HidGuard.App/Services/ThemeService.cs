using System;
using System.Collections.Generic;
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
        var app = WpfApplication.Current;
        if (app == null)
        {
            return;
        }

        var palette = GetPalette(theme);

        ApplyPaletteToDictionary(app.Resources, palette);

        foreach (Window window in app.Windows)
        {
            ApplyPaletteToDictionary(window.Resources, palette);
            window.InvalidateVisual();
        }
    }

    private static void ApplyPaletteToDictionary(ResourceDictionary res, Dictionary<string, string> palette)
    {
        if (res == null)
        {
            return;
        }

        foreach (var pair in palette)
        {
            var key = pair.Key;
            var hex = pair.Value;
            var color = (WpfColor)WpfColorConverter.ConvertFromString(hex);

            if (res.Contains(key) && res[key] is SolidColorBrush existing && !existing.IsFrozen)
            {
                existing.Color = color;
            }
            else
            {
                res[key] = new SolidColorBrush(color);
            }
        }

        if (res.MergedDictionaries != null)
        {
            foreach (var md in res.MergedDictionaries)
            {
                ApplyPaletteToDictionary(md, palette);
            }
        }
    }

    private static Dictionary<string, string> GetPalette(AppTheme theme) => theme switch
    {
        AppTheme.GreenBlack => new Dictionary<string, string>
        {
            ["PageBrush"] = "#070F0B",
            ["SurfaceBrush"] = "#0F1A14",
            ["SurfaceAltBrush"] = "#15261E",
            ["SurfaceHoverBrush"] = "#1C3328",
            ["BorderBrush"] = "#1F382B",
            ["BorderStrongBrush"] = "#2E5240",
            ["TrackBrush"] = "#15261E",
            ["TextPrimaryBrush"] = "#F0FDF4",
            ["TextSecondaryBrush"] = "#86EFAC",
            ["TextMutedBrush"] = "#52796F",
            ["AccentBrush"] = "#10B981",
            ["AccentHoverBrush"] = "#34D399",
            ["AccentPressedBrush"] = "#059669",
            ["AccentSoftBrush"] = "#0D2B1E",
            ["SuccessBrush"] = "#10B981",
            ["SuccessSoftBrush"] = "#0D2B1E",
            ["SuccessBorderBrush"] = "#059669",
            ["WarningBrush"] = "#F59E0B",
            ["WarningSoftBrush"] = "#291E0C",
            ["WarningBorderBrush"] = "#D97706",
            ["DangerBrush"] = "#EF4444",
            ["DangerSoftBrush"] = "#2A1316",
            ["DangerBorderBrush"] = "#DC2626",
            ["SidebarBrush"] = "#040906",
            ["SidebarHoverBrush"] = "#0F1A14",
            ["SidebarActiveBrush"] = "#15261E",
            ["SidebarDividerBrush"] = "#15261E",
            ["SidebarTextBrush"] = "#86EFAC",
            ["SidebarMutedBrush"] = "#52796F",
            ["SidebarIndicatorBrush"] = "#10B981",
            ["BackgroundBrush"] = "#070F0B",
            ["CardBrush"] = "#0F1A14",
            ["CardHoverBrush"] = "#15261E",
            ["BorderLightBrush"] = "#1F382B",
            ["AccentEmeraldBrush"] = "#10B981",
            ["AccentCyanBrush"] = "#10B981",
            ["AccentRedBrush"] = "#EF4444",
            ["AccentAmberBrush"] = "#F59E0B",
            ["AccentBlueBrush"] = "#10B981"
        },
        AppTheme.BlueBlack => new Dictionary<string, string>
        {
            ["PageBrush"] = "#080C14",
            ["SurfaceBrush"] = "#0F1726",
            ["SurfaceAltBrush"] = "#162238",
            ["SurfaceHoverBrush"] = "#1E2D4A",
            ["BorderBrush"] = "#22324F",
            ["BorderStrongBrush"] = "#334B75",
            ["TrackBrush"] = "#162238",
            ["TextPrimaryBrush"] = "#F8FAFC",
            ["TextSecondaryBrush"] = "#93C5FD",
            ["TextMutedBrush"] = "#64748B",
            ["AccentBrush"] = "#2563EB",
            ["AccentHoverBrush"] = "#3B82F6",
            ["AccentPressedBrush"] = "#1D4ED8",
            ["AccentSoftBrush"] = "#0F2042",
            ["SuccessBrush"] = "#10B981",
            ["SuccessSoftBrush"] = "#0E261B",
            ["SuccessBorderBrush"] = "#059669",
            ["WarningBrush"] = "#F59E0B",
            ["WarningSoftBrush"] = "#291E0C",
            ["WarningBorderBrush"] = "#D97706",
            ["DangerBrush"] = "#EF4444",
            ["DangerSoftBrush"] = "#2A1316",
            ["DangerBorderBrush"] = "#DC2626",
            ["SidebarBrush"] = "#05080E",
            ["SidebarHoverBrush"] = "#0F1726",
            ["SidebarActiveBrush"] = "#162238",
            ["SidebarDividerBrush"] = "#162238",
            ["SidebarTextBrush"] = "#94A3B8",
            ["SidebarMutedBrush"] = "#64748B",
            ["SidebarIndicatorBrush"] = "#3B82F6",
            ["BackgroundBrush"] = "#080C14",
            ["CardBrush"] = "#0F1726",
            ["CardHoverBrush"] = "#162238",
            ["BorderLightBrush"] = "#22324F",
            ["AccentEmeraldBrush"] = "#10B981",
            ["AccentCyanBrush"] = "#2563EB",
            ["AccentRedBrush"] = "#EF4444",
            ["AccentAmberBrush"] = "#F59E0B",
            ["AccentBlueBrush"] = "#3B82F6"
        },
        _ => new Dictionary<string, string>
        {
            ["PageBrush"] = "#0A0D14",
            ["SurfaceBrush"] = "#121722",
            ["SurfaceAltBrush"] = "#19202E",
            ["SurfaceHoverBrush"] = "#222B3D",
            ["BorderBrush"] = "#253043",
            ["BorderStrongBrush"] = "#374660",
            ["TrackBrush"] = "#19202E",
            ["TextPrimaryBrush"] = "#F8FAFC",
            ["TextSecondaryBrush"] = "#94A3B8",
            ["TextMutedBrush"] = "#64748B",
            ["AccentBrush"] = "#DC2626",
            ["AccentHoverBrush"] = "#EF4444",
            ["AccentPressedBrush"] = "#B91C1C",
            ["AccentSoftBrush"] = "#2A1316",
            ["SuccessBrush"] = "#10B981",
            ["SuccessSoftBrush"] = "#0E261B",
            ["SuccessBorderBrush"] = "#059669",
            ["WarningBrush"] = "#F59E0B",
            ["WarningSoftBrush"] = "#291E0C",
            ["WarningBorderBrush"] = "#D97706",
            ["DangerBrush"] = "#EF4444",
            ["DangerSoftBrush"] = "#2A1316",
            ["DangerBorderBrush"] = "#DC2626",
            ["SidebarBrush"] = "#06080E",
            ["SidebarHoverBrush"] = "#121722",
            ["SidebarActiveBrush"] = "#19202E",
            ["SidebarDividerBrush"] = "#19202E",
            ["SidebarTextBrush"] = "#94A3B8",
            ["SidebarMutedBrush"] = "#64748B",
            ["SidebarIndicatorBrush"] = "#EF4444",
            ["BackgroundBrush"] = "#0A0D14",
            ["CardBrush"] = "#121722",
            ["CardHoverBrush"] = "#19202E",
            ["BorderLightBrush"] = "#253043",
            ["AccentEmeraldBrush"] = "#10B981",
            ["AccentCyanBrush"] = "#DC2626",
            ["AccentRedBrush"] = "#EF4444",
            ["AccentAmberBrush"] = "#F59E0B",
            ["AccentBlueBrush"] = "#DC2626"
        }
    };
}
