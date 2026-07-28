namespace RedBolt.Tests.GraphicsTests;

using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.RenderAPI;

public sealed class VertexBufferTests
{
    [Fact]
    public void Buffer_types_match_cpp_noncopyable_reference_classes()
    {
        Assert.False(typeof(VertexBuffer).IsSealed);
        Assert.False(typeof(ICloneable).IsAssignableFrom(typeof(VertexBuffer)));
        Assert.False(typeof(DynamicVertexBuffer).IsSealed);
        Assert.True(typeof(RedObject).IsAssignableFrom(typeof(DynamicVertexBuffer)));
        Assert.False(typeof(ICloneable).IsAssignableFrom(typeof(DynamicVertexBuffer)));
    }

    [Fact]
    public void Vertex_elements_are_layouted_and_unpacked_in_vertex_order()
    {
        var elements = new[]
        {
            new VertexElement(VertexElementType.Vector3, VertexElementSemantic.Position),
            new VertexElement(VertexElementType.UByte4Norm, VertexElementSemantic.Color),
            new VertexElement(VertexElementType.Vector2, VertexElementSemantic.TexCoord)
        };
        var vertexBuffer = new VertexBuffer();
        vertexBuffer.SetShadowed(true);
        vertexBuffer.SetSize(2, elements);
        Assert.NotNull(vertexBuffer.GetRawBuffer());

        vertexBuffer.SetUnpackedData(new[]
        {
            new Vector4f(1, 2, 3, 1), new Vector4f(0.5f, 0.25f, 1, 0), new Vector4f(4, 5, 0, 0),
            new Vector4f(6, 7, 8, 1), new Vector4f(0, 1, 0.5f, 0), new Vector4f(9, 10, 0, 0)
        });

        Assert.Equal(24u, vertexBuffer.GetVertexSize());
        Assert.Equal(0u, vertexBuffer.GetElementOffset(VertexElementSemantic.Position));
        Assert.Equal(12u, vertexBuffer.GetElementOffset(VertexElementSemantic.Color));
        Assert.Equal(16u, vertexBuffer.GetElementOffset(VertexElementSemantic.TexCoord));
        Assert.True(vertexBuffer.HasElement(VertexElementType.Vector3, VertexElementSemantic.Position));
        Assert.Equal(48, vertexBuffer.GetShadowData().Length);

        var unpacked = vertexBuffer.GetUnpackedData();
        Assert.Equal(6, unpacked.Count);
        Assert.True(unpacked[0].ApproximatelyEquivalent(new Vector4f(1, 2, 3, 1)));
        Assert.True(unpacked[1].ApproximatelyEquivalent(new Vector4f(128 / 255.0f, 64 / 255.0f, 1, 0)));
        Assert.True(unpacked[2].ApproximatelyEquivalent(new Vector4f(4, 5, 0, 0)));
    }

    [Fact]
    public void Legacy_masks_and_static_element_helpers_match_cpp_layout_rules()
    {
        var elements = VertexBuffer.GetElements((uint)(VertexMask.Position | VertexMask.Normal | VertexMask.Tangent));
        VertexBuffer.UpdateOffsets(elements);

        Assert.Equal(3, elements.Count);
        Assert.Equal(40u, VertexBuffer.GetVertexSize(elements));
        Assert.True(VertexBuffer.HasElement(elements, VertexElementType.Vector3, VertexElementSemantic.Normal));
        Assert.Equal(12u, VertexBuffer.GetElementOffset(elements, VertexElementType.Vector3, VertexElementSemantic.Normal));
        Assert.Null(VertexBuffer.GetElement(elements, VertexElementType.Vector2, VertexElementSemantic.Normal));
    }

    [Fact]
    public void SetSize_replaces_same_sized_shadow_storage_like_cpp_create()
    {
        using var vertexBuffer = new VertexBuffer(DeviceObjectFlag.Headless);
        vertexBuffer.SetShadowed(true);
        Assert.True(vertexBuffer.SetSize(1, VertexMask.Position));
        var original = vertexBuffer.GetShadowData();

        Assert.True(vertexBuffer.SetSize(1, VertexMask.Position));

        Assert.NotSame(original, vertexBuffer.GetShadowData());
    }

