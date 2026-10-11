using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using HidGuard.Core.Audit;
using HidGuard.Core.Configuration;
using HidGuard.Core.Countermeasures;
using HidGuard.Core.Input;
using HidGuard.Core.Trust;

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

        services.TryAddSingleton<ITrustStoreStorage>(_ => new FileTrustStoreStorage(trustStorePath));
        services.TryAddSingleton<ITrustStore, TrustStore>();
        services.TryAddSingleton<IConfigurationManager>(_ => new FileConfigurationManager(configPath));
        services.TryAddSingleton<IAuditTrailService>(_ => new FileAuditTrailService(auditDir));
        services.TryAddSingleton<IKeystrokeSource, WindowsRawInputListener>();
        services.TryAddSingleton<IKeystrokeInterceptor, WindowsLowLevelKeyboardHook>();
        services.TryAddSingleton<IWorkstationLocker, WindowsWorkstationLocker>();
        services.TryAddSingleton<IDeviceDisabler, WindowsDeviceDisabler>();

        return services;
    }
}
