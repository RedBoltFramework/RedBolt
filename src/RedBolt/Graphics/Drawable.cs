namespace RedBolt;

[Flags]
public enum DrawableFlags : byte
{
    Undefined = 0,
    Geometry = 1 << 0,
    Light = 1 << 1,
    Zone = 1 << 2,
    Geometry2D = 1 << 3,
    Any = 0xff
}

public enum GlobalIlluminationType
{
    None,
    UseLightMap,
    BlendLightProbes
}

public enum ReflectionMode
{
    Zone,
    NearestProbe,
    BlendProbes,
    BlendProbesAndZone
}

public abstract class Drawable : Component
{
    public const uint DefaultViewMask = uint.MaxValue;
    public const uint DefaultLightMask = uint.MaxValue;
    public const uint DefaultShadowMask = uint.MaxValue;
    public const uint DefaultZoneMask = uint.MaxValue;
    public const uint PortableLightMask = 0x0f;
    public const int MaxVertexLights = 4;
    public const float AnimationLodBaseScale = 2500.0f;

    protected Drawable(DrawableFlags drawableFlags = DrawableFlags.Undefined)
    {
        DrawableFlags = drawableFlags;
    }

    private BoundingBox boundingBox;
    private BoundingBox worldBoundingBox;
    private bool worldBoundingBoxDirty = true;

    public BoundingBox BoundingBox => boundingBox;

    public BoundingBox WorldBoundingBox => GetWorldBoundingBox();

    public DrawableFlags DrawableFlags { get; }

    public float DrawDistance { get; set; }

    public float ShadowDistance { get; set; }

    public float LodBias { get; set; } = 1.0f;

    public uint ViewMask { get; set; } = DefaultViewMask;

    public uint LightMask { get; set; } = DefaultLightMask;

    public uint ShadowMask { get; set; } = DefaultShadowMask;

    public uint ZoneMask { get; set; } = DefaultZoneMask;

    public bool CastShadows { get; set; }

    public bool Occluder { get; set; } = true;

    public bool Occludee { get; set; } = true;

    public GlobalIlluminationType GlobalIlluminationType { get; set; }

    public ReflectionMode ReflectionMode { get; set; }

    public BoundingBox GetBoundingBox() => boundingBox;

    public DrawableFlags GetDrawableFlags() => DrawableFlags;

    public float GetDrawDistance() => DrawDistance;

    public float GetShadowDistance() => ShadowDistance;

    public float GetLodBias() => LodBias;

    public uint GetViewMask() => ViewMask;

    public uint GetLightMask() => LightMask;

    public uint GetShadowMask() => ShadowMask;

    public uint GetZoneMask() => ZoneMask;

    public bool GetCastShadows() => CastShadows;

    public bool IsOccluder() => Occluder;

    public bool IsOccludee() => Occludee;

    public GlobalIlluminationType GetGlobalIlluminationType() => GlobalIlluminationType;

    public ReflectionMode GetReflectionMode() => ReflectionMode;

    public BoundingBox GetWorldBoundingBox()
    {
        if (worldBoundingBoxDirty)
        {
            OnWorldBoundingBoxUpdate();
            worldBoundingBoxDirty = false;
        }

        return worldBoundingBox;
    }

    public void SetDrawDistance(float distance)
    {
        DrawDistance = Math.Max(distance, 0.0f);
    }

    public void SetShadowDistance(float distance)
    {
        ShadowDistance = Math.Max(distance, 0.0f);
    }

    public void SetLodBias(float bias)
    {
        LodBias = Math.Max(bias, MathDefs.Epsilon);
    }

    public void SetViewMask(uint mask)
    {
        ViewMask = mask;
    }

    public void SetLightMask(uint mask)
    {
        LightMask = mask;
    }

    public void SetShadowMask(uint mask)
    {
        ShadowMask = mask;
    }

    public void SetZoneMask(uint mask)
    {
        ZoneMask = mask;
    }

    public void SetCastShadows(bool enable)
    {
        CastShadows = enable;
    }

    public void SetOccluder(bool enable)
    {
        Occluder = enable;
    }

    public void SetOccludee(bool enable)
    {
        Occludee = enable;
    }

    public void SetGlobalIlluminationType(GlobalIlluminationType type)
    {
        GlobalIlluminationType = type;
    }

    public void SetReflectionMode(ReflectionMode mode)
    {
        ReflectionMode = mode;
    }

    public void MarkForUpdate()
    {
        worldBoundingBoxDirty = true;
    }

    protected void SetLocalBoundingBox(BoundingBox box)
    {
        boundingBox = box;
        MarkForUpdate();
    }

    protected void SetWorldBoundingBox(BoundingBox box)
    {
        worldBoundingBox = box;
    }

    protected virtual void OnWorldBoundingBoxUpdate()
    {
        worldBoundingBox = IsAttached ? boundingBox.Transformed(Node.GetWorldTransform()) : boundingBox;
    }

    protected internal override void OnMarkedDirty(Node node)
    {
        MarkForUpdate();
    }
}
