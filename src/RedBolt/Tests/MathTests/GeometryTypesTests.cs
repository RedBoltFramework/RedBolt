using RedBolt;

using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Math;
namespace RedBolt.Tests;

public sealed class GeometryTypesTests
{
    [Fact]
    public void Rect_merge_and_containment_work()
    {
        var rect = new Rect();
        rect.Merge(new Vector2f(0.0f, 0.0f));
        rect.Merge(new Vector2f(2.0f, 2.0f));
        Assert.Equal(Intersection.Inside, rect.IsInside(new Vector2f(1.0f, 1.0f)));
        Assert.Equal(new Vector2f(2.0f, 2.0f), rect.Size);
        Assert.Equal(new Vector4f(0.0f, 0.0f, 2.0f, 2.0f), rect.ToVector4());
    }

    [Fact]
    public void Rect_define_scalar_ops_and_clip_work()
    {
        var rect = new Rect([1.0f, 2.0f, 5.0f, 6.0f]);
        var copy = new Rect();
        copy.Define(rect);
        Assert.Equal(rect, copy);

        copy.Define(new Vector2f(2.0f, 3.0f), new Vector2f(6.0f, 7.0f));
        Assert.Equal(new Vector2f(2.0f, 3.0f), copy.Min);
        Assert.Equal(new Vector2f(6.0f, 7.0f), copy.Max);

        copy.Define(new Vector2f(4.0f, 5.0f));
        Assert.Equal(new Vector2f(4.0f, 5.0f), copy.Min);
        Assert.Equal(new Vector2f(4.0f, 5.0f), copy.Max);

        var scaled = rect * 2.0f;
        Assert.Equal(new Rect(2.0f, 4.0f, 10.0f, 12.0f), scaled);
        Assert.Equal(rect, scaled / 2.0f);

        rect.Clip(new Rect(2.0f, 1.0f, 4.0f, 8.0f));
        Assert.Equal(new Rect(2.0f, 2.0f, 4.0f, 6.0f), rect);
        Assert.NotEqual(0u, rect.ToHash());
    }

    [Fact]
    public void IntRect_containment_respects_exclusive_max()
    {
        var r = new IntRect(0, 0, 10, 10);
        Assert.Equal(Intersection.Inside, r.IsInside(new Vector2i(9, 9)));
        Assert.Equal(Intersection.Outside, r.IsInside(new Vector2i(10, 10)));
        Assert.True(r.Contains(new Vector2i(0, 0)));
    }

    [Fact]
    public void IntRect_rect_containment_and_iterator_work_row_by_row()
    {
        var rect = new IntRect(1, 2, 3, 4);
        var inside = new IntRect(1, 2, 2, 3);
        var intersecting = new IntRect(0, 2, 2, 3);
        var outside = new IntRect(5, 5, 6, 6);

        Assert.Equal(Intersection.Inside, rect.IsInside(inside));
        Assert.Equal(Intersection.Intersects, rect.IsInside(intersecting));
        Assert.Equal(Intersection.Outside, rect.IsInside(outside));

        var iterator = rect.Begin();
        var end = rect.End();
        var points = new List<Vector2i>();
        while (iterator != end)
        {
            points.Add(iterator.Current);
            iterator++;
        }

        Assert.Equal(
        [
            new Vector2i(1, 2),
            new Vector2i(2, 2),
            new Vector2i(1, 3),
            new Vector2i(2, 3)
        ], points);
    }

    [Fact]
    public void Color_converts_to_vector4_and_packed_uint()
    {
        var c = new Color(0.25f, 0.5f, 1.0f, 1.0f);
        Assert.Equal(new Vector4f(0.25f, 0.5f, 1.0f, 1.0f), c.ToVector4());
        Assert.NotEqual(0u, c.ToUInt());
    }

