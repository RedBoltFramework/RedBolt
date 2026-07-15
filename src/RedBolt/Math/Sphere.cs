using System.Runtime.InteropServices;

namespace RedBolt;

[StructLayout(LayoutKind.Sequential)]
public struct Circle
{
    public Vector3f Center;
    public Vector3f Normal;
    public float Radius;

    public Circle(Vector3f center, Vector3f normal, float radius)
    {
        Center = center;
        Normal = normal;
        Radius = radius;
    }

    public readonly bool IsValid => Radius >= 0.0f;

    public readonly Vector3f GetPoint(Vector3f directionHint)
    {
        if (!IsValid)
            return Center;
        return Center + directionHint.Orthogonalize(Normal) * Radius;
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct Sphere : IEquatable<Sphere>, IApproximateEquatable<Sphere>
{
    public Vector3f Center;
    public float Radius;

    public Sphere(Vector3f? center = null, float radius = float.NegativeInfinity)
    {
        Center = center ?? Vector3f.Zero;
        Radius = radius;
    }

    public Sphere(IReadOnlyList<Vector3f> vertices)
    {
        Center = Vector3f.Zero;
        Radius = 0.0f;
        Define(vertices);
    }

    public Sphere(BoundingBox box)
    {
        Center = Vector3f.Zero;
        Radius = 0.0f;
        Define(box);
    }

    // C++: Sphere(const Frustum& frustum);
    // C++: Sphere(const Polyhedron& poly);

    // C++: void Define(const Frustum& frustum);
    // C++: void Define(const Polyhedron& poly);
    // C++: void Merge(const Frustum& frustum);
    // C++: void Merge(const Polyhedron& poly);

    public bool Defined => Radius >= 0.0f;

    public void Clear()
    {
        Center = Vector3f.Zero;
        Radius = float.NegativeInfinity;
    }

    public void Define(Sphere sphere) => Define(sphere.Center, sphere.Radius);
    public void Define(Vector3f center, float radius)
    {
        Center = center;
        Radius = radius;
    }

    public void Define(IReadOnlyList<Vector3f> vertices)
    {
        Clear();
        Merge(vertices);
    }

    public void Define(BoundingBox box)
    {
        Clear();
        Merge(box);
    }

    public Intersection IsInside(Vector3f point) => (point - Center).LengthSquared < Radius * Radius ? Intersection.Inside : Intersection.Outside;

    public Intersection IsInside(Sphere sphere)
    {
        var distance = (sphere.Center - Center).Length;
        if (distance >= sphere.Radius + Radius)
            return Intersection.Outside;
        if (distance + sphere.Radius < Radius)
            return Intersection.Inside;
        return Intersection.Intersects;
    }

    public Intersection IsInsideFast(Sphere sphere)
    {
        var combined = sphere.Radius + Radius;
        return (sphere.Center - Center).LengthSquared >= combined * combined ? Intersection.Outside : Intersection.Inside;
    }

    public Intersection IsInside(BoundingBox box)
    {
        if (!box.Defined)
            return Intersection.Outside;

        var allInside = true;
        var corners = GetCorners(box);
        foreach (var corner in corners)
        {
            if ((corner - Center).LengthSquared >= Radius * Radius)
            {
                allInside = false;
                break;
            }
        }

        if (allInside)
            return Intersection.Inside;

        return box.IsInside(this) == Intersection.Outside ? Intersection.Outside : Intersection.Intersects;
    }

    public Intersection IsInsideFast(BoundingBox box) => IsInside(box) == Intersection.Outside ? Intersection.Outside : Intersection.Inside;

    public readonly float Distance(Vector3f point) => Math.Max((point - Center).Length - Radius, 0.0f);

    public readonly Vector3f GetLocalPoint(float theta, float phi) => new(
        Radius * MathDefs.Sin(theta) * MathDefs.Sin(phi),
        Radius * MathDefs.Cos(phi),
        Radius * MathDefs.Cos(theta) * MathDefs.Sin(phi));

    public readonly Vector3f GetPoint(float theta, float phi) => Center + GetLocalPoint(theta, phi);

    public readonly Circle Intersect(Sphere sphere, out float distanceFromCenter)
    {
        var offset = sphere.Center - Center;
        var distance = offset.Length;
        var outerRadius = Radius;
        var innerRadius = sphere.Radius;
        var clampedDistance = Math.Min(outerRadius + innerRadius, distance);
        var areaSquared = (-clampedDistance + innerRadius - outerRadius)
            * (-clampedDistance - innerRadius + outerRadius)
            * (-clampedDistance + innerRadius + outerRadius)
            * (clampedDistance + innerRadius + outerRadius);
        var intersectionRadius = MathF.Sqrt(Math.Max(0.0f, areaSquared)) / (2.0f * clampedDistance);
        var isOutside = distance > outerRadius + innerRadius;
        var isInside = areaSquared < 0.0f;
        distanceFromCenter = MathF.Sqrt(outerRadius * outerRadius - intersectionRadius * intersectionRadius);
        var normal = offset / distance;
        var center = Center + distanceFromCenter * normal;
        return new Circle(center, normal, isInside || isOutside ? float.NegativeInfinity : intersectionRadius);
    }

    public readonly Circle Intersect(Sphere sphere) => Intersect(sphere, out _);

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
            Center += distance > MathDefs.Epsilon ? offset * (half / distance) : Vector3f.Zero;
        }
    }

