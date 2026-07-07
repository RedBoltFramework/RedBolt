using RedBolt.Bindings.Diligent;

namespace RedBolt;

public static class RenderApiLimits
{
    public const uint MaxRenderTargets = 8;
    public const uint MaxVertexStreams = 4;
    public const uint MaxNumVertexElements = 32;
    public const uint MaxNumImmutableSamplers = 16;
    public const uint MaxShaderParameterGroups = 7;
}

public enum FrameIndex : long
{
    None = 0,
    First = 1
}

public enum RenderBackend
{
    D3D11,
    D3D12,
    OpenGL,
    Vulkan,
    Count
}

public enum WindowMode
{
    Windowed,
    Borderless,
    Fullscreen
}

public enum ShaderTranslationPolicy
{
    Verbatim,
    Translate,
    Optimize
}

public readonly record struct FullscreenMode(IntVector2 Size, int RefreshRate);

public sealed class WindowSettings
{
    public WindowMode Mode { get; set; }

    public IntVector2 Size { get; set; }

    public string Title { get; set; } = string.Empty;

    public bool Resizable { get; set; }

    public int Monitor { get; set; }

    public bool VSync { get; set; }

    public int RefreshRate { get; set; }

    public int MultiSample { get; set; } = 1;

    public bool SRgb { get; set; }

    public IReadOnlyList<string> Orientations { get; set; } = ["LandscapeLeft", "LandscapeRight"];
}

public sealed class RenderDeviceSettings
{
    public RenderBackend Backend { get; set; }

    public nint ExternalWindowHandle { get; set; }

    public bool GpuDebug { get; set; }

    public uint? AdapterId { get; set; }

    public RenderDeviceSettingsVulkan Vulkan { get; set; } = new();

    public RenderDeviceSettingsD3D12 D3D12 { get; set; } = new();
}

public sealed class RenderDeviceSettingsVulkan
{
    public List<string> InstanceExtensions { get; } = [];

    public List<string> DeviceExtensions { get; } = [];

    public VulkanDescriptorPoolSize? MainDescriptorPoolSize { get; set; }

    public VulkanDescriptorPoolSize? DynamicDescriptorPoolSize { get; set; }

    public uint? DeviceLocalMemoryPageSize { get; set; }

    public uint? HostVisibleMemoryPageSize { get; set; }

    public uint? DeviceLocalMemoryReserveSize { get; set; }

    public uint? HostVisibleMemoryReserveSize { get; set; }

    public uint? UploadHeapPageSize { get; set; }

    public uint? DynamicHeapSize { get; set; }

    public uint? DynamicHeapPageSize { get; set; }

    public uint?[] QueryPoolSizes { get; } = new uint?[(int)QueryType.QueryTypeNumTypes];
}

public sealed class RenderDeviceSettingsD3D12
{
    public uint?[] CpuDescriptorHeapAllocationSize { get; } = new uint?[4];

    public uint?[] GpuDescriptorHeapSize { get; } = new uint?[2];

    public uint?[] GpuDescriptorHeapDynamicSize { get; } = new uint?[2];

    public uint?[] DynamicDescriptorAllocationChunkSize { get; } = new uint?[2];

    public uint? DynamicHeapPageSize { get; set; }

    public uint? NumDynamicHeapPagesToReserve { get; set; }

    public uint?[] QueryPoolSizes { get; } = new uint?[(int)QueryType.QueryTypeNumTypes];
}

public sealed class RenderDeviceCaps
{
    public bool ComputeShaders { get; set; }

    public bool DrawBaseVertex { get; set; }

    public bool DrawBaseInstance { get; set; }

    public bool ClipDistance { get; set; }

    public bool ReadOnlyDepth { get; set; }

    public bool SrgbOutput { get; set; }

    public bool HdrOutput { get; set; }

    public uint ConstantBufferOffsetAlignment { get; set; }

    public uint MaxTextureSize { get; set; }

    public uint MaxRenderTargetSize { get; set; }
}

public sealed class RenderDeviceStats
{
    public uint NumPrimitives { get; set; }

    public uint NumDraws { get; set; }

    public uint NumDispatches { get; set; }
}

