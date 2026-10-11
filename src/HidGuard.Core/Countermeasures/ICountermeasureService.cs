using HidGuard.Core.Policy;

namespace HidGuard.Core.Countermeasures;

public interface ICountermeasureService
{
    Task<CountermeasureExecutionResult> ExecuteAsync(
        PolicyDecision decision,
        int totalKeyboardsAttached,
        CancellationToken cancellationToken = default);

    event EventHandler<CountermeasureExecutionResult>? CountermeasureExecuted;
}
