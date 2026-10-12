namespace HidGuard.App.Services;

public interface ISingleInstanceService : IDisposable
{
    bool IsSingleInstance { get; }
}
