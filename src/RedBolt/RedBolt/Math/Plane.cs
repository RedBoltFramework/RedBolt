namespace RedBolt.Math;

using System.Runtime.InteropServices;

/// <summary>Surface in three-dimensional space.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct Plane : ICloneable
{
    /// <summary>Construct a degenerate plane with zero normal and parameter.</summary>
    public Plane()
    {
        Normal = default;
        AbsNormal = default;
        D = 0.0f;
    }

    /// <summary>Copy-construct from another plane.</summary>
    public Plane(Plane plane)
    {
        Normal = plane.Normal;
        AbsNormal = plane.AbsNormal;
        D = plane.D;
    }

    /// <summary>Construct from 3 vertices.</summary>
    public Plane(Vector3f v0, Vector3f v1, Vector3f v2)
    {
        Normal = default;
        AbsNormal = default;
        D = default;
        Define(v0, v1, v2);
    }

    /// <summary>Construct from a normal vector and a point on the plane.</summary>
    public Plane(Vector3f normal, Vector3f point)
    {
        Normal = default;
        AbsNormal = default;
        D = default;
        Define(normal, point);
    }

    /// <summary>Construct from a 4-dimensional vector, where the w coordinate is the plane parameter.</summary>
    public Plane(Vector4f plane)
    {
        Normal = default;
        AbsNormal = default;
        D = default;
        Define(plane);
    }

    public readonly Plane Clone() => this;

    readonly object ICloneable.Clone() => Clone();

    /// <summary>Define from 3 vertices.</summary>
    public void Define(Vector3f v0, Vector3f v1, Vector3f v2)
    {
        var dist1 = v1 - v0;
        var dist2 = v2 - v0;

        Define(dist1.CrossProduct(dist2), v0);
    }

    /// <summary>Define from a normal vector and a point on the plane.</summary>
    public void Define(Vector3f normal, Vector3f point)
    {
        Normal = normal.Normalized;
        AbsNormal = Normal.Abs;
        D = -Normal.DotProduct(point);
    }

    /// <summary>Define from a 4-dimensional vector, where the w coordinate is the plane parameter.</summary>
    public void Define(Vector4f plane)
    {
        Normal = new Vector3f(plane.X, plane.Y, plane.Z);
        AbsNormal = Normal.Abs;
        D = plane.W;
    }

    /// <summary>Transform with a 3x3 matrix.</summary>
    public void Transform(Matrix3 transform)
    {
        Define(new Matrix4(transform).Inverse.Transpose * ToVector4());
    }

    /// <summary>Transform with a 3x4 matrix.</summary>
    public void Transform(Matrix3x4 transform)
    {
        Define(transform.Matrix4.Inverse.Transpose * ToVector4());
    }

    /// <summary>Transform with a 4x4 matrix.</summary>
    public void Transform(Matrix4 transform)
    {
        Define(transform.Inverse.Transpose * ToVector4());
    }

    /// <summary>Project a point on the plane.</summary>
    public readonly Vector3f Project(Vector3f point) => point - Normal * (Normal.DotProduct(point) + D);

    /// <summary>Return signed distance to a point.</summary>
    public readonly float Distance(Vector3f point) => Normal.DotProduct(point) + D;

    /// <summary>Reflect a normalized direction vector.</summary>
    public readonly Vector3f Reflect(Vector3f direction) => direction - 2.0f * Normal.DotProduct(direction) * Normal;

    /// <summary>Return a reflection matrix.</summary>
    public readonly Matrix3x4 ReflectionMatrix => new(
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

    /// <summary>Return transformed by a 3x3 matrix.</summary>
    public readonly Plane Transformed(Matrix3 transform)
    {
        return new Plane(new Matrix4(transform).Inverse.Transpose * ToVector4());
    }

    /// <summary>Return transformed by a 3x4 matrix.</summary>
    public readonly Plane Transformed(Matrix3x4 transform)
    {
        return new Plane(transform.Matrix4.Inverse.Transpose * ToVector4());
    }

    /// <summary>Return transformed by a 4x4 matrix.</summary>
    public readonly Plane Transformed(Matrix4 transform)
    {
        return new Plane(transform.Inverse.Transpose * ToVector4());
    }

    /// <summary>Returns closest point to origin.</summary>
    public readonly Vector3f GetPoint() => -Normal * D;

    /// <summary>Returns a line that defines an intersection of 2 planes.</summary>
    public readonly Ray Intersect(Plane other)
    {
        // Calculate the direction of the line
        var direction = Normal.CrossProduct(other.Normal);

        // If the direction is zero, then the planes are parallel and do not intersect
        if (direction.ApproximatelyEquivalent(Vector3f.Zero, 1e-6f))
            return new Ray(GetPoint(), Vector3f.Zero);

        // Calculate the determinant of the matrix using cross product
        var det = Normal.DotProduct(other.Normal.CrossProduct(direction));

        // If the determinant is zero, then the planes do not all intersect at one point
        if (MathF.Abs(det) < 1e-6f)
            return new Ray(GetPoint(), direction);

        var point = D * other.Normal.CrossProduct(direction) + other.D * direction.CrossProduct(Normal);

        return new Ray(-point / det, direction);
    }

    /// <summary>Returns a point that defines an intersection of 3 planes.</summary>
    public readonly Vector3f Intersect(Plane planeB, Plane planeC)
    {
        // Calculate the determinant of the matrix using cross product
        var det = Normal.DotProduct(planeB.Normal.CrossProduct(planeC.Normal));

        // If the determinant is zero, then the planes do not all intersect at one point
        if (MathF.Abs(det) < 1e-6f)
            return GetPoint();

        // Calculate the intersection point using Cramer's rule
        var result =
            (-D * planeB.Normal.CrossProduct(planeC.Normal) - planeB.D * planeC.Normal.CrossProduct(Normal)
                - planeC.D * Normal.CrossProduct(planeB.Normal))
            / det;

        return result;
    }

    /// <summary>Return as a vector.</summary>
    public readonly Vector4f ToVector4() => new(Normal, D);

    /// <summary>Plane normal.</summary>
    public Vector3f Normal;

    /// <summary>Plane absolute normal.</summary>
    public Vector3f AbsNormal;

    /// <summary>Plane constant.</summary>
    public float D;

    // Static initialization order can not be relied on, so do not use Vector3 constants
    /// <summary>Plane at origin with normal pointing up.</summary>
    public static readonly Plane Up = new(new Vector3f(0.0f, 1.0f, 0.0f), new Vector3f(0.0f, 0.0f, 0.0f));
}
