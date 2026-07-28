namespace RedBolt.Tests;

using RedBolt.Graphics;
using RedBolt.Math;

public sealed class DebugRendererTests
{
    [Fact]
    public void DebugRendererIsNotCopyable()
    {
        Assert.False(typeof(ICloneable).IsAssignableFrom(typeof(DebugRenderer)));
    }

    [Fact]
    public void SetViewCopiesFrustumValue()
    {
        using var renderer = new DebugRenderer();
        var camera = new Camera();
        var cameraFrustum = camera.GetFrustum();

        renderer.SetView(camera);
        var rendererVertex = renderer.GetFrustum().Vertices[0];
        cameraFrustum.Vertices[0] = new Vector3f(100.0f, 200.0f, 300.0f);

        Assert.NotSame(cameraFrustum, renderer.GetFrustum());
        Assert.Equal(rendererVertex, renderer.GetFrustum().Vertices[0]);
    }

    [Fact]
    public void SetViewDoesNotOwnCamera()
    {
        using var renderer = new DebugRenderer();
        var cameraReference = SetViewAndReleaseCamera(renderer);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.False(cameraReference.TryGetTarget(out _));
        renderer.AddLine2D(Vector2f.Zero, Vector2f.One, Color.White);
        Assert.False(renderer.HasContent());
    }

    [Fact]
    public void EndFrameClearsAccumulatedGeometry()
    {
        using var renderer = new DebugRenderer();
        renderer.AddLine(Vector3f.Zero, Vector3f.One, Color.White);
        renderer.AddTriangle(Vector3f.Zero, Vector3f.Right, Vector3f.Up, Color.White, false);

        Assert.True(renderer.HasContent());

        renderer.HandleEndFrame();

        Assert.False(renderer.HasContent());
    }

    [Fact]
    public void DebugLineStoresCppFields()
    {
        var start = new Vector3f(1.0f, 2.0f, 3.0f);
        var end = new Vector3f(4.0f, 5.0f, 6.0f);
        var line = new DebugLine(start, end, 0x12345678u);

        Assert.Equal(start, line.Start);
        Assert.Equal(end, line.End);
        Assert.Equal(0x12345678u, line.Color);
    }

    [Fact]
    public void DebugTriangleStoresCppFields()
    {
        var v1 = new Vector3f(1.0f, 2.0f, 3.0f);
        var v2 = new Vector3f(4.0f, 5.0f, 6.0f);
        var v3 = new Vector3f(7.0f, 8.0f, 9.0f);
        var triangle = new DebugTriangle(v1, v2, v3, 0x87654321u);

        Assert.Equal(v1, triangle.VertexA);
        Assert.Equal(v2, triangle.VertexB);
        Assert.Equal(v3, triangle.VertexC);
        Assert.Equal(0x87654321u, triangle.Color);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference<Camera> SetViewAndReleaseCamera(DebugRenderer renderer)
    {
        var camera = new Camera();
        renderer.SetView(camera);
        return new WeakReference<Camera>(camera);
    }
}
