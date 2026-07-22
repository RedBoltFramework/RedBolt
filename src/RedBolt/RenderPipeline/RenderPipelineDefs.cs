using Microsoft.Extensions.Logging;

namespace RedBolt;

public readonly record struct RenderPassToggle(string Name, bool Enabled);

/// <summary>
/// Sub-passes of user pass.
/// </summary>
public enum BatchCompositorSubpass
{
    /// <summary>
    /// Geometry buffer building pass of deferred rendering.
    /// </summary>
    Deferred,
    /// <summary>
    /// Base pass, optionally lit with forward rendering.
    /// </summary>
    Base,
    /// <summary>
    /// Additive light pass for forward rendering.
    /// </summary>
    Light,
    /// <summary>
    /// Artificial value that indicates to ignore subpass-specific properties.
    /// </summary>
    Ignored
}

/// <summary>
/// Traits of scene pass.
/// </summary>
[Flags]
public enum DrawableProcessorPassFlag
{
    None = 0,
    HasAmbientLighting = 1 << 0,
    DisableInstancing = 1 << 1,
    DeferredLightMaskToStencil = 1 << 2,
    NeedReadableDepth = 1 << 3,
    RefractionPass = 1 << 4,
    DepthOnlyPass = 1 << 5,
    ReadOnlyDepth = 1 << 6,
    StereoInstancing = 1 << 7,
    BatchCallback = 1 << 8,
    PipelineStateCallback = 1 << 9
}

/// <summary>
/// Flags that control how exactly batches are rendered.
/// </summary>
[Flags]
public enum BatchRenderFlag
{
    None = 0,
    EnableAmbientLighting = 1 << 0,
    EnableVertexLights = 1 << 1,
    EnablePixelLights = 1 << 2,
    EnableInstancingForStaticGeometry = 1 << 3,
    DisableColorOutput = 1 << 4,
    LightMaskToStencil = 1 << 5,
    LinearColorSpace = 1 << 6,
    EnableAmbientAndVertexLighting = EnableAmbientLighting | EnableVertexLights
}

/// <summary>
/// Render buffer traits.
/// </summary>
[Flags]
public enum RenderBufferFlag
{
    None = 0,
    /// <summary>
    /// Texture content is preserved between frames.
    /// </summary>
    Persistent = 1 << 0,
    FixedTextureSize = 1 << 1,
    BilinearFiltering = 1 << 2,
    CubeMap = 1 << 3,
    NoMultiSampledAutoResolve = 1 << 4
}

/// <summary>
/// Render buffer parameters. Actual render buffer size is controlled externally.
/// </summary>
public readonly record struct RenderBufferParams(TextureFormat TextureFormat, int MultiSampleLevel = 1, RenderBufferFlag Flags = RenderBufferFlag.None);

/// <summary>
/// Color space of primary color outputs of render pipeline.
/// Color buffer is guaranteed to have Red, Green and Blue channels regardless of this choice.
/// </summary>
public enum RenderPipelineColorSpace
{
    /// <summary>
    /// Low dynamic range lighting in Gamma space, trimmed to [0, 1].
    /// </summary>
    GammaLDR,
    /// <summary>
    /// Low dynamic range lighting in Linear space, trimmed to [0, 1].
    /// </summary>
    LinearLDR,
    /// <summary>
    /// High dynamic range lighting in Linear space. Should be tone mapped before frame end.
    /// </summary>
    LinearHDR,
    /// <summary>
    /// Use the color space that matches output render texture.
    /// </summary>
    Optimized
}

/// <summary>
/// Controls how DrawTexture[Region] handles Gamma and Linear color space.
/// </summary>
public enum ColorSpaceTransition
{
    /// <summary>
    /// Read and write as is.
    /// </summary>
    None,
    /// <summary>
    /// Write linear data to output texture if output is sRGB.
    /// Write gamma data to output texture if output is not sRGB.
    /// </summary>
    Automatic
}

internal static class RenderPipelineColorSpaceUtility
{
    public static TextureFormat GetTextureFormat(RenderPipelineColorSpace colorSpace)
    {
        return colorSpace switch
        {
            RenderPipelineColorSpace.GammaLDR => TextureFormat.TexFormatRgba8Unorm,
            RenderPipelineColorSpace.LinearLDR => TextureFormat.TexFormatRgba8UnormSrgb,
            RenderPipelineColorSpace.LinearHDR => TextureFormat.TexFormatRgba16Float,
            _ => TextureFormat.TexFormatRgba8Unorm
        };
    }
}

/// <summary>
/// Rarely-changing settings of render buffer manager.
/// </summary>
public struct RenderBufferManagerSettings : IEquatable<RenderBufferManagerSettings>
{
    /// <summary>
    /// Whether to inherit multisample level from output render texture.
    /// </summary>
    public bool InheritMultiSampleLevel { get; set; } = true;

    /// <summary>
    /// Multisample level of both output color buffers and depth buffer.
    /// </summary>
    public int MultiSampleLevel { get; set; } = 1;

    /// <summary>
    /// Preferred color space of both output color buffers.
    /// </summary>
    public RenderPipelineColorSpace ColorSpace { get; set; }

    /// <summary>
    /// Whether output color buffers are required to have at least bilinear filtering.
    /// </summary>
    public bool FilteredColor { get; set; }

    /// <summary>
    /// Whether the depth-stencil buffer is required to have stencil.
    /// </summary>
    public bool StencilBuffer { get; set; }

    /// <summary>
    /// Whether the depth buffer should be readable.
    /// </summary>
    public bool ReadableDepth { get; set; }

    /// <summary>
    /// Whether the both of output color buffers should be usable with other render targets.
    /// OpenGL backbuffer color cannot do that.
    /// </summary>
    public bool ColorUsableWithMultipleRenderTargets { get; set; }

    public RenderBufferManagerSettings()
    {
        InheritMultiSampleLevel = true;
        MultiSampleLevel = 1;
    }

    public uint CalculatePipelineStateHash()
    {
        var hash = 0u;
        CombineHash(ref hash, ReadableDepth ? 1u : 0u);
        return hash;
    }

    public void Validate()
    {
        MultiSampleLevel = (int)Math.Clamp(MathDefs.ClosestPowerOfTwo((uint)Math.Max(MultiSampleLevel, 1)), 1u, 16u);
    }

