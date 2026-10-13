using System.Windows.Input;
using HidGuard.App.Mvvm;
using HidGuard.Core.Configuration;
using HidGuard.Core.Timing;

namespace HidGuard.App.ViewModels;

public sealed class SettingsViewModel : ObservableObject
{
    private readonly IConfigurationManager configurationManager;

    private int windowSize = 30;
    private double humanFeasibilityFloorMs = 25.0;
    private double subHumanRatioThreshold = 0.35;
    private double coefficientOfVariationThreshold = 0.18;
    private double minimumIntervalThresholdMs = 8.0;
    private int burstLengthThreshold = 15;
    private double suspicionScoreThreshold = 65.0;

    private bool lockWorkstationEnabled = true;
    private bool deviceDisableEnabled = true;
    private bool keySuppressionEnabled = true;
    private int temporarySuppressionSeconds = 3;

    private bool startMinimizedToTray = true;
    private bool notifyOnBlockedDevices = true;
    private bool notifyOnAllowedDevices;
    private string theme = "Dark";

    private bool enableOptInPayloadCapture;
    private int maxIncidentCount = 1000;
    private int retentionDays = 30;
    private int maxPayloadBytes = 4096;

    private string feedbackMessage = string.Empty;
    private string feedbackColor = "#10B981";

    public int WindowSize
    {
        get => windowSize;
        set => SetProperty(ref windowSize, value);
    }

    public double HumanFeasibilityFloorMs
    {
        get => humanFeasibilityFloorMs;
        set => SetProperty(ref humanFeasibilityFloorMs, value);
    }

    public double SubHumanRatioThreshold
    {
        get => subHumanRatioThreshold;
        set => SetProperty(ref subHumanRatioThreshold, value);
    }

    public double CoefficientOfVariationThreshold
    {
        get => coefficientOfVariationThreshold;
        set => SetProperty(ref coefficientOfVariationThreshold, value);
    }

    public double MinimumIntervalThresholdMs
    {
        get => minimumIntervalThresholdMs;
        set => SetProperty(ref minimumIntervalThresholdMs, value);
    }

    public int BurstLengthThreshold
    {
        get => burstLengthThreshold;
        set => SetProperty(ref burstLengthThreshold, value);
    }

    public double SuspicionScoreThreshold
    {
        get => suspicionScoreThreshold;
        set => SetProperty(ref suspicionScoreThreshold, value);
    }

    public bool LockWorkstationEnabled
    {
        get => lockWorkstationEnabled;
        set => SetProperty(ref lockWorkstationEnabled, value);
    }

    public bool DeviceDisableEnabled
    {
        get => deviceDisableEnabled;
        set => SetProperty(ref deviceDisableEnabled, value);
    }

    public bool KeySuppressionEnabled
    {
        get => keySuppressionEnabled;
        set => SetProperty(ref keySuppressionEnabled, value);
    }

    public int TemporarySuppressionSeconds
    {
        get => temporarySuppressionSeconds;
        set => SetProperty(ref temporarySuppressionSeconds, value);
    }

    public bool StartMinimizedToTray
    {
        get => startMinimizedToTray;
        set => SetProperty(ref startMinimizedToTray, value);
    }

    public bool NotifyOnBlockedDevices
    {
        get => notifyOnBlockedDevices;
        set => SetProperty(ref notifyOnBlockedDevices, value);
    }

    public bool NotifyOnAllowedDevices
    {
        get => notifyOnAllowedDevices;
        set => SetProperty(ref notifyOnAllowedDevices, value);
    }

    public string Theme
    {
        get => theme;
        set => SetProperty(ref theme, value);
    }

    public bool EnableOptInPayloadCapture
    {
        get => enableOptInPayloadCapture;
        set => SetProperty(ref enableOptInPayloadCapture, value);
    }

    public int MaxIncidentCount
    {
        get => maxIncidentCount;
        set => SetProperty(ref maxIncidentCount, value);
    }

    public int RetentionDays
    {
        get => retentionDays;
        set => SetProperty(ref retentionDays, value);
    }

    public int MaxPayloadBytes
    {
        get => maxPayloadBytes;
        set => SetProperty(ref maxPayloadBytes, value);
    }

    public string FeedbackMessage
    {
        get => feedbackMessage;
        set => SetProperty(ref feedbackMessage, value);
    }

    public string FeedbackColor
    {
        get => feedbackColor;
        set => SetProperty(ref feedbackColor, value);
    }

