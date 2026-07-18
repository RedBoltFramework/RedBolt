using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace RedBolt;

/// <summary>Component that describes global rendering properties.</summary>
/// <remarks>
/// </remarks>
public class Zone : Drawable
{
    private const float DefaultFogStart = 250.0f;
    private const float DefaultFogEnd = 1000.0f;
    private const float DefaultFogHeight = 0.0f;
    private const float DefaultFogHeightScale = 0.5f;

    private static readonly Vector3f DefaultBoundingBoxMin = new(-10.0f, -10.0f, -10.0f);
    private static readonly Vector3f DefaultBoundingBoxMax = new(10.0f, 10.0f, 10.0f);
    private static readonly Color DefaultAmbientColor = new(0.1f, 0.1f, 0.1f);
    private static readonly Color DefaultFogColor = Color.Black;

    private Matrix3x4 _inverseWorld = Matrix3x4.Identity;
    private bool _inverseWorldDirty = true;
    private bool _heightFog;
    private bool _override;
    private bool _ambientGradient;
    private BoundingBox _lastWorldBoundingBox;
    private Color _ambientColor = DefaultAmbientColor;
    private float _ambientBrightness = 1.0f;
    private float _backgroundBrightness;
    private bool _backgroundStatic;
    private Color _ambientStartColor;
    private Color _ambientEndColor;
    private Color _fogColor = DefaultFogColor;
    private float _fogStart = DefaultFogStart;
    private float _fogEnd = DefaultFogEnd;
    private float _fogHeight = DefaultFogHeight;
    private float _fogHeightScale = DefaultFogHeightScale;
    private int _priority;
    private Texture? _zoneTexture;
    private readonly WeakReference<Zone> _lastAmbientStartZone = new(null!);
    private readonly WeakReference<Zone> _lastAmbientEndZone = new(null!);
    private readonly ThreadSafeCache<ReflectionProbeData> _reflectionProbeData = new();
    private readonly ThreadSafeCache<Vector3f> _cachedAmbientLighting = new();
    private readonly ThreadSafeCache<SphericalHarmonicsDot9> _cachedTextureLighting = new();
    private readonly ThreadSafeCache<SphericalHarmonicsDot9> _cachedAmbientAndBackgroundLighting = new();

    public Zone()
        : base(DrawableFlags.Zone)
    {
        _boundingBox = new BoundingBox(DefaultBoundingBoxMin, DefaultBoundingBoxMax);

        RegisterAttribute("Bounding Box Min", typeof(Vector3f), self => ((Zone)self)._boundingBox.Min,
            (self, value) =>
            {
                var zone = (Zone)self;
                zone._boundingBox.Min = (Vector3f)value!;
                zone.MarkNodeDirty();
            }, DefaultBoundingBoxMin);
        RegisterAttribute("Bounding Box Max", typeof(Vector3f), self => ((Zone)self)._boundingBox.Max,
            (self, value) =>
            {
                var zone = (Zone)self;
                zone._boundingBox.Max = (Vector3f)value!;
                zone.MarkNodeDirty();
            }, DefaultBoundingBoxMax);
        RegisterAttribute("Ambient Color", typeof(Color), self => ((Zone)self)._ambientColor,
            (self, value) => ((Zone)self).SetAmbientColor((Color)value!), DefaultAmbientColor);
        RegisterAttribute("Ambient Brightness", typeof(float), self => ((Zone)self)._ambientBrightness,
            (self, value) => ((Zone)self).SetAmbientBrightness((float)value!), 1.0f);
        RegisterAttribute("Background Brightness", typeof(float), self => ((Zone)self)._backgroundBrightness,
            (self, value) => ((Zone)self).SetBackgroundBrightness((float)value!), 0.0f);
        RegisterAttribute("Is Background Static", typeof(bool), self => ((Zone)self)._backgroundStatic,
            (self, value) => ((Zone)self).SetBackgroundStatic(value is bool isStatic && isStatic), false);
        RegisterAttribute("Fog Color", typeof(Color), self => ((Zone)self)._fogColor,
            (self, value) => ((Zone)self).SetFogColor((Color)value!), DefaultFogColor);
        RegisterAttribute("Fog Start", typeof(float), self => ((Zone)self)._fogStart,
            (self, value) => ((Zone)self)._fogStart = (float)value!, DefaultFogStart);
        RegisterAttribute("Fog End", typeof(float), self => ((Zone)self)._fogEnd,
            (self, value) => ((Zone)self)._fogEnd = (float)value!, DefaultFogEnd);
        RegisterAttribute("Fog Height", typeof(float), self => ((Zone)self)._fogHeight,
            (self, value) => ((Zone)self)._fogHeight = (float)value!, DefaultFogHeight);
        RegisterAttribute("Fog Height Scale", typeof(float), self => ((Zone)self)._fogHeightScale,
            (self, value) => ((Zone)self)._fogHeightScale = (float)value!, DefaultFogHeightScale);
        RegisterAttribute("Height Fog Mode", typeof(bool), self => ((Zone)self)._heightFog,
            (self, value) => ((Zone)self)._heightFog = value is bool enabled && enabled, false);
        RegisterAttribute("Override Mode", typeof(bool), self => ((Zone)self)._override,
            (self, value) => ((Zone)self)._override = value is bool enabled && enabled, false);
        RegisterAttribute("Ambient Gradient", typeof(bool), self => ((Zone)self)._ambientGradient,
            (self, value) => ((Zone)self)._ambientGradient = value is bool enabled && enabled, false);
        RegisterAttribute("Priority", typeof(int), self => ((Zone)self)._priority,
            (self, value) =>
            {
                var zone = (Zone)self;
                zone._priority = (int)value!;
                zone.MarkNodeDirty();
            }, 0);
        RegisterAttribute("Zone Texture", typeof(ResourceRef), self => ((Zone)self).GetZoneTextureAttr(),
            (self, value) => ((Zone)self).SetZoneTextureAttr((ResourceRef)value!),
            new ResourceRef(ObjectReflection<TextureCube>.TypeId, string.Empty));
        RegisterAttribute("Light Mask", typeof(uint), self => ((Zone)self).GetLightMask(),
            (self, value) => ((Zone)self).SetLightMask((uint)value!), DefaultLightMask);
        RegisterAttribute("Shadow Mask", typeof(uint), self => ((Zone)self).GetShadowMask(),
            (self, value) => ((Zone)self).SetShadowMask((uint)value!), DefaultShadowMask);
        RegisterAttribute("Zone Mask", typeof(uint), self => ((Zone)self).GetZoneMask(),
            (self, value) => ((Zone)self).SetZoneMask((uint)value!), DefaultZoneMask);
    }

