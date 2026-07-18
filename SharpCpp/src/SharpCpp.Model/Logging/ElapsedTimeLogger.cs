using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace SharpCpp.Model.Logging;

/// <summary>
/// An <see cref="ILogger"/> implementation that writes messages to the console
/// with a timestamp counting from process startup, a log-level label, and the
/// logger category name.
/// </summary>
internal sealed class ElapsedTimeLogger : ILogger
{
    private readonly string _categoryName;
    private readonly LogLevel _minLevel;
    private readonly Stopwatch _stopwatch;
    private readonly object _lock = new();

    public ElapsedTimeLogger(
        string categoryName,
        LogLevel minLevel,
        Stopwatch stopwatch)
    {
        _categoryName = categoryName;
        _minLevel = minLevel;
        _stopwatch = stopwatch;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= _minLevel && logLevel != LogLevel.None;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var elapsed = _stopwatch.Elapsed;
        var elapsedFormatted = FormatElapsed(elapsed);

        var levelLabel = logLevel switch
        {
            LogLevel.Trace => "TRC",
            LogLevel.Debug => "DBG",
            LogLevel.Information => "INF",
            LogLevel.Warning => "WRN",
            LogLevel.Error => "ERR",
            LogLevel.Critical => "CRT",
            _ => "???"
        };

        var message = formatter(state, exception);
        var output = $"[+{elapsedFormatted}] [{levelLabel}] [{_categoryName}] {message}";

        // Warnings and above go to stderr; Info and below go to stdout.
        var writer = logLevel >= LogLevel.Warning ? Console.Error : Console.Out;

        lock (_lock)
        {
            writer.WriteLine(output);
            SharpCppLog.FileWriter?.WriteLine(output);
            SharpCppLog.FileWriter?.Flush();
        }
    }

    private static string FormatElapsed(TimeSpan elapsed)
    {
        if (elapsed.TotalHours >= 1)
        {
            return $"{(int)elapsed.TotalHours}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";
        }

        if (elapsed.TotalSeconds >= 10)
        {
            return $"{elapsed.TotalSeconds:F1}s";
        }

        // Sub-second precision so fast operations are distinguishable from zero.
        return $"{elapsed.TotalSeconds:F3}s";
    }
}