public sealed class RenderPoolSettings
{
    public uint TextureCacheMinFrames { get; set; } = 1u;

    public uint TextureCacheMaxFrames { get; set; } = 1000u;

    public float TextureCacheMinLoadFactor { get; set; } = 0.1f;

    public uint LogPeriod { get; set; } = 1000u;
}

public readonly record struct RawBufferParams(BufferType Type, uint Size, uint Stride, BufferFlag Flags);

public enum BufferType
{
    Vertex,
    Index,
    Uniform
}

[Flags]
public enum BufferFlag
{
    None = 0,
    Shadowed = 1 << 0,
    Dynamic = 1 << 1,
    Discard = 1 << 2,
    BindUnorderedAccess = 1 << 3,
    PerInstanceData = 1 << 4,
    Immutable = 1 << 5
}

public enum TextureType
{
    Texture2D,
    TextureCube,
    Texture3D,
    Texture2DArray
}

[Flags]
public enum TextureFlag
{
    None = 0,
    BindRenderTarget = 1 << 1,
    BindDepthStencil = 1 << 2,
    BindUnorderedAccess = 1 << 3,
    NoMultiSampledAutoResolve = 1 << 4
}

public enum ShaderType
{
    VS = 0,
    PS,
    GS,
    HS,
    DS,
    CS,
    MaxShaderTypes
}

public enum VertexElementSemantic : byte
{
    Position = 0,
    Normal,
    Binormal,
    Tangent,
    TexCoord,
    Color,
    BlendWeights,
    BlendIndices,
    ObjectIndex
}

public enum VertexElementType : byte
{
    Int = 0,
    Float,
    Vector2,
    Vector3,
    Vector4,
    UByte4,
    UByte4Norm
}

public enum TextureFilterMode : byte
{
    Nearest,
    Bilinear,
    Trilinear,
    Anisotropic,
    NearestAnisotropic,
    Default
}

public enum TextureAddressMode
{
    Wrap,
    Mirror,
    Clamp,
    Border
}

public enum TextureCoordinate
{
    U,
    V,
    W
}

public readonly record struct VertexShaderAttribute(VertexElementSemantic Semantic, uint SemanticIndex, uint InputIndex);

public sealed class SamplerStateDesc : IEquatable<SamplerStateDesc>
{
    private readonly TextureAddressMode[] addressModes = [TextureAddressMode.Wrap, TextureAddressMode.Wrap, TextureAddressMode.Wrap];

    public TextureFilterMode FilterMode { get; set; } = TextureFilterMode.Default;

    public byte Anisotropy { get; set; }

    public bool ShadowCompare { get; set; }

    public float MinLod { get; set; }

    public float MaxLod { get; set; } = float.MaxValue;

    public TextureAddressMode this[TextureCoordinate coordinate]
    {
        get => addressModes[(int)coordinate];
        set => addressModes[(int)coordinate] = value;
    }

    public static SamplerStateDesc Default(TextureAddressMode addressMode = TextureAddressMode.Clamp) => Create(TextureFilterMode.Default, addressMode);

    public static SamplerStateDesc Nearest(TextureAddressMode addressMode = TextureAddressMode.Clamp) => Create(TextureFilterMode.Nearest, addressMode);

    public static SamplerStateDesc Bilinear(TextureAddressMode addressMode = TextureAddressMode.Clamp) => Create(TextureFilterMode.Bilinear, addressMode);

    public static SamplerStateDesc Trilinear(TextureAddressMode addressMode = TextureAddressMode.Clamp) => Create(TextureFilterMode.Trilinear, addressMode);

    public bool Equals(SamplerStateDesc? other)
    {
        if (other is null)
            return false;

        return FilterMode == other.FilterMode
            && Anisotropy == other.Anisotropy
            && ShadowCompare == other.ShadowCompare
            && addressModes.SequenceEqual(other.addressModes)
            && MinLod.Equals(other.MinLod)
            && MaxLod.Equals(other.MaxLod);
    }

