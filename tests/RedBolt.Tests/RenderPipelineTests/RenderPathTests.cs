using RedBolt;

namespace RedBolt.Tests;

public sealed class RenderPathTests
{
    [Fact]
    public void Render_path_repairs_enabled_passes_by_name()
    {
        var renderPath = new RenderPath("RenderPaths/Test.xml");
        var alpha = new TrackingRenderPass("Alpha") { EnabledByDefaultValue = false };
        var beta = new TrackingRenderPass("Beta");
        renderPath.AddPass(alpha);
        renderPath.AddPass(beta);

        var repaired = renderPath.RepairEnabledRenderPasses(
        [
            new RenderPassToggle("Beta", false),
            new RenderPassToggle("Alpha", true)
        ]);

        Assert.Collection(
            repaired,
            pass => Assert.Equal(new RenderPassToggle("Alpha", true), pass),
            pass => Assert.Equal(new RenderPassToggle("Beta", false), pass));
    }

    [Fact]
    public void Render_path_updates_and_renders_only_enabled_passes()
    {
        var renderPath = new RenderPath("RenderPaths/Test.xml");
        var first = new TrackingRenderPass("First");
        var second = new TrackingRenderPass("Second") { InternallyEnabledValue = false };
        renderPath.AddPass(first);
        renderPath.AddPass(second);

        var settings = new RenderPipelineSettings();
        var parameters = new Dictionary<string, object?>();
        renderPath.UpdateParameters(
        settings,
        [
            new RenderPassToggle("First", true),
            new RenderPassToggle("Second", true)
        ],
        parameters);

        var sharedState = new SharedRenderPassState();
        renderPath.Update(sharedState);
        renderPath.Render(sharedState);

        Assert.Equal(1, first.UpdateCalls);
        Assert.Equal(1, first.RenderCalls);
        Assert.Equal(0, second.UpdateCalls);
        Assert.Equal(0, second.RenderCalls);
        Assert.True(renderPath.GetAggregatedPassTraits().NeedReadWriteColorBuffer);
        Assert.True(renderPath.GetAggregatedPassTraits().NeedBilinearColorSampler);
    }

    [Fact]
    public void Render_path_xml_loads_through_resource_cache()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "RedBolt.RenderPathTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(tempRoot, "RenderPaths"));
        var resources = new ResourceCache();
        resources.RegisterDefaultResources();
        resources.AddResourceDir(tempRoot);

        try
        {
            File.WriteAllText(Path.Combine(tempRoot, "RenderPaths", "Simple.xml"), """
                <renderpath>
                    <pass name="Opaque" enabled="true" comment="Geometry pass">
                        <parameter name="Exposure" type="Float" value="1.25" />
                    </pass>
                    <pass name="PostProcess" enabled="false">
                        <parameter name="Enabled" type="Bool" value="true" />
                    </pass>
                </renderpath>
                """);

            var renderPath = resources.GetResource<RenderPath>("RenderPaths/Simple.xml");

            Assert.Equal(2, renderPath.GetPasses().Count);

            var collectedParameters = new Dictionary<string, object?>();
            renderPath.CollectParameters(collectedParameters);

            Assert.Equal(1.25f, Assert.IsType<float>(collectedParameters["Exposure"]));
            Assert.True(Assert.IsType<bool>(collectedParameters["Enabled"]));
        }
        finally
        {
            resources.RemoveResourceDir(tempRoot);
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    private sealed class TrackingRenderPass : RenderPass
    {
        public TrackingRenderPass(string passName)
            : base(passName)
        {
        }

        public bool EnabledByDefaultValue
        {
            set => SetEnabledByDefault(value);
        }

        public bool InternallyEnabledValue
        {
            set => SetInternallyEnabled(value);
        }

        public int UpdateCalls { get; private set; }

        public int RenderCalls { get; private set; }

        public override void UpdateParameters(RenderPipelineSettings settings, IReadOnlyDictionary<string, object?> parameters)
        {
            SetTraits(new RenderPassTraits(NeedReadWriteColorBuffer: true, NeedBilinearColorSampler: true));
        }

        public override void Update(SharedRenderPassState sharedState)
        {
            ++UpdateCalls;
        }

        public override void Render(SharedRenderPassState sharedState)
        {
            ++RenderCalls;
        }

        public override RenderPass Clone()
        {
            var clone = new TrackingRenderPass(GetPassName())
            {
                EnabledByDefaultValue = IsEnabledByDefault(),
                InternallyEnabledValue = true
            };
            clone.SetComment(GetComment());
            return clone;
        }
    }
}
