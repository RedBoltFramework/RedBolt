using RedBolt;
using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.Scene;
using SceneType = global::RedBolt.Scene.Scene;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed partial class SkyboxTests
{
    private readonly HeadlessEngineFixture _fixture;

    public SkyboxTests(HeadlessEngineFixture fixture)
    {
        this._fixture = fixture;
        FirstCubeTestData.AddResourceDirs(Resources);
    }

    private ResourceCache Resources => _fixture.Resources;

    [Fact]
    public void Defaults_and_bounds_work()
    {
        var model = Resources.Get<Model>("Models/ProceduralBox");
        var material = Resources.Get<Material>("Materials/FirstCube");
        var skybox = new SceneType().CreateChild("Skybox").CreateComponent<Skybox>();
        skybox.SetModel(model);
        skybox.SetMaterial(material);

        Assert.Equal(ReflectionMode.Zone, skybox.GetReflectionMode());
        Assert.Equal(DrawableFlags.Geometry, skybox.GetDrawableFlags());
        Assert.Null(skybox.GetImage());
        Assert.Equal(Intersection.Inside,
            skybox.GetWorldBoundingBox().IsInside(new Vector3f(50_000.0f, 0.0f, 0.0f)));
        Assert.False(typeof(Skybox).IsSealed);
        Assert.False(typeof(ICloneable).IsAssignableFrom(typeof(Skybox)));
    }

    [Fact]
    public void Registration_and_camera_transform_lifetime_match_cpp()
    {
        var context = Context.Instance;
        using var reflected = Assert.IsType<Skybox>(context.CreateObject(ObjectReflection<Skybox>.TypeId));
        Assert.Equal(ObjectCategory.Category_Geometry, context.GetReflection<Skybox>().Category);

        var scene = new SceneType();
        var skyboxNode = scene.CreateChild("Skybox");
        skyboxNode.SetPosition(new Vector3f(1.0f, 2.0f, 3.0f));
        var skybox = skyboxNode.CreateComponent<TestSkybox>();
        skybox.SetModel(Resources.Get<Model>("Models/ProceduralBox"));
        skybox.SetMaterial(Resources.Get<Material>("Materials/FirstCube"));
        var cameraNode = scene.CreateChild("Camera");
        cameraNode.SetPosition(new Vector3f(10.0f, 20.0f, 30.0f));
        var camera = cameraNode.CreateComponent<Camera>();
        var frame = new FrameInfo { FrameNumber = 1, Camera = camera };

        skybox.UpdateBatches(frame);

        var firstTransform = skybox.GetBatches()[0].WorldTransform;
        Assert.Equal(new Vector3f(11.0f, 22.0f, 33.0f), firstTransform[0].Translation);
        Assert.Equal(1, skybox.NumCustomWorldTransforms);

        skyboxNode.SetPosition(new Vector3f(2.0f, 3.0f, 4.0f));
        skybox.UpdateBatches(frame);
        Assert.Same(firstTransform, skybox.GetBatches()[0].WorldTransform);
        Assert.Equal(new Vector3f(11.0f, 22.0f, 33.0f), firstTransform[0].Translation);

        frame.FrameNumber = 2;
        skybox.UpdateBatches(frame);
        Assert.Same(firstTransform, skybox.GetBatches()[0].WorldTransform);
        Assert.Equal(new Vector3f(12.0f, 23.0f, 34.0f), skybox.GetBatches()[0].WorldTransform[0].Translation);

        skybox.Dispose();
        Assert.Equal(0, skybox.NumCustomWorldTransforms);
    }

    [Reflected]
    private sealed partial class TestSkybox : Skybox
    {
        public int NumCustomWorldTransforms => _customWorldTransforms.Count;
    }
}
