using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class RibbonTrailTests
{
    private readonly HeadlessEngineFixture _fixture;

    public RibbonTrailTests(HeadlessEngineFixture fixture)
    {
        _fixture = fixture;
        FirstCubeTestData.AddResourceDirs(Resources);
    }

    private ResourceCache Resources => _fixture.Resources;

    [Fact]
    public void Class_kinds_match_cpp()
    {
        Assert.True(typeof(TrailPoint).IsValueType);
        Assert.False(typeof(ICloneable).IsAssignableFrom(typeof(TrailPoint)));
        Assert.False(typeof(RibbonTrail).IsValueType);
        Assert.False(typeof(ICloneable).IsAssignableFrom(typeof(RibbonTrail)));
    }

    [Fact]
    public void Trail_point_copy_preserves_cpp_pod_semantics()
    {
        var point = new TrailPoint(new Vector3f(1, 2, 3), Vector3f.Forward) { NextIndex = 4 };
        var copy = point;
        copy.Position = Vector3f.Zero;

        Assert.Equal(new Vector3f(1, 2, 3), point.Position);
        Assert.Equal(4, copy.NextIndex);
        Assert.Equal(point.NextIndex, copy.NextIndex);
        Assert.Null(default(TrailPoint).NextIndex);
    }

    [Fact]
    public void Dispose_releases_owned_geometry_before_drawable_teardown()
    {
        var trail = new RibbonTrail();
        var geometry = trail.GetBatches()[0].Geometry!;

        trail.Dispose();

        Assert.Equal(0u, geometry.GetNumVertexBuffers());
        Assert.Null(geometry.GetIndexBuffer());
        Assert.Empty(trail.GetBatches());
    }

    [Fact]
    public void Defaults_match_cpp_constructor()
    {
        var trail = new Scene().CreateChild("Trail").CreateComponent<RibbonTrail>();
        Assert.Equal(DrawableFlags.Geometry, trail.GetDrawableFlags());
        Assert.True(trail.IsEmitting());
        Assert.False(trail.GetUpdateInvisible());
        Assert.Equal(TrailType.FaceCamera, trail.GetTrailType());
        Assert.Equal(Vector3f.Zero, trail.GetBaseVelocity());
        Assert.Equal(1.0f, trail.GetLifetime());
        Assert.Equal(1u, trail.GetTailColumn());
        Assert.Equal(0.1f, trail.GetVertexDistance());
        Assert.Equal(0.2f, trail.GetWidth());
        Assert.Equal(1.0f, trail.GetStartScale());
        Assert.Equal(1.0f, trail.GetEndScale());
        Assert.Equal(Color.White, trail.GetStartColor());
        Assert.Equal(new Color(1.0f, 1.0f, 1.0f, 0.0f), trail.GetEndColor());
        Assert.Equal(1.0f, trail.GetAnimationLodBias());
        Assert.False(trail.IsSorted());
        Assert.Equal(GeometryType.TrailFaceCamera, trail.GetBatches()[0].GeometryType);
    }

    [Fact]
    public void Material_and_parameters_roundtrip()
    {
        var material = Resources.Get<Material>("Materials/FirstCube");
        var trail = new Scene().CreateChild("Trail").CreateComponent<RibbonTrail>();
        trail.SetMaterial(material);
        trail.SetWidth(0.5f);
        trail.SetVertexDistance(0.25f);
        trail.SetLifetime(2.0f);
        trail.SetStartScale(2.0f);
        trail.SetEndScale(0.5f);
        trail.SetStartColor(Color.Red);
        trail.SetEndColor(Color.Blue);
        trail.SetSorted(true);
        trail.SetTailColumn(3);
        trail.SetAnimationLodBias(1.5f);
        trail.SetUpdateInvisible(true);
        trail.SetBaseVelocity(new Vector3f(0, 1, 0));

        Assert.Same(material, trail.GetMaterial());
        Assert.Equal(0.5f, trail.GetWidth());
        Assert.Equal(0.25f, trail.GetVertexDistance());
        Assert.Equal(2.0f, trail.GetLifetime());
        Assert.Equal(2.0f, trail.GetStartScale());
        Assert.Equal(0.5f, trail.GetEndScale());
        Assert.Equal(Color.Red, trail.GetStartColor());
        Assert.Equal(Color.Blue, trail.GetEndColor());
        Assert.True(trail.IsSorted());
        Assert.Equal(3u, trail.GetTailColumn());
        Assert.Equal(1.5f, trail.GetAnimationLodBias());
        Assert.True(trail.GetUpdateInvisible());
        Assert.Equal(new Vector3f(0, 1, 0), trail.GetBaseVelocity());
        Assert.Equal(material.Name, trail.GetMaterialAttr().Name);
    }

    [Fact]
    public void Update_emits_points_when_node_moves()
    {
        var scene = new Scene();
        var node = scene.CreateChild("Trail");
        node.SetWorldPosition(Vector3f.Zero);
        var trail = node.CreateComponent<RibbonTrail>();
        trail.SetUpdateInvisible(true);
        trail.SetVertexDistance(0.05f);
        trail.OnSetEnabled();

        // First post-update seeds previous position.
        var post = new SceneEvents.ScenePostUpdateEvent { Scene = scene, TimeStep = 1.0f / 60.0f };
        scene.SendEvent(ref post);
        trail.Update(new FrameInfo { FrameNumber = 1, TimeStep = 1.0f / 60.0f, Scene = scene });

        node.SetWorldPosition(new Vector3f(0, 0, 1));
        post = new SceneEvents.ScenePostUpdateEvent { Scene = scene, TimeStep = 1.0f / 60.0f };
        scene.SendEvent(ref post);
        trail.Update(new FrameInfo { FrameNumber = 2, TimeStep = 1.0f / 60.0f, Scene = scene });

        var frame = new FrameInfo { FrameNumber = 2, TimeStep = 1.0f / 60.0f, Scene = scene };
        trail.UpdateBatchesDelayed(frame);
        Assert.True(trail.GetBatches()[0].Geometry!.GetIndexCount() > 0);
    }

    [Fact]
    public void Tail_column_clamps_to_max()
    {
        var trail = new Scene().CreateChild("Trail").CreateComponent<RibbonTrail>();
        trail.SetTailColumn(100);
        Assert.Equal(16u, trail.GetTailColumn());
        trail.SetTailColumn(0);
        Assert.Equal(1u, trail.GetTailColumn());
    }
}
