using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using HidGuard.Core.Countermeasures;
using HidGuard.Core.Input;
using HidGuard.Core.Trust;

namespace HidGuard.Platform.Windows.DependencyInjection;

public static class HidGuardWindowsPlatformServiceCollectionExtensions
{
    public static IServiceCollection AddHidGuardWindowsPlatform(
        this IServiceCollection services,
        string? trustStoreFilePath = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var defaultPath = trustStoreFilePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HIDGuard",
            "truststore.json");

        services.TryAddSingleton<ITrustStoreStorage>(_ => new FileTrustStoreStorage(defaultPath));
        services.TryAddSingleton<ITrustStore, TrustStore>();
        services.TryAddSingleton<IKeystrokeSource, WindowsRawInputListener>();
        services.TryAddSingleton<IKeystrokeInterceptor, WindowsLowLevelKeyboardHook>();
        services.TryAddSingleton<IWorkstationLocker, WindowsWorkstationLocker>();
        services.TryAddSingleton<IDeviceDisabler, WindowsDeviceDisabler>();

        return services;
    }
}
