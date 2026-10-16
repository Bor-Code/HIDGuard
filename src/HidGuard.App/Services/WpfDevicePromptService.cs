using HidGuard.App.Views;
using HidGuard.Core.Domain;
using HidGuard.Core.Trust;

namespace HidGuard.App.Services;

public sealed class WpfDevicePromptService : IDevicePromptService
{
    public Task<DeviceTrustLevel?> PromptDeviceTrustAsync(
        DeviceIdentifier deviceId,
        string? friendlyName = null,
        CancellationToken cancellationToken = default)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher == null)
        {
            return Task.FromResult<DeviceTrustLevel?>(null);
        }

        var tcs = new TaskCompletionSource<DeviceTrustLevel?>(TaskCreationOptions.RunContinuationsAsynchronously);

        dispatcher.InvokeAsync(() =>
        {
            try
            {
                var dialog = new DevicePromptDialog(deviceId, friendlyName);
                if (System.Windows.Application.Current?.MainWindow is { IsVisible: true } main)
                {
                    dialog.Owner = main;
                }

                dialog.ShowDialog();
                tcs.TrySetResult(dialog.Result);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });

        return tcs.Task;
    }
}
