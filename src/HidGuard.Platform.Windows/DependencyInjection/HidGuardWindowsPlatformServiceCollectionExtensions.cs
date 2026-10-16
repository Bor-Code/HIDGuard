using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using HidGuard.Core.Audit;
using HidGuard.Core.Configuration;
using HidGuard.Core.Countermeasures;
using HidGuard.Core.Incidents;
using HidGuard.Core.Input;
using HidGuard.Core.Security;
using HidGuard.Core.Trust;
using HidGuard.Platform.Windows.Logging;
using HidGuard.Platform.Windows.Security;
using Microsoft.Extensions.Logging;

namespace HidGuard.Platform.Windows.DependencyInjection;

public static class HidGuardWindowsPlatformServiceCollectionExtensions
{
    public static IServiceCollection AddHidGuardWindowsPlatform(
        this IServiceCollection services,
        string? appDataRootDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var root = appDataRootDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HIDGuard");

        var trustStorePath = Path.Combine(root, "truststore.json");
        var configPath = Path.Combine(root, "config.json");
        var auditDir = Path.Combine(root, "audit");
        var incidentDir = Path.Combine(root, "incidents");

        services.TryAddSingleton<ITrustStoreStorage>(_ => new FileTrustStoreStorage(trustStorePath));
        services.TryAddSingleton<ITrustStore, TrustStore>();
        services.TryAddSingleton<IConfigurationManager>(_ => new FileConfigurationManager(configPath));
        services.TryAddSingleton<IAuditTrailService>(_ => new FileAuditTrailService(auditDir));
        services.TryAddSingleton<IDataProtector, DpapiDataProtector>();
        services.TryAddSingleton<IIncidentStore>(sp =>
        {
            var configManager = sp.GetService<IConfigurationManager>();
            var incidentConfig = configManager?.CurrentConfiguration.Incidents ?? new IncidentStoreConfiguration();
            var protector = sp.GetRequiredService<IDataProtector>();
            var logger = sp.GetService<ILogger<EncryptedIncidentStore>>();
            return new EncryptedIncidentStore(incidentDir, incidentConfig, protector, logger);
        });
        services.TryAddSingleton<IncidentPayloadBuffer>(sp =>
        {
            var configManager = sp.GetService<IConfigurationManager>();
            var incidentConfig = configManager?.CurrentConfiguration.Incidents ?? new IncidentStoreConfiguration();
            return new IncidentPayloadBuffer(incidentConfig);
        });
        services.TryAddSingleton<IncidentMaintenanceService>();
        services.TryAddSingleton<IKeystrokeSource, WindowsRawInputListener>();
        services.TryAddSingleton<IKeystrokeInterceptor, WindowsLowLevelKeyboardHook>();
        services.TryAddSingleton<IWorkstationLocker, WindowsWorkstationLocker>();
        services.TryAddSingleton<IDeviceDisabler, WindowsDeviceDisabler>();
        services.TryAddSingleton<IWindowsEventLogService, WindowsEventLogService>();
        services.TryAddSingleton<IStartupService, WindowsStartupService>();

        return services;
    }
}
