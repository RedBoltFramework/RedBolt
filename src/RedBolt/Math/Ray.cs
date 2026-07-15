using System.Runtime.InteropServices;

namespace RedBolt;

public readonly record struct DistanceAndNormal(Vector3f Normal, float Distance)
{
    public DistanceAndNormal() : this(Vector3f.Zero, float.PositiveInfinity) { }
}

[StructLayout(LayoutKind.Sequential)]
public struct Ray : IEquatable<Ray>, IApproximateEquatable<Ray>
{
    public Vector3f Origin;
    public Vector3f Direction;

    public Ray(Vector3f origin, Vector3f direction)
    {
        Origin = origin;
        Direction = direction.Normalized;
    }

    public Vector3f Project(Vector3f point)
    {
        var offset = point - Origin;
        return Origin + offset.DotProduct(Direction) * Direction;
    }

    public float Distance(Vector3f point) => (point - Project(point)).Length;

    public Vector3f ClosestPoint(Ray ray)
    {
        var p13 = Origin - ray.Origin;
        var p43 = ray.Direction;
        var p21 = Direction;
        var d1343 = p13.DotProduct(p43);
        var d4321 = p43.DotProduct(p21);
        var d1321 = p13.DotProduct(p21);
        var d4343 = p43.DotProduct(p43);
        var d2121 = p21.DotProduct(p21);
        var denominator = d2121 * d4343 - d4321 * d4321;
        if (Math.Abs(denominator) < MathDefs.Epsilon)
            return Origin;
        var numerator = d1343 * d4321 - d1321 * d4343;
        return Origin + numerator / denominator * Direction;
    }

    public void Define(Vector3f origin, Vector3f direction)
    {
        Origin = origin;
        Direction = direction.Normalized;
    }

    public float HitDistance(Plane plane)
    {
        var denominator = plane.Normal.DotProduct(Direction);
        if (Math.Abs(denominator) < MathDefs.Epsilon)
            return float.PositiveInfinity;

        var distance = -(plane.Normal.DotProduct(Origin) + plane.D) / denominator;
        return distance >= 0.0f ? distance : float.PositiveInfinity;
    }

