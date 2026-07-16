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
            RenderPipelineColorSpace.GammaLDR => TextureFormat.RGBA8,
            RenderPipelineColorSpace.LinearLDR => TextureFormat.RGBA8,
            RenderPipelineColorSpace.LinearHDR => TextureFormat.RGBA16F,
            _ => TextureFormat.RGBA8
        };
    }
}

/// <summary>
/// Rarely-changing settings of render buffer manager.
/// </summary>
public sealed class RenderBufferManagerSettings : IEquatable<RenderBufferManagerSettings>
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

    public uint CalculatePipelineStateHash()
    {
        var hash = new HashCode();
        hash.Add(ReadableDepth);
        return unchecked((uint)hash.ToHashCode());
    }

    public void Validate()
    {
        MultiSampleLevel = (int)Math.Clamp(MathDefs.ClosestPowerOfTwo((uint)Math.Max(MultiSampleLevel, 1)), 1u, 16u);
    }

    public bool Equals(RenderBufferManagerSettings? other)
    {
        if (other is null)
            return false;

        return InheritMultiSampleLevel == other.InheritMultiSampleLevel
            && MultiSampleLevel == other.MultiSampleLevel
            && ColorSpace == other.ColorSpace
            && FilteredColor == other.FilteredColor
            && StencilBuffer == other.StencilBuffer
            && ReadableDepth == other.ReadableDepth
            && ColorUsableWithMultipleRenderTargets == other.ColorUsableWithMultipleRenderTargets;
    }

    public override bool Equals(object? obj) => Equals(obj as RenderBufferManagerSettings);

    public override int GetHashCode()
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

    public bool FilteredColor { get; set; }

    public bool ReadableDepth { get; set; }
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
public sealed class RenderPipelineStats
{
    /// <summary>
    /// Total number of lights processed.
    /// </summary>
    public uint NumLights { get; set; }

    /// <summary>
    /// Total number of lights with shadows processed.
    /// </summary>
    public uint NumShadowedLights { get; set; }

    /// <summary>
    /// Total number of geometries in the frame (excluding shadow casters).
    /// </summary>
    public uint NumGeometries { get; set; }

    /// <summary>
    /// Number of occluders rendered.
    /// </summary>
    public uint NumOccluders { get; set; }
}

public readonly record struct BatchStateLookupKey(
    uint DrawableHash,
    uint PixelLightHash,
    GeometryType GeometryType,
    Geometry? Geometry,
    Material? Material,
    Pass? Pass);

public readonly record struct BatchStateCreateKey(
    uint DrawableHash,
    uint PixelLightHash,
    GeometryType GeometryType,
    Geometry? Geometry,
    Material? Material,
    Pass? Pass,
    Drawable? Drawable,
    LightProcessor? PixelLight,
    uint PixelLightIndex)
{
    public BatchStateLookupKey LookupKey => new(DrawableHash, PixelLightHash, GeometryType, Geometry, Material, Pass);
}

public readonly record struct BatchStateCreateContext(Object? Pass, uint SubpassIndex, uint ShadowSplitIndex);

public readonly record struct UIBatchStateKey(
    bool LinearOutput,
    PipelineStateOutputDesc OutputDesc,
    Material? Material,
    Pass? Pass,
    BlendMode BlendMode,
    uint SamplerStateHash);

