using Microsoft.Extensions.Logging;
using MicrosoftLogLevel = Microsoft.Extensions.Logging.LogLevel;
using System.Globalization;
using System.Text;

using RedBolt.Core;
using RedBolt.IO;
using FileMode = global::RedBolt.IO.FileMode;
using RedBolt.Platform;

namespace RedBolt.Logging;

public enum LogLevel
{
    /// <summary>Trace message level.</summary>
    Trace = 0,
    /// <summary>Debug message level. By default only shown in debug mode.</summary>
    Debug = 1,
    /// <summary>Informative message level.</summary>
    Info = 2,
    /// <summary>Warning message level.</summary>
    Warning = 3,
    /// <summary>Error message level.</summary>
    Error = 4,
    /// <summary>Disable all log messages.</summary>
    None = 5,
    /// <summary>Number of log levels.</summary>
    MaxLogLevels
}

/// <summary>Stored log message from another thread.</summary>
public struct StoredLogMessage
{
    /// <summary>Construct undefined.</summary>
    public StoredLogMessage()
    {
        Level = default;
        Timestamp = default;
        Logger = string.Empty;
        Message = string.Empty;
    }

    /// <summary>Construct with parameters.</summary>
    public StoredLogMessage(LogLevel level, long timestamp, string logger, string message)
    {
        Level = level;
        Timestamp = timestamp;
        Logger = logger;
        Message = message;
    }

    /// <summary>Message level. -1 for raw messages.</summary>
    public LogLevel Level;
    /// <summary>Timestamp when message was logged.</summary>
    public long Timestamp;
    /// <summary>Message text.</summary>
    public string Logger;
    /// <summary>Message text.</summary>
    public string Message;
}

/// <summary>Configuration of log file sink.</summary>
public struct LogFileParams
{
    public LogFileParams()
    {
        RotateOnOpen = true;
        RotateBySize = false;
        MaxFiles = 20;
        MaxSize = 20 * 1024 * 1024;
    }

    public bool RotateOnOpen;
    public bool RotateBySize;
    public uint MaxFiles;
    public uint MaxSize;
}

/// <summary>Forwards a message to underlying logger. Use <see cref="Log.GetLogger()"/> to obtain instance of this class.</summary>
public class Logger : ILogger, ICloneable
{
    private sealed class LoggerHandle(Log log, string name)
    {
        public Log Log { get; } = log;
        public string Name { get; } = name;
    }

    private readonly LoggerHandle? _logger;

    public Logger()
    {
    }

    internal Logger(Log log, string name)
    {
        _logger = new LoggerHandle(log, name);
    }

    private Logger(Logger other)
    {
        _logger = other._logger;
    }

    public object Clone() => new Logger(this);

    /// <summary>Write formatted message to log if there are extra arguments.</summary>
    public void Write(LogLevel level, string format, params object?[] args)
    {
        Write(level, Format(format, args));
    }

    /// <summary>Write message to log as is if there's no extra arguments.</summary>
    public void Write(LogLevel level, string message)
    {
        _logger?.Log.Write(_logger.Name, level, message);
    }

    public void Trace(string format, params object?[] args) => Write(LogLevel.Trace, format, args);
    public void Debug(string format, params object?[] args) => Write(LogLevel.Debug, format, args);
    public void Info(string format, params object?[] args) => Write(LogLevel.Info, format, args);
    public void Warning(string format, params object?[] args) => Write(LogLevel.Warning, format, args);
    public void Error(string format, params object?[] args) => Write(LogLevel.Error, format, args);

    IDisposable? ILogger.BeginScope<TState>(TState state) => null;

    bool ILogger.IsEnabled(MicrosoftLogLevel logLevel) => IsEnabled(ConvertLogLevel(logLevel));

    public void Log<TState>(MicrosoftLogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(ConvertLogLevel(logLevel)))
            return;

