using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class DecalSetTests
{
    private readonly HeadlessEngineFixture _fixture;

    public DecalSetTests(HeadlessEngineFixture fixture)
    {
        _fixture = fixture;
        FirstCubeTestData.AddResourceDirs(Resources);
    }

    private ResourceCache Resources => _fixture.Resources;

    [Fact]
    public void DecalVertex_copy_is_independent()
    {
        var source = new DecalVertex(Vector3f.One, Vector3f.Up);
        var copy = source;
        copy.Position = Vector3f.Zero;

        Assert.Equal(Vector3f.One, source.Position);
        Assert.Equal(Vector3f.Zero, copy.Position);
    }

    [Fact]
    public void Decal_clone_deep_copies_value_containers()
    {
        var source = new Decal();
        source.AddVertex(new DecalVertex(Vector3f.One, Vector3f.Up));

        var copy = (Decal)source.Clone();
        var copiedVertex = copy.Vertices[0];
        copiedVertex.Position = Vector3f.Zero;
        copy.Vertices[0] = copiedVertex;
        copy.Indices.Clear();

        Assert.Equal(Vector3f.One, source.Vertices[0].Position);
        Assert.Single(source.Indices);
    }

    [Fact]
    public void DecalSet_is_not_copyable()
    {
        Assert.False(typeof(ICloneable).IsAssignableFrom(typeof(DecalSet)));
    }

    [Fact]
    public void Defaults_match_cpp_constructor()
    {
        var decals = new Scene().CreateChild("Decals").CreateComponent<DecalSet>();
        Assert.Equal(DrawableFlags.Geometry, decals.GetDrawableFlags());
        Assert.Equal(0u, decals.GetNumDecals());
        Assert.Equal(0u, decals.GetNumVertices());
        Assert.Equal(0u, decals.GetNumIndices());
        Assert.Equal(512u, decals.GetMaxVertices());
        Assert.Equal(1024u, decals.GetMaxIndices());
        Assert.False(decals.GetOptimizeBufferSize());
        Assert.Null(decals.GetMaterial());
        Assert.Equal(GeometryType.StaticNoInstancing, decals.GetBatches()[0].GeometryType);
    }

    [Fact]
    public void Material_and_limits_roundtrip()
    {
        var material = Resources.Get<Material>("Materials/FirstCube");
        var decals = new Scene().CreateChild("Decals").CreateComponent<DecalSet>();
        decals.SetMaterial(material);
        decals.SetMaxVertices(256);
        decals.SetMaxIndices(512);
        decals.SetOptimizeBufferSize(true);

        Assert.Same(material, decals.GetMaterial());
        Assert.Equal(256u, decals.GetMaxVertices());
        Assert.Equal(512u, decals.GetMaxIndices());
        Assert.True(decals.GetOptimizeBufferSize());
        Assert.Equal(material.Name, decals.GetMaterialAttr().Name);
    }

    [Fact]
    public void AddDecal_without_cpu_geometry_returns_empty_success()
    {
        var model = Resources.Get<Model>("Models/ProceduralBox");
        var material = Resources.Get<Material>("Materials/FirstCube");
        var scene = new Scene();
        var targetNode = scene.CreateChild("Target");
        var target = targetNode.CreateComponent<StaticModel>();
        target.SetModel(model);
        target.SetMaterial(material);

        var decalNode = scene.CreateChild("Decals");
        var decals = decalNode.CreateComponent<DecalSet>();
        decals.SetMaterial(material);

        var geometry = Assert.IsType<Geometry>(target.GetLodGeometry(0, 0));
        geometry.GetRawData(out var vertexData, out _, out _, out _, out _);
        Assert.Null(vertexData);

        var ok = decals.AddDecal(target, Vector3f.Zero, Quaternion.Identity, 2.0f, 1.0f, 1.0f,
            new Vector2f(0, 0), new Vector2f(1, 1));
        Assert.True(ok);
        Assert.Equal(0u, decals.GetNumDecals());
        Assert.Equal(0u, decals.GetNumVertices());
        Assert.Equal(0u, decals.GetNumIndices());
    }

    [Fact]
    public void Decals_attribute_empty_roundtrip()
    {
        var decals = new Scene().CreateChild("Decals").CreateComponent<DecalSet>();
        var attr = decals.GetDecalsAttr();
        Assert.NotEmpty(attr);
        decals.SetDecalsAttr(attr);
        Assert.Equal(0u, decals.GetNumDecals());
    }

    [Fact]
    public void ProcessRayQuery_returns_no_hits()
    {
        var decals = new Scene().CreateChild("Decals").CreateComponent<DecalSet>();
        var results = new List<RayQueryResult>();
        var query = new RayOctreeQuery(results, new Ray(Vector3f.Zero, Vector3f.Forward));
        decals.ProcessRayQuery(query, results);
        Assert.Empty(results);
    }
}
