using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using HidGuard.Core.Countermeasures;
using HidGuard.Core.Events;
using HidGuard.Core.Pipeline;
using HidGuard.Core.Policy;
using HidGuard.Core.Scoring;
using HidGuard.Core.Timing;

namespace HidGuard.Core.DependencyInjection;

public static class HidGuardCoreServiceCollectionExtensions
{
    public static IServiceCollection AddHidGuardCore(
        this IServiceCollection services,
        PolicyOptions? policyOptions = null,
        TimingThresholds? timingThresholds = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var effectiveThresholds = timingThresholds ?? new TimingThresholds();
        effectiveThresholds.Validate();
        services.TryAddSingleton(effectiveThresholds);

        var effectivePolicy = policyOptions ?? new PolicyOptions();
        effectivePolicy.Validate();
        services.TryAddSingleton(effectivePolicy);

        services.TryAddSingleton<IAnomalyScorer, AnomalyScorer>();
        services.TryAddSingleton<ITimingAnalysisEngine, TimingAnalysisEngine>();
        services.TryAddSingleton<IPolicyEngine, PolicyEngine>();
        services.TryAddSingleton<IPipelineEventAggregator, PipelineEventAggregator>();
        services.TryAddSingleton<ICountermeasureService, CountermeasureService>();
        services.TryAddSingleton<IKeystrokePipeline, KeystrokePipeline>();

        return services;
    }
}
