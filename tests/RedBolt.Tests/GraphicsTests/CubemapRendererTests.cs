using System.Collections;
using System.Reflection;
using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class CubemapRendererTests
{
    private readonly HeadlessEngineFixture _fixture;

    public CubemapRendererTests(HeadlessEngineFixture fixture)
    {
        _fixture = fixture;
    }

    private static void ClearQueuedViewports()
    {
        var renderer = Context.Instance?.GetSubsystem<Renderer>();
        if (renderer is null)
            return;
        var field = typeof(Renderer).GetField("_queuedViewports", BindingFlags.Instance | BindingFlags.NonPublic);
        if (field?.GetValue(renderer) is IList list)
            list.Clear();
    }

    [Fact]
    public void Update_parameters_default_constructor_matches_cpp_member_initializers()
    {
        var parameters = new CubemapUpdateParameters();

        Assert.Equal(CubemapRenderingSettings.DefaultTextureSize, parameters.Settings.TextureSize);
        Assert.Equal(CubemapRenderingSettings.DefaultViewMask, parameters.Settings.ViewMask);
        Assert.Equal(CubemapRenderingSettings.DefaultNearClip, parameters.Settings.NearClip);
        Assert.Equal(CubemapRenderingSettings.DefaultFarClip, parameters.Settings.FarClip);
        Assert.Equal(Vector3f.Zero, parameters.Position);
        Assert.False(parameters.SlicedUpdate);
        Assert.False(parameters.FilterResult);
        Assert.Null(parameters.OverrideFinalTexture);
    }

    [Fact]
    public void Borrowed_dependencies_are_stored_as_weak_references()
    {
        var scene = new Scene();
        var renderer = new CubemapRenderer(scene);
        var texture = new TextureCube { Name = "CubemapRendererTests.Override" };
        var parameters = new CubemapUpdateParameters { OverrideFinalTexture = texture };

        var sceneReference = GetPrivateField<WeakReference<Scene>>(renderer, "_scene");
        var overrideReference = GetPrivateField<WeakReference<TextureCube>?>(parameters, "_overrideFinalTexture");
        var viewportReference = GetPrivateField<WeakReference<TextureCube>>(renderer, "_currentViewportTexture");
        var filteredReference = GetPrivateField<WeakReference<TextureCube>>(renderer, "_currentFilteredTexture");

        Assert.True(sceneReference.TryGetTarget(out var resolvedScene));
        Assert.Same(scene, resolvedScene);
        Assert.NotNull(overrideReference);
        Assert.True(overrideReference.TryGetTarget(out var resolvedTexture));
        Assert.Same(texture, resolvedTexture);
        Assert.False(viewportReference.TryGetTarget(out _));
        Assert.False(filteredReference.TryGetTarget(out _));

        renderer.Dispose();
    }

    [Fact]
    public void Parameter_copy_keeps_an_independent_weak_alias()
    {
        var texture = new TextureCube { Name = "CubemapRendererTests.Copy" };
        var source = new CubemapUpdateParameters { OverrideFinalTexture = texture };
        var copy = Assert.IsType<CubemapUpdateParameters>(source.Clone());
        var sourceReference = GetPrivateField<WeakReference<TextureCube>?>(source, "_overrideFinalTexture");
        var copyReference = GetPrivateField<WeakReference<TextureCube>?>(copy, "_overrideFinalTexture");

        source.OverrideFinalTexture = null;

        Assert.NotSame(sourceReference, copyReference);
        Assert.Null(source.OverrideFinalTexture);
        Assert.Same(texture, copy.OverrideFinalTexture);
        Assert.False(source.IsConsistentWith(copy));
    }

    [Fact]
    public void Renderer_is_not_copyable()
    {
        Assert.False(typeof(ICloneable).IsAssignableFrom(typeof(CubemapRenderer)));
    }

    [Fact]
    public void Dispose_releases_managed_scene_owned_camera_nodes()
    {
        var scene = new Scene();
        var initialChildren = scene.Root.GetNumChildren();
        var renderer = new CubemapRenderer(scene);

        Assert.Equal(initialChildren + (uint)CubeMapFace.MaxCubeMapFaces, scene.Root.GetNumChildren());

        renderer.Dispose();

        Assert.Equal(initialChildren, scene.Root.GetNumChildren());
    }

    [Fact]
    public void FilterCubemap_creates_uav_for_each_mip_when_compute_is_supported()
    {
        WithInitializedGraphics(() =>
        {
            var renderDevice = Context.Instance!.GetSubsystem<RenderDevice>()!;
            if (!renderDevice.GetCaps().ComputeShaders)
                return;

            var scene = new Scene();
            using var renderer = new CubemapRenderer(scene);
            using var sourceTexture = new TextureCube { Name = "CubemapFilterSource" };
            using var destinationTexture = new TextureCube { Name = "CubemapFilterDestination" };
            var settings = new CubemapRenderingSettings { TextureSize = 16 };
            CubemapRenderer.DefineTexture(sourceTexture, settings);
            CubemapRenderer.DefineTexture(destinationTexture, settings, TextureFlag.BindUnorderedAccess);

            var filterCubemap = typeof(CubemapRenderer).GetMethod(
                "FilterCubemap", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("CubemapRenderer.FilterCubemap was not found.");
            filterCubemap.Invoke(renderer, [sourceTexture, destinationTexture]);

            var destinationRaw = Assert.IsAssignableFrom<RawTexture>(destinationTexture.GetRawTexture());
            for (var level = 0; level < destinationTexture.GetLevels(); ++level)
            {
                var view = destinationRaw.GetUAV(new RawTextureUAVKey().FromLevel(checked((int)level)));
                Assert.NotNull(view);
                using var desc = view.GetDesc();
                Assert.Equal(checked((uint)level), desc.MostDetailedMip);
            }
        });
    }

    [Fact]
    public void DefineTexture_sets_size_format_and_render_target_flag()
    {
        var texture = new TextureCube { Name = "TestCubemap" };
        var settings = new CubemapRenderingSettings { TextureSize = 64 };

        CubemapRenderer.DefineTexture(texture, settings, TextureFlag.None);

        Assert.Equal(64, texture.Width);
        Assert.Equal(64, texture.Height);
        Assert.Equal(TextureFormat.TexFormatRgba8Unorm, texture.Format);
        Assert.True(texture.IsRenderTarget());
        Assert.Equal(TextureFilterMode.Trilinear, texture.GetFilterMode());
    }

    [Fact]
    public void Update_full_queues_all_faces_and_reports_complete()
    {
        ClearQueuedViewports();
        var scene = new Scene();
        scene.Root.GetOrCreateComponent<Octree>();
        using var renderer = new CubemapRenderer(scene);

        var result = renderer.Update(new CubemapUpdateParameters
        {
            Settings = new CubemapRenderingSettings { TextureSize = 32 },
            Position = Vector3f.Zero,
            SlicedUpdate = false,
            FilterResult = false
        });

        Assert.Equal((uint)CubeMapFace.MaxCubeMapFaces, result.NumRenderedFaces);
        Assert.True(result.IsComplete);
        ClearQueuedViewports();
    }

    [Fact]
    public void Update_sliced_renders_one_face_per_call_until_complete()
    {
        ClearQueuedViewports();
        var scene = new Scene();
        scene.Root.GetOrCreateComponent<Octree>();
        using var cubemapRenderer = new CubemapRenderer(scene);
        var parameters = new CubemapUpdateParameters
        {
            Settings = new CubemapRenderingSettings { TextureSize = 32 },
            Position = new Vector3f(1, 2, 3),
            SlicedUpdate = true,
            FilterResult = false
        };

        uint totalFaces = 0;
        CubemapUpdateResult result;
        var iterations = 0;
        do
        {
            result = cubemapRenderer.Update(parameters);
            totalFaces += result.NumRenderedFaces;
            ++iterations;
            Assert.True(iterations <= (int)CubeMapFace.MaxCubeMapFaces + 1);
        } while (!result.IsComplete);

        Assert.Equal((uint)CubeMapFace.MaxCubeMapFaces, totalFaces);
        Assert.True(result.IsComplete);
        ClearQueuedViewports();
    }

    [Fact]
    public void Update_with_mismatched_override_returns_complete_error_result()
    {
        ClearQueuedViewports();
        var scene = new Scene();
        scene.Root.GetOrCreateComponent<Octree>();
        using var cubemapRenderer = new CubemapRenderer(scene);
        var overrideTexture = new TextureCube { Name = "Override" };
        CubemapRenderer.DefineTexture(overrideTexture, new CubemapRenderingSettings { TextureSize = 16 });

        var result = cubemapRenderer.Update(new CubemapUpdateParameters
        {
            Settings = new CubemapRenderingSettings { TextureSize = 64 },
            OverrideFinalTexture = overrideTexture
        });

        Assert.True(result.IsComplete);
        Assert.Equal((uint)CubeMapFace.MaxCubeMapFaces, result.NumRenderedFaces);
        ClearQueuedViewports();
    }

    private static T GetPrivateField<T>(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<T>(field.GetValue(instance));
    }

    private void WithInitializedGraphics(Action action)
    {
        var parameters = new EngineParameters
        {
            ApplicationName = "RedBolt.Tests.CubemapRendererTests",
            Headless = false,
            WindowWidth = 64,
            WindowHeight = 64
        };

        RbfxTestData.AddResourceDirs(_fixture.Resources);
        _fixture.Input.Initialize(parameters);
        _fixture.Graphics.Initialize(parameters, _fixture.Input);
        _fixture.RenderDevice.RestoreDeviceState();
        try
        {
            action();
        }
        finally
        {
            _fixture.RenderDevice.InvalidateDeviceState();
            _fixture.Graphics.Shutdown();
            _fixture.Input.Shutdown();
            RbfxTestData.RemoveResourceDirs(_fixture.Resources);
        }
    }
}
