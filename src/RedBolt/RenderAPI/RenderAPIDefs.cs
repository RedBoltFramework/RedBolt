using RedBolt.Bindings.Diligent;

namespace RedBolt;

public static class RenderApiLimits
{
    /// <summary>Maximum number of bound render targets supported by the engine. Hardware limit could be lower.</summary>
    public const uint MaxRenderTargets = 8;

    /// <summary>Maximum number of bound vertex buffers supported by the engine.</summary>
    public const uint MaxVertexStreams = 4;

    /// <summary>
    /// Some vertex elements in layout may be unused and the hard GPU limit is only applied to the used ones.
    /// </summary>
    public const uint MaxNumVertexElements = 32;

    /// <summary>
    /// Max number of immutable samplers on CPU side. Can be extended freely if needed.
    /// </summary>
    public const uint MaxNumImmutableSamplers = 16;

    public const uint MaxShaderParameterGroups = 7;

    public const uint MaxTextureCoordinates = 3;
}

/// <summary>Index of the frame, counted by the presents of the primary swap chain.</summary>
public enum FrameIndex : long
{
    None = 0,
    First = 1
}

/// <summary>GAPI used for rendering.</summary>
public enum RenderBackend
{
    /// <summary>Direct3D 11.1 or later.</summary>
    D3D11,
    /// <summary>Direct3D 12.0 for SDK 10.0.17763.0 or later.</summary>
    D3D12,
    /// <summary>OpenGL 4.1 (for Desktop) or OpenGL ES 3.0 (for mobiles) or later.</summary>
    OpenGL,
    /// <summary>Vulkan 1.0 or later.</summary>
    Vulkan,
    Count
}

/// <summary>Window mode.</summary>
public enum WindowMode
{
    /// <summary>Windowed.</summary>
    Windowed,
    /// <summary>Borderless "full-screen" window.</summary>
    Borderless,
    /// <summary>Native full-screen.</summary>
    Fullscreen
}

/// <summary>Shader translation policy.</summary>
public enum ShaderTranslationPolicy
{
    /// <summary>
    /// Do not translate shaders, use universal GLSL shaders directly.
    /// This mode is only supported for OpenGL and OpenGL ES backends (GLSL-based backends).
    /// </summary>
    Verbatim,
    /// <summary>
    /// Preprocess and translate shader to the target language through SPIR-V without any optimization.
    /// This results in slower shader compilation, especially in Debug builds.
    /// This mode may help to work around OpenGL driver bugs if used for GLSL-based backends.
    /// </summary>
    Translate,
    /// <summary>
    /// Fully process and optimize shader via SPIR-V Tools.
    /// This results in even slower shader compilation, especially in Debug builds.
    /// This mode may improve realtime performance of the shaders, especially on mobile platforms.
    /// </summary>
    Optimize
}

/// <summary>Description of fullscreen mode (resolution and refresh rate).</summary>
public readonly record struct FullscreenMode(Vector2i Size, int RefreshRate);

/// <summary>Description of the window and GAPI. Some settings may be changed in real time.</summary>
public sealed class WindowSettings
{
    /// <summary>Type of window (windowed, borderless fullscreen, native fullscreen).</summary>
    public WindowMode Mode { get; set; }

    /// <summary>
    /// Windowed: size of the window in units. May be different from the size in pixels due to DPI scale.
    /// Fullscreen: display resolution in pixels.
    /// Borderless: ignored.
    /// Set to 0 to pick automatically.
    /// </summary>
    public Vector2i Size { get; set; }

    /// <summary>Window title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Windowed only: whether the window can be resized.</summary>
    public bool Resizable { get; set; }

    /// <summary>Fullscreen and Borderless only: index of the monitor.</summary>
    public int Monitor { get; set; }

    /// <summary>Whether to enable vertical synchronization.</summary>
    public bool VSync { get; set; }

    /// <summary>Refresh rate. 0 to pick automatically.</summary>
    public int RefreshRate { get; set; }

    /// <summary>Multi-sampling level.</summary>
    public int MultiSample { get; set; } = 1;

