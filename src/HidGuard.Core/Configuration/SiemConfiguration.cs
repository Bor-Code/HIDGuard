namespace HidGuard.Core.Configuration;

public enum SiemProtocol
{
    Udp,
    Tcp
}

public enum SiemFormat
{
    Rfc5424,
    Cef
}

public sealed record SiemConfiguration
{
    public bool Enabled { get; init; }
    public string Host { get; init; } = "127.0.0.1";
    public int Port { get; init; } = 514;
    public SiemProtocol Protocol { get; init; } = SiemProtocol.Udp;
    public SiemFormat Format { get; init; } = SiemFormat.Cef;
    public int Facility { get; init; } = 10;
    public string AppName { get; init; } = "HIDGuard";
    public int TimeoutMilliseconds { get; init; } = 2000;

    public void Validate()
    {
        if (!Enabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(Host))
        {
            throw new ArgumentException("Host must not be empty when SIEM forwarding is enabled.", nameof(Host));
        }

        if (Port is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(Port), "Port must be between 1 and 65535.");
        }

        if (Facility is < 0 or > 23)
        {
            throw new ArgumentOutOfRangeException(nameof(Facility), "Syslog facility must be between 0 and 23.");
        }

        if (TimeoutMilliseconds is < 100 or > 30000)
        {
            throw new ArgumentOutOfRangeException(nameof(TimeoutMilliseconds), "TimeoutMilliseconds must be between 100 and 30000.");
        }
    }
}
