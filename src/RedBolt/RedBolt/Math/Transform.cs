using System.Runtime.InteropServices;

namespace RedBolt.Math;

/// <summary>3D transform decomposed into translation, rotation and scale components.</summary>
/// <remarks>TODO: Expand it into something more user-friendly.</remarks>
[StructLayout(LayoutKind.Sequential)]
public struct Transform
{
    public Vector3f Position;
    public Quaternion Rotation;
    public Vector3f Scale;

    public static readonly Transform Identity = new(Vector3f.Zero, Quaternion.IDENTITY, Vector3f.One);

    public Transform()
    {
        Position = Vector3f.Zero;
        Rotation = Quaternion.IDENTITY;
        Scale = Vector3f.One;
    }

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

    /// <summary>Construct Transform from Matrix3x4. It is not precise for non-uniform scale.</summary>
    public static Transform FromMatrix3x4(in Matrix3x4 matrix)
    {
        var result = new Transform();
        matrix.Decompose(out result.Position, out result.Rotation, out result.Scale);
        return result;
    }

    /// <summary>Construct Matrix3x4 from Transform.</summary>
    public readonly Matrix3x4 ToMatrix3x4() => new(Position, Rotation, Scale);

    /// <summary>Interpolate between two transforms.</summary>
    public readonly Transform Lerp(in Transform rhs, float t) => new(
        Position.Lerp(rhs.Position, t),
        Rotation.Slerp(rhs.Rotation, t),
        Scale.Lerp(rhs.Scale, t));

    /// <summary>Return inverse transform. It is not precise for non-uniform scale.</summary>
    public readonly Transform Inverse()
    {
        var ret = new Transform();
        ret.Rotation = Rotation.Inverse;
        ret.Scale = Vector3f.One / Scale;
        ret.Position = -(ret.Rotation * Position) * ret.Scale;
        return ret;
    }

    public static Transform Lerp(in Transform lhs, in Transform rhs, float t) => lhs.Lerp(rhs, t);

    /// <summary>Return transform multiplied by another transform.</summary>
    public static Transform operator *(Transform lhs, Transform rhs)
    {
        var ret = new Transform();
        ret.Rotation = lhs.Rotation * rhs.Rotation;
        ret.Scale = lhs.Scale * rhs.Scale;
        ret.Position = lhs.Position + lhs.Rotation * (rhs.Position * lhs.Scale);
        return ret;
    }

    /// <summary>Return position multiplied by the transform.</summary>
    public static Vector3f operator *(Transform lhs, Vector3f rhs) => lhs.Position + lhs.Rotation * (rhs * lhs.Scale);

    /// <summary>Return rotation multiplied by the transform.</summary>
    public static Quaternion operator *(Transform lhs, Quaternion rhs) => lhs.Rotation * rhs;
}

/// <summary>3D transform decomposed into translation, rotation and scale components.</summary>
/// <remarks>Position uses <see langword="double"/> precision components.</remarks>
[StructLayout(LayoutKind.Sequential)]
public struct DoubleTransform
{
    public Vector3d Position;
    public Quaternion Rotation;
    public Vector3f Scale;

    public static readonly DoubleTransform Identity = new()
    {
        Position = Vector3d.Zero,
        Rotation = Quaternion.IDENTITY,
        Scale = Vector3f.One
    };

    public DoubleTransform()
    {
        Position = Vector3d.Zero;
        Rotation = Quaternion.IDENTITY;
        Scale = Vector3f.One;
    }

    /// <summary>Interpolate between two transforms.</summary>
    public readonly DoubleTransform Lerp(in DoubleTransform rhs, float t) => new()
    {
        Position = Position.Lerp(rhs.Position, t),
        Rotation = Rotation.Slerp(rhs.Rotation, t),
        Scale = Scale.Lerp(rhs.Scale, t)
    };

    public static DoubleTransform Lerp(in DoubleTransform lhs, in DoubleTransform rhs, float t) => lhs.Lerp(rhs, t);
}
