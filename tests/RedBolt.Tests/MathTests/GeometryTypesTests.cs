using RedBolt;

namespace RedBolt.Tests;

public sealed class GeometryTypesTests
{
    [Fact]
    public void Rect_merge_and_containment_work()
    {
        var rect = new Rect();
        rect.Merge(new Vector2(0.0f, 0.0f));
        rect.Merge(new Vector2(2.0f, 2.0f));
        Assert.Equal(Intersection.Inside, rect.IsInside(new Vector2(1.0f, 1.0f)));
        Assert.Equal(new Vector2(2.0f, 2.0f), rect.Size);
    }

    [Fact]
    public void IntRect_containment_respects_exclusive_max()
    {
        var r = new IntRect(0, 0, 10, 10);
        Assert.Equal(Intersection.Inside, r.IsInside(new IntVector2(9, 9)));
        Assert.Equal(Intersection.Outside, r.IsInside(new IntVector2(10, 10)));
    }

    [Fact]
    public void Color_converts_to_vector4_and_packed_uint()
    {
        var c = new Color(0.25f, 0.5f, 1.0f, 1.0f);
        Assert.Equal(new Vector4(0.25f, 0.5f, 1.0f, 1.0f), c.ToVector4());
        Assert.NotEqual(0u, c.ToUInt());
    }

    [Fact]
    public void BoundingBox_and_sphere_containment_work()
    {
        var box = new BoundingBox(new Vector3(-1.0f, -1.0f, -1.0f), new Vector3(1.0f, 1.0f, 1.0f));
        var sphere = new Sphere(Vector3.Zero, 1.0f);
        Assert.Equal(Intersection.Inside, box.IsInside(Vector3.Zero));
        Assert.Equal(Intersection.Inside, box.IsInside(sphere));
        Assert.False(box.IsInside(new Vector3(2.0f, 0.0f, 0.0f)) == Intersection.Inside);
    }

    [Fact]
    public void Ray_hit_distance_works()
    {
        var box = new BoundingBox(new Vector3(-1.0f, -1.0f, -1.0f), new Vector3(1.0f, 1.0f, 1.0f));
        var sphere = new Sphere(Vector3.Zero, 1.0f);
        var ray = new Ray(new Vector3(0.0f, 0.0f, -5.0f), Vector3.Forward);
        Assert.True(MathDefs.ApproximatelyEquivalent(ray.HitDistance(box), 4.0f));
        Assert.True(MathDefs.ApproximatelyEquivalent(ray.HitDistance(sphere), 4.0f));
    }

    [Fact]
    public void Plane_ray_intersection_works()
    {
        var plane = new Plane(Vector3.Up, Vector3.Zero);
        var downRay = new Ray(new Vector3(0.0f, 1.0f, 0.0f), Vector3.Down);
        Assert.True(MathDefs.ApproximatelyEquivalent(downRay.HitDistance(plane), 1.0f));
    }

    [Fact]
    public void Matrix4_transform_and_inverse_work()
    {
        var m4 = new Matrix3x4(new Vector3(1.0f, 2.0f, 3.0f), Quaternion.Identity, new Vector3(2.0f, 3.0f, 4.0f)).Matrix4;
        var transformed = m4 * Vector3.One;
        Assert.True(transformed.ApproximatelyEquivalent(new Vector3(3.0f, 5.0f, 7.0f)));
        Assert.True((m4.Inverted * transformed).ApproximatelyEquivalent(Vector3.One));
    }

    [Fact]
    public void Plane_matrix3x4_transform_works()
    {
        var plane = new Plane(Vector3.Up, Vector3.Zero);
        var moved = plane.Transformed(new Matrix3x4(new Vector3(0.0f, 2.0f, 0.0f), Quaternion.Identity, 1.0f));
        Assert.True(MathDefs.ApproximatelyEquivalent(moved.Distance(new Vector3(0.0f, 2.0f, 0.0f)), 0.0f));
    }

    [Fact]
    public void Matrix2_inverse_roundtrips()
    {
        var m = new Matrix2(2.0f, 0.0f, 0.0f, 4.0f);
        Assert.True(((m * m.Inverse) * Vector2.One).ApproximatelyEquivalent(Vector2.One));
    }
}
