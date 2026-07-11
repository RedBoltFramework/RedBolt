using System.Globalization;
using System.Runtime.InteropServices;

namespace RedBolt;

[StructLayout(LayoutKind.Sequential)]
public struct Quaternion : IEquatable<Quaternion>, IApproximateEquatable<Quaternion>
{
    public float W;
    public float X;
    public float Y;
    public float Z;

    public Quaternion(float w = 1.0f, float x = 0.0f, float y = 0.0f, float z = 0.0f)
    {
        W = w;
        X = x;
        Y = y;
        Z = z;
    }

    public Quaternion(float angle, Vector3f axis)
    {
        this = FromAngleAxis(angle, axis);
    }

    public Quaternion(float angle)
    {
        this = FromRotation2D(angle);
    }

    public Quaternion(float x, float y, float z)
    {
        this = FromEulerAngles(x, y, z);
    }

    public Quaternion(Vector3f eulerAngles)
    {
        this = FromEulerAngles(eulerAngles.X, eulerAngles.Y, eulerAngles.Z);
    }

    public Quaternion(Vector3f start, Vector3f end)
    {
        this = FromRotationTo(start, end);
    }

    public Quaternion(Vector3f xAxis, Vector3f yAxis, Vector3f zAxis)
    {
        this = FromAxes(xAxis, yAxis, zAxis);
    }

    public Quaternion(Matrix3 matrix)
    {
        this = FromRotationMatrix(matrix);
    }

    public static readonly Quaternion IDENTITY = new(1.0f, 0.0f, 0.0f, 0.0f);
    public static Quaternion Identity => IDENTITY;

    public Vector3f Axis
    {
        get
        {
            var inverseScale = MathF.Sqrt(Math.Max(0.0f, 1.0f - W * W));
            return inverseScale < 1e-6f ? Vector3f.Up : new Vector3f(X, Y, Z) / inverseScale;
        }
    }
    public Quaternion Conjugated => new(W, -X, -Y, -Z);
    public float[] Data => [W, X, Y, Z];
    public float DotProduct(Quaternion rhs) => W * rhs.W + X * rhs.X + Y * rhs.Y + Z * rhs.Z;
    public Vector3f EulerAngles => ToEulerAngles();
    public Quaternion Inverse => Inversed;
    public Quaternion Inversed
    {
        get
        {
            var lenSquared = LengthSquared;
            if (MathDefs.Equals(lenSquared, 1.0f))
                return Conjugated;
            if (lenSquared >= MathDefs.Epsilon)
                return Conjugated * (1.0f / lenSquared);
            return Identity;
        }
    }

    public bool IsNaN => float.IsNaN(W) || float.IsNaN(X) || float.IsNaN(Y) || float.IsNaN(Z);
    public bool IsInf => float.IsInfinity(W) || float.IsInfinity(X) || float.IsInfinity(Y) || float.IsInfinity(Z);
    public float Length => MathF.Sqrt(LengthSquared);
    public float LengthSquared => W * W + X * X + Y * Y + Z * Z;
    public float PitchAngle => EulerAngles.X;
    public float RollAngle => EulerAngles.Z;
    public Matrix3 RotationMatrix => new(
        1.0f - 2.0f * Y * Y - 2.0f * Z * Z,
        2.0f * X * Y - 2.0f * W * Z,
        2.0f * X * Z + 2.0f * W * Y,
        2.0f * X * Y + 2.0f * W * Z,
        1.0f - 2.0f * X * X - 2.0f * Z * Z,
        2.0f * Y * Z - 2.0f * W * X,
        2.0f * X * Z - 2.0f * W * Y,
        2.0f * Y * Z + 2.0f * W * X,
        1.0f - 2.0f * X * X - 2.0f * Y * Y);
    public Vector3f Xyz => new(X, Y, Z);
    public float YawAngle => EulerAngles.Y;
    public float Angle => 2.0f * MathDefs.Acos(W);
    public Vector3f AngularVelocity
    {
        get
        {
            var inverseScale = MathF.Sqrt(Math.Max(0.0f, 1.0f - W * W));
            if (inverseScale < MathDefs.Epsilon)
                return Vector3f.Zero;
            var sign = W >= 0.0f ? 1.0f : -1.0f;
            var axis = new Vector3f(X, Y, Z) / inverseScale;
            var angleRadians = 2.0f * MathF.Acos(sign * W);
            return sign * axis * angleRadians;
        }
    }