    public ICommand SaveSettingsCommand { get; }
    public ICommand ResetDefaultsCommand { get; }

    public SettingsViewModel(IConfigurationManager configurationManager)
    {
        this.configurationManager = configurationManager ?? throw new ArgumentNullException(nameof(configurationManager));

        SaveSettingsCommand = new AsyncRelayCommand(SaveSettingsAsync);
        ResetDefaultsCommand = new RelayCommand(ResetDefaults);

        LoadFromConfiguration(this.configurationManager.CurrentConfiguration);
    }

    public void LoadFromConfiguration(HidGuardConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);

        WindowSize = config.Timing.WindowSize;
        HumanFeasibilityFloorMs = config.Timing.HumanFeasibilityFloorMs;
        SubHumanRatioThreshold = config.Timing.SubHumanRatioThreshold;
        CoefficientOfVariationThreshold = config.Timing.CoefficientOfVariationThreshold;
        MinimumIntervalThresholdMs = config.Timing.MinimumIntervalThresholdMs;
        BurstLengthThreshold = config.Timing.BurstLengthThreshold;
        SuspicionScoreThreshold = config.Timing.SuspicionScoreThreshold;

        LockWorkstationEnabled = config.Countermeasures.LockWorkstationEnabled;
        DeviceDisableEnabled = config.Countermeasures.DeviceDisableEnabled;
        KeySuppressionEnabled = config.Countermeasures.KeySuppressionEnabled;
        TemporarySuppressionSeconds = config.Countermeasures.TemporarySuppressionSeconds;

        StartMinimizedToTray = config.Ui.StartMinimizedToTray;
        NotifyOnBlockedDevices = config.Ui.NotifyOnBlockedDevices;
        NotifyOnAllowedDevices = config.Ui.NotifyOnAllowedDevices;
        Theme = config.Ui.Theme;

        EnableOptInPayloadCapture = config.Incidents.EnableOptInPayloadCapture;
        MaxIncidentCount = config.Incidents.MaxIncidentCount;
        RetentionDays = config.Incidents.RetentionDays;
        MaxPayloadBytes = config.Incidents.MaxPayloadBytes;
    }

    private async Task SaveSettingsAsync()
    {
        try
        {
            var newConfig = new HidGuardConfiguration
            {
                Timing = new TimingThresholds
                {
                    WindowSize = WindowSize,
                    HumanFeasibilityFloorMs = HumanFeasibilityFloorMs,
                    SubHumanRatioThreshold = SubHumanRatioThreshold,
                    CoefficientOfVariationThreshold = CoefficientOfVariationThreshold,
                    MinimumIntervalThresholdMs = MinimumIntervalThresholdMs,
                    BurstLengthThreshold = BurstLengthThreshold,
                    SuspicionScoreThreshold = SuspicionScoreThreshold
                },
                Countermeasures = new CountermeasureConfiguration
                {
                    LockWorkstationEnabled = LockWorkstationEnabled,
                    DeviceDisableEnabled = DeviceDisableEnabled,
                    KeySuppressionEnabled = KeySuppressionEnabled,
                    TemporarySuppressionSeconds = TemporarySuppressionSeconds
                },
                Ui = new UiConfiguration
                {
                    StartMinimizedToTray = StartMinimizedToTray,
                    NotifyOnBlockedDevices = NotifyOnBlockedDevices,
                    NotifyOnAllowedDevices = NotifyOnAllowedDevices,
                    Theme = Theme
                },
                Incidents = new IncidentStoreConfiguration
                {
                    EnableOptInPayloadCapture = EnableOptInPayloadCapture,
                    MaxIncidentCount = MaxIncidentCount,
                    RetentionDays = RetentionDays,
                    MaxPayloadBytes = MaxPayloadBytes
                }
            };

            newConfig.Validate();
            await configurationManager.SaveAsync(newConfig).ConfigureAwait(false);

            FeedbackMessage = "Settings saved successfully.";
            FeedbackColor = "#10B981";
        }
        catch (Exception ex)
        {
            FeedbackMessage = $"Validation error: {ex.Message}";
            FeedbackColor = "#EF4444";
        }
    }

    private void ResetDefaults()
    {
        var defaultConfig = new HidGuardConfiguration();
        LoadFromConfiguration(defaultConfig);
        FeedbackMessage = "Settings reset to defaults. Click Save to apply.";
        FeedbackColor = "#F59E0B";
    }
}
