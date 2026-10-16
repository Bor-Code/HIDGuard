using System.Windows;
using HidGuard.Core.Domain;
using HidGuard.Core.Trust;

namespace HidGuard.App.Views;

public partial class DevicePromptDialog : Window
{
    public DeviceTrustLevel? Result { get; private set; }

    public DevicePromptDialog(DeviceIdentifier deviceId, string? friendlyName = null)
    {
        ArgumentNullException.ThrowIfNull(deviceId);
        InitializeComponent();
        HardwareIdText.Text = deviceId.FormattedHardwareId;
        DevicePathText.Text = deviceId.Path;
        if (!string.IsNullOrWhiteSpace(friendlyName))
        {
            DeviceFriendlyNameText.Text = friendlyName;
            FriendlyNameRow.Visibility = Visibility.Visible;
        }
    }

    private void OnTrustClicked(object sender, RoutedEventArgs e)
    {
        Result = DeviceTrustLevel.Trusted;
        DialogResult = true;
        Close();
    }

    private void OnBlockClicked(object sender, RoutedEventArgs e)
    {
        Result = DeviceTrustLevel.Blocked;
        DialogResult = true;
        Close();
    }

    private void OnCancelClicked(object sender, RoutedEventArgs e)
    {
        Result = null;
        DialogResult = false;
        Close();
    }
}
