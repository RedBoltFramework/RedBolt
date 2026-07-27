using System.Runtime.InteropServices;
using RedBolt;

using RedBolt.Math;
namespace RedBolt.Tests;

public sealed class RayTests
{
    [Fact]
    public void Value_shapes_match_native()
    {
        Assert.True(typeof(DistanceAndNormal).IsValueType);
        Assert.False(typeof(DistanceAndNormal).IsDefined(typeof(System.Runtime.CompilerServices.IsReadOnlyAttribute), false));
        Assert.Equal(0, Marshal.OffsetOf<DistanceAndNormal>(nameof(DistanceAndNormal.Normal)).ToInt32());
        Assert.Equal(12, Marshal.OffsetOf<DistanceAndNormal>(nameof(DistanceAndNormal.Distance)).ToInt32());

        Assert.True(typeof(Ray).IsValueType);
        Assert.True(typeof(ICloneable).IsAssignableFrom(typeof(Ray)));
        Assert.Equal(0, Marshal.OffsetOf<Ray>(nameof(Ray.Origin)).ToInt32());
        Assert.Equal(12, Marshal.OffsetOf<Ray>(nameof(Ray.Direction)).ToInt32());
    }

    [Fact]
    public void Defaults_and_copies_are_independent()
    {
        var hit = new DistanceAndNormal();
        Assert.Equal(Vector3f.Up, hit.Normal);
        Assert.Equal(float.PositiveInfinity, hit.Distance);

        var source = new Ray(new Vector3f(1.0f, 2.0f, 3.0f), new Vector3f(0.0f, 2.0f, 0.0f));
        var copy = new Ray(source);
        var clone = source.Clone();
        source.Define(Vector3f.Zero, Vector3f.Right);

        Assert.Equal(new Vector3f(1.0f, 2.0f, 3.0f), copy.Origin);
        Assert.Equal(Vector3f.Up, copy.Direction);
        Assert.Equal(copy, clone);
    }

    [Fact]
    public void Hit_distance_matches_native_plane_test()
    {
        var ray = new Ray(new Vector3f(0.0f, 2.0f, 0.0f), new Vector3f(0.0f, -1.0f, 0.0f));
        var plane = new Plane(new Vector3f(0.0f, 1.0f, 0.0f), new Vector3f(0.0f, -1.0f, 0.0f));

        Assert.Equal(3.0f, ray.HitDistance(plane), 1e-6f);
    }

    [Fact]
    public void Bounding_box_hit_returns_native_distance_and_normal()
    {
        var box = new BoundingBox(-Vector3f.One, Vector3f.One);
        var outsideRay = new Ray(new Vector3f(-2.0f, 0.0f, 0.0f), Vector3f.Right);
        var outsideHit = outsideRay.HitDistanceAndNormal(box);
        Assert.Equal(1.0f, outsideHit.Distance);
        Assert.Equal(new Vector3f(-1.0f, 0.0f, 0.0f), outsideHit.Normal);
        Assert.Equal(outsideHit.Distance, outsideRay.HitDistance(box));

        var insideRay = new Ray(Vector3f.Zero, Vector3f.Right);
        var insideHit = insideRay.HitDistanceAndNormal(box);
        Assert.Equal(0.0f, insideHit.Distance);
        Assert.Equal(-Vector3f.Right, insideHit.Normal);
    }

    [Fact]
    public void Transform_preserves_scaled_non_normalized_direction()
    {
        var transform = new Matrix3x4(
            2.0f, 0.0f, 0.0f, 5.0f,
            0.0f, 3.0f, 0.0f, 6.0f,
            0.0f, 0.0f, 4.0f, 7.0f);
        var transformed = new Ray(new Vector3f(1.0f, 2.0f, 3.0f), Vector3f.Right).Transformed(transform);

        Assert.Equal(new Vector3f(7.0f, 12.0f, 19.0f), transformed.Origin);
        Assert.Equal(new Vector3f(2.0f, 0.0f, 0.0f), transformed.Direction);
    }

    [Fact]
    public void Geometry_hits_match_nonindexed_and_indexed_native_paths()
    {
        const int stride = 20;
        Span<byte> vertices = stackalloc byte[stride * 3];
        Write(vertices, 0, new Vector3f(0.0f, 0.0f, 0.0f));
        Write(vertices, 12, new Vector2f(0.0f, 0.0f));
        Write(vertices, stride, new Vector3f(0.0f, 1.0f, 0.0f));
        Write(vertices, stride + 12, new Vector2f(0.0f, 1.0f));
        Write(vertices, stride * 2, new Vector3f(1.0f, 0.0f, 0.0f));
        Write(vertices, stride * 2 + 12, new Vector2f(1.0f, 0.0f));

        var ray = new Ray(new Vector3f(0.25f, 0.25f, -1.0f), Vector3f.Forward);
        var nonIndexedDistance = ray.HitDistance(vertices, stride, 0u, 3u, out var normal, out var uv, 12u);
        Assert.Equal(1.0f, nonIndexedDistance);
        Assert.Equal(new Vector3f(0.0f, 0.0f, -1.0f), normal);
        Assert.Equal(new Vector2f(0.25f, 0.25f), uv);

        Span<byte> indices16 = stackalloc byte[sizeof(ushort) * 3];
        Write(indices16, 0, (ushort)0);
        Write(indices16, sizeof(ushort), (ushort)1);
        Write(indices16, sizeof(ushort) * 2, (ushort)2);
        Assert.Equal(1.0f, ray.HitDistance(vertices, stride, indices16, sizeof(ushort), 0u, 3u, out normal, out uv, 12u));
        Assert.Equal(new Vector2f(0.25f, 0.25f), uv);

        Span<byte> indices32 = stackalloc byte[sizeof(uint) * 3];
        Write(indices32, 0, 0u);
        Write(indices32, sizeof(uint), 1u);
        Write(indices32, sizeof(uint) * 2, 2u);
        Assert.Equal(1.0f, ray.HitDistance(vertices, stride, indices32, sizeof(uint), 0u, 3u, out normal, out uv, 12u));
        Assert.Equal(new Vector2f(0.25f, 0.25f), uv);
    }

    private static void Write<T>(Span<byte> destination, int offset, T value) where T : struct =>
        MemoryMarshal.Write(destination[offset..], in value);
}
