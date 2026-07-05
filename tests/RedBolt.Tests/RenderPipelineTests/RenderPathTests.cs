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

    [Fact]
    public void Render_path_json_renderpath_loads_through_resource_cache()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "RedBolt.RenderPathTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(tempRoot, "RenderPaths"));
        var resources = new ResourceCache();
        resources.RegisterDefaultResources();
        resources.AddResourceDir(tempRoot);

        try
        {
            File.WriteAllText(Path.Combine(tempRoot, "RenderPaths", "Simple.renderpath"), """
                {
                  "passes": [
                    {
                      "type": "FullScreenShaderPass",
                      "value": {
                        "attributes": [
                          { "name": "Pass Name", "type": "String", "value": "Postprocess: Adjust Color" },
                          { "name": "Comment", "type": "String", "value": "Adjust output color RGB and HSV." },
                          { "name": "Read+Write Color Output", "type": "Bool", "value": true },
                          { "name": "Bilinear Color Sampler", "type": "Bool", "value": true },
                          { "name": "Parameters Prefix", "type": "String", "value": "Adjust Color: " },
                          {
                            "name": "Parameters",
                            "type": "StringVariantMap",
                            "value": [
                              { "key": "HSVParams", "type": "Vector4", "value": "1 1 1 1" }
                            ]
                          }
                        ]
                      }
                    }
                  ]
                }
                """);

            var renderPath = resources.GetResource<RenderPath>("RenderPaths/Simple");

            var collectedParameters = new Dictionary<string, object?>();
            renderPath.CollectParameters(collectedParameters);
            renderPath.UpdateParameters(new RenderPipelineSettings(), renderPath.RepairEnabledRenderPasses([]), collectedParameters);

            var pass = Assert.IsType<FullScreenShaderPass>(Assert.Single(renderPath.GetPasses()));
            Assert.Equal("Postprocess: Adjust Color", pass.GetPassName());
            Assert.Equal("Adjust output color RGB and HSV.", pass.GetComment());
            Assert.Equal(new Vector4(1.0f, 1.0f, 1.0f, 1.0f), Assert.IsType<Vector4>(collectedParameters["Adjust Color: HSVParams"]));
            Assert.True(renderPath.GetAggregatedPassTraits().NeedReadWriteColorBuffer);
            Assert.True(renderPath.GetAggregatedPassTraits().NeedBilinearColorSampler);
        }
        finally
        {
            resources.RemoveResourceDir(tempRoot);
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Render_path_loads_real_rbfx_default_renderpath_types()
    {
        var resources = new ResourceCache();
        resources.RegisterDefaultResources();
        var upstreamCoreData = GetUpstreamRbfxCoreData();
        resources.AddResourceDir(upstreamCoreData);

        try
        {
            var renderPath = resources.GetResource<RenderPath>(RenderPipeline.DefaultRenderPathName);

            Assert.NotEmpty(renderPath.GetPasses());
            Assert.Collection(
                renderPath.GetPasses().Take(5),
                pass => Assert.IsType<AutoExposurePass>(pass),
                pass => Assert.IsType<AmbientOcclusionPass>(pass),
                pass => Assert.IsType<BloomPass>(pass),
                pass => Assert.IsType<OutlineRenderPass>(pass),
                pass => Assert.IsType<ToneMappingPass>(pass));
            Assert.Contains(renderPath.GetPasses(), static pass => pass is FullScreenShaderPass);

            var collectedParameters = new Dictionary<string, object?>();
            renderPath.CollectParameters(collectedParameters);
            renderPath.UpdateParameters(new RenderPipelineSettings(), renderPath.RepairEnabledRenderPasses([]), collectedParameters);

            Assert.Contains("Exposure: Automatic", collectedParameters.Keys);
            Assert.Contains("Bloom: Num Iterations", collectedParameters.Keys);
            Assert.Contains("Tone Mapping: Mode", collectedParameters.Keys);
            Assert.Equal(new Vector4(1.0f, 1.0f, 1.0f, 1.0f), Assert.IsType<Vector4>(collectedParameters["Adjust Color: HSVParams"]));
            Assert.True(renderPath.GetAggregatedPassTraits().NeedReadWriteColorBuffer);
            Assert.True(renderPath.GetAggregatedPassTraits().NeedBilinearColorSampler);
        }
        finally
        {
            resources.RemoveResourceDir(upstreamCoreData);
        }
    }

    private static string GetUpstreamRbfxCoreData()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "RENDER_PORT_PROGRESS.md")))
            directory = directory.Parent;

        Assert.NotNull(directory);

        var coreData = Path.GetFullPath(Path.Combine(directory!.FullName, "..", "rbfx", "bin", "CoreData"));
        Assert.True(Directory.Exists(coreData), $"Expected upstream rbfx CoreData directory at '{coreData}'.");
        return coreData;
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
