namespace HidGuard.Core.Policy;

public sealed record PolicyOptions
{
    public UnknownDeviceAction DefaultUnknownDeviceAction { get; init; } = UnknownDeviceAction.AllowWithObservation;
    public AnomalyAction DefaultAnomalyAction { get; init; } = AnomalyAction.LockWorkstation;
    public bool AutoEnrollFirstRun { get; init; } = true;
    public bool EnforceMinimumOneActiveKeyboard { get; init; } = true;

    public void Validate()
    {
        if (!Enum.IsDefined(DefaultUnknownDeviceAction))
        {
            throw new ArgumentOutOfRangeException(nameof(DefaultUnknownDeviceAction), "Invalid UnknownDeviceAction value.");
        }

        if (!Enum.IsDefined(DefaultAnomalyAction))
        {
            throw new ArgumentOutOfRangeException(nameof(DefaultAnomalyAction), "Invalid AnomalyAction value.");
        }
    }
}