    [Fact]
    public void Vector4_constructors_indexer_and_projection_helpers_work()
    {
        var vector = new Vector4f(new Vector2f(1.0f, 2.0f), new Vector2f(3.0f, 4.0f));
        var copied = new Vector4f([1.0f, 2.0f, 3.0f, 4.0f]);

        Assert.Equal(vector, copied);
        Assert.Equal(1.0f, vector[0]);
        Assert.Equal(4.0f, vector[3]);

        copied[2] = 6.0f;
        Assert.Equal(6.0f, copied.Z);
        Assert.True(MathDefs.Equals(vector.LengthSquared, 30.0f));
        Assert.True(MathDefs.Equals(vector.ProjectOntoAxis(Vector3f.Right), 1.0f));
        Assert.Equal(new Vector3f(1.0f, 2.0f, 3.0f), vector.ToVector3());
        Assert.Equal(new Vector3i(1, 2, 3), vector.ToIntVector3());
        Assert.NotEqual(0u, vector.ToHash());
        Assert.False(vector.IsInf);
        Assert.False(vector.IsNaN);
    }

    [Fact]
    public void Vector4_free_helpers_work_componentwise()
    {
        var lhs = new Vector4f(1.2f, 2.8f, -3.2f, 4.5f);
        var rhs = new Vector4f(3.2f, 1.8f, -1.2f, 7.5f);
        var t = new Vector4f(0.5f, 0.25f, 0.5f, 0.0f);

        Assert.Equal(new Vector4f(2.2f, 2.55f, -2.2f, 4.5f), MathDefs.VectorLerp(lhs, rhs, t));
        Assert.Equal(new Vector4f(1.2f, 1.8f, -3.2f, 4.5f), MathDefs.VectorMin(lhs, rhs));
        Assert.Equal(new Vector4f(3.2f, 2.8f, -1.2f, 7.5f), MathDefs.VectorMax(lhs, rhs));
        Assert.Equal(new Vector4f(1.0f, 2.0f, -4.0f, 4.0f), MathDefs.VectorFloor(lhs));
        Assert.Equal(new Vector4f(1.0f, 3.0f, -3.0f, 5.0f), MathDefs.VectorRound(lhs));
        Assert.Equal(new Vector4f(2.0f, 3.0f, -3.0f, 5.0f), MathDefs.VectorCeil(lhs));
    }

    [Fact]
    public void Vector2_and_intvector2_follow_rbfx_helper_surface()
    {
        var vector = new Vector2f([3.0f, 4.0f]);
        var fallback = new Vector2f(7.0f, 8.0f);
        var tiny = new Vector2f(0.0f, 0.0f);

        Assert.Equal(5.0f, vector.Length);
        Assert.Equal(new Vector2f(2.0f, 2.5f), Vector2f.Lerp(new Vector2f(1.0f, 2.0f), new Vector2f(3.0f, 4.0f), new Vector2f(0.5f, 0.25f)));
        Assert.Equal(new Vector2f(1.2f, 1.6f), vector.ReNormalized(1.0f, 2.0f));
        Assert.Equal(fallback, tiny.ReNormalized(1.0f, 2.0f, fallback));
        Assert.NotEqual(0u, vector.ToHash());

        var intVector = new Vector2i([2, 3]);
        Assert.True(intVector < new Vector2i(2, 4));
        Assert.True(new Vector2i(2, 4) > intVector);
        Assert.Equal(new Vector3f(2.0f, 3.0f, 0.0f), intVector.ToVector3());
        Assert.NotEqual(0u, intVector.ToHash());
    }

    [Fact]
    public void BoundingBox_and_sphere_containment_work()
    {
        var box = new BoundingBox(new Vector3f(-1.0f, -1.0f, -1.0f), new Vector3f(1.0f, 1.0f, 1.0f));
        var sphere = new Sphere(Vector3f.Zero, 1.0f);
        Assert.Equal(Intersection.Inside, box.IsInside(Vector3f.Zero));
        Assert.Equal(Intersection.Inside, box.IsInside(sphere));
        Assert.False(box.IsInside(new Vector3f(2.0f, 0.0f, 0.0f)) == Intersection.Inside);
    }

