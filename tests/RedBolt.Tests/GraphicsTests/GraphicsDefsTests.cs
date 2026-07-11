namespace RedBolt.Tests;

public sealed class GraphicsDefsTests
{
    [Fact]
    public void Graphics_enums_preserve_cpp_values()
    {
        Assert.Equal(7, (int)GeometryType.MaxGeometryTypes);
        Assert.Equal(5, (int)CubeMapFace.NegativeZ);
        Assert.Equal(6, (int)CubeMapFace.MaxCubeMapFaces);
        Assert.Equal(3, (int)LockState.Scratch);
        Assert.Equal(13, (int)LegacyVertexElement.ObjectIndex);
        Assert.Equal(14, (int)LegacyVertexElement.MaxLegacyVertexElements);
        Assert.Equal(5, (int)ShadowQuality.BlurVsm);
    }

    [Fact]
    public void Graphics_constants_preserve_shader_names_and_vertex_layout()
    {
        Assert.Equal(new StringHash("AmbientStartColor"), GraphicsDefs.VspAmbientStartColor);
        Assert.Equal(new StringHash("CameraPosPS"), GraphicsDefs.PspCameraPos);
        Assert.Equal(new Vector3f(1.0f / 3.0f, 1.0f / 3.0f, 1.0f / 3.0f), GraphicsDefs.DotScale);
        Assert.Equal(14, GraphicsDefs.LegacyVertexElements.Length);
        Assert.Equal(VertexElementType.Vector4, GraphicsDefs.LegacyVertexElements[10].Type);
        Assert.Equal(16u, GraphicsDefs.ElementTypeSizes[4]);
    }

    [Fact]
    public void Vertex_element_equality_ignores_offset_and_hash_includes_it()
    {
        var first = new VertexElement(VertexElementType.Vector3, VertexElementSemantic.Position, offset: 0);
        var second = new VertexElement(VertexElementType.Vector3, VertexElementSemantic.Position, offset: 4);

        Assert.Equal(first, second);
        Assert.NotEqual(first.ToHash(), second.ToHash());
    }
}
