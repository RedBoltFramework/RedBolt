namespace RedBolt;

/// <summary>Result of light query for drawable.</summary>
public struct LightGeometryQueryResult
{
    public bool IsLit { get; set; }
    public bool IsForwardLit { get; set; }
    public bool IsShadowCaster { get; set; }
}

/// <summary>Frustum query for point light forward lit geometries and shadow casters.</summary>
public class PointLightGeometryQuery : SphereOctreeQuery
{
    /// <summary>Immutable.</summary>
    private readonly DrawableProcessor _drawableProcessor;
    private readonly uint _lightMask;
    private readonly uint _shadowViewMask;

    private bool _hasLitGeometries;
    private readonly List<Drawable>? _shadowCasters;

    /// <remarks>
    /// C++ takes <c>bool&amp; hasLitGeometries</c> and writes through the reference.
    /// C# exposes the same result as <see cref="HasLitGeometries"/> after the octree query runs.
    /// </remarks>
    public PointLightGeometryQuery(List<Drawable> result, List<Drawable>? shadowCasters,
        DrawableProcessor drawableProcessor, Light light, uint primaryViewMask, uint shadowViewMask)
        : base(result, GetLightSphere(light), DrawableFlags.Geometry, primaryViewMask | shadowViewMask)
    {
        _drawableProcessor = drawableProcessor;
        _lightMask = light.GetLightMaskEffective();
        _shadowViewMask = shadowViewMask;
        _shadowCasters = shadowCasters;
        _hasLitGeometries = false;
        shadowCasters?.Clear();
    }

    /// <summary>True when any lit geometry was found during the last query.</summary>
    public bool HasLitGeometries => _hasLitGeometries;

    public override void TestDrawables(IReadOnlyList<Drawable> drawables, bool inside)
    {
        for (var i = 0; i < drawables.Count; ++i)
        {
            var drawable = drawables[i];
            var result = IsLitOrShadowCaster(drawable, inside);
            if (result.IsLit)
                _hasLitGeometries = true;
            if (result.IsForwardLit)
                Result.Add(drawable);
            if (result.IsShadowCaster)
                _shadowCasters!.Add(drawable);
        }
    }

    private LightGeometryQueryResult IsLitOrShadowCaster(Drawable drawable, bool inside)
    {
        var drawableIndex = drawable.GetDrawableIndex();
        var geometryFlags = _drawableProcessor.GetGeometryRenderFlags(drawableIndex);

        var isInside = (drawable.GetDrawableFlags() & DrawableFlags) != 0
            && (drawable.GetViewMask() & ViewMask) != 0
            && (inside || Sphere.IsInsideFast(drawable.GetWorldBoundingBox()) != Intersection.Outside);
        var isLit = isInside
            && (geometryFlags & GeometryRenderFlag.Lit) != 0
            && (drawable.GetLightMaskInZone() & _lightMask) != 0;
        var isForwardLit = isLit && (geometryFlags & GeometryRenderFlag.ForwardLit) != 0;
        var isShadowCaster = _shadowCasters is not null && isInside
            && drawable.GetCastShadows()
            && (drawable.GetViewMask() & _shadowViewMask) != 0
            && (drawable.GetShadowMask() & _lightMask) != 0;
        return new LightGeometryQueryResult
        {
            IsLit = isLit,
            IsForwardLit = isForwardLit,
            IsShadowCaster = isShadowCaster
        };
    }

    private static Sphere GetLightSphere(Light light) =>
        new(light.Node!.GetWorldPosition(), light.GetRange());
}

/// <summary>Frustum query for spot light forward lit geometries and shadow casters.</summary>
public class SpotLightGeometryQuery : FrustumOctreeQuery
{
    /// <summary>Immutable.</summary>
    private readonly DrawableProcessor _drawableProcessor;
    private readonly uint _lightMask;
    private readonly uint _shadowViewMask;

    private bool _hasLitGeometries;
    private readonly List<Drawable>? _shadowCasters;

