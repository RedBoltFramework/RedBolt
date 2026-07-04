using System.Runtime.InteropServices;

namespace RedBolt;

[StructLayout(LayoutKind.Sequential)]
public struct Plane
{
    public Vector3 Normal;
    public Vector3 AbsNormal;
    public float D;

    public Plane(Vector3 v0, Vector3 v1, Vector3 v2)
    {
        Normal = default;
        AbsNormal = default;
        D = default;
        Define(v0, v1, v2);
    }

    public Plane(Vector3 normal, Vector3 point)
    {
        Normal = default;
        AbsNormal = default;
        D = default;
        Define(normal, point);
    }

    public Plane(Vector4 plane)
    {
        Normal = new Vector3(plane.X, plane.Y, plane.Z);
        AbsNormal = Normal.Abs;
        D = plane.W;
    }

    public static readonly Plane Up = new(new Vector3(0.0f, 1.0f, 0.0f), Vector3.Zero);

    public Vector4 Vector4 => new(Normal, D);

    public void Define(Vector3 v0, Vector3 v1, Vector3 v2)
    {
        Define((v1 - v0).CrossProduct(v2 - v0), v0);
    }

    public void Define(Vector3 normal, Vector3 point)
    {
        Normal = normal.Normalized;
        AbsNormal = Normal.Abs;
        D = -Normal.DotProduct(point);
    }

    public void Define(Vector4 plane)
    {
        Normal = new Vector3(plane.X, plane.Y, plane.Z);
        AbsNormal = Normal.Abs;
        D = plane.W;
    }

    public float Distance(Vector3 point) => Normal.DotProduct(point) + D;

    public Vector3 Project(Vector3 point) => point - Normal * Distance(point);

    public Vector3 Reflect(Vector3 direction) => direction - 2.0f * Normal.DotProduct(direction) * Normal;

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
}
