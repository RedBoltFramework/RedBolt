using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using RedBolt;

namespace RedBolt.Tests;

public sealed class BoundingBoxTests
{
    [Fact]
    public void BoundingBox_is_pod_value_type_with_native_layout()
    {
        Assert.True(typeof(BoundingBox).IsValueType);
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<BoundingBox>());
        Assert.Equal(32, Marshal.SizeOf<BoundingBox>());
    }

    [Fact]
    public void Default_constructor_creates_undefined_box()
    {
        var box = new BoundingBox();

        Assert.False(box.Defined);
        Assert.Equal(new Vector3f(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity), box.Min);
        Assert.Equal(new Vector3f(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity), box.Max);
    }

    [Fact]
    public void Copy_is_independent_value()
    {
        var box = new BoundingBox(-1.0f, 1.0f);
        var copy = box;

        copy.Min.X = -2.0f;

        Assert.Equal(-1.0f, box.Min.X);
        Assert.Equal(-2.0f, copy.Min.X);
        Assert.True(box == new BoundingBox(-1.0f, 1.0f));
        Assert.True(box != copy);
    }

    [Fact]
    public void Padded_preserves_native_undefined_and_degenerate_results()
    {
        Assert.False(new BoundingBox().Padded(Vector3f.One).Defined);

        var box = new BoundingBox(-1.0f, 1.0f);
        Assert.False(box.Padded(new Vector3f(-2.0f, -2.0f, -2.0f)).Defined);
    }

    [Fact]
    public void Tangent_sphere_intersection_uses_native_strictness()
    {
        var box = new BoundingBox(-1.0f, 1.0f);
        var sphere = new Sphere(new Vector3f(2.0f, 0.0f, 0.0f), 1.0f);

        Assert.Equal(Intersection.Intersects, box.IsInside(sphere));
        Assert.Equal(Intersection.Outside, box.IsInsideFast(sphere));
    }
}
