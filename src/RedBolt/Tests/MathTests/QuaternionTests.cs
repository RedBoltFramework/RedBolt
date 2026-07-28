namespace RedBolt.Tests.MathTests;

using System.Runtime.InteropServices;

using RedBolt.Math;

public class QuaternionTests
{
    [Fact]
    public void Shape_copy_construction_and_contiguous_data_match_native()
    {
        Assert.True(typeof(Quaternion).IsValueType);
        Assert.True(typeof(Quaternion).IsAssignableTo(typeof(ICloneable)));
        Assert.Equal(16, Marshal.SizeOf<Quaternion>());
        Assert.Equal(Quaternion.IDENTITY, new Quaternion());

        var source = new Quaternion(1.0f, 2.0f, 3.0f, 4.0f);
        var copy = new Quaternion(source);
        var clone = (Quaternion)((ICloneable)source).Clone();
        source.X = 10.0f;

        Assert.Equal(2.0f, copy.X);
        Assert.Equal(copy, clone);

        var fromData = new Quaternion(new float[] { 5.0f, 6.0f, 7.0f, 8.0f });
        var data = fromData.Data();
        Assert.Equal(new float[] { 5.0f, 6.0f, 7.0f, 8.0f }, data.ToArray());
        fromData.W = 9.0f;
        Assert.Equal(9.0f, data[0]);
    }

    [Fact]
    public void Constructors_and_mutating_definitions_match_native()
    {
        var angleAxis = new Quaternion(45.0f, Vector3f.Up);
        var definedAngleAxis = new Quaternion();
        definedAngleAxis.FromAngleAxis(45.0f, Vector3f.Up);
        Assert.True(angleAxis.Equivalent(definedAngleAxis));
        Assert.True(new Quaternion(45.0f).Equivalent(new Quaternion(45.0f, Vector3f.Forward)));

        var euler = new Quaternion(10.0f, -20.0f, 30.0f);
        var definedEuler = new Quaternion();
        definedEuler.FromEulerAngles(10.0f, -20.0f, 30.0f);
        Assert.True(euler.Equivalent(definedEuler));

        var matrix = euler.RotationMatrix;
        var fromMatrix = new Quaternion(matrix);
        var definedMatrix = new Quaternion();
        definedMatrix.FromRotationMatrix(matrix);
        Assert.True(euler.Equivalent(fromMatrix, 0.0001f));
        Assert.True(fromMatrix.Equivalent(definedMatrix, 0.0001f));

        var fromAxes = new Quaternion(matrix.Column(0), matrix.Column(1), matrix.Column(2));
        Assert.True(euler.Equivalent(fromAxes, 0.0001f));
        Assert.True(new Quaternion(Vector3f.Forward, Vector3f.Back).Equivalent(new Quaternion(180.0f, Vector3f.Up)));
    }

    [Fact]
    public void Failed_look_rotation_preserves_current_value()
    {
        var rotation = new Quaternion(10.0f, 20.0f, 30.0f);
        var original = rotation;

        Assert.False(rotation.FromLookRotation(
            Vector3f.One, new Vector3f(float.MaxValue, -float.MaxValue, float.MaxValue)));
        Assert.Equal(original, rotation);

        Assert.True(rotation.FromLookRotation(Vector3f.Forward, Vector3f.Up));
        Assert.True(rotation.Equivalent(Quaternion.IDENTITY));
    }

    [Fact]
    public void Euler_angles_and_gimbal_lock_match_native_tests()
    {
        var angles = new Vector3f(10.0f, -20.0f, 30.0f);
        var matrix = new Matrix3(angles.Y, Vector3f.Up) * new Matrix3(angles.X, Vector3f.Right) *
            new Matrix3(angles.Z, Vector3f.Forward);
        Assert.True(matrix.ApproximatelyEquivalent(new Quaternion(angles).RotationMatrix));

        foreach (var pitch in new[] { 90.0f, -90.0f })
        {
            var expected = new Vector3f(pitch, -10.0f, 0.0f);
            var expectedMatrix = new Matrix3(expected.Y, Vector3f.Up) * new Matrix3(expected.X, Vector3f.Right);
            var actualQuaternion = new Quaternion(expected);

            Assert.True(expected.ApproximatelyEquivalent(actualQuaternion.EulerAngles));
            Assert.True(expectedMatrix.ApproximatelyEquivalent(actualQuaternion.RotationMatrix));
        }
    }

    [Fact]
    public void Operators_inverse_conjugate_and_hash_match_native()
    {
        var lhs = new Quaternion(1.0f, 2.0f, 3.0f, 4.0f);
        var rhs = new Quaternion(5.0f, 6.0f, 7.0f, 8.0f);

        Assert.Equal(new Quaternion(6.0f, 8.0f, 10.0f, 12.0f), lhs + rhs);
        Assert.Equal(new Quaternion(-4.0f, -4.0f, -4.0f, -4.0f), lhs - rhs);
        Assert.Equal(new Quaternion(2.0f, 4.0f, 6.0f, 8.0f), lhs * 2.0f);
        Assert.Equal(new Quaternion(-1.0f, -2.0f, -3.0f, -4.0f), -lhs);
        Assert.Equal(new Quaternion(1.0f, -2.0f, -3.0f, -4.0f), lhs.Conjugate());

        var rotation = new Quaternion(35.0f, Vector3f.Up);
        Assert.True((rotation * rotation.Inverse).Equivalent(Quaternion.IDENTITY));

        var hash = 37u;
        hash = unchecked(37u * hash + (uint)BitConverter.SingleToInt32Bits(lhs.X));
        hash = unchecked(37u * hash + (uint)BitConverter.SingleToInt32Bits(lhs.Y));
        hash = unchecked(37u * hash + (uint)BitConverter.SingleToInt32Bits(lhs.Z));
        hash = unchecked(37u * hash + (uint)BitConverter.SingleToInt32Bits(lhs.W));
        Assert.Equal(hash, lhs.ToHash());
        Assert.Equal(unchecked((int)hash), lhs.GetHashCode());
    }

    [Fact]
    public void Interpolation_swing_twist_and_angular_velocity_match_native()
    {
        var begin = new Quaternion(10.0f, Vector3f.Up);
        var end = new Quaternion(70.0f, Vector3f.Up);

        Assert.True(begin.Slerp(end, 0.0f).Equivalent(begin));
        Assert.True(begin.Slerp(end, 1.0f).Equivalent(end));
        Assert.True(begin.Nlerp(end, 0.5f, true).Equivalent(new Quaternion(40.0f, Vector3f.Up), 0.001f));

        var rotation = new Quaternion(20.0f, 30.0f, 40.0f);
        var (swing, twist) = rotation.ToSwingTwist(Vector3f.Up);
        Assert.True((swing * twist).Equivalent(rotation, 0.0001f));

        var velocity = new Vector3f(0.1f, -0.2f, 0.3f);
        Assert.True(Quaternion.FromAngularVelocity(velocity).AngularVelocity.ApproximatelyEquivalent(velocity, 0.0001f));
    }
}