    /// <summary>Return hit distance to a frustum.</summary>
    // C++: solidInside defaults to true
    public float HitDistance(Frustum frustum, bool solidInside = false)
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
        if (maxOutside <= minInside)
            return maxOutside;
        return float.PositiveInfinity;
    }

    public float HitDistance(BoundingBox box)
    {
        if (!box.Defined)
            return float.PositiveInfinity;
        if (box.IsInside(Origin) != Intersection.Outside)
            return 0.0f;

        var distance = float.PositiveInfinity;
        CheckPlane(box.Min.X, Direction.X, Origin.X, axis: 0, box, ref distance);
        CheckPlane(box.Max.X, Direction.X, Origin.X, axis: 0, box, ref distance);
        CheckPlane(box.Min.Y, Direction.Y, Origin.Y, axis: 1, box, ref distance);
        CheckPlane(box.Max.Y, Direction.Y, Origin.Y, axis: 1, box, ref distance);
        CheckPlane(box.Min.Z, Direction.Z, Origin.Z, axis: 2, box, ref distance);
        CheckPlane(box.Max.Z, Direction.Z, Origin.Z, axis: 2, box, ref distance);
        return distance;
    }

    public DistanceAndNormal HitDistanceAndNormal(BoundingBox box)
    {
        if (!box.Defined)
            return new DistanceAndNormal();
        if (box.IsInside(Origin) != Intersection.Outside)
            return new DistanceAndNormal(-Direction, 0.0f);

        var result = new DistanceAndNormal();
        CheckBoxFace(box.Min.X, Direction.X, Origin.X, 0, -1.0f, box, ref result);
        CheckBoxFace(box.Max.X, Direction.X, Origin.X, 0, 1.0f, box, ref result);
        CheckBoxFace(box.Min.Y, Direction.Y, Origin.Y, 1, -1.0f, box, ref result);
        CheckBoxFace(box.Max.Y, Direction.Y, Origin.Y, 1, 1.0f, box, ref result);
        CheckBoxFace(box.Min.Z, Direction.Z, Origin.Z, 2, -1.0f, box, ref result);
        CheckBoxFace(box.Max.Z, Direction.Z, Origin.Z, 2, 1.0f, box, ref result);
        return result;
    }

    public float HitDistance(Sphere sphere)
    {
        var centeredOrigin = Origin - sphere.Center;
        var squaredRadius = sphere.Radius * sphere.Radius;
        if (centeredOrigin.LengthSquared <= squaredRadius)
            return 0.0f;

        var a = Direction.DotProduct(Direction);
        var b = 2.0f * centeredOrigin.DotProduct(Direction);
        var c = centeredOrigin.DotProduct(centeredOrigin) - squaredRadius;
        var discriminant = b * b - 4.0f * a * c;
        if (discriminant < 0.0f)
            return float.PositiveInfinity;

        var sqrt = MathF.Sqrt(discriminant);
        var distance = (-b - sqrt) / (2.0f * a);
        return distance >= 0.0f ? distance : (-b + sqrt) / (2.0f * a);
    }

    public float HitDistance(Vector3f v0, Vector3f v1, Vector3f v2, out Vector3f outNormal, out Vector3f outBary)
    {
        var edge1 = v1 - v0;
        var edge2 = v2 - v0;
        var p = Direction.CrossProduct(edge2);
        var det = edge1.DotProduct(p);
        if (det >= MathDefs.Epsilon)
        {
            var t = Origin - v0;
            var u = t.DotProduct(p);
            if (u >= 0.0f && u <= det)
            {
                var q = t.CrossProduct(edge1);
                var v = Direction.DotProduct(q);
                if (v >= 0.0f && u + v <= det)
                {
                    var distance = edge2.DotProduct(q) / det;
                    if (distance >= 0.0f)
                    {
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

    public Ray Transformed(Matrix3x4 transform)
    {
        return new Ray(transform * Origin, transform.RotationMatrix * Direction);
    }

    public float HitDistance(ReadOnlySpan<byte> vertexData, int vertexStride, int vertexStart, int vertexCount,
        out Vector3f outNormal, out Vector2f outUv, int uvOffset = 0)
    {
        var nearest = float.PositiveInfinity;
        var nearestIndex = -1;
        outNormal = Vector3f.Zero;
        outUv = Vector2f.Zero;
        var nearestBarycentric = Vector3f.Zero;

        for (var index = 0; index + 2 < vertexCount; index += 3)
        {
            var v0 = ReadVector3(vertexData, (vertexStart + index) * vertexStride);
            var v1 = ReadVector3(vertexData, (vertexStart + index + 1) * vertexStride);
            var v2 = ReadVector3(vertexData, (vertexStart + index + 2) * vertexStride);
            var distance = HitDistance(v0, v1, v2, out var normal, out var barycentric);
            if (distance < nearest)
            {
                nearest = distance;
                nearestIndex = index;
                outNormal = normal;
                nearestBarycentric = barycentric;
            }
        }

        if (nearestIndex >= 0)
        {
            var uv0 = ReadVector2(vertexData, (vertexStart + nearestIndex) * vertexStride + uvOffset);
            var uv1 = ReadVector2(vertexData, (vertexStart + nearestIndex + 1) * vertexStride + uvOffset);
            var uv2 = ReadVector2(vertexData, (vertexStart + nearestIndex + 2) * vertexStride + uvOffset);
            outUv = uv0 * nearestBarycentric.X + uv1 * nearestBarycentric.Y + uv2 * nearestBarycentric.Z;
        }
        else
            outUv = Vector2f.Zero;

        return nearest;
    }

    public float HitDistance(ReadOnlySpan<byte> vertexData, int vertexStride, ReadOnlySpan<byte> indexData, int indexSize,
        int indexStart, int indexCount, out Vector3f outNormal, out Vector2f outUv, int uvOffset = 0)
    {
        var nearest = float.PositiveInfinity;
        var nearestIndex0 = 0;
        var nearestIndex1 = 0;
        var nearestIndex2 = 0;
        var hasNearest = false;
        outNormal = Vector3f.Zero;
        outUv = Vector2f.Zero;
        var nearestBarycentric = Vector3f.Zero;

        for (var index = indexStart; index + 2 < indexStart + indexCount; index += 3)
        {
            var i0 = ReadIndex(indexData, index, indexSize);
            var i1 = ReadIndex(indexData, index + 1, indexSize);
            var i2 = ReadIndex(indexData, index + 2, indexSize);
            var distance = HitDistance(ReadVector3(vertexData, i0 * vertexStride), ReadVector3(vertexData, i1 * vertexStride),
                ReadVector3(vertexData, i2 * vertexStride), out var normal, out var barycentric);
            if (distance < nearest)
            {
                nearest = distance;
                nearestIndex0 = i0;
                nearestIndex1 = i1;
                nearestIndex2 = i2;
                hasNearest = true;
                outNormal = normal;
                nearestBarycentric = barycentric;
            }
        }

        if (hasNearest)
        {
            var uv0 = ReadVector2(vertexData, nearestIndex0 * vertexStride + uvOffset);
            var uv1 = ReadVector2(vertexData, nearestIndex1 * vertexStride + uvOffset);
            var uv2 = ReadVector2(vertexData, nearestIndex2 * vertexStride + uvOffset);
            outUv = uv0 * nearestBarycentric.X + uv1 * nearestBarycentric.Y + uv2 * nearestBarycentric.Z;
        }
        else
            outUv = Vector2f.Zero;

        return nearest;
    }

    public bool InsideGeometry(ReadOnlySpan<byte> vertexData, int vertexStride, int vertexStart, int vertexCount)
    {
        var front = float.PositiveInfinity;
        var back = float.PositiveInfinity;
        for (var index = 0; index + 2 < vertexCount; index += 3)
        {
            var v0 = ReadVector3(vertexData, (vertexStart + index) * vertexStride);
            var v1 = ReadVector3(vertexData, (vertexStart + index + 1) * vertexStride);
            var v2 = ReadVector3(vertexData, (vertexStart + index + 2) * vertexStride);
            UpdateInsideDistances(v0, v1, v2, ref front, ref back);
        }
        return back < front;
    }

    public bool InsideGeometry(ReadOnlySpan<byte> vertexData, int vertexStride, ReadOnlySpan<byte> indexData, int indexSize,
        int indexStart, int indexCount)
    {
        var front = float.PositiveInfinity;
        var back = float.PositiveInfinity;
        for (var index = indexStart; index + 2 < indexStart + indexCount; index += 3)
        {
            var v0 = ReadVector3(vertexData, ReadIndex(indexData, index, indexSize) * vertexStride);
            var v1 = ReadVector3(vertexData, ReadIndex(indexData, index + 1, indexSize) * vertexStride);
            var v2 = ReadVector3(vertexData, ReadIndex(indexData, index + 2, indexSize) * vertexStride);
            UpdateInsideDistances(v0, v1, v2, ref front, ref back);
        }
        return back < front;
    }

    private static Vector3f ReadVector3(ReadOnlySpan<byte> data, int offset) => MemoryMarshal.Read<Vector3f>(data[offset..]);

    private static Vector2f ReadVector2(ReadOnlySpan<byte> data, int offset) => MemoryMarshal.Read<Vector2f>(data[offset..]);

    private static int ReadIndex(ReadOnlySpan<byte> data, int index, int indexSize) => indexSize == sizeof(ushort)
        ? MemoryMarshal.Read<ushort>(data[(index * sizeof(ushort))..])
        : checked((int)MemoryMarshal.Read<uint>(data[(index * sizeof(uint))..]));

    private void UpdateInsideDistances(Vector3f v0, Vector3f v1, Vector3f v2, ref float front, ref float back)
    {
        var frontDistance = HitDistance(v0, v1, v2, out _, out _);
        var backDistance = HitDistance(v2, v1, v0, out _, out _);
        if (frontDistance > 0.0f)
            front = MathF.Min(frontDistance, front);
        if (backDistance > 0.0f)
            back = MathF.Min(backDistance, back);
    }

    public bool ApproximatelyEquivalent(Ray rhs, float epsilon = MathDefs.Epsilon) =>
        Origin.ApproximatelyEquivalent(rhs.Origin, epsilon) && Direction.ApproximatelyEquivalent(rhs.Direction, epsilon);

    public bool Equals(Ray other) => Origin.Equals(other.Origin) && Direction.Equals(other.Direction);
    public override bool Equals(object? obj) => obj is Ray other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Origin, Direction);
    public static bool operator ==(Ray lhs, Ray rhs) => lhs.Equals(rhs);
    public static bool operator !=(Ray lhs, Ray rhs) => !lhs.Equals(rhs);

    private void CheckPlane(float planeCoordinate, float directionComponent, float originComponent, int axis, BoundingBox box, ref float distance)
    {
        if (Math.Abs(directionComponent) <= MathDefs.Epsilon)
            return;

        var candidate = (planeCoordinate - originComponent) / directionComponent;
        if (candidate < 0.0f || candidate >= distance)
            return;

        var point = Origin + candidate * Direction;
        var inside = axis switch
        {
            0 => point.Y >= box.Min.Y && point.Y <= box.Max.Y && point.Z >= box.Min.Z && point.Z <= box.Max.Z,
            1 => point.X >= box.Min.X && point.X <= box.Max.X && point.Z >= box.Min.Z && point.Z <= box.Max.Z,
            _ => point.X >= box.Min.X && point.X <= box.Max.X && point.Y >= box.Min.Y && point.Y <= box.Max.Y
        };
        if (inside)
            distance = candidate;
    }

    private void CheckBoxFace(float plane, float direction, float origin, int axis, float normalSign,
        BoundingBox box, ref DistanceAndNormal result)
    {
        if ((normalSign < 0.0f && !(origin < plane && direction > 0.0f))
            || (normalSign > 0.0f && !(origin > plane && direction < 0.0f)))
            return;
        var distance = (plane - origin) / direction;
        if (distance >= result.Distance)
            return;
        var point = Origin + distance * Direction;
        var insideFace = axis switch
        {
            0 => point.Y >= box.Min.Y && point.Y <= box.Max.Y && point.Z >= box.Min.Z && point.Z <= box.Max.Z,
            1 => point.X >= box.Min.X && point.X <= box.Max.X && point.Z >= box.Min.Z && point.Z <= box.Max.Z,
            _ => point.X >= box.Min.X && point.X <= box.Max.X && point.Y >= box.Min.Y && point.Y <= box.Max.Y
        };
        if (!insideFace)
            return;
        var normal = axis switch
        {
            0 => new Vector3f(normalSign, 0, 0),
            1 => new Vector3f(0, normalSign, 0),
            _ => new Vector3f(0, 0, normalSign)
        };
        result = new DistanceAndNormal(normal, distance);
    }
}