    /// <remarks>
    /// C++ takes <c>bool&amp; hasLitGeometries</c> and writes through the reference.
    /// C# exposes the same result as <see cref="HasLitGeometries"/> after the octree query runs.
    /// </remarks>
    public SpotLightGeometryQuery(List<Drawable> result, List<Drawable>? shadowCasters,
        DrawableProcessor drawableProcessor, Light light, uint primaryViewMask, uint shadowViewMask)
        : base(result, light.GetFrustum(), DrawableFlags.Geometry, primaryViewMask | shadowViewMask)
    {
        _drawableProcessor = drawableProcessor;
        _lightMask = light.GetLightMaskEffective();
        _shadowViewMask = shadowViewMask;
        _shadowCasters = shadowCasters;
        _hasLitGeometries = false;
        shadowCasters?.Clear();
    }

    /// <summary>True when any lit geometry was found during the last query.</summary>
    public bool HasLitGeometries => _hasLitGeometries;

    public override void TestDrawables(IReadOnlyList<Drawable> drawables, bool inside)
    {
        for (var i = 0; i < drawables.Count; ++i)
        {
            var drawable = drawables[i];
            var result = IsLitOrShadowCaster(drawable, inside);
            if (result.IsLit)
                _hasLitGeometries = true;
            if (result.IsForwardLit)
                Result.Add(drawable);
            if (result.IsShadowCaster)
                _shadowCasters!.Add(drawable);
        }
    }

    private LightGeometryQueryResult IsLitOrShadowCaster(Drawable drawable, bool inside)
    {
        var drawableIndex = drawable.GetDrawableIndex();
        var geometryFlags = _drawableProcessor.GetGeometryRenderFlags(drawableIndex);

        var isInside = (drawable.GetDrawableFlags() & DrawableFlags) != 0
            && (drawable.GetViewMask() & ViewMask) != 0
            && (inside || Frustum.IsInsideFast(drawable.GetWorldBoundingBox()) != Intersection.Outside);
        var isLit = isInside
            && (geometryFlags & GeometryRenderFlag.Lit) != 0
            && (drawable.GetLightMaskInZone() & _lightMask) != 0;
        var isForwardLit = isLit && (geometryFlags & GeometryRenderFlag.ForwardLit) != 0;
        var isShadowCaster = _shadowCasters is not null && isInside
            && drawable.GetCastShadows()
            && (drawable.GetViewMask() & _shadowViewMask) != 0
            && (drawable.GetShadowMask() & _lightMask) != 0;
        return new LightGeometryQueryResult
        {
            IsLit = isLit,
            IsForwardLit = isForwardLit,
            IsShadowCaster = isShadowCaster
        };
    }
}

/// <summary>Frustum query for directional light shadow casters.</summary>
public class DirectionalLightShadowCasterQuery : FrustumOctreeQuery
{
    private readonly uint _lightMask;

    public DirectionalLightShadowCasterQuery(List<Drawable> result,
        Frustum frustum, DrawableFlags drawableFlags, Light light, uint viewMask)
        : base(result, frustum, drawableFlags, viewMask)
    {
        _lightMask = light.GetLightMask();
    }

    public override void TestDrawables(IReadOnlyList<Drawable> drawables, bool inside)
    {
        for (var i = 0; i < drawables.Count; ++i)
        {
            var drawable = drawables[i];
            if (IsShadowCaster(drawable, inside))
                Result.Add(drawable);
        }
    }

    private bool IsShadowCaster(Drawable drawable, bool inside) =>
        drawable.GetCastShadows()
        && (drawable.GetDrawableFlags() & DrawableFlags) != 0
        && (drawable.GetViewMask() & ViewMask) != 0
        && (drawable.GetShadowMask() & _lightMask) != 0
        && (inside || Frustum.IsInsideFast(drawable.GetWorldBoundingBox()) != Intersection.Outside);
}