    public readonly bool Equals(RenderBufferManagerSettings other)
    {
        return InheritMultiSampleLevel == other.InheritMultiSampleLevel
            && MultiSampleLevel == other.MultiSampleLevel
            && ColorSpace == other.ColorSpace
            && FilteredColor == other.FilteredColor
            && StencilBuffer == other.StencilBuffer
            && ReadableDepth == other.ReadableDepth
            && ColorUsableWithMultipleRenderTargets == other.ColorUsableWithMultipleRenderTargets;
    }

    public static bool operator ==(RenderBufferManagerSettings left, RenderBufferManagerSettings right) => left.Equals(right);

    public static bool operator !=(RenderBufferManagerSettings left, RenderBufferManagerSettings right) => !left.Equals(right);

    public override readonly bool Equals(object? obj) => obj is RenderBufferManagerSettings other && Equals(other);

    public override readonly int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(InheritMultiSampleLevel);
        hash.Add(MultiSampleLevel);
        hash.Add(ColorSpace);
        hash.Add(FilteredColor);
        hash.Add(StencilBuffer);
        hash.Add(ReadableDepth);
        hash.Add(ColorUsableWithMultipleRenderTargets);
        return hash.ToHashCode();
    }

    private static void CombineHash(ref uint result, uint hash) =>
        result ^= hash + 0x9e3779b9u + (result << 6) + (result >> 2);
}

/// <summary>
/// Frequently-changing settings of render buffer manager.
/// </summary>
public struct RenderBufferManagerFrameSettings
{
    /// <summary>
    /// Whether the both of output color buffers should be readable.
    /// </summary>
    public bool ReadableColor { get; set; }

    /// <summary>
    /// Whether it's should be supported to read from and write to output color buffer simultaneously.
    /// </summary>
    public bool SupportColorReadWrite { get; set; }
}

/// <summary>
/// Traits of post-processing pass.
/// </summary>
[Flags]
public enum RenderOutputFlag
{
    None = 0,
    NeedColorOutputReadAndWrite = 1 << 0,
    NeedColorOutputBilinear = 1 << 1
}

/// <summary>
/// Render pipeline frame statistics.
/// </summary>
public struct RenderPipelineStats
{
    /// <summary>
    /// Total number of lights processed.
    /// </summary>
    public int NumLights { get; set; }

    /// <summary>
    /// Total number of lights with shadows processed.
    /// </summary>
    public int NumShadowedLights { get; set; }

    /// <summary>
    /// Total number of geometries in the frame (excluding shadow casters).
    /// </summary>
    public int NumGeometries { get; set; }

    /// <summary>
    /// Number of occluders rendered.
    /// </summary>
    public int NumOccluders { get; set; }
}

public delegate void RenderPipelineStatsHandler(ref RenderPipelineStats stats);

/// <summary>
/// ID of static pipeline state.
/// </summary>
public enum StaticPipelineStateId : uint
{
    Invalid,
}

/// <summary>
/// Pipeline state, shader parameters and shader resources needed to draw a fullscreen quad.
/// </summary>
public struct DrawQuadParams
{
    private IReadOnlyList<ShaderResourceDesc>? _resources;
    private IReadOnlyList<ShaderParameterDesc>? _parameters;

    public StaticPipelineStateId PipelineStateId { get; set; }
    public PipelineState? PipelineState { get; set; }
    public Vector4f ClipToUVOffsetAndScale { get; set; }
    public Vector2f InvInputSize { get; set; }
    public bool BindSecondaryColorToDiffuse { get; set; }
    public IReadOnlyList<ShaderResourceDesc> Resources
    {
        readonly get => _resources ?? Array.Empty<ShaderResourceDesc>();
        set => _resources = value;
    }
    public IReadOnlyList<ShaderParameterDesc> Parameters
    {
        readonly get => _parameters ?? Array.Empty<ShaderParameterDesc>();
        set => _parameters = value;
    }
}

/// <summary>
/// Region of shadow map that contains one or more shadow split.
/// </summary>
public readonly record struct ShadowMapRegion(uint PageIndex, Texture2D? Texture, IntRect Rect)
{
    /// <summary>
    /// Return shadow map region for one split.
    /// </summary>
    public ShadowMapRegion GetSplit(int split, Vector2i numSplits)
    {
        var splitSize = new Vector2i(Rect.Width / numSplits.X, Rect.Height / numSplits.Y);
        System.Diagnostics.Debug.Assert(splitSize.X * numSplits.X == Rect.Width);
        System.Diagnostics.Debug.Assert(splitSize.Y * numSplits.Y == Rect.Height);
        var splitIndex = new Vector2i(split % numSplits.X, split / numSplits.X);
        var splitBegin = Rect.Min + splitIndex * splitSize;
        return this with { Rect = new IntRect(splitBegin, splitBegin + splitSize) };
    }

    /// <summary>
    /// Return whether the shadow map region is not empty.
    /// </summary>
    public bool IsEmpty => Texture is null;
}

/// <summary>
/// Pipeline state cache callback used to create actual pipeline state.
/// </summary>
/// <remarks>
/// C++ uses a pure-virtual class with multiple inheritance; mapped to an interface in C#.
/// </remarks>
public interface BatchStateCacheCallback
{
    /// <summary>
    /// Create pipeline state for given context and key.
    /// Only attributes that contribute to pipeline state hashes are safe to use.
    /// </summary>
    PipelineState? CreateBatchPipelineState(
        BatchStateCreateKey key,
        BatchStateCreateContext context,
        PipelineStateOutputDesc outputDesc);

    /// <summary>
    /// Create placeholder pipeline state when actual pipeline state creation fails.
    /// </summary>
    PipelineState? CreateBatchPipelineStatePlaceholder(
        int vertexStride,
        PipelineStateOutputDesc outputDesc);
}

/// <summary>
/// Light processor callback.
/// </summary>
/// <remarks>
/// C++ uses a pure-virtual class with multiple inheritance; mapped to an interface in C#.
/// </remarks>
public interface LightProcessorCallback
{
    /// <summary>
    /// Return whether light needs shadow.
    /// </summary>
    bool IsLightShadowed(Light light);

    /// <summary>
    /// Return best shadow map size for given light. Should be safe to call from multiple threads.
    /// </summary>
    int GetShadowMapSize(Light light, int numActiveSplits);

    /// <summary>
    /// Allocate shadow map for one frame.
    /// </summary>
    ShadowMapRegion AllocateTransientShadowMap(Vector2i size);
}