    public Quaternion Normalized
    {
        get
        {
            var lenSquared = LengthSquared;
            if (!MathDefs.Equals(lenSquared, 1.0f) && lenSquared > 0.0f)
                return this * (1.0f / MathF.Sqrt(lenSquared));
            return this;
        }
    }

    public void Normalize()
    {
        var lenSquared = LengthSquared;
        if (!MathDefs.Equals(lenSquared, 1.0f) && lenSquared > 0.0f)
            this *= 1.0f / MathF.Sqrt(lenSquared);
    }

    public Quaternion Nlerp(Quaternion rhs, float t, bool shortestPath = false)
    {
        Quaternion result;
        var cos = DotProduct(rhs);
        if (cos < 0.0f && shortestPath)
            result = this + (-rhs - this) * t;
        else
            result = this + (rhs - this) * t;
        result.Normalize();
        return result;
    }

    public Quaternion Slerp(Quaternion rhs, float t)
    {
        var cosAngle = DotProduct(rhs);
        var sign = 1.0f;
        if (cosAngle < 0.0f)
        {
            cosAngle = -cosAngle;
            sign = -1.0f;
        }

        var angle = MathF.Acos(MathDefs.Clamp(cosAngle, -1.0f, 1.0f));
        var sinAngle = MathF.Sin(angle);
        float t1;
        float t2;
        if (sinAngle > 0.001f)
        {
            var invSinAngle = 1.0f / sinAngle;
            t1 = MathF.Sin((1.0f - t) * angle) * invSinAngle;
            t2 = MathF.Sin(t * angle) * invSinAngle;
        }
        else
        {
            t1 = 1.0f - t;
            t2 = t;
        }

        return this * t1 + rhs * sign * t2;
    }

    public bool ApproximatelyEquivalent(Quaternion rhs, float epsilon = MathDefs.Epsilon) =>
        MathDefs.ApproximatelyEquivalent(W, rhs.W, epsilon) &&
        MathDefs.ApproximatelyEquivalent(X, rhs.X, epsilon) &&
        MathDefs.ApproximatelyEquivalent(Y, rhs.Y, epsilon) &&
        MathDefs.ApproximatelyEquivalent(Z, rhs.Z, epsilon);

    public bool Equivalent(Quaternion rhs, float epsilon = MathDefs.Epsilon) =>
        ApproximatelyEquivalent(rhs, epsilon) || ApproximatelyEquivalent(-rhs, epsilon);

    public (Quaternion Swing, Quaternion Twist) ToSwingTwist(Vector3f twistAxis)
    {
        var rotationAxis = new Vector3f(X, Y, Z);
        var projected = twistAxis * rotationAxis.ProjectOntoAxis(twistAxis);
        var twist = new Quaternion(W, projected.X, projected.Y, projected.Z).Normalized;
        return (this * twist.Conjugated, twist);
    }

    public float TwistAngle(Vector3f axis)
    {
        var twist = ToSwingTwist(axis).Twist;
        var axisSign = twist.Axis.DotProduct(axis) >= 0.0f ? 1.0f : -1.0f;
        return twist.Angle * axisSign;
    }

    public uint ToHash()
    {
        var hash = 37u;
        foreach (var value in Data)
            hash = unchecked(37u * hash + (uint)BitConverter.SingleToInt32Bits(value));
        return hash;
    }

    public bool Equals(Quaternion other) => this == other;
    public override bool Equals(object? obj) => obj is Quaternion other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(W, X, Y, Z);
    public override string ToString() => string.Format(CultureInfo.InvariantCulture, "{0} {1} {2} {3}", W, X, Y, Z);

    public static Quaternion FromAngleAxis(float angle, Vector3f axis)
    {
        var normalizedAxis = axis.Normalized;
        var halfAngle = MathDefs.DegreesToRadians2(angle);
        var sin = MathF.Sin(halfAngle);
        var cos = MathF.Cos(halfAngle);
        return new Quaternion(cos, normalizedAxis.X * sin, normalizedAxis.Y * sin, normalizedAxis.Z * sin);
    }

