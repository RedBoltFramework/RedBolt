namespace RedBolt.Tests.GraphicsTests;

public sealed class DebugGeometryTests
{
    [Fact]
    public void DebugLine_constructor_preserves_values()
    {
        var line = new DebugLine(new Vector3f(1.0f, 2.0f, 3.0f), new Vector3f(4.0f, 5.0f, 6.0f), 0x12345678);

        Assert.Equal(new Vector3f(1.0f, 2.0f, 3.0f), line.Start);
        Assert.Equal(new Vector3f(4.0f, 5.0f, 6.0f), line.End);
        Assert.Equal(0x12345678u, line.Color);
    }

    [Fact]
    public void DebugTriangle_constructor_preserves_values()
    {
        var triangle = new DebugTriangle(
            new Vector3f(1.0f, 2.0f, 3.0f),
            new Vector3f(4.0f, 5.0f, 6.0f),
            new Vector3f(7.0f, 8.0f, 9.0f),
            0x12345678);

        Assert.Equal(new Vector3f(1.0f, 2.0f, 3.0f), triangle.VertexA);
        Assert.Equal(new Vector3f(4.0f, 5.0f, 6.0f), triangle.VertexB);
        Assert.Equal(new Vector3f(7.0f, 8.0f, 9.0f), triangle.VertexC);
        Assert.Equal(0x12345678u, triangle.Color);
    }
}
