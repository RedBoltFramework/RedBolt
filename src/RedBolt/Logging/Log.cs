using System.Text;
using Microsoft.Extensions.Logging;

namespace RedBolt;

internal static class Log
{
    private static readonly object SyncRoot = new();
    private static ILoggerFactory _loggerFactory = new LoggerFactory();
    private static ILogger _currentLogger = _loggerFactory.CreateLogger("RedBolt");
    private static bool _initialized;

    public static ILogger Logger => _currentLogger;

    public static void Initialize(EngineParameters parameters, string logPath)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var minimumLevel = ParseLevel(parameters.LogLevel);

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

            if (!string.IsNullOrEmpty(logPath))
                builder.AddProvider(new FileLoggerProvider(logPath));
        });

        lock (SyncRoot)
        {
            _loggerFactory.Dispose();
            _loggerFactory = factory;
            _currentLogger = factory.CreateLogger("RedBolt");
            _initialized = true;
        }
    }

    public static void Shutdown()
    {
        lock (SyncRoot)
        {
            if (_initialized)
            {
                _loggerFactory.Dispose();
                _loggerFactory = new LoggerFactory();
                _currentLogger = _loggerFactory.CreateLogger("RedBolt");
                _initialized = false;
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
        private readonly string _path = Path.GetFullPath(path);
        private static readonly object FileSync = new();

        public void Dispose()
        {
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new FileLogger(_path, categoryName, FileSync);
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
                    System.IO.File.AppendAllText(path, entry.ToString());
                    System.IO.File.AppendAllText(path, Environment.NewLine);
                }
            }
        }
    }
}