    /// <summary>Whether to use sRGB framebuffer.</summary>
    public bool SRgb { get; set; }

    /// <summary>
    /// Mobiles: orientation hints.
    /// Could be any combination of "LandscapeLeft", "LandscapeRight", "Portrait" and "PortraitUpsideDown".
    /// </summary>
    public IReadOnlyList<string> Orientations { get; set; } = ["LandscapeLeft", "LandscapeRight"];
}

/// <summary>Immutable settings of the render device.</summary>
public sealed class RenderDeviceSettings
{
    /// <summary>Render backend to use.</summary>
    public RenderBackend Backend { get; set; }

    /// <summary>Pointer to external window native handle.</summary>
    public nint ExternalWindowHandle { get; set; }

    /// <summary>Whether to enable debug mode on GPU if possible.</summary>
    public bool GpuDebug { get; set; }

    /// <summary>Adapter ID.</summary>
    public uint? AdapterId { get; set; }

    /// <summary>Extra tweaks for Vulkan backend.</summary>
    public RenderDeviceSettingsVulkan Vulkan { get; set; } = new();

    /// <summary>Extra tweaks for D3D12 backend.</summary>
    public RenderDeviceSettingsD3D12 D3D12 { get; set; } = new();
}

/// <summary>Extra tweaks for Vulkan backend.</summary>
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

/// <summary>Extra tweaks for D3D12 backend.</summary>
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

/// <summary>Capabilities of the render device.</summary>
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

/// <summary>Statistics of the render device and/or context.</summary>
public sealed class RenderDeviceStats
{
    /// <summary>Number of primitives drawn (triangles, lines, patches, etc.)</summary>
    public uint NumPrimitives { get; set; }

    /// <summary>Number of draw operations.</summary>
    public uint NumDraws { get; set; }

    /// <summary>Number of compute dispatches.</summary>
    public uint NumDispatches { get; set; }
}

public sealed class RenderPoolSettings
{
    public uint TextureCacheMinFrames { get; set; } = 1u;

    public uint TextureCacheMaxFrames { get; set; } = 1000u;

    public float TextureCacheMinLoadFactor { get; set; } = 0.1f;

    public uint LogPeriod { get; set; } = 1000u;
}

/// <summary>Parameters of the RawBuffer.</summary>
public readonly record struct RawBufferParams(BufferType Type, uint Size, uint Stride, BufferFlag Flags);

/// <param name="Size">Size of the topmost mip level of the single 2D texture for 2D and cube textures and arrays.
/// Size of the topmost mip level for 3D textures.</param>
/// <param name="ArraySize">Array size for array types. Ignored for single textures.</param>
/// <param name="NumLevels">Number of mip levels. 0 to deduce automatically.</param>
/// <param name="MultiSample">Number of samples per pixel. 1 to disable multi-sampling.</param>
/// <param name="NumLevelsRtv">Number of mip levels of the render target. This value is deduced automatically.
/// If the texture used multi-sampling with automatic resolve and has multiple mip levels, two textures are created:
/// multi-sampled texture with single mip level (aka NumLevelsRtv) used as RTV,
/// and resolved texture with multiple mip levels (aka NumLevels) used as SRV.
/// Otherwise, NumLevelsRtv is equal to NumLevels.</param>
/// <param name="SRGB">Whether the texture uses sRGB color space.</param>
public readonly record struct RawTextureParams(
    TextureType Type,
    TextureFormat Format,
    TextureFlag Flags,
    Vector3i Size,
    uint ArraySize = 1u,
    uint NumLevels = 0u,
    uint MultiSample = 1u,
    uint NumLevelsRtv = 0u,
    bool SRGB = false);

