using HidGuard.Core.Domain;

namespace HidGuard.Core.Timing;

public sealed class DeviceKeystrokeTracker
{
    private readonly object syncLock = new();
    private readonly double[] intervalBuffer;
    private readonly int capacity;
    private int bufferCount;
    private int bufferHead;
    private int burstLength;
    private int totalKeyCount;
    private DateTimeOffset? lastKeyDownTime;

    public DeviceIdentifier DeviceId { get; }
    public TimingThresholds Thresholds { get; }
    public DateTimeOffset LastSeen { get; private set; }

    public DeviceKeystrokeTracker(DeviceIdentifier deviceId, TimingThresholds thresholds)
    {
        ArgumentNullException.ThrowIfNull(deviceId);
        ArgumentNullException.ThrowIfNull(thresholds);
        thresholds.Validate();

        DeviceId = deviceId;
        Thresholds = thresholds;
        capacity = thresholds.WindowSize;
        intervalBuffer = new double[capacity];
        LastSeen = DateTimeOffset.UtcNow;
    }

    public bool IsInObservationWindow
    {
        get
        {
            lock (syncLock)
            {
                return totalKeyCount <= Thresholds.ObservationWindowKeyCount;
            }
        }
    }

    public int TotalKeyCount
    {
        get
        {
            lock (syncLock)
            {
                return totalKeyCount;
            }
        }
    }

    public SlidingWindowStatistics RecordKeyDown(DateTimeOffset timestamp)
    {
        lock (syncLock)
        {
            LastSeen = timestamp;
            totalKeyCount++;

            if (!lastKeyDownTime.HasValue)
            {
                lastKeyDownTime = timestamp;
                burstLength = 1;
                return BuildCurrentStatistics();
            }

            var elapsed = timestamp - lastKeyDownTime.Value;
            lastKeyDownTime = timestamp;

            if (elapsed < TimeSpan.Zero)
            {
                return BuildCurrentStatistics();
            }

            var intervalMs = elapsed.TotalMilliseconds;

            if (intervalMs < Thresholds.BurstPauseFloorMs)
            {
                burstLength++;
            }
            else
            {
                burstLength = 1;
            }

            intervalBuffer[bufferHead] = intervalMs;
            bufferHead = (bufferHead + 1) % capacity;

            if (bufferCount < capacity)
            {
                bufferCount++;
            }

            return BuildCurrentStatistics();
        }
    }

    public SlidingWindowStatistics GetCurrentStatistics()
    {
        lock (syncLock)
        {
            return BuildCurrentStatistics();
        }
    }

    public void Reset()
    {
        lock (syncLock)
        {
            bufferCount = 0;
            bufferHead = 0;
            burstLength = 0;
            totalKeyCount = 0;
            lastKeyDownTime = null;
            Array.Clear(intervalBuffer, 0, capacity);
        }
    }

    private SlidingWindowStatistics BuildCurrentStatistics()
    {
        if (bufferCount == 0)
        {
            return SlidingWindowStatistics.Empty with { BurstLength = burstLength };
        }

        var samples = new double[bufferCount];
        for (var i = 0; i < bufferCount; i++)
        {
            var index = (bufferHead - bufferCount + i + capacity) % capacity;
            samples[i] = intervalBuffer[index];
        }

        var sum = 0.0;
        var min = double.MaxValue;
        var subHumanCount = 0;

        for (var i = 0; i < samples.Length; i++)
        {
            var val = samples[i];
            sum += val;

            if (val < min)
            {
                min = val;
            }

            if (val < Thresholds.HumanFeasibilityFloorMs)
            {
                subHumanCount++;
            }
        }

        var mean = sum / samples.Length;

        var varianceSum = 0.0;
        for (var i = 0; i < samples.Length; i++)
        {
            var diff = samples[i] - mean;
            varianceSum += diff * diff;
        }

        var variance = varianceSum / samples.Length;
        var standardDeviation = Math.Sqrt(variance);
        var coefficientOfVariation = mean > 0.0001 ? standardDeviation / mean : 0.0;

        var sorted = (double[])samples.Clone();
        Array.Sort(sorted);

        double median;
        var middle = sorted.Length / 2;
        if (sorted.Length % 2 == 0)
        {
            median = (sorted[middle - 1] + sorted[middle]) / 2.0;
        }
        else
        {
            median = sorted[middle];
        }

        var subHumanRatio = (double)subHumanCount / samples.Length;

        return new SlidingWindowStatistics
        {
            SampleCount = samples.Length,
            MeanIntervalMs = Math.Round(mean, 2),
            MedianIntervalMs = Math.Round(median, 2),
            VarianceMs = Math.Round(variance, 2),
            StandardDeviationMs = Math.Round(standardDeviation, 2),
            CoefficientOfVariation = Math.Round(coefficientOfVariation, 4),
            MinimumIntervalMs = Math.Round(min, 2),
            BurstLength = burstLength,
            SubHumanIntervalRatio = Math.Round(subHumanRatio, 4)
        };
    }
}
