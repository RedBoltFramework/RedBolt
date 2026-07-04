using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class OctreeTests
{
    private readonly HeadlessEngineFixture fixture;

    public OctreeTests(HeadlessEngineFixture fixture)
    {
        this.fixture = fixture;
    }

    private ResourceCache Resources => fixture.Resources;

    [Fact]
    public void Size_and_levels_work()
    {
        var octree = new Scene().CreateComponent<Octree>();
        octree.SetSize(new BoundingBox(-100.0f, 100.0f), 4u);
        Assert.Equal(4u, octree.GetNumLevels());
    }

    [Fact]
    public void Drawable_collection_contains_inserted_drawables()
    {
        var scene = new Scene();
        var octree = scene.CreateComponent<Octree>();
        octree.SetSize(new BoundingBox(-100.0f, 100.0f), 4u);

        var cube = scene.CreateChild("Cube").CreateComponent<StaticModel>();
        cube.Node.SetPosition(new Vector3(10.0f, 0.0f, 0.0f));
        octree.QueueUpdate(cube);
        octree.Update();

        Assert.Contains(cube, octree.GetAllDrawables());
    }

    [Fact]
    public void Bounding_volume_queries_work()
    {
        var scene = new Scene();
        var octree = scene.CreateComponent<Octree>();
        octree.SetSize(new BoundingBox(-100.0f, 100.0f), 4u);

        var cube = scene.CreateChild("Cube").CreateComponent<StaticModel>();
        cube.SetModel(Resources.Get<Model>("Models/ProceduralBox"));
        cube.Node.SetPosition(new Vector3(10.0f, 0.0f, 0.0f));
        octree.QueueUpdate(cube);
        octree.Update();

        Assert.Contains(cube, octree.GetDrawables(cube.GetWorldBoundingBox()));
    }

    [Fact]
    public void Raycast_single_hits_drawable()
    {
        var scene = new Scene();
        var octree = scene.CreateComponent<Octree>();
        octree.SetSize(new BoundingBox(-100.0f, 100.0f), 4u);

        var cubeNode = scene.CreateChild("Cube");
        cubeNode.SetPosition(new Vector3(10.0f, 0.0f, 0.0f));
        var cube = cubeNode.CreateComponent<StaticModel>();
        octree.QueueUpdate(cube);
        octree.Update();

        var ray = new Ray(cube.GetWorldBoundingBox().Center - Vector3.Forward * 10.0f, Vector3.Forward);
        var hit = octree.RaycastSingle(ray, maxDistance: 100.0f);
        Assert.NotNull(hit);
        Assert.Same(cube, hit.Drawable);
        Assert.Same(cubeNode, hit.Node);
    }

    [Fact]
    public void Manual_drawable_add_and_remove_work()
    {
        var octree = new Scene().CreateComponent<Octree>();
        octree.SetSize(new BoundingBox(-100.0f, 100.0f), 4u);

        var manual = new StaticModel();
        manual.SetBoundingBox(new BoundingBox(
            new Vector3(20.0f, 20.0f, 20.0f), new Vector3(21.0f, 21.0f, 21.0f)));
        octree.AddManualDrawable(manual);
        Assert.Contains(manual, octree.GetAllDrawables());

        octree.RemoveManualDrawable(manual);
        Assert.DoesNotContain(manual, octree.GetAllDrawables());
    }

    [Fact]
    public void Lock_prevents_manual_add()
    {
        var octree = new Scene().CreateComponent<Octree>();
        octree.SetSize(new BoundingBox(-100.0f, 100.0f), 4u);
        octree.SetLocked(true);
        var manual = new StaticModel();
        octree.AddManualDrawable(manual);
        Assert.DoesNotContain(manual, octree.GetAllDrawables());
        Assert.True(octree.GetLocked());
        octree.SetLocked(false);
    }
}