/// <param name="FirstSlice">The first array slice or cube face to be viewed.</param>
/// <param name="FirstLevel">The first mip level to be viewed.</param>
/// <param name="NumSlices">The number of array slices or cube faces to be viewed. 0 to deduce automatically.</param>
/// <param name="NumLevels">The number of mip levels to be viewed. 0 to deduce automatically.</param>
public readonly record struct RawTextureUAVKey(
    bool CanWrite = true,
    bool CanRead = true,
    uint FirstSlice = 0u,
    uint FirstLevel = 0u,
    uint NumSlices = 0u,
    uint NumLevels = 0u)
{
    public RawTextureUAVKey ReadOnly() => this with { CanWrite = false };

    public RawTextureUAVKey WriteOnly() => this with { CanRead = false };

    public RawTextureUAVKey FromLevel(uint level, uint numLevels = 0u) => this with { FirstLevel = level, NumLevels = numLevels };

    public RawTextureUAVKey FromSlice(uint slice, uint numSlices = 0u) => this with { FirstSlice = slice, NumSlices = numSlices };
}

/// <summary>GPU buffer types.</summary>
public enum BufferType
{
    /// <summary>Vertex buffer for per-vertex or per-instance data.</summary>
    Vertex,
    /// <summary>Index buffer.</summary>
    Index,
    /// <summary>Uniform aka constant buffer.</summary>
    Uniform
}

/// <summary>Buffer usage flags.</summary>
[Flags]
public enum BufferFlag
{
    None = 0,
    /// <summary>Buffer maintains up-to-date CPU-readable copy of the data. Some buffer types cannot be shadowed.</summary>
    Shadowed = 1 << 0,
    /// <summary>Buffer is dynamic and will be updated frequently.</summary>
    Dynamic = 1 << 1,
    /// <summary>Buffer data is discarded when frame ends.</summary>
    Discard = 1 << 2,
    /// <summary>Buffer can be accessed via unordered access view.</summary>
    BindUnorderedAccess = 1 << 3,
    /// <summary>
    /// Buffer contains instance data. This hint is used only on OpenGL ES platforms to emulate base instance.
    /// </summary>
    PerInstanceData = 1 << 4,
    /// <summary>Buffer data cannot change after creation. Data updates lead to buffer recreation.</summary>
    Immutable = 1 << 5
}

/// <summary>GPU texture types.</summary>
public enum TextureType
{
    /// <summary>Singular 2D texture.</summary>
    Texture2D,
    /// <summary>Singular cube texture.</summary>
    TextureCube,
    /// <summary>Singular 3D texture. Support is not guaranteed.</summary>
    Texture3D,
    /// <summary>Array of 2D textures. Support is not guaranteed.</summary>
    Texture2DArray
}

/// <summary>Texture usage flags.</summary>
[Flags]
public enum TextureFlag
{
    None = 0,
    /// <summary>Texture can be used as render target.</summary>
    BindRenderTarget = 1 << 1,
    /// <summary>Texture can be used as depth-stencil target.</summary>
    BindDepthStencil = 1 << 2,
    /// <summary>Texture can be used via unordered access view.</summary>
    BindUnorderedAccess = 1 << 3,
    /// <summary>
    /// Whether NOT to resolve multisampled texture after rendering.
    /// If set, multisampled texture is used as is. Keep in mind that you cannot easily sample such texture in shader.
    /// By default, shader resource view will point to the resolved texture.
    /// Automatically resolved textures cannot be accessed via unordered access view.
    /// </summary>
    NoMultiSampledAutoResolve = 1 << 4
}

/// <summary>Shader types.</summary>
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

/// <summary>Texture formats emulated by the engine when the backend does not expose them.</summary>
public static class EmulatedTextureFormat
{
    private const int NativeTextureFormatCount = (int)global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatNumFormats;
    public static readonly TextureFormat PvrtcRgb2Bpp = (TextureFormat)(NativeTextureFormatCount + 1);
    public static readonly TextureFormat PvrtcRgba2Bpp = (TextureFormat)(NativeTextureFormatCount + 2);
    public static readonly TextureFormat PvrtcRgb4Bpp = (TextureFormat)(NativeTextureFormatCount + 3);
    public static readonly TextureFormat PvrtcRgba4Bpp = (TextureFormat)(NativeTextureFormatCount + 4);
}

/// <summary>Vertex declaration element semantics.</summary>
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

