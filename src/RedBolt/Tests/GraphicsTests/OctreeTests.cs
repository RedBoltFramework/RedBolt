using RedBolt;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.Scene;
using SceneType = global::RedBolt.Scene.Scene;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class OctreeTests
{
    private sealed class WorldOriginProbeDrawable : Drawable
    {
        public IntVector3? UpdatedOldOrigin { get; private set; }
        public IntVector3? UpdatedNewOrigin { get; private set; }
        public IntVector3? UpdatedDelta { get; private set; }
        public IntVector3? PostUpdatedOldOrigin { get; private set; }
        public IntVector3? PostUpdatedNewOrigin { get; private set; }
        public IntVector3? PostUpdatedDelta { get; private set; }

        public override void UpdateWorldOrigin(IntVector3 oldOrigin, IntVector3 newOrigin, IntVector3 delta)
        {
            UpdatedOldOrigin = oldOrigin;
            UpdatedNewOrigin = newOrigin;
            UpdatedDelta = delta;
        }

        public override void PostUpdateWorldOrigin(IntVector3 oldOrigin, IntVector3 newOrigin, IntVector3 delta)
        {
            PostUpdatedOldOrigin = oldOrigin;
            PostUpdatedNewOrigin = newOrigin;
            PostUpdatedDelta = delta;
        }

        protected override void OnWorldBoundingBoxUpdate()
        {
            _worldBoundingBox = _boundingBox;
        }
    }

    private readonly HeadlessEngineFixture _fixture;

    public OctreeTests(HeadlessEngineFixture fixture)
    {
        this._fixture = fixture;
    }

    private ResourceCache Resources => _fixture.Resources;

    [Fact]
    public void Size_and_levels_work()
    {
        var octree = new SceneType().CreateComponent<Octree>();
        octree.SetSize(new BoundingBox(-100.0f, 100.0f), 4u);
        Assert.Equal(4u, octree.GetNumLevels());
    }

    [Fact]
    public void Drawable_collection_contains_inserted_drawables()
    {
        var scene = new SceneType();
        var octree = scene.CreateComponent<Octree>();
        octree.SetSize(new BoundingBox(-100.0f, 100.0f), 4u);

        var cube = scene.CreateChild("Cube").CreateComponent<StaticModel>();
        cube.Node.SetPosition(new Vector3f(10.0f, 0.0f, 0.0f));
        octree.QueueUpdate(cube);
        octree.Update(new FrameInfo());

        Assert.Contains(cube, octree.GetAllDrawables());
    }

    [Fact]
    public void Bounding_volume_queries_work()
    {
        var scene = new SceneType();
        var octree = scene.CreateComponent<Octree>();
        octree.SetSize(new BoundingBox(-100.0f, 100.0f), 4u);

        var cube = scene.CreateChild("Cube").CreateComponent<StaticModel>();
        cube.SetModel(Resources.Get<Model>("Models/ProceduralBox"));
        cube.Node.SetPosition(new Vector3f(10.0f, 0.0f, 0.0f));
        octree.QueueUpdate(cube);
        octree.Update(new FrameInfo());

        var result = new List<Drawable>();
        using var query = new BoxOctreeQuery(result, cube.GetWorldBoundingBox());
        octree.GetDrawables(query);
        Assert.Contains(cube, result);
    }

    [Fact]
    public void Raycast_single_hits_drawable()
    {
        var scene = new SceneType();
        var octree = scene.CreateComponent<Octree>();
        octree.SetSize(new BoundingBox(-100.0f, 100.0f), 4u);

        var cubeNode = scene.CreateChild("Cube");
        cubeNode.SetPosition(new Vector3f(10.0f, 0.0f, 0.0f));
        var cube = cubeNode.CreateComponent<StaticModel>();
        octree.QueueUpdate(cube);
        octree.Update(new FrameInfo());

        var ray = new Ray(cube.GetWorldBoundingBox().Center - Vector3f.Forward * 10.0f, Vector3f.Forward);
        using var query = new RayOctreeQuery(ray, RayQueryLevel.RayAabb, maxDistance: 100.0f);
        octree.RaycastSingle(query);
        Assert.Single(query.Result);
        Assert.Same(cube, query.Result[0].Drawable);
        Assert.Same(cubeNode, query.Result[0].Node);
    }

    [Fact]
    public void Manual_drawable_add_and_remove_work()
    {
        var octree = new SceneType().CreateComponent<Octree>();
        octree.SetSize(new BoundingBox(-100.0f, 100.0f), 4u);

        var manualNode = new SceneType().CreateChild("Manual");
        var manual = manualNode.CreateComponent<StaticModel>();
        manualNode.Remove();
        manual.SetBoundingBox(new BoundingBox(
            new Vector3f(20.0f, 20.0f, 20.0f), new Vector3f(21.0f, 21.0f, 21.0f)));
        octree.AddManualDrawable(manual);
        Assert.Contains(manual, octree.GetAllDrawables());
        Assert.NotNull(manual.GetOctant());
        Assert.NotEqual(uint.MaxValue, manual.GetDrawableIndex());

        octree.RemoveManualDrawable(manual);
        Assert.DoesNotContain(manual, octree.GetAllDrawables());
        Assert.Null(manual.GetOctant());
        Assert.Equal(uint.MaxValue, manual.GetDrawableIndex());
    }

    [Fact]
    public void Lock_prevents_manual_add()
    {
        var octree = new SceneType().CreateComponent<Octree>();
        octree.SetSize(new BoundingBox(-100.0f, 100.0f), 4u);
        octree.SetLocked(true);
        var manual = new StaticModel();
        octree.AddManualDrawable(manual);
        Assert.DoesNotContain(manual, octree.GetAllDrawables());
        octree.SetLocked(false);
    }

    [Fact]
    public void Scene_drawable_is_registered_without_being_manual()
    {
        var scene = new SceneType();
        var octree = scene.CreateComponent<Octree>();
        var node = scene.CreateChild("Drawable");
        var drawable = node.CreateComponent<StaticModel>();

        Assert.Contains(drawable, octree.GetAllDrawables());
        octree.RemoveDrawable(drawable, drawable.GetOctant()!);
        Assert.DoesNotContain(drawable, octree.GetAllDrawables());
    }

    [Fact]
    public void Octant_children_and_recursive_insertion_work()
    {
        var octree = new SceneType().CreateComponent<Octree>();
        octree.SetSize(new BoundingBox(-100.0f, 100.0f), 4u);
        var root = octree.GetRootOctant();
        var child = root.GetOrCreateChild(7u);

        Assert.Same(root, child.GetParent());
        Assert.Equal(1u, child.GetLevel());
        Assert.Equal(new BoundingBox(Vector3f.Zero, new Vector3f(100.0f, 100.0f, 100.0f)), child.GetWorldBoundingBox());

        var manualNode = new SceneType().CreateChild("Manual");
        var manual = manualNode.CreateComponent<StaticModel>();
        manualNode.Remove();
        manual.SetBoundingBox(new BoundingBox(new Vector3f(10.0f, 10.0f, 10.0f), new Vector3f(11.0f, 11.0f, 11.0f)));
        root.InsertDrawable(manual);

        Assert.Same(root, manual.GetOctant()?.GetParent()?.GetParent()?.GetParent()?.GetParent());
        Assert.Equal(1u, root.GetNumDrawables());
        manual.GetOctant()!.RemoveDrawable(manual);
        Assert.Null(manual.GetOctant());
        Assert.Equal(0u, root.GetNumDrawables());
    }

    [Fact]
    public void Resizing_octree_moves_child_drawables_to_root_and_queues_reinsertion()
    {
        var scene = new SceneType();
        var octree = scene.CreateComponent<Octree>();
        var box = new BoundingBox(-100.0f, 100.0f);
        octree.SetSize(box, 4u);

        var manualNode = new SceneType().CreateChild("Manual");
        var manual = manualNode.CreateComponent<StaticModel>();
        manualNode.Remove();
        manual.SetBoundingBox(new BoundingBox(new Vector3f(10.0f, 10.0f, 10.0f), new Vector3f(11.0f, 11.0f, 11.0f)));
        octree.AddManualDrawable(manual);
        Assert.NotSame(octree.GetRootOctant(), manual.GetOctant());

        octree.SetSize(box, 4u);

        Assert.Same(octree.GetRootOctant(), manual.GetOctant());
        Assert.Equal(1u, octree.GetRootOctant().GetNumDrawables());

        octree.Update(new FrameInfo());
        Assert.NotNull(manual.GetOctant());
        Assert.NotEqual(uint.MaxValue, manual.GetDrawableIndex());
    }

    [Fact]
    public void Disposing_octree_detaches_drawables_recursively()
    {
        var scene = new SceneType();
        var octree = scene.CreateComponent<Octree>();
        octree.SetSize(new BoundingBox(-100.0f, 100.0f), 4u);

        var manualNode = new SceneType().CreateChild("Manual");
        var manual = manualNode.CreateComponent<StaticModel>();
        manualNode.Remove();
        manual.SetBoundingBox(new BoundingBox(new Vector3f(10.0f, 10.0f, 10.0f), new Vector3f(11.0f, 11.0f, 11.0f)));
        octree.AddManualDrawable(manual);
        Assert.NotSame(octree.GetRootOctant(), manual.GetOctant());

        octree.Dispose();

        Assert.Null(manual.GetOctant());
        Assert.Equal(uint.MaxValue, manual.GetDrawableIndex());
    }

    [Fact]
    public void Update_sends_drawable_update_finished_event()
    {
        var scene = new SceneType();
        var octree = scene.CreateComponent<Octree>();
        SceneEvents.SceneDrawableUpdateFinishedEvent? received = null;
        scene.SubscribeToEvent<SceneEvents.SceneDrawableUpdateFinishedEvent>((ref SceneEvents.SceneDrawableUpdateFinishedEvent eventData) => received = eventData);

        octree.Update(new FrameInfo { TimeStep = 0.25f });

        Assert.True(received.HasValue);
        Assert.Same(scene, received.Value.Scene);
        Assert.Equal(0.25f, received.Value.TimeStep);
    }

    [Fact]
    public void Draw_debug_geometry_uses_scene_debug_renderer()
    {
        var scene = new SceneType();
        var octree = scene.CreateComponent<Octree>();
        var debugRenderer = scene.CreateComponent<DebugRenderer>();
        debugRenderer.SetView(new Camera());

        octree.SetSize(new BoundingBox(-1.0f, 1.0f), 1u);
        octree.DrawDebugGeometry(debugRenderer, depthTest: false);

        Assert.True(debugRenderer.HasContent());
    }

    [Fact]
    public void World_origin_update_forwards_to_drawables()
    {
        var scene = new SceneType();
        var octree = scene.CreateComponent<Octree>();
        var drawable = new WorldOriginProbeDrawable();
        octree.AddManualDrawable(drawable);

        var oldOrigin = IntVector3.Zero;
        var newOrigin = new IntVector3(11, 22, 33);
        var delta = newOrigin;
        scene.UpdateWorldOrigin(newOrigin);

        Assert.Equal(oldOrigin, drawable.UpdatedOldOrigin);
        Assert.Equal(newOrigin, drawable.UpdatedNewOrigin);
        Assert.Equal(delta, drawable.UpdatedDelta);
        Assert.Equal(oldOrigin, drawable.PostUpdatedOldOrigin);
        Assert.Equal(newOrigin, drawable.PostUpdatedNewOrigin);
        Assert.Equal(delta, drawable.PostUpdatedDelta);
    }
}
