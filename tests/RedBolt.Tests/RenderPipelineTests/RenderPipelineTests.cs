using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class RenderPipelineTests
{
    private readonly HeadlessEngineFixture fixture;

    public RenderPipelineTests(HeadlessEngineFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public void Render_pipeline_collects_render_path_parameters()
    {
        var scene = new Scene();
        var renderPipeline = scene.CreateComponent<RenderPipeline>();
        var renderPath = new RenderPath("RenderPaths/Test.xml");
        renderPath.AddPass(new ParameterRenderPass("Exposure", 1.25f));

        renderPipeline.SetRenderPath(renderPath);

        Assert.Equal(1.25f, Assert.IsType<float>(renderPipeline.GetRenderPathParameters()["Exposure"]));
    }

    [Fact]
    public void Render_pipeline_view_updates_and_renders_enabled_passes()
    {
        var scene = new Scene();
        var renderPipeline = scene.CreateComponent<RenderPipeline>();
        var renderPath = new RenderPath("RenderPaths/Test.xml");
        var trackingPass = new TrackingRenderPass("Tracking");
        renderPath.AddPass(trackingPass);
        renderPipeline.SetRenderPath(renderPath);

        var camera = scene.CreateChild("Camera").CreateComponent<Camera>();
        var viewport = new Viewport(scene, camera, new IntRect(0, 0, 320, 200));
        var view = renderPipeline.Instantiate();

        Assert.True(view.Define(viewport, fixture.Graphics));

        view.Update(fixture.Graphics);
        view.Render(fixture.Graphics, MaterialQuality.High);

        Assert.Equal(1, trackingPass.UpdateCalls);
        Assert.Equal(1, trackingPass.RenderCalls);
        Assert.Equal(new IntVector2(320, 200), view.GetFrameInfo().ViewportSize);
        Assert.Equal(0u, view.GetStats().NumGeometries);
    }

    [Fact]
    public void Render_buffer_manager_supports_secondary_color_buffer_when_requested()
    {
        var manager = new RenderBufferManager();
        manager.SetSettings(new RenderBufferManagerSettings());
        manager.SetFrameSettings(new RenderBufferManagerFrameSettings
        {
            SupportColorReadWrite = true
        });
        manager.OnViewportDefined(null, new IntRect(0, 0, 640, 360));

        var firstColor = manager.GetColorOutput();
        Assert.NotNull(manager.GetSecondaryColorTexture());

        manager.SwapColorBuffers(synchronizeContents: false);

        Assert.NotSame(firstColor, manager.GetColorOutput());
        Assert.Equal(new IntVector2(640, 360), manager.GetOutputSize());
    }

    [Fact]
    public void Render_buffer_manager_uses_offscreen_primary_buffer_for_color_read_write_frames()
    {
        var manager = new RenderBufferManager();
        manager.SetSettings(new RenderBufferManagerSettings
        {
            ColorSpace = RenderPipelineColorSpace.LinearHDR
        });
        manager.SetFrameSettings(new RenderBufferManagerFrameSettings
        {
            SupportColorReadWrite = true
        });
        manager.OnViewportDefined(null, new IntRect(0, 0, 640, 360));

        Assert.NotNull(manager.GetColorOutput().GetTexture());
        Assert.NotNull(manager.GetDepthStencilOutput().GetTexture());
        Assert.Equal(TextureFormat.RGBA16F, manager.GetColorOutput().GetTexture()!.Format);
        Assert.Equal(TextureFormat.Depth24Stencil8, manager.GetDepthStencilOutput().GetTexture()!.Format);
        Assert.NotNull(manager.GetSecondaryColorTexture());
        Assert.True(manager.NeedsViewportResolve());
    }

    [Fact]
    public void Render_buffer_manager_allocates_readable_depth_buffer_when_requested()
    {
        var manager = new RenderBufferManager();
        manager.SetSettings(new RenderBufferManagerSettings
        {
            ReadableDepth = true
        });
        manager.SetFrameSettings(new RenderBufferManagerFrameSettings());
        manager.OnViewportDefined(null, new IntRect(0, 0, 640, 360));

        Assert.Null(manager.GetColorOutput().GetTexture());
        Assert.NotNull(manager.GetDepthStencilOutput().GetTexture());
        Assert.Equal(TextureFormat.Depth24Stencil8, manager.GetDepthStencilOutput().GetTexture()!.Format);
        Assert.Same(manager.GetDepthStencilOutput().GetTexture(), manager.GetDepthStencilTexture());
        Assert.False(manager.NeedsViewportResolve());
    }

    [Fact]
    public void Render_buffer_manager_shared_state_uses_readable_depth_buffer_when_requested()
    {
        var manager = new RenderBufferManager();
        manager.SetSettings(new RenderBufferManagerSettings
        {
            ReadableDepth = true
        });
        manager.SetFrameSettings(new RenderBufferManagerFrameSettings());
        manager.OnViewportDefined(null, new IntRect(0, 0, 320, 200));

        var sharedState = new SharedRenderPassState();
        manager.PopulateSharedState(sharedState);

        Assert.True(sharedState.RenderBuffers.TryGetValue("Viewport.Depth", out var depthBuffer));
        Assert.NotNull(depthBuffer.GetTexture());
        Assert.Same(manager.GetDepthStencilTexture(), depthBuffer.GetTexture());
    }

    [Fact]
    public void Render_pipeline_uses_default_renderpath_on_attach()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "RedBolt.RenderPipelineTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(tempRoot, "RenderPaths"));
        fixture.Resources.AddResourceDir(tempRoot);

        try
        {
            File.WriteAllText(Path.Combine(tempRoot, "RenderPaths", "Default.renderpath"), """
                {
                  "passes": [
                    {
                      "type": "FullScreenShaderPass",
                      "value": {
                        "attributes": [
                          { "name": "Pass Name", "type": "String", "value": "Postprocess: Default" },
                          {
                            "name": "Parameters",
                            "type": "StringVariantMap",
                            "value": [
                              { "key": "Exposure", "type": "Float", "value": 1.25 }
                            ]
                          }
                        ]
                      }
                    }
                  ]
                }
                """);

            fixture.Resources.ReleaseResource<RenderPath>(RenderPipeline.DefaultRenderPathName);

            var scene = new Scene();
            var renderPipeline = scene.CreateComponent<RenderPipeline>();

            Assert.NotNull(renderPipeline.GetRenderPath());
            Assert.Equal(RenderPipeline.DefaultRenderPathName, renderPipeline.GetRenderPath()!.Name);
            Assert.Equal(1.25f, Assert.IsType<float>(renderPipeline.GetRenderPathParameters()["Exposure"]));
        }
        finally
        {
            fixture.Resources.ReleaseResource<RenderPath>(RenderPipeline.DefaultRenderPathName);
            fixture.Resources.RemoveResourceDir(tempRoot);
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    private sealed class ParameterRenderPass : RenderPass
    {
        private readonly string parameterName;
        private readonly object? value;

        public ParameterRenderPass(string parameterName, object? value)
            : base(parameterName)
        {
            this.parameterName = parameterName;
            this.value = value;
        }

        public override void CollectParameters(IDictionary<string, object?> parameters)
        {
            DeclareParameter(parameterName, value, parameters);
        }

        public override RenderPass Clone() => new ParameterRenderPass(parameterName, value);
    }

    private sealed class TrackingRenderPass : RenderPass
    {
        private readonly Counter counter;

        public TrackingRenderPass(string passName)
            : this(passName, new Counter())
        {
        }

        private TrackingRenderPass(string passName, Counter counter)
            : base(passName)
        {
            this.counter = counter;
        }

        public int UpdateCalls => counter.UpdateCalls;

        public int RenderCalls => counter.RenderCalls;

        public override void Update(SharedRenderPassState sharedState)
        {
            ++counter.UpdateCalls;
        }

        public override void Render(SharedRenderPassState sharedState)
        {
            ++counter.RenderCalls;
        }

        public override RenderPass Clone() => new TrackingRenderPass(GetPassName(), counter);

        private sealed class Counter
        {
            public int UpdateCalls;
            public int RenderCalls;
        }
    }
}
