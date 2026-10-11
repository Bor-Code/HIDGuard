namespace HidGuard.Core.Events;

public interface IPipelineEventAggregator
{
    void PublishKeystrokeProcessed(PipelineKeystrokeProcessedEvent evt);
    void PublishDeviceLifecycle(DeviceLifecycleEvent evt);
    event EventHandler<PipelineKeystrokeProcessedEvent>? KeystrokeProcessed;
    event EventHandler<DeviceLifecycleEvent>? DeviceLifecycleChanged;
}