    public void Merge(IReadOnlyList<Vector3f> vertices)
    {
        foreach (var vertex in vertices)
            Merge(vertex);
    }

    public void Merge(BoundingBox box)
    {
        foreach (var corner in GetCorners(box))
            Merge(corner);
    }

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
        if (distance + sphere.Radius < Radius)
            return;
        if (distance + Radius < sphere.Radius)
        {
            Center = sphere.Center;
            Radius = sphere.Radius;
            return;
        }

        if (distance <= MathDefs.Epsilon)
        {
            Radius = Math.Max(Radius, sphere.Radius);
            return;
        }

        var normalizedOffset = offset / distance;
        var min = Center - Radius * normalizedOffset;
        var max = sphere.Center + sphere.Radius * normalizedOffset;
        Center = (min + max) * 0.5f;
        Radius = (max - Center).Length;
    }

    public Sphere Transformed(Matrix3x4 transform)
    {
        var transformedCenter = transform * Center;
        var transformedScale = transform.Scale;
        var transformedRadius = Radius * Math.Max(transformedScale.X, Math.Max(transformedScale.Y, transformedScale.Z));
        return new Sphere(transformedCenter, transformedRadius);
    }

    public bool ApproximatelyEquivalent(Sphere rhs, float epsilon = MathDefs.Epsilon) =>
        Center.ApproximatelyEquivalent(rhs.Center, epsilon) && MathDefs.ApproximatelyEquivalent(Radius, rhs.Radius, epsilon);

    public bool Equals(Sphere other) => Center.Equals(other.Center) && Radius.Equals(other.Radius);
    public override bool Equals(object? obj) => obj is Sphere other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Center, Radius);
    public static bool operator ==(Sphere lhs, Sphere rhs) => lhs.Equals(rhs);
    public static bool operator !=(Sphere lhs, Sphere rhs) => !lhs.Equals(rhs);

    private static Vector3f[] GetCorners(BoundingBox box) =>
    [
        box.Min,
        new(box.Max.X, box.Min.Y, box.Min.Z),
        new(box.Min.X, box.Max.Y, box.Min.Z),
        new(box.Max.X, box.Max.Y, box.Min.Z),
        new(box.Min.X, box.Min.Y, box.Max.Z),
        new(box.Max.X, box.Min.Y, box.Max.Z),
        new(box.Min.X, box.Max.Y, box.Max.Z),
        box.Max
    ];
}
