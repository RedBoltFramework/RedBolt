using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class EngineTests
{
    private readonly HeadlessEngineFixture _fixture;

    public EngineTests(HeadlessEngineFixture fixture)
    {
        this._fixture = fixture;
    }

    [Fact]
    public void Fps_helpers_roundtrip()
    {
        _fixture.Engine.SetMinFps(0);
        _fixture.Engine.SetMaxFps(144);
        _fixture.Engine.SetMaxInactiveFps(30);
        _fixture.Engine.SetTimeStepSmoothing(0);
        _fixture.Engine.SetPauseMinimized(false);
        _fixture.Engine.SetAutoExit(false);

        Assert.Equal(0, _fixture.Engine.GetMinFps());
        Assert.Equal(144, _fixture.Engine.GetMaxFps());
        Assert.Equal(30, _fixture.Engine.GetMaxInactiveFps());
        Assert.Equal(1, _fixture.Engine.GetTimeStepSmoothing());
        Assert.False(_fixture.Engine.GetPauseMinimized());
        Assert.False(_fixture.Engine.GetAutoExit());
    }

    [Fact]
    public void Parameter_helpers_roundtrip()
    {
        _fixture.Engine.SetParameter("CustomParameter", 42);
        Assert.True(_fixture.Engine.HasParameter("CustomParameter"));
        Assert.Equal(42, _fixture.Engine.GetParameter("CustomParameter"));
    }

    [Fact]
    public void Exit_performs_exit_housekeeping()
    {
        using var engine = new EngineScope(new Engine(), new EngineParameters
        {
            ApplicationName = "RedBolt.Tests.Exit",
            ConfigName = string.Empty,
            Headless = true
        });

        engine.Value.Exit();

        Assert.True(engine.Value.IsExiting);
        Assert.False(engine.Value.Context.GetSubsystem<Graphics>()!.IsInitialized);
    }

    [Fact]
    public void Time_step_helpers_work()
    {
        _fixture.Engine.SetNextTimeStep(0.125f);
        var receiver = new TestObject();
        var observed = -1.0f;
        var beginFrameCalls = 0;
        var endFrameCalls = 0;
        var endFramePrivateCalls = 0;
        var before = _fixture.Engine.GetNextTimeStep();

        receiver.SubscribeToEvent<CoreEvents.BeginFrameEvent>((ref CoreEvents.BeginFrameEvent eventData) =>
        {
            ++beginFrameCalls;
            Assert.Equal(1u, eventData.FrameNumber);
            Assert.True(MathDefs.ApproximatelyEquivalent(eventData.TimeStep, 0.125f));
        });
        receiver.SubscribeToEvent<CoreEvents.EndFrameEvent>((ref CoreEvents.EndFrameEvent _) => ++endFrameCalls);
        receiver.SubscribeToEvent<CoreEvents.EndFramePrivateEvent>((ref CoreEvents.EndFramePrivateEvent _) => ++endFramePrivateCalls);

        _fixture.Engine.RunFrame(timeStep => observed = timeStep);

        Assert.True(MathDefs.ApproximatelyEquivalent(observed, 0.125f));
        Assert.True(MathDefs.ApproximatelyEquivalent(before, 0.125f));
        Assert.True(_fixture.Engine.GetNextTimeStep() >= 0.0f);

        _fixture.Time.SetTimerPeriod(1u);
        Assert.Equal(1u, _fixture.Time.GetFrameNumber());
        Assert.Equal(1u, _fixture.Time.GetTimerPeriod());

        // Frame timing from the same RunFrame call.
        Assert.True(MathDefs.ApproximatelyEquivalent(_fixture.Time.GetTimeStep(), 0.125f));
        Assert.True(_fixture.Time.GetElapsedTime() >= 0.0f);
        Assert.True(MathDefs.ApproximatelyEquivalent(_fixture.Time.GetFramesPerSecond(), 8.0f));
        Assert.Equal(1, beginFrameCalls);
        Assert.Equal(1, endFrameCalls);
        Assert.Equal(1, endFramePrivateCalls);
    }

    [Fact]
    public void Engine_initialization_applies_configured_resource_paths()
    {
        var resourceRoot = Path.Combine(Path.GetTempPath(), $"redbolt-engine-paths-{Guid.NewGuid():N}");
        var coreData = Path.Combine(resourceRoot, "CoreData");
        var data = Path.Combine(resourceRoot, "Data");
        Directory.CreateDirectory(coreData);
        Directory.CreateDirectory(data);

        try
        {
            using var engine = new EngineScope(new Engine(), new EngineParameters
            {
                ApplicationName = "RedBolt.Tests.ResourcePaths",
                Headless = true,
                ResourcePrefixPaths = resourceRoot,
                ResourcePaths = "CoreData;Data"
            });

            var resources = engine.Value.Context.GetSubsystem<ResourceCache>();
            Assert.NotNull(resources);
            Assert.Contains(coreData, resources!.GetResourceDirs());
            Assert.Contains(data, resources.GetResourceDirs());

            var virtualFileSystem = engine.Value.Context.GetSubsystem<VirtualFileSystem>();
            Assert.NotNull(virtualFileSystem);
            var hasAliasRoot = false;
            var hasRoot = false;
            for (var index = 0u; index < virtualFileSystem!.NumMountPoints(); ++index)
            {
                hasAliasRoot |= virtualFileSystem.GetMountPoint(index) is MountedAliasRoot;
                hasRoot |= virtualFileSystem.GetMountPoint(index) is MountedRoot;
            }
            Assert.True(hasAliasRoot);
            Assert.True(hasRoot);
        }
        finally
        {
            Directory.Delete(resourceRoot, recursive: true);
        }
    }

    [Fact]
    public void Engine_maps_log_file_identifiers_to_system_names()
    {
        Assert.EndsWith("logs/engine.log", _fixture.Engine.GetLogFileName("file://logs/engine.log"), StringComparison.Ordinal);
        Assert.EndsWith("engine.log", _fixture.Engine.GetLogFileName("conf://engine.log"), StringComparison.Ordinal);
        Assert.Equal(string.Empty, _fixture.Engine.GetLogFileName("pkg://engine.log"));
    }

    private sealed class EngineScope : IDisposable
    {
        public EngineScope(Engine engine, EngineParameters parameters)
        {
            Value = engine;
            Value.Initialize(parameters);
        }

        public Engine Value { get; }

        public void Dispose()
        {
            Value.Dispose();
        }
    }

    private sealed class TestObject : Object
    {
    }
}
