using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class ConfigFileTests
{
    private readonly HeadlessEngineFixture _fixture;

    public ConfigFileTests(HeadlessEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Config_file_loads_rbfx_defaults_and_overrides_through_vfs()
    {
        var fileSystem = _fixture.Context.GetSubsystem<VirtualFileSystem>()!;
        var mountPoint = new MountedExternalMemory("memory");
        fileSystem.Mount(mountPoint);
        mountPoint.LinkMemory("ConfigDefaults.json", """
            {
                "Default": [
                    {
                        "Flavor": [],
                        "Variables": [
                            { "key": "FullScreen", "type": "Bool", "value": true },
                            { "key": "Plugins", "type": "String", "value": "SampleProject;TestPlugin" },
                            { "key": "MainPlugin", "type": "String", "value": "SampleProject" }
                        ]
                    }
                ]
            }
            """);
        mountPoint.LinkMemory("ConfigOverrides.json", """
            {
                "FullScreen": { "type": "Bool", "value": false }
            }
            """);

        try
        {
            var configFile = new ConfigFile();
            configFile.DefineVariable("FullScreen", new Variant(true)).Overridable();

            Assert.True(configFile.LoadDefaults("memory://ConfigDefaults.json", ApplicationFlavor.Universal));
            Assert.Equal(new Variant(true), configFile.GetVariable("FullScreen"));
            Assert.Equal(new Variant("SampleProject;TestPlugin"), configFile.GetVariable("Plugins"));
            Assert.Equal(new Variant("SampleProject"), configFile.GetVariable("MainPlugin"));

            Assert.True(configFile.LoadOverrides("memory://ConfigOverrides.json"));
            Assert.Equal(new Variant(false), configFile.GetVariable("FullScreen"));
            Assert.Single(configFile.GetChangedVariables(ApplicationFlavor.Universal));
        }
        finally
        {
            fileSystem.Unmount(mountPoint);
            mountPoint.Dispose();
        }
    }
}
