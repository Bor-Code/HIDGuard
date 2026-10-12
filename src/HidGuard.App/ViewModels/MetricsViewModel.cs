using System.Collections.ObjectModel;
using HidGuard.App.Mvvm;
using HidGuard.Core.Events;

namespace HidGuard.App.ViewModels;

public sealed class MetricsViewModel : ObservableObject
{
    private double currentWpm;
    private double currentMeanIntervalMs;
    private double currentStdDevIntervalMs;
    private double anomalyScore;
    private string threatLevelText = "Normal";
    private string threatLevelColor = "#10B981";
    private int totalEvaluations;
    private int anomalyCount;

    public double CurrentWpm
    {
        get => currentWpm;
        set => SetProperty(ref currentWpm, value);
    }

    public double CurrentMeanIntervalMs
    {
        get => currentMeanIntervalMs;
        set => SetProperty(ref currentMeanIntervalMs, value);
    }

    public double CurrentStdDevIntervalMs
    {
        get => currentStdDevIntervalMs;
        set => SetProperty(ref currentStdDevIntervalMs, value);
    }

    public double AnomalyScore
    {
        get => anomalyScore;
        set => SetProperty(ref anomalyScore, value);
    }

    public string ThreatLevelText
    {
        get => threatLevelText;
        set => SetProperty(ref threatLevelText, value);
    }

    public string ThreatLevelColor
    {
        get => threatLevelColor;
        set => SetProperty(ref threatLevelColor, value);
    }

    public int TotalEvaluations
    {
        get => totalEvaluations;
        set => SetProperty(ref totalEvaluations, value);
    }

    public int AnomalyCount
    {
        get => anomalyCount;
        set => SetProperty(ref anomalyCount, value);
    }

    public ObservableCollection<double> RecentIntervals { get; } = new();

    public void RecordEvaluation(PipelineKeystrokeProcessedEvent evt)
    {
        ArgumentNullException.ThrowIfNull(evt);

        if (evt.AnomalyResult == null)
        {
            return;
        }

        var stats = evt.AnomalyResult.Statistics;
        CurrentWpm = stats.MeanIntervalMs > 0 ? Math.Round((60000.0 / stats.MeanIntervalMs) / 5.0, 1) : 0;
        CurrentMeanIntervalMs = Math.Round(stats.MeanIntervalMs, 2);
        CurrentStdDevIntervalMs = Math.Round(stats.StandardDeviationMs, 2);
        AnomalyScore = Math.Round(evt.AnomalyResult.Score, 1);
        TotalEvaluations++;

        if (evt.AnomalyResult.IsSuspicious)
        {
            AnomalyCount++;
            ThreatLevelText = "Injection Attack Detected";
            ThreatLevelColor = "#EF4444";
        }
        else if (evt.AnomalyResult.Score > 50)
        {
            ThreatLevelText = "Suspicious Speed / Uniformity";
            ThreatLevelColor = "#F59E0B";
        }
        else
        {
            ThreatLevelText = "Normal Human Keystroke Dynamics";
            ThreatLevelColor = "#10B981";
        }

        DispatchToUi(() =>
        {
            RecentIntervals.Add(Math.Min(stats.MeanIntervalMs, 500));
            while (RecentIntervals.Count > 30)
            {
                RecentIntervals.RemoveAt(0);
            }
        });
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
