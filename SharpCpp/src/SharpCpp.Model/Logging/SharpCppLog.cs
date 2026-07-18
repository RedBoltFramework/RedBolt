using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace SharpCpp.Model.Logging;

/// <summary>
/// Static entry point for SharpCpp build-time logging.
///
/// Configure once at process startup via <see cref="Configure"/>,
/// then create loggers via <see cref="For"/>.
///
/// Every logger produced by this factory shares a single <see cref="Stopwatch"/>
/// started when this class is first touched, so timestamps count from process
/// boot even when the first logger is created long after startup.
/// </summary>
public static class SharpCppLog
{
    private static readonly Stopwatch StartupStopwatch = Stopwatch.StartNew();
    private static LogLevel s_minLevel = LogLevel.Information;
    private static TextWriter? s_fileWriter;

    /// <summary>
    /// The current file writer, if one has been configured.
    /// Read by <see cref="ElapsedTimeLogger"/> at log time so that loggers
    /// created before <see cref="Configure"/> is called still pick up file output.
    /// </summary>
    internal static TextWriter? FileWriter => s_fileWriter;

    /// <summary>
    /// Configure logging for the process. Call once at startup, before creating
    /// any loggers.
    /// </summary>
    /// <param name="minLevel">Minimum log level to emit (default <see cref="LogLevel.Information"/>).</param>
    /// <param name="fileWriter">An optional <see cref="TextWriter"/> for file logging.
    /// The caller owns its lifetime; <see cref="SharpCppLog"/> writes and flushes but
    /// never closes or disposes it.</param>
    public static void Configure(
        LogLevel minLevel = LogLevel.Information,
        TextWriter? fileWriter = null)
    {
        s_minLevel = minLevel;
        s_fileWriter = fileWriter;
    }

    /// <summary>
    /// Create a logger with the given category name.
    /// </summary>
    public static ILogger For(string categoryName)
    {
        return new ElapsedTimeLogger(categoryName, s_minLevel, StartupStopwatch);
    }
}