public enum DrawableAmbientMode
{
    Constant,
    Flat,
    Directional
}

public enum DirectLightingMode
{
    Forward,
    DeferredBlinnPhong,
    DeferredPBR
}

public enum SpecularQuality
{
    Disabled,
    Simple,
    Antialiased
}

public enum ReflectionQuality
{
    Vertex,
    Pixel
}

public struct LightProcessorCacheSettings : IEquatable<LightProcessorCacheSettings>
{
    /// <summary>
    /// How many lights there could be before cache is cleaned up aggressively.
    /// </summary>
    public int Budget { get; set; }

    /// <summary>
    /// Number of seconds to keep cached LightProcessor if cache size is within the budget.
    /// </summary>
    public int NormalTimeToLive { get; set; }

    /// <summary>
    /// Number of seconds to keep cached LightProcessor if cache size is out of the budget.
    /// </summary>
    public int AggressiveTimeToLive { get; set; }

    public LightProcessorCacheSettings()
    {
        Budget = 64;
        NormalTimeToLive = 60;
        AggressiveTimeToLive = 2;
    }

    public readonly bool Equals(LightProcessorCacheSettings other)
    {
        return Budget == other.Budget
            && NormalTimeToLive == other.NormalTimeToLive
            && AggressiveTimeToLive == other.AggressiveTimeToLive;
    }

    public static bool operator ==(LightProcessorCacheSettings left, LightProcessorCacheSettings right) => left.Equals(right);

    public static bool operator !=(LightProcessorCacheSettings left, LightProcessorCacheSettings right) => !left.Equals(right);

    public override readonly bool Equals(object? obj) => obj is LightProcessorCacheSettings other && Equals(other);

    public override readonly int GetHashCode() => HashCode.Combine(Budget, NormalTimeToLive, AggressiveTimeToLive);
}

public struct DrawableProcessorSettings : IEquatable<DrawableProcessorSettings>
{
    public MaterialQuality MaterialQuality { get; set; }

    public int MaxVertexLights { get; set; }

    public int MaxPixelLights { get; set; }

    public int PcfKernelSize { get; set; }

    public float NormalOffsetScale { get; set; }

    public LightProcessorCacheSettings LightProcessorCache { get; set; }

    public DrawableProcessorSettings()
    {
        MaterialQuality = MaterialQuality.High;
        MaxVertexLights = 4;
        MaxPixelLights = 4;
        PcfKernelSize = 1;
        NormalOffsetScale = 1.0f;
        LightProcessorCache = new LightProcessorCacheSettings();
    }

    public uint CalculatePipelineStateHash()
    {
        var hash = 0u;
        CombineHash(ref hash, unchecked((uint)MaxVertexLights));
        CombineHash(ref hash, unchecked((uint)PcfKernelSize));
        CombineHash(ref hash, unchecked((uint)BitConverter.SingleToInt32Bits(NormalOffsetScale)));
        return hash;
    }

    public void Validate()
    {
        MaxVertexLights = Math.Clamp(MaxVertexLights, 0, 4);
        MaxPixelLights = Math.Clamp(MaxPixelLights, 0, 256);
        PcfKernelSize = Math.Clamp(PcfKernelSize, 1, 5);
        NormalOffsetScale = Math.Max(0.0f, NormalOffsetScale);
        if (PcfKernelSize == 4)
            PcfKernelSize = 3;
    }

    public readonly bool Equals(DrawableProcessorSettings other)
    {
        return MaterialQuality == other.MaterialQuality
            && MaxVertexLights == other.MaxVertexLights
            && MaxPixelLights == other.MaxPixelLights
            && PcfKernelSize == other.PcfKernelSize
            && NormalOffsetScale.Equals(other.NormalOffsetScale)
            && LightProcessorCache.Equals(other.LightProcessorCache);
    }

    public static bool operator ==(DrawableProcessorSettings left, DrawableProcessorSettings right) => left.Equals(right);

    public static bool operator !=(DrawableProcessorSettings left, DrawableProcessorSettings right) => !left.Equals(right);

    public override readonly bool Equals(object? obj) => obj is DrawableProcessorSettings other && Equals(other);

    public override readonly int GetHashCode() => HashCode.Combine(MaterialQuality, MaxVertexLights, MaxPixelLights, PcfKernelSize, NormalOffsetScale, LightProcessorCache);

    private static void CombineHash(ref uint result, uint hash) =>
        result ^= hash + 0x9e3779b9u + (result << 6) + (result >> 2);
}

public struct InstancingBufferSettings : IEquatable<InstancingBufferSettings>
{
    public bool EnableInstancing { get; set; }

    public int FirstInstancingTexCoord { get; set; }

    public int NumInstancingTexCoords { get; set; }

    public int StepRate { get; set; }

    public InstancingBufferSettings()
    {
        StepRate = 1;
    }

    public readonly uint CalculatePipelineStateHash()
    {
        var hash = 0u;
        hash ^= (EnableInstancing ? 1u : 0u) + 0x9e3779b9u + (hash << 6) + (hash >> 2);
        hash ^= unchecked((uint)FirstInstancingTexCoord) + 0x9e3779b9u + (hash << 6) + (hash >> 2);
        hash ^= unchecked((uint)NumInstancingTexCoords) + 0x9e3779b9u + (hash << 6) + (hash >> 2);
        hash ^= unchecked((uint)StepRate) + 0x9e3779b9u + (hash << 6) + (hash >> 2);
        return hash;
    }

    public void Validate()
    {
    }

    public readonly bool Equals(InstancingBufferSettings other)
    {
        return EnableInstancing == other.EnableInstancing
            && FirstInstancingTexCoord == other.FirstInstancingTexCoord
            && NumInstancingTexCoords == other.NumInstancingTexCoords
            && StepRate == other.StepRate;
    }

    public static bool operator ==(InstancingBufferSettings left, InstancingBufferSettings right) => left.Equals(right);

    public static bool operator !=(InstancingBufferSettings left, InstancingBufferSettings right) => !left.Equals(right);

    public override readonly bool Equals(object? obj) => obj is InstancingBufferSettings other && Equals(other);

    public override readonly int GetHashCode() => unchecked((int)CalculatePipelineStateHash());
}

public struct BatchRendererSettings : IEquatable<BatchRendererSettings>
{
    public bool CubemapBoxProjection { get; set; }

    public DrawableAmbientMode AmbientMode { get; set; }

