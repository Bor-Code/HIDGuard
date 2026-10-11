using Shouldly;
using Xunit;
using HidGuard.Core.Configuration;
using HidGuard.Core.Timing;

namespace HidGuard.Core.Tests;

public sealed class ConfigurationTests
{
    [Fact]
    public void DefaultConfigurationShouldValidateSuccessfully()
    {
        var config = new HidGuardConfiguration();

        Should.NotThrow(() => config.Validate());
    }

    [Fact]
    public void CountermeasureConfigurationWithInvalidDurationShouldThrow()
    {
        var invalid = new CountermeasureConfiguration { TemporarySuppressionSeconds = 0 };

        Should.Throw<ArgumentOutOfRangeException>(() => invalid.Validate());
    }

    [Fact]
    public void LoggingConfigurationWithInvalidMinimumLevelShouldThrow()
    {
        var invalid = new LoggingConfiguration { MinimumLevel = "InvalidLevel" };

        Should.Throw<ArgumentException>(() => invalid.Validate());
    }

    [Fact]
    public void LoggingConfigurationWithInvalidRetentionShouldThrow()
    {
        var invalid = new LoggingConfiguration { RetainedFileCountLimit = 0 };

        Should.Throw<ArgumentOutOfRangeException>(() => invalid.Validate());
    }

    [Fact]
    public void UiConfigurationWithInvalidThemeShouldThrow()
    {
        var invalid = new UiConfiguration { Theme = "NeonPink" };

        Should.Throw<ArgumentException>(() => invalid.Validate());
    }

    [Fact]
    public async Task FileConfigurationManagerShouldRoundtripConfigurationAtomically()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "HIDGuard_ConfigTest_" + Guid.NewGuid().ToString("N"));
        var configFile = Path.Combine(tempDir, "config.json");

        try
        {
            using var manager = new FileConfigurationManager(configFile);

            var loaded = await manager.LoadAsync();
            loaded.ShouldNotBeNull();
            File.Exists(configFile).ShouldBeTrue();

            var modified = new HidGuardConfiguration
            {
                Timing = new TimingThresholds { WindowSize = 50 },
                Ui = new UiConfiguration { Theme = "Light" }
            };

            await manager.SaveAsync(modified);

            using var reloadedManager = new FileConfigurationManager(configFile);
            var reloaded = await reloadedManager.LoadAsync();

            reloaded.Timing.WindowSize.ShouldBe(50);
            reloaded.Ui.Theme.ShouldBe("Light");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task FileConfigurationManagerWhenFileCorruptedShouldFallbackToDefaults()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "HIDGuard_ConfigCorrupt_" + Guid.NewGuid().ToString("N"));
        var configFile = Path.Combine(tempDir, "config.json");

        try
        {
            Directory.CreateDirectory(tempDir);
            await File.WriteAllTextAsync(configFile, "{ invalid json structure !!!");

            using var manager = new FileConfigurationManager(configFile);
            var loaded = await manager.LoadAsync();

            loaded.ShouldNotBeNull();
            loaded.Ui.Theme.ShouldBe("Dark");
            loaded.Timing.WindowSize.ShouldBe(30);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}
