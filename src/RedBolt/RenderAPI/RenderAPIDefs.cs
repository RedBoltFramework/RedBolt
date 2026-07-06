namespace RedBolt;

public static class RenderApiLimits
{
    public const uint MaxRenderTargets = 8;
    public const uint MaxVertexStreams = 4;
    public const uint MaxNumVertexElements = 32;
    public const uint MaxNumImmutableSamplers = 16;
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
    Vulkan
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
public enum ClearTarget
{
    None = 0,
    Color = 0x1,
    Depth = 0x2,
    Stencil = 0x4
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

public sealed class ImmutableSamplersDesc : IEquatable<ImmutableSamplersDesc>
{
    private readonly StringHash[] names = new StringHash[RenderApiLimits.MaxNumImmutableSamplers];
    private readonly SamplerStateDesc[] desc = new SamplerStateDesc[RenderApiLimits.MaxNumImmutableSamplers];

    public uint Size { get; private set; }

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
        desc[Size] = samplerDesc;
        ++Size;
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
}

public sealed class PipelineStateOutputDesc : IEquatable<PipelineStateOutputDesc>
{
    private readonly TextureFormat[] renderTargetFormats = new TextureFormat[RenderApiLimits.MaxRenderTargets];

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
