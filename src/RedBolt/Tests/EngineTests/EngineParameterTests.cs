using System.Reflection;
using RedBolt;

namespace RedBolt.Tests;

public sealed class EngineParameterTests
{
    [Fact]
    public void Engine_parameter_command_line_priorities_match_cpp_defaults()
    {
        using var engine = new Engine();
        var configFile = (ConfigFile)typeof(Engine)
            .GetField("_configFile", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(engine)!;

        Assert.False(configFile.Definitions[EngineParameter.ConfigName].HasCommandLinePriority);
        Assert.True(configFile.Definitions[EngineParameter.LogRotateOnOpen].HasCommandLinePriority);
        Assert.True(configFile.Definitions[EngineParameter.LogRotateBySize].HasCommandLinePriority);
        Assert.True(configFile.Definitions[EngineParameter.LogMaxFiles].HasCommandLinePriority);
        Assert.True(configFile.Definitions[EngineParameter.LogMaxSize].HasCommandLinePriority);
    }
}