    public static Quaternion FromAngularVelocity(Vector3f angularVelocity)
    {
        var lengthSquared = angularVelocity.LengthSquared;
        if (lengthSquared < MathDefs.Epsilon * MathDefs.Epsilon)
            return Identity;
        var length = MathF.Sqrt(lengthSquared);
        return FromAngleAxis(length * MathDefs.RadToDeg, angularVelocity / length);
    }

    public static Quaternion FromAxes(Vector3f xAxis, Vector3f yAxis, Vector3f zAxis)
    {
        return FromRotationMatrix(new Matrix3(
            xAxis.X, yAxis.X, zAxis.X,
            xAxis.Y, yAxis.Y, zAxis.Y,
            xAxis.Z, yAxis.Z, zAxis.Z));
    }

    public static Quaternion FromEulerAngles(float x, float y, float z)
    {
        x = MathDefs.DegreesToRadians2(x);
        y = MathDefs.DegreesToRadians2(y);
        z = MathDefs.DegreesToRadians2(z);
        var sinX = MathF.Sin(x);
        var cosX = MathF.Cos(x);
        var sinY = MathF.Sin(y);
        var cosY = MathF.Cos(y);
        var sinZ = MathF.Sin(z);
        var cosZ = MathF.Cos(z);

        return new Quaternion(
            cosY * cosX * cosZ + sinY * sinX * sinZ,
            cosY * sinX * cosZ + sinY * cosX * sinZ,
            sinY * cosX * cosZ - cosY * sinX * sinZ,
            cosY * cosX * sinZ - sinY * sinX * cosZ);
    }

    public static Quaternion FromRotation2D(float angle) => FromAngleAxis(angle, Vector3f.Forward);

    public static Quaternion FromRotationMatrix(Matrix3 matrix)
    {
        var t = matrix.M00 + matrix.M11 + matrix.M22;
        if (t > 0.0f)
        {
            var invS = 0.5f / MathF.Sqrt(1.0f + t);
            return new Quaternion(
                0.25f / invS,
                (matrix.M21 - matrix.M12) * invS,
                (matrix.M02 - matrix.M20) * invS,
                (matrix.M10 - matrix.M01) * invS);
        }

        if (matrix.M00 > matrix.M11 && matrix.M00 > matrix.M22)
        {
            var invS = 0.5f / MathF.Sqrt(1.0f + matrix.M00 - matrix.M11 - matrix.M22);
            return new Quaternion(
                (matrix.M21 - matrix.M12) * invS,
                0.25f / invS,
                (matrix.M01 + matrix.M10) * invS,
                (matrix.M20 + matrix.M02) * invS);
        }

        if (matrix.M11 > matrix.M22)
        {
            var invS = 0.5f / MathF.Sqrt(1.0f + matrix.M11 - matrix.M00 - matrix.M22);
            return new Quaternion(
                (matrix.M02 - matrix.M20) * invS,
                (matrix.M01 + matrix.M10) * invS,
                0.25f / invS,
                (matrix.M12 + matrix.M21) * invS);
        }
        else
        {
            var invS = 0.5f / MathF.Sqrt(1.0f + matrix.M22 - matrix.M00 - matrix.M11);
            return new Quaternion(
                (matrix.M10 - matrix.M01) * invS,
                (matrix.M02 + matrix.M20) * invS,
                (matrix.M12 + matrix.M21) * invS,
                0.25f / invS);
        }
    }

    public static Quaternion FromRotationTo(Vector3f start, Vector3f end)
    {
        var normStart = start.Normalized;
        var normEnd = end.Normalized;
        var dot = normStart.DotProduct(normEnd);
        if (dot > -1.0f + float.Epsilon)
        {
            var cross = normStart.CrossProduct(normEnd);
            var scale = MathF.Sqrt((1.0f + dot) * 2.0f);
            var invScale = 1.0f / scale;
            return new Quaternion(0.5f * scale, cross.X * invScale, cross.Y * invScale, cross.Z * invScale).Normalized;
        }

        var axis = Vector3f.Right.CrossProduct(normStart);
        if (axis.Length < float.Epsilon)
            axis = Vector3f.Up.CrossProduct(normStart);
        return FromAngleAxis(180.0f, axis);
    }

