namespace HidGuard.App.Services;

public enum AppTheme
{
    RedBlack,
    GreenBlack,
    BlueBlack
}

public interface IThemeService
{
    AppTheme CurrentTheme { get; }
    string CurrentThemeName { get; }
    event EventHandler<AppTheme>? ThemeChanged;
    void ApplyTheme(AppTheme theme);
    void ApplyTheme(string themeName);
}
