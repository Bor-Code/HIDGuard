using System.Globalization;
using System.Text;

namespace HidGuard.Simulator.Verification;

public sealed record VerificationResult
{
    public required IReadOnlyList<VerificationScenarioResult> Scenarios { get; init; }

    public bool AllPassed => Scenarios.All(s => s.Success);
    public int TotalCount => Scenarios.Count;
    public int PassedCount => Scenarios.Count(s => s.Success);
    public int FailedCount => Scenarios.Count(s => !s.Success);
    public TimeSpan TotalDuration => TimeSpan.FromTicks(Scenarios.Sum(s => s.Duration.Ticks));

    public string FormatReport()
    {
        var builder = new StringBuilder();
        builder.AppendLine("================================================================================");
        builder.AppendLine("HIDGUARD END-TO-END AUTOMATED VERIFICATION REPORT");
        builder.AppendLine("================================================================================");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Summary: {PassedCount} / {TotalCount} Scenarios Passed (Total Duration: {TotalDuration.TotalMilliseconds:F1} ms)");
        builder.AppendLine("--------------------------------------------------------------------------------");
        builder.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0,-45} | {1,-8} | {2,-12}", "Scenario", "Status", "Duration"));
        builder.AppendLine("--------------------------------------------------------------------------------");

        foreach (var scenario in Scenarios)
        {
            var status = scenario.Success ? "PASS" : "FAIL";
            builder.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0,-45} | {1,-8} | {2,-12:F1} ms", scenario.ScenarioName, status, scenario.Duration.TotalMilliseconds));

            if (!scenario.Success && !string.IsNullOrWhiteSpace(scenario.FailureReason))
            {
                builder.AppendLine(CultureInfo.InvariantCulture, $"  Reason: {scenario.FailureReason}");
            }

            if (scenario.Metrics.Count > 0)
            {
                foreach (var (key, value) in scenario.Metrics)
                {
                    builder.AppendLine(CultureInfo.InvariantCulture, $"    - {key}: {value}");
                }
            }
        }

        builder.AppendLine("================================================================================");
        return builder.ToString();
    }
}
