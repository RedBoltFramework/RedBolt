using System.Runtime.InteropServices;

namespace RedBolt;

[StructLayout(LayoutKind.Sequential)]
public struct Transform : IEquatable<Transform>, IApproximateEquatable<Transform>
{
    public Vector3 Position;
    public Quaternion Rotation;
    public Vector3 Scale;

    public Transform(Vector3 position, Quaternion rotation)
    {
        Position = position;
        Rotation = rotation;
        Scale = Vector3.One;
    }

    public Transform(Vector3 position, Quaternion rotation, Vector3 scale)
    {
        Position = position;
        Rotation = rotation;
        Scale = scale;
    }

    public Matrix3x4 ToMatrix3x4() => new(Position, Rotation, Scale);

    public bool ApproximatelyEquivalent(Transform other, float epsilon = MathDefs.Epsilon) =>
        Position.ApproximatelyEquivalent(other.Position, epsilon) &&
        Rotation.ApproximatelyEquivalent(other.Rotation, epsilon) &&
        Scale.ApproximatelyEquivalent(other.Scale, epsilon);

    public bool Equals(Transform other) => Position.Equals(other.Position) && Rotation.Equals(other.Rotation) && Scale.Equals(other.Scale);
    public override bool Equals(object? obj) => obj is Transform other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Position, Rotation, Scale);

    public static bool operator ==(Transform lhs, Transform rhs) => lhs.Equals(rhs);
    public static bool operator !=(Transform lhs, Transform rhs) => !lhs.Equals(rhs);
}