    public Vector2f VarianceShadowMapParams { get; set; }

    public BatchRendererSettings()
    {
        AmbientMode = DrawableAmbientMode.Directional;
        VarianceShadowMapParams = new Vector2f(0.0000001f, 0.9f);
    }

    public readonly uint CalculatePipelineStateHash()
    {
        var hash = 0u;
        hash ^= (CubemapBoxProjection ? 1u : 0u) + 0x9e3779b9u + (hash << 6) + (hash >> 2);
        hash ^= unchecked((uint)AmbientMode) + 0x9e3779b9u + (hash << 6) + (hash >> 2);
        return hash;
    }

    public void Validate()
    {
    }

    public readonly bool Equals(BatchRendererSettings other)
    {
        return CubemapBoxProjection == other.CubemapBoxProjection
            && AmbientMode == other.AmbientMode
            && VarianceShadowMapParams.Equals(other.VarianceShadowMapParams);
    }

    public static bool operator ==(BatchRendererSettings left, BatchRendererSettings right) => left.Equals(right);

    public static bool operator !=(BatchRendererSettings left, BatchRendererSettings right) => !left.Equals(right);

    public override readonly bool Equals(object? obj) => obj is BatchRendererSettings other && Equals(other);

    public override readonly int GetHashCode() => HashCode.Combine(CubemapBoxProjection, AmbientMode, VarianceShadowMapParams);
}

public struct ShadowMapAllocatorSettings : IEquatable<ShadowMapAllocatorSettings>
{
    public bool EnableVarianceShadowMaps { get; set; }

    public int VarianceShadowMapMultiSample { get; set; }

    public bool Use16BitShadowMaps { get; set; }

    public int ShadowAtlasPageSize { get; set; }

    public float DepthBiasScale { get; set; }

    public float DepthBiasOffset { get; set; }

    public ShadowMapAllocatorSettings()
    {
        VarianceShadowMapMultiSample = 1;
        ShadowAtlasPageSize = 2048;
        DepthBiasScale = 1.0f;
    }

    public uint CalculatePipelineStateHash()
    {
        var hash = 0u;
        CombineHash(ref hash, EnableVarianceShadowMaps ? 1u : 0u);
        CombineHash(ref hash, Use16BitShadowMaps ? 1u : 0u);
        CombineHash(ref hash, unchecked((uint)BitConverter.SingleToInt32Bits(DepthBiasScale)));
        CombineHash(ref hash, unchecked((uint)BitConverter.SingleToInt32Bits(DepthBiasOffset)));
        return hash;
    }

    public void Validate()
    {
        VarianceShadowMapMultiSample = (int)Math.Clamp(MathDefs.ClosestPowerOfTwo((uint)Math.Max(VarianceShadowMapMultiSample, 1)), 1u, 16u);
        ShadowAtlasPageSize = checked((int)Math.Clamp(
            MathDefs.ClosestPowerOfTwo(checked((uint)ShadowAtlasPageSize)), 128u, 16u * 1024u));
        DepthBiasScale = Math.Max(0.0f, DepthBiasScale);
    }

    public readonly bool Equals(ShadowMapAllocatorSettings other)
    {
        return EnableVarianceShadowMaps == other.EnableVarianceShadowMaps
            && VarianceShadowMapMultiSample == other.VarianceShadowMapMultiSample
            && Use16BitShadowMaps == other.Use16BitShadowMaps
            && ShadowAtlasPageSize == other.ShadowAtlasPageSize
            && DepthBiasScale.Equals(other.DepthBiasScale)
            && DepthBiasOffset.Equals(other.DepthBiasOffset);
    }

    public static bool operator ==(ShadowMapAllocatorSettings left, ShadowMapAllocatorSettings right) => left.Equals(right);

    public static bool operator !=(ShadowMapAllocatorSettings left, ShadowMapAllocatorSettings right) => !left.Equals(right);

    public override readonly bool Equals(object? obj) => obj is ShadowMapAllocatorSettings other && Equals(other);

    public override readonly int GetHashCode() => HashCode.Combine(EnableVarianceShadowMaps, VarianceShadowMapMultiSample, Use16BitShadowMaps, ShadowAtlasPageSize, DepthBiasScale, DepthBiasOffset);

    private static void CombineHash(ref uint result, uint hash) =>
        result ^= hash + 0x9e3779b9u + (result << 6) + (result >> 2);
}

public struct OcclusionBufferSettings : IEquatable<OcclusionBufferSettings>
{
    public bool ThreadedOcclusion { get; set; }

    public int MaxOccluderTriangles { get; set; }

    public int OcclusionBufferSize { get; set; }

    public float OccluderSizeThreshold { get; set; }

    public OcclusionBufferSettings()
    {
        MaxOccluderTriangles = 5000;
        OcclusionBufferSize = 256;
        OccluderSizeThreshold = 0.025f;
    }

    public uint CalculatePipelineStateHash() => 0;

    public void Validate()
    {
        OcclusionBufferSize = Math.Clamp(OcclusionBufferSize, 1, 16 * 1024);
    }

    public readonly bool Equals(OcclusionBufferSettings other)
    {
        return ThreadedOcclusion == other.ThreadedOcclusion
            && MaxOccluderTriangles == other.MaxOccluderTriangles
            && OcclusionBufferSize == other.OcclusionBufferSize
            && OccluderSizeThreshold.Equals(other.OccluderSizeThreshold);
    }

    public static bool operator ==(OcclusionBufferSettings left, OcclusionBufferSettings right) => left.Equals(right);

    public static bool operator !=(OcclusionBufferSettings left, OcclusionBufferSettings right) => !left.Equals(right);

    public override readonly bool Equals(object? obj) => obj is OcclusionBufferSettings other && Equals(other);

    public override readonly int GetHashCode() => HashCode.Combine(ThreadedOcclusion, MaxOccluderTriangles, OcclusionBufferSize, OccluderSizeThreshold);
}

public struct SceneProcessorSettings : IEquatable<SceneProcessorSettings>
{
    public MaterialQuality MaterialQuality { get; set; }

    public int MaxVertexLights { get; set; }

    public int MaxPixelLights { get; set; }

    public int PcfKernelSize { get; set; }

    public float NormalOffsetScale { get; set; }

    public LightProcessorCacheSettings LightProcessorCache { get; set; }

    public bool ThreadedOcclusion { get; set; }

    public int MaxOccluderTriangles { get; set; }

