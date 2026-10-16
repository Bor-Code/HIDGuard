using System.Collections.ObjectModel;
using System.Windows.Input;
using HidGuard.App.Mvvm;
using HidGuard.Core.Events;

namespace HidGuard.App.ViewModels;

public sealed class MetricsViewModel : ObservableObject
{
    private double currentWpm;
    private double currentMeanIntervalMs;
    private double currentStdDevIntervalMs;
    private double coefficientOfVariation;
    private double subHumanRatioPercent;
    private double minimumIntervalMs;
    private int burstLength;
    private double anomalyScore;
    private double humanConfidenceScore = 100.0;
    private double botProbabilityScore;
    private string threatLevelText = "Normal";
    private string threatLevelColor = "#10B981";
    private string activeDeviceId = "None";
    private int totalEvaluations;
    private int anomalyCount;
    private int totalHistogramSamples;

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

    public double CoefficientOfVariation
    {
        get => coefficientOfVariation;
        set => SetProperty(ref coefficientOfVariation, value);
    }

    public double SubHumanRatioPercent
    {
        get => subHumanRatioPercent;
        set => SetProperty(ref subHumanRatioPercent, value);
    }

    public double MinimumIntervalMs
    {
        get => minimumIntervalMs;
        set => SetProperty(ref minimumIntervalMs, value);
    }

    public int BurstLength
    {
        get => burstLength;
        set => SetProperty(ref burstLength, value);
    }

    public double AnomalyScore
    {
        get => anomalyScore;
        set => SetProperty(ref anomalyScore, value);
    }

    public double HumanConfidenceScore
    {
        get => humanConfidenceScore;
        set => SetProperty(ref humanConfidenceScore, value);
    }

    public double BotProbabilityScore
    {
        get => botProbabilityScore;
        set => SetProperty(ref botProbabilityScore, value);
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

    public string ActiveDeviceId
    {
        get => activeDeviceId;
        set => SetProperty(ref activeDeviceId, value);
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
    public ObservableCollection<LiveIntervalSample> RecentSamples { get; } = new();
    public ObservableCollection<TimingHistogramBucket> HistogramBuckets { get; } = new();

    public ICommand ResetMetricsCommand { get; }

    public MetricsViewModel()
    {
        HistogramBuckets.Add(new TimingHistogramBucket("< 10 ms", "#EF4444"));
        HistogramBuckets.Add(new TimingHistogramBucket("10-25 ms", "#F59E0B"));
        HistogramBuckets.Add(new TimingHistogramBucket("25-60 ms", "#3B82F6"));
        HistogramBuckets.Add(new TimingHistogramBucket("60-150 ms", "#10B981"));
        HistogramBuckets.Add(new TimingHistogramBucket("150-300 ms", "#06B6D4"));
        HistogramBuckets.Add(new TimingHistogramBucket("> 300 ms", "#9CA3AF"));

        ResetMetricsCommand = new RelayCommand(ResetMetrics);
    }

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
        CoefficientOfVariation = Math.Round(stats.CoefficientOfVariation, 3);
        SubHumanRatioPercent = Math.Round(stats.SubHumanIntervalRatio * 100.0, 1);
        MinimumIntervalMs = Math.Round(stats.MinimumIntervalMs, 2);
        BurstLength = stats.BurstLength;
        AnomalyScore = Math.Round(evt.AnomalyResult.Score, 1);
        BotProbabilityScore = Math.Clamp(AnomalyScore, 0.0, 100.0);
        HumanConfidenceScore = Math.Round(100.0 - BotProbabilityScore, 1);
        ActiveDeviceId = evt.Keystroke.DeviceId.FormattedHardwareId;
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

        var sampleMs = stats.MinimumIntervalMs > 0 && stats.MinimumIntervalMs < 500
            ? stats.MinimumIntervalMs
            : Math.Min(stats.MeanIntervalMs, 500.0);

        var sampleHeight = Math.Clamp(sampleMs / 5.0, 8.0, 100.0);
        var sampleColor = evt.AnomalyResult.IsSuspicious
            ? "#EF4444"
            : (sampleMs < 25.0 ? "#F59E0B" : (sampleMs < 60.0 ? "#3B82F6" : "#06B6D4"));

        var sample = new LiveIntervalSample
        {
            IntervalMs = Math.Round(sampleMs, 1),
            BarHeight = sampleHeight,
            Color = sampleColor,
            IsAnomaly = evt.AnomalyResult.IsSuspicious
        };

        UpdateHistogram(sampleMs);

        DispatchToUi(() =>
        {
            RecentIntervals.Add(Math.Min(stats.MeanIntervalMs, 500));
            while (RecentIntervals.Count > 30)
            {
                RecentIntervals.RemoveAt(0);
            }

            RecentSamples.Add(sample);
            while (RecentSamples.Count > 30)
            {
                RecentSamples.RemoveAt(0);
            }
        });
    }

    public void ResetMetrics()
    {
        CurrentWpm = 0;
        CurrentMeanIntervalMs = 0;
        CurrentStdDevIntervalMs = 0;
        CoefficientOfVariation = 0;
        SubHumanRatioPercent = 0;
        MinimumIntervalMs = 0;
        BurstLength = 0;
        AnomalyScore = 0;
        HumanConfidenceScore = 100.0;
        BotProbabilityScore = 0;
        ThreatLevelText = "Normal";
        ThreatLevelColor = "#10B981";
        ActiveDeviceId = "None";
        TotalEvaluations = 0;
        AnomalyCount = 0;
        totalHistogramSamples = 0;

        DispatchToUi(() =>
        {
            RecentIntervals.Clear();
            RecentSamples.Clear();
            foreach (var bucket in HistogramBuckets)
            {
                bucket.Count = 0;
                bucket.Percentage = 0;
                bucket.BarHeight = 0;
            }
        });
    }

    private void UpdateHistogram(double intervalMs)
    {
        totalHistogramSamples++;

        var bucketIndex = intervalMs switch
        {
            < 10.0 => 0,
            < 25.0 => 1,
            < 60.0 => 2,
            < 150.0 => 3,
            < 300.0 => 4,
            _ => 5
        };

        DispatchToUi(() =>
        {
            HistogramBuckets[bucketIndex].Count++;

            foreach (var b in HistogramBuckets)
            {
                var pct = totalHistogramSamples > 0
                    ? Math.Round(((double)b.Count / totalHistogramSamples) * 100.0, 1)
                    : 0.0;
                b.Percentage = pct;
                b.BarHeight = Math.Clamp(pct * 1.5, 4.0, 100.0);
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
