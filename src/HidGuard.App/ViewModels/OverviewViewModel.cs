using System.Collections.ObjectModel;
using System.Windows.Input;
using HidGuard.App.Mvvm;
using HidGuard.Core.Audit;
using HidGuard.Core.Events;
using HidGuard.Core.Pipeline;
using HidGuard.Core.Policy;

namespace HidGuard.App.ViewModels;

public sealed class OverviewViewModel : ObservableObject
{
    private readonly IKeystrokePipeline pipeline;
    private readonly IAuditTrailService auditTrail;
    private bool isProtectionActive = true;
    private long totalKeystrokes;
    private long suppressedKeystrokes;
    private long attacksDetected;
    private long threatsNeutralized;
    private int connectedKeyboardsCount;
    private double latestAnomalyScore;
    private double currentWpm;
    private string statusText = "Active Defense Engaged";
    private string statusColor = "#10B981";

    public bool IsProtectionActive
    {
        get => isProtectionActive;
        set
        {
            if (SetProperty(ref isProtectionActive, value))
            {
                UpdateStatusText();
            }
        }
    }

    public long TotalKeystrokes
    {
        get => totalKeystrokes;
        set => SetProperty(ref totalKeystrokes, value);
    }

    public long SuppressedKeystrokes
    {
        get => suppressedKeystrokes;
        set => SetProperty(ref suppressedKeystrokes, value);
    }

    public long AttacksDetected
    {
        get => attacksDetected;
        set => SetProperty(ref attacksDetected, value);
    }

    public long ThreatsNeutralized
    {
        get => threatsNeutralized;
        set => SetProperty(ref threatsNeutralized, value);
    }

    public int ConnectedKeyboardsCount
    {
        get => connectedKeyboardsCount;
        set => SetProperty(ref connectedKeyboardsCount, value);
    }

    public double LatestAnomalyScore
    {
        get => latestAnomalyScore;
        set => SetProperty(ref latestAnomalyScore, value);
    }

    public double CurrentWpm
    {
        get => currentWpm;
        set => SetProperty(ref currentWpm, value);
    }

    public string StatusText
    {
        get => statusText;
        set => SetProperty(ref statusText, value);
    }

    public string StatusColor
    {
        get => statusColor;
        set => SetProperty(ref statusColor, value);
    }

    public ObservableCollection<AuditEventItemViewModel> RecentAlerts { get; } = new();

    public bool HasRecentAlerts => RecentAlerts.Count > 0;

    public ICommand ToggleProtectionCommand { get; }
    public ICommand ClearStatsCommand { get; }

    public OverviewViewModel(IKeystrokePipeline pipeline, IAuditTrailService auditTrail)
    {
        this.pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        this.auditTrail = auditTrail ?? throw new ArgumentNullException(nameof(auditTrail));

        RecentAlerts.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasRecentAlerts));

        ToggleProtectionCommand = new RelayCommand(ToggleProtection);
        ClearStatsCommand = new RelayCommand(ClearStats);

        UpdateFromPipelineStats();
    }

    public void UpdateFromPipelineStats()
    {
        var stats = pipeline.GetStatistics();
        TotalKeystrokes = stats.TotalKeystrokesProcessed;
        SuppressedKeystrokes = stats.TotalKeystrokesSuppressed;
        AttacksDetected = stats.TotalAttacksDetected;
        ThreatsNeutralized = stats.TotalAttacksNeutralized;
        ConnectedKeyboardsCount = stats.ActiveTrackedDevices;
    }

    public void RecordLiveKeystroke(PipelineKeystrokeProcessedEvent evt)
    {
        ArgumentNullException.ThrowIfNull(evt);

        UpdateFromPipelineStats();

        if (evt.AnomalyResult != null)
        {
            var mean = evt.AnomalyResult.Statistics.MeanIntervalMs;
            CurrentWpm = mean > 0 ? Math.Round((60000.0 / mean) / 5.0, 1) : 0;
            LatestAnomalyScore = Math.Round(evt.AnomalyResult.Score, 1);
        }

        var isAttack = evt.Decision?.Action is PolicyAction.LockWorkstation or PolicyAction.BlockDevice or PolicyAction.LockAndBlock;
        if (isAttack)
        {
            StatusText = "Injection Attack Detected & Mitigated";
            StatusColor = "#EF4444";
        }
        else if (isProtectionActive && StatusColor == "#EF4444")
        {
            UpdateStatusText();
        }
    }

    public void AddRecentAlert(AuditEvent auditEvent)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        DispatchToUi(() =>
        {
            RecentAlerts.Insert(0, new AuditEventItemViewModel(auditEvent));
            while (RecentAlerts.Count > 10)
            {
                RecentAlerts.RemoveAt(RecentAlerts.Count - 1);
            }
        });
    }

    private void ToggleProtection()
    {
        IsProtectionActive = !IsProtectionActive;
    }

    private void ClearStats()
    {
        TotalKeystrokes = 0;
        SuppressedKeystrokes = 0;
        AttacksDetected = 0;
        ThreatsNeutralized = 0;
        LatestAnomalyScore = 0;
        CurrentWpm = 0;
        RecentAlerts.Clear();
    }

    private void UpdateStatusText()
    {
        if (isProtectionActive)
        {
            StatusText = "Active Defense Engaged";
            StatusColor = "#10B981";
        }
        else
        {
            StatusText = "Protection Paused (Monitoring Only)";
            StatusColor = "#F59E0B";
        }
    }

    private static void DispatchToUi(Action action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(action);
        }
        else
        {
            action();
        }
    }
}