    public override bool Equals(object? obj) => Equals(obj as SamplerStateDesc);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(FilterMode);
        hash.Add(Anisotropy);
        hash.Add(ShadowCompare);
        foreach (var addressMode in addressModes)
            hash.Add(addressMode);
        hash.Add(MinLod);
        hash.Add(MaxLod);
        return hash.ToHashCode();
    }

    private static SamplerStateDesc Create(TextureFilterMode filterMode, TextureAddressMode addressMode)
    {
        var desc = new SamplerStateDesc
        {
            FilterMode = filterMode
        };
        desc[TextureCoordinate.U] = addressMode;
        desc[TextureCoordinate.V] = addressMode;
        desc[TextureCoordinate.W] = addressMode;
        return desc;
    }
}

public enum DeviceObjectEvent
{
    Invalidate,
    Restore,
    Destroy
}

[Flags]
public enum DeviceObjectFlag : byte
{
    None = 0,
    Headless = 1 << 0
}

[Flags]
public enum ClearTarget
{
    None = 0,
    Color = 0x1,
    Depth = 0x2,
    Stencil = 0x4
}

public enum StencilOp
{
    Keep = 0,
    Zero,
    Ref,
    Incr,
    Decr
}

public enum ShaderParameterGroup
{
    Frame = 0,
    Camera,
    Zone,
    Light,
    Material,
    Object,
    Custom
}

public enum PipelineStateType
{
    Graphics,
    Compute
}

public readonly record struct InputLayoutElementDesc(
    uint BufferIndex,
    uint BufferStride,
    uint ElementOffset,
    uint InstanceStepRate,
    VertexElementType ElementType,
    VertexElementSemantic ElementSemantic,
    byte ElementSemanticIndex);

public sealed class InputLayoutDesc : IEquatable<InputLayoutDesc>
{
    private readonly InputLayoutElementDesc[] elements = new InputLayoutElementDesc[RenderApiLimits.MaxNumVertexElements];

    public InputLayoutDesc()
    {
    }

    public InputLayoutDesc(InputLayoutDesc? other)
    {
        if (other is not null)
            SetElements(other.Elements.ToArray());
    }

    public uint Size { get; private set; }

    public ReadOnlySpan<InputLayoutElementDesc> Elements => elements.AsSpan(0, checked((int)Size));

    public void Clear()
    {
        Array.Clear(elements, 0, elements.Length);
        Size = 0;
    }

    public void SetElements(params InputLayoutElementDesc[] value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length > RenderApiLimits.MaxNumVertexElements)
            throw new ArgumentOutOfRangeException(nameof(value));

        Array.Clear(elements, 0, elements.Length);
        Array.Copy(value, elements, value.Length);
        Size = (uint)value.Length;
    }

    public bool Equals(InputLayoutDesc? other)
    {
        if (other is null || Size != other.Size)
            return false;

        for (var i = 0; i < Size; ++i)
        {
            if (elements[i] != other.elements[i])
                return false;
        }

        return true;
    }

    public override bool Equals(object? obj) => Equals(obj as InputLayoutDesc);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Size);
        for (var i = 0; i < Size; ++i)
            hash.Add(elements[i]);
        return hash.ToHashCode();
    }
}

public readonly record struct NamedSamplerStateDesc(StringHash Name, SamplerStateDesc Desc);

public sealed class ImmutableSamplersDesc : IEquatable<ImmutableSamplersDesc>
{
    private readonly StringHash[] names = new StringHash[RenderApiLimits.MaxNumImmutableSamplers];
    private readonly SamplerStateDesc[] desc = new SamplerStateDesc[RenderApiLimits.MaxNumImmutableSamplers];

    public ImmutableSamplersDesc()
    {
    }

    public ImmutableSamplersDesc(ImmutableSamplersDesc? other)
    {
        if (other is null)
            return;

        for (var i = 0; i < other.Size; ++i)
            Add(other.names[i], other.desc[i]);
    }

    public uint Size { get; private set; }

    public ReadOnlySpan<StringHash> Names => names.AsSpan(0, checked((int)Size));

    public ReadOnlySpan<SamplerStateDesc> Descriptions => desc.AsSpan(0, checked((int)Size));

    public void Clear()
    {
        Array.Clear(names, 0, names.Length);
        Array.Clear(desc, 0, desc.Length);
        Size = 0;
    }

