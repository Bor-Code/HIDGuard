using HidGuard.Core.Events;

namespace HidGuard.Core.Pipeline;

public interface IKeystrokePipeline : IAsyncDisposable, IDisposable
{
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    bool IsRunning { get; }
    PipelineStatistics GetStatistics();
}
