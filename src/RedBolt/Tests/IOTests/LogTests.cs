using RedBolt;
using System.Reflection;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class LogTests
{
    private static readonly MethodInfo SetMainThreadMethod = typeof(WorkQueue).GetMethod(
        "SetMainThread", BindingFlags.NonPublic | BindingFlags.Static)!;

    private sealed class EventReceiver : RedObject
    {
    }

    private readonly HeadlessEngineFixture _fixture;

    public LogTests(HeadlessEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Types_and_copies_match_cpp_shape()
    {
        Assert.True(typeof(LogLevel).IsEnum);
        Assert.Equal(typeof(int), Enum.GetUnderlyingType(typeof(LogLevel)));
        Assert.True(typeof(StoredLogMessage).IsValueType);
        Assert.True(typeof(LogFileParams).IsValueType);
        Assert.False(typeof(Logger).IsValueType);
        Assert.False(typeof(Logger).IsSealed);
        Assert.False(typeof(Log).IsValueType);
        Assert.False(typeof(Log).IsSealed);

        var stored = new StoredLogMessage(LogLevel.Info, 1, "logger", "message");
        var storedCopy = stored;
        storedCopy.Level = LogLevel.Error;
        storedCopy.Logger = "copy";
        storedCopy.Message = "copy";

        Assert.Equal(LogLevel.Info, stored.Level);
        Assert.Equal("logger", stored.Logger);
        Assert.Equal("message", stored.Message);

        var parameters = new LogFileParams();
        Assert.True(parameters.RotateOnOpen);
        Assert.False(parameters.RotateBySize);
        Assert.Equal(20u, parameters.MaxFiles);
        Assert.Equal(20u * 1024u * 1024u, parameters.MaxSize);

        var parametersCopy = parameters;
        parametersCopy.MaxFiles = 1;
        Assert.Equal(20u, parameters.MaxFiles);

        var logger = Log.GetLogger("copy-test");
        var loggerCopy = Assert.IsType<Logger>(logger.Clone());
        Assert.NotSame(logger, loggerCopy);
    }

    [Fact]
    public void Main_thread_log_sends_log_message_event()
    {
        SetMainThreadMethod.Invoke(null, null);
        var log = _fixture.Engine.GetSubsystem<Log>()!;
        using var receiver = new EventReceiver();
        var message = $"main-log-event-{Guid.NewGuid():N}";
        var calls = 0;
        RedObject? sender = null;
        IOEvents.LogMessage observed = default;
        receiver.SubscribeToEvent<IOEvents.LogMessage>(log, (ref IOEvents.LogMessage eventData) =>
        {
            if (eventData.Message != message)
                return;
            ++calls;
            sender = receiver.GetEventSender();
            observed = eventData;
        });

        Log.GetLogger("main-event-test").Info(message);

        Assert.Equal(1, calls);
        Assert.Same(log, sender);
        Assert.Equal(message, observed.Message);
        Assert.Equal("main-event-test", observed.Logger);
        Assert.Equal((int)LogLevel.Info, observed.Level);
        Assert.NotEqual(0u, observed.Time);
    }

    [Fact]
    public void Worker_thread_log_sends_log_message_event_on_engine_end_frame()
    {
        SetMainThreadMethod.Invoke(null, null);
        var log = _fixture.Engine.GetSubsystem<Log>()!;
        using var receiver = new EventReceiver();
        var message = $"worker-log-event-{Guid.NewGuid():N}";
        var calls = 0;
        RedObject? sender = null;
        IOEvents.LogMessage observed = default;
        receiver.SubscribeToEvent<IOEvents.LogMessage>(log, (ref IOEvents.LogMessage eventData) =>
        {
            if (eventData.Message != message)
                return;
            ++calls;
            sender = receiver.GetEventSender();
            observed = eventData;
        });

        var thread = new Thread(() => Log.GetLogger("worker-event-test").Warning(message));
        thread.Start();
        thread.Join();

        Assert.Equal(0, calls);
        _fixture.Time.EndFrame();

        Assert.Equal(1, calls);
        Assert.Same(log, sender);
        Assert.Equal(message, observed.Message);
        Assert.Equal("worker-event-test", observed.Logger);
        Assert.Equal((int)LogLevel.Warning, observed.Level);
        Assert.NotEqual(0u, observed.Time);
    }
}
