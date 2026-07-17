using System.Collections;
using System.Reflection;
using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class CubemapRendererTests
{
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
    public void DefineTexture_sets_size_format_and_render_target_flag()
    {
        var texture = new TextureCube("TestCubemap");
        var settings = new CubemapRenderingSettings { TextureSize = 64 };

        CubemapRenderer.DefineTexture(texture, settings, TextureFlag.None);

        Assert.Equal(64, texture.Width);
        Assert.Equal(64, texture.Height);
        Assert.Equal(TextureFormat.RGBA8, texture.Format);
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
        var overrideTexture = new TextureCube("Override");
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
}
