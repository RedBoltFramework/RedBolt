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

public abstract class Drawable : Component
{
    public const uint DefaultViewMask = uint.MaxValue;
    public const uint DefaultLightMask = uint.MaxValue;
    public const uint DefaultShadowMask = uint.MaxValue;
    public const uint DefaultZoneMask = uint.MaxValue;

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

    public BoundingBox GetBoundingBox() => boundingBox;

    public BoundingBox GetWorldBoundingBox()
    {
        if (worldBoundingBoxDirty)
        {
            OnWorldBoundingBoxUpdate();
            worldBoundingBoxDirty = false;
        }

        return worldBoundingBox;
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

    protected virtual void OnWorldBoundingBoxUpdate()
    {
        worldBoundingBox = IsAttached ? boundingBox.Transformed(Node.GetWorldTransform()) : boundingBox;
    }

    protected internal override void OnMarkedDirty(Node node)
    {
        MarkForUpdate();
    }
}