    public int OcclusionBufferSize { get; set; }

    public float OccluderSizeThreshold { get; set; }

    public bool CubemapBoxProjection { get; set; }

    public DrawableAmbientMode AmbientMode { get; set; }

    public Vector2f VarianceShadowMapParams { get; set; }

    public SpecularQuality SpecularQuality { get; set; }

    public ReflectionQuality ReflectionQuality { get; set; }

    public bool LinearReflectionMaps { get; set; }

    public bool DepthPrePass { get; set; }

    public bool EnableShadows { get; set; }

    public DirectLightingMode LightingMode { get; set; }

    public int DirectionalShadowSize { get; set; }

    public int SpotShadowSize { get; set; }

    public int PointShadowSize { get; set; }

    public SceneProcessorSettings()
    {
        MaterialQuality = MaterialQuality.High;
        MaxVertexLights = 4;
        MaxPixelLights = 4;
        PcfKernelSize = 1;
        NormalOffsetScale = 1.0f;
        LightProcessorCache = new LightProcessorCacheSettings();
        MaxOccluderTriangles = 5000;
        OcclusionBufferSize = 256;
        OccluderSizeThreshold = 0.025f;
        AmbientMode = DrawableAmbientMode.Directional;
        VarianceShadowMapParams = new Vector2f(0.0000001f, 0.9f);
        SpecularQuality = SpecularQuality.Simple;
        ReflectionQuality = ReflectionQuality.Pixel;
        EnableShadows = true;
        DirectionalShadowSize = 1024;
        SpotShadowSize = 1024;
        PointShadowSize = 256;
    }

    public bool IsDeferredLighting() => LightingMode is DirectLightingMode.DeferredBlinnPhong or DirectLightingMode.DeferredPBR;

    public static implicit operator DrawableProcessorSettings(SceneProcessorSettings settings) => new()
    {
        MaterialQuality = settings.MaterialQuality,
        MaxVertexLights = settings.MaxVertexLights,
        MaxPixelLights = settings.MaxPixelLights,
        PcfKernelSize = settings.PcfKernelSize,
        NormalOffsetScale = settings.NormalOffsetScale,
        LightProcessorCache = settings.LightProcessorCache
    };

    public static implicit operator OcclusionBufferSettings(SceneProcessorSettings settings) => new()
    {
        ThreadedOcclusion = settings.ThreadedOcclusion,
        MaxOccluderTriangles = settings.MaxOccluderTriangles,
        OcclusionBufferSize = settings.OcclusionBufferSize,
        OccluderSizeThreshold = settings.OccluderSizeThreshold
    };

    public static implicit operator BatchRendererSettings(SceneProcessorSettings settings) => new()
    {
        CubemapBoxProjection = settings.CubemapBoxProjection,
        AmbientMode = settings.AmbientMode,
        VarianceShadowMapParams = settings.VarianceShadowMapParams
    };

    public uint CalculatePipelineStateHash()
    {
        var hash = 0u;
        CombineHash(ref hash, ((DrawableProcessorSettings)this).CalculatePipelineStateHash());
        CombineHash(ref hash, ((OcclusionBufferSettings)this).CalculatePipelineStateHash());
        CombineHash(ref hash, ((BatchRendererSettings)this).CalculatePipelineStateHash());
        CombineHash(ref hash, unchecked((uint)SpecularQuality));
        CombineHash(ref hash, unchecked((uint)ReflectionQuality));
        CombineHash(ref hash, LinearReflectionMaps ? 1u : 0u);
        CombineHash(ref hash, EnableShadows ? 1u : 0u);
        CombineHash(ref hash, unchecked((uint)LightingMode));
        return hash;
    }

    public void Validate()
    {
        MaxVertexLights = Math.Clamp(MaxVertexLights, 0, 4);
        MaxPixelLights = Math.Clamp(MaxPixelLights, 0, 256);
        PcfKernelSize = Math.Clamp(PcfKernelSize, 1, 5);
        NormalOffsetScale = Math.Max(0.0f, NormalOffsetScale);
        if (PcfKernelSize == 4)
            PcfKernelSize = 3;

        OcclusionBufferSize = Math.Clamp(OcclusionBufferSize, 1, 16 * 1024);
        DirectionalShadowSize = checked((int)MathDefs.ClosestPowerOfTwo(checked((uint)DirectionalShadowSize)));
        SpotShadowSize = checked((int)MathDefs.ClosestPowerOfTwo(checked((uint)SpotShadowSize)));
        PointShadowSize = checked((int)MathDefs.ClosestPowerOfTwo(checked((uint)PointShadowSize)));
    }

    public readonly bool Equals(SceneProcessorSettings other)
    {
        return MaterialQuality == other.MaterialQuality
            && MaxVertexLights == other.MaxVertexLights
            && MaxPixelLights == other.MaxPixelLights
            && PcfKernelSize == other.PcfKernelSize
            && NormalOffsetScale.Equals(other.NormalOffsetScale)
            && LightProcessorCache.Equals(other.LightProcessorCache)
            && ThreadedOcclusion == other.ThreadedOcclusion
            && MaxOccluderTriangles == other.MaxOccluderTriangles
            && OcclusionBufferSize == other.OcclusionBufferSize
            && OccluderSizeThreshold.Equals(other.OccluderSizeThreshold)
            && CubemapBoxProjection == other.CubemapBoxProjection
            && AmbientMode == other.AmbientMode
            && VarianceShadowMapParams.Equals(other.VarianceShadowMapParams)
            && SpecularQuality == other.SpecularQuality
            && ReflectionQuality == other.ReflectionQuality
            && LinearReflectionMaps == other.LinearReflectionMaps
            && DepthPrePass == other.DepthPrePass
            && EnableShadows == other.EnableShadows
            && LightingMode == other.LightingMode
            && DirectionalShadowSize == other.DirectionalShadowSize
            && SpotShadowSize == other.SpotShadowSize
            && PointShadowSize == other.PointShadowSize;
    }

    public static bool operator ==(SceneProcessorSettings left, SceneProcessorSettings right) => left.Equals(right);

    public static bool operator !=(SceneProcessorSettings left, SceneProcessorSettings right) => !left.Equals(right);

    public override readonly bool Equals(object? obj) => obj is SceneProcessorSettings other && Equals(other);

