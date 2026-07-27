using RedBolt;

using RedBolt.Core;
using RedBolt.Engine;
using RedBolt.IO;
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
    public void Engine_mounts_preferences_directory_before_loading_overrides()
    {
        var preferencesDirectory = Path.Combine(Path.GetTempPath(), $"redbolt-engine-config-{Guid.NewGuid():N}");
        Directory.CreateDirectory(preferencesDirectory);
        System.IO.File.WriteAllText(Path.Combine(preferencesDirectory, "EngineConfig.json"), """
            {
                "FullScreen": { "type": "Bool", "value": false }
            }
            """);

        try
        {
            using var engine = new EngineSystem();
            Assert.True(engine.Initialize(new EngineParameters
            {
                ApplicationName = "RedBolt.Tests.Config",
                ApplicationPreferencesDir = preferencesDirectory + Path.DirectorySeparatorChar,
                ConfigName = "EngineConfig.json",
                FullScreen = true,
                Headless = true,
                LogName = "none://"
            }));

            Assert.False(engine.Parameters.FullScreen);
            Assert.Equal(preferencesDirectory + Path.DirectorySeparatorChar, engine.GetAppPreferencesDir());
        }
        finally
        {
            if (Directory.Exists(preferencesDirectory))
                Directory.Delete(preferencesDirectory, recursive: true);
        }
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
            using var configFile = new ConfigFile();
            configFile.DefineVariable("FullScreen", new Variant(true)).Overridable();

            Assert.True(configFile.LoadDefaults("memory://ConfigDefaults.json", ApplicationFlavor.Universal));
            Assert.Equal(new Variant(true), configFile.GetVariable("FullScreen"));
            Assert.Equal(new Variant("SampleProject;TestPlugin"), configFile.GetVariable("Plugins"));
            Assert.Equal(new Variant("SampleProject"), configFile.GetVariable("MainPlugin"));

            Assert.True(configFile.LoadOverrides("memory://ConfigOverrides.json"));
            Assert.Equal(new Variant(false), configFile.GetVariable("FullScreen"));
            using var changedVariables = configFile.GetChangedVariables(ApplicationFlavor.Universal);
            Assert.Single(changedVariables);
        }
        finally
        {
            fileSystem.Unmount(mountPoint);
            mountPoint.Dispose();
        }
    }

    [Fact]
    public void Config_file_copies_owned_variant_values()
    {
        using var configFile = new ConfigFile();
        using var defaultItems = new VariantVector { new Variant(new byte[] { 1, 2, 3 }) };
        using var defaultValue = new Variant(defaultItems);
        configFile.DefineVariable("Nested", defaultValue).Overridable();

        defaultValue.GetVariantVector()[0].GetBuffer()[0] = 9;
        ref readonly var storedDefault = ref configFile.GetVariable("Nested");
        Assert.Equal(1, storedDefault.GetVariantVector()[0].GetBuffer()[0]);

        using var overrideItems = new VariantVector { new Variant(new byte[] { 4, 5, 6 }) };
        using var overrideValue = new Variant(overrideItems);
        configFile.SetVariable("Nested", overrideValue);
        overrideValue.GetVariantVector()[0].GetBuffer()[0] = 8;

        using var changedVariables = configFile.GetChangedVariables(ApplicationFlavor.Universal);
        Assert.Equal(4, changedVariables["Nested"].GetVariantVector()[0].GetBuffer()[0]);
        changedVariables["Nested"].GetVariantVector()[0].GetBuffer()[0] = 7;

        ref readonly var storedOverride = ref configFile.GetVariable("Nested");
        Assert.Equal(4, storedOverride.GetVariantVector()[0].GetBuffer()[0]);
    }

    [Fact]
    public void Engine_config_lifecycle_applies_defaults_and_overrides()
    {
        var fileSystem = _fixture.Context.GetSubsystem<VirtualFileSystem>()!;
        var defaultsMount = new MountedExternalMemory("");
        var overridesMount = new MountedExternalMemory("conf");
        fileSystem.Mount(defaultsMount);
        fileSystem.Mount(overridesMount);
        defaultsMount.LinkMemory("EngineConfig.json", """
            {
                "Default": [
                    {
                        "Flavor": [],
                        "Variables": [
                            { "key": "FullScreen", "type": "Bool", "value": true }
                        ]
                    }
                ]
            }
            """);
        overridesMount.LinkMemory("EngineConfig.json", """
            {
                "FullScreen": { "type": "Bool", "value": false }
            }
            """);

        try
        {
            _fixture.Engine.SetParameter(EngineParameter.ConfigName, "EngineConfig.json");
            _fixture.Engine.LoadConfigFiles();

            Assert.False(_fixture.Engine.Parameters.FullScreen);
        }
        finally
        {
            fileSystem.Unmount(overridesMount);
            fileSystem.Unmount(defaultsMount);
            overridesMount.Dispose();
            defaultsMount.Dispose();
        }
    }
}