    [Fact]
    public void Ray_hit_distance_works()
    {
        var box = new BoundingBox(new Vector3f(-1.0f, -1.0f, -1.0f), new Vector3f(1.0f, 1.0f, 1.0f));
        var sphere = new Sphere(Vector3f.Zero, 1.0f);
        var ray = new Ray(new Vector3f(0.0f, 0.0f, -5.0f), Vector3f.Forward);
        Assert.True(MathDefs.Equals(ray.HitDistance(box), 4.0f));
        Assert.True(MathDefs.Equals(ray.HitDistance(sphere), 4.0f));
    }

    [Fact]
    public void Plane_ray_intersection_works()
    {
        var plane = new Plane(Vector3f.Up, Vector3f.Zero);
        var downRay = new Ray(new Vector3f(0.0f, 1.0f, 0.0f), Vector3f.Down);
        Assert.True(MathDefs.Equals(downRay.HitDistance(plane), 1.0f));
    }

    [Fact]
    public void Matrix4_transform_and_inverse_work()
    {
        var m4 = new Matrix3x4(new Vector3f(1.0f, 2.0f, 3.0f), Quaternion.IDENTITY, new Vector3f(2.0f, 3.0f, 4.0f)).Matrix4;
        var transformed = m4 * Vector3f.One;
        Assert.True(transformed.ApproximatelyEquivalent(new Vector3f(3.0f, 5.0f, 7.0f)));
        Assert.True((m4.Inverse * transformed).ApproximatelyEquivalent(Vector3f.One));
    }

    [Fact]
    public void Plane_matrix3x4_transform_works()
    {
        var plane = new Plane(Vector3f.Up, Vector3f.Zero);
        var moved = plane.Transformed(new Matrix3x4(new Vector3f(0.0f, 2.0f, 0.0f), Quaternion.IDENTITY, 1.0f));
        Assert.True(MathDefs.Equals(moved.Distance(new Vector3f(0.0f, 2.0f, 0.0f)), 0.0f));
    }

    [Fact]
    public void Matrix2_inverse_roundtrips()
    {
        var m = new Matrix2(2.0f, 0.0f, 0.0f, 4.0f);
        Assert.True(((m * m.Inverse) * Vector2f.One).ApproximatelyEquivalent(Vector2f.One));
    }

    [Fact]
    public void Matrix2_preserves_native_value_layout_and_copy_semantics()
    {
        Assert.Equal(16, System.Runtime.CompilerServices.Unsafe.SizeOf<Matrix2>());
        Assert.False(System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<Matrix2>());
        Assert.Equal(Matrix2.Identity, new Matrix2());

        ReadOnlySpan<float> values = [1.0f, 2.0f, 3.0f, 4.0f];
        var matrix = new Matrix2(values);
        var copy = matrix;
        copy.M00 = 5.0f;

        Assert.Equal(1.0f, matrix.M00);
        Assert.Equal(values.ToArray(), matrix.Data.ToArray());
        Assert.Equal(new Matrix2(1.0f, 3.0f, 2.0f, 4.0f), matrix.Transpose);

        var source = new[] { 1.0f, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f, 7.0f, 8.0f };
        var destination = new float[source.Length];
        Matrix2.BulkTranspose(destination, source, 2);
        Assert.Equal(new[] { 1.0f, 3.0f, 2.0f, 4.0f, 5.0f, 7.0f, 6.0f, 8.0f }, destination);
    }

    [Fact]
    public void Matrix3x4_decompose_roundtrips_transform_components()
    {
        var matrix = new Matrix3x4(
            new Vector3f(1.0f, 2.0f, 3.0f),
            new Quaternion(45.0f, Vector3f.Up),
            new Vector3f(2.0f, 3.0f, 4.0f));

        matrix.Decompose(out var translation, out var rotation, out var scale);

        Assert.True(translation.ApproximatelyEquivalent(new Vector3f(1.0f, 2.0f, 3.0f)));
        Assert.True(rotation.ApproximatelyEquivalent(new Quaternion(45.0f, Vector3f.Up), 0.001f));
        Assert.True(scale.ApproximatelyEquivalent(new Vector3f(2.0f, 3.0f, 4.0f)));
    }

