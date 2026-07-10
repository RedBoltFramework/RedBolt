using System.Runtime.InteropServices;

namespace RedBolt;

[StructLayout(LayoutKind.Sequential)]
public struct Transform : IEquatable<Transform>, IApproximateEquatable<Transform>
{
    public Vector3f Position;
    public Quaternion Rotation;
    public Vector3f Scale;

    public static readonly Transform IDENTITY = new(Vector3f.Zero, Quaternion.Identity, Vector3f.One);
    public static Transform Identity => IDENTITY;

    public Transform(Vector3f position, Quaternion rotation)
    {
        Position = position;
        Rotation = rotation;
        Scale = Vector3f.One;
    }

    public Transform(Vector3f position, Quaternion rotation, Vector3f scale)
    {
        Position = position;
        Rotation = rotation;
        Scale = scale;
    }

    public static Transform FromMatrix3x4(Matrix3x4 matrix)
    {
        matrix.Decompose(out var position, out var rotation, out var scale);
        return new Transform(position, rotation, scale);
    }

    public Matrix3x4 ToMatrix3x4() => new(Position, Rotation, Scale);

    public Transform Lerp(Transform rhs, float t) => new(
        Position.Lerp(rhs.Position, t),
        Rotation.Slerp(rhs.Rotation, t),
        Scale.Lerp(rhs.Scale, t));

    public Transform Inverse()
    {
        var rotation = Rotation.Inverse;
        var scale = Vector3f.One / Scale;
        var position = -(rotation * Position) * scale;
        return new Transform(position, rotation, scale);
    }

    public bool ApproximatelyEquivalent(Transform other, float epsilon = MathDefs.Epsilon) =>
        Position.ApproximatelyEquivalent(other.Position, epsilon) &&
        Rotation.ApproximatelyEquivalent(other.Rotation, epsilon) &&
        Scale.ApproximatelyEquivalent(other.Scale, epsilon);

    public bool Equals(Transform other) => Position.Equals(other.Position) && Rotation.Equals(other.Rotation) && Scale.Equals(other.Scale);
    public override bool Equals(object? obj) => obj is Transform other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Position, Rotation, Scale);

    public static bool operator ==(Transform lhs, Transform rhs) => lhs.Equals(rhs);
    public static bool operator !=(Transform lhs, Transform rhs) => !lhs.Equals(rhs);
    public static Transform Lerp(Transform lhs, Transform rhs, float t) => lhs.Lerp(rhs, t);
    public static Transform operator *(Transform lhs, Transform rhs) => new(
        lhs.Position + lhs.Rotation * (rhs.Position * lhs.Scale),
        lhs.Rotation * rhs.Rotation,
        lhs.Scale * rhs.Scale);
    public static Vector3f operator *(Transform lhs, Vector3f rhs) => lhs.Position + lhs.Rotation * (rhs * lhs.Scale);
    public static Quaternion operator *(Transform lhs, Quaternion rhs) => lhs.Rotation * rhs;
}

[StructLayout(LayoutKind.Sequential)]
public struct DoubleTransform : IEquatable<DoubleTransform>, IApproximateEquatable<DoubleTransform>
{
    public Vector3d Position;
    public Quaternion Rotation;
    public Vector3f Scale;

    public static readonly DoubleTransform IDENTITY = new() { Position = Vector3d.Zero, Rotation = Quaternion.Identity, Scale = Vector3f.One };
    public static DoubleTransform Identity => IDENTITY;

    public DoubleTransform Lerp(DoubleTransform rhs, float t) => new()
    {
        Position = Position.Lerp(rhs.Position, t),
        Rotation = Rotation.Slerp(rhs.Rotation, t),
        Scale = Scale.Lerp(rhs.Scale, t)
    };

    public bool ApproximatelyEquivalent(DoubleTransform other, float epsilon = MathDefs.Epsilon) =>
        Position.ApproximatelyEquivalent(other.Position, epsilon) &&
        Rotation.ApproximatelyEquivalent(other.Rotation, epsilon) &&
        Scale.ApproximatelyEquivalent(other.Scale, epsilon);

    public bool Equals(DoubleTransform other) => Position.Equals(other.Position) && Rotation.Equals(other.Rotation) && Scale.Equals(other.Scale);
    public override bool Equals(object? obj) => obj is DoubleTransform other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Position, Rotation, Scale);

    public static bool operator ==(DoubleTransform lhs, DoubleTransform rhs) => lhs.Equals(rhs);
    public static bool operator !=(DoubleTransform lhs, DoubleTransform rhs) => !lhs.Equals(rhs);
    public static DoubleTransform Lerp(DoubleTransform lhs, DoubleTransform rhs, float t) => lhs.Lerp(rhs, t);
}
