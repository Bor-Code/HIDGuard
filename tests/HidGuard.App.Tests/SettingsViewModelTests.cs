using System.IO;
using Shouldly;
using Xunit;
using HidGuard.App.ViewModels;
using HidGuard.Core.Configuration;

namespace HidGuard.App.Tests;

public sealed class SettingsViewModelTests
{
    [Fact]
    public async Task SettingsViewModelShouldLoadAndSaveConfiguration()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), "HIDGuard_ConfigVM_" + Guid.NewGuid().ToString("N") + ".json");

        try
        {
            using var configManager = new FileConfigurationManager(tempFile);
            await configManager.LoadAsync();

            var viewModel = new SettingsViewModel(configManager);

            viewModel.WindowSize.ShouldBe(30);
            viewModel.LockWorkstationEnabled.ShouldBeTrue();

            viewModel.WindowSize = 45;
            viewModel.LockWorkstationEnabled = false;

            viewModel.SaveSettingsCommand.Execute(null);

            await Task.Delay(100);

            configManager.CurrentConfiguration.Timing.WindowSize.ShouldBe(45);
            configManager.CurrentConfiguration.Countermeasures.LockWorkstationEnabled.ShouldBeFalse();
            viewModel.FeedbackMessage.ShouldContain("successfully");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task ResetDefaultsShouldRestoreDefaultValues()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), "HIDGuard_ConfigReset_" + Guid.NewGuid().ToString("N") + ".json");

        try
        {
            using var configManager = new FileConfigurationManager(tempFile);
            await configManager.LoadAsync();

            var viewModel = new SettingsViewModel(configManager)
            {
                WindowSize = 100,
                LockWorkstationEnabled = false
            };

            viewModel.ResetDefaultsCommand.Execute(null);

            viewModel.WindowSize.ShouldBe(30);
            viewModel.LockWorkstationEnabled.ShouldBeTrue();
            viewModel.StartWithWindows.ShouldBeFalse();
            viewModel.FeedbackMessage.ShouldContain("defaults");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private sealed class StubStartupService : IStartupService
    {
        public bool IsEnabled { get; set; }
        public bool LastSetCall { get; private set; }

        public bool IsStartWithWindowsEnabled() => IsEnabled;

        public bool SetStartWithWindows(bool enabled)
        {
            LastSetCall = enabled;
            IsEnabled = enabled;
            return true;
        }
    }

    [Fact]
    public async Task SettingsViewModelShouldInitializeStartWithWindowsFromStartupService()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), "HIDGuard_ConfigStartup_" + Guid.NewGuid().ToString("N") + ".json");

        try
        {
            using var configManager = new FileConfigurationManager(tempFile);
            await configManager.LoadAsync();

            var startupService = new StubStartupService { IsEnabled = true };
            var viewModel = new SettingsViewModel(configManager, startupService);

            viewModel.StartWithWindows.ShouldBeTrue();
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task SettingsViewModelShouldSaveStartWithWindowsToStartupService()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), "HIDGuard_ConfigStartupSave_" + Guid.NewGuid().ToString("N") + ".json");

        try
        {
            using var configManager = new FileConfigurationManager(tempFile);
            await configManager.LoadAsync();

            var startupService = new StubStartupService { IsEnabled = false };
            var viewModel = new SettingsViewModel(configManager, startupService);

            viewModel.StartWithWindows = true;
            viewModel.SaveSettingsCommand.Execute(null);

            await Task.Delay(100);

            startupService.LastSetCall.ShouldBeTrue();
            startupService.IsEnabled.ShouldBeTrue();
            configManager.CurrentConfiguration.Ui.StartWithWindows.ShouldBeTrue();
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task SettingsViewModelShouldSaveTheme()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), "HIDGuard_ConfigTheme_" + Guid.NewGuid().ToString("N") + ".json");

        try
        {
            using var configManager = new FileConfigurationManager(tempFile);
            await configManager.LoadAsync();

            var viewModel = new SettingsViewModel(configManager);
            viewModel.Theme.ShouldBe("RedBlack");

            viewModel.Theme = "GreenBlack";
            viewModel.SaveSettingsCommand.Execute(null);

            await Task.Delay(100);

            configManager.CurrentConfiguration.Ui.Theme.ShouldBe("GreenBlack");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}