public readonly record struct UIBatchStateCreateContext(
    VertexBuffer? VertexBuffer,
    IndexBuffer? IndexBuffer,
    SamplerStateDesc? DefaultSampler);

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
    public ShadowMapRegion GetSplit(uint split, Vector2i numSplits)
    {
        var splitSize = new Vector2i(Rect.Width / numSplits.X, Rect.Height / numSplits.Y);
        System.Diagnostics.Debug.Assert(splitSize.X * numSplits.X == Rect.Width);
        System.Diagnostics.Debug.Assert(splitSize.Y * numSplits.Y == Rect.Height);
        var splitIndex = new Vector2i((int)(split % (uint)numSplits.X), (int)(split / (uint)numSplits.X));
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
public abstract class BatchStateCacheCallback
{
    /// <summary>
    /// Create pipeline state for given context and key.
    /// Only attributes that contribute to pipeline state hashes are safe to use.
    /// </summary>
    public abstract PipelineState? CreateBatchPipelineState(
        BatchStateCreateKey key,
        BatchStateCreateContext context,
        PipelineStateOutputDesc outputDesc);

    /// <summary>
    /// Create placeholder pipeline state when actual pipeline state creation fails.
    /// </summary>
    public abstract PipelineState? CreateBatchPipelineStatePlaceholder(
        uint vertexStride,
        PipelineStateOutputDesc outputDesc);
}

/// <summary>
/// Pipeline state cache callback used to create actual pipeline state for UI batches.
/// </summary>
public abstract class UIBatchStateCacheCallback
{
    /// <summary>
    /// Create pipeline state for given key.
    /// Only attributes that contribute to pipeline state hashes are safe to use.
    /// </summary>
    public abstract PipelineState? CreateUIBatchPipelineState(
        UIBatchStateKey key,
        UIBatchStateCreateContext context);
}

/// <summary>
/// Light processor callback.
/// </summary>
public abstract class LightProcessorCallback
{
    /// <summary>
    /// Return whether light needs shadow.
    /// </summary>
    public abstract bool IsLightShadowed(Light light);

    /// <summary>
    /// Return best shadow map size for given light. Should be safe to call from multiple threads.
    /// </summary>
    public abstract uint GetShadowMapSize(Light light, uint numActiveSplits);

    /// <summary>
    /// Allocate shadow map for one frame.
    /// </summary>
    public abstract ShadowMapRegion AllocateTransientShadowMap(Vector2i size);
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

public sealed class LightProcessorCacheSettings : IEquatable<LightProcessorCacheSettings>
{
    /// <summary>
    /// How many lights there could be before cache is cleaned up aggressively.
    /// </summary>
    public uint Budget { get; set; } = 64;

    /// <summary>
    /// Number of seconds to keep cached LightProcessor if cache size is within the budget.
    /// </summary>
    public uint NormalTimeToLive { get; set; } = 60;

    /// <summary>
    /// Number of seconds to keep cached LightProcessor if cache size is out of the budget.
    /// </summary>
    public uint AggressiveTimeToLive { get; set; } = 2;

    public bool Equals(LightProcessorCacheSettings? other)
    {
        return other is not null
            && Budget == other.Budget
            && NormalTimeToLive == other.NormalTimeToLive
            && AggressiveTimeToLive == other.AggressiveTimeToLive;
    }

    public override bool Equals(object? obj) => Equals(obj as LightProcessorCacheSettings);

    public override int GetHashCode() => HashCode.Combine(Budget, NormalTimeToLive, AggressiveTimeToLive);
}

public sealed class DrawableProcessorSettings : IEquatable<DrawableProcessorSettings>
{
    public MaterialQuality MaterialQuality { get; set; } = MaterialQuality.High;

    public uint MaxVertexLights { get; set; } = 4;

    public uint MaxPixelLights { get; set; } = 4;

    public uint PcfKernelSize { get; set; } = 1;

    public float NormalOffsetScale { get; set; } = 1.0f;

    public LightProcessorCacheSettings LightProcessorCache { get; } = new();

    public uint CalculatePipelineStateHash()
    {
        var hash = new HashCode();
        hash.Add(MaxVertexLights);
        hash.Add(PcfKernelSize);
        hash.Add(NormalOffsetScale);
        return unchecked((uint)hash.ToHashCode());
    }

    public void Validate()
    {
        MaxVertexLights = Math.Clamp(MaxVertexLights, 0u, 4u);
        MaxPixelLights = Math.Clamp(MaxPixelLights, 0u, 256u);
        PcfKernelSize = Math.Clamp(PcfKernelSize, 1u, 5u);
        NormalOffsetScale = Math.Max(0.0f, NormalOffsetScale);
        if (PcfKernelSize == 4)
            PcfKernelSize = 3;
    }

    public bool Equals(DrawableProcessorSettings? other)
    {
        return other is not null
            && MaterialQuality == other.MaterialQuality
            && MaxVertexLights == other.MaxVertexLights
            && MaxPixelLights == other.MaxPixelLights
            && PcfKernelSize == other.PcfKernelSize
            && NormalOffsetScale.Equals(other.NormalOffsetScale)
            && LightProcessorCache.Equals(other.LightProcessorCache);
    }

    public override bool Equals(object? obj) => Equals(obj as DrawableProcessorSettings);

    public override int GetHashCode() => HashCode.Combine(MaterialQuality, MaxVertexLights, MaxPixelLights, PcfKernelSize, NormalOffsetScale, LightProcessorCache);
}

public sealed class InstancingBufferSettings : IEquatable<InstancingBufferSettings>
{
    public bool EnableInstancing { get; set; }

    public uint FirstInstancingTexCoord { get; set; }

    public uint NumInstancingTexCoords { get; set; }

    public uint StepRate { get; set; } = 1;

    public uint CalculatePipelineStateHash()
    {
        var hash = new HashCode();
        hash.Add(EnableInstancing);
        hash.Add(FirstInstancingTexCoord);
        hash.Add(NumInstancingTexCoords);
        hash.Add(StepRate);
        return unchecked((uint)hash.ToHashCode());
    }

    public void Validate()
    {
    }

    public bool Equals(InstancingBufferSettings? other)
    {
        return other is not null
            && EnableInstancing == other.EnableInstancing
            && FirstInstancingTexCoord == other.FirstInstancingTexCoord
            && NumInstancingTexCoords == other.NumInstancingTexCoords
            && StepRate == other.StepRate;
    }

    public override bool Equals(object? obj) => Equals(obj as InstancingBufferSettings);

    public override int GetHashCode() => HashCode.Combine(EnableInstancing, FirstInstancingTexCoord, NumInstancingTexCoords, StepRate);
}

public sealed class BatchRendererSettings : IEquatable<BatchRendererSettings>
{
    public bool CubemapBoxProjection { get; set; }

    public DrawableAmbientMode AmbientMode { get; set; } = DrawableAmbientMode.Directional;

    public Vector2f VarianceShadowMapParams { get; set; } = new(0.0000001f, 0.9f);

    public uint CalculatePipelineStateHash()
    {
        var hash = new HashCode();
        hash.Add(CubemapBoxProjection);
        hash.Add(AmbientMode);
        return unchecked((uint)hash.ToHashCode());
    }

    public void Validate()
    {
    }

    public bool Equals(BatchRendererSettings? other)
    {
        return other is not null
            && CubemapBoxProjection == other.CubemapBoxProjection
            && AmbientMode == other.AmbientMode
            && VarianceShadowMapParams.Equals(other.VarianceShadowMapParams);
    }

    public override bool Equals(object? obj) => Equals(obj as BatchRendererSettings);

    public override int GetHashCode() => HashCode.Combine(CubemapBoxProjection, AmbientMode, VarianceShadowMapParams);
}

public sealed class ShadowMapAllocatorSettings : IEquatable<ShadowMapAllocatorSettings>
{
    public bool EnableVarianceShadowMaps { get; set; }

    public int VarianceShadowMapMultiSample { get; set; } = 1;

    public bool Use16BitShadowMaps { get; set; }

    public uint ShadowAtlasPageSize { get; set; } = 2048;

    public float DepthBiasScale { get; set; } = 1.0f;

    public float DepthBiasOffset { get; set; }

    public uint CalculatePipelineStateHash()
    {
        var hash = new HashCode();
        hash.Add(EnableVarianceShadowMaps);
        hash.Add(Use16BitShadowMaps);
        hash.Add(DepthBiasScale);
        hash.Add(DepthBiasOffset);
        return unchecked((uint)hash.ToHashCode());
    }

    public void Validate()
    {
        VarianceShadowMapMultiSample = (int)Math.Clamp(MathDefs.ClosestPowerOfTwo((uint)Math.Max(VarianceShadowMapMultiSample, 1)), 1u, 16u);
        ShadowAtlasPageSize = Math.Clamp(MathDefs.ClosestPowerOfTwo(ShadowAtlasPageSize), 128u, 16u * 1024u);
        DepthBiasScale = Math.Max(0.0f, DepthBiasScale);
    }

    public bool Equals(ShadowMapAllocatorSettings? other)
    {
        return other is not null
            && EnableVarianceShadowMaps == other.EnableVarianceShadowMaps
            && VarianceShadowMapMultiSample == other.VarianceShadowMapMultiSample
            && Use16BitShadowMaps == other.Use16BitShadowMaps
            && ShadowAtlasPageSize == other.ShadowAtlasPageSize
            && DepthBiasScale.Equals(other.DepthBiasScale)
            && DepthBiasOffset.Equals(other.DepthBiasOffset);
    }

    public override bool Equals(object? obj) => Equals(obj as ShadowMapAllocatorSettings);

    public override int GetHashCode() => HashCode.Combine(EnableVarianceShadowMaps, VarianceShadowMapMultiSample, Use16BitShadowMaps, ShadowAtlasPageSize, DepthBiasScale, DepthBiasOffset);
}

public sealed class OcclusionBufferSettings : IEquatable<OcclusionBufferSettings>
{
    public bool ThreadedOcclusion { get; set; }

    public uint MaxOccluderTriangles { get; set; } = 5000;

    public uint OcclusionBufferSize { get; set; } = 256;

    public float OccluderSizeThreshold { get; set; } = 0.025f;

    public uint CalculatePipelineStateHash() => 0;

    public void Validate()
    {
        OcclusionBufferSize = Math.Clamp(OcclusionBufferSize, 1u, 16u * 1024u);
    }

    public bool Equals(OcclusionBufferSettings? other)
    {
        return other is not null
            && ThreadedOcclusion == other.ThreadedOcclusion
            && MaxOccluderTriangles == other.MaxOccluderTriangles
            && OcclusionBufferSize == other.OcclusionBufferSize
            && OccluderSizeThreshold.Equals(other.OccluderSizeThreshold);
    }

    public override bool Equals(object? obj) => Equals(obj as OcclusionBufferSettings);

    public override int GetHashCode() => HashCode.Combine(ThreadedOcclusion, MaxOccluderTriangles, OcclusionBufferSize, OccluderSizeThreshold);
}

public sealed class SceneProcessorSettings : IEquatable<SceneProcessorSettings>
{
    public MaterialQuality MaterialQuality { get; set; } = MaterialQuality.High;

    public uint MaxVertexLights { get; set; } = 4;

    public uint MaxPixelLights { get; set; } = 4;

    public uint PcfKernelSize { get; set; } = 1;

    public float NormalOffsetScale { get; set; } = 1.0f;

    public LightProcessorCacheSettings LightProcessorCache { get; } = new();

    public bool ThreadedOcclusion { get; set; }

    public uint MaxOccluderTriangles { get; set; } = 5000;

    public uint OcclusionBufferSize { get; set; } = 256;

    public float OccluderSizeThreshold { get; set; } = 0.025f;

    public bool CubemapBoxProjection { get; set; }

    public DrawableAmbientMode AmbientMode { get; set; } = DrawableAmbientMode.Directional;

    public Vector2f VarianceShadowMapParams { get; set; } = new(0.0000001f, 0.9f);

    public SpecularQuality SpecularQuality { get; set; } = SpecularQuality.Simple;

    public ReflectionQuality ReflectionQuality { get; set; } = ReflectionQuality.Pixel;

    public bool LinearReflectionMaps { get; set; }

    public bool DepthPrePass { get; set; }

    public bool EnableShadows { get; set; } = true;

    public DirectLightingMode LightingMode { get; set; }

    public uint DirectionalShadowSize { get; set; } = 1024;

    public uint SpotShadowSize { get; set; } = 1024;

    public uint PointShadowSize { get; set; } = 256;

    public bool IsDeferredLighting() => LightingMode is DirectLightingMode.DeferredBlinnPhong or DirectLightingMode.DeferredPBR;

    public uint CalculatePipelineStateHash()
    {
        var hash = new HashCode();
        hash.Add(MaxVertexLights);
        hash.Add(PcfKernelSize);
        hash.Add(NormalOffsetScale);
        hash.Add(CubemapBoxProjection);
        hash.Add(AmbientMode);
        hash.Add(SpecularQuality);
        hash.Add(ReflectionQuality);
        hash.Add(LinearReflectionMaps);
        hash.Add(EnableShadows);
        hash.Add(LightingMode);
        return unchecked((uint)hash.ToHashCode());
    }

    public void Validate()
    {
        MaxVertexLights = Math.Clamp(MaxVertexLights, 0u, 4u);
        MaxPixelLights = Math.Clamp(MaxPixelLights, 0u, 256u);
        PcfKernelSize = Math.Clamp(PcfKernelSize, 1u, 5u);
        NormalOffsetScale = Math.Max(0.0f, NormalOffsetScale);
        if (PcfKernelSize == 4)
            PcfKernelSize = 3;

        OcclusionBufferSize = Math.Clamp(OcclusionBufferSize, 1u, 16u * 1024u);
        DirectionalShadowSize = MathDefs.ClosestPowerOfTwo(DirectionalShadowSize);
        SpotShadowSize = MathDefs.ClosestPowerOfTwo(SpotShadowSize);
        PointShadowSize = MathDefs.ClosestPowerOfTwo(PointShadowSize);
    }

    public bool Equals(SceneProcessorSettings? other)
    {
        return other is not null
            && MaterialQuality == other.MaterialQuality
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

    public override bool Equals(object? obj) => Equals(obj as SceneProcessorSettings);

    public override int GetHashCode()
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
}

/// <summary>
/// Settings that contribute to shader defines.
/// </summary>
public class ShaderProgramCompositorSettings : IEquatable<ShaderProgramCompositorSettings>
{
    public RenderBufferManagerSettings RenderBufferManager { get; } = new();

    public SceneProcessorSettings SceneProcessor { get; } = new();

    public ShadowMapAllocatorSettings ShadowMapAllocator { get; } = new();

    public InstancingBufferSettings InstancingBuffer { get; } = new();

    public uint CalculatePipelineStateHash()
    {
        var hash = new HashCode();
        hash.Add(RenderBufferManager.CalculatePipelineStateHash());
        hash.Add(SceneProcessor.CalculatePipelineStateHash());
        hash.Add(ShadowMapAllocator.CalculatePipelineStateHash());
        hash.Add(InstancingBuffer.CalculatePipelineStateHash());
        return unchecked((uint)hash.ToHashCode());
    }

    public void Validate()
    {
        RenderBufferManager.Validate();
        SceneProcessor.Validate();
        ShadowMapAllocator.Validate();
        InstancingBuffer.Validate();
    }

    public bool Equals(ShaderProgramCompositorSettings? other)
    {
        return other is not null
            && RenderBufferManager.Equals(other.RenderBufferManager)
            && SceneProcessor.Equals(other.SceneProcessor)
            && ShadowMapAllocator.Equals(other.ShadowMapAllocator)
            && InstancingBuffer.Equals(other.InstancingBuffer);
    }

    public override bool Equals(object? obj) => Equals(obj as ShaderProgramCompositorSettings);

    public override int GetHashCode() => HashCode.Combine(RenderBufferManager, SceneProcessor, ShadowMapAllocator, InstancingBuffer);
}

/// <summary>
/// Settings of default render pipeline.
/// </summary>
public sealed class RenderPipelineSettings : ShaderProgramCompositorSettings, IEquatable<RenderPipelineSettings>
{
    public bool DrawDebugGeometry { get; set; } = true;

    public new uint CalculatePipelineStateHash()
    {
        var hash = new HashCode();
        hash.Add(base.CalculatePipelineStateHash());
        hash.Add(DrawDebugGeometry);
        return unchecked((uint)hash.ToHashCode());
    }

    public new void Validate()
    {
        base.Validate();
    }

    public void AdjustToSupported(Graphics? graphics)
    {
        if (graphics is null)
            return;

        var caps = graphics.GetRenderDeviceCaps();
        if (RenderBufferManager.ColorSpace == RenderPipelineColorSpace.LinearHDR && !caps.HdrOutput)
            RenderBufferManager.ColorSpace = RenderPipelineColorSpace.LinearLDR;

        if (RenderBufferManager.ColorSpace == RenderPipelineColorSpace.LinearLDR && !caps.SrgbOutput)
            RenderBufferManager.ColorSpace = RenderPipelineColorSpace.GammaLDR;

        RenderBufferManager.MultiSampleLevel = graphics.GetSupportedMultiSample(RenderPipelineColorSpaceUtility.GetTextureFormat(RenderBufferManager.ColorSpace), RenderBufferManager.MultiSampleLevel);

        if (SceneProcessor.IsDeferredLighting() && !caps.ReadOnlyDepth)
            SceneProcessor.LightingMode = DirectLightingMode.Forward;

        if (!graphics.IsRenderTargetFormatSupported(TextureFormat.RG16F))
            ShadowMapAllocator.EnableVarianceShadowMaps = false;

        ShadowMapAllocator.VarianceShadowMapMultiSample = graphics.GetSupportedMultiSample(TextureFormat.RG16F, ShadowMapAllocator.VarianceShadowMapMultiSample);
        ShadowMapAllocator.ShadowAtlasPageSize = Math.Min(ShadowMapAllocator.ShadowAtlasPageSize, caps.MaxRenderTargetSize);
    }

    public void PropagateImpliedSettings()
    {
        if (SceneProcessor.IsDeferredLighting())
        {
            RenderBufferManager.ColorUsableWithMultipleRenderTargets = true;
            RenderBufferManager.StencilBuffer = true;
            RenderBufferManager.ReadableDepth = true;
            RenderBufferManager.InheritMultiSampleLevel = false;
        }

        if (InstancingBuffer.EnableInstancing)
        {
            InstancingBuffer.FirstInstancingTexCoord = 4;
            InstancingBuffer.NumInstancingTexCoords = SceneProcessor.AmbientMode switch
            {
                DrawableAmbientMode.Constant => 3,
                DrawableAmbientMode.Flat => 4,
                DrawableAmbientMode.Directional => 10,
                _ => 3
            };
        }
    }

    public void AdjustForRenderPath(RenderOutputFlag flags)
    {
        RenderBufferManager.FilteredColor = (flags & RenderOutputFlag.NeedColorOutputBilinear) == RenderOutputFlag.NeedColorOutputBilinear;
    }

    public bool Equals(RenderPipelineSettings? other)
    {
        if (other is null)
            return false;

        return base.Equals(other)
            && DrawDebugGeometry == other.DrawDebugGeometry;
    }

    public override bool Equals(object? obj) => Equals(obj as RenderPipelineSettings);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(base.GetHashCode());
        hash.Add(DrawDebugGeometry);
        return hash.ToHashCode();
    }
}

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
        RenderTargetView? depthStencilTarget = null, TextureFormat depthStencilFormat = TextureFormat.Unknown,
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

    public bool HasDepthStencil => DepthStencilTarget is not null || DepthStencilFormat != TextureFormat.Unknown;

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
            depthStencilTexture?.Format ?? TextureFormat.Unknown,
            outputMultiSample,
            colorTexture,
            depthStencilTexture,
            bilinearFiltered: colorTexture.GetFilterMode() is TextureFilterMode.Bilinear or TextureFilterMode.Trilinear or TextureFilterMode.Anisotropic,
            linearColorSpace: colorTexture.GetLinear() || colorTexture.GetSRGB() || TextureFormatUtility.IsFloatColorFormat(colorTexture.Format),
            hdr: TextureFormatUtility.IsFloatColorFormat(colorTexture.Format),
            hasReadableDepth: depthStencilTexture is not null,
            hasStencil: depthStencilTexture?.Format == TextureFormat.Depth24Stencil8);
    }
}

