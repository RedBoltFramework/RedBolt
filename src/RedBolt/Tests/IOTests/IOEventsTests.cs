using RedBolt;
using RedBolt.Core;
using RedBolt.IO;
using RedBolt.Logging;

namespace RedBolt.Tests;

public sealed class IOEventsTests
{
    [Fact]
    public void Log_message_preserves_cpp_event_fields()
    {
        Assert.True(typeof(IOEvents.LogMessage).IsValueType);

        var value = new IOEvents.LogMessage
        {
            Message = "message",
            Logger = "logger",
            Level = 2,
            Time = 3
        };

        Assert.Equal("message", value.Message);
        Assert.Equal("logger", value.Logger);
        Assert.Equal(2, value.Level);
        Assert.Equal(3u, value.Time);

        var copy = value;
        copy.Message = "copy";
        copy.Level = 4;

        Assert.Equal("message", value.Message);
        Assert.Equal(2, value.Level);
    }

    [Fact]
    public void Async_exec_finished_preserves_cpp_event_fields()
    {
        Assert.True(typeof(IOEvents.AsyncExecFinished).IsValueType);

        var value = new IOEvents.AsyncExecFinished
        {
            RequestId = 4,
            ExitCode = 5
        };

        Assert.Equal(4u, value.RequestId);
        Assert.Equal(5, value.ExitCode);

        var copy = value;
        copy.RequestId = 6;
        copy.ExitCode = 7;

        Assert.Equal(4u, value.RequestId);
        Assert.Equal(5, value.ExitCode);
    }
}
