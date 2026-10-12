using System.Windows.Input;
using HidGuard.App.Mvvm;
using HidGuard.Core.Trust;

namespace HidGuard.App.ViewModels;

public sealed class DeviceItemViewModel : ObservableObject
{
    private readonly Func<string, DeviceTrustLevel, Task>? onTrustChanged;
    private DeviceTrustLevel trustLevel;
    private DateTimeOffset lastSeen;

    public string DeviceId { get; }
    public string HardwareId { get; }
    public string DisplayName { get; }
    public DateTimeOffset FirstEnrolled { get; }

    public DateTimeOffset LastSeen
    {
        get => lastSeen;
        set => SetProperty(ref lastSeen, value);
    }

    public DeviceTrustLevel TrustLevel
    {
        get => trustLevel;
        set
        {
            if (SetProperty(ref trustLevel, value))
            {
                OnPropertyChanged(nameof(TrustBadgeText));
                OnPropertyChanged(nameof(TrustBadgeColor));
                OnPropertyChanged(nameof(IsTrusted));
                OnPropertyChanged(nameof(IsBlocked));
                OnPropertyChanged(nameof(IsUnknown));
            }
        }
    }

    public string TrustBadgeText => TrustLevel switch
    {
        DeviceTrustLevel.Trusted => "Trusted",
        DeviceTrustLevel.Blocked => "Blocked",
        _ => "Unknown"
    };

    public string TrustBadgeColor => TrustLevel switch
    {
        DeviceTrustLevel.Trusted => "#10B981",
        DeviceTrustLevel.Blocked => "#EF4444",
        _ => "#F59E0B"
    };

    public bool IsTrusted => TrustLevel == DeviceTrustLevel.Trusted;
    public bool IsBlocked => TrustLevel == DeviceTrustLevel.Blocked;
    public bool IsUnknown => TrustLevel == DeviceTrustLevel.Unknown;

    public ICommand TrustCommand { get; }
    public ICommand BlockCommand { get; }
    public ICommand ResetTrustCommand { get; }

    public DeviceItemViewModel(
        string deviceId,
        string hardwareId,
        string displayName,
        DeviceTrustLevel trustLevel,
        DateTimeOffset firstEnrolled,
        DateTimeOffset lastSeen,
        Func<string, DeviceTrustLevel, Task>? onTrustChanged = null)
    {
        DeviceId = deviceId ?? string.Empty;
        HardwareId = hardwareId ?? string.Empty;
        DisplayName = displayName ?? string.Empty;
        this.trustLevel = trustLevel;
        FirstEnrolled = firstEnrolled;
        this.lastSeen = lastSeen;
        this.onTrustChanged = onTrustChanged;

        TrustCommand = new AsyncRelayCommand(
            () => ChangeTrustLevelAsync(DeviceTrustLevel.Trusted),
            () => TrustLevel != DeviceTrustLevel.Trusted);

        BlockCommand = new AsyncRelayCommand(
            () => ChangeTrustLevelAsync(DeviceTrustLevel.Blocked),
            () => TrustLevel != DeviceTrustLevel.Blocked);

        ResetTrustCommand = new AsyncRelayCommand(
            () => ChangeTrustLevelAsync(DeviceTrustLevel.Unknown),
            () => TrustLevel != DeviceTrustLevel.Unknown);
    }

    private async Task ChangeTrustLevelAsync(DeviceTrustLevel newLevel)
    {
        if (onTrustChanged != null)
        {
            await onTrustChanged(DeviceId, newLevel).ConfigureAwait(false);
        }
        TrustLevel = newLevel;
    }
}