/// <summary>Arbitrary vertex declaration element datatypes.</summary>
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

/// <summary>Texture filtering mode.</summary>
public enum TextureFilterMode : byte
{
    Nearest,
    Bilinear,
    Trilinear,
    Anisotropic,
    NearestAnisotropic,
    Default,
    Max
}

/// <summary>Texture addressing mode.</summary>
public enum TextureAddressMode
{
    Wrap,
    Mirror,
    Clamp,
    Max
}

/// <summary>Texture coordinates.</summary>
public enum TextureCoordinate
{
    U,
    V,
    W
}

/// <summary>Description of the single input required by the vertex shader.</summary>
public readonly record struct VertexShaderAttribute(VertexElementSemantic Semantic, uint SemanticIndex, uint InputIndex);

/// <summary>Description of immutable texture sampler bound to the pipeline.</summary>
public sealed class SamplerStateDesc : IEquatable<SamplerStateDesc>
{
    private readonly TextureAddressMode[] _addressModes = [TextureAddressMode.Wrap, TextureAddressMode.Wrap, TextureAddressMode.Wrap];

    public TextureFilterMode FilterMode { get; set; } = TextureFilterMode.Default;

    public byte Anisotropy { get; set; }

    public bool ShadowCompare { get; set; }

    public float MinLod { get; set; }

    public float MaxLod { get; set; } = float.MaxValue;