    public override void DrawDebugGeometry(DebugRenderer? debug, bool depthTest)
    {
        if (debug is not null && IsEnabledEffective)
            debug.AddBoundingBox(BoundingBox, Node.GetWorldTransform(), new Color(0.0f, 1.0f, 0.0f), depthTest);
    }

    public void SetBoundingBox(BoundingBox box)
    {
        _boundingBox = box;
        OnMarkedDirty(GetNode()!);
    }

    public void SetAmbientColor(Color color)
    {
        _ambientColor = color;
        MarkCachedAmbientDirty();
    }

    public Color GetAmbientColor() => _ambientColor;

    public void SetAmbientBrightness(float brightness)
    {
        _ambientBrightness = brightness;
        MarkCachedAmbientDirty();
    }

    public float GetAmbientBrightness() => _ambientBrightness;

    public void SetBackgroundBrightness(float brightness)
    {
        _backgroundBrightness = brightness;
        MarkCachedAmbientDirty();
    }

    public float GetBackgroundBrightness() => _backgroundBrightness;

    public void SetBackgroundStatic(bool isStatic)
    {
        _backgroundStatic = isStatic;
    }

    public bool IsBackgroundStatic() => _backgroundStatic;

    public void SetFogColor(Color color)
    {
        _fogColor = color;
        MarkCachedAmbientDirty();
    }

    public Color GetFogColor() => _fogColor;

    public void SetFogStart(float start)
    {
        if (start < 0.0f)
            start = 0.0f;

        _fogStart = start;
    }

    public float GetFogStart() => _fogStart;

    public void SetFogEnd(float end)
    {
        if (end < 0.0f)
            end = 0.0f;

        _fogEnd = end;
    }

    public float GetFogEnd() => _fogEnd;

    public void SetFogHeight(float height)
    {
        _fogHeight = height;
    }

    public float GetFogHeight() => _fogHeight;

    public void SetFogHeightScale(float scale)
    {
        _fogHeightScale = scale;
    }

    public float GetFogHeightScale() => _fogHeightScale;

    public void SetPriority(int priority)
    {
        _priority = priority;
    }

    public int GetPriority() => _priority;

    public void SetHeightFog(bool enable)
    {
        _heightFog = enable;
    }

    public bool GetHeightFog() => _heightFog;

    public void SetOverride(bool enable)
    {
        _override = enable;
    }

    public bool GetOverride() => _override;

    public void SetAmbientGradient(bool enable)
    {
        _ambientGradient = enable;
    }

    public bool GetAmbientGradient() => _ambientGradient;

    public void SetZoneTexture(Texture? texture)
    {
        if (_zoneTexture is not null)
            _zoneTexture.ReloadFinished -= HandleZoneTextureReloadFinished;

        _zoneTexture = texture;
        UpdateZoneTextureSubscription();
        MarkCachedTextureDirty();
    }

