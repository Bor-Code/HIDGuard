using System.Globalization;
using Serilog;
using Serilog.Events;
using HidGuard.Core.Configuration;

namespace HidGuard.Platform.Windows.Logging;

public static class HidGuardLoggingFactory
{
    private const string DefaultOutputTemplate =
        "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}";

    public static ILogger CreateLogger(LoggingConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);
        config.Validate();

        var level = ParseLevel(config.MinimumLevel);
        var logDir = string.IsNullOrWhiteSpace(config.LogDirectory)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HIDGuard", "logs")
            : config.LogDirectory;

        if (!Directory.Exists(logDir))
        {
            Directory.CreateDirectory(logDir);
        }

        var logFilePath = Path.Combine(logDir, "hidguard-.log");

        var loggerConfig = new LoggerConfiguration()
            .MinimumLevel.Is(level)
            .WriteTo.File(
                path: logFilePath,
                rollingInterval: RollingInterval.Day,
                fileSizeLimitBytes: config.FileSizeLimitBytes,
                retainedFileCountLimit: config.RetainedFileCountLimit,
                outputTemplate: DefaultOutputTemplate,
                formatProvider: CultureInfo.InvariantCulture);

        return loggerConfig.CreateLogger();
    }

    private static LogEventLevel ParseLevel(string level)
    {
        if (Enum.TryParse<LogEventLevel>(level, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        return LogEventLevel.Information;
    }
}
