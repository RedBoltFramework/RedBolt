namespace RedBolt;

public enum CubemapUpdateStage
{
    Idle,
    RenderFaces,
    Ready
}

public struct CubemapUpdateResult
{
    public uint NumRenderedFaces;
    public bool IsComplete;
}

public struct CubemapRenderingSettings : IEquatable<CubemapRenderingSettings>
{
    public const uint DefaultTextureSize = 256;
    public const uint DefaultViewMask = 0xffffffff;
    public const float DefaultNearClip = 0.1f;
    public const float DefaultFarClip = 100.0f;

    public uint TextureSize { get; set; }
    public uint ViewMask { get; set; }
    public float NearClip { get; set; }
    public float FarClip { get; set; }

    public CubemapRenderingSettings()
    {
        TextureSize = DefaultTextureSize;
        ViewMask = DefaultViewMask;
        NearClip = DefaultNearClip;
        FarClip = DefaultFarClip;
    }

    public bool Equals(CubemapRenderingSettings other) =>
        TextureSize == other.TextureSize
        && ViewMask == other.ViewMask
        && NearClip.Equals(other.NearClip)
        && FarClip.Equals(other.FarClip);

    public override bool Equals(object? obj) => obj is CubemapRenderingSettings other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(TextureSize, ViewMask, NearClip, FarClip);

    public static bool operator ==(CubemapRenderingSettings lhs, CubemapRenderingSettings rhs) => lhs.Equals(rhs);

    public static bool operator !=(CubemapRenderingSettings lhs, CubemapRenderingSettings rhs) => !lhs.Equals(rhs);
}

public struct CubemapUpdateParameters
{
    public CubemapRenderingSettings Settings;
    public Vector3f Position;
    public bool SlicedUpdate;
    public bool FilterResult;
    public TextureCube? OverrideFinalTexture;

    public readonly bool IsConsistentWith(CubemapUpdateParameters rhs) =>
        Settings == rhs.Settings
        && SlicedUpdate == rhs.SlicedUpdate
        && FilterResult == rhs.FilterResult
        && ReferenceEquals(OverrideFinalTexture, rhs.OverrideFinalTexture);
}

/// <summary>Utility class that handles cubemap rendering from scene.</summary>
public class CubemapRenderer : Object
{
    public event Action<TextureCube>? CubemapRendered;

    private readonly Scene? _scene;
    private CubemapUpdateStage _updateStage = CubemapUpdateStage.Idle;

    public CubemapRenderer(Scene scene)
    {
        _scene = scene ?? throw new ArgumentNullException(nameof(scene));
        // TODO: port complete C++ CubemapRenderer camera/viewport/pipeline initialization
    }

    public override void Dispose()
    {
        CubemapRendered = null;
        base.Dispose();
    }

    public static void DefineTexture(TextureCube texture, CubemapRenderingSettings settings,
        TextureFlag flags = TextureFlag.None)
    {
        ArgumentNullException.ThrowIfNull(texture);
        // TODO: port complete C++ CubemapRenderer::DefineTexture size/format setup
        _ = settings;
        _ = flags;
    }

    public CubemapUpdateResult Update(CubemapUpdateParameters updateParameters)
    {
        // TODO: port complete C++ CubemapRenderer::Update face rendering and filtering
        _ = updateParameters;
        _ = _scene;
        _updateStage = CubemapUpdateStage.Idle;
        return new CubemapUpdateResult { NumRenderedFaces = 0, IsComplete = true };
    }
}
