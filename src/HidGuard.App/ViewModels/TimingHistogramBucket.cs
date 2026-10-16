using HidGuard.App.Mvvm;

namespace HidGuard.App.ViewModels;

public sealed class TimingHistogramBucket : ObservableObject
{
    private int count;
    private double percentage;
    private double barHeight;

    public string Label { get; }
    public string ColorBrush { get; }

    public int Count
    {
        get => count;
        set => SetProperty(ref count, value);
    }

    public double Percentage
    {
        get => percentage;
        set => SetProperty(ref percentage, value);
    }

    public double BarHeight
    {
        get => barHeight;
        set => SetProperty(ref barHeight, value);
    }

    public TimingHistogramBucket(string label, string colorBrush)
    {
        Label = label;
        ColorBrush = colorBrush;
    }
}