    [Fact]
    public void Transform_from_matrix_lerp_inverse_and_multiply_work()
    {
        var lhs = new Transform(
            new Vector3f(1.0f, 2.0f, 3.0f),
            new Quaternion(30.0f, Vector3f.Up),
            new Vector3f(2.0f, 2.0f, 2.0f));
        var rhs = new Transform(
            new Vector3f(5.0f, 6.0f, 7.0f),
            new Quaternion(60.0f, Vector3f.Right),
            new Vector3f(4.0f, 4.0f, 4.0f));

        var fromMatrix = Transform.FromMatrix3x4(lhs.ToMatrix3x4());
        Assert.True(fromMatrix.Position.ApproximatelyEquivalent(lhs.Position));
        Assert.True(fromMatrix.Rotation.ApproximatelyEquivalent(lhs.Rotation, 0.001f));
        Assert.True(fromMatrix.Scale.ApproximatelyEquivalent(lhs.Scale));

        var midpoint = lhs.Lerp(rhs, 0.5f);
        Assert.True(midpoint.Position.ApproximatelyEquivalent(new Vector3f(3.0f, 4.0f, 5.0f)));
        Assert.True(midpoint.Scale.ApproximatelyEquivalent(new Vector3f(3.0f, 3.0f, 3.0f)));

        var invertible = new Transform(
            new Vector3f(1.0f, 2.0f, 3.0f),
            new Quaternion(30.0f, Vector3f.Up),
            new Vector3f(2.0f, 2.0f, 2.0f));
        var transformedPoint = invertible * Vector3f.One;
        Assert.True((invertible.Inverse() * transformedPoint).ApproximatelyEquivalent(Vector3f.One, 0.001f));

        var composed = lhs * rhs;
        var expected = lhs.ToMatrix3x4() * rhs.ToMatrix3x4();
        Assert.True(composed.ToMatrix3x4().ApproximatelyEquivalent(expected, 0.001f));

        var rotated = lhs * Quaternion.IDENTITY;
        Assert.True(rotated.ApproximatelyEquivalent(lhs.Rotation));
    }

    [Fact]
    public void Transform_types_are_pod_values_with_native_defaults()
    {
        Assert.False(System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<Transform>());
        Assert.False(System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<DoubleTransform>());
        Assert.False(System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<AnimationKeyFrame>());
        Assert.Equal(40, System.Runtime.InteropServices.Marshal.SizeOf<Transform>());
        Assert.Equal(56, System.Runtime.InteropServices.Marshal.SizeOf<DoubleTransform>());
        Assert.Equal(44, System.Runtime.InteropServices.Marshal.SizeOf<AnimationKeyFrame>());
        Assert.Equal(0, System.Runtime.InteropServices.Marshal.OffsetOf<Transform>(nameof(Transform.Position)).ToInt32());
        Assert.Equal(12, System.Runtime.InteropServices.Marshal.OffsetOf<Transform>(nameof(Transform.Rotation)).ToInt32());
        Assert.Equal(28, System.Runtime.InteropServices.Marshal.OffsetOf<Transform>(nameof(Transform.Scale)).ToInt32());
        Assert.Equal(0, System.Runtime.InteropServices.Marshal.OffsetOf<DoubleTransform>(nameof(DoubleTransform.Position)).ToInt32());
        Assert.Equal(24, System.Runtime.InteropServices.Marshal.OffsetOf<DoubleTransform>(nameof(DoubleTransform.Rotation)).ToInt32());
        Assert.Equal(40, System.Runtime.InteropServices.Marshal.OffsetOf<DoubleTransform>(nameof(DoubleTransform.Scale)).ToInt32());
        Assert.Equal(0, System.Runtime.InteropServices.Marshal.OffsetOf<AnimationKeyFrame>(nameof(AnimationKeyFrame.Position)).ToInt32());
        Assert.Equal(12, System.Runtime.InteropServices.Marshal.OffsetOf<AnimationKeyFrame>(nameof(AnimationKeyFrame.Rotation)).ToInt32());
        Assert.Equal(28, System.Runtime.InteropServices.Marshal.OffsetOf<AnimationKeyFrame>(nameof(AnimationKeyFrame.Scale)).ToInt32());
        Assert.Equal(40, System.Runtime.InteropServices.Marshal.OffsetOf<AnimationKeyFrame>(nameof(AnimationKeyFrame.Time)).ToInt32());

        var transform = new Transform();
        Assert.Equal(Vector3f.Zero, transform.Position);
        Assert.Equal(Quaternion.IDENTITY, transform.Rotation);
        Assert.Equal(Vector3f.One, transform.Scale);
        var transformCopy = transform;
        transformCopy.Position = Vector3f.One;
        Assert.Equal(Vector3f.Zero, transform.Position);

        var doubleTransform = new DoubleTransform();
        Assert.Equal(Vector3d.Zero, doubleTransform.Position);
        Assert.Equal(Quaternion.IDENTITY, doubleTransform.Rotation);
        Assert.Equal(Vector3f.One, doubleTransform.Scale);
        var doubleTransformCopy = doubleTransform;
        doubleTransformCopy.Position = Vector3d.One;
        Assert.Equal(Vector3d.Zero, doubleTransform.Position);

        var keyFrame = new AnimationKeyFrame();
        Assert.Equal(Vector3f.Zero, keyFrame.Position);
        Assert.Equal(Quaternion.IDENTITY, keyFrame.Rotation);
        Assert.Equal(Vector3f.One, keyFrame.Scale);
        Assert.Equal(0.0f, keyFrame.Time);
        var keyFrameCopy = keyFrame;
        keyFrameCopy.Position = Vector3f.One;
        keyFrameCopy.Time = 1.0f;
        Assert.Equal(Vector3f.Zero, keyFrame.Position);
        Assert.Equal(0.0f, keyFrame.Time);
    }

