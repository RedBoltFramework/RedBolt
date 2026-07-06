namespace RedBolt;

public readonly record struct RenderPassToggle(string Name, bool Enabled);

public enum BatchCompositorSubpass
{
    Deferred,
    Base,
    Light,
    Ignored
}

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

[Flags]
public enum RenderBufferFlag
{
    None = 0,
    Persistent = 1 << 0,
    FixedTextureSize = 1 << 1,
    BilinearFiltering = 1 << 2,
    CubeMap = 1 << 3,
    NoMultiSampledAutoResolve = 1 << 4
}

public readonly record struct RenderBufferParams(TextureFormat TextureFormat, int MultiSampleLevel = 1, RenderBufferFlag Flags = RenderBufferFlag.None);

public enum RenderPipelineColorSpace
{
    GammaLDR,
    LinearLDR,
    LinearHDR,
    Optimized
}

public enum ColorSpaceTransition
{
    None,
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

public sealed class RenderBufferManagerSettings : IEquatable<RenderBufferManagerSettings>
{
    public bool InheritMultiSampleLevel { get; set; } = true;

    public int MultiSampleLevel { get; set; } = 1;

    public RenderPipelineColorSpace ColorSpace { get; set; }

    public bool FilteredColor { get; set; }

    public bool StencilBuffer { get; set; }

    public bool ReadableDepth { get; set; }

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

public struct RenderBufferManagerFrameSettings
{
    public bool ReadableColor { get; set; }

    public bool SupportColorReadWrite { get; set; }

    public bool FilteredColor { get; set; }

    public bool ReadableDepth { get; set; }
}

[Flags]
public enum RenderOutputFlag
{
    None = 0,
    NeedColorOutputReadAndWrite = 1 << 0,
    NeedColorOutputBilinear = 1 << 1
}

public sealed class RenderPipelineStats
{
    public uint NumLights { get; set; }

    public uint NumShadowedLights { get; set; }

    public uint NumGeometries { get; set; }

    public uint NumOccluders { get; set; }
}

public readonly record struct BatchStateCreateKey;

public readonly record struct BatchStateCreateContext;

public readonly record struct UIBatchStateKey;

public readonly record struct UIBatchStateCreateContext;

public readonly record struct ShadowMapRegion(uint PageIndex, Texture2D? Texture, IntRect Rect)
{
    public bool IsEmpty => Texture is null;
}

public abstract class BatchStateCacheCallback
{
    public abstract PipelineState? CreateBatchPipelineState(
        BatchStateCreateKey key,
        BatchStateCreateContext context,
        PipelineStateOutputDesc outputDesc);

    public abstract PipelineState? CreateBatchPipelineStatePlaceholder(
        uint vertexStride,
        PipelineStateOutputDesc outputDesc);
}

public abstract class UIBatchStateCacheCallback
{
    public abstract PipelineState? CreateUIBatchPipelineState(
        UIBatchStateKey key,
        UIBatchStateCreateContext context);
}

public abstract class LightProcessorCallback
{
    public abstract bool IsLightShadowed(Light light);

    public abstract uint GetShadowMapSize(Light light, uint numActiveSplits);

    public abstract ShadowMapRegion AllocateTransientShadowMap(IntVector2 size);
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
    public uint Budget { get; set; } = 64;

    public uint NormalTimeToLive { get; set; } = 60;

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

    public Vector2 VarianceShadowMapParams { get; set; } = new(0.0000001f, 0.9f);

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

    public Vector2 VarianceShadowMapParams { get; set; } = new(0.0000001f, 0.9f);

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
        RenderBufferManager.FilteredColor = flags.HasFlag(RenderOutputFlag.NeedColorOutputBilinear);
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

public readonly record struct ShaderResourceDesc(StringHash Name, Texture? Texture);

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
        Texture? depthStencilTexture = null, int multiSample = 1)
    {
        ArgumentNullException.ThrowIfNull(colorTexture);

        return new RenderOutputTargetInfo(
            RenderTargetView.Texture(colorTexture),
            fullViewportRect,
            colorTexture.Format,
            depthStencilTexture is not null ? RenderTargetView.Texture(depthStencilTexture) : null,
            depthStencilTexture?.Format ?? TextureFormat.Unknown,
            multiSample,
            colorTexture,
            depthStencilTexture,
            bilinearFiltered: colorTexture.GetFilterMode() is TextureFilterMode.Bilinear or TextureFilterMode.Trilinear or TextureFilterMode.Anisotropic,
            linearColorSpace: colorTexture.GetLinear() || colorTexture.GetSRGB() || colorTexture.Format == TextureFormat.RGBA16F,
            hdr: colorTexture.Format == TextureFormat.RGBA16F,
            hasReadableDepth: depthStencilTexture is not null,
            hasStencil: depthStencilTexture?.Format == TextureFormat.Depth24Stencil8);
    }
}

public sealed class CommonFrameInfo
{
    public uint FrameNumber { get; set; }

    public float TimeStep { get; set; }

    public IntVector2 ViewportSize { get; set; }

    public IntRect ViewportRect { get; set; }

    public Viewport? Viewport { get; set; }

    public RenderOutputTargetInfo? RenderTarget { get; set; }

    public Camera?[] Cameras { get; } = new Camera?[2];
}

public interface IRenderPipelineInterface
{
    bool IsLinearColorSpace { get; }

    event Action<CommonFrameInfo>? UpdateBegin;

    event Action<CommonFrameInfo>? UpdateEnd;

    event Action<CommonFrameInfo>? RenderBegin;

    event Action<CommonFrameInfo>? RenderEnd;

    event Action? PipelineStatesInvalidated;

    event Action<RenderPipelineStats>? CollectStatistics;
}

public abstract class RenderPipelineView : IRenderPipelineInterface
{
    public abstract bool IsLinearColorSpace { get; }

    public abstract bool Define(Viewport viewport, Graphics graphics);

    public abstract void Update(Graphics graphics);

    public abstract void Render(Graphics graphics, MaterialQuality materialQuality, string? screenshotPath = null);

    public abstract CommonFrameInfo GetFrameInfo();

    public abstract RenderPipelineStats GetStats();

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