    public void Add(StringHash name, SamplerStateDesc samplerDesc)
    {
        ArgumentNullException.ThrowIfNull(samplerDesc);
        if (Size >= RenderApiLimits.MaxNumImmutableSamplers)
            throw new InvalidOperationException("Too many immutable samplers.");

        names[Size] = name;
        desc[Size] = CloneSamplerStateDesc(samplerDesc);
        ++Size;
    }

    public void Add(NamedSamplerStateDesc sampler)
    {
        Add(sampler.Name, sampler.Desc);
    }

    public bool Equals(ImmutableSamplersDesc? other)
    {
        if (other is null || Size != other.Size)
            return false;

        for (var i = 0; i < Size; ++i)
        {
            if (names[i] != other.names[i] || !desc[i].Equals(other.desc[i]))
                return false;
        }

        return true;
    }

    public override bool Equals(object? obj) => Equals(obj as ImmutableSamplersDesc);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Size);
        for (var i = 0; i < Size; ++i)
        {
            hash.Add(names[i]);
            hash.Add(desc[i]);
        }

        return hash.ToHashCode();
    }

    private static SamplerStateDesc CloneSamplerStateDesc(SamplerStateDesc source)
    {
        var result = new SamplerStateDesc
        {
            FilterMode = source.FilterMode,
            Anisotropy = source.Anisotropy,
            ShadowCompare = source.ShadowCompare,
            MinLod = source.MinLod,
            MaxLod = source.MaxLod
        };
        result[TextureCoordinate.U] = source[TextureCoordinate.U];
        result[TextureCoordinate.V] = source[TextureCoordinate.V];
        result[TextureCoordinate.W] = source[TextureCoordinate.W];
        return result;
    }
}

public sealed class PipelineStateOutputDesc : IEquatable<PipelineStateOutputDesc>
{
    private readonly TextureFormat[] renderTargetFormats = new TextureFormat[RenderApiLimits.MaxRenderTargets];

    public PipelineStateOutputDesc()
    {
    }

    public PipelineStateOutputDesc(PipelineStateOutputDesc? other)
    {
        if (other is null)
            return;

        DepthStencilFormat = other.DepthStencilFormat;
        MultiSample = other.MultiSample;
        SetRenderTargetFormats(other.RenderTargetFormats.ToArray());
    }

    public TextureFormat DepthStencilFormat { get; set; } = TextureFormat.Unknown;

    public uint NumRenderTargets { get; private set; }

    public uint MultiSample { get; set; } = 1u;

    public ReadOnlySpan<TextureFormat> RenderTargetFormats => renderTargetFormats.AsSpan(0, checked((int)NumRenderTargets));

    public void ClearRenderTargetFormats()
    {
        Array.Clear(renderTargetFormats, 0, renderTargetFormats.Length);
        NumRenderTargets = 0u;
    }

    public void SetRenderTargetFormats(params TextureFormat[] formats)
    {
        ArgumentNullException.ThrowIfNull(formats);

        if (formats.Length > RenderApiLimits.MaxRenderTargets)
            throw new ArgumentOutOfRangeException(nameof(formats), $"At most {RenderApiLimits.MaxRenderTargets} render targets are supported.");

        Array.Clear(renderTargetFormats, 0, renderTargetFormats.Length);
        Array.Copy(formats, renderTargetFormats, formats.Length);
        NumRenderTargets = (uint)formats.Length;
    }

    public uint ToHash()
    {
        var hash = new HashCode();
        hash.Add(DepthStencilFormat);
        hash.Add(NumRenderTargets);
        hash.Add(MultiSample);
        for (var i = 0; i < NumRenderTargets; ++i)
            hash.Add(renderTargetFormats[i]);
        return unchecked((uint)hash.ToHashCode());
    }

    public bool Equals(PipelineStateOutputDesc? other)
    {
        if (other is null)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        if (DepthStencilFormat != other.DepthStencilFormat || NumRenderTargets != other.NumRenderTargets || MultiSample != other.MultiSample)
            return false;

        for (var i = 0; i < NumRenderTargets; ++i)
        {
            if (renderTargetFormats[i] != other.renderTargetFormats[i])
                return false;
        }

        return true;
    }

