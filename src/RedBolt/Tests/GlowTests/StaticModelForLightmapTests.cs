namespace RedBolt.Tests;

using RedBolt.Glow;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.Scene;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class StaticModelForLightmapTests
{
    private readonly HeadlessEngineFixture _fixture;

    public StaticModelForLightmapTests(HeadlessEngineFixture fixture)
    {
        _fixture = fixture;
    }

    private ResourceCache Resources => _fixture.Resources;

    [Fact]
    public void Initialize_creates_baking_batches_and_geometry_mapping()
    {
        var scene = new Scene();
        scene.CreateComponent<Octree>();
        var sourceNode = scene.CreateChild("Source");
        sourceNode.SetPosition(new Vector3f(1.0f, 2.0f, 3.0f));
        var sourceObject = sourceNode.CreateComponent<StaticModel>();
        var sourceModel = Resources.Get<Model>("Models/ProceduralBox");
        sourceObject.SetModel(sourceModel);
        var sourceMaterial = new Material();
        sourceMaterial.SetShaderParameter("MatDiffColor", Vector4f.One);
        sourceMaterial.SetShaderParameter("MatEmissiveColor", Vector3f.Zero);
        sourceMaterial.SetShaderParameter("UOffset", Vector4f.Zero);
        sourceMaterial.SetShaderParameter("VOffset", Vector4f.Zero);
        sourceObject.SetMaterial(sourceMaterial);

        var bakingNode = scene.CreateChild("Baking");
        bakingNode.SetPosition(sourceNode.GetWorldPosition());
        var staticModelForLightmap = bakingNode.CreateComponent<StaticModelForLightmap>();
        var bakingMaterial = Resources.Get<Material>("Materials/LightmapBaker.xml");
        Vector2f[] multiTapOffsets = [Vector2f.Zero, new Vector2f(0.25f, 0.5f)];

        var mapping = staticModelForLightmap.Initialize(
            5, sourceObject, bakingMaterial, 10, multiTapOffsets, new Vector2f(0.01f, 0.01f),
            new Vector4f(0.5f, 0.5f, 0.25f, 0.25f), new Vector2f(0.00002f, 0.002f));

        var objectMapping = Assert.Single(mapping);
        Assert.Equal(5, objectMapping.ObjectIndex);
        Assert.Equal(0, objectMapping.GeometryIndex);
        Assert.Equal(0, objectMapping.LodIndex);

        var batches = staticModelForLightmap.GetBatches();
        Assert.Equal(multiTapOffsets.Length, batches.Count);
        foreach (var batch in batches)
        {
            Assert.Same(sourceModel.GetGeometry(0, 0), batch.Geometry);
            Assert.Equal(GeometryType.StaticNoInstancing, batch.GeometryType);
            Assert.NotSame(bakingMaterial, batch.Material);
            Assert.Equal(1U, batch.NumWorldTransforms);
            Assert.Equal(bakingNode.GetWorldTransform(), batch.WorldTransform[0]);
        }
        Assert.Same(batches[0].WorldTransform, batches[1].WorldTransform);

        var expectedWorldBoundingBox = new BoundingBox(
            -Vector3f.One * MathDefs.LargeValue, Vector3f.One * MathDefs.LargeValue);
        Assert.Equal(expectedWorldBoundingBox, staticModelForLightmap.GetWorldBoundingBox());
        Assert.Equal(0.0f, staticModelForLightmap.GetDistance());
        Assert.Equal(0.0f, staticModelForLightmap.GetLodDistance());
    }
}
