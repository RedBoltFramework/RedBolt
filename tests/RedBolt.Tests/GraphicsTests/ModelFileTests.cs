using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class ModelFileTests
{
    private readonly HeadlessEngineFixture fixture;

    public ModelFileTests(HeadlessEngineFixture fixture)
    {
        this.fixture = fixture;
    }

    private ResourceCache Resources => fixture.Resources;

    [Fact]
    public void File_backed_model_loads_through_resource_cache()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "RedBolt.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(tempDir, "Models"));
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "Models", "Box.mdl"),
                "{\"BoundingBox\":{\"Min\":{\"X\":-1,\"Y\":-1,\"Z\":-1},\"Max\":{\"X\":1,\"Y\":1,\"Z\":1}},\"Geometries\":[{\"PrimitiveType\":\"TriangleList\",\"Positions\":[{\"X\":-1,\"Y\":-1,\"Z\":0},{\"X\":1,\"Y\":-1,\"Z\":0},{\"X\":1,\"Y\":1,\"Z\":0},{\"X\":-1,\"Y\":1,\"Z\":0},{\"X\":-1,\"Y\":-1,\"Z\":1},{\"X\":1,\"Y\":-1,\"Z\":1},{\"X\":1,\"Y\":1,\"Z\":1},{\"X\":-1,\"Y\":1,\"Z\":1}],\"Indices\":[0,1,2,0,2,3,0,4,5,0,5,1,0,3,7,0,7,4,3,2,6,3,6,7,4,7,6,4,6,5,1,5,6,1,6,2]}]}" );
            Resources.AddResourceDir(tempDir);

            var model = Resources.GetResource<Model>("Models/Box.mdl");
            Assert.NotNull(model.GetGeometry(0));
            Assert.Equal(1u, model.GetNumGeometries());
            Assert.Equal(36u, model.GetGeometry(0)!.GetEffectiveIndexCount());
            Assert.Equal(8u, model.GetGeometry(0)!.GetVertexBuffer(0)!.GetVertexCount());
            Assert.Equal(Intersection.Inside, model.GetBoundingBox().IsInside(Vector3.Zero));
        }
        finally
        {
            Resources.RemoveResourceDir(tempDir);
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
