using RedBolt;

namespace RedBolt.Tests;

public sealed class FrustumTests
{
    [Fact]
    public void Perspective_frustum_classifies_points_spheres_and_boxes()
    {
        var frustum = new Frustum();
        frustum.Define(new Vector3f(1.0f, 1.0f, 1.0f), new Vector3f(10.0f, 10.0f, 10.0f));

        Assert.Equal(Frustum.NumFrustumPlanes, frustum.Planes.Length);
        Assert.Equal(Frustum.NumFrustumVertices, frustum.Vertices.Length);
        Assert.Equal(Intersection.Inside, frustum.IsInside(new Vector3f(0.0f, 0.0f, 5.0f)));
        Assert.Equal(Intersection.Outside, frustum.IsInside(new Vector3f(0.0f, 0.0f, -1.0f)));
        Assert.Equal(Intersection.Outside, frustum.IsInside(new Vector3f(20.0f, 0.0f, 5.0f)));
        Assert.Equal(0.0f, frustum.Distance(new Vector3f(0.0f, 0.0f, 5.0f)));
        Assert.True(frustum.Distance(new Vector3f(0.0f, 0.0f, -1.0f)) > 0.0f);

        var insideSphere = new Sphere(new Vector3f(0.0f, 0.0f, 5.0f), 0.25f);
        var intersectingSphere = new Sphere(new Vector3f(0.0f, 0.0f, 1.0f), 1.0f);
        Assert.Equal(Intersection.Inside, frustum.IsInside(insideSphere));
        Assert.Equal(Intersection.Intersects, frustum.IsInside(intersectingSphere));
        Assert.Equal(Intersection.Inside, frustum.IsInsideFast(insideSphere));

        var insideBox = new BoundingBox(new Vector3f(-0.25f, -0.25f, 4.0f), new Vector3f(0.25f, 0.25f, 6.0f));
        var outsideBox = new BoundingBox(new Vector3f(-1.0f, -1.0f, -2.0f), new Vector3f(1.0f, 1.0f, -1.0f));
        Assert.Equal(Intersection.Inside, frustum.IsInside(insideBox));
        Assert.Equal(Intersection.Outside, frustum.IsInside(outsideBox));
        Assert.Equal(Intersection.Inside, frustum.IsInsideFast(insideBox));
    }

    [Fact]
    public void Orthographic_definition_and_transform_update_planes()
    {
        var frustum = new Frustum();
        frustum.DefineOrtho(2.0f, 1.0f, 1.0f, 1.0f, 3.0f);

        Assert.Equal(Intersection.Inside, frustum.IsInside(new Vector3f(0.0f, 0.0f, 2.0f)));
        Assert.Equal(Intersection.Outside, frustum.IsInside(new Vector3f(2.0f, 0.0f, 2.0f)));

        var translated = frustum.Transformed(Matrix3x4.FromTranslation(new Vector3f(5.0f, 0.0f, 0.0f)));

        Assert.Equal(Intersection.Inside, translated.IsInside(new Vector3f(5.0f, 0.0f, 2.0f)));
        Assert.Equal(Intersection.Outside, translated.IsInside(new Vector3f(0.0f, 0.0f, 2.0f)));
    }

    [Fact]
    public void Clone_and_projected_rect_preserve_frustum_geometry()
    {
        Assert.False(typeof(Frustum).IsValueType);

        var frustum = new Frustum();
        frustum.Define(new Vector3f(1.0f, 1.0f, 1.0f), new Vector3f(2.0f, 2.0f, 2.0f));
        var copy = new Frustum(frustum);
        var clone = (Frustum)frustum.Clone();

        Assert.NotSame(frustum.Planes, copy.Planes);
        Assert.NotSame(frustum.Vertices, copy.Vertices);
        Assert.NotSame(frustum.Planes, clone.Planes);
        Assert.NotSame(frustum.Vertices, clone.Vertices);
        Assert.Equal(frustum.Vertices, clone.Vertices);
        Assert.Equal(frustum.Planes, clone.Planes);

        var copiedVertex = copy.Vertices[0];
        var copiedPlane = copy.Planes[0];
        frustum.Vertices[0] = new Vector3f(10.0f, 20.0f, 30.0f);
        frustum.Planes[0].D += 1.0f;
        Assert.Equal(copiedVertex, copy.Vertices[0]);
        Assert.Equal(copiedPlane, copy.Planes[0]);

        var projected = clone.Projected(Matrix4.Identity);

        Assert.True(projected.Defined);
        Assert.True(projected.Min.X <= projected.Max.X);
        Assert.True(projected.Min.Y <= projected.Max.Y);
    }
}
