using System.Diagnostics.CodeAnalysis;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using HidGuard.App.Services;
using HidGuard.App.ViewModels;
using HidGuard.App.Views;
using HidGuard.Core.Audit;
using HidGuard.Core.Configuration;
using HidGuard.Core.DependencyInjection;
using HidGuard.Core.Events;
using HidGuard.Core.Incidents;
using HidGuard.Core.Pipeline;
using HidGuard.Core.Trust;
using HidGuard.Platform.Windows.DependencyInjection;

namespace HidGuard.App;

[SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "WPF Application manages disposal during OnExit.")]
public partial class App : System.Windows.Application
{
    private ServiceProvider? serviceProvider;
    private SingleInstanceService? singleInstanceService;
    private ITrayIconService? trayIconService;
    private IKeystrokePipeline? pipeline;
    private IncidentMaintenanceService? incidentMaintenanceService;
    private MainWindow? mainWindow;
    private readonly CancellationTokenSource shutdownCts = new();

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        singleInstanceService = new SingleInstanceService();
        if (!singleInstanceService.IsSingleInstance)
        {
            Shutdown();
            return;
        }

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var services = new ServiceCollection();
        ConfigureServices(services);
        serviceProvider = services.BuildServiceProvider();

        var configManager = serviceProvider.GetRequiredService<IConfigurationManager>();
        var initialConfig = await configManager.LoadAsync(shutdownCts.Token).ConfigureAwait(true);

        pipeline = serviceProvider.GetRequiredService<IKeystrokePipeline>();
        await pipeline.StartAsync(shutdownCts.Token).ConfigureAwait(true);

        incidentMaintenanceService = serviceProvider.GetService<IncidentMaintenanceService>();
        incidentMaintenanceService?.Start();

        var mainViewModel = serviceProvider.GetRequiredService<MainViewModel>();
        mainWindow = new MainWindow(mainViewModel);

        trayIconService = serviceProvider.GetRequiredService<ITrayIconService>();
        trayIconService.Initialize(
            onOpenRequested: () => Dispatcher.Invoke(() => mainWindow.ShowAndActivate()),
            onToggleProtectionRequested: () => Dispatcher.Invoke(() => mainViewModel.IsProtectionActive = !mainViewModel.IsProtectionActive),
            onSettingsRequested: () => Dispatcher.Invoke(() =>
            {
                mainViewModel.NavigateTo("Settings");
                mainWindow.ShowAndActivate();
            }),
            onAuditRequested: () => Dispatcher.Invoke(() =>
            {
                mainViewModel.NavigateTo("AuditLog");
                mainWindow.ShowAndActivate();
            }),
            onExitRequested: () => Dispatcher.Invoke(ShutdownApplication));

        if (!initialConfig.Ui.StartMinimizedToTray)
        {
            mainWindow.ShowAndActivate();
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        await shutdownCts.CancelAsync().ConfigureAwait(false);

        if (incidentMaintenanceService != null)
        {
            await incidentMaintenanceService.DisposeAsync().ConfigureAwait(false);
        }

        if (pipeline != null)
        {
            await pipeline.StopAsync(CancellationToken.None).ConfigureAwait(false);
        }

        trayIconService?.Dispose();
        singleInstanceService?.Dispose();
        serviceProvider?.Dispose();
        shutdownCts.Dispose();

        base.OnExit(e);
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddHidGuardCore();
        services.AddHidGuardWindowsPlatform();

        services.AddSingleton<IUacElevationService, UacElevationService>();
        services.AddSingleton<ITrayIconService, TrayIconService>();
        services.AddSingleton<IDevicePromptService, WpfDevicePromptService>();

        services.AddSingleton<OverviewViewModel>();
        services.AddSingleton<DevicesViewModel>();
        services.AddSingleton<MetricsViewModel>();
        services.AddSingleton<AuditLogViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<MainViewModel>();
    }

    private void ShutdownApplication()
    {
        mainWindow?.ForceClose();
        Shutdown();
    }
}
