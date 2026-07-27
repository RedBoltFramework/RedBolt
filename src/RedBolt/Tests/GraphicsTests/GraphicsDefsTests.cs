namespace RedBolt.Tests;

public sealed class GraphicsDefsTests
{
    [Fact]
    public void Graphics_enums_preserve_cpp_values()
    {
        Assert.Equal(7, (int)GeometryType.MaxGeometryTypes);
        Assert.Equal(7, (int)GeometryType.StaticNoInstancing);
        Assert.Equal(5, (int)CubeMapFace.NegativeZ);
        Assert.Equal(6, (int)CubeMapFace.MaxCubeMapFaces);
        Assert.Equal(3, (int)LockState.Scratch);
        Assert.Equal(13, (int)LegacyVertexElement.ObjectIndex);
        Assert.Equal(14, (int)LegacyVertexElement.MaxLegacyVertexElements);
        Assert.Equal(4, (int)CubeMapLayout.Blender);
        Assert.Equal(2, (int)RenderSurfaceUpdateMode.UpdateAlways);
        Assert.Equal(7, (int)FaceCameraMode.AxisAngle);
        Assert.Equal(5, (int)ShadowQuality.BlurVsm);
        Assert.Equal(15u, (uint)MaterialQuality.Max);
        Assert.Equal(0x2000u, (uint)VertexMask.ObjectIndex);
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
        var first = new VertexElement(VertexElementType.Vector3, VertexElementSemantic.Position);
        var second = new VertexElement(VertexElementType.Vector3, VertexElementSemantic.Position);
        second.Offset = 4;

        Assert.Equal(first, second);
        Assert.NotEqual(first.ToHash(), second.ToHash());
    }

    [Fact]
    public void Vertex_element_default_constructor_matches_cpp_defaults()
    {
        var element = new VertexElement();

        Assert.Equal(VertexElementType.Vector3, element.Type);
        Assert.Equal(VertexElementSemantic.Position, element.Semantic);
        Assert.Equal(0, element.Index);
        Assert.Equal(0u, element.StepRate);
        Assert.Equal(0u, element.Offset);
    }

    [Fact]
    public void Vertex_element_copy_has_independent_value_semantics()
    {
        var source = new VertexElement(VertexElementType.Vector4, VertexElementSemantic.Tangent, 1, 2)
        {
            Offset = 16
        };

        var copy = source;
        copy.Offset = 32;

        Assert.Equal(16u, source.Offset);
        Assert.Equal(32u, copy.Offset);
    }

    [Fact]
    public void Vertex_element_is_reference_free_value_type()
    {
        Assert.True(typeof(VertexElement).IsValueType);
        Assert.False(System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<VertexElement>());
    }
}
