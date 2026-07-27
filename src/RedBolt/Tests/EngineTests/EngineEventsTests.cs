using RedBolt;
using RedBolt.Engine;

namespace RedBolt.Tests;

public sealed class EngineEventsTests
{
    [Fact]
    public void Engine_event_payloads_default_to_empty_strings()
    {
        var command = new EngineEvents.ConsoleCommandEvent();
        var uriClick = new EngineEvents.ConsoleUriClickEvent();

        Assert.Equal(string.Empty, command.Command);
        Assert.Equal(string.Empty, command.Id);
        Assert.Equal(string.Empty, uriClick.Address);
        Assert.Equal(string.Empty, uriClick.Protocol);
    }
}