    public override bool Equals(object? obj) => Equals(obj as PipelineStateOutputDesc);

    public override int GetHashCode() => unchecked((int)ToHash());
}

public sealed class GraphicsPipelineStateDesc : IEquatable<GraphicsPipelineStateDesc>
{
    public string DebugName { get; set; } = string.Empty;

    public bool ColorWriteEnabled { get; set; }

    public BlendMode BlendMode { get; set; }

    public bool AlphaToCoverageEnabled { get; set; }

    public FillMode FillMode { get; set; }

    public CullMode CullMode { get; set; }

    public float ConstantDepthBias { get; set; }

    public float SlopeScaledDepthBias { get; set; }

    public bool ScissorTestEnabled { get; set; }

    public bool LineAntiAlias { get; set; }

    public bool DepthWriteEnabled { get; set; }

    public bool StencilTestEnabled { get; set; }

    public CompareMode DepthCompareFunction { get; set; }

    public CompareMode StencilCompareFunction { get; set; }

    public StencilOp StencilOperationOnPassed { get; set; }

    public StencilOp StencilOperationOnStencilFailed { get; set; }

    public StencilOp StencilOperationOnDepthFailed { get; set; }

    public uint StencilCompareMask { get; set; }

    public uint StencilWriteMask { get; set; }

    public InputLayoutDesc InputLayout { get; } = new();

    public PrimitiveType PrimitiveType { get; set; }

    public PipelineStateOutputDesc Output { get; } = new();

    public ImmutableSamplersDesc Samplers { get; } = new();

    public bool ReadOnlyDepth { get; set; }

    public RawShader? VertexShader { get; set; }

    public RawShader? PixelShader { get; set; }

    public RawShader? DomainShader { get; set; }

    public RawShader? HullShader { get; set; }

    public RawShader? GeometryShader { get; set; }

    public GraphicsPipelineStateDesc()
    {
    }

    public GraphicsPipelineStateDesc(GraphicsPipelineStateDesc? other)
    {
        if (other is null)
            return;

        DebugName = other.DebugName;
        ColorWriteEnabled = other.ColorWriteEnabled;
        BlendMode = other.BlendMode;
        AlphaToCoverageEnabled = other.AlphaToCoverageEnabled;
        FillMode = other.FillMode;
        CullMode = other.CullMode;
        ConstantDepthBias = other.ConstantDepthBias;
        SlopeScaledDepthBias = other.SlopeScaledDepthBias;
        ScissorTestEnabled = other.ScissorTestEnabled;
        LineAntiAlias = other.LineAntiAlias;
        DepthWriteEnabled = other.DepthWriteEnabled;
        StencilTestEnabled = other.StencilTestEnabled;
        DepthCompareFunction = other.DepthCompareFunction;
        StencilCompareFunction = other.StencilCompareFunction;
        StencilOperationOnPassed = other.StencilOperationOnPassed;
        StencilOperationOnStencilFailed = other.StencilOperationOnStencilFailed;
        StencilOperationOnDepthFailed = other.StencilOperationOnDepthFailed;
        StencilCompareMask = other.StencilCompareMask;
        StencilWriteMask = other.StencilWriteMask;
        InputLayout.SetElements(other.InputLayout.Elements.ToArray());
        PrimitiveType = other.PrimitiveType;
        Output.DepthStencilFormat = other.Output.DepthStencilFormat;
        Output.MultiSample = other.Output.MultiSample;
        Output.SetRenderTargetFormats(other.Output.RenderTargetFormats.ToArray());
        for (var samplerIndex = 0; samplerIndex < other.Samplers.Size; ++samplerIndex)
            Samplers.Add(other.Samplers.Names[samplerIndex], other.Samplers.Descriptions[samplerIndex]);
        ReadOnlyDepth = other.ReadOnlyDepth;
        VertexShader = other.VertexShader;
        PixelShader = other.PixelShader;
        DomainShader = other.DomainShader;
        HullShader = other.HullShader;
        GeometryShader = other.GeometryShader;
    }

