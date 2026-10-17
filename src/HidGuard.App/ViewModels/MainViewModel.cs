using System.Windows.Forms;
using System.Windows.Input;
using HidGuard.App.Mvvm;
using HidGuard.App.Services;
using HidGuard.Core.Audit;
using HidGuard.Core.Events;
using HidGuard.Core.Pipeline;

namespace HidGuard.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly IUacElevationService uacElevationService;
    private readonly ITrayIconService trayIconService;
    private readonly IPipelineEventAggregator eventAggregator;
    private readonly IAuditTrailService auditTrailService;

    private object currentView;
    private string currentTabName = "Overview";

    public OverviewViewModel Overview { get; }
    public DevicesViewModel Devices { get; }
    public MetricsViewModel Metrics { get; }
    public SimulatorViewModel Simulator { get; }
    public AuditLogViewModel AuditLog { get; }
    public SettingsViewModel Settings { get; }

    public object CurrentView
    {
        get => currentView;
        set => SetProperty(ref currentView, value);
    }

    public string CurrentTabName
    {
        get => currentTabName;
        set => SetProperty(ref currentTabName, value);
    }

    public bool IsElevated => uacElevationService.IsProcessElevated;

    public bool IsProtectionActive
    {
        get => Overview.IsProtectionActive;
        set
        {
            Overview.IsProtectionActive = value;
            trayIconService.UpdateProtectionState(value);
            OnPropertyChanged();
        }
    }

    public ICommand NavigateCommand { get; }
    public ICommand ToggleProtectionCommand { get; }
    public ICommand RestartElevatedCommand { get; }

    public MainViewModel(
        OverviewViewModel overview,
        DevicesViewModel devices,
        MetricsViewModel metrics,
        SimulatorViewModel simulator,
        AuditLogViewModel auditLog,
        SettingsViewModel settings,
        IUacElevationService uacElevationService,
        ITrayIconService trayIconService,
        IPipelineEventAggregator eventAggregator,
        IAuditTrailService auditTrailService)
    {
        Overview = overview ?? throw new ArgumentNullException(nameof(overview));
        Devices = devices ?? throw new ArgumentNullException(nameof(devices));
        Metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        Simulator = simulator ?? throw new ArgumentNullException(nameof(simulator));
        AuditLog = auditLog ?? throw new ArgumentNullException(nameof(auditLog));
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.uacElevationService = uacElevationService ?? throw new ArgumentNullException(nameof(uacElevationService));
        this.trayIconService = trayIconService ?? throw new ArgumentNullException(nameof(trayIconService));
        this.eventAggregator = eventAggregator ?? throw new ArgumentNullException(nameof(eventAggregator));
        this.auditTrailService = auditTrailService ?? throw new ArgumentNullException(nameof(auditTrailService));

        currentView = Overview;

        NavigateCommand = new RelayCommand<string>(NavigateTo);
        ToggleProtectionCommand = new RelayCommand(ToggleProtection);
        RestartElevatedCommand = new RelayCommand(RestartElevated);

        SubscribeToEvents();
    }

    public void NavigateTo(string? target)
    {
        switch (target)
        {
            case "Devices":
                CurrentView = Devices;
                CurrentTabName = "Devices";
                _ = Devices.RefreshDevicesAsync();
                break;
            case "Metrics":
                CurrentView = Metrics;
                CurrentTabName = "Metrics";
                break;
            case "Simulator":
                CurrentView = Simulator;
                CurrentTabName = "Simulator";
                break;
            case "AuditLog":
                CurrentView = AuditLog;
                CurrentTabName = "AuditLog";
                _ = AuditLog.LoadAuditEventsAsync();
                break;
            case "Settings":
                CurrentView = Settings;
                CurrentTabName = "Settings";
                break;
            default:
                CurrentView = Overview;
                CurrentTabName = "Overview";
                break;
        }
    }

    private void ToggleProtection()
    {
        IsProtectionActive = !IsProtectionActive;
    }

    private void RestartElevated()
    {
        uacElevationService.RestartElevated();
    }

    private void SubscribeToEvents()
    {
        eventAggregator.KeystrokeProcessed += OnKeystrokeProcessed;
        eventAggregator.DeviceLifecycleChanged += OnDeviceLifecycleChanged;
        auditTrailService.AuditEventRecorded += OnAuditEventRecorded;
    }

    private void OnKeystrokeProcessed(object? sender, PipelineKeystrokeProcessedEvent e)
    {
        ArgumentNullException.ThrowIfNull(e);

        Overview.RecordLiveKeystroke(e);
        Metrics.RecordEvaluation(e);

        var isAttack = e.Decision?.Action is HidGuard.Core.Policy.PolicyAction.LockWorkstation
            or HidGuard.Core.Policy.PolicyAction.BlockDevice
            or HidGuard.Core.Policy.PolicyAction.LockAndBlock;

        if (isAttack)
        {
            trayIconService.ShowNotification(
                "Tuş Enjeksiyon Saldırısı Tespit Edildi",
                $"{e.Keystroke.DeviceId.FormattedHardwareId} donanımından gelen saldırı engellendi. Savunma eylemleri uygulandı.",
                ToolTipIcon.Warning);
        }
    }

    private void OnDeviceLifecycleChanged(object? sender, DeviceLifecycleEvent e)
    {
        Devices.HandleDeviceLifecycleEvent(e);
    }

    private void OnAuditEventRecorded(object? sender, AuditEvent e)
    {
        Overview.AddRecentAlert(e);
    }
}
