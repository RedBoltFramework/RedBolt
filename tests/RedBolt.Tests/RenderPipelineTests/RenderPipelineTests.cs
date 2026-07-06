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
    public void Render_pipeline_raises_events_for_settings_parameters_and_renderpath_changes()
    {
        var scene = new Scene();
        var renderPipeline = scene.CreateComponent<RenderPipeline>();
        var renderPath = new RenderPath("RenderPaths/Test.xml");
        renderPath.AddPass(new ParameterRenderPass("Exposure", 1.25f));

        var settingsEvents = 0;
        var parameterEvents = 0;
        RenderPath? changedRenderPath = null;

        renderPipeline.SettingsChanged += (_, settings) =>
        {
            ++settingsEvents;
            Assert.Same(renderPipeline.Settings, settings);
        };
        renderPipeline.ParametersChanged += _ => ++parameterEvents;
        renderPipeline.RenderPathChanged += (_, newRenderPath) => changedRenderPath = newRenderPath;

        renderPipeline.SetSettings(new RenderPipelineSettings());
        renderPipeline.SetRenderPath(renderPath);
        renderPipeline.UpdateRenderPathParameter("Exposure", 2.0f);

        Assert.Equal(1, settingsEvents);
        Assert.Equal(1, parameterEvents);
        Assert.Same(renderPath, changedRenderPath);
    }

    [Fact]
    public void Render_pipeline_only_marks_parameters_dirty_on_actual_render_path_changes()
    {
        var scene = new Scene();
        var renderPipeline = scene.CreateComponent<RenderPipeline>();
        var renderPath = new RenderPath("RenderPaths/Test.xml");
        renderPath.AddPass(new ParameterRenderPass("Exposure", 1.25f));
        renderPipeline.SetRenderPath(renderPath);

        var initialRevision = renderPipeline.Revision;

        renderPipeline.UpdateRenderPathParameter("Missing", 2.0f);
        renderPipeline.UpdateRenderPathParameter("Exposure", 1.25f);

        Assert.Equal(initialRevision, renderPipeline.Revision);

        renderPipeline.UpdateRenderPathParameter("Exposure", 2.0f);

        Assert.Equal(initialRevision + 1, renderPipeline.Revision);
        Assert.Equal(2.0f, Assert.IsType<float>(renderPipeline.GetRenderPathParameters()["Exposure"]));
    }

    [Fact]
    public void Render_pipeline_frame_shader_parameters_do_not_bump_revision()
    {
        var scene = new Scene();
        var renderPipeline = scene.CreateComponent<RenderPipeline>();
        var initialRevision = renderPipeline.Revision;

        renderPipeline.SetFrameShaderParameter("Time", 1.0f);
        renderPipeline.SetFrameShaderParameters(
        [
            new KeyValuePair<string, object?>("FrameIndex", 4)
        ]);

        Assert.Equal(initialRevision, renderPipeline.Revision);
        Assert.Equal(4, Assert.IsType<int>(renderPipeline.GetFrameShaderParameters()["FrameIndex"]));
    }

    [Fact]
    public void Render_pipeline_set_render_pass_enabled_only_notifies_on_change()
    {
        var scene = new Scene();
        var renderPipeline = scene.CreateComponent<RenderPipeline>();
        var renderPath = new RenderPath("RenderPaths/Test.xml");
        renderPath.AddPass(new TrackingRenderPass("Tracking"));
        renderPipeline.SetRenderPath(renderPath);

        var parameterEvents = 0;
        renderPipeline.ParametersChanged += _ => ++parameterEvents;

        renderPipeline.SetRenderPassEnabled("Tracking", false);
        renderPipeline.SetRenderPassEnabled("Tracking", false);

        Assert.Equal(1, parameterEvents);
        Assert.Equal(new RenderPassToggle("Tracking", false), Assert.Single(renderPipeline.GetRenderPasses()));
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
    public void Render_buffer_manager_derives_output_traits_from_render_target_texture()
    {
        var manager = new RenderBufferManager();
        manager.SetSettings(new RenderBufferManagerSettings
        {
            ColorSpace = RenderPipelineColorSpace.GammaLDR
        });
        manager.SetFrameSettings(new RenderBufferManagerFrameSettings
        {
            SupportColorReadWrite = true
        });

        var renderTarget = new Texture2D("Textures/ViewportTarget");
        Assert.True(renderTarget.SetSize(320, 200, TextureFormat.RGBA16F));
        renderTarget.SetFilterMode(TextureFilterMode.Bilinear);
        renderTarget.SetLinear(true);

        manager.OnViewportDefined(renderTarget, new IntRect(0, 0, 320, 200));

        Assert.Equal(TextureFormat.RGBA16F, manager.GetOutputColorFormat());
        Assert.Equal(1, manager.GetOutputMultiSample());
        Assert.True(manager.IsLinearColorSpace());
        Assert.True(manager.IsHDR());
        var colorTexture = manager.GetColorOutput().GetTexture();
        var secondaryColorTexture = manager.GetSecondaryColorTexture();
        Assert.NotNull(colorTexture);
        Assert.NotNull(secondaryColorTexture);
        Assert.Equal(TextureFormat.RGBA16F, colorTexture!.Format);
        Assert.Equal(TextureFormat.RGBA16F, secondaryColorTexture!.Format);
    }

    [Fact]
    public void Render_pipeline_uses_default_renderpath_on_attach()
    {
        var previousContext = Context.TryGetInstance();
        var tempRoot = Path.Combine(Path.GetTempPath(), "RedBolt.RenderPipelineTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(tempRoot, "RenderPaths"));
        fixture.Resources.AddResourceDir(tempRoot);

        try
        {
            Context.SetInstance(fixture.Context);
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
            Context.SetInstance(previousContext);
            fixture.Resources.ReleaseResource<RenderPath>(RenderPipeline.DefaultRenderPathName);
            fixture.Resources.RemoveResourceDir(tempRoot);
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Render_pipeline_reacts_to_render_path_resource_reload()
    {
        const string renderPathName = "RenderPaths/Reload.renderpath";
        var tempRoot = Path.Combine(Path.GetTempPath(), "RedBolt.RenderPipelineTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(tempRoot, "RenderPaths"));
        fixture.Resources.AddResourceDir(tempRoot);

        try
        {
            var renderPathFile = Path.Combine(tempRoot, renderPathName);
            WriteRenderPath(renderPathFile, 1.25f, includeBloom: false);
            fixture.Resources.ReleaseResource<RenderPath>(renderPathName);

            var scene = new Scene();
            var renderPipeline = scene.CreateComponent<RenderPipeline>();
            renderPipeline.SetRenderPath(fixture.Resources.GetResource<RenderPath>(renderPathName));

            var renderPathChangedEvents = 0;
            renderPipeline.RenderPathChanged += (_, _) => ++renderPathChangedEvents;

            Assert.Equal(1.25f, Assert.IsType<float>(renderPipeline.GetRenderPathParameters()["Exposure"]));
            Assert.DoesNotContain("BloomStrength", renderPipeline.GetRenderPathParameters().Keys);

            WriteRenderPath(renderPathFile, 2.5f, includeBloom: true);
            Assert.True(fixture.Resources.ReloadResource<RenderPath>(renderPathName));

            Assert.Equal(1, renderPathChangedEvents);
            Assert.Equal(1.25f, Assert.IsType<float>(renderPipeline.GetRenderPathParameters()["Exposure"]));
            Assert.Equal(0.8f, Assert.IsType<float>(renderPipeline.GetRenderPathParameters()["BloomStrength"]));
        }
        finally
        {
            fixture.Resources.ReleaseResource<RenderPath>(renderPathName);
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

    private static void WriteRenderPath(string fileName, float exposure, bool includeBloom)
    {
        var bloomEntry = includeBloom
            ? """
                                      ,
                                      { "key": "BloomStrength", "type": "Float", "value": 0.8 }
                """
            : string.Empty;

        File.WriteAllText(fileName, $$"""
            {
              "passes": [
                {
                  "type": "FullScreenShaderPass",
                  "value": {
                    "attributes": [
                      { "name": "Pass Name", "type": "String", "value": "Postprocess: Reload" },
                      {
                        "name": "Parameters",
                        "type": "StringVariantMap",
                        "value": [
                          { "key": "Exposure", "type": "Float", "value": {{exposure}} }{{bloomEntry}}
                        ]
                      }
                    ]
                  }
                }
              ]
            }
            """);
    }
}
