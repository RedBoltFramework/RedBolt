using System.Globalization;
using System.Runtime.InteropServices;

namespace RedBolt;

/// <summary>Rotation represented as a four-dimensional normalized vector.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct Quaternion : ICloneable, IEquatable<Quaternion>, IApproximateEquatable<Quaternion>
{
    /// <summary>Construct an identity quaternion.</summary>
    public Quaternion()
    {
        W = 1.0f;
        X = 0.0f;
        Y = 0.0f;
        Z = 0.0f;
    }

    /// <summary>Copy-construct from another quaternion.</summary>
    public Quaternion(Quaternion quat)
    {
        W = quat.W;
        X = quat.X;
        Y = quat.Y;
        Z = quat.Z;
    }

    public readonly Quaternion Clone() => new(this);

    readonly object ICloneable.Clone() => Clone();

    /// <summary>Construct from values.</summary>
    public Quaternion(float w, float x, float y, float z)
    {
        W = w;
        X = x;
        Y = y;
        Z = z;
    }

    /// <summary>Construct from a float array.</summary>
    public Quaternion(ReadOnlySpan<float> data)
    {
        W = data[0];
        X = data[1];
        Y = data[2];
        Z = data[3];
    }

    /// <summary>Construct from an angle (in degrees) and axis.</summary>
    public Quaternion(float angle, Vector3f axis)
    {
        this = default;
        FromAngleAxis(angle, axis);
    }

    /// <summary>Construct from an angle (in degrees, for Urho2D).</summary>
    public Quaternion(float angle)
    {
        this = default;
        FromAngleAxis(angle, Vector3f.Forward);
    }

    /// <summary>Construct from Euler angles (in degrees). Equivalent to Y*X*Z.</summary>
    public Quaternion(float x, float y, float z)
    {
        this = default;
        FromEulerAngles(x, y, z);
    }

    /// <summary>Construct from Euler angles (in degrees).</summary>
    public Quaternion(Vector3f angles)
    {
        this = default;
        FromEulerAngles(angles.X, angles.Y, angles.Z);
    }

    /// <summary>Construct from the rotation difference between two direction vectors.</summary>
    public Quaternion(Vector3f start, Vector3f end)
    {
        this = default;
        FromRotationTo(start, end);
    }

    /// <summary>Construct from orthonormal axes.</summary>
    public Quaternion(Vector3f xAxis, Vector3f yAxis, Vector3f zAxis)
    {
        this = default;
        FromAxes(xAxis, yAxis, zAxis);
    }

    /// <summary>Construct from a rotation matrix.</summary>
    public Quaternion(Matrix3 matrix)
    {
        this = default;
        FromRotationMatrix(matrix);
    }

    /// <summary>Construct from angular velocity assuming unit time.</summary>
    /// <remarks>Absolute value of angular velocity is measured in radians.</remarks>
    public static Quaternion FromAngularVelocity(Vector3f angularVelocity)
    {
        var len2 = angularVelocity.LengthSquared;
        if (len2 < MathDefs.Epsilon * MathDefs.Epsilon)
            return IDENTITY;

        var len = MathF.Sqrt(len2);
        var direction = angularVelocity / len;
        var angle = len * MathDefs.RadToDeg;
        return new Quaternion(angle, direction);
    }

    /// <summary>Test for equality with another quaternion without epsilon.</summary>
    public static bool operator ==(Quaternion lhs, Quaternion rhs) =>
        lhs.W == rhs.W && lhs.X == rhs.X && lhs.Y == rhs.Y && lhs.Z == rhs.Z;

    /// <summary>Test for inequality with another quaternion without epsilon.</summary>
    public static bool operator !=(Quaternion lhs, Quaternion rhs) => !(lhs == rhs);

    /// <summary>Multiply with a scalar.</summary>
    public static Quaternion operator *(Quaternion lhs, float rhs) =>
        new(lhs.W * rhs, lhs.X * rhs, lhs.Y * rhs, lhs.Z * rhs);

    /// <summary>Return negation.</summary>
    public static Quaternion operator -(Quaternion rhs) => new(-rhs.W, -rhs.X, -rhs.Y, -rhs.Z);

    /// <summary>Add a quaternion.</summary>
    public static Quaternion operator +(Quaternion lhs, Quaternion rhs) =>
        new(lhs.W + rhs.W, lhs.X + rhs.X, lhs.Y + rhs.Y, lhs.Z + rhs.Z);

    /// <summary>Subtract a quaternion.</summary>
    public static Quaternion operator -(Quaternion lhs, Quaternion rhs) =>
        new(lhs.W - rhs.W, lhs.X - rhs.X, lhs.Y - rhs.Y, lhs.Z - rhs.Z);

    /// <summary>Multiply a quaternion.</summary>
    public static Quaternion operator *(Quaternion lhs, Quaternion rhs) => new(
        lhs.W * rhs.W - lhs.X * rhs.X - lhs.Y * rhs.Y - lhs.Z * rhs.Z,
        lhs.W * rhs.X + lhs.X * rhs.W + lhs.Y * rhs.Z - lhs.Z * rhs.Y,
        lhs.W * rhs.Y + lhs.Y * rhs.W + lhs.Z * rhs.X - lhs.X * rhs.Z,
        lhs.W * rhs.Z + lhs.Z * rhs.W + lhs.X * rhs.Y - lhs.Y * rhs.X);

    /// <summary>Multiply a Vector3.</summary>
    public static Vector3f operator *(Quaternion lhs, Vector3f rhs)
    {
        var qVec = new Vector3f(lhs.X, lhs.Y, lhs.Z);
        var cross1 = qVec.CrossProduct(rhs);
        var cross2 = qVec.CrossProduct(cross1);

        return rhs + 2.0f * (cross1 * lhs.W + cross2);
    }

    /// <summary>Define from an angle (in degrees) and axis.</summary>
    public void FromAngleAxis(float angle, Vector3f axis)
    {
        var normAxis = axis.Normalized;
        MathDefs.SinCos(angle * 0.5f, out var sinAngle, out var cosAngle);

        W = cosAngle;
        X = normAxis.X * sinAngle;
        Y = normAxis.Y * sinAngle;
        Z = normAxis.Z * sinAngle;
    }

    /// <summary>Define from Euler angles (in degrees). Equivalent to Y*X*Z.</summary>
    public void FromEulerAngles(float x, float y, float z)
    {
        // Order of rotations: Z first, then X, then Y (mimics typical FPS camera with gimbal lock at top/bottom)
        MathDefs.SinCos(x * 0.5f, out var sinX, out var cosX);
        MathDefs.SinCos(y * 0.5f, out var sinY, out var cosY);
        MathDefs.SinCos(z * 0.5f, out var sinZ, out var cosZ);

        W = cosY * cosX * cosZ + sinY * sinX * sinZ;
        X = cosY * sinX * cosZ + sinY * cosX * sinZ;
        Y = sinY * cosX * cosZ - cosY * sinX * sinZ;
        Z = cosY * cosX * sinZ - sinY * sinX * cosZ;
    }

    /// <summary>Define from the rotation difference between two direction vectors.</summary>
    public void FromRotationTo(Vector3f start, Vector3f end)
    {
        var normStart = start.Normalized;
        var normEnd = end.Normalized;
        var d = normStart.DotProduct(normEnd);

        if (d > -1.0f + MathDefs.Epsilon)
        {
            var c = normStart.CrossProduct(normEnd);
            var s = MathF.Sqrt((1.0f + d) * 2.0f);
            var invS = 1.0f / s;

            X = c.X * invS;
            Y = c.Y * invS;
            Z = c.Z * invS;
            W = 0.5f * s;
        }
        else
        {
            var axis = Vector3f.Right.CrossProduct(normStart);
            if (axis.Length < MathDefs.Epsilon)
                axis = Vector3f.Up.CrossProduct(normStart);

            FromAngleAxis(180.0f, axis);
        }
    }

    /// <summary>Define from orthonormal axes.</summary>
    public void FromAxes(Vector3f xAxis, Vector3f yAxis, Vector3f zAxis)
    {
        var matrix = new Matrix3(
            xAxis.X, yAxis.X, zAxis.X,
            xAxis.Y, yAxis.Y, zAxis.Y,
            xAxis.Z, yAxis.Z, zAxis.Z);

        FromRotationMatrix(matrix);
    }

    /// <summary>Define from a rotation matrix.</summary>
    public void FromRotationMatrix(Matrix3 matrix)
    {
        var t = matrix.M00 + matrix.M11 + matrix.M22;

        if (t > 0.0f)
        {
            var invS = 0.5f / MathF.Sqrt(1.0f + t);

            X = (matrix.M21 - matrix.M12) * invS;
            Y = (matrix.M02 - matrix.M20) * invS;
            Z = (matrix.M10 - matrix.M01) * invS;
            W = 0.25f / invS;
        }
        else
        {
            if (matrix.M00 > matrix.M11 && matrix.M00 > matrix.M22)
            {
                var invS = 0.5f / MathF.Sqrt(1.0f + matrix.M00 - matrix.M11 - matrix.M22);

                X = 0.25f / invS;
                Y = (matrix.M01 + matrix.M10) * invS;
                Z = (matrix.M20 + matrix.M02) * invS;
                W = (matrix.M21 - matrix.M12) * invS;
            }
            else if (matrix.M11 > matrix.M22)
            {
                var invS = 0.5f / MathF.Sqrt(1.0f + matrix.M11 - matrix.M00 - matrix.M22);

                X = (matrix.M01 + matrix.M10) * invS;
                Y = 0.25f / invS;
                Z = (matrix.M12 + matrix.M21) * invS;
                W = (matrix.M02 - matrix.M20) * invS;
            }
            else
            {
                var invS = 0.5f / MathF.Sqrt(1.0f + matrix.M22 - matrix.M00 - matrix.M11);

                X = (matrix.M02 + matrix.M20) * invS;
                Y = (matrix.M12 + matrix.M21) * invS;
                Z = 0.25f / invS;
                W = (matrix.M10 - matrix.M01) * invS;
            }
        }
    }

    /// <summary>Define from a direction to look in and an up direction. Return true if successful, or false if would result in a NaN, in which case the current value remains.</summary>
    public bool FromLookRotation(Vector3f direction, Vector3f up)
    {
        var ret = new Quaternion();
        var forward = direction.Normalized;

        var v = forward.CrossProduct(up);
        // If direction & up are parallel and crossproduct becomes zero, use FromRotationTo() fallback
        if (v.LengthSquared >= MathDefs.Epsilon)
        {
            v.Normalize();
            var correctedUp = v.CrossProduct(forward);
            var right = correctedUp.CrossProduct(forward);
            ret.FromAxes(right, correctedUp, forward);
        }
        else
            ret.FromRotationTo(Vector3f.Forward, forward);

        if (!ret.IsNaN)
        {
            this = ret;
            return true;
        }
        else
            return false;
    }

    /// <summary>Normalize to unit length.</summary>
    public void Normalize()
    {
        var lenSquared = LengthSquared;
        if (!MathDefs.Equals(lenSquared, 1.0f) && lenSquared > 0.0f)
        {
            var invLen = 1.0f / MathF.Sqrt(lenSquared);
            W *= invLen;
            X *= invLen;
            Y *= invLen;
            Z *= invLen;
        }
    }

    /// <summary>Return normalized to unit length.</summary>
    public readonly Quaternion Normalized
    {
        get
        {
            var lenSquared = LengthSquared;
            if (!MathDefs.Equals(lenSquared, 1.0f) && lenSquared > 0.0f)
            {
                var invLen = 1.0f / MathF.Sqrt(lenSquared);
                return this * invLen;
            }
            else
                return this;
        }
    }

    /// <summary>Return inverse.</summary>
    public readonly Quaternion Inverse
    {
        get
        {
            var lenSquared = LengthSquared;
            if (lenSquared == 1.0f)
                return Conjugate();
            else if (lenSquared >= MathDefs.Epsilon)
                return Conjugate() * (1.0f / lenSquared);
            else
                return IDENTITY;
        }
    }

    /// <summary>Return squared length.</summary>
    public readonly float LengthSquared => W * W + X * X + Y * Y + Z * Z;

    /// <summary>Calculate dot product.</summary>
    public readonly float DotProduct(Quaternion rhs) => W * rhs.W + X * rhs.X + Y * rhs.Y + Z * rhs.Z;

    /// <summary>Test for equality with another quaternion with epsilon.</summary>
    public readonly bool ApproximatelyEquivalent(Quaternion rhs, float epsilon = MathDefs.Epsilon) =>
        MathDefs.Equals(W, rhs.W, epsilon) && MathDefs.Equals(X, rhs.X, epsilon) &&
        MathDefs.Equals(Y, rhs.Y, epsilon) && MathDefs.Equals(Z, rhs.Z, epsilon);

    /// <summary>Test for equivalence with another quaternion with epsilon.</summary>
    public readonly bool Equivalent(Quaternion rhs, float epsilon = MathDefs.Epsilon) =>
        MathDefs.Equals(MathF.Abs(DotProduct(rhs)), 1.0f, epsilon);

    /// <summary>Return whether any element is NaN.</summary>
    public readonly bool IsNaN => float.IsNaN(W) || float.IsNaN(X) || float.IsNaN(Y) || float.IsNaN(Z);

    /// <summary>Return whether any element is Inf.</summary>
    public readonly bool IsInf => float.IsInfinity(W) || float.IsInfinity(X) || float.IsInfinity(Y) || float.IsInfinity(Z);

    /// <summary>Return conjugate.</summary>
    public readonly Quaternion Conjugate() => new(W, -X, -Y, -Z);

    /// <summary>Return Euler angles in degrees.</summary>
    public readonly Vector3f EulerAngles
    {
        get
        {
            // Derivation from http://www.geometrictools.com/Documentation/EulerAngles.pdf
            // Order of rotations: Z first, then X, then Y
            var check = 2.0f * (-Y * Z + W * X);

            // More on singularity handling here:
            // http://www.euclideanspace.com/maths/geometry/rotations/conversions/quaternionToEuler/
            const float singularityThreshold = 0.999999f;
            var y2 = Y * Y;
            var z2 = Z * Z;
            var x2 = X * X;
            var unit = W * W + x2 + y2 + z2;

            // In case of singularity treat rotation as yaw angle as it fits yaw/pitch 3D games better.
            if (MathF.Abs(check) > singularityThreshold * unit)
            {
                return new Vector3f(
                    90.0f * MathDefs.Sign(check),
                    -MathF.Atan2(2.0f * (X * Z - W * Y), 1.0f - 2.0f * (y2 + z2)) * MathDefs.RadToDeg,
                    0.0f);
            }
            return new Vector3f(
                MathF.Asin(check) * MathDefs.RadToDeg,
                MathF.Atan2(2.0f * (X * Z + W * Y), 1.0f - 2.0f * (X * X + y2)) * MathDefs.RadToDeg,
                MathF.Atan2(2.0f * (X * Y + W * Z), 1.0f - 2.0f * (X * X + z2)) * MathDefs.RadToDeg);
        }
    }

    /// <summary>Return yaw angle in degrees.</summary>
    public readonly float YawAngle => EulerAngles.Y;

    /// <summary>Return pitch angle in degrees.</summary>
    public readonly float PitchAngle => EulerAngles.X;

    /// <summary>Return roll angle in degrees.</summary>
    public readonly float RollAngle => EulerAngles.Z;

    /// <summary>Return rotation axis.</summary>
    public readonly Vector3f Axis
    {
        get
        {
            var axisScaleInv = MathF.Sqrt(Math.Max(0.0f, 1.0f - W * W));
            if (axisScaleInv < 1e-6f)
                return Vector3f.Up;
            return new Vector3f(X, Y, Z) / axisScaleInv;
        }
    }

    /// <summary>Return rotation angle.</summary>
    public readonly float Angle => 2.0f * MathDefs.Acos(W);

    /// <summary>Return angular velocity assuming unit time.</summary>
    /// <remarks>Absolute value of angular velocity is measured in radians.</remarks>
    public readonly Vector3f AngularVelocity
    {
        get
        {
            var axisScaleInv = MathF.Sqrt(Math.Max(0.0f, 1.0f - W * W));
            if (axisScaleInv < MathDefs.Epsilon)
                return Vector3f.Zero;

            var sign = W >= 0.0f ? 1.0f : -1.0f;
            var axis = new Vector3f(X, Y, Z) / axisScaleInv;
            var angleRad = 2.0f * MathF.Acos(sign * W);
            return sign * axis * angleRad;
        }
    }

    /// <summary>Return the rotation matrix that corresponds to this quaternion.</summary>
    public readonly Matrix3 RotationMatrix => new(
        1.0f - 2.0f * Y * Y - 2.0f * Z * Z,
        2.0f * X * Y - 2.0f * W * Z,
        2.0f * X * Z + 2.0f * W * Y,
        2.0f * X * Y + 2.0f * W * Z,
        1.0f - 2.0f * X * X - 2.0f * Z * Z,
        2.0f * Y * Z - 2.0f * W * X,
        2.0f * X * Z - 2.0f * W * Y,
        2.0f * Y * Z + 2.0f * W * X,
        1.0f - 2.0f * X * X - 2.0f * Y * Y);

    /// <summary>Spherical interpolation with another quaternion.</summary>
    public readonly Quaternion Slerp(Quaternion rhs, float t)
    {
        if (OperatingSystem.IsBrowser())
        {
            // Use fast approximation for Emscripten builds
            var angle = DotProduct(rhs);
            var sign = 1.0f; // Multiply by a sign of +/-1 to guarantee we rotate the shorter arc.
            if (angle < 0.0f)
            {
                angle = -angle;
                sign = -1.0f;
            }

            float a;
            float b;
            if (angle < 0.999f) // perform spherical linear interpolation.
            {
                // angle = acos(angle); // After this, angle is in the range pi/2 -> 0 as the original angle variable ranged from 0 -> 1.
                angle = (-0.69813170079773212f * angle * angle - 0.87266462599716477f) * angle + 1.5707963267948966f;
                var ta = t * angle;
                // Manually compute the two sines by using a very rough approximation.
                var ta2 = ta * ta;
                b = ((5.64311797634681035370e-03f * ta2 - 1.55271410633428644799e-01f) * ta2 + 9.87862135574673806965e-01f) * ta;
                a = angle - ta;
                var a2 = a * a;
                a = ((5.64311797634681035370e-03f * a2 - 1.55271410633428644799e-01f) * a2 + 9.87862135574673806965e-01f) * a;
            }
            else // If angle is close to taking the denominator to zero, resort to linear interpolation (and normalization).
            {
                a = 1.0f - t;
                b = t;
            }
            // Lerp and renormalize.
            return (this * (a * sign) + rhs * b).Normalized;
        }
        else
        {
            // Favor accuracy for native code builds
            var cosAngle = DotProduct(rhs);
            var sign = 1.0f;
            // Enable shortest path rotation
            if (cosAngle < 0.0f)
            {
                cosAngle = -cosAngle;
                sign = -1.0f;
            }

            var angle = MathF.Acos(cosAngle);
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

            return this * t1 + (rhs * sign) * t2;
        }
    }

    /// <summary>Normalized linear interpolation with another quaternion.</summary>
    public readonly Quaternion Nlerp(Quaternion rhs, float t, bool shortestPath = false)
    {
        Quaternion result;
        var fCos = DotProduct(rhs);
        if (fCos < 0.0f && shortestPath)
            result = this + ((-rhs - this) * t);
        else
            result = this + ((rhs - this) * t);
        result.Normalize();
        return result;
    }

    /// <summary>Decompose quaternion to swing and twist components. swing * twist is the original quaternion.</summary>
    public readonly (Quaternion Swing, Quaternion Twist) ToSwingTwist(Vector3f twistAxis)
    {
        // https://stackoverflow.com/questions/3684269/component-of-a-quaternion-rotation-around-an-axis
        var ra = new Vector3f(X, Y, Z);
        var p = twistAxis * ra.ProjectOntoAxis(twistAxis);

        var twist = new Quaternion(W, p.X, p.Y, p.Z);
        twist.Normalize();

        var swing = this * twist.Conjugate();
        return (swing, twist);
    }

    /// <summary>Return twist angle around given axis. Quaternion may or may not have additional rotation that will be ignored.</summary>
    public readonly float TwistAngle(Vector3f axis)
    {
        var (_, twist) = ToSwingTwist(axis);
        var axisSign = twist.Axis.DotProduct(axis) >= 0.0f ? 1.0f : -1.0f;
        return twist.Angle * axisSign;
    }

    /// <summary>Return float data.</summary>
    public ReadOnlySpan<float> Data() => MemoryMarshal.CreateReadOnlySpan(ref W, 4);

    /// <summary>Return as string.</summary>
    public override readonly string ToString() =>
        string.Format(CultureInfo.InvariantCulture, "{0:g} {1:g} {2:g} {3:g}", W, X, Y, Z);

    /// <summary>Return hash value for HashSet &amp; HashMap.</summary>
    public readonly uint ToHash()
    {
        var hash = 37u;
        hash = unchecked(37u * hash + (uint)BitConverter.SingleToInt32Bits(X));
        hash = unchecked(37u * hash + (uint)BitConverter.SingleToInt32Bits(Y));
        hash = unchecked(37u * hash + (uint)BitConverter.SingleToInt32Bits(Z));
        hash = unchecked(37u * hash + (uint)BitConverter.SingleToInt32Bits(W));

        return hash;
    }

    public readonly bool Equals(Quaternion other) => this == other;

    public override readonly bool Equals(object? obj) => obj is Quaternion other && Equals(other);

    public override readonly int GetHashCode() => unchecked((int)ToHash());

    /// <summary>W coordinate.</summary>
    public float W;

    /// <summary>X coordinate.</summary>
    public float X;

    /// <summary>Y coordinate.</summary>
    public float Y;

    /// <summary>Z coordinate.</summary>
    public float Z;

    /// <summary>Identity quaternion.</summary>
    public static readonly Quaternion IDENTITY = new();

    /// <summary>Zero quaternion.</summary>
    public static readonly Quaternion ZERO = new(0.0f, 0.0f, 0.0f, 0.0f);
}
