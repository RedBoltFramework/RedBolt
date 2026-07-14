namespace RedBolt;

public sealed class Zone : Drawable
{
    public const float DefaultFogStart = 250.0f;
    public const float DefaultFogEnd = 1000.0f;
    public const float DefaultFogHeight = 0.0f;
    public const float DefaultFogHeightScale = 0.5f;

    public static readonly Vector3f DefaultBoundingBoxMin = new(-10.0f, -10.0f, -10.0f);
    public static readonly Vector3f DefaultBoundingBoxMax = new(10.0f, 10.0f, 10.0f);
    public static readonly Color DefaultAmbientColor = new(0.1f, 0.1f, 0.1f);
    public static readonly Color DefaultFogColor = Color.Black;

    private Texture? _zoneTexture;
    private ResourceRef _zoneTextureAttribute = new(StringHash.Calculate("TextureCube"), string.Empty);
    private Matrix3x4 _inverseWorld = Matrix3x4.Identity;
    private bool _inverseWorldDirty = true;
    private Color _ambientStartColor = DefaultAmbientColor;
    private Color _ambientEndColor = DefaultAmbientColor;
    private BoundingBox _lastWorldBoundingBox;
    private Zone? _lastAmbientStartZone;
    private Zone? _lastAmbientEndZone;
    private readonly ThreadSafeCache<SphericalHarmonicsDot9> _cachedTextureLighting = new();
    private readonly ThreadSafeCache<Vector3f> _cachedAmbientLighting = new();
    private readonly ThreadSafeCache<SphericalHarmonicsDot9> _cachedAmbientAndBackgroundLighting = new();
    private readonly ThreadSafeCache<ReflectionProbeData> _reflectionProbeData = new();

    public Zone()
        : base(DrawableFlags.Zone)
    {
        SetLocalBoundingBox(new BoundingBox(DefaultBoundingBoxMin, DefaultBoundingBoxMax));
    }

    public void DrawDebugGeometry(DebugRenderer? debug, bool depthTest)
    {
        if (debug is not null && IsEnabledEffective)
            debug.AddBoundingBox(BoundingBox, Node.GetWorldTransform(), new Color(0.0f, 1.0f, 0.0f), depthTest);
    }

    public Color AmbientColor { get; private set; } = DefaultAmbientColor;

    public float AmbientBrightness { get; private set; } = 1.0f;

    public float BackgroundBrightness { get; private set; }

    public bool BackgroundStatic { get; private set; }

    public Color FogColor { get; private set; } = DefaultFogColor;

    public float FogStart { get; private set; } = DefaultFogStart;

    public float FogEnd { get; private set; } = DefaultFogEnd;

    public float FogHeight { get; private set; } = DefaultFogHeight;

    public float FogHeightScale { get; private set; } = DefaultFogHeightScale;

    public int Priority { get; private set; }

    public bool HeightFog { get; private set; }

    public bool Override { get; private set; }

    public bool AmbientGradient { get; private set; }

    public void SetBoundingBox(BoundingBox box)
    {
        SetLocalBoundingBox(box);
        MarkZoneDirty();
    }

    public void SetAmbientColor(Color color)
    {
        AmbientColor = color;
        _ambientStartColor = color;
        _ambientEndColor = color;
        MarkCachedAmbientDirty();
    }

    public Color GetAmbientColor() => AmbientColor;

    public void SetAmbientBrightness(float brightness)
    {
        AmbientBrightness = brightness;
        MarkCachedAmbientDirty();
    }

    public float GetAmbientBrightness() => AmbientBrightness;

    public void SetBackgroundBrightness(float brightness)
    {
        BackgroundBrightness = brightness;
        MarkCachedAmbientDirty();
    }

    public float GetBackgroundBrightness() => BackgroundBrightness;

    public void SetBackgroundStatic(bool isStatic)
    {
        BackgroundStatic = isStatic;
    }

    public bool IsBackgroundStatic() => BackgroundStatic;

    public void SetFogColor(Color color)
    {
        FogColor = color;
        MarkCachedAmbientDirty();
    }

