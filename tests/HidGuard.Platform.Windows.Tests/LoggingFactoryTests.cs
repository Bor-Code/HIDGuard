using Serilog;
using Shouldly;
using Xunit;
using HidGuard.Core.Configuration;
using HidGuard.Platform.Windows.Logging;

namespace HidGuard.Platform.Windows.Tests;

public sealed class LoggingFactoryTests
{
    [Fact]
    public void CreateLoggerShouldThrowWhenConfigIsNull()
    {
        Should.Throw<ArgumentNullException>(() => HidGuardLoggingFactory.CreateLogger(null!));
    }

    [Fact]
    public void CreateLoggerShouldCreateValidSerilogLoggerAndWriteToFile()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "HIDGuard_LogTest_" + Guid.NewGuid().ToString("N"));

        try
        {
            var config = new LoggingConfiguration
            {
                MinimumLevel = "Information",
                LogDirectory = tempDir,
                RetainedFileCountLimit = 5,
                FileSizeLimitBytes = 1048576,
                EnableConsoleLogging = false
            };

            var logger = HidGuardLoggingFactory.CreateLogger(config);
            logger.ShouldNotBeNull();

            logger.Information("Security telemetry subsystem initialized.");

            if (logger is IDisposable disposableLogger)
            {
                disposableLogger.Dispose();
            }

            Log.CloseAndFlush();

            Directory.Exists(tempDir).ShouldBeTrue();
            var logFiles = Directory.GetFiles(tempDir, "hidguard-*.log");
            logFiles.Length.ShouldBeGreaterThanOrEqualTo(1);

            var logContent = File.ReadAllText(logFiles[0]);
            logContent.ShouldContain("Security telemetry subsystem initialized.");
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
            catch (IOException)
            {
            }
        }
    }
}