    public Texture? GetZoneTexture() => _zoneTexture;

    public void SetZoneTextureAttr(ResourceRef value)
    {
        if (_zoneTexture is not null)
            _zoneTexture.ReloadFinished -= HandleZoneTextureReloadFinished;

        var cache = GetSubsystem<ResourceCache>()!;
        _zoneTexture = cache.GetResource(value.Type, value.Name) as Texture;
        UpdateZoneTextureSubscription();
        MarkCachedTextureDirty();
        MarkCachedAmbientDirty();
    }

    public ResourceRef GetZoneTextureAttr() => Resource.GetResourceRef(_zoneTexture, ObjectReflection<TextureCube>.TypeId);

    public StrongBox<ReflectionProbeData> GetReflectionProbe() => _reflectionProbeData.GetStorage();

    public Matrix3x4 GetInverseWorldTransform()
    {
        if (_inverseWorldDirty)
        {
            _inverseWorld = IsAttached ? Node.GetWorldTransform().Inverse : Matrix3x4.Identity;
            _inverseWorldDirty = false;
        }

        return _inverseWorld;
    }

    public Color GetAmbientStartColor()
    {
        if (!_ambientGradient)
            return _ambientColor;

        if (!_lastAmbientStartZone.TryGetTarget(out _) || !_lastAmbientEndZone.TryGetTarget(out _))
            UpdateAmbientGradient();

        return _ambientStartColor;
    }

    public Color GetAmbientEndColor()
    {
        if (!_ambientGradient)
            return _ambientColor;

        if (!_lastAmbientStartZone.TryGetTarget(out _) || !_lastAmbientEndZone.TryGetTarget(out _))
            UpdateAmbientGradient();

        return _ambientEndColor;
    }

    /// <summary>
    /// Recalculate the ambient gradient colors from neighbor zones.
    /// </summary>
    public void UpdateAmbientGradient()
    {
        // In case no neighbor zones are found, reset ambient start/end with own ambient color
        _ambientStartColor = _ambientColor;
        _ambientEndColor = _ambientColor;
        _lastAmbientStartZone.SetTarget(this);
        _lastAmbientEndZone.SetTarget(this);

        var octant = GetOctant();
        if (octant is null)
            return;

        var octree = octant.GetOctree()!;

        var worldTransform = Node.GetWorldTransform();
        var center = BoundingBox.Center;
        var minZPosition = worldTransform * new Vector3f(center.X, center.Y, BoundingBox.Min.Z);
        var maxZPosition = worldTransform * new Vector3f(center.X, center.Y, BoundingBox.Max.Z);

        var result = new List<Drawable>();
        using (var query = new PointOctreeQuery(result, minZPosition, DrawableFlags.Zone))
            octree.GetDrawables(query);

        // Gradient start position: get the highest priority zone that is not this zone
        var bestPriority = int.MinValue;
        Zone? bestZone = null;
        foreach (var drawable in result)
        {
            var zone = (Zone)drawable;
            var priority = zone.GetPriority();
            if (priority > bestPriority && !ReferenceEquals(zone, this) && zone.IsInside(minZPosition))
            {
                bestZone = zone;
                bestPriority = priority;
            }
        }

        if (bestZone is not null)
        {
            _ambientStartColor = bestZone.GetAmbientColor();
            _lastAmbientStartZone.SetTarget(bestZone);
        }

        // Do the same for gradient end position
        using (var query = new PointOctreeQuery(result, maxZPosition, DrawableFlags.Zone))
            octree.GetDrawables(query);
        bestPriority = int.MinValue;
        bestZone = null;

        foreach (var drawable in result)
        {
            var zone = (Zone)drawable;
            var priority = zone.GetPriority();
            if (priority > bestPriority && !ReferenceEquals(zone, this) && zone.IsInside(maxZPosition))
            {
                bestZone = zone;
                bestPriority = priority;
            }
        }

        if (bestZone is not null)
        {
            _ambientEndColor = bestZone.GetAmbientColor();
            _lastAmbientEndZone.SetTarget(bestZone);
        }
    }

    public Vector3f GetAmbientLighting() => _cachedAmbientLighting.Get();

    public SphericalHarmonicsDot9 GetAmbientAndBackgroundLighting() => _cachedAmbientAndBackgroundLighting.Get();

