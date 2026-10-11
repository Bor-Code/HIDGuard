namespace HidGuard.Core.Configuration;

public sealed record CountermeasureConfiguration
{
    public bool LockWorkstationEnabled { get; init; } = true;
    public bool DeviceDisableEnabled { get; init; } = true;
    public bool KeySuppressionEnabled { get; init; } = true;
    public int TemporarySuppressionSeconds { get; init; } = 3;

    public void Validate()
    {
        if (TemporarySuppressionSeconds is < 1 or > 60)
        {
            throw new ArgumentOutOfRangeException(nameof(TemporarySuppressionSeconds), "TemporarySuppressionSeconds must be between 1 and 60.");
        }
    }
}
