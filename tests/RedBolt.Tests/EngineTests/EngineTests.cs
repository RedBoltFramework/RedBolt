using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class EngineTests
{
    private readonly HeadlessEngineFixture fixture;

    public EngineTests(HeadlessEngineFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public void Fps_helpers_roundtrip()
    {
        fixture.Engine.SetMinFps(0);
        fixture.Engine.SetMaxFps(144);
        fixture.Engine.SetMaxInactiveFps(30);
        fixture.Engine.SetTimeStepSmoothing(0);
        fixture.Engine.SetPauseMinimized(false);
        fixture.Engine.SetAutoExit(false);

        Assert.Equal(1, fixture.Engine.GetMinFps());
        Assert.Equal(144, fixture.Engine.GetMaxFps());
        Assert.Equal(30, fixture.Engine.GetMaxInactiveFps());
        Assert.Equal(1, fixture.Engine.GetTimeStepSmoothing());
        Assert.False(fixture.Engine.GetPauseMinimized());
        Assert.False(fixture.Engine.GetAutoExit());
    }

    [Fact]
    public void Parameter_helpers_roundtrip()
    {
        fixture.Engine.SetParameter("CustomParameter", 42);
        Assert.True(fixture.Engine.HasParameter("CustomParameter"));
        Assert.Equal(42, fixture.Engine.GetParameter("CustomParameter"));
    }

    [Fact]
    public void Time_step_helpers_work()
    {
        fixture.Engine.SetNextTimeStep(0.125f);
        var observed = -1.0f;
        fixture.Engine.RunFrame(timeStep => observed = timeStep);

        Assert.True(MathDefs.ApproximatelyEquivalent(observed, 0.125f));
        Assert.True(MathDefs.ApproximatelyEquivalent(fixture.Engine.GetNextTimeStep(), 0.125f));

        fixture.Engine.Time.SetTimerPeriod(1u);
        Assert.Equal(1u, fixture.Engine.Time.GetFrameNumber());
        Assert.Equal(1u, fixture.Engine.Time.GetTimerPeriod());

        // Frame timing from the same RunFrame call.
        Assert.True(MathDefs.ApproximatelyEquivalent(fixture.Engine.Time.GetTimeStep(), 0.125f));
        Assert.True(MathDefs.ApproximatelyEquivalent(fixture.Engine.Time.GetElapsedTime(), 0.125f));
        Assert.True(MathDefs.ApproximatelyEquivalent(fixture.Engine.Time.GetFramesPerSecond(), 8.0f));
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

            Assert.Contains(coreData, engine.Value.Resources.GetResourceDirs());
            Assert.Contains(data, engine.Value.Resources.GetResourceDirs());
        }
        finally
        {
            Directory.Delete(resourceRoot, recursive: true);
        }
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
            Value.Shutdown();
        }
    }
}
