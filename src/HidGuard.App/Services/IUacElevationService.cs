namespace HidGuard.App.Services;

public interface IUacElevationService
{
    bool IsProcessElevated { get; }
    bool RestartElevated();
}
