using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using HidGuard.Core.Configuration;
using HidGuard.Core.Countermeasures;
using HidGuard.Core.Events;
using HidGuard.Core.Pipeline;
using HidGuard.Core.Policy;
using HidGuard.Core.Scoring;
using HidGuard.Core.Siem;
using HidGuard.Core.Timing;

namespace HidGuard.Core.DependencyInjection;

public static class HidGuardCoreServiceCollectionExtensions
{
    public static IServiceCollection AddHidGuardCore(
        this IServiceCollection services,
        PolicyOptions? policyOptions = null,
        TimingThresholds? timingThresholds = null,
        SiemConfiguration? siemConfiguration = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var effectiveThresholds = timingThresholds ?? new TimingThresholds();
        effectiveThresholds.Validate();
        services.TryAddSingleton(effectiveThresholds);

        var effectivePolicy = policyOptions ?? new PolicyOptions();
        effectivePolicy.Validate();
        services.TryAddSingleton(effectivePolicy);

        var effectiveSiem = siemConfiguration ?? new SiemConfiguration();
        effectiveSiem.Validate();
        services.TryAddSingleton(effectiveSiem);

        services.TryAddSingleton<IAnomalyScorer, AnomalyScorer>();
        services.TryAddSingleton<ITimingAnalysisEngine, TimingAnalysisEngine>();
        services.TryAddSingleton<IPolicyEngine, PolicyEngine>();
        services.TryAddSingleton<IPipelineEventAggregator, PipelineEventAggregator>();
        services.TryAddSingleton<ICountermeasureService, CountermeasureService>();
        services.TryAddSingleton<IKeystrokePipeline, KeystrokePipeline>();
        services.TryAddSingleton<ISiemTransport, NetworkSiemTransport>();
        services.TryAddSingleton<ISecurityEventForwarder, SecurityEventForwarder>();

        return services;
    }
}
