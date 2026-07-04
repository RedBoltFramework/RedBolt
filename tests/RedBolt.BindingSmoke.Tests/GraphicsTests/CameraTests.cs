using RedBolt;

namespace RedBolt.BindingSmoke.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class CameraTests
{
    [Fact]
    public void Enabled_attribute_updates_live_property()
    {
        var scene = new Scene();
        var camera = scene.CreateChild("Camera").CreateComponent<Camera>();
        Assert.True(camera.SetAttribute("Is Enabled", false));
        Assert.False(camera.Enabled);
        Assert.False(camera.IsEnabledEffective);
    }

    [Fact]
    public void Clip_and_fov_helpers_clamp_values()
    {
        var scene = new Scene();
        var camera = scene.CreateChild("Camera").CreateComponent<Camera>();
        camera.SetNearClip(-1.0f);
        camera.SetFarClip(200.0f);
        camera.SetFov(200.0f);

        Assert.True(MathDefs.ApproximatelyEquivalent(camera.GetNearClip(), MathDefs.MinNearClip));
        Assert.True(MathDefs.ApproximatelyEquivalent(camera.GetFarClip(), 200.0f));
        Assert.True(MathDefs.ApproximatelyEquivalent(camera.GetFov(), 160.0f));
    }

    [Fact]
    public void Aspect_and_ortho_size_helpers_work()
    {
        var scene = new Scene();
        var camera = scene.CreateChild("Camera").CreateComponent<Camera>();
        camera.SetAspectRatio(2.0f);
        camera.SetOrthoSize(new Vector2(40.0f, 20.0f));

        Assert.False(camera.GetAutoAspectRatio());
        Assert.True(MathDefs.ApproximatelyEquivalent(camera.GetAspectRatio(), 2.0f));
        Assert.True(MathDefs.ApproximatelyEquivalent(camera.GetOrthoSize(), 20.0f));
    }

    [Fact]
    public void Zoom_and_lod_bias_clamp_to_epsilon()
    {
        var scene = new Scene();
        var camera = scene.CreateChild("Camera").CreateComponent<Camera>();
        camera.SetZoom(0.0f);
        camera.SetLodBias(0.0f);
        Assert.True(MathDefs.ApproximatelyEquivalent(camera.GetZoom(), MathDefs.Epsilon));
        Assert.True(MathDefs.ApproximatelyEquivalent(camera.GetLodBias(), MathDefs.Epsilon));
    }

    [Fact]
    public void View_masks_roundtrip()
    {
        var scene = new Scene();
        var camera = scene.CreateChild("Camera").CreateComponent<Camera>();
        camera.SetViewMask(0x0fu);
        camera.SetZoneMask(0xf0u);
        camera.SetViewOverrideFlags(ViewOverride.DisableShadows | ViewOverride.DisableOcclusion);

        Assert.Equal(0x0fu, camera.GetViewMask());
        Assert.Equal(0xf0u, camera.GetZoneMask());
        Assert.Equal(ViewOverride.DisableShadows | ViewOverride.DisableOcclusion, camera.GetViewOverrideFlags());
    }

    [Fact]
    public void Projection_and_mouse_helpers_work()
    {
        var scene = new Scene();
        var camera = scene.CreateChild("Camera").CreateComponent<Camera>();
        camera.SetProjectionOffset(new Vector2(0.1f, 0.2f));
        camera.SetMousePosition(new Vector2(0.5f, 0.25f));
        camera.SetOrthographic(true);

        Assert.True(camera.GetProjectionOffset().ApproximatelyEquivalent(new Vector2(0.1f, 0.2f)));
        Assert.True(camera.HasMousePosition());
        Assert.True(camera.IsOrthographic());
    }

    [Fact]
    public void FillMode_and_distance_helpers_work()
    {
        var scene = new Scene();
        var camera = scene.CreateChild("Camera").CreateComponent<Camera>();
        camera.SetFillMode(FillMode.Point);
        Assert.Equal(FillMode.Point, camera.GetFillMode());
        Assert.True(MathDefs.ApproximatelyEquivalent(camera.GetDistance(new Vector3(0.0f, 0.0f, 5.0f)), 5.0f));
        Assert.True(camera.GetLodDistance(100.0f, 1.0f, 1.0f) > 0.0f);
    }

    [Fact]
    public void Viewport_managed_state_helpers_work()
    {
        var scene = new Scene();
        var camera = scene.CreateChild("Camera").CreateComponent<Camera>();
        var viewport = new Viewport(scene, camera, new IntRect(0, 0, 320, 200));
        var childCamera = scene.CreateChild("ChildCamera").CreateComponent<Camera>();

        viewport.SetDrawDebug(false);
        viewport.SetCullCamera(childCamera);
        viewport.SetEye(childCamera, 1);

        Assert.Equal(new IntRect(0, 0, 320, 200), viewport.GetRect());
        Assert.False(viewport.GetDrawDebug());
        Assert.Same(childCamera, viewport.GetCullCamera());
        Assert.True(viewport.IsStereo());
        Assert.Same(childCamera, viewport.GetEye(1));
    }
}
