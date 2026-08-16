// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Math;

using SystemMath = global::System.Math;
using System.Runtime.InteropServices;

/// <summary>Circle in three-dimensional space.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct Circle
{
    public Circle()
    {
        Center = Vector3f.Zero;
        Normal = Vector3f.Zero;
        Radius = float.NegativeInfinity;
    }

    public Circle(Vector3f center, Vector3f normal, float radius)
    {
        Center = center;
        Normal = normal;
        Radius = radius;
    }

    /// <summary>Return whether the circle is valid.</summary>
    public readonly bool IsValid => Radius >= 0.0f;

    /// <summary>Return point on the sphere closest to given direction.</summary>
    public readonly Vector3f GetPoint(Vector3f directionHint)
    {
        if (!IsValid)
            return Center;

        var direction = directionHint.Orthogonalize(Normal);
        return Center + direction * Radius;
    }

    /// <summary>Center of the circle.</summary>
    public Vector3f Center;

    /// <summary>Normal of the plane containing the circle.</summary>
    public Vector3f Normal;

    /// <summary>Radius of the circle. Negative is invalid.</summary>
    public float Radius;
}

/// <summary>Sphere in three-dimensional space.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct Sphere : ICloneable, IEquatable<Sphere>
{
    /// <summary>Construct undefined.</summary>
    public Sphere()
    {
        Center = Vector3f.Zero;
        Radius = float.NegativeInfinity;
    }

    /// <summary>Copy-construct from another sphere.</summary>
    public Sphere(Sphere sphere)
    {
        Center = sphere.Center;
        Radius = sphere.Radius;
    }

    public readonly Sphere Clone() => new(this);
    readonly object ICloneable.Clone() => Clone();

    /// <summary>Construct from center and radius.</summary>
    public Sphere(Vector3f center, float radius)
    {
        Center = center;
        Radius = radius;
    }

    /// <summary>Construct from an array of vertices.</summary>
    public Sphere(ReadOnlySpan<Vector3f> vertices)
    {
        Center = Vector3f.Zero;
        Radius = 0.0f;
        Define(vertices);
    }

    /// <summary>Construct from a bounding box.</summary>
    public Sphere(BoundingBox box)
    {
        Center = Vector3f.Zero;
        Radius = 0.0f;
        Define(box);
    }

    /// <summary>Construct from a frustum.</summary>
    public Sphere(Frustum frustum)
    {
        Center = Vector3f.Zero;
        Radius = 0.0f;
        Define(frustum);
    }

    /// <summary>Construct from a polyhedron.</summary>
    public Sphere(Polyhedron polyhedron)
    {
        Center = Vector3f.Zero;
        Radius = 0.0f;
        Define(polyhedron);
    }

    /// <summary>Test for equality with another sphere.</summary>
    public static bool operator ==(Sphere lhs, Sphere rhs) => lhs.Center == rhs.Center && lhs.Radius == rhs.Radius;

    /// <summary>Test for inequality with another sphere.</summary>
    public static bool operator !=(Sphere lhs, Sphere rhs) => lhs.Center != rhs.Center || lhs.Radius != rhs.Radius;

    /// <summary>Define from another sphere.</summary>
    public void Define(Sphere sphere)
    {
        Define(sphere.Center, sphere.Radius);
    }

    /// <summary>Define from center and radius.</summary>
    public void Define(Vector3f center, float radius)
    {
        Center = center;
        Radius = radius;
    }

    /// <summary>Define from an array of vertices.</summary>
    public void Define(ReadOnlySpan<Vector3f> vertices)
    {
        if (vertices.IsEmpty)
            return;

        Clear();
        Merge(vertices);
    }

    /// <summary>Define from a bounding box.</summary>
    public void Define(BoundingBox box)
    {
        var min = box.Min;
        var max = box.Max;

        Clear();
        Merge(min);
        Merge(new Vector3f(max.X, min.Y, min.Z));
        Merge(new Vector3f(min.X, max.Y, min.Z));
        Merge(new Vector3f(max.X, max.Y, min.Z));
        Merge(new Vector3f(min.X, min.Y, max.Z));
        Merge(new Vector3f(max.X, min.Y, max.Z));
        Merge(new Vector3f(min.X, max.Y, max.Z));
        Merge(max);
    }

    /// <summary>Define from a frustum.</summary>
    public void Define(Frustum frustum)
    {
        Define(frustum.Vertices);
    }

    /// <summary>Define from a polyhedron.</summary>
    public void Define(Polyhedron polyhedron)
    {
        Clear();
        Merge(polyhedron);
    }

    /// <summary>Merge a point.</summary>
    public void Merge(Vector3f point)
    {
        if (Radius < 0.0f)
        {
            Center = point;
            Radius = 0.0f;
            return;
        }

        var offset = point - Center;
        var distance = offset.Length;

        if (distance > Radius)
        {
            var half = (distance - Radius) * 0.5f;
            Radius += half;
            Center += (half / distance) * offset;
        }
    }

    /// <summary>Merge an array of vertices.</summary>
    public void Merge(ReadOnlySpan<Vector3f> vertices)
    {
        for (var i = 0; i < vertices.Length; ++i)
            Merge(vertices[i]);
    }

    /// <summary>Merge a bounding box.</summary>
    public void Merge(BoundingBox box)
    {
        var min = box.Min;
        var max = box.Max;

        Merge(min);
        Merge(new Vector3f(max.X, min.Y, min.Z));
        Merge(new Vector3f(min.X, max.Y, min.Z));
        Merge(new Vector3f(max.X, max.Y, min.Z));
        Merge(new Vector3f(min.X, min.Y, max.Z));
        Merge(new Vector3f(max.X, min.Y, max.Z));
        Merge(new Vector3f(min.X, max.Y, max.Z));
        Merge(max);
    }

    /// <summary>Merge a frustum.</summary>
    public void Merge(Frustum frustum)
    {
        Merge(frustum.Vertices);
    }

    /// <summary>Merge a polyhedron.</summary>
    public void Merge(Polyhedron polyhedron)
    {
        for (var i = 0; i < polyhedron.Faces.Count; ++i)
        {
            var face = polyhedron.Faces[i];
            if (face.Count != 0)
                Merge(CollectionsMarshal.AsSpan(face));
        }
    }

    /// <summary>Merge a sphere.</summary>
    public void Merge(Sphere sphere)
    {
        if (Radius < 0.0f)
        {
            Center = sphere.Center;
            Radius = sphere.Radius;
            return;
        }

        var offset = sphere.Center - Center;
        var distance = offset.Length;

        // If sphere fits inside, do nothing
        if (distance + sphere.Radius < Radius)
            return;

        // If we fit inside the other sphere, become it
        if (distance + Radius < sphere.Radius)
        {
            Center = sphere.Center;
            Radius = sphere.Radius;
        }
        else
        {
            var normalizedOffset = offset / distance;

            var min = Center - Radius * normalizedOffset;
            var max = sphere.Center + sphere.Radius * normalizedOffset;
            Center = (min + max) * 0.5f;
            Radius = (max - Center).Length;
        }
    }

    /// <summary>Clear to undefined state.</summary>
    public void Clear()
    {
        Center = Vector3f.Zero;
        Radius = float.NegativeInfinity;
    }

    /// <summary>Return true if this sphere is defined via a previous call to Define() or Merge().</summary>
    public readonly bool Defined => Radius >= 0.0f;

    /// <summary>Test if a point is inside.</summary>
    public readonly Intersection IsInside(Vector3f point)
    {
        var distanceSquared = (point - Center).LengthSquared;
        if (distanceSquared < Radius * Radius)
            return Intersection.Inside;
        else
            return Intersection.Outside;
    }

    /// <summary>Test if another sphere is inside, outside or intersects.</summary>
    public readonly Intersection IsInside(Sphere sphere)
    {
        var distance = (sphere.Center - Center).Length;
        if (distance >= sphere.Radius + Radius)
            return Intersection.Outside;
        else if (distance + sphere.Radius < Radius)
            return Intersection.Inside;
        else
            return Intersection.Intersects;
    }

    /// <summary>Test if another sphere is (partially) inside or outside.</summary>
    public readonly Intersection IsInsideFast(Sphere sphere)
    {
        var distanceSquared = (sphere.Center - Center).LengthSquared;
        var combined = sphere.Radius + Radius;

        if (distanceSquared >= combined * combined)
            return Intersection.Outside;
        else
            return Intersection.Inside;
    }

    /// <summary>Test if a bounding box is inside, outside or intersects.</summary>
    public readonly Intersection IsInside(BoundingBox box)
    {
        var radiusSquared = Radius * Radius;
        var distanceSquared = 0.0f;
        float temp;
        var min = box.Min;
        var max = box.Max;

        if (Center.X < min.X)
        {
            temp = Center.X - min.X;
            distanceSquared += temp * temp;
        }
        else if (Center.X > max.X)
        {
            temp = Center.X - max.X;
            distanceSquared += temp * temp;
        }
        if (Center.Y < min.Y)
        {
            temp = Center.Y - min.Y;
            distanceSquared += temp * temp;
        }
        else if (Center.Y > max.Y)
        {
            temp = Center.Y - max.Y;
            distanceSquared += temp * temp;
        }
        if (Center.Z < min.Z)
        {
            temp = Center.Z - min.Z;
            distanceSquared += temp * temp;
        }
        else if (Center.Z > max.Z)
        {
            temp = Center.Z - max.Z;
            distanceSquared += temp * temp;
        }

        if (distanceSquared >= radiusSquared)
            return Intersection.Outside;

        min -= Center;
        max -= Center;

        var tempVector = min; // - - -
        if (tempVector.LengthSquared >= radiusSquared)
            return Intersection.Intersects;
        tempVector.X = max.X; // + - -
        if (tempVector.LengthSquared >= radiusSquared)
            return Intersection.Intersects;
        tempVector.Y = max.Y; // + + -
        if (tempVector.LengthSquared >= radiusSquared)
            return Intersection.Intersects;
        tempVector.X = min.X; // - + -
        if (tempVector.LengthSquared >= radiusSquared)
            return Intersection.Intersects;
        tempVector.Z = max.Z; // - + +
        if (tempVector.LengthSquared >= radiusSquared)
            return Intersection.Intersects;
        tempVector.Y = min.Y; // - - +
        if (tempVector.LengthSquared >= radiusSquared)
            return Intersection.Intersects;
        tempVector.X = max.X; // + - +
        if (tempVector.LengthSquared >= radiusSquared)
            return Intersection.Intersects;
        tempVector.Y = max.Y; // + + +
        if (tempVector.LengthSquared >= radiusSquared)
            return Intersection.Intersects;

        return Intersection.Inside;
    }

    /// <summary>Test if a bounding box is (partially) inside or outside.</summary>
    public readonly Intersection IsInsideFast(BoundingBox box)
    {
        var radiusSquared = Radius * Radius;
        var distanceSquared = 0.0f;
        float temp;
        var min = box.Min;
        var max = box.Max;

        if (Center.X < min.X)
        {
            temp = Center.X - min.X;
            distanceSquared += temp * temp;
        }
        else if (Center.X > max.X)
        {
            temp = Center.X - max.X;
            distanceSquared += temp * temp;
        }
        if (Center.Y < min.Y)
        {
            temp = Center.Y - min.Y;
            distanceSquared += temp * temp;
        }
        else if (Center.Y > max.Y)
        {
            temp = Center.Y - max.Y;
            distanceSquared += temp * temp;
        }
        if (Center.Z < min.Z)
        {
            temp = Center.Z - min.Z;
            distanceSquared += temp * temp;
        }
        else if (Center.Z > max.Z)
        {
            temp = Center.Z - max.Z;
            distanceSquared += temp * temp;
        }

        if (distanceSquared >= radiusSquared)
            return Intersection.Outside;
        else
            return Intersection.Inside;
    }

    /// <summary>Intersect with another sphere.</summary>
    /// <param name="sphere">Another sphere.</param>
    /// <param name="distanceFromCenter">Optional distance from the center of this sphere to intersection circle.</param>
    /// <returns>If there's no intersection, closest point to another sphere.</returns>
    public readonly Circle Intersect(Sphere sphere, out float distanceFromCenter)
    {
        var offset = sphere.Center - Center;
        var distance = offset.Length;

        // http://mathworld.wolfram.com/Sphere-SphereIntersection.html
        var outerRadius = Radius;
        var innerRadius = sphere.Radius;
        var clampedDistance = SystemMath.Min(outerRadius + innerRadius, distance);
        var areaSquared = (-clampedDistance + innerRadius - outerRadius) * (-clampedDistance - innerRadius + outerRadius)
            * (-clampedDistance + innerRadius + outerRadius) * (clampedDistance + innerRadius + outerRadius);
        var intersectionRadius = MathF.Sqrt(SystemMath.Max(0.0f, areaSquared)) / (2.0f * clampedDistance);

        var isOutside = distance > outerRadius + innerRadius;
        var isInside = areaSquared < 0.0f;

        distanceFromCenter = MathF.Sqrt(outerRadius * outerRadius - intersectionRadius * intersectionRadius);

        var normal = offset / distance;
        var center = Center + distanceFromCenter * normal;
        var effectiveRadius = isInside || isOutside ? float.NegativeInfinity : intersectionRadius;
        return new Circle(center, normal, effectiveRadius);
    }

    public readonly Circle Intersect(Sphere sphere) => Intersect(sphere, out _);

    /// <summary>Return distance of a point to the surface, or 0 if inside.</summary>
    public readonly float Distance(Vector3f point) => SystemMath.Max((point - Center).Length - Radius, 0.0f);

    /// <summary>Return point on the sphere relative to sphere position.</summary>
    public readonly Vector3f GetLocalPoint(float theta, float phi) => new(
        Radius * MathDefs.Sin(theta) * MathDefs.Sin(phi),
        Radius * MathDefs.Cos(phi),
        Radius * MathDefs.Cos(theta) * MathDefs.Sin(phi));

    /// <summary>Return point on the sphere.</summary>
    public readonly Vector3f GetPoint(float theta, float phi) => Center + GetLocalPoint(theta, phi);

    /// <summary>Sphere center.</summary>
    public Vector3f Center;

    /// <summary>Sphere radius.</summary>
    public float Radius;

    public readonly bool Equals(Sphere other) => this == other;
    public override readonly bool Equals(object? obj) => obj is Sphere other && Equals(other);
    public override readonly int GetHashCode() => HashCode.Combine(Center, Radius);
}