    public TextureAddressMode this[TextureCoordinate coordinate]
    {
        get => _addressModes[(int)coordinate];
        set => _addressModes[(int)coordinate] = value;
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
            && ManagedSequence.Equal(_addressModes, other._addressModes)
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
        foreach (var addressMode in _addressModes)
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

/// <summary>
/// Internal event sent to DeviceObject by RenderDevice.
/// </summary>
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
    /// <summary>
    /// Device object should be created only on CPU side, like in "headless" engine mode.
    /// Such object cannot be used during rendering. Headless objects could be constructed from any thread.
    /// </summary>
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

/// <summary>Stencil operation.</summary>
public enum StencilOp
{
    Keep = 0,
    Zero,
    Ref,
    Incr,
    Decr
}

/// <summary>Hard-coded uniform buffer slots.</summary>
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

/// <summary>Pipeline state type.</summary>
public enum PipelineStateType
{
    Graphics,
    Compute
}

/// <summary>Description of input layout element.</summary>
public readonly record struct InputLayoutElementDesc(
    uint BufferIndex,
    uint BufferStride,
    uint ElementOffset,
    uint InstanceStepRate,
    VertexElementType ElementType,
    VertexElementSemantic ElementSemantic,
    byte ElementSemanticIndex);

/// <summary>Description of input layout of graphics pipeline state.</summary>
public sealed class InputLayoutDesc : IEquatable<InputLayoutDesc>
{
    private readonly InputLayoutElementDesc[] _elements = new InputLayoutElementDesc[RenderApiLimits.MaxNumVertexElements];

    public InputLayoutDesc()
    {
    }

    public InputLayoutDesc(InputLayoutDesc? other)
    {
        if (other is not null)
            SetElements(other.Elements.ToArray());
    }

    public uint Size { get; private set; }

    public ReadOnlySpan<InputLayoutElementDesc> Elements => _elements.AsSpan(0, checked((int)Size));

    public void Clear()
    {
        Array.Clear(_elements, 0, _elements.Length);
        Size = 0;
    }

    public void SetElements(ReadOnlySpan<InputLayoutElementDesc> value)
    {
        if (value.Length > RenderApiLimits.MaxNumVertexElements)
            throw new ArgumentOutOfRangeException(nameof(value));

        Array.Clear(_elements, 0, _elements.Length);
        value.CopyTo(_elements);
        Size = (uint)value.Length;
    }

    public void SetElements(params InputLayoutElementDesc[] value)
    {
        ArgumentNullException.ThrowIfNull(value);
        SetElements(value.AsSpan());
    }

    public bool Equals(InputLayoutDesc? other)
    {
        if (other is null || Size != other.Size)
            return false;

        for (var i = 0; i < Size; ++i)
        {
            if (_elements[i] != other._elements[i])
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
            hash.Add(_elements[i]);
        return hash.ToHashCode();
    }
}

public readonly record struct NamedSamplerStateDesc(StringHash Name, SamplerStateDesc Desc);

/// <summary>Description of immutable texture samplers used by the pipeline.</summary>
public sealed class ImmutableSamplersDesc : IEquatable<ImmutableSamplersDesc>
{
    private readonly StringHash[] _names = new StringHash[RenderApiLimits.MaxNumImmutableSamplers];
    private readonly SamplerStateDesc[] _desc = new SamplerStateDesc[RenderApiLimits.MaxNumImmutableSamplers];

    public ImmutableSamplersDesc()
    {
    }

    public ImmutableSamplersDesc(ImmutableSamplersDesc? other)
    {
        if (other is null)
            return;

        for (var i = 0; i < other.Size; ++i)
            Add(other._names[i], other._desc[i]);
    }

    public uint Size { get; private set; }

    public ReadOnlySpan<StringHash> Names => _names.AsSpan(0, checked((int)Size));

    public ReadOnlySpan<SamplerStateDesc> Descriptions => _desc.AsSpan(0, checked((int)Size));

    /// <summary>Clear the collection.</summary>
    public void Clear()
    {
        Array.Clear(_names, 0, _names.Length);
        Array.Clear(_desc, 0, _desc.Length);
        Size = 0;
    }

    /// <summary>Add sampler to collection.</summary>
    public void Add(StringHash name, SamplerStateDesc samplerDesc)
    {
        ArgumentNullException.ThrowIfNull(samplerDesc);
        if (Size >= RenderApiLimits.MaxNumImmutableSamplers)
            throw new InvalidOperationException("Too many immutable samplers.");

        _names[Size] = name;
        _desc[Size] = CloneSamplerStateDesc(samplerDesc);
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
            if (_names[i] != other._names[i] || !_desc[i].Equals(other._desc[i]))
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
            hash.Add(_names[i]);
            hash.Add(_desc[i]);
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

/// <summary>Description of pipeline state output (depth-stencil and render targets).</summary>
public sealed class PipelineStateOutputDesc : IEquatable<PipelineStateOutputDesc>
{
    private readonly TextureFormat[] _renderTargetFormats = new TextureFormat[RenderApiLimits.MaxRenderTargets];

    public PipelineStateOutputDesc()
    {
    }

    public PipelineStateOutputDesc(PipelineStateOutputDesc? other)
    {
        if (other is null)
            return;

        DepthStencilFormat = other.DepthStencilFormat;
        MultiSample = other.MultiSample;
        SetRenderTargetFormats(other.RenderTargetFormats);
    }

    public TextureFormat DepthStencilFormat { get; set; } = TextureFormat.Unknown;

    public uint NumRenderTargets { get; private set; }

    public uint MultiSample { get; set; } = 1u;

    public ReadOnlySpan<TextureFormat> RenderTargetFormats => _renderTargetFormats.AsSpan(0, checked((int)NumRenderTargets));

    public void ClearRenderTargetFormats()
    {
        Array.Clear(_renderTargetFormats, 0, _renderTargetFormats.Length);
        NumRenderTargets = 0u;
    }

    public void SetRenderTargetFormat(TextureFormat format)
    {
        Array.Clear(_renderTargetFormats, 0, _renderTargetFormats.Length);
        _renderTargetFormats[0] = format;
        NumRenderTargets = 1u;
    }

    public void SetRenderTargetFormats(ReadOnlySpan<TextureFormat> formats)
    {
        if (formats.Length > RenderApiLimits.MaxRenderTargets)
            throw new ArgumentOutOfRangeException(nameof(formats), $"At most {RenderApiLimits.MaxRenderTargets} render targets are supported.");

        Array.Clear(_renderTargetFormats, 0, _renderTargetFormats.Length);
        formats.CopyTo(_renderTargetFormats);
        NumRenderTargets = (uint)formats.Length;
    }

    public void SetRenderTargetFormats(params TextureFormat[] formats)
    {
        ArgumentNullException.ThrowIfNull(formats);
        SetRenderTargetFormats(formats.AsSpan());
    }

    public uint ToHash()
    {
        var hash = new HashCode();
        hash.Add(DepthStencilFormat);
        hash.Add(NumRenderTargets);
        hash.Add(MultiSample);
        for (var i = 0; i < NumRenderTargets; ++i)
            hash.Add(_renderTargetFormats[i]);
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
            if (_renderTargetFormats[i] != other._renderTargetFormats[i])
                return false;
        }

        return true;
    }

    public override bool Equals(object? obj) => Equals(obj as PipelineStateOutputDesc);

    public override int GetHashCode() => unchecked((int)ToHash());
}

/// <summary>
/// Description of graphics pipeline state.
/// </summary>
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

    /// <summary>
    /// Input layout.
    /// </summary>
    public InputLayoutDesc InputLayout { get; } = new();

    /// <summary>
    /// Primitive topology.
    /// </summary>
    public PrimitiveType PrimitiveType { get; set; }

    /// <summary>
    /// Render Target(s) and Depth Stencil formats.
    /// </summary>
    public PipelineStateOutputDesc Output { get; } = new();

    /// <summary>
    /// Immutable Samplers.
    /// </summary>
    public ImmutableSamplersDesc Samplers { get; } = new();

    /// <summary>
    /// Whether to use depth-stencil in read-only mode.
    /// </summary>
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
        Output.SetRenderTargetFormats(other.Output.RenderTargetFormats);
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

/// <summary>
/// Description of compute pipeline state.
/// </summary>
public sealed class ComputePipelineStateDesc : IEquatable<ComputePipelineStateDesc>
{
    public string DebugName { get; set; } = string.Empty;

    /// <summary>
    /// Immutable Samplers.
    /// </summary>
    public ImmutableSamplersDesc Samplers { get; } = new();

    /// <summary>
    /// Compute shader.
    /// </summary>
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
    private readonly GraphicsPipelineStateDesc? _graphicsDesc;
    private readonly ComputePipelineStateDesc? _computeDesc;
    private readonly uint _hash;

    public PipelineStateDesc()
    {
        _graphicsDesc = new GraphicsPipelineStateDesc();
    }

    public PipelineStateDesc(GraphicsPipelineStateDesc desc)
    {
        ArgumentNullException.ThrowIfNull(desc);
        _graphicsDesc = new GraphicsPipelineStateDesc(desc);
        _hash = _graphicsDesc.ToHash();
    }

    public PipelineStateDesc(ComputePipelineStateDesc desc)
    {
        ArgumentNullException.ThrowIfNull(desc);
        _computeDesc = new ComputePipelineStateDesc(desc);
        _hash = _computeDesc.ToHash();
    }

    public uint ToHash() => _hash;

    public PipelineStateType GetPipelineType() => _computeDesc is not null ? PipelineStateType.Compute : PipelineStateType.Graphics;

    public string GetDebugName() => _graphicsDesc?.DebugName ?? _computeDesc?.DebugName ?? string.Empty;

    public GraphicsPipelineStateDesc? AsGraphics() => _graphicsDesc is not null ? new GraphicsPipelineStateDesc(_graphicsDesc) : null;

    public ComputePipelineStateDesc? AsCompute() => _computeDesc is not null ? new ComputePipelineStateDesc(_computeDesc) : null;

    public bool Equals(PipelineStateDesc? other)
    {
        if (other is null)
            return false;

        return _hash == other._hash
            && GetPipelineType() == other.GetPipelineType()
            && Equals(_graphicsDesc, other._graphicsDesc)
            && Equals(_computeDesc, other._computeDesc);
    }

    public override bool Equals(object? obj) => Equals(obj as PipelineStateDesc);

    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(_hash);
        hashCode.Add(GetPipelineType());
        hashCode.Add(_graphicsDesc);
        hashCode.Add(_computeDesc);
        return hashCode.ToHashCode();
    }
}
