namespace HidGuard.Core.Events;

public sealed class PipelineEventAggregator : IPipelineEventAggregator
{
    public event EventHandler<PipelineKeystrokeProcessedEvent>? KeystrokeProcessed;
    public event EventHandler<DeviceLifecycleEvent>? DeviceLifecycleChanged;

    public void PublishKeystrokeProcessed(PipelineKeystrokeProcessedEvent evt)
    {
        ArgumentNullException.ThrowIfNull(evt);
        KeystrokeProcessed?.Invoke(this, evt);
    }

    public void PublishDeviceLifecycle(DeviceLifecycleEvent evt)
    {
        ArgumentNullException.ThrowIfNull(evt);
        DeviceLifecycleChanged?.Invoke(this, evt);
    }
}
