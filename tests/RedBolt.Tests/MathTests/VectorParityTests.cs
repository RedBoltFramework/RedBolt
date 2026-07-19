using System.Runtime.CompilerServices;

namespace RedBolt.Tests.MathTests;

public class VectorParityTests
{
    [Fact]
    public void IntersectionMatchesNativeEnumShapeAndValues()
    {
        Assert.True(typeof(Intersection).IsEnum);
        Assert.Equal(typeof(int), Enum.GetUnderlyingType(typeof(Intersection)));
        Assert.Equal(0, (int)Intersection.Outside);
        Assert.Equal(1, (int)Intersection.Intersects);
        Assert.Equal(2, (int)Intersection.Inside);
    }

    [Fact]
    public void NormalizedOrDefaultUsesConfiguredFallbackAndEpsilon()
    {
        var fallback2 = new Vector2f(2.0f, 3.0f);
        var fallback3 = new Vector3f(2.0f, 3.0f, 4.0f);

        Assert.Equal(fallback2, new Vector2f(0.01f, 0.0f).NormalizedOrDefault(fallback2, 0.1f));
        Assert.Equal(fallback3, new Vector3f(0.01f, 0.0f, 0.0f).NormalizedOrDefault(fallback3, 0.1f));
        Assert.True(new Vector2f(3.0f, 4.0f).NormalizedOrDefault().ApproximatelyEquivalent(new Vector2f(0.6f, 0.8f)));
    }

    [Fact]
    public void RealVector2HasNativePodShapeStorageDefaultsCopyHashAndConversions()
    {
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<Vector2f>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<Vector2d>());
        Assert.Equal(8, Unsafe.SizeOf<Vector2f>());
        Assert.Equal(16, Unsafe.SizeOf<Vector2d>());

        var value = new Vector2f([1.0f, 2.0f]);
        var copy = value;
        var data = value.Data();
        value.X = 3.0f;

