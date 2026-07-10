using System.Runtime.InteropServices;

namespace RedBolt;

[StructLayout(LayoutKind.Sequential)]
public struct Plane
{
    public Vector3f Normal;
    public Vector3f AbsNormal;
    public float D;

    public Plane(Vector3f v0, Vector3f v1, Vector3f v2)
    {
        Normal = default;
        AbsNormal = default;
        D = default;
        Define(v0, v1, v2);
    }

    public Plane(Vector3f normal, Vector3f point)
    {
        Normal = default;
        AbsNormal = default;
        D = default;
        Define(normal, point);
    }

    public Plane(Vector4f plane)
    {
        Normal = new Vector3f(plane.X, plane.Y, plane.Z);
        AbsNormal = Normal.Abs;
        D = plane.W;
    }

    public static readonly Plane Up = new(new Vector3f(0.0f, 1.0f, 0.0f), Vector3f.Zero);

    public Vector4f Vector4f => new(Normal, D);

    public void Define(Vector3f v0, Vector3f v1, Vector3f v2)
    {
        Define((v1 - v0).CrossProduct(v2 - v0), v0);
    }

    public void Define(Vector3f normal, Vector3f point)
    {
        Normal = normal.Normalized;
        AbsNormal = Normal.Abs;
        D = -Normal.DotProduct(point);
    }

    public void Define(Vector4f plane)
    {
        Normal = new Vector3f(plane.X, plane.Y, plane.Z);
        AbsNormal = Normal.Abs;
        D = plane.W;
    }

    public float Distance(Vector3f point) => Normal.DotProduct(point) + D;

    public Vector3f Project(Vector3f point) => point - Normal * Distance(point);

    public Vector3f Reflect(Vector3f direction) => direction - 2.0f * Normal.DotProduct(direction) * Normal;

    public void Transform(Matrix3 transform) => Define(new Matrix4(transform).Inverted.Transposed * Vector4f);
    public void Transform(Matrix3x4 transform) => Define(transform.Matrix4.Inverted.Transposed * Vector4f);
    public void Transform(Matrix4 transform) => Define(transform.Inverted.Transposed * Vector4f);

    public Matrix3x4 ReflectionMatrix() => new(
        -2.0f * Normal.X * Normal.X + 1.0f,
        -2.0f * Normal.X * Normal.Y,
        -2.0f * Normal.X * Normal.Z,
        -2.0f * Normal.X * D,
        -2.0f * Normal.Y * Normal.X,
        -2.0f * Normal.Y * Normal.Y + 1.0f,
        -2.0f * Normal.Y * Normal.Z,
        -2.0f * Normal.Y * D,
        -2.0f * Normal.Z * Normal.X,
        -2.0f * Normal.Z * Normal.Y,
        -2.0f * Normal.Z * Normal.Z + 1.0f,
        -2.0f * Normal.Z * D);

    public Plane Transformed(Matrix3 transform) => new(new Matrix4(transform).Inverted.Transposed * Vector4f);
    public Plane Transformed(Matrix3x4 transform) => new(transform.Matrix4.Inverted.Transposed * Vector4f);
    public Plane Transformed(Matrix4 transform) => new(transform.Inverted.Transposed * Vector4f);
}
