namespace RedBolt.Tests.MathTests;

using RedBolt.Math;

public class GeometryParityTests
{
    [Fact]
    public void PlaneIsReferenceFreeValueTypeWithIndependentCopies()
    {
        Assert.True(typeof(Plane).IsValueType);
        Assert.True(typeof(Plane).IsAssignableTo(typeof(ICloneable)));

        var plane = new Plane(Vector3f.Up, new Vector3f(0.0f, 2.0f, 0.0f));
        var copy = plane;
        plane.D += 1.0f;

        Assert.Equal(-2.0f, copy.D);
        Assert.Equal(new Vector4f(Vector3f.Up, -2.0f), copy.ToVector4());
        Assert.Equal(new Matrix3x4(
            1.0f, 0.0f, 0.0f, 0.0f,
            0.0f, -1.0f, 0.0f, 0.0f,
            0.0f, 0.0f, 1.0f, 0.0f), Plane.Up.ReflectionMatrix);
        Assert.Equal(default, new Plane());
    }

    [Fact]
    public void PlaneIntersectionsMatchRbfxGeometry()
    {
        var x = new Plane(Vector3f.Right, new Vector3f(2, 0, 0));
        var y = new Plane(Vector3f.Up, new Vector3f(0, 3, 0));
        var z = new Plane(Vector3f.Forward, new Vector3f(0, 0, 4));

        var line = x.Intersect(y);
        Assert.True(line.Origin.ApproximatelyEquivalent(new Vector3f(2, 3, 0)));
        Assert.True(x.Intersect(y, z).ApproximatelyEquivalent(new Vector3f(2, 3, 4)));
    }

    [Fact]
    public void BoundingBoxDistancePaddingAndMergeMatchRbfx()
    {
        var box = new BoundingBox(new Vector3f(-1, -1, -1), new Vector3f(1, 1, 1));

        Assert.Equal(8.0f, box.Volume);
        Assert.Equal(-1.0f, box.SignedDistanceToPoint(Vector3f.Zero));
        Assert.Equal(1.0f, box.DistanceToPoint(new Vector3f(2, 0, 0)));
        Assert.Equal(new Vector3f(-2, -3, -4), box.Padded(new Vector3f(1, 2, 3)).Min);
        Assert.Equal(new Vector3f(3, 1, 1), box.Merged(new Vector3f(3, 0, 0)).Max);
    }

    [Fact]
    public void RayBoxHitIncludesSurfaceNormal()
    {
        var ray = new Ray(new Vector3f(-2, 0, 0), Vector3f.Right);
        var box = new BoundingBox(new Vector3f(-1, -1, -1), new Vector3f(1, 1, 1));

        var hit = ray.HitDistanceAndNormal(box);

        Assert.Equal(1.0f, hit.Distance);
        Assert.Equal(Vector3f.Left, hit.Normal);
    }

    [Fact]
    public void SphereIntersectionAndParametricPointsMatchRbfx()
    {
        var lhs = new Sphere(Vector3f.Zero, 2.0f);
        var rhs = new Sphere(new Vector3f(3, 0, 0), 2.0f);

        var circle = lhs.Intersect(rhs, out var distance);

        Assert.True(circle.IsValid);
        Assert.Equal(1.5f, distance, 0.00001f);
        Assert.Equal(new Vector3f(1.5f, 0, 0), circle.Center);
        Assert.Equal(2.0f, lhs.GetLocalPoint(0, 0).Y, 0.00001f);
        Assert.Equal(1.0f, lhs.Distance(new Vector3f(3, 0, 0)));
    }

    [Fact]
    public void MathDefsVariableLengthAndZigzagRoundTrip()
    {
        Span<byte> encoded = stackalloc byte[10];
        var length = MathDefs.EncodeVariableLength(123456UL, encoded);
        ulong decoded = 0;
        uint offset = 0;
        for (uint i = 0; i < length; ++i)
            MathDefs.DecodeVariableLength(ref decoded, ref offset, encoded[(int)i]);

        Assert.Equal(123456UL, decoded);
        Assert.Equal(21u, offset);
        Assert.Equal(-123456, MathDefs.ZigzagDecode(MathDefs.ZigzagEncode(-123456)));
        Assert.Equal(2.0f, MathDefs.Average(new[] { 1.0f, 2.0f, 3.0f }));
    }
}