    [Fact]
    public void Double_vector_and_double_transform_helpers_work()
    {
        var lhs = new Vector3d(1.0, 2.0, 3.0);
        var rhs = new Vector3d(4.0, 6.0, 8.0);
        Assert.True(lhs.Lerp(rhs, 0.5).ApproximatelyEquivalent(new Vector3d(2.5, 4.0, 5.5)));
        Assert.True(lhs.CrossProduct(rhs).ApproximatelyEquivalent(new Vector3d(-2.0, 4.0, -2.0)));
        Assert.Equal(new Vector2d(1.0, 3.0), lhs.ToXZ());

        var start = new DoubleTransform
        {
            Position = new Vector3d(1.0, 2.0, 3.0),
            Rotation = Quaternion.IDENTITY,
            Scale = new Vector3f(1.0f, 2.0f, 3.0f)
        };
        var end = new DoubleTransform
        {
            Position = new Vector3d(5.0, 6.0, 7.0),
            Rotation = new Quaternion(90.0f, Vector3f.Up),
            Scale = new Vector3f(3.0f, 4.0f, 5.0f)
        };

        var midpoint = start.Lerp(end, 0.5f);
        Assert.True(midpoint.Position.ApproximatelyEquivalent(new Vector3d(3.0, 4.0, 5.0)));
        Assert.True(midpoint.Scale.ApproximatelyEquivalent(new Vector3f(2.0f, 3.0f, 4.0f)));
        Assert.True(DoubleTransform.Identity.Position.ApproximatelyEquivalent(Vector3d.Zero));
    }

    [Fact]
    public void Generic_base_vectors_support_double_precision_shape()
    {
        var vector2 = new Vector2d(1.25, 2.5);
        Assert.True(vector2.Normalized.Length > 0.999 && vector2.Normalized.Length < 1.001);
        Assert.True(vector2.Lerp(new Vector2d(3.25, 4.5), 0.5).ApproximatelyEquivalent(new Vector2d(2.25, 3.5)));

        var vector3 = new Vector3d(1.0, 2.0, 3.0);
        var other = new Vector3d(4.0, 6.0, 8.0);
        Assert.True(vector3.CrossProduct(other).ApproximatelyEquivalent(new Vector3d(-2.0, 4.0, -2.0)));
        Assert.Equal(new Vector2d(1.0, 3.0), vector3.ToXZ());
    }

}