    public static bool LookRotation(Vector3f direction, Vector3f up, out Quaternion rotation)
    {
        var forward = direction.Normalized;
        var side = forward.CrossProduct(up);
        if (side.LengthSquared >= float.Epsilon)
        {
            side.Normalize();
            var up2 = side.CrossProduct(forward);
            var right = up2.CrossProduct(forward);
            rotation = new Quaternion(right, up2, forward);
        }
        else
            rotation = FromRotationTo(Vector3f.Forward, forward);

        return !rotation.IsNaN;
    }

    public static bool operator ==(Quaternion lhs, Quaternion rhs) => lhs.W == rhs.W && lhs.X == rhs.X && lhs.Y == rhs.Y && lhs.Z == rhs.Z;
    public static bool operator !=(Quaternion lhs, Quaternion rhs) => !(lhs == rhs);
    public static Quaternion operator -(Quaternion rhs) => new(-rhs.W, -rhs.X, -rhs.Y, -rhs.Z);
    public static Quaternion operator +(Quaternion lhs, Quaternion rhs) => new(lhs.W + rhs.W, lhs.X + rhs.X, lhs.Y + rhs.Y, lhs.Z + rhs.Z);
    public static Quaternion operator -(Quaternion lhs, Quaternion rhs) => new(lhs.W - rhs.W, lhs.X - rhs.X, lhs.Y - rhs.Y, lhs.Z - rhs.Z);
    public static Quaternion operator *(Quaternion lhs, float rhs) => new(lhs.W * rhs, lhs.X * rhs, lhs.Y * rhs, lhs.Z * rhs);
    public static Quaternion operator *(float lhs, Quaternion rhs) => rhs * lhs;

    public static Quaternion operator *(Quaternion lhs, Quaternion rhs) => new(
        lhs.W * rhs.W - lhs.X * rhs.X - lhs.Y * rhs.Y - lhs.Z * rhs.Z,
        lhs.W * rhs.X + lhs.X * rhs.W + lhs.Y * rhs.Z - lhs.Z * rhs.Y,
        lhs.W * rhs.Y + lhs.Y * rhs.W + lhs.Z * rhs.X - lhs.X * rhs.Z,
        lhs.W * rhs.Z + lhs.Z * rhs.W + lhs.X * rhs.Y - lhs.Y * rhs.X);

    public static Vector3f operator *(Quaternion lhs, Vector3f rhs)
    {
        var qVec = new Vector3f(lhs.X, lhs.Y, lhs.Z);
        var cross1 = qVec.CrossProduct(rhs);
        var cross2 = qVec.CrossProduct(cross1);
        return rhs + 2.0f * (cross1 * lhs.W + cross2);
    }

    private Vector3f ToEulerAngles()
    {
        var check = 2.0f * (-Y * Z + W * X);
        const float singularityThreshold = 0.999999f;
        var unit = W * W + X * X + Y * Y + Z * Z;

        if (check < -singularityThreshold * unit)
        {
            return new Vector3f(
                -90.0f,
                MathDefs.RadiansToDegrees(-MathF.Atan2(2.0f * (X * Z - W * Y), 1.0f - 2.0f * (Y * Y + Z * Z))),
                0.0f);
        }

        if (check > singularityThreshold * unit)
        {
            return new Vector3f(
                90.0f,
                MathDefs.RadiansToDegrees(-MathF.Atan2(2.0f * (X * Z - W * Y), 1.0f - 2.0f * (Y * Y + Z * Z))),
                0.0f);
        }

        return new Vector3f(
            MathDefs.RadiansToDegrees(MathF.Asin(check)),
            MathDefs.RadiansToDegrees(MathF.Atan2(2.0f * (X * Z + W * Y), 1.0f - 2.0f * (X * X + Y * Y))),
            MathDefs.RadiansToDegrees(MathF.Atan2(2.0f * (X * Y + W * Z), 1.0f - 2.0f * (X * X + Z * Z))));
    }
}
