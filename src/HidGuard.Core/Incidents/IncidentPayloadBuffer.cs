using HidGuard.Core.Configuration;
using HidGuard.Core.Domain;

namespace HidGuard.Core.Incidents;

public sealed class IncidentPayloadBuffer
{
    private readonly object gate = new();
    private readonly Dictionary<string, Queue<byte>> buffers = new(StringComparer.OrdinalIgnoreCase);
    private bool enabled;
    private int capacity;

    public IncidentPayloadBuffer(IncidentStoreConfiguration? configuration = null)
    {
        Configure(configuration ?? new IncidentStoreConfiguration());
    }

    public bool IsEnabled
    {
        get
        {
            lock (gate)
            {
                return enabled;
            }
        }
    }

    public int Capacity
    {
        get
        {
            lock (gate)
            {
                return capacity;
            }
        }
    }

    public void Configure(IncidentStoreConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        configuration.Validate();

        lock (gate)
        {
            enabled = configuration.EnableOptInPayloadCapture;
            capacity = Math.Min(configuration.CaptureWindowKeystrokes, configuration.MaxPayloadBytes);

            if (!enabled)
            {
                buffers.Clear();
                return;
            }

            foreach (var queue in buffers.Values)
            {
                while (queue.Count > capacity)
                {
                    queue.Dequeue();
                }
            }
        }
    }

    public void Record(KeystrokeEvent keystroke)
    {
        ArgumentNullException.ThrowIfNull(keystroke);

        if (keystroke.State != KeyTransitionState.KeyDown)
        {
            return;
        }

        lock (gate)
        {
            if (!enabled)
            {
                return;
            }

            var key = keystroke.DeviceId.Path;
            if (!buffers.TryGetValue(key, out var queue))
            {
                queue = new Queue<byte>(capacity);
                buffers[key] = queue;
            }

            if (queue.Count >= capacity)
            {
                queue.Dequeue();
            }

            queue.Enqueue((byte)(keystroke.VirtualKey & 0xFF));
        }
    }

    public byte[]? TakeSnapshot(DeviceIdentifier device)
    {
        ArgumentNullException.ThrowIfNull(device);

        lock (gate)
        {
            if (!enabled || !buffers.Remove(device.Path, out var queue) || queue.Count == 0)
            {
                return null;
            }

            return queue.ToArray();
        }
    }

    public void RemoveDevice(DeviceIdentifier device)
    {
        ArgumentNullException.ThrowIfNull(device);

        lock (gate)
        {
            buffers.Remove(device.Path);
        }
    }

    public void Clear()
    {
        lock (gate)
        {
            buffers.Clear();
        }
    }
}