    public Color GetFogColor() => FogColor;

    public void SetFogStart(float start)
    {
        FogStart = Math.Max(start, 0.0f);
    }

    public float GetFogStart() => FogStart;

    public void SetFogEnd(float end)
    {
        FogEnd = Math.Max(end, 0.0f);
    }

    public float GetFogEnd() => FogEnd;

    public void SetFogHeight(float height)
    {
        FogHeight = height;
    }

    public float GetFogHeight() => FogHeight;

    public void SetFogHeightScale(float scale)
    {
        FogHeightScale = scale;
    }

    public float GetFogHeightScale() => FogHeightScale;

    public void SetPriority(int priority)
    {
        Priority = priority;
    }

    public int GetPriority() => Priority;

    public void SetHeightFog(bool enable)
    {
        HeightFog = enable;
    }

    public bool GetHeightFog() => HeightFog;

    public void SetOverride(bool enable)
    {
        Override = enable;
    }

    public bool GetOverride() => Override;

    public void SetAmbientGradient(bool enable)
    {
        AmbientGradient = enable;
    }

    public bool GetAmbientGradient() => AmbientGradient;

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
        _zoneTextureAttribute = value;
        if (_zoneTexture is not null)
            _zoneTexture.ReloadFinished -= HandleZoneTextureReloadFinished;