        Assert.Equal(new Vector2f(1.0f, 2.0f), copy);
        Assert.Equal(3.0f, data[0]);
        Assert.Equal(2.0f, data[1]);
        value.MutableData()[1] = 4.0f;
        Assert.Equal(new Vector2f(3.0f, 4.0f), value);
        Assert.Equal(new Vector2f(), Vector2f.Zero);
        Assert.Equal(new Vector2f(-1.0f, 0.0f), Vector2f.Left);
        Assert.Equal(new Vector2f(1.0f, 1.0f), Vector2f.One);
        Assert.Equal(1580515294u, new Vector2f(1.0f, 2.0f).ToHash());
        Assert.Equal(new Vector2f(1.0f, 2.0f), Vector2f.VectorAbs(new Vector2f(-1.0f, -2.0f)));
        Assert.Equal(new Vector2i(1, 2), new Vector2f(1.0f, 2.0f).ToIntVector2());
        Assert.Equal(new Vector3i(1, 2, 0), new Vector2f(1.0f, 2.0f).ToIntVector3());
        Assert.Equal(new Vector3f(1.0f, 2.0f, 0.0f), new Vector2f(1.0f, 2.0f).ToVector3());
        Assert.Equal(new Vector3f(1.0f, 2.0f, 3.0f), new Vector2d(1.0, 2.0).ToVector3(3.0f));
    }

    [Fact]
    public void RealVector3HasNativePodShapeStorageDefaultsCopyHashAndConversions()
    {
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<Vector3f>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<Vector3d>());
        Assert.Equal(12, Unsafe.SizeOf<Vector3f>());
        Assert.Equal(24, Unsafe.SizeOf<Vector3d>());

        var value = new Vector3f([1.0f, 2.0f, 3.0f]);
        var copy = value;
        var data = value.Data();
        value.X = 4.0f;

        Assert.Equal(new Vector3f(1.0f, 2.0f, 3.0f), copy);
        Assert.Equal(4.0f, data[0]);
        Assert.Equal(2.0f, data[1]);
        Assert.Equal(3.0f, data[2]);
        value.MutableData()[2] = 5.0f;
        Assert.Equal(new Vector3f(4.0f, 2.0f, 5.0f), value);
        Assert.Equal(new Vector3f(), Vector3f.Zero);
        Assert.Equal(new Vector3f(-1.0f, 0.0f, 0.0f), Vector3f.Left);
        Assert.Equal(new Vector3f(1.0f, 1.0f, 1.0f), Vector3f.One);
        Assert.Equal(3707772654u, new Vector3f(1.0f, 2.0f, 3.0f).ToHash());
        Assert.Equal(new Vector3f(1.0f, 2.0f, 3.0f), Vector3f.VectorAbs(new Vector3f(-1.0f, -2.0f, -3.0f)));
        Assert.Equal(new Vector2f(1.0f, 2.0f), new Vector3d(1.0, 2.0, 3.0).ToVector2());
        Assert.Equal(new Vector2i(1, 2), new Vector3f(1.9f, 2.9f, 3.9f).ToIntVector2());
        Assert.Equal(new Vector3i(1, 2, 3), new Vector3f(1.9f, 2.9f, 3.9f).ToIntVector3());

        var axis = new Vector3f(0.0f, 0.0f, 1.0f);
        var vectorAlpha = new Vector3f(1.0f, 0.0f, 0.0f);
        var vectorBeta = new Vector3f(0.0f, 1.0f, 0.0f);
        var angleAlphaToBeta = vectorAlpha.SignedAngle(vectorBeta, axis);
        var angleBetaToAlpha = vectorBeta.SignedAngle(vectorAlpha, axis);
        Assert.Equal(angleAlphaToBeta, -angleBetaToAlpha);
        Assert.True((new Quaternion(angleAlphaToBeta, axis) * vectorAlpha).ApproximatelyEquivalent(vectorBeta, 0.00001f));
        Assert.True((new Quaternion(angleBetaToAlpha, axis) * vectorBeta).ApproximatelyEquivalent(vectorAlpha, 0.00001f));
    }

    [Fact]
    public void Vector4HasNativeValueShapeStorageDefaultsCopyHashAndConversions()
    {
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<Vector4f>());
        Assert.Equal(16, Unsafe.SizeOf<Vector4f>());

        var value = new Vector4f([1.0f, 2.0f, 3.0f, 4.0f]);
        var copy = value.Clone();
        var data = value.Data();
        value.X = 5.0f;

        Assert.Equal(new Vector4f(1.0f, 2.0f, 3.0f, 4.0f), copy);
        Assert.Equal(5.0f, data[0]);
        Assert.Equal(2.0f, data[1]);
        Assert.Equal(3.0f, data[2]);
        Assert.Equal(4.0f, data[3]);
        Assert.Equal(new Vector4f(), Vector4f.Zero);
        Assert.Equal(new Vector4f(1.0f, 1.0f, 1.0f, 1.0f), Vector4f.One);
        Assert.Equal(526523093u, new Vector4f(1.0f, 2.0f, 3.0f, 4.0f).ToHash());
        Assert.Equal(new Vector2f(1.0f, 2.0f), new Vector4f(1.0f, 2.0f, 3.0f, 4.0f).ToVector2());
        Assert.Equal(new Vector2i(1, 2), new Vector4f(1.9f, 2.9f, 3.9f, 4.9f).ToIntVector2());
        Assert.Equal(new Vector3i(1, 2, 3), new Vector4f(1.9f, 2.9f, 3.9f, 4.9f).ToIntVector3());
        Assert.Equal(new Vector4f(2.0f, 3.0f, -3.0f, 5.0f),
            MathDefs.VectorCeil(new Vector4f(1.2f, 2.8f, -3.2f, 4.5f)));
    }

    [Fact]
    public void VectorFreeFunctionsMatchRbfxBehavior()
    {
        Assert.Equal(new Vector2f(2.0f, 3.0f), Vector2f.Sqrt(new Vector2f(4.0f, 9.0f)));
        Assert.Equal(new Vector3f(2.0f, 3.0f, 4.0f), Vector3f.Sqrt(new Vector3f(4.0f, 9.0f, 16.0f)));

        var random2 = Vector2f.StableRandom(new Vector2f(1.0f, 2.0f));
        var random3 = Vector3f.StableRandom(new Vector3f(1.0f, 2.0f, 3.0f));
        Assert.InRange(random2, 0.0f, 1.0f);
        Assert.InRange(random3, 0.0f, 1.0f);
        Assert.Equal(random2, Vector2f.StableRandom(new Vector2f(1.0f, 2.0f)));
    }

    [Fact]
    public void IntegerVector3HasArrayHashOrderingAndConversionParity()
    {
        var value = new Vector3i([1, 2, 3]);

        Assert.True(value < new Vector3i(1, 2, 4));
        Assert.Equal(new Vector3i(1, 2, 3).ToHash(), value.ToHash());
        Assert.Equal(new Vector3i(1, 2, 3), Vector3i.Abs(new Vector3i(-1, -2, -3)));
        Assert.Equal(new Vector4f(1, 2, 3, 4), value.ToVector4(4));
    }

    [Fact]
    public void IntegerVector2HasNativeValueShapeCopyLengthHashAndConversions()
    {
        Assert.True(typeof(Vector2i).IsValueType);
        Assert.Equal(8, Unsafe.SizeOf<Vector2i>());

        var value = new Vector2i([3, 4]);
        var copy = value;
        var data = value.Data();
        value.X = 6;

        Assert.Equal(new Vector2i(3, 4), copy);
        Assert.Equal(6, data[0]);
        Assert.Equal(4, data[1]);
        Assert.Equal(7, value.Length);
        Assert.Equal(3449077523u, new Vector2i(1, 2).ToHash());
        Assert.Equal(new Vector3i(1, 2, 3), new Vector2i(1, 2).ToIntVector3(3));
        Assert.Equal(new Vector3f(1, 2, 3), new Vector2i(1, 2).ToVector3(3));
        Assert.Equal(new Vector4f(1, 2, 3, 4), new Vector2i(1, 2).ToVector4(3, 4));
    }

    [Fact]
    public void IntegerVector3HasNativeValueShapeCopyLengthAndHash()
    {
        Assert.True(typeof(Vector3i).IsValueType);
        Assert.Equal(12, Unsafe.SizeOf<Vector3i>());

        var value = new Vector3i([2, 3, 6]);
        var copy = value;
        var data = value.Data();
        value.X = 4;

        Assert.Equal(new Vector3i(2, 3, 6), copy);
        Assert.Equal(4, data[0]);
        Assert.Equal(3, data[1]);
        Assert.Equal(6, data[2]);
        Assert.Equal(7, value.Length);
        Assert.Equal(4216901971u, new Vector3i(1, 2, 3).ToHash());
    }

    [Fact]
    public void IntegerVectorTemplatesSupportUnsignedNativeScalarSemantics()
    {
        Assert.Equal(uint.MaxValue, BaseIntegerVector2<uint>.Left.X);
        Assert.Equal(uint.MaxValue, BaseIntegerVector3<uint>.Back.Z);
        Assert.Equal("1 2", new BaseIntegerVector2<uint>(1, 2).ToString());
        Assert.Equal(new Vector3i(1, 2, 3), new BaseIntegerVector2<uint>(1, 2).ToIntVector3(3));
    }

    [Fact]
    public void VectorArrayConstructorsAndQuaternionZeroMatchRbfx()
    {
        Assert.Equal(new Vector3f(1, 2, 3), new Vector3f([1, 2, 3]));
        Assert.Equal(new Quaternion(0, 0, 0, 0), Quaternion.ZERO);
    }
}
