using HidGuard.Core.Policy;
using HidGuard.Core.Timing;

namespace HidGuard.Core.Configuration;

public sealed record HidGuardConfiguration
{
    public TimingThresholds Timing { get; init; } = new();
    public PolicyOptions Policy { get; init; } = new();
    public CountermeasureConfiguration Countermeasures { get; init; } = new();
    public LoggingConfiguration Logging { get; init; } = new();
    public UiConfiguration Ui { get; init; } = new();
    public IncidentStoreConfiguration Incidents { get; init; } = new();
    public SiemConfiguration Siem { get; init; } = new();

    public void Validate()
    {
        Timing.Validate();
        Policy.Validate();
        Countermeasures.Validate();
        Logging.Validate();
        Ui.Validate();
        Incidents.Validate();
        Siem.Validate();
    }
}