        var cache = GetSubsystem<ResourceCache>();
        _zoneTexture = null;
        if (cache is not null)
        {
            foreach (var item in cache.AllResources)
            {
                if (string.Equals(item.Key.Name, value.Name, StringComparison.Ordinal)
                    && item.Value is Texture texture)
                {
                    _zoneTexture = texture;
                    break;
                }
            }
        }
        UpdateZoneTextureSubscription();
        MarkCachedTextureDirty();
        MarkCachedAmbientDirty();
    }

    public ResourceRef GetZoneTextureAttr() => _zoneTextureAttribute;

    public ReflectionProbeData GetReflectionProbe() => _reflectionProbeData.Get();

    public Matrix3x4 GetInverseWorldTransform()
    {
        if (_inverseWorldDirty)
        {
            _inverseWorld = IsAttached ? BuildInverseWorldTransform() : Matrix3x4.Identity;
            _inverseWorldDirty = false;
        }

        return _inverseWorld;
    }

    public Color GetAmbientStartColor()
    {
        if (!AmbientGradient)
            return AmbientColor;

        if (_lastAmbientStartZone is null)
            UpdateAmbientGradient();

        return _ambientStartColor;
    }

    public Color GetAmbientEndColor()
    {
        if (!AmbientGradient)
            return AmbientColor;

        if (_lastAmbientEndZone is null)
            UpdateAmbientGradient();

        return _ambientEndColor;
    }

    /// <summary>
    /// Recalculate the ambient gradient colors from neighbor zones.
    /// </summary>
    public void UpdateAmbientGradient()
    {
        // In case no neighbor zones are found, reset ambient start/end with own ambient color
        _ambientStartColor = AmbientColor;
        _ambientEndColor = AmbientColor;
        _lastAmbientStartZone = this;
        _lastAmbientEndZone = this;

        if (!IsAttached || Scene?.Root.GetComponent<Octree>() is not { } octree)
            return;

        var worldTransform = Node.GetWorldTransform();
        var center = BoundingBox.Center;
        var minZPosition = worldTransform * new Vector3f(center.X, center.Y, BoundingBox.Min.Z);
        var maxZPosition = worldTransform * new Vector3f(center.X, center.Y, BoundingBox.Max.Z);

        var result = octree.GetDrawables(minZPosition, DrawableFlags.Zone);

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
            _lastAmbientStartZone = bestZone;
        }

        // Do the same for gradient end position
        result = octree.GetDrawables(maxZPosition, DrawableFlags.Zone);
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
            _lastAmbientEndZone = bestZone;
        }
    }

    public Vector3f GetAmbientLighting() => _cachedAmbientLighting.Get();

    public SphericalHarmonicsDot9 GetAmbientAndBackgroundLighting() => _cachedAmbientAndBackgroundLighting.Get().Clone();

    public void UpdateCachedData()
    {
        if (_cachedTextureLighting.IsInvalidated())
        {
            var sh = new SphericalHarmonicsDot9();
            if (_zoneTexture is not null)
            {
                var zoneImage = GetSubsystem<ResourceCache>()?.GetTempResource<ImageCube>(_zoneTexture.Name, false);
                if (zoneImage is not null)
                    sh = zoneImage.GetOrCreateSphericalHarmonics();
            }

            _cachedTextureLighting.Restore(sh);
        }

        if (_cachedAmbientLighting.IsInvalidated())
            _cachedAmbientLighting.Restore((AmbientColor * AmbientBrightness).GammaToLinear().ToVector3());

        if (_cachedAmbientAndBackgroundLighting.IsInvalidated())
        {
            var sh = _zoneTexture is not null
                ? _cachedTextureLighting.Get().Clone()
                : new SphericalHarmonicsDot9(FogColor.GammaToLinear());
            sh.Scale(BackgroundBrightness);
            sh.Add(GetAmbientLighting());
            _cachedAmbientAndBackgroundLighting.Restore(sh);
        }

        if (_reflectionProbeData.IsInvalidated())
        {
            var data = new ReflectionProbeData
            {
                ReflectionMap = _zoneTexture as TextureCube,
                RoughnessToLodFactor = _zoneTexture is TextureCube textureCube
                    ? MathF.Log2(textureCube.Width)
                    : 1.0f
            };
            _reflectionProbeData.Restore(data);
        }
    }

    public bool IsInside(Vector3f point)
    {
        var localPoint = IsAttached ? Node.WorldToLocal(point) : point;
        return BoundingBox.IsInside(localPoint) != Intersection.Outside;
    }

    protected internal override void OnMarkedDirty(Node node)
    {
        base.OnMarkedDirty(node);
        ClearDrawablesZone();
        MarkZoneDirty();
    }

    protected override void OnWorldBoundingBoxUpdate()
    {
        SetWorldBoundingBox(BoundingBox.Transformed(Node.GetWorldTransform()));
    }

    private void MarkZoneDirty()
    {
        _inverseWorldDirty = true;
        _ambientStartColor = AmbientColor;
        _ambientEndColor = AmbientColor;
    }

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
        if (Scene?.Root.GetComponent<Octree>() is { } octree)
        {
            foreach (var drawable in octree.GetDrawables(dirtyWorldBoundingBox, DrawableFlags.Geometry | DrawableFlags.Zone))
            {
                var drawableFlags = drawable.GetDrawableFlags();
                if ((drawableFlags & DrawableFlags.Geometry) != 0)
                    drawable.SetZone(null);
                else if ((drawableFlags & DrawableFlags.Zone) != 0 && drawable is Zone zone)
                {
                    zone._lastAmbientStartZone = null;
                    zone._lastAmbientEndZone = null;
                }
            }
        }

        _lastWorldBoundingBox = currentWorldBoundingBox;
        _lastAmbientStartZone = null;
        _lastAmbientEndZone = null;
    }

    protected override void OnRemoveFromOctree()
    {
        ClearDrawablesZone();
    }

    public override void Dispose()
    {
        if (_zoneTexture is not null)
            _zoneTexture.ReloadFinished -= HandleZoneTextureReloadFinished;

        base.Dispose();
    }

    private Matrix3x4 BuildInverseWorldTransform()
    {
        var origin = Node.WorldToLocal(Vector3f.Zero);
        var xAxis = Node.WorldToLocal(Vector3f.Right) - origin;
        var yAxis = Node.WorldToLocal(Vector3f.Up) - origin;
        var zAxis = Node.WorldToLocal(Vector3f.Forward) - origin;
        return new Matrix3x4(
            xAxis.X, yAxis.X, zAxis.X, origin.X,
            xAxis.Y, yAxis.Y, zAxis.Y, origin.Y,
            xAxis.Z, yAxis.Z, zAxis.Z, origin.Z);
    }
}
