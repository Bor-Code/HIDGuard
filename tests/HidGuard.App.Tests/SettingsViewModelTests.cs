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
}