    public void UpdateCachedData()
    {
        if (_cachedTextureLighting.IsInvalidated())
        {
            var sh = new SphericalHarmonicsDot9();
            if (_zoneTexture is not null)
            {
                var zoneTextureName = _zoneTexture.Name;
                var cache = GetSubsystem<ResourceCache>();
                var zoneImage = !string.IsNullOrEmpty(zoneTextureName)
                    ? cache?.GetTempResource<ImageCube>(zoneTextureName, false)
                    : null;
                if (zoneImage is not null)
                    sh = zoneImage.GetOrCreateSphericalHarmonics();
                else
                    Log.Logger.LogWarning(
                        "Cannot extract spherical harmonics from Zone texture without corresponding resource in cache");
            }

            _cachedTextureLighting.Restore(sh);
        }

        if (_cachedAmbientLighting.IsInvalidated())
            _cachedAmbientLighting.Restore((_ambientColor * _ambientBrightness).GammaToLinear().ToVector3());

        if (_cachedAmbientAndBackgroundLighting.IsInvalidated())
        {
            var sh = _zoneTexture is not null
                ? _cachedTextureLighting.Get()
                : new SphericalHarmonicsDot9(_fogColor.GammaToLinear());
            sh *= _backgroundBrightness;
            sh += GetAmbientLighting();
            _cachedAmbientAndBackgroundLighting.Restore(sh);
        }

        if (_reflectionProbeData.IsInvalidated())
        {
            var data = new ReflectionProbeData();
            data.ReflectionMap = _zoneTexture as TextureCube;
            var renderer = GetSubsystem<Renderer>();
            if (data.ReflectionMap is null && renderer is not null)
                data.ReflectionMap = renderer.GetBlackCubeMap();
            data.RoughnessToLodFactor = data.ReflectionMap is not null
                ? MathF.Log2(data.ReflectionMap.Width)
                : 1.0f;
            _reflectionProbeData.Restore(data);
        }
    }

    public bool IsInside(Vector3f point)
    {
        var localPoint = GetInverseWorldTransform() * point;
        return BoundingBox.IsInside(localPoint) != Intersection.Outside;
    }

    protected internal override void OnMarkedDirty(Node node)
    {
        var scene = Scene;
        if (scene is not null && scene.IsThreadedUpdate())
        {
            scene.DelayedMarkedDirty(this);
            return;
        }

        base.OnMarkedDirty(node);

        if (GetOctant() is { } octant)
            octant.GetOctree()!.MarkZoneDirty(this);

        ClearDrawablesZone();

        _inverseWorldDirty = true;
    }

    protected override void OnWorldBoundingBoxUpdate()
    {
        SetWorldBoundingBox(BoundingBox.Transformed(Node.GetWorldTransform()));
    }

    private void MarkNodeDirty() => OnMarkedDirty(GetNode()!);

    private void MarkCachedAmbientDirty()
    {
        _cachedAmbientLighting.Invalidate();
        _cachedAmbientAndBackgroundLighting.Invalidate();
    }

    private void MarkCachedTextureDirty()
    {
        _cachedTextureLighting.Invalidate();
        _reflectionProbeData.Invalidate();
        _cachedAmbientAndBackgroundLighting.Invalidate();
    }

    private void UpdateZoneTextureSubscription()
    {
        if (_zoneTexture is not null)
            _zoneTexture.ReloadFinished += HandleZoneTextureReloadFinished;
    }

    private void HandleZoneTextureReloadFinished(Resource resource)
    {
        MarkCachedTextureDirty();
    }

    internal void ClearDrawablesZone()
    {
        var currentWorldBoundingBox = GetWorldBoundingBox();
        var dirtyWorldBoundingBox = currentWorldBoundingBox.Merged(_lastWorldBoundingBox);
        if (GetOctant() is { } octant)
        {
            var octree = octant.GetOctree()!;
            var result = new List<Drawable>();
            using var query = new BoxOctreeQuery(result, dirtyWorldBoundingBox, DrawableFlags.Geometry | DrawableFlags.Zone);
            octree.GetDrawables(query);

            foreach (var drawable in result)
            {
                var drawableFlags = drawable.GetDrawableFlags();
                if ((drawableFlags & DrawableFlags.Geometry) != 0)
                    drawable.SetZone(null);
                else if ((drawableFlags & DrawableFlags.Zone) != 0)
                {
                    var zone = (Zone)drawable;
                    zone._lastAmbientStartZone.SetTarget(null!);
                    zone._lastAmbientEndZone.SetTarget(null!);
                }
            }
        }

        _lastWorldBoundingBox = currentWorldBoundingBox;
        _lastAmbientStartZone.SetTarget(null!);
        _lastAmbientEndZone.SetTarget(null!);
    }

    protected override void OnRemoveFromOctree()
    {
        ClearDrawablesZone();
    }

    public override void Dispose()
    {
        _reflectionProbeData.GetStorage().Value = default;
        _lastAmbientEndZone.SetTarget(null!);
        _lastAmbientStartZone.SetTarget(null!);
        if (_zoneTexture is not null)
            _zoneTexture.ReloadFinished -= HandleZoneTextureReloadFinished;
        _zoneTexture = null;

        base.Dispose();
    }
}