        var message = formatter(state, exception);
        if (exception is not null)
            message = string.Concat(message, Environment.NewLine, exception);
        Write(ConvertLogLevel(logLevel), message);
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        var message = formatter(state, exception);
        if (exception is not null)
            message = string.Concat(message, Environment.NewLine, exception);
        Write(logLevel, message);
    }

    private bool IsEnabled(LogLevel level) => _logger is not null && _logger.Log.IsEnabled(level);

    private static LogLevel ConvertLogLevel(MicrosoftLogLevel level)
    {
        return level switch
        {
            MicrosoftLogLevel.Trace => LogLevel.Trace,
            MicrosoftLogLevel.Debug => LogLevel.Debug,
            MicrosoftLogLevel.Information => LogLevel.Info,
            MicrosoftLogLevel.Warning => LogLevel.Warning,
            MicrosoftLogLevel.Error or MicrosoftLogLevel.Critical => LogLevel.Error,
            _ => LogLevel.None
        };
    }

    private static string Format(string format, object?[] args)
    {
        if (args.Length == 0)
            return format;

        var result = new StringBuilder(format.Length + args.Length * 8);
        var argumentIndex = 0;
        for (var index = 0; index < format.Length; ++index)
        {
            if (format[index] == '{' && index + 1 < format.Length && format[index + 1] == '{')
            {
                result.Append('{');
                ++index;
            }
            else if (format[index] == '}' && index + 1 < format.Length && format[index + 1] == '}')
            {
                result.Append('}');
                ++index;
            }
            else if (format[index] == '{' && argumentIndex < args.Length)
            {
                var end = format.IndexOf('}', index + 1);
                if (end == -1)
                {
                    result.Append(format[index]);
                    continue;
                }

                result.Append(Convert.ToString(args[argumentIndex++], CultureInfo.InvariantCulture));
                index = end;
            }
            else
                result.Append(format[index]);
        }
        return result.ToString();
    }
}

internal class LogImpl : RedObject
{
    private struct MessageInfo
    {
        public int Hash;
        public DateTime LastMessageTime;
    }

    private readonly object _sinkMutex = new();
    private StreamWriter? _fileSink;
    private string? _fileName;
    private LogFileParams _fileParams;
    private readonly List<MessageInfo> _lastMessages = new(10);
    private uint _skipCounter;

    public LogImpl()
    {
        for (var i = 0; i < 10; ++i)
            _lastMessages.Add(default);
    }

    public void Open(string fileName, in LogFileParams parameters)
    {
        lock (_sinkMutex)
        {
            CloseFile();
            _fileName = fileName;
            _fileParams = parameters;

            if (parameters.RotateOnOpen)
                RotateFiles();

            var directoryName = Path.GetDirectoryName(fileName);
            if (!string.IsNullOrEmpty(directoryName))
                Directory.CreateDirectory(directoryName);
            _fileSink = new StreamWriter(new FileStream(fileName, System.IO.FileMode.Append, FileAccess.Write, FileShare.Read));
        }
    }

    public void Close()
    {
        lock (_sinkMutex)
            CloseFile();
    }

    public bool IsOpen => _fileSink is not null;

    public void Write(Log log, string logger, LogLevel level, long timestamp, string message)
    {
        lock (_sinkMutex)
        {
            if (level >= LogLevel.Error && IsDuplicateMessage(message))
            {
                ++_skipCounter;
                return;
            }

            if (_skipCounter > 0)
            {
                WriteMessage(log, logger, LogLevel.Debug, timestamp,
                    $"Skipped {_skipCounter.ToString(CultureInfo.InvariantCulture)} duplicate messages..");
                _skipCounter = 0;
            }

            WriteMessage(log, logger, level, timestamp, message);
        }
    }

    public override void Dispose()
    {
        Close();
        base.Dispose();
    }

    private void WriteMessage(Log log, string logger, LogLevel level, long timestamp, string message)
    {
        var entry = FormatEntry(log.LogFormat, logger, level, timestamp, message);
        if (!log.IsQuiet)
            Console.WriteLine(entry);

        if (_fileSink is not null)
        {
            if (_fileParams.RotateBySize && _fileParams.MaxSize > 0
                && _fileSink.BaseStream.Length + Encoding.UTF8.GetByteCount(entry) + Environment.NewLine.Length > _fileParams.MaxSize)
            {
                CloseFile();
                RotateFiles();
                _fileSink = new StreamWriter(new FileStream(_fileName!, System.IO.FileMode.Append, FileAccess.Write, FileShare.Read));
            }
            _fileSink.WriteLine(entry);
            _fileSink.Flush();
        }

        log.SendMessageEvent(level, timestamp, logger, message);
    }