    public override readonly int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(MaterialQuality);
        hash.Add(MaxVertexLights);
        hash.Add(MaxPixelLights);
        hash.Add(PcfKernelSize);
        hash.Add(NormalOffsetScale);
        hash.Add(LightProcessorCache);
        hash.Add(ThreadedOcclusion);
        hash.Add(MaxOccluderTriangles);
        hash.Add(OcclusionBufferSize);
        hash.Add(OccluderSizeThreshold);
        hash.Add(CubemapBoxProjection);
        hash.Add(AmbientMode);
        hash.Add(VarianceShadowMapParams);
        hash.Add(SpecularQuality);
        hash.Add(ReflectionQuality);
        hash.Add(LinearReflectionMaps);
        hash.Add(DepthPrePass);
        hash.Add(EnableShadows);
        hash.Add(LightingMode);
        hash.Add(DirectionalShadowSize);
        hash.Add(SpotShadowSize);
        hash.Add(PointShadowSize);
        return hash.ToHashCode();
    }

    private static void CombineHash(ref uint result, uint hash) =>
        result ^= hash + 0x9e3779b9u + (result << 6) + (result >> 2);
}

/// <summary>
/// Settings that contribute to shader defines.
/// </summary>
public struct ShaderProgramCompositorSettings : IEquatable<ShaderProgramCompositorSettings>
{
    public RenderBufferManagerSettings RenderBufferManager { get; set; }

    public SceneProcessorSettings SceneProcessor { get; set; }

    public ShadowMapAllocatorSettings ShadowMapAllocator { get; set; }

    public InstancingBufferSettings InstancingBuffer { get; set; }

    public ShaderProgramCompositorSettings()
    {
        RenderBufferManager = new RenderBufferManagerSettings();
        SceneProcessor = new SceneProcessorSettings();
        ShadowMapAllocator = new ShadowMapAllocatorSettings();
        InstancingBuffer = new InstancingBufferSettings();
    }

    public readonly uint CalculatePipelineStateHash()
    {
        var hash = 0u;
        CombineHash(ref hash, RenderBufferManager.CalculatePipelineStateHash());
        CombineHash(ref hash, SceneProcessor.CalculatePipelineStateHash());
        CombineHash(ref hash, ShadowMapAllocator.CalculatePipelineStateHash());
        CombineHash(ref hash, InstancingBuffer.CalculatePipelineStateHash());
        return hash;
    }

    public void Validate()
    {
        var renderBufferManager = RenderBufferManager;
        renderBufferManager.Validate();
        RenderBufferManager = renderBufferManager;
        var sceneProcessor = SceneProcessor;
        sceneProcessor.Validate();
        SceneProcessor = sceneProcessor;
        var shadowMapAllocator = ShadowMapAllocator;
        shadowMapAllocator.Validate();
        ShadowMapAllocator = shadowMapAllocator;
        var instancingBuffer = InstancingBuffer;
        instancingBuffer.Validate();
        InstancingBuffer = instancingBuffer;
    }

    public readonly bool Equals(ShaderProgramCompositorSettings other)
    {
        return RenderBufferManager.Equals(other.RenderBufferManager)
            && SceneProcessor.Equals(other.SceneProcessor)
            && ShadowMapAllocator.Equals(other.ShadowMapAllocator)
            && InstancingBuffer.Equals(other.InstancingBuffer);
    }

    public static bool operator ==(ShaderProgramCompositorSettings left, ShaderProgramCompositorSettings right) => left.Equals(right);

    public static bool operator !=(ShaderProgramCompositorSettings left, ShaderProgramCompositorSettings right) => !left.Equals(right);

    public override readonly bool Equals(object? obj) => obj is ShaderProgramCompositorSettings other && Equals(other);

    public override readonly int GetHashCode() => HashCode.Combine(RenderBufferManager, SceneProcessor, ShadowMapAllocator, InstancingBuffer);

    private static void CombineHash(ref uint result, uint hash) =>
        result ^= hash + 0x9e3779b9u + (result << 6) + (result >> 2);
}

/// <summary>
/// Settings of default render pipeline.
/// </summary>
public struct RenderPipelineSettings : IEquatable<RenderPipelineSettings>
{
    public RenderBufferManagerSettings RenderBufferManager { get; set; }

    public SceneProcessorSettings SceneProcessor { get; set; }

    public ShadowMapAllocatorSettings ShadowMapAllocator { get; set; }

    public InstancingBufferSettings InstancingBuffer { get; set; }

    public bool DrawDebugGeometry { get; set; }

    public RenderPipelineSettings()
    {
        RenderBufferManager = new RenderBufferManagerSettings();
        SceneProcessor = new SceneProcessorSettings();
        ShadowMapAllocator = new ShadowMapAllocatorSettings();
        InstancingBuffer = new InstancingBufferSettings();
        DrawDebugGeometry = true;
    }

    public static implicit operator ShaderProgramCompositorSettings(RenderPipelineSettings settings) => new()
    {
        RenderBufferManager = settings.RenderBufferManager,
        SceneProcessor = settings.SceneProcessor,
        ShadowMapAllocator = settings.ShadowMapAllocator,
        InstancingBuffer = settings.InstancingBuffer
    };

    public readonly uint CalculatePipelineStateHash()
    {
        return ((ShaderProgramCompositorSettings)this).CalculatePipelineStateHash();
    }

    public void Validate()
    {
        var renderBufferManager = RenderBufferManager;
        renderBufferManager.Validate();
        RenderBufferManager = renderBufferManager;
        var sceneProcessor = SceneProcessor;
        sceneProcessor.Validate();
        SceneProcessor = sceneProcessor;
        var shadowMapAllocator = ShadowMapAllocator;
        shadowMapAllocator.Validate();
        ShadowMapAllocator = shadowMapAllocator;
        var instancingBuffer = InstancingBuffer;
        instancingBuffer.Validate();
        InstancingBuffer = instancingBuffer;
    }