    public bool IsInitialized() => VertexShader is not null && PixelShader is not null;

    public uint ToHash()
    {
        var hash = new HashCode();
        hash.Add(ColorWriteEnabled);
        hash.Add(BlendMode);
        hash.Add(AlphaToCoverageEnabled);
        hash.Add(FillMode);
        hash.Add(CullMode);
        hash.Add(ConstantDepthBias);
        hash.Add(SlopeScaledDepthBias);
        hash.Add(ScissorTestEnabled);
        hash.Add(LineAntiAlias);
        hash.Add(DepthWriteEnabled);
        hash.Add(StencilTestEnabled);
        hash.Add(DepthCompareFunction);
        hash.Add(StencilCompareFunction);
        hash.Add(StencilOperationOnPassed);
        hash.Add(StencilOperationOnStencilFailed);
        hash.Add(StencilOperationOnDepthFailed);
        hash.Add(StencilCompareMask);
        hash.Add(StencilWriteMask);
        hash.Add(InputLayout);
        hash.Add(PrimitiveType);
        hash.Add(Output);
        hash.Add(Samplers);
        hash.Add(ReadOnlyDepth);
        AddRawShaderHash(ref hash, VertexShader);
        AddRawShaderHash(ref hash, PixelShader);
        AddRawShaderHash(ref hash, DomainShader);
        AddRawShaderHash(ref hash, HullShader);
        AddRawShaderHash(ref hash, GeometryShader);
        return Math.Max(unchecked((uint)hash.ToHashCode()), 1u);
    }

    public bool Equals(GraphicsPipelineStateDesc? other)
    {
        if (other is null)
            return false;

        return ColorWriteEnabled == other.ColorWriteEnabled
            && BlendMode == other.BlendMode
            && AlphaToCoverageEnabled == other.AlphaToCoverageEnabled
            && FillMode == other.FillMode
            && CullMode == other.CullMode
            && ConstantDepthBias.Equals(other.ConstantDepthBias)
            && SlopeScaledDepthBias.Equals(other.SlopeScaledDepthBias)
            && ScissorTestEnabled == other.ScissorTestEnabled
            && LineAntiAlias == other.LineAntiAlias
            && DepthWriteEnabled == other.DepthWriteEnabled
            && StencilTestEnabled == other.StencilTestEnabled
            && DepthCompareFunction == other.DepthCompareFunction
            && StencilCompareFunction == other.StencilCompareFunction
            && StencilOperationOnPassed == other.StencilOperationOnPassed
            && StencilOperationOnStencilFailed == other.StencilOperationOnStencilFailed
            && StencilOperationOnDepthFailed == other.StencilOperationOnDepthFailed
            && StencilCompareMask == other.StencilCompareMask
            && StencilWriteMask == other.StencilWriteMask
            && InputLayout.Equals(other.InputLayout)
            && PrimitiveType == other.PrimitiveType
            && Output.Equals(other.Output)
            && Samplers.Equals(other.Samplers)
            && ReadOnlyDepth == other.ReadOnlyDepth
            && RawShaderEquals(VertexShader, other.VertexShader)
            && RawShaderEquals(PixelShader, other.PixelShader)
            && RawShaderEquals(DomainShader, other.DomainShader)
            && RawShaderEquals(HullShader, other.HullShader)
            && RawShaderEquals(GeometryShader, other.GeometryShader);
    }

    public override bool Equals(object? obj) => Equals(obj as GraphicsPipelineStateDesc);

    public override int GetHashCode() => unchecked((int)ToHash());

    internal static void AddRawShaderHash(ref HashCode hash, RawShader? shader)
    {
        if (shader is ShaderVariation shaderVariation)
        {
            hash.Add(shaderVariation.Name, StringComparer.Ordinal);
            hash.Add(shaderVariation.ShaderType);
            hash.Add(shaderVariation.Defines, StringComparer.Ordinal);
            return;
        }

        hash.Add(shader?.GetShaderType());
        if (shader is not null)
            hash.Add(shader.GetBytecode());
    }