    private bool IsDuplicateMessage(string message)
    {
        var messageHash = StringComparer.Ordinal.GetHashCode(message);
        var now = DateTime.UtcNow;
        var index = _lastMessages.FindIndex(info => info.Hash == messageHash);
        if (index == -1 || now - _lastMessages[index].LastMessageTime > TimeSpan.FromSeconds(5))
        {
            _lastMessages[^1] = new MessageInfo { Hash = messageHash, LastMessageTime = now };
            var messageInfo = _lastMessages[^1];
            _lastMessages.RemoveAt(_lastMessages.Count - 1);
            _lastMessages.Insert(0, messageInfo);
            return false;
        }

        var duplicate = _lastMessages[index];
        _lastMessages.RemoveAt(index);
        _lastMessages.Insert(0, duplicate);
        return true;
    }

    private void RotateFiles()
    {
        if (_fileName is null || _fileParams.MaxFiles == 0 || !System.IO.File.Exists(_fileName))
            return;

        for (var index = _fileParams.MaxFiles - 1; index > 0; --index)
        {
            var source = index == 1 ? _fileName : $"{_fileName}.{index - 1}";
            var destination = $"{_fileName}.{index}";
            if (System.IO.File.Exists(source))
                System.IO.File.Move(source, destination, true);
        }
    }

    private void CloseFile()
    {
        _fileSink?.Dispose();
        _fileSink = null;
    }

