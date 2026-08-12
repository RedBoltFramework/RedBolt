namespace RedBolt.PhysicsJolt;

using RedBolt.Bindings.Jolt;
using RedBolt.Math;

internal static class JoltPhysicsQuery
{
    internal readonly record struct Hit(
        uint BodyId, Vector3f Position, Vector3f Normal, float Fraction);

    internal static Hit[] Raycast(PhysicsSystem physicsSystem, Ray ray,
        BroadPhaseLayerFilter broadPhaseLayerFilter, ObjectLayerFilter objectLayerFilter)
    {
        using var settings = new RayCastSettings();
        using var collector = new RedBoltAllHitCastRayCollector();
        physicsSystem.GetNarrowPhaseQuery().CastRay(
            in ray, settings, collector, broadPhaseLayerFilter, objectLayerFilter);
        collector.Sort();

        using var hits = collector.MHits;
        var result = new Hit[hits.Count];
        var bodyInterface = physicsSystem.GetBodyInterface();
        for (var i = 0; i < result.Length; ++i)
        {
            var nativeHit = hits[i];
            var position = ray.Origin + ray.Direction * nativeHit.Fraction;
            var normal = Vector3f.Zero;
            if (bodyInterface.IsAdded(in nativeHit.BodyId))
            {
                using var transformedShape = bodyInterface.GetTransformedShape(in nativeHit.BodyId);
                normal = transformedShape.GetWorldSpaceSurfaceNormal(in nativeHit.SubShapeId, position);
            }
            result[i] = new Hit(nativeHit.BodyId, position, normal, nativeHit.Fraction);
        }
        return result;
    }

    internal static bool SphereCast(PhysicsSystem physicsSystem, Ray ray, float radius,
        BroadPhaseLayerFilter broadPhaseLayerFilter, ObjectLayerFilter objectLayerFilter, out Hit hit)
    {
        using var shape = new SphereShape(MathF.Max(radius, 0.001f));
        return CastShape(physicsSystem, shape, Vector3f.One, ray.Origin, Quaternion.IDENTITY,
            ray.Direction, uint.MaxValue, broadPhaseLayerFilter, objectLayerFilter, out hit);
    }

    internal static bool ConvexCast(PhysicsSystem physicsSystem, Shape shape, Vector3f scale,
        Vector3f startPosition, Quaternion startRotation, Vector3f endPosition, uint ignoreBodyId,
        BroadPhaseLayerFilter broadPhaseLayerFilter, ObjectLayerFilter objectLayerFilter, out Hit hit) =>
        CastShape(physicsSystem, shape, scale, startPosition, startRotation, endPosition - startPosition,
            ignoreBodyId, broadPhaseLayerFilter, objectLayerFilter, out hit);

    internal static uint[] QuerySphere(PhysicsSystem physicsSystem, Vector3f center, float radius,
        BroadPhaseLayerFilter broadPhaseLayerFilter, ObjectLayerFilter objectLayerFilter)
    {
        using var shape = new SphereShape(MathF.Max(radius, 0.001f));
        return CollideShape(physicsSystem, shape, center, Quaternion.IDENTITY, uint.MaxValue,
            broadPhaseLayerFilter, objectLayerFilter);
    }

    internal static uint[] QueryBox(PhysicsSystem physicsSystem, Vector3f minimum, Vector3f maximum,
        BroadPhaseLayerFilter broadPhaseLayerFilter, ObjectLayerFilter objectLayerFilter)
    {
        var halfExtent = (maximum - minimum) * 0.5f;
        halfExtent = new Vector3f(
            MathF.Max(halfExtent.X, 0.001f),
            MathF.Max(halfExtent.Y, 0.001f),
            MathF.Max(halfExtent.Z, 0.001f));
        using var shape = new BoxShape(halfExtent);
        return CollideShape(physicsSystem, shape, (minimum + maximum) * 0.5f, Quaternion.IDENTITY,
            uint.MaxValue, broadPhaseLayerFilter, objectLayerFilter);
    }

    internal static uint[] QueryShape(PhysicsSystem physicsSystem, Shape shape, Vector3f position,
        Quaternion rotation, uint ignoreBodyId, BroadPhaseLayerFilter broadPhaseLayerFilter,
        ObjectLayerFilter objectLayerFilter) =>
        CollideShape(physicsSystem, shape, position, rotation, ignoreBodyId,
            broadPhaseLayerFilter, objectLayerFilter);

    private static bool CastShape(PhysicsSystem physicsSystem, Shape shape, Vector3f scale,
        Vector3f startPosition, Quaternion startRotation, Vector3f direction, uint ignoreBodyId,
        BroadPhaseLayerFilter broadPhaseLayerFilter, ObjectLayerFilter objectLayerFilter, out Hit hit)
    {
        using var worldTransform = Mat44.SRotationTranslation(startRotation, startPosition);
        using var centerOfMassTransform = worldTransform.PreTranslated(shape.GetCenterOfMass());
        using var shapeCast = new RShapeCast(shape, scale, centerOfMassTransform, direction);
        using var settings = new ShapeCastSettings();
        using var collector = new RedBoltClosestCastShapeCollector();

        var query = physicsSystem.GetNarrowPhaseQuery();
        if (ignoreBodyId == uint.MaxValue)
            query.CastShape(shapeCast, settings, Vector3f.Zero, collector,
                broadPhaseLayerFilter, objectLayerFilter);
        else
        {
            using var bodyFilter = new IgnoreSingleBodyFilter(in ignoreBodyId);
            query.CastShape(shapeCast, settings, Vector3f.Zero, collector,
                broadPhaseLayerFilter, objectLayerFilter, bodyFilter);
        }

        if (!collector.HadHit())
        {
            hit = default;
            return false;
        }

        using var nativeHit = collector.MHit;
        hit = new Hit(nativeHit.MBodyID2, nativeHit.MContactPointOn2,
            (-nativeHit.MPenetrationAxis).NormalizedOrDefault(Vector3f.Up), nativeHit.MFraction);
        return true;
    }

    private static uint[] CollideShape(PhysicsSystem physicsSystem, Shape shape, Vector3f position,
        Quaternion rotation, uint ignoreBodyId, BroadPhaseLayerFilter broadPhaseLayerFilter,
        ObjectLayerFilter objectLayerFilter)
    {
        using var transform = Mat44.SRotationTranslation(rotation, position);
        using var settings = new CollideShapeSettings();
        using var collector = new RedBoltAllHitCollideShapeCollector();
        var query = physicsSystem.GetNarrowPhaseQuery();
        if (ignoreBodyId == uint.MaxValue)
            query.CollideShape(shape, Vector3f.One, transform, settings, Vector3f.Zero, collector,
                broadPhaseLayerFilter, objectLayerFilter);
        else
        {
            using var bodyFilter = new IgnoreSingleBodyFilter(in ignoreBodyId);
            query.CollideShape(shape, Vector3f.One, transform, settings, Vector3f.Zero, collector,
                broadPhaseLayerFilter, objectLayerFilter, bodyFilter);
        }

        var unique = new HashSet<uint>();
        using var hits = collector.MHits;
        for (var i = 0; i < hits.Count; ++i)
        {
            using var nativeHit = hits[i];
            unique.Add(nativeHit.MBodyID2);
        }
        return [.. unique];
    }
}
