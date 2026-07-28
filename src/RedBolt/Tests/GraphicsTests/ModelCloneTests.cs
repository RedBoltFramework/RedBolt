namespace RedBolt.Tests;

using RedBolt;
using RedBolt.Container;
using RedBolt.Graphics;
using RedBolt.RenderAPI;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class ModelCloneTests
{
    [Fact]
    public void Clone_deep_copies_buffers_and_geometries()
    {
        var vertexBuffer = new VertexBuffer();
        vertexBuffer.SetDebugName("Vertices");
        vertexBuffer.SetShadowed(true);
        vertexBuffer.SetSize(3u, [new VertexElement(VertexElementType.Vector3, VertexElementSemantic.Position)]);
        vertexBuffer.Update(new byte[36]);

        var indexBuffer = new IndexBuffer();
        indexBuffer.SetDebugName("Indices");
        indexBuffer.SetShadowed(true);
        indexBuffer.SetSize(3u);
        indexBuffer.SetUnpackedData(new uint[] { 0u, 1u, 2u });

        var geometry = new Geometry();
        geometry.SetVertexBuffer(0u, vertexBuffer);
        geometry.SetIndexBuffer(indexBuffer);
        geometry.SetDrawRange(PrimitiveType.TriangleList, 0u, 3u, 0u, 3u);

        var model = new Model { Name = "Original" };
        model.SetVertexBuffers([vertexBuffer]);
        model.SetIndexBuffers([indexBuffer]);
        model.SetNumGeometries(1u);
        model.SetNumGeometryLodLevels(0u, 1u);
        model.SetGeometry(0u, 0u, geometry);
        var morphData = new byte[16];
        model.SetMorphs([new ModelMorph
        {
            Name = "Morph",
            NameHash = new StringHash("Morph"),
            Buffers = new Dictionary<uint, VertexBufferMorph>
            {
                [0] = new VertexBufferMorph { DataSize = 16, MorphData = morphData }
            }
        }]);

        var clone = model.Clone("Clone");

        Assert.NotSame(vertexBuffer, clone.GetVertexBuffers()[0]);
        Assert.NotSame(indexBuffer, clone.GetIndexBuffers()[0]);
        Assert.NotSame(geometry, clone.GetGeometry(0u));
        Assert.Same(clone.GetVertexBuffers()[0], clone.GetGeometry(0u)!.GetVertexBuffer(0u));
        Assert.Same(clone.GetIndexBuffers()[0], clone.GetGeometry(0u)!.GetIndexBuffer());
        Assert.Equal("Vertices", clone.GetVertexBuffers()[0].GetDebugName());
        Assert.Equal("Indices", clone.GetIndexBuffers()[0].GetDebugName());
        Assert.NotSame(model.GetMorph(0u), clone.GetMorph(0u));
        Assert.NotSame(model.GetMorph(0u)!.Buffers[0], clone.GetMorph(0u)!.Buffers[0]);
        Assert.NotSame(model.GetMorph(0u)!.Buffers[0].MorphData, clone.GetMorph(0u)!.Buffers[0].MorphData);
    }
}
