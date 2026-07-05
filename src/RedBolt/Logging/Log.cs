using System.Text;
using Microsoft.Extensions.Logging;

namespace RedBolt;

internal static class Log
{
    private static readonly object SyncRoot = new();
    private static ILoggerFactory loggerFactory = new LoggerFactory();
    private static ILogger currentLogger = loggerFactory.CreateLogger("RedBolt");
    private static bool initialized;

    public static ILogger Logger => currentLogger;

    public static void Initialize(EngineParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var minimumLevel = ParseLevel(parameters.LogLevel);
        var logPath = string.IsNullOrWhiteSpace(parameters.LogName)
            ? "RedBolt.log"
            : Path.GetFullPath(parameters.LogName);

        var factory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(minimumLevel);
            builder.ClearProviders();

            if (!parameters.LogQuiet)
            {
                builder.AddSimpleConsole(options =>
                {
                    options.TimestampFormat = "HH:mm:ss.fff ";
                    options.SingleLine = true;
                });
            }

            builder.AddProvider(new FileLoggerProvider(logPath));
        });

        lock (SyncRoot)
        {
            loggerFactory.Dispose();
            loggerFactory = factory;
            currentLogger = factory.CreateLogger("RedBolt");
            initialized = true;
        }
    }

    public static void Shutdown()
    {
        lock (SyncRoot)
        {
            if (initialized)
            {
                loggerFactory.Dispose();
                loggerFactory = new LoggerFactory();
                currentLogger = loggerFactory.CreateLogger("RedBolt");
                initialized = false;
            }
        }
    }

    private static LogLevel ParseLevel(string logLevel)
    {
        return logLevel.Trim().ToLowerInvariant() switch
        {
            "trace" => LogLevel.Trace,
            "debug" => LogLevel.Debug,
            "info" => LogLevel.Information,
            "warning" => LogLevel.Warning,
            "error" => LogLevel.Error,
            "critical" => LogLevel.Critical,
            "none" => LogLevel.None,
            _ => throw new ArgumentException($"Unsupported log level '{logLevel}'.", nameof(logLevel))
        };
    }

    private sealed class FileLoggerProvider(string path) : ILoggerProvider
    {
        private readonly string path = Path.GetFullPath(path);
        private static readonly object FileSync = new();

        public void Dispose()
        {
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new FileLogger(path, categoryName, FileSync);
        }

        private sealed class FileLogger(string path, string categoryName, object sync) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull
            {
                return null;
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return logLevel is not LogLevel.None;
            }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                if (!IsEnabled(logLevel))
                    return;

                ArgumentNullException.ThrowIfNull(formatter);
                var message = formatter(state, exception);
                if (string.IsNullOrWhiteSpace(message))
                    return;

                var entry = new StringBuilder()
                    .Append(DateTimeOffset.UtcNow.ToString("O"))
                    .Append(" [")
                    .Append(categoryName)
                    .Append("] [")
                    .Append(logLevel)
                    .Append("] ")
                    .Append(message);

                if (exception is not null)
                    entry.AppendLine().Append(exception);

                lock (sync)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
                    File.AppendAllText(path, entry.ToString());
                    File.AppendAllText(path, Environment.NewLine);
                }
            }
        }
    }
}
