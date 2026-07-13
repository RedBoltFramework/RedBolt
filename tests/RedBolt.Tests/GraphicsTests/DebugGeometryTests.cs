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

    [Fact]
    public void DebugRenderer_accumulates_and_clears_geometry()
    {
        var debugRenderer = new DebugRenderer();

        debugRenderer.AddLine(Vector3f.Zero, Vector3f.One, new Color(1.0f, 0.0f, 0.0f));
        debugRenderer.AddPolygon(Vector3f.Zero, Vector3f.One, Vector3f.One, Vector3f.Zero, 0x12345678u, depthTest: false);

        Assert.True(debugRenderer.HasContent());

        debugRenderer.HandleEndFrame();

        Assert.False(debugRenderer.HasContent());
    }

    [Fact]
    public void DebugRenderer_set_view_preserves_camera_transforms()
    {
        var camera = new Camera();
        var debugRenderer = new DebugRenderer();

        debugRenderer.SetView(camera);

        Assert.Equal(camera.GetView(), debugRenderer.GetView());
        Assert.Equal(camera.GetProjection(), debugRenderer.GetProjection());
        Assert.Equal(camera.GetFrustum(), debugRenderer.GetFrustum());
    }

    [Fact]
    public void DebugRenderer_shape_helpers_accumulate_geometry()
    {
        var debugRenderer = new DebugRenderer();

        debugRenderer.AddCross(Vector3f.Zero, 2.0f, Color.White);
        debugRenderer.AddQuad(Vector3f.Zero, 2.0f, 2.0f, Color.White);
        debugRenderer.AddCircle(Vector3f.Zero, Vector3f.Up, 1.0f, Color.White, steps: 8);

        Assert.True(debugRenderer.HasContent());
    }
}