/// <summary>
/// Common parameters of rendered frame.
/// </summary>
public sealed class CommonFrameInfo
{
    public uint FrameNumber { get; set; }

    public float TimeStep { get; set; }

    public Vector2i ViewportSize { get; set; }

    public IntRect ViewportRect { get; set; }

    public Viewport? Viewport { get; set; }

    public Scene? Scene { get; set; }

    public Node? ViewReferenceNode { get; set; }

    public Camera? Camera { get; set; }

    public Octree? Octree { get; set; }

    public RenderOutputTargetInfo? RenderTarget { get; set; }

    public Camera?[] Cameras { get; } = new Camera?[2];
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

    event Action<RenderPipelineStats>? CollectStatistics;
}

/// <summary>
/// Base interface of render pipeline viewport instance.
/// </summary>
public abstract class RenderPipelineView : IRenderPipelineInterface
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
        _frameParameters.Clear();
        foreach (var (name, value) in _renderPipeline.GetFrameShaderParameters())
            _frameParameters.Add(new ShaderParameterDesc(new StringHash(name), Variant.FromObject(value)));
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
    public abstract CommonFrameInfo GetFrameInfo();

    /// <summary>
    /// Return render pipeline statistics for profiling.
    /// </summary>
    public abstract RenderPipelineStats GetStats();

    /// <summary>Draw debug geometries, if applicable.</summary>
    public abstract void DrawDebugGeometries(bool depthTest);

    /// <summary>Draw debug lights, if applicable.</summary>
    public abstract void DrawDebugLights(bool depthTest);

    public event Action<CommonFrameInfo>? UpdateBegin;

    public event Action<CommonFrameInfo>? UpdateEnd;

    public event Action<CommonFrameInfo>? RenderBegin;

    public event Action<CommonFrameInfo>? RenderEnd;

    public event Action? PipelineStatesInvalidated;

    public event Action<RenderPipelineStats>? CollectStatistics;

    protected void RaiseUpdateBegin(CommonFrameInfo frameInfo) => UpdateBegin?.Invoke(frameInfo);

    protected void RaiseUpdateEnd(CommonFrameInfo frameInfo) => UpdateEnd?.Invoke(frameInfo);

    protected void RaiseRenderBegin(CommonFrameInfo frameInfo) => RenderBegin?.Invoke(frameInfo);

    protected void RaiseRenderEnd(CommonFrameInfo frameInfo) => RenderEnd?.Invoke(frameInfo);

    protected void RaisePipelineStatesInvalidated() => PipelineStatesInvalidated?.Invoke();

    protected void RaiseCollectStatistics(RenderPipelineStats stats) => CollectStatistics?.Invoke(stats);
}
