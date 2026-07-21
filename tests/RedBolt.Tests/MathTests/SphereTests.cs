using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using RedBolt;

namespace RedBolt.Tests;

public sealed class SphereTests
{
    [Fact]
    public void Shapes_and_defaults_match_native()
    {
        Assert.True(typeof(Circle).IsValueType);
        Assert.True(typeof(Sphere).IsValueType);
        Assert.True(typeof(ICloneable).IsAssignableFrom(typeof(Sphere)));
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<Circle>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<Sphere>());
        Assert.Equal(28, Marshal.SizeOf<Circle>());
        Assert.Equal(16, Marshal.SizeOf<Sphere>());
        Assert.Equal(0, Marshal.OffsetOf<Circle>(nameof(Circle.Center)).ToInt32());
        Assert.Equal(12, Marshal.OffsetOf<Circle>(nameof(Circle.Normal)).ToInt32());
        Assert.Equal(24, Marshal.OffsetOf<Circle>(nameof(Circle.Radius)).ToInt32());
        Assert.Equal(0, Marshal.OffsetOf<Sphere>(nameof(Sphere.Center)).ToInt32());
        Assert.Equal(12, Marshal.OffsetOf<Sphere>(nameof(Sphere.Radius)).ToInt32());

        var circle = new Circle();
        Assert.False(circle.IsValid);
        Assert.Equal(Vector3f.Zero, circle.Center);
        Assert.Equal(Vector3f.Zero, circle.Normal);
        Assert.Equal(float.NegativeInfinity, circle.Radius);

        var sphere = new Sphere();
        Assert.False(sphere.Defined);
        Assert.Equal(Vector3f.Zero, sphere.Center);
        Assert.Equal(float.NegativeInfinity, sphere.Radius);
    }

    [Fact]
    public void Sphere_copies_are_independent()
    {
        var source = new Sphere(new Vector3f(1.0f, 2.0f, 3.0f), 4.0f);
        var assignment = source;
        var copy = new Sphere(source);
        var clone = source.Clone();
        source.Clear();

        var expected = new Sphere(new Vector3f(1.0f, 2.0f, 3.0f), 4.0f);
        Assert.Equal(expected, assignment);
        Assert.Equal(expected, copy);
        Assert.Equal(expected, clone);
    }

    [Fact]
    public void Empty_vertex_definition_preserves_native_state()
    {
        var constructed = new Sphere(Array.Empty<Vector3f>());
        Assert.True(constructed.Defined);
        Assert.Equal(Vector3f.Zero, constructed.Center);
        Assert.Equal(0.0f, constructed.Radius);

        var sphere = new Sphere(new Vector3f(1.0f, 2.0f, 3.0f), 4.0f);
        sphere.Define(ReadOnlySpan<Vector3f>.Empty);
        Assert.Equal(new Sphere(new Vector3f(1.0f, 2.0f, 3.0f), 4.0f), sphere);

        sphere.Define(new[] { new Vector3f(-1.0f, 0.0f, 0.0f), new Vector3f(1.0f, 0.0f, 0.0f) });
        Assert.Equal(new Sphere(Vector3f.Zero, 1.0f), sphere);
    }

    [Fact]
    public void Bounding_box_classification_matches_native_boundaries()
    {
        var box = new BoundingBox(-1.0f, 1.0f);
        Assert.Equal(Intersection.Inside, new Sphere(Vector3f.Zero, 2.0f).IsInside(box));
        Assert.Equal(Intersection.Intersects, new Sphere(Vector3f.Zero, 1.5f).IsInside(box));

        var touchingBox = new BoundingBox(
            new Vector3f(1.0f, -0.1f, -0.1f), new Vector3f(2.0f, 0.1f, 0.1f));
        var sphere = new Sphere(Vector3f.Zero, 1.0f);
        Assert.Equal(Intersection.Outside, sphere.IsInside(touchingBox));
        Assert.Equal(Intersection.Outside, sphere.IsInsideFast(touchingBox));
    }

    [Fact]
    public void Equal_sphere_merge_preserves_native_zero_distance_result()
    {
        var sphere = new Sphere(Vector3f.Zero, 1.0f);
        sphere.Merge(new Sphere(Vector3f.Zero, 1.0f));

        Assert.True(float.IsNaN(sphere.Center.X));
        Assert.True(float.IsNaN(sphere.Center.Y));
        Assert.True(float.IsNaN(sphere.Center.Z));
        Assert.True(float.IsNaN(sphere.Radius));
    }

    [Fact]
    public void Sphere_intersection_and_circle_behavior_match_native()
    {
        var sphere = new Sphere(Vector3f.Zero, 2.0f);
        var circle = sphere.Intersect(new Sphere(new Vector3f(3.0f, 0.0f, 0.0f), 2.0f), out var distance);

        Assert.True(circle.IsValid);
        Assert.True(MathDefs.Equals(1.5f, distance));
        Assert.True(circle.Center.ApproximatelyEquivalent(new Vector3f(1.5f, 0.0f, 0.0f)));
        Assert.Equal(Vector3f.Right, circle.Normal);
        Assert.True(MathDefs.Equals(MathF.Sqrt(7.0f) * 0.5f, circle.Radius));

        var invalid = new Circle();
        invalid.Center = new Vector3f(1.0f, 2.0f, 3.0f);
        Assert.Equal(invalid.Center, invalid.GetPoint(Vector3f.Right));
    }

    [Fact]
    public void Point_queries_and_spherical_coordinates_match_native()
    {
        var sphere = new Sphere(new Vector3f(1.0f, 2.0f, 3.0f), 2.0f);
        Assert.Equal(Intersection.Inside, sphere.IsInside(new Vector3f(1.0f, 2.0f, 4.0f)));
        Assert.Equal(Intersection.Outside, sphere.IsInside(new Vector3f(1.0f, 2.0f, 5.0f)));
        Assert.Equal(0.0f, sphere.Distance(new Vector3f(1.0f, 2.0f, 4.0f)));
        Assert.Equal(1.0f, sphere.Distance(new Vector3f(1.0f, 2.0f, 6.0f)));
        Assert.True(sphere.GetLocalPoint(0.0f, 90.0f).ApproximatelyEquivalent(new Vector3f(0.0f, 0.0f, 2.0f)));
        Assert.True(sphere.GetPoint(0.0f, 90.0f).ApproximatelyEquivalent(new Vector3f(1.0f, 2.0f, 5.0f)));
    }
}