    [Fact]
    public void Raw_buffer_inheritance_is_forwarded_through_the_managed_composition()
    {
        using var vertexBuffer = new VertexBuffer(DeviceObjectFlag.Headless);
        vertexBuffer.SetShadowed(true);
        Assert.True(vertexBuffer.SetSize(2, VertexMask.Position));
        var rawBuffer = vertexBuffer.GetRawBuffer()!;

        Assert.Equal(rawBuffer.GetBufferType(), vertexBuffer.GetBufferType());
        Assert.Equal(rawBuffer.GetSize(), vertexBuffer.GetSize());
        Assert.Equal(rawBuffer.GetStride(), vertexBuffer.GetStride());
        Assert.Equal(rawBuffer.GetFlags(), vertexBuffer.GetFlags());
        Assert.Equal(rawBuffer.IsLocked(), vertexBuffer.IsLocked());
        Assert.Equal(rawBuffer.IsShadowed(), vertexBuffer.IsShadowed());
        Assert.Same(rawBuffer.GetShadowData(), vertexBuffer.GetShadowDataShared());
        Assert.Same(rawBuffer.GetHandle(), vertexBuffer.GetHandle());
        Assert.Same(rawBuffer.GetRenderDevice(), vertexBuffer.GetRenderDevice());

        vertexBuffer.Resolve();
        vertexBuffer.Invalidate();
        Assert.Equal(rawBuffer.IsDataLost(), vertexBuffer.IsDataLost());
        vertexBuffer.ClearDataLost();
        Assert.False(vertexBuffer.IsDataLost());
        vertexBuffer.Destroy();
        Assert.Empty(vertexBuffer.GetShadowData());
    }

    [Fact]
    public void Static_pack_unpack_and_shuffle_handle_strides_and_missing_elements()
    {
        var element = new VertexElement(VertexElementType.Vector3, VertexElementSemantic.Position) { Offset = 4 };
        var packed = new byte[28];
        VertexBuffer.PackVertexData(new[] { new Vector4f(1, 2, 3, 1), new Vector4f(4, 5, 6, 1) },
            sourceStride: 16, packed, destStride: 12, element, start: 0, count: 2);

        var unpacked = new Vector4f[2];
        VertexBuffer.UnpackVertexData(packed, sourceStride: 12, element, start: 0, count: 2, unpacked, destStride: 16);
        Assert.True(unpacked[0].ApproximatelyEquivalent(new Vector4f(1, 2, 3, 1)));
        Assert.True(unpacked[1].ApproximatelyEquivalent(new Vector4f(4, 5, 6, 1)));

        var sourceElements = new[] { new VertexElement(VertexElementType.Vector3, VertexElementSemantic.Position) };
        var destElements = new[]
        {
            new VertexElement(VertexElementType.Vector3, VertexElementSemantic.Normal),
            new VertexElement(VertexElementType.Vector3, VertexElementSemantic.Position)
        };
        var shuffled = new Vector4f[4];
        VertexBuffer.ShuffleUnpackedVertexData(2, new[] { new Vector4f(1, 2, 3, 1), new Vector4f(4, 5, 6, 1) },
            sourceElements, shuffled, destElements);

        Assert.Equal(Vector4f.Zero, shuffled[0]);
        Assert.Equal(new Vector4f(1, 2, 3, 1), shuffled[1]);
        Assert.Equal(Vector4f.Zero, shuffled[2]);
        Assert.Equal(new Vector4f(4, 5, 6, 1), shuffled[3]);
    }

    [Fact]
    public void Dynamic_vertex_buffer_grows_and_commits_shadow_data()
    {
        var dynamicBuffer = new DynamicVertexBuffer();
        Assert.True(dynamicBuffer.Initialize(1,
            new[] { new VertexElement(VertexElementType.Vector3, VertexElementSemantic.Position) }));

        var first = dynamicBuffer.AddVertices(1);
        VertexBuffer.PackVertexData(new[] { new Vector4f(1, 2, 3, 1) }, sourceStride: 16, dest: first.Data.Span,
            destStride: 12, element: new VertexElement(VertexElementType.Vector3, VertexElementSemantic.Position),
            start: 0, count: 1);
        dynamicBuffer.AddVertices(2, new byte[24]);
        dynamicBuffer.Commit();

        Assert.Equal(0u, first.StartVertex);
        Assert.Equal(3u, dynamicBuffer.GetVertexCount());
        Assert.Equal(36, dynamicBuffer.GetVertexBuffer().GetShadowData().Length);
    }
}
