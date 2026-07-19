using System.Runtime.InteropServices;

namespace RedBolt;

[StructLayout(LayoutKind.Sequential)]
public struct DistanceAndNormal
{
    public DistanceAndNormal()
    {
        Normal = Vector3f.Up;
        Distance = float.PositiveInfinity;
    }

    public DistanceAndNormal(Vector3f normal, float distance)
    {
        Normal = normal;
        Distance = distance;
    }

    public Vector3f Normal;
    public float Distance;
}

/// <summary>Infinite straight line in three-dimensional space.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct Ray : ICloneable, IEquatable<Ray>
{
    /// <summary>Construct a degenerate ray with zero origin and direction.</summary>
    public Ray()
    {
        Origin = Vector3f.Zero;
        Direction = Vector3f.Zero;
    }

    /// <summary>Construct from origin and direction. The direction will be normalized.</summary>
    public Ray(Vector3f origin, Vector3f direction)
    {
        this = default;
        Define(origin, direction);
    }

    /// <summary>Copy-construct from another ray.</summary>
    public Ray(Ray ray)
    {
        Origin = ray.Origin;
        Direction = ray.Direction;
    }

    public readonly Ray Clone() => new(this);

    readonly object ICloneable.Clone() => Clone();

    public readonly bool Equals(Ray other) => Origin == other.Origin && Direction == other.Direction;

    public override readonly bool Equals(object? obj) => obj is Ray other && Equals(other);

    public override readonly int GetHashCode() => HashCode.Combine(Origin, Direction);

    /// <summary>Check for equality with another ray.</summary>
    public static bool operator ==(Ray lhs, Ray rhs) => lhs.Origin == rhs.Origin && lhs.Direction == rhs.Direction;

    /// <summary>Check for inequality with another ray.</summary>
    public static bool operator !=(Ray lhs, Ray rhs) => lhs.Origin != rhs.Origin || lhs.Direction != rhs.Direction;

    /// <summary>Define from origin and direction. The direction will be normalized.</summary>
    public void Define(Vector3f origin, Vector3f direction)
    {
        Origin = origin;
        Direction = direction.Normalized;
    }

    /// <summary>Project a point on the ray.</summary>
    public readonly Vector3f Project(Vector3f point)
    {
        var offset = point - Origin;
        return Origin + offset.DotProduct(Direction) * Direction;
    }

    /// <summary>Return distance of a point from the ray.</summary>
    public readonly float Distance(Vector3f point)
    {
        var projected = Project(point);
        return (point - projected).Length;
    }

    /// <summary>Return closest point to another ray.</summary>
    public readonly Vector3f ClosestPoint(Ray ray)
    {
        // Algorithm based on http://paulbourke.net/geometry/lineline3d/
        var p13 = Origin - ray.Origin;
        var p43 = ray.Direction;
        var p21 = Direction;

        var d1343 = p13.DotProduct(p43);
        var d4321 = p43.DotProduct(p21);
        var d1321 = p13.DotProduct(p21);
        var d4343 = p43.DotProduct(p43);
        var d2121 = p21.DotProduct(p21);

        var d = d2121 * d4343 - d4321 * d4321;
        if (Math.Abs(d) < MathDefs.Epsilon)
            return Origin;
        var n = d1343 * d4321 - d1321 * d4343;
        var a = n / d;

        return Origin + a * Direction;
    }

    /// <summary>Return hit distance to a plane, or infinity if no hit.</summary>
    public readonly float HitDistance(Plane plane)
    {
        var d = plane.Normal.DotProduct(Direction);
        if (Math.Abs(d) >= MathDefs.Epsilon)
        {
            var t = -(plane.Normal.DotProduct(Origin) + plane.D) / d;
            if (t >= 0.0f)
                return t;
            else
                return float.PositiveInfinity;
        }
        else
            return float.PositiveInfinity;
    }

    /// <summary>Return hit distance to a bounding box, or infinity if no hit.</summary>
    public readonly float HitDistance(BoundingBox box) => HitDistanceAndNormal(box).Distance;

    /// <summary>Return hit distance to a bounding box with normal, or infinity if no hit.</summary>
    public readonly DistanceAndNormal HitDistanceAndNormal(BoundingBox box)
    {
        // If undefined, no hit (infinite distance)
        if (!box.Defined)
            return new DistanceAndNormal();

        // Check for ray origin being inside the box
        if (box.IsInside(Origin) != Intersection.Outside)
            return new DistanceAndNormal(-Direction, 0.0f);

        var result = new DistanceAndNormal();

        // Check for intersecting in the X-direction
        if (Origin.X < box.Min.X && Direction.X > 0.0f)
        {
            var x = (box.Min.X - Origin.X) / Direction.X;
            if (x < result.Distance)
            {
                var point = Origin + x * Direction;
                if (point.Y >= box.Min.Y && point.Y <= box.Max.Y && point.Z >= box.Min.Z && point.Z <= box.Max.Z)
                {
                    result.Distance = x;
                    result.Normal = new Vector3f(-1.0f, 0.0f, 0.0f);
                }
            }
        }
        if (Origin.X > box.Max.X && Direction.X < 0.0f)
        {
            var x = (box.Max.X - Origin.X) / Direction.X;
            if (x < result.Distance)
            {
                var point = Origin + x * Direction;
                if (point.Y >= box.Min.Y && point.Y <= box.Max.Y && point.Z >= box.Min.Z && point.Z <= box.Max.Z)
                {
                    result.Distance = x;
                    result.Normal = new Vector3f(1.0f, 0.0f, 0.0f);
                }
            }
        }
        // Check for intersecting in the Y-direction
        if (Origin.Y < box.Min.Y && Direction.Y > 0.0f)
        {
            var x = (box.Min.Y - Origin.Y) / Direction.Y;
            if (x < result.Distance)
            {
                var point = Origin + x * Direction;
                if (point.X >= box.Min.X && point.X <= box.Max.X && point.Z >= box.Min.Z && point.Z <= box.Max.Z)
                {
                    result.Distance = x;
                    result.Normal = new Vector3f(0.0f, -1.0f, 0.0f);
                }
            }
        }
        if (Origin.Y > box.Max.Y && Direction.Y < 0.0f)
        {
            var x = (box.Max.Y - Origin.Y) / Direction.Y;
            if (x < result.Distance)
            {
                var point = Origin + x * Direction;
                if (point.X >= box.Min.X && point.X <= box.Max.X && point.Z >= box.Min.Z && point.Z <= box.Max.Z)
                {
                    result.Distance = x;
                    result.Normal = new Vector3f(0.0f, 1.0f, 0.0f);
                }
            }
        }
        // Check for intersecting in the Z-direction
        if (Origin.Z < box.Min.Z && Direction.Z > 0.0f)
        {
            var x = (box.Min.Z - Origin.Z) / Direction.Z;
            if (x < result.Distance)
            {
                var point = Origin + x * Direction;
                if (point.X >= box.Min.X && point.X <= box.Max.X && point.Y >= box.Min.Y && point.Y <= box.Max.Y)
                {
                    result.Distance = x;
                    result.Normal = new Vector3f(0.0f, 0.0f, -1.0f);
                }
            }
        }
        if (Origin.Z > box.Max.Z && Direction.Z < 0.0f)
        {
            var x = (box.Max.Z - Origin.Z) / Direction.Z;
            if (x < result.Distance)
            {
                var point = Origin + x * Direction;
                if (point.X >= box.Min.X && point.X <= box.Max.X && point.Y >= box.Min.Y && point.Y <= box.Max.Y)
                {
                    result.Distance = x;
                    result.Normal = new Vector3f(0.0f, 0.0f, 1.0f);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Return hit distance to a frustum, or infinity if no hit. If solidInside parameter is true (default) rays
    /// originating from inside return zero distance, otherwise the distance to the closest plane.
    /// </summary>
    public readonly float HitDistance(Frustum frustum, bool solidInside = true)
    {
        var maxOutside = 0.0f;
        var minInside = float.PositiveInfinity;
        var allInside = true;

        foreach (var plane in frustum.Planes)
        {
            var distance = HitDistance(plane);

            if (plane.Distance(Origin) < 0.0f)
            {
                maxOutside = Math.Max(maxOutside, distance);
                allInside = false;
            }
            else
                minInside = Math.Min(minInside, distance);
        }

        if (allInside)
            return solidInside ? 0.0f : minInside;
        else if (maxOutside <= minInside)
            return maxOutside;
        else
            return float.PositiveInfinity;
    }

    /// <summary>Return hit distance to a sphere, or infinity if no hit.</summary>
    public readonly float HitDistance(Sphere sphere)
    {
        var centeredOrigin = Origin - sphere.Center;
        var squaredRadius = sphere.Radius * sphere.Radius;

        // Check if ray originates inside the sphere
        if (centeredOrigin.LengthSquared <= squaredRadius)
            return 0.0f;

        // Calculate intersection by quadratic equation
        var a = Direction.DotProduct(Direction);
        var b = 2.0f * centeredOrigin.DotProduct(Direction);
        var c = centeredOrigin.DotProduct(centeredOrigin) - squaredRadius;
        var discriminant = b * b - 4.0f * a * c;

        // No solution
        if (discriminant < 0.0f)
            return float.PositiveInfinity;

        // Get the nearer solution
        var sqrt = MathF.Sqrt(discriminant);
        var distance = (-b - sqrt) / (2.0f * a);
        return distance >= 0.0f ? distance : (-b + sqrt) / (2.0f * a);
    }

    /// <summary>Return hit distance to a triangle, or infinity if no hit. Optionally return hit normal and hit barycentric coordinate at intersect point.</summary>
    public readonly float HitDistance(Vector3f v0, Vector3f v1, Vector3f v2, out Vector3f outNormal, out Vector3f outBary)
    {
        // Based on Fast, Minimum Storage Ray/Triangle Intersection by Möller & Trumbore
        // http://www.graphics.cornell.edu/pubs/1997/MT97.pdf
        // Calculate edge vectors
        var edge1 = v1 - v0;
        var edge2 = v2 - v0;

        // Calculate determinant & check backfacing
        var p = Direction.CrossProduct(edge2);
        var det = edge1.DotProduct(p);
        if (det >= MathDefs.Epsilon)
        {
            // Calculate u & v parameters and test
            var t = Origin - v0;
            var u = t.DotProduct(p);
            if (u >= 0.0f && u <= det)
            {
                var q = t.CrossProduct(edge1);
                var v = Direction.DotProduct(q);
                if (v >= 0.0f && u + v <= det)
                {
                    var distance = edge2.DotProduct(q) / det;
                    // Discard hits behind the ray
                    if (distance >= 0.0f)
                    {
                        // There is an intersection, so calculate distance & optional normal
                        outNormal = edge1.CrossProduct(edge2);
                        outBary = new Vector3f(1.0f - u / det - v / det, u / det, v / det);
                        return distance;
                    }
                }
            }
        }

        outNormal = Vector3f.Zero;
        outBary = Vector3f.Zero;
        return float.PositiveInfinity;
    }

    /// <summary>Return hit distance to non-indexed geometry data, or infinity if no hit. Optionally return hit normal and hit uv coordinates at intersect point.</summary>
    public readonly float HitDistance(ReadOnlySpan<byte> vertexData, uint vertexStride, uint vertexStart, uint vertexCount,
        out Vector3f outNormal, out Vector2f outUv, uint uvOffset = 0)
    {
        var nearest = float.PositiveInfinity;
        var nearestIndex = uint.MaxValue;
        outNormal = Vector3f.Zero;
        outUv = Vector2f.Zero;
        var nearestBarycentric = Vector3f.Zero;

        for (var index = 0u; index + 2 < vertexCount; index += 3)
        {
            var v0 = ReadVector3(vertexData, (int)((vertexStart + index) * vertexStride));
            var v1 = ReadVector3(vertexData, (int)((vertexStart + index + 1) * vertexStride));
            var v2 = ReadVector3(vertexData, (int)((vertexStart + index + 2) * vertexStride));
            var distance = HitDistance(v0, v1, v2, out var normal, out var barycentric);
            if (distance < nearest)
            {
                nearest = distance;
                nearestIndex = index;
                outNormal = normal;
                nearestBarycentric = barycentric;
            }
        }

        if (nearestIndex == uint.MaxValue)
            outUv = Vector2f.Zero;
        else
        {
            // Interpolate the UV coordinate using barycentric coordinate
            var uv0 = ReadVector2(vertexData, (int)(uvOffset + (vertexStart + nearestIndex) * vertexStride));
            var uv1 = ReadVector2(vertexData, (int)(uvOffset + (vertexStart + nearestIndex + 1) * vertexStride));
            var uv2 = ReadVector2(vertexData, (int)(uvOffset + (vertexStart + nearestIndex + 2) * vertexStride));
            outUv = uv0 * nearestBarycentric.X + uv1 * nearestBarycentric.Y + uv2 * nearestBarycentric.Z;
        }

        return nearest;
    }

    /// <summary>Return hit distance to indexed geometry data, or infinity if no hit. Optionally return hit normal and hit uv coordinates at intersect point.</summary>
    public readonly float HitDistance(ReadOnlySpan<byte> vertexData, uint vertexStride, ReadOnlySpan<byte> indexData, uint indexSize,
        uint indexStart, uint indexCount, out Vector3f outNormal, out Vector2f outUv, uint uvOffset = 0)
    {
        var nearest = float.PositiveInfinity;
        outNormal = Vector3f.Zero;
        outUv = Vector2f.Zero;
        var nearestBarycentric = Vector3f.Zero;

        // 16-bit indices
        if (indexSize == sizeof(ushort))
        {
            var nearestIndex = uint.MaxValue;
            for (var index = indexStart; index < indexStart + indexCount; index += 3)
            {
                var i0 = MemoryMarshal.Read<ushort>(indexData[(int)(index * sizeof(ushort))..]);
                var i1 = MemoryMarshal.Read<ushort>(indexData[(int)((index + 1) * sizeof(ushort))..]);
                var i2 = MemoryMarshal.Read<ushort>(indexData[(int)((index + 2) * sizeof(ushort))..]);
                var distance = HitDistance(ReadVector3(vertexData, (int)(i0 * vertexStride)), ReadVector3(vertexData, (int)(i1 * vertexStride)),
                    ReadVector3(vertexData, (int)(i2 * vertexStride)), out var normal, out var barycentric);
                if (distance < nearest)
                {
                    nearestIndex = index;
                    nearest = distance;

                    outNormal = normal;
                    nearestBarycentric = barycentric;
                }
            }

            if (nearestIndex == uint.MaxValue)
                outUv = Vector2f.Zero;
            else
            {
                // Interpolate the UV coordinate using barycentric coordinate
                var i0 = MemoryMarshal.Read<ushort>(indexData[(int)(nearestIndex * sizeof(ushort))..]);
                var i1 = MemoryMarshal.Read<ushort>(indexData[(int)((nearestIndex + 1) * sizeof(ushort))..]);
                var i2 = MemoryMarshal.Read<ushort>(indexData[(int)((nearestIndex + 2) * sizeof(ushort))..]);
                var uv0 = ReadVector2(vertexData, (int)(uvOffset + i0 * vertexStride));
                var uv1 = ReadVector2(vertexData, (int)(uvOffset + i1 * vertexStride));
                var uv2 = ReadVector2(vertexData, (int)(uvOffset + i2 * vertexStride));
                outUv = new Vector2f(
                    uv0.X * nearestBarycentric.X + uv1.X * nearestBarycentric.Y + uv2.X * nearestBarycentric.Z,
                    uv0.Y * nearestBarycentric.X + uv1.Y * nearestBarycentric.Y + uv2.Y * nearestBarycentric.Z);
            }
        }
        // 32-bit indices
        else
        {
            var nearestIndex = uint.MaxValue;
            for (var index = indexStart; index < indexStart + indexCount; index += 3)
            {
                var i0 = MemoryMarshal.Read<uint>(indexData[(int)(index * sizeof(uint))..]);
                var i1 = MemoryMarshal.Read<uint>(indexData[(int)((index + 1) * sizeof(uint))..]);
                var i2 = MemoryMarshal.Read<uint>(indexData[(int)((index + 2) * sizeof(uint))..]);
                var distance = HitDistance(ReadVector3(vertexData, (int)(i0 * vertexStride)), ReadVector3(vertexData, (int)(i1 * vertexStride)),
                    ReadVector3(vertexData, (int)(i2 * vertexStride)), out var normal, out var barycentric);
                if (distance < nearest)
                {
                    nearestIndex = index;
                    nearest = distance;

                    outNormal = normal;
                    nearestBarycentric = barycentric;
                }
            }

            if (nearestIndex == uint.MaxValue)
                outUv = Vector2f.Zero;
            else
            {
                // Interpolate the UV coordinate using barycentric coordinate
                var i0 = MemoryMarshal.Read<uint>(indexData[(int)(nearestIndex * sizeof(uint))..]);
                var i1 = MemoryMarshal.Read<uint>(indexData[(int)((nearestIndex + 1) * sizeof(uint))..]);
                var i2 = MemoryMarshal.Read<uint>(indexData[(int)((nearestIndex + 2) * sizeof(uint))..]);
                var uv0 = ReadVector2(vertexData, (int)(uvOffset + i0 * vertexStride));
                var uv1 = ReadVector2(vertexData, (int)(uvOffset + i1 * vertexStride));
                var uv2 = ReadVector2(vertexData, (int)(uvOffset + i2 * vertexStride));
                outUv = new Vector2f(
                    uv0.X * nearestBarycentric.X + uv1.X * nearestBarycentric.Y + uv2.X * nearestBarycentric.Z,
                    uv0.Y * nearestBarycentric.X + uv1.Y * nearestBarycentric.Y + uv2.Y * nearestBarycentric.Z);
            }
        }

        return nearest;
    }

    /// <summary>Return whether ray is inside non-indexed geometry.</summary>
    public readonly bool InsideGeometry(ReadOnlySpan<byte> vertexData, uint vertexStride, uint vertexStart, uint vertexCount)
    {
        var front = float.PositiveInfinity;
        var back = float.PositiveInfinity;
        for (var index = 0u; index + 2 < vertexCount; index += 3)
        {
            var v0 = ReadVector3(vertexData, (int)((vertexStart + index) * vertexStride));
            var v1 = ReadVector3(vertexData, (int)((vertexStart + index + 1) * vertexStride));
            var v2 = ReadVector3(vertexData, (int)((vertexStart + index + 2) * vertexStride));
            var frontFaceDistance = HitDistance(v0, v1, v2, out _, out _);
            var backFaceDistance = HitDistance(v2, v1, v0, out _, out _);
            front = MathF.Min(frontFaceDistance > 0.0f ? frontFaceDistance : float.PositiveInfinity, front);
            // A backwards face is just a regular one, with the vertices in the opposite order. This essentially checks backfaces by
            // checking reversed frontfaces
            back = MathF.Min(backFaceDistance > 0.0f ? backFaceDistance : float.PositiveInfinity, back);
        }

        // If the closest face is a backface, that means that the ray originates from the inside of the geometry
        // NOTE: there may be cases where both are equal, as in, no collision to either. This is prevented in the most likely case
        // (ray doesn't hit either) by this conditional
        if (front != float.PositiveInfinity || back != float.PositiveInfinity)
            return back < front;

        // It is still possible for two triangles to be equally distant from the triangle, however, this is extremely unlikely.
        // As such, it is safe to assume they are not
        return false;
    }

    /// <summary>Return whether ray is inside indexed geometry.</summary>
    public readonly bool InsideGeometry(ReadOnlySpan<byte> vertexData, uint vertexStride, ReadOnlySpan<byte> indexData, uint indexSize,
        uint indexStart, uint indexCount)
    {
        var front = float.PositiveInfinity;
        var back = float.PositiveInfinity;

        // 16-bit indices
        if (indexSize == sizeof(ushort))
        {
            for (var index = indexStart; index < indexStart + indexCount; index += 3)
            {
                var i0 = MemoryMarshal.Read<ushort>(indexData[(int)(index * sizeof(ushort))..]);
                var i1 = MemoryMarshal.Read<ushort>(indexData[(int)((index + 1) * sizeof(ushort))..]);
                var i2 = MemoryMarshal.Read<ushort>(indexData[(int)((index + 2) * sizeof(ushort))..]);
                var v0 = ReadVector3(vertexData, (int)(i0 * vertexStride));
                var v1 = ReadVector3(vertexData, (int)(i1 * vertexStride));
                var v2 = ReadVector3(vertexData, (int)(i2 * vertexStride));
                var frontFaceDistance = HitDistance(v0, v1, v2, out _, out _);
                var backFaceDistance = HitDistance(v2, v1, v0, out _, out _);
                front = MathF.Min(frontFaceDistance > 0.0f ? frontFaceDistance : float.PositiveInfinity, front);
                // A backwards face is just a regular one, with the vertices in the opposite order. This essentially checks backfaces by
                // checking reversed frontfaces
                back = MathF.Min(backFaceDistance > 0.0f ? backFaceDistance : float.PositiveInfinity, back);
            }
        }
        // 32-bit indices
        else
        {
            for (var index = indexStart; index < indexStart + indexCount; index += 3)
            {
                var i0 = MemoryMarshal.Read<uint>(indexData[(int)(index * sizeof(uint))..]);
                var i1 = MemoryMarshal.Read<uint>(indexData[(int)((index + 1) * sizeof(uint))..]);
                var i2 = MemoryMarshal.Read<uint>(indexData[(int)((index + 2) * sizeof(uint))..]);
                var v0 = ReadVector3(vertexData, (int)(i0 * vertexStride));
                var v1 = ReadVector3(vertexData, (int)(i1 * vertexStride));
                var v2 = ReadVector3(vertexData, (int)(i2 * vertexStride));
                var frontFaceDistance = HitDistance(v0, v1, v2, out _, out _);
                var backFaceDistance = HitDistance(v2, v1, v0, out _, out _);
                front = MathF.Min(frontFaceDistance > 0.0f ? frontFaceDistance : float.PositiveInfinity, front);
                // A backwards face is just a regular one, with the vertices in the opposite order. This essentially checks backfaces by
                // checking reversed frontfaces
                back = MathF.Min(backFaceDistance > 0.0f ? backFaceDistance : float.PositiveInfinity, back);
            }
        }

        // If the closest face is a backface, that means that the ray originates from the inside of the geometry
        // NOTE: there may be cases where both are equal, as in, no collision to either. This is prevented in the most likely case
        // (ray doesn't hit either) by this conditional
        if (front != float.PositiveInfinity || back != float.PositiveInfinity)
            return back < front;

        // It is still possible for two triangles to be equally distant from the triangle, however, this is extremely unlikely.
        // As such, it is safe to assume they are not
        return false;
    }

    /// <summary>Return transformed by a 3x4 matrix. This may result in a non-normalized direction.</summary>
    public readonly Ray Transformed(Matrix3x4 transform)
    {
        var ret = new Ray();
        ret.Origin = transform * Origin;
        ret.Direction = transform * Direction.ToVector4();
        return ret;
    }

    private static Vector3f ReadVector3(ReadOnlySpan<byte> data, int offset) => MemoryMarshal.Read<Vector3f>(data[offset..]);

    private static Vector2f ReadVector2(ReadOnlySpan<byte> data, int offset) => MemoryMarshal.Read<Vector2f>(data[offset..]);

    /// <summary>Ray origin.</summary>
    public Vector3f Origin;

    /// <summary>Ray direction.</summary>
    public Vector3f Direction;
}