    private static string FormatEntry(string format, string logger, LogLevel level, long timestamp, string message)
    {
        var time = DateTimeOffset.FromUnixTimeSeconds(timestamp).ToLocalTime();
        return format
            .Replace("%H", time.ToString("HH", CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("%M", time.ToString("mm", CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("%S", time.ToString("ss", CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("%l", GetLogLevelName(level), StringComparison.Ordinal)
            .Replace("%n", logger, StringComparison.Ordinal)
            .Replace("%v", message, StringComparison.Ordinal);
    }

    private static string GetLogLevelName(LogLevel level)
    {
        return level switch
        {
            LogLevel.Trace => "trace",
            LogLevel.Debug => "debug",
            LogLevel.Info => "info",
            LogLevel.Warning => "warning",
            LogLevel.Error => "error",
            _ => string.Empty
        };
    }
}

/// <summary>Logging subsystem.</summary>
public class Log : RedObject
{
    private static readonly string NullDevice = OperatingSystem.IsWindows() ? "NUL" : "/dev/null";
    private static bool _threadErrorDisplayed;

    /// <summary>Implementation hiding logger class types from public headers.</summary>
    private readonly LogImpl _impl = new();
    /// <summary>Log format pattern.</summary>
    private string _formatPattern = "[%H:%M:%S] [%l] [%n] : %v";
    /// <summary>Mutex for threaded operation.</summary>
    private readonly object _logMutex = new();
    /// <summary>Log messages from other threads.</summary>
    private readonly LinkedList<StoredLogMessage> _threadMessages = [];
    /// <summary>Logging level.</summary>
#if DEBUG
    private LogLevel _level = LogLevel.Debug;
#else
    private LogLevel _level = LogLevel.Info;
#endif
    /// <summary>In write flag to prevent recursion.</summary>
    private bool _inWrite;
    /// <summary>Quiet mode flag.</summary>
    private bool _quiet;
    /// <summary>Default logger used to log messages from Urho3D library.</summary>
    private readonly Logger _defaultLogger;

    /// <summary>Construct.</summary>
    public Log()
    {
        _defaultLogger = GetOrCreateLogger("main");
        SubscribeToEvent<CoreEvents.EndFrameEvent>((ref CoreEvents.EndFrameEvent _) => PumpThreadMessages());
    }

    /// <summary>Open the log file.</summary>
    public void Open(string fileName)
    {
        var parameters = new LogFileParams();
        Open(fileName, in parameters);
    }

    /// <summary>Open the log file.</summary>
    public void Open(string fileName, in LogFileParams parameters)
    {
        if (!Context.Instance.RuntimeServices.Platform.SupportsFileLogging)
            return;

        if (string.IsNullOrEmpty(fileName) || fileName == NullDevice)
            return;

        Close();
        if (parameters.RotateBySize || parameters.RotateOnOpen)
        {
            try
            {
                _impl.Open(fileName, in parameters);
            }
            catch (Exception exception)
            {
                GetLogger().Error("Cannot open log file: {}", exception.Message);
            }
        }

        if (!_impl.IsOpen)
        {
            try
            {
                var basicParameters = parameters;
                basicParameters.RotateOnOpen = false;
                basicParameters.RotateBySize = false;
                _impl.Open(fileName, in basicParameters);
            }
            catch (Exception exception)
            {
                GetLogger().Error("Cannot open log file: {}", exception.Message);
                return;
            }
        }
    }

    /// <summary>Close the log file.</summary>
    public void Close()
    {
        if (Context.Instance.RuntimeServices.Platform.SupportsFileLogging)
            _impl.Close();
    }

    /// <summary>Set logging level.</summary>
    public void SetLevel(LogLevel level)
    {
        if (level < LogLevel.Trace || level > LogLevel.None)
        {
            var logger = GetLogger();
            logger.Error("Attempted to set erroneous log level {}", (int)level);
            return;
        }

        _level = level;
    }

    /// <summary>Set whether to timestamp log messages.</summary>
    public void SetLogFormat(string format)
    {
        _formatPattern = format;
    }

    /// <summary>Set quiet mode ie. only print error entries to standard error stream (which is normally redirected to console also). Output to log file is not affected by this mode.</summary>
    public void SetQuiet(bool quiet)
    {
        _quiet = quiet;
    }

    /// <summary>Return logging level.</summary>
    public LogLevel Level => _level;

    /// <summary>Return whether log is in quiet mode (only errors printed to standard error stream).</summary>
    public bool IsQuiet => _quiet;

    internal string LogFormat => _formatPattern;

    /// <summary>Returns a logger with specified name.</summary>
    public static Logger GetLogger(string name)
    {
        var logInstance = Context.Instance.GetSubsystem<Log>();
        return logInstance is null ? new Logger() : logInstance.GetOrCreateLogger(name);
    }

    /// <summary>Returns default logger.</summary>
    public static Logger GetLogger()
    {
        var logInstance = Context.Instance.GetSubsystem<Log>();
        return logInstance is null ? new Logger() : (Logger)logInstance._defaultLogger.Clone();
    }

    public bool IsEnabled(LogLevel level) => level >= _level && level < LogLevel.None;

    internal void Write(string logger, LogLevel level, string message)
    {
        if (level is LogLevel.None or LogLevel.MaxLogLevels || level < LogLevel.Trace || level > LogLevel.Error)
            level = LogLevel.Warning;
        if (!IsEnabled(level))
            return;

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        _impl.Write(this, logger, level, timestamp, message);
    }

    public void SendMessageEvent(LogLevel level, long timestamp, string logger, string message)
    {
        // No-op if illegal level
        if (level < LogLevel.Trace || level >= LogLevel.None)
            return;

        var logInstance = Context.Instance.GetSubsystem<Log>();
        if (logInstance is null)
            return;

        // If not in the main thread, store message for later processing
        if (Environment.CurrentManagedThreadId != WorkQueue.GetMainThreadId())
        {
            lock (_logMutex)
                _threadMessages.AddLast(new StoredLogMessage(level, timestamp, logger, message));
            return;
        }

        if (_inWrite)
            return;

        _inWrite = true;
        var eventData = new IOEvents.LogMessage
        {
            Level = (int)level,
            Time = (uint)timestamp,
            Logger = logger,
            Message = message
        };
        SendEvent(ref eventData);
        _inWrite = false;
    }

    public void PumpThreadMessages()
    {
        // If the MainThreadID is not valid, processing this loop can potentially be endless
        if (Environment.CurrentManagedThreadId != WorkQueue.GetMainThreadId())
        {
            if (!_threadErrorDisplayed)
            {
                Console.Error.WriteLine("Thread::mainThreadID is not setup correctly! Threaded log handling disabled");
                _threadErrorDisplayed = true;
            }
            return;
        }

        lock (_logMutex)
        {
            // Process messages accumulated from other threads (if any)
            while (_threadMessages.First is not null)
            {
                var stored = _threadMessages.First.Value;
                SendMessageEvent(stored.Level, stored.Timestamp, stored.Logger, stored.Message);
                _threadMessages.RemoveFirst();
            }
        }
    }

    public override void Dispose()
    {
        _impl.Close();
        _threadMessages.Clear();
        _impl.Dispose();
        base.Dispose();
    }

    private Logger GetOrCreateLogger(string name)
    {
        lock (_logMutex)
            return new Logger(this, name);
    }
}