    internal static bool RawShaderEquals(RawShader? lhs, RawShader? rhs)
    {
        if (lhs is null || rhs is null)
            return lhs is null && rhs is null;

        if (lhs is ShaderVariation lhsVariation && rhs is ShaderVariation rhsVariation)
        {
            return lhsVariation.ShaderType == rhsVariation.ShaderType
                && string.Equals(lhsVariation.Name, rhsVariation.Name, StringComparison.Ordinal)
                && string.Equals(lhsVariation.Defines, rhsVariation.Defines, StringComparison.Ordinal);
        }

        return lhs.GetShaderType() == rhs.GetShaderType()
            && lhs.GetBytecode().Equals(rhs.GetBytecode());
    }
}

public sealed class ComputePipelineStateDesc : IEquatable<ComputePipelineStateDesc>
{
    public string DebugName { get; set; } = string.Empty;

    public ImmutableSamplersDesc Samplers { get; } = new();

    public RawShader? ComputeShader { get; set; }

    public ComputePipelineStateDesc()
    {
    }

    public ComputePipelineStateDesc(ComputePipelineStateDesc? other)
    {
        if (other is null)
            return;

        DebugName = other.DebugName;
        for (var samplerIndex = 0; samplerIndex < other.Samplers.Size; ++samplerIndex)
            Samplers.Add(other.Samplers.Names[samplerIndex], other.Samplers.Descriptions[samplerIndex]);
        ComputeShader = other.ComputeShader;
    }

    public bool IsInitialized() => ComputeShader is not null;

    public uint ToHash()
    {
        var hash = new HashCode();
        hash.Add(Samplers);
        GraphicsPipelineStateDesc.AddRawShaderHash(ref hash, ComputeShader);
        return Math.Max(unchecked((uint)hash.ToHashCode()), 1u);
    }

    public bool Equals(ComputePipelineStateDesc? other)
    {
        if (other is null)
            return false;

        return Samplers.Equals(other.Samplers)
            && GraphicsPipelineStateDesc.RawShaderEquals(ComputeShader, other.ComputeShader);
    }

    public override bool Equals(object? obj) => Equals(obj as ComputePipelineStateDesc);

    public override int GetHashCode() => unchecked((int)ToHash());
}

public sealed class PipelineStateDesc : IEquatable<PipelineStateDesc>
{
    private readonly GraphicsPipelineStateDesc? graphicsDesc;
    private readonly ComputePipelineStateDesc? computeDesc;
    private readonly uint hash;

    public PipelineStateDesc()
    {
    }

    public PipelineStateDesc(GraphicsPipelineStateDesc desc)
    {
        ArgumentNullException.ThrowIfNull(desc);
        graphicsDesc = new GraphicsPipelineStateDesc(desc);
        hash = graphicsDesc.ToHash();
    }

    public PipelineStateDesc(ComputePipelineStateDesc desc)
    {
        ArgumentNullException.ThrowIfNull(desc);
        computeDesc = new ComputePipelineStateDesc(desc);
        hash = computeDesc.ToHash();
    }

    public uint ToHash() => hash;

    public PipelineStateType GetPipelineType() => computeDesc is not null ? PipelineStateType.Compute : PipelineStateType.Graphics;

    public string GetDebugName() => graphicsDesc?.DebugName ?? computeDesc?.DebugName ?? string.Empty;

    public GraphicsPipelineStateDesc? AsGraphics() => graphicsDesc is not null ? new GraphicsPipelineStateDesc(graphicsDesc) : null;

    public ComputePipelineStateDesc? AsCompute() => computeDesc is not null ? new ComputePipelineStateDesc(computeDesc) : null;

    public bool Equals(PipelineStateDesc? other)
    {
        if (other is null)
            return false;

        return hash == other.hash
            && GetPipelineType() == other.GetPipelineType()
            && Equals(graphicsDesc, other.graphicsDesc)
            && Equals(computeDesc, other.computeDesc);
    }

    public override bool Equals(object? obj) => Equals(obj as PipelineStateDesc);

    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(hash);
        hashCode.Add(GetPipelineType());
        hashCode.Add(graphicsDesc);
        hashCode.Add(computeDesc);
        return hashCode.ToHashCode();
    }
}
