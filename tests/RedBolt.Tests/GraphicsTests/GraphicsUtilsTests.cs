using Xunit;

namespace RedBolt.Tests;

public sealed class GraphicsUtilsTests
{
    [Fact]
    public void Initialize_input_layout_copies_vertex_buffer_elements()
    {
        var vertexBuffer = new VertexBuffer();
        vertexBuffer.SetSize(3, new[]
        {
            new VertexElement(VertexElementType.Vector3, VertexElementSemantic.Position),
            new VertexElement(VertexElementType.Vector2, VertexElementSemantic.TexCoord, 0)
        });

        var desc = new InputLayoutDesc();
        GraphicsUtils.InitializeInputLayout(desc, new VertexBuffer?[] { vertexBuffer, null });

        Assert.Equal(2u, desc.Size);
        Assert.Equal(new InputLayoutElementDesc(0, vertexBuffer.GetVertexSize(), 0, 0,
            VertexElementType.Vector3, VertexElementSemantic.Position, 0), desc.Elements[0]);
        Assert.Equal(new InputLayoutElementDesc(0, vertexBuffer.GetVertexSize(), 12, 0,
            VertexElementType.Vector2, VertexElementSemantic.TexCoord, 0), desc.Elements[1]);
    }

    [Fact]
    public void Initialize_input_layout_and_primitive_type_copies_geometry_state()
    {
        var vertexBuffer = new VertexBuffer();
        vertexBuffer.SetSize(3, new[]
        {
            new VertexElement(VertexElementType.Vector3, VertexElementSemantic.Position)
        });

        var geometry = new Geometry();
        Assert.True(geometry.SetVertexBuffer(0, vertexBuffer));
        Assert.True(geometry.SetDrawRange(PrimitiveType.LineList, 0, 0, 0, 3));

        var desc = new GraphicsPipelineStateDesc();
        GraphicsUtils.InitializeInputLayoutAndPrimitiveType(desc, geometry);

        Assert.Equal(PrimitiveType.LineList, desc.PrimitiveType);
        Assert.Equal(1u, desc.InputLayout.Size);
        Assert.Equal(new InputLayoutElementDesc(0, vertexBuffer.GetVertexSize(), 0, 0,
            VertexElementType.Vector3, VertexElementSemantic.Position, 0), desc.InputLayout.Elements[0]);
    }
}
