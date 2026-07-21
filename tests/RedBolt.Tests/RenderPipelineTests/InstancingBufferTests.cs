namespace RedBolt.Tests;

public sealed class InstancingBufferTests
{
    [Fact]
    public void Disabled_settings_leave_buffer_uninitialized()
    {
        var buffer = new InstancingBuffer();

        Assert.False(buffer.IsEnabled());
        Assert.Null(buffer.GetVertexBuffer());
    }

    [Fact]
    public void Enabled_settings_create_cpp_instancing_elements()
    {
        var buffer = new InstancingBuffer();
        buffer.SetSettings(new InstancingBufferSettings
        {
            EnableInstancing = true,
            FirstInstancingTexCoord = 4,
            NumInstancingTexCoords = 3,
            StepRate = 2
        });

        var vertexBuffer = Assert.IsType<VertexBuffer>(buffer.GetVertexBuffer());
        Assert.True(buffer.IsEnabled());
        Assert.Equal(3, vertexBuffer.GetElements().Count);
        Assert.Collection(vertexBuffer.GetElements(),
            element => AssertElement(element, 4),
            element => AssertElement(element, 5),
            element => AssertElement(element, 6));
    }

    [Fact]
    public void Composition_tracks_indices_and_copies_elements()
    {
        var buffer = new InstancingBuffer();
        buffer.SetSettings(new InstancingBufferSettings
        {
            EnableInstancing = true,
            FirstInstancingTexCoord = 4,
            NumInstancingTexCoords = 2
        });

        buffer.Begin();
        Assert.Equal(0, buffer.GetNextInstanceIndex());
        Assert.Equal(0, buffer.AddInstance());
        buffer.SetElements(new byte[checked((int)(2 * InstancingBuffer.ElementStride))], 0, 2);
        Assert.Equal(1, buffer.GetNextInstanceIndex());
        Assert.Equal(1, buffer.AddInstance());
        Assert.Equal(2, buffer.GetNextInstanceIndex());

        buffer.Begin();
        Assert.Equal(0, buffer.GetNextInstanceIndex());
    }

    private static void AssertElement(VertexElement element, byte index)
    {
        Assert.Equal(VertexElementType.Vector4, element.Type);
        Assert.Equal(VertexElementSemantic.TexCoord, element.Semantic);
        Assert.Equal(index, element.Index);
        Assert.Equal(2u, element.StepRate);
    }
}