    public void AdjustToSupported(Context context)
    {
        var renderDevice = context.GetSubsystem<RenderDevice>();
        if (renderDevice is null || renderDevice.GetRenderDevice() is null)
            return;

        var caps = renderDevice.GetCaps();
        var renderBufferManager = RenderBufferManager;
        if (renderBufferManager.ColorSpace == RenderPipelineColorSpace.LinearHDR && !caps.HdrOutput)
        {
            Log.GetLogger().LogWarning("HDR rendering is not supported, falling back to LDR");
            renderBufferManager.ColorSpace = RenderPipelineColorSpace.LinearLDR;
        }

        if (renderBufferManager.ColorSpace == RenderPipelineColorSpace.LinearLDR && !caps.SrgbOutput)
        {
            Log.GetLogger().LogWarning("sRGB render targets are not supported, falling back to gamma color space");
            renderBufferManager.ColorSpace = RenderPipelineColorSpace.GammaLDR;
        }

        var multiSample = renderDevice.GetSupportedMultiSample(
            RenderPipelineColorSpaceUtility.GetTextureFormat(renderBufferManager.ColorSpace),
            renderBufferManager.MultiSampleLevel);
        if (multiSample != renderBufferManager.MultiSampleLevel)
        {
            Log.GetLogger().LogWarning("MSAA level {RequestedLevel} is not supported, falling back to {SupportedLevel}",
                renderBufferManager.MultiSampleLevel, multiSample);
            renderBufferManager.MultiSampleLevel = multiSample;
        }
        RenderBufferManager = renderBufferManager;

        var sceneProcessor = SceneProcessor;
        if (sceneProcessor.IsDeferredLighting() && !caps.ReadOnlyDepth)
            sceneProcessor.LightingMode = DirectLightingMode.Forward;
        SceneProcessor = sceneProcessor;

        var shadowMapAllocator = ShadowMapAllocator;
        if (!renderDevice.IsRenderTargetFormatSupported(TextureFormat.TexFormatRg32Float))
            shadowMapAllocator.EnableVarianceShadowMaps = false;

        shadowMapAllocator.VarianceShadowMapMultiSample = renderDevice.GetSupportedMultiSample(
            TextureFormat.TexFormatRg32Float, shadowMapAllocator.VarianceShadowMapMultiSample);
        shadowMapAllocator.ShadowAtlasPageSize = Math.Min(shadowMapAllocator.ShadowAtlasPageSize, caps.MaxRenderTargetSize);
        ShadowMapAllocator = shadowMapAllocator;
    }

    public void PropagateImpliedSettings()
    {
        // Deferred rendering expects certain properties from render textures
        if (SceneProcessor.IsDeferredLighting())
        {
            var renderBufferManager = RenderBufferManager;
            renderBufferManager.ColorUsableWithMultipleRenderTargets = true;
            renderBufferManager.StencilBuffer = true;
            renderBufferManager.ReadableDepth = true;
            renderBufferManager.InheritMultiSampleLevel = false;
            RenderBufferManager = renderBufferManager;
        }

        // Setup instancing buffer format
        if (InstancingBuffer.EnableInstancing)
        {
            var instancingBuffer = InstancingBuffer;
            instancingBuffer.FirstInstancingTexCoord = 4;
            instancingBuffer.NumInstancingTexCoords = SceneProcessor.AmbientMode switch
            {
                DrawableAmbientMode.Constant => 3,
                DrawableAmbientMode.Flat => 4,
                DrawableAmbientMode.Directional => 10,
                _ => 3
            };
            InstancingBuffer = instancingBuffer;
        }
    }

    public void AdjustForRenderPath(RenderOutputFlag flags)
    {
        var renderBufferManager = RenderBufferManager;
        renderBufferManager.FilteredColor = (flags & RenderOutputFlag.NeedColorOutputBilinear) == RenderOutputFlag.NeedColorOutputBilinear;
        RenderBufferManager = renderBufferManager;
    }

    public readonly bool Equals(RenderPipelineSettings other)
    {
        return RenderBufferManager.Equals(other.RenderBufferManager)
            && SceneProcessor.Equals(other.SceneProcessor)
            && ShadowMapAllocator.Equals(other.ShadowMapAllocator)
            && InstancingBuffer.Equals(other.InstancingBuffer)
            && DrawDebugGeometry == other.DrawDebugGeometry;
    }

    public static bool operator ==(RenderPipelineSettings left, RenderPipelineSettings right) => left.Equals(right);

    public static bool operator !=(RenderPipelineSettings left, RenderPipelineSettings right) => !left.Equals(right);

    public override readonly bool Equals(object? obj) => obj is RenderPipelineSettings other && Equals(other);

    public override readonly int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(RenderBufferManager);
        hash.Add(SceneProcessor);
        hash.Add(ShadowMapAllocator);
        hash.Add(InstancingBuffer);
        hash.Add(DrawDebugGeometry);
        return hash.ToHashCode();
    }
}

/// <summary>
/// Helper for pipeline state creation.
/// Keep entire string because it should be used only on startup.
/// </summary>
public readonly record struct NamedSamplerStateDesc(string Name, SamplerStateDesc Desc);

/// <summary>
/// Reference to input shader resource. Only textures are supported now.
/// </summary>
public readonly record struct ShaderResourceDesc(StringHash Name, Texture? Texture);

/// <summary>
/// Generic description of shader parameter.
/// Beware of Variant allocations for types larger than Vector4f!
/// </summary>
public readonly record struct ShaderParameterDesc(StringHash Name, Variant Value);

public sealed class RenderOutputTargetInfo
{
    public RenderOutputTargetInfo(RenderTargetView colorTarget, IntRect fullViewportRect, TextureFormat colorFormat,
        RenderTargetView? depthStencilTarget = null, TextureFormat depthStencilFormat = TextureFormat.TexFormatUnknown,
        int multiSample = 1, Texture? colorTexture = null, Texture? depthStencilTexture = null,
        bool bilinearFiltered = false, bool linearColorSpace = false, bool hdr = false,
        bool hasReadableDepth = false, bool hasStencil = false)
    {
        ColorTarget = colorTarget;
        FullViewportRect = fullViewportRect;
        ColorFormat = colorFormat;
        DepthStencilTarget = depthStencilTarget;
        DepthStencilFormat = depthStencilFormat;
        MultiSample = Math.Max(multiSample, 1);
        ColorTexture = colorTexture;
        DepthStencilTexture = depthStencilTexture;
        BilinearFiltered = bilinearFiltered;
        LinearColorSpace = linearColorSpace;
        HDR = hdr;
        HasReadableDepth = hasReadableDepth;
        HasStencil = hasStencil;
    }

    public RenderTargetView ColorTarget { get; }

    public RenderTargetView? DepthStencilTarget { get; }

    public IntRect FullViewportRect { get; }

    public TextureFormat ColorFormat { get; }

    public TextureFormat DepthStencilFormat { get; }

    public int MultiSample { get; }

