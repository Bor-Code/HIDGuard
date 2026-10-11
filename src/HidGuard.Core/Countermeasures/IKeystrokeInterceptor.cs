namespace HidGuard.Core.Countermeasures;

public interface IKeystrokeInterceptor : IDisposable
{
    void StartInterception();
    void StopInterception();
    bool IsSuppressionActive { get; set; }
    void SuppressTemporarily(TimeSpan duration);
    event EventHandler<int>? KeySuppressed;
}
