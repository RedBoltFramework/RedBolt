namespace RedBolt;

public sealed class Zone : Drawable
{
    public const float DefaultFogStart = 250.0f;
    public const float DefaultFogEnd = 1000.0f;
    public const float DefaultFogHeight = 0.0f;
    public const float DefaultFogHeightScale = 0.5f;

    public static readonly Vector3 DefaultBoundingBoxMin = new(-10.0f, -10.0f, -10.0f);
    public static readonly Vector3 DefaultBoundingBoxMax = new(10.0f, 10.0f, 10.0f);
    public static readonly Color DefaultAmbientColor = new(0.1f, 0.1f, 0.1f);
    public static readonly Color DefaultFogColor = Color.Black;

    private Texture? _zoneTexture;
    private ResourceRef _zoneTextureAttribute = new(StringHash.Calculate("TextureCube"), string.Empty);
    private Matrix3x4 _inverseWorld = Matrix3x4.Identity;
    private bool _inverseWorldDirty = true;
    private Color _ambientStartColor = DefaultAmbientColor;
    private Color _ambientEndColor = DefaultAmbientColor;

    public Zone()
        : base(DrawableFlags.Zone)
    {
        SetLocalBoundingBox(new BoundingBox(DefaultBoundingBoxMin, DefaultBoundingBoxMax));
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
    }

    public Color GetAmbientColor() => AmbientColor;

    public void SetAmbientBrightness(float brightness)
    {
        AmbientBrightness = brightness;
    }

    public float GetAmbientBrightness() => AmbientBrightness;

    public void SetBackgroundBrightness(float brightness)
    {
        BackgroundBrightness = brightness;
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
        MarkZoneDirty();
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
        _zoneTexture = texture;
    }

    public Texture? GetZoneTexture() => _zoneTexture;

    public void SetZoneTextureAttr(ResourceRef value)
    {
        _zoneTextureAttribute = value;
    }

    public ResourceRef GetZoneTextureAttr() => _zoneTextureAttribute;

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
        return AmbientGradient ? _ambientStartColor : AmbientColor;
    }

    public Color GetAmbientEndColor()
    {
        return AmbientGradient ? _ambientEndColor : AmbientColor;
    }

    public Vector3 GetAmbientLighting() => (AmbientColor * AmbientBrightness).ToVector3();

    public bool IsInside(Vector3 point)
    {
        var localPoint = IsAttached ? Node.WorldToLocal(point) : point;
        return BoundingBox.IsInside(localPoint) != Intersection.Outside;
    }

    protected internal override void OnMarkedDirty(Node node)
    {
        base.OnMarkedDirty(node);
        MarkZoneDirty();
    }

    private void MarkZoneDirty()
    {
        _inverseWorldDirty = true;
        _ambientStartColor = AmbientColor;
        _ambientEndColor = AmbientColor;
    }

    private Matrix3x4 BuildInverseWorldTransform()
    {
        var origin = Node.WorldToLocal(Vector3.Zero);
        var xAxis = Node.WorldToLocal(Vector3.Right) - origin;
        var yAxis = Node.WorldToLocal(Vector3.Up) - origin;
        var zAxis = Node.WorldToLocal(Vector3.Forward) - origin;
        return new Matrix3x4(
            xAxis.X, yAxis.X, zAxis.X, origin.X,
            xAxis.Y, yAxis.Y, zAxis.Y, origin.Y,
            xAxis.Z, yAxis.Z, zAxis.Z, origin.Z);
    }
}