    public Texture? ColorTexture { get; }

    public Texture? DepthStencilTexture { get; }

    public bool BilinearFiltered { get; }

    public bool LinearColorSpace { get; }

    public bool HDR { get; }

    public bool HasReadableDepth { get; }

    public bool HasStencil { get; }

    public bool HasDepthStencil => DepthStencilTarget is not null || DepthStencilFormat != TextureFormat.TexFormatUnknown;

    public static RenderOutputTargetInfo FromTexture(Texture colorTexture, IntRect fullViewportRect,
        Texture? depthStencilTexture = null, int multiSample = 0)
    {
        ArgumentNullException.ThrowIfNull(colorTexture);

        var outputMultiSample = multiSample > 0 ? multiSample : colorTexture.MultiSample;
        return new RenderOutputTargetInfo(
            RenderTargetView.Texture(colorTexture),
            fullViewportRect,
            colorTexture.Format,
            depthStencilTexture is not null ? RenderTargetView.Texture(depthStencilTexture) : null,
            depthStencilTexture?.Format ?? TextureFormat.TexFormatUnknown,
            outputMultiSample,
            colorTexture,
            depthStencilTexture,
            bilinearFiltered: colorTexture.GetFilterMode() is TextureFilterMode.Bilinear or TextureFilterMode.Trilinear or TextureFilterMode.Anisotropic,
            linearColorSpace: colorTexture.GetLinear() || colorTexture.GetSRGB() || TextureFormatUtility.IsFloatColorFormat(colorTexture.Format),
            hdr: TextureFormatUtility.IsFloatColorFormat(colorTexture.Format),
            hasReadableDepth: depthStencilTexture is not null,
            hasStencil: depthStencilTexture?.Format == TextureFormat.TexFormatD24UnormS8Uint);
    }
}

/// <summary>
/// Common parameters of rendered frame.
/// </summary>
public struct CommonFrameInfo
{
    public uint FrameNumber;

    public float TimeStep;

    public Vector2i ViewportSize;

    public IntRect ViewportRect;

    public Viewport? Viewport;

    public RenderOutputTargetInfo? RenderTarget;

    public (Camera? First, Camera? Second) Cameras;
}

/// <summary>
/// Base interface of render pipeline required by Render Pipeline classes.
/// </summary>
public interface IRenderPipelineInterface
{
    Context? GetContext();

    RenderPipelineDebugger GetDebugger();

    bool IsLinearColorSpace { get; }

    event Action<CommonFrameInfo>? UpdateBegin;

    event Action<CommonFrameInfo>? UpdateEnd;

    event Action<CommonFrameInfo>? RenderBegin;

    event Action<CommonFrameInfo>? RenderEnd;

    event Action? PipelineStatesInvalidated;

    event RenderPipelineStatsHandler? CollectStatistics;
}

/// <summary>
/// Base interface of render pipeline viewport instance.
/// </summary>
public abstract class RenderPipelineView : Object, IRenderPipelineInterface
{
    private readonly RenderPipeline _renderPipeline;
    private readonly RenderPipelineDebugger _debugger = new();
    protected readonly List<ShaderParameterDesc> _frameParameters = [];

    protected RenderPipelineView(RenderPipeline renderPipeline)
    {
        _renderPipeline = renderPipeline ?? throw new ArgumentNullException(nameof(renderPipeline));
    }

    public RenderPipeline GetRenderPipeline() => _renderPipeline;

    protected void UpdateFrameParameters()
    {
        for (var i = _frameParameters.Count - 1; i >= 0; --i)
        {
            var value = _frameParameters[i].Value;
            value.Dispose();
        }
        _frameParameters.Clear();
        foreach (var (name, value) in _renderPipeline.GetFrameShaderParameters())
            _frameParameters.Add(new ShaderParameterDesc(new StringHash(name), value.Clone()));
    }

    public override void Dispose()
    {
        for (var i = _frameParameters.Count - 1; i >= 0; --i)
        {
            var value = _frameParameters[i].Value;
            value.Dispose();
        }
        _frameParameters.Clear();
        base.Dispose();
    }

    public Context? GetContext() => Context.Instance;

    public RenderPipelineDebugger GetDebugger() => _debugger;

    public abstract bool IsLinearColorSpace { get; }

    /// <summary>
    /// Called in the beginning of the update to check if pipeline should be executed.
    /// </summary>
    public abstract bool Define(RenderSurface? renderTarget, Viewport viewport, Graphics graphics);

    /// <summary>
    /// Called for defined pipelines before rendering. Frame info is only partially filled.
    /// </summary>
    public abstract void Update(Graphics graphics);

    /// <summary>
    /// Called for updated pipelines in appropriate order.
    /// </summary>
    public abstract void Render(Graphics graphics, MaterialQuality materialQuality);

    /// <summary>
    /// Return frame info with all members filled.
    /// </summary>
    public abstract FrameInfo GetFrameInfo();

    /// <summary>
    /// Return render pipeline statistics for profiling.
    /// </summary>
    public abstract ref readonly RenderPipelineStats GetStats();

    /// <summary>Draw debug geometries, if applicable.</summary>
    public abstract void DrawDebugGeometries(bool depthTest);

    /// <summary>Draw debug lights, if applicable.</summary>
    public abstract void DrawDebugLights(bool depthTest);

    public event Action<CommonFrameInfo>? UpdateBegin;

    public event Action<CommonFrameInfo>? UpdateEnd;

    public event Action<CommonFrameInfo>? RenderBegin;

    public event Action<CommonFrameInfo>? RenderEnd;

    public event Action? PipelineStatesInvalidated;

    public event RenderPipelineStatsHandler? CollectStatistics;

    protected void RaiseUpdateBegin(CommonFrameInfo frameInfo) => UpdateBegin?.Invoke(frameInfo);

    protected void RaiseUpdateEnd(CommonFrameInfo frameInfo) => UpdateEnd?.Invoke(frameInfo);

    protected void RaiseRenderBegin(CommonFrameInfo frameInfo) => RenderBegin?.Invoke(frameInfo);

    protected void RaiseRenderEnd(CommonFrameInfo frameInfo) => RenderEnd?.Invoke(frameInfo);

    protected void RaisePipelineStatesInvalidated() => PipelineStatesInvalidated?.Invoke();

    protected void RaiseCollectStatistics(ref RenderPipelineStats stats) => CollectStatistics?.Invoke(ref stats);
}
