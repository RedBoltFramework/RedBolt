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
            File.WriteAllBytes(Path.Combine(tempDir, "Models", "Box.mdl"), CreateUrhoModelBytes());
            Resources.AddResourceDir(tempDir);

            var model = Resources.GetResource<Model>("Models/Box.mdl");
            Assert.NotNull(model.GetGeometry(0));
            Assert.Equal(1u, model.GetNumGeometries());
            Assert.Equal(36u, model.GetGeometry(0)!.GetEffectiveIndexCount());
            Assert.Equal(8u, model.GetGeometry(0)!.GetVertexBuffer(0)!.GetVertexCount());
            Assert.Equal((uint)VertexMask.Position, model.GetGeometry(0)!.GetVertexBuffer(0)!.GetElementMask());
            Assert.Equal(Intersection.Inside, model.GetBoundingBox().IsInside(Vector3.Zero));
        }
        finally
        {
            Resources.RemoveResourceDir(tempDir);
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private static byte[] CreateUrhoModelBytes()
    {
        var buffer = new VectorBuffer();
        buffer.WriteFileID("UMD2");

        buffer.WriteUInt(1u);
        buffer.WriteUInt(8u);
        buffer.WriteUInt(1u);
        buffer.WriteUInt((uint)VertexElementType.Vector3 | ((uint)VertexElementSemantic.Position << 8));
        buffer.WriteUInt(0u);
        buffer.WriteUInt(0u);

        foreach (var position in GetPositions())
            buffer.WriteVector3(position);

        buffer.WriteUInt(1u);
        buffer.WriteUInt(36u);
        buffer.WriteUInt(2u);
        foreach (var index in GetIndices())
            buffer.WriteUShort(index);

        buffer.WriteUInt(1u);
        buffer.WriteUInt(0u);
        buffer.WriteUInt(1u);
        buffer.WriteFloat(0.0f);
        buffer.WriteUInt((uint)PrimitiveType.TriangleList);
        buffer.WriteUInt(0u);
        buffer.WriteUInt(0u);
        buffer.WriteUInt(0u);
        buffer.WriteUInt(36u);

        buffer.WriteUInt(0u);
        buffer.WriteUInt(0u);
        buffer.WriteBoundingBox(new BoundingBox(new Vector3(-1.0f, -1.0f, -1.0f), new Vector3(1.0f, 1.0f, 1.0f)));
        buffer.WriteVector3(Vector3.Zero);
        return buffer.GetBuffer();
    }

    private static IReadOnlyList<Vector3> GetPositions() =>
    [
        new Vector3(-1.0f, -1.0f, 0.0f),
        new Vector3(1.0f, -1.0f, 0.0f),
        new Vector3(1.0f, 1.0f, 0.0f),
        new Vector3(-1.0f, 1.0f, 0.0f),
        new Vector3(-1.0f, -1.0f, 1.0f),
        new Vector3(1.0f, -1.0f, 1.0f),
        new Vector3(1.0f, 1.0f, 1.0f),
        new Vector3(-1.0f, 1.0f, 1.0f)
    ];

    private static IReadOnlyList<ushort> GetIndices() =>
    [
        0, 1, 2, 0, 2, 3,
        0, 4, 5, 0, 5, 1,
        0, 3, 7, 0, 7, 4,
        3, 2, 6, 3, 6, 7,
        4, 7, 6, 4, 6, 5,
        1, 5, 6, 1, 6, 2
    ];
}
