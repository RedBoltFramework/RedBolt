using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using RedBolt.Math;
namespace RedBolt.Tests.MathTests;

public class PlaneTests
{
    [Fact]
    public void Plane_is_sequential_reference_free_value_type()
    {
        Assert.True(typeof(Plane).IsValueType);
        Assert.Equal(LayoutKind.Sequential, typeof(Plane).StructLayoutAttribute!.Value);
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<Plane>());
        Assert.Equal(28, Unsafe.SizeOf<Plane>());

        var plane = new Plane();
        Assert.Equal(Vector3f.Zero, plane.Normal);
        Assert.Equal(Vector3f.Zero, plane.AbsNormal);
        Assert.Equal(0.0f, plane.D);
    }

    [Fact]
    public void Copy_construction_and_clone_are_independent_values()
    {
        var source = new Plane(new Vector3f(0.0f, 1.0f, 0.0f), new Vector3f(0.0f, 3.0f, 0.0f));
        var copy = new Plane(source);
        var clone = source.Clone();
        var interfaceClone = (Plane)((ICloneable)source).Clone();

        copy.Normal.X = 4.0f;
        clone.D = 5.0f;

        Assert.Equal(Vector3f.Up, source.Normal);
        Assert.Equal(-3.0f, source.D);
        AssertPlane(source, interfaceClone);
    }

    [Fact]
    public void Constructors_and_geometry_operations_match_native()
    {
        var fromNormal = new Plane(new Vector3f(0.0f, 2.0f, 0.0f), new Vector3f(0.0f, 3.0f, 0.0f));
        var fromVertices = new Plane(
            new Vector3f(0.0f, 3.0f, 0.0f),
            new Vector3f(0.0f, 3.0f, 1.0f),
            new Vector3f(1.0f, 3.0f, 0.0f));
        var fromVector = new Plane(new Vector4f(0.0f, 1.0f, 0.0f, -3.0f));

        AssertPlane(fromNormal, fromVertices);
        AssertPlane(fromNormal, fromVector);
        Assert.Equal(new Vector4f(0.0f, 1.0f, 0.0f, -3.0f), fromNormal.ToVector4());
        Assert.Equal(2.0f, fromNormal.Distance(new Vector3f(1.0f, 5.0f, 2.0f)));
        AssertVector(new Vector3f(1.0f, 3.0f, 2.0f), fromNormal.Project(new Vector3f(1.0f, 5.0f, 2.0f)));
        AssertVector(Vector3f.Up, fromNormal.Reflect(Vector3f.Down));
        AssertVector(new Vector3f(0.0f, 3.0f, 0.0f), fromNormal.GetPoint());
    }

    [Fact]
    public void Transform_reflection_and_transformed_match_native()
    {
        var source = new Plane(new Vector3f(0.0f, 1.0f, 0.0f), Vector3f.Zero);
        Assert.Equal(new Matrix3x4(
            1.0f, 0.0f, 0.0f, 0.0f,
            0.0f, -1.0f, 0.0f, 0.0f,
            0.0f, 0.0f, 1.0f, 0.0f), source.ReflectionMatrix);

        var matrix3 = Matrix3.Identity;
        var transformed3 = source.Transformed(matrix3);
        var mutable3 = source;
        mutable3.Transform(matrix3);
        AssertPlane(source, transformed3);
        AssertPlane(source, mutable3);

        var matrix3x4 = Matrix3x4.Identity;
        var transformed3x4 = source.Transformed(matrix3x4);
        var mutable3x4 = source;
        mutable3x4.Transform(matrix3x4);
        AssertPlane(source, transformed3x4);
        AssertPlane(source, mutable3x4);

        var matrix4 = Matrix4.Identity;
        var transformed4 = source.Transformed(matrix4);
        var mutable4 = source;
        mutable4.Transform(matrix4);
        AssertPlane(source, transformed4);
        AssertPlane(source, mutable4);
    }

    [Fact]
    public void Native_get_point_test_is_preserved()
    {
        var plane = new Plane(new Vector3f(0, 0, 1), new Vector3f(0, 0, 1));
        var point = plane.GetPoint();
        AssertVector(new Vector3f(0, 0, 1), point);
    }

    [Fact]
    public void Native_two_planes_intersect_tests_are_preserved()
    {
        var testsData = new[]
        {
            (Point: new Vector3f(1, 2, 3), N1: new Vector3f(1, 0, 0), N2: new Vector3f(0, 1, 0)),
            (Point: new Vector3f(1, 2, 3), N1: new Vector3f(0, 1, 0), N2: new Vector3f(0, 0, 1)),
            (Point: new Vector3f(1, 2, 3), N1: new Vector3f(0, 0, 1), N2: new Vector3f(1, 0, 0))
        };

        foreach (var data in testsData)
        {
            var planeA = new Plane(data.N1, data.Point);
            var planeB = new Plane(data.N2, data.Point);
            var ray = planeA.Intersect(planeB);
            AssertClose(0.0f, planeA.Distance(ray.Origin));
            AssertClose(0.0f, planeB.Distance(ray.Origin));
            AssertClose(0.0f, ray.Direction.DotProduct(planeA.Normal));
            AssertClose(0.0f, ray.Direction.DotProduct(planeB.Normal));
        }
    }

    [Fact]
    public void Native_three_planes_intersect_tests_are_preserved()
    {
        var testsData = new[]
        {
            (Point: new Vector3f(1, 2, 3), N1: new Vector3f(1, 0, 0), N2: new Vector3f(0, 1, 0), N3: new Vector3f(0, 0, 1)),
            (Point: new Vector3f(1, 2, 3), N1: new Vector3f(1, 0, 0), N2: new Vector3f(0, 1, 0), N3: new Vector3f(1, 1, 1)),
            (Point: new Vector3f(0, 0, 0), N1: new Vector3f(-1, 0, 0), N2: new Vector3f(0, 1, 0), N3: new Vector3f(1, 1, 1))
        };

        foreach (var data in testsData)
        {
            var planeA = new Plane(data.N1, data.Point);
            var planeB = new Plane(data.N2, data.Point);
            var planeC = new Plane(data.N3, data.Point);
            var point = planeA.Intersect(planeB, planeC);

            AssertClose(0.0f, planeA.Distance(point));
            AssertClose(0.0f, planeB.Distance(point));
            AssertClose(0.0f, planeC.Distance(point));
            AssertVector(data.Point, point);
        }
    }

    private static void AssertPlane(Plane expected, Plane actual)
    {
        AssertVector(expected.Normal, actual.Normal);
        AssertVector(expected.AbsNormal, actual.AbsNormal);
        AssertClose(expected.D, actual.D);
    }

    private static void AssertVector(Vector3f expected, Vector3f actual)
        => Assert.True(actual.ApproximatelyEquivalent(expected, 1e-6f), $"Expected {expected}, got {actual}");

    private static void AssertClose(float expected, float actual)
        => Assert.True(MathF.Abs(expected - actual) <= 1e-6f, $"Expected {expected}, got {actual}");
}
