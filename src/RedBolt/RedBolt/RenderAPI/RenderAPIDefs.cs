namespace RedBolt.RenderAPI;

using System.Collections.Immutable;
using SystemMath = global::System.Math;
using System.Runtime.CompilerServices;

using RedBolt.Bindings.Diligent;
using RedBolt.Container;
using RedBolt.Graphics;
using CullMode = global::RedBolt.Graphics.CullMode;
using FillMode = global::RedBolt.Graphics.FillMode;
using RedBolt.Input;
using RedBolt.Math;

public static class RenderApiLimits
{
    /// <summary>Maximum number of bound render targets supported by the engine. Hardware limit could be lower.</summary>
    public const int MaxRenderTargets = 8;

    /// <summary>Maximum number of bound vertex buffers supported by the engine.</summary>
    public const int MaxVertexStreams = 4;

    /// <summary>
    /// Some vertex elements in layout may be unused and the hard GPU limit is only applied to the used ones.
    /// </summary>
    public const int MaxNumVertexElements = 32;

    /// <summary>
    /// Max number of immutable samplers on CPU side. Can be extended freely if needed.
    /// </summary>
    public const int MaxNumImmutableSamplers = 16;

    public const int MaxShaderParameterGroups = 7;

    public const int MaxTextureCoordinates = 3;
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
public struct WindowSettings
{
    public WindowSettings()
    {
        Orientations = ["LandscapeLeft", "LandscapeRight"];
    }

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
    public ImmutableArray<string> Orientations { get; set; }
}

public struct VulkanDescriptorPoolSize
{
    public int MaxDescriptorSets { get; set; }
    public int NumSeparateSamplerDescriptors { get; set; }
    public int NumCombinedSamplerDescriptors { get; set; }
    public int NumSampledImageDescriptors { get; set; }
    public int NumStorageImageDescriptors { get; set; }
    public int NumUniformBufferDescriptors { get; set; }
    public int NumStorageBufferDescriptors { get; set; }
    public int NumUniformTexelBufferDescriptors { get; set; }
    public int NumStorageTexelBufferDescriptors { get; set; }
    public int NumInputAttachmentDescriptors { get; set; }
    public int NumAccelStructDescriptors { get; set; }
}

/// <summary>Extra tweaks for Vulkan backend.</summary>
public struct RenderDeviceSettingsVulkan
{
    [InlineArray((int)QueryType.QueryTypeNumTypes)]
    public struct QueryPoolSizeBuffer
    {
        private int? _element0;
    }

    public RenderDeviceSettingsVulkan()
    {
        InstanceExtensions = [];
        DeviceExtensions = [];
    }

    public ImmutableArray<string> InstanceExtensions { get; set; }

    public ImmutableArray<string> DeviceExtensions { get; set; }

    public VulkanDescriptorPoolSize? MainDescriptorPoolSize { get; set; }

    public VulkanDescriptorPoolSize? DynamicDescriptorPoolSize { get; set; }

    public int? DeviceLocalMemoryPageSize { get; set; }

    public int? HostVisibleMemoryPageSize { get; set; }

    public int? DeviceLocalMemoryReserveSize { get; set; }

    public int? HostVisibleMemoryReserveSize { get; set; }

    public int? UploadHeapPageSize { get; set; }

    public int? DynamicHeapSize { get; set; }

    public int? DynamicHeapPageSize { get; set; }

    public QueryPoolSizeBuffer QueryPoolSizes;
}

/// <summary>Extra tweaks for D3D12 backend.</summary>
public struct RenderDeviceSettingsD3D12
{
    [InlineArray(4)]
    public struct CpuDescriptorHeapAllocationSizeBuffer
    {
        private int? _element0;
    }

    [InlineArray(2)]
    public struct DescriptorHeapSizeBuffer
    {
        private int? _element0;
    }

    [InlineArray((int)QueryType.QueryTypeNumTypes)]
    public struct QueryPoolSizeBuffer
    {
        private int? _element0;
    }

    public CpuDescriptorHeapAllocationSizeBuffer CpuDescriptorHeapAllocationSize;

    public DescriptorHeapSizeBuffer GpuDescriptorHeapSize;

    public DescriptorHeapSizeBuffer GpuDescriptorHeapDynamicSize;

    public DescriptorHeapSizeBuffer DynamicDescriptorAllocationChunkSize;

    public int? DynamicHeapPageSize { get; set; }

    public int? NumDynamicHeapPagesToReserve { get; set; }

    public QueryPoolSizeBuffer QueryPoolSizes;
}

/// <summary>Immutable settings of the render device.</summary>
public struct RenderDeviceSettings
{
    public RenderDeviceSettings()
    {
        Vulkan = new();
        D3D12 = new();
    }

    /// <summary>Render backend to use.</summary>
    public RenderBackend Backend { get; set; }

    /// <summary>Pointer to external window native handle.</summary>
    public nint ExternalWindowHandle { get; set; }

    /// <summary>Whether to enable debug mode on GPU if possible.</summary>
    public bool GpuDebug { get; set; }

    /// <summary>Adapter ID.</summary>
    public int? AdapterId { get; set; }

    /// <summary>Extra tweaks for Vulkan backend.</summary>
    public RenderDeviceSettingsVulkan Vulkan;

    /// <summary>Extra tweaks for D3D12 backend.</summary>
    public RenderDeviceSettingsD3D12 D3D12;
}

/// <summary>Capabilities of the render device.</summary>
public struct RenderDeviceCaps
{
    public bool ComputeShaders { get; set; }

    public bool DrawBaseVertex { get; set; }

    public bool DrawBaseInstance { get; set; }

    public bool ClipDistance { get; set; }

    public bool ReadOnlyDepth { get; set; }

    public bool SrgbOutput { get; set; }

    public bool HdrOutput { get; set; }

    public int ConstantBufferOffsetAlignment { get; set; }

    public int MaxTextureSize { get; set; }

    public int MaxRenderTargetSize { get; set; }
}

/// <summary>Statistics of the render device and/or context.</summary>
public struct RenderDeviceStats
{
    /// <summary>Number of primitives drawn (triangles, lines, patches, etc.)</summary>
    public int NumPrimitives { get; set; }

    /// <summary>Number of draw operations.</summary>
    public int NumDraws { get; set; }

    /// <summary>Number of compute dispatches.</summary>
    public int NumDispatches { get; set; }
}

public struct RenderPoolSettings
{
    public RenderPoolSettings()
    {
        TextureCacheMinFrames = 1;
        TextureCacheMaxFrames = 1000;
        TextureCacheMinLoadFactor = 0.1f;
        LogPeriod = 1000;
    }

    public int TextureCacheMinFrames { get; set; } = 1;

    public int TextureCacheMaxFrames { get; set; } = 1000;

    public float TextureCacheMinLoadFactor { get; set; } = 0.1f;

    public int LogPeriod { get; set; } = 1000;
}

/// <summary>Parameters of the RawBuffer.</summary>
public readonly record struct RawBufferParams(BufferType Type, int Size, int Stride, BufferFlag Flags);

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
public readonly record struct RawTextureParams(
    TextureType Type,
    TextureFormat Format,
    TextureFlag Flags,
    Vector3i Size,
    int ArraySize = 1,
    int NumLevels = 0,
    int MultiSample = 1,
    int NumLevelsRtv = 0);

/// <param name="FirstSlice">The first array slice or cube face to be viewed.</param>
/// <param name="FirstLevel">The first mip level to be viewed.</param>
/// <param name="NumSlices">The number of array slices or cube faces to be viewed. 0 to deduce automatically.</param>
/// <param name="NumLevels">The number of mip levels to be viewed. 0 to deduce automatically.</param>
public readonly record struct RawTextureUAVKey(
    bool CanWrite = true,
    bool CanRead = true,
    int FirstSlice = 0,
    int FirstLevel = 0,
    int NumSlices = 0,
    int NumLevels = 0)
{
    public RawTextureUAVKey()
        : this(true, true, 0, 0, 0, 0)
    {
    }

    public RawTextureUAVKey ReadOnly() => this with { CanWrite = false };

    public RawTextureUAVKey WriteOnly() => this with { CanRead = false };

    public RawTextureUAVKey FromLevel(int level, int numLevels = 0) => this with { FirstLevel = level, NumLevels = numLevels };

    public RawTextureUAVKey FromSlice(int slice, int numSlices = 0) => this with { FirstSlice = slice, NumSlices = numSlices };
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
    Texture2DArray,

    Count
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

/// <summary>
/// Describes invariant texture format attributes. These attributes are intrinsic to the texture format itself and do
/// not depend on the format support.
/// </summary>
public readonly record struct TextureFormatInfo(
    string Name,
    TextureFormat Format,
    byte ComponentSize,
    byte NumComponents,
    global::RedBolt.Bindings.Diligent.ComponentType ComponentType,
    bool IsTypeless,
    byte BlockWidth,
    byte BlockHeight)
{
    /// <summary>
    /// For non-compressed formats, returns the texel size. For block-compressed formats, returns the block size.
    /// </summary>
    public int GetElementSize() => ComponentSize *
        (ComponentType != global::RedBolt.Bindings.Diligent.ComponentType.ComponentTypeCompressed ? NumComponents : 1);
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
    UByte4Norm,
    MaxVertexElementTypes
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
public readonly record struct VertexShaderAttribute(VertexElementSemantic Semantic, int SemanticIndex, int InputIndex);

/// <summary>Description of immutable texture sampler bound to the pipeline.</summary>
public struct SamplerStateDesc : IEquatable<SamplerStateDesc>
{
    public TextureFilterMode FilterMode;
    public byte Anisotropy;
    public bool ShadowCompare;
    private TextureAddressMode _addressModeU;
    private TextureAddressMode _addressModeV;
    private TextureAddressMode _addressModeW;
    public float MinLod;
    public float MaxLod;

    public SamplerStateDesc()
    {
        FilterMode = TextureFilterMode.Default;
        Anisotropy = 0;
        ShadowCompare = false;
        _addressModeU = TextureAddressMode.Wrap;
        _addressModeV = TextureAddressMode.Wrap;
        _addressModeW = TextureAddressMode.Wrap;
        MinLod = 0.0f;
        MaxLod = MathDefs.LargeValue;
    }

    public TextureAddressMode this[TextureCoordinate coordinate]
    {
        readonly get => coordinate switch
        {
            TextureCoordinate.U => _addressModeU,
            TextureCoordinate.V => _addressModeV,
            TextureCoordinate.W => _addressModeW,
            _ => throw new ArgumentOutOfRangeException(nameof(coordinate))
        };
        set
        {
            switch (coordinate)
            {
                case TextureCoordinate.U:
                    _addressModeU = value;
                    break;
                case TextureCoordinate.V:
                    _addressModeV = value;
                    break;
                case TextureCoordinate.W:
                    _addressModeW = value;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(coordinate));
            }
        }
    }

    public static SamplerStateDesc Default(TextureAddressMode addressMode = TextureAddressMode.Clamp) => Create(TextureFilterMode.Default, addressMode);

    public static SamplerStateDesc Nearest(TextureAddressMode addressMode = TextureAddressMode.Clamp) => Create(TextureFilterMode.Nearest, addressMode);

    public static SamplerStateDesc Bilinear(TextureAddressMode addressMode = TextureAddressMode.Clamp) => Create(TextureFilterMode.Bilinear, addressMode);

    public static SamplerStateDesc Trilinear(TextureAddressMode addressMode = TextureAddressMode.Clamp) => Create(TextureFilterMode.Trilinear, addressMode);

    public readonly bool Equals(SamplerStateDesc other)
    {
        return FilterMode == other.FilterMode
            && Anisotropy == other.Anisotropy
            && ShadowCompare == other.ShadowCompare
            && _addressModeU == other._addressModeU
            && _addressModeV == other._addressModeV
            && _addressModeW == other._addressModeW
            && MinLod.Equals(other.MinLod)
            && MaxLod.Equals(other.MaxLod);
    }

    public override readonly bool Equals(object? obj) => obj is SamplerStateDesc other && Equals(other);

    public override readonly int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(FilterMode);
        hash.Add(Anisotropy);
        hash.Add(ShadowCompare);
        hash.Add(_addressModeU);
        hash.Add(_addressModeV);
        hash.Add(_addressModeW);
        hash.Add(MinLod);
        hash.Add(MaxLod);
        return hash.ToHashCode();
    }

    public readonly uint ToHash()
    {
        var hash = 0u;
        CombineHash(ref hash, (uint)FilterMode);
        CombineHash(ref hash, Anisotropy);
        CombineHash(ref hash, ShadowCompare ? 1u : 0u);
        CombineHash(ref hash, (uint)_addressModeU);
        CombineHash(ref hash, (uint)_addressModeV);
        CombineHash(ref hash, (uint)_addressModeW);
        CombineHash(ref hash, unchecked((uint)BitConverter.SingleToInt32Bits(MinLod)));
        CombineHash(ref hash, unchecked((uint)BitConverter.SingleToInt32Bits(MaxLod)));
        return hash;
    }

    private static void CombineHash(ref uint result, uint hash)
    {
        result ^= hash + 0x9e3779b9u + (result << 6) + (result >> 2);
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
    int BufferIndex,
    int BufferStride,
    int ElementOffset,
    int InstanceStepRate,
    VertexElementType ElementType,
    VertexElementSemantic ElementSemantic,
    byte ElementSemanticIndex)
{
    public uint ToHash() => unchecked((uint)GetHashCode());
}

/// <summary>Description of input layout of graphics pipeline state.</summary>
public struct InputLayoutDesc : IEquatable<InputLayoutDesc>
{
    [InlineArray(RenderApiLimits.MaxNumVertexElements)]
    public struct ElementBuffer
    {
        private InputLayoutElementDesc _element0;
    }

    public int Size { get; set; }

    public ElementBuffer Elements;

    public readonly bool Equals(InputLayoutDesc other)
    {
        if (Size != other.Size)
            return false;

        for (var i = 0; i < Size; ++i)
        {
            if (Elements[i] != other.Elements[i])
                return false;
        }

        return true;
    }

    public override readonly bool Equals(object? obj) => obj is InputLayoutDesc other && Equals(other);

    public override readonly int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Size);
        for (var i = 0; i < Size; ++i)
            hash.Add(Elements[i]);
        return hash.ToHashCode();
    }

    public readonly uint ToHash() => unchecked((uint)GetHashCode());
}

/// <summary>Description of immutable texture samplers used by the pipeline.</summary>
public struct ImmutableSamplersDesc : IEquatable<ImmutableSamplersDesc>
{
    [InlineArray(RenderApiLimits.MaxNumImmutableSamplers)]
    public struct NameBuffer
    {
        private StringHash _element0;
    }

    [InlineArray(RenderApiLimits.MaxNumImmutableSamplers)]
    public struct DescBuffer
    {
        private SamplerStateDesc _element0;
    }

    public int Size { get; set; }

    public NameBuffer Names;

    public DescBuffer Descriptions;

    /// <summary>Clear the collection.</summary>
    public void Clear()
    {
        Size = 0;
    }

    /// <summary>Add sampler to collection.</summary>
    public void Add(StringHash name, SamplerStateDesc samplerDesc)
    {
        if (Size >= RenderApiLimits.MaxNumImmutableSamplers)
        {
            System.Diagnostics.Debug.Assert(false,
                "Too many immutable samplers, increase MaxNumImmutableSamplers");
            return;
        }

        Names[Size] = name;
        Descriptions[Size] = samplerDesc;
        ++Size;
    }

    public readonly bool Equals(ImmutableSamplersDesc other)
    {
        if (Size != other.Size)
            return false;

        for (var i = 0; i < Size; ++i)
        {
            if (Names[i] != other.Names[i] || !Descriptions[i].Equals(other.Descriptions[i]))
                return false;
        }

        return true;
    }

    public override readonly bool Equals(object? obj) => obj is ImmutableSamplersDesc other && Equals(other);

    public override readonly int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Size);
        for (var i = 0; i < Size; ++i)
        {
            hash.Add(Names[i]);
            hash.Add(Descriptions[i]);
        }

        return hash.ToHashCode();
    }

    public readonly uint ToHash() => unchecked((uint)GetHashCode());
}

/// <summary>Description of pipeline state output (depth-stencil and render targets).</summary>
public struct PipelineStateOutputDesc : IEquatable<PipelineStateOutputDesc>
{
    [InlineArray(RenderApiLimits.MaxRenderTargets)]
    public struct RenderTargetFormatBuffer
    {
        private TextureFormat _element0;
    }

    public PipelineStateOutputDesc()
    {
        DepthStencilFormat = TextureFormat.TexFormatUnknown;
        MultiSample = 1;
    }

    public TextureFormat DepthStencilFormat { get; set; }

    public int NumRenderTargets { get; set; }

    public RenderTargetFormatBuffer RenderTargetFormats;

    public int MultiSample { get; set; }

    public readonly uint ToHash()
    {
        var hash = new HashCode();
        hash.Add(DepthStencilFormat);
        hash.Add(NumRenderTargets);
        hash.Add(MultiSample);
        for (var i = 0; i < NumRenderTargets; ++i)
            hash.Add(RenderTargetFormats[i]);
        return unchecked((uint)hash.ToHashCode());
    }

    public readonly bool Equals(PipelineStateOutputDesc other)
    {
        if (DepthStencilFormat != other.DepthStencilFormat || NumRenderTargets != other.NumRenderTargets || MultiSample != other.MultiSample)
            return false;

        for (var i = 0; i < NumRenderTargets; ++i)
        {
            if (RenderTargetFormats[i] != other.RenderTargetFormats[i])
                return false;
        }

        return true;
    }

    public override readonly bool Equals(object? obj) => obj is PipelineStateOutputDesc other && Equals(other);

    public override readonly int GetHashCode() => unchecked((int)ToHash());
}

/// <summary>
/// Description of graphics pipeline state.
/// </summary>
public struct GraphicsPipelineStateDesc : IEquatable<GraphicsPipelineStateDesc>
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
    public InputLayoutDesc InputLayout;

    /// <summary>
    /// Primitive topology.
    /// </summary>
    public PrimitiveType PrimitiveType { get; set; }

    /// <summary>
    /// Render Target(s) and Depth Stencil formats.
    /// </summary>
    public PipelineStateOutputDesc Output = new();

    /// <summary>
    /// Immutable Samplers.
    /// </summary>
    public ImmutableSamplersDesc Samplers;

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
        DebugName = string.Empty;
        Output = new();
    }

    public bool IsInitialized() => VertexShader is not null && PixelShader is not null;

    public readonly uint ToHash()
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
        return SystemMath.Max(unchecked((uint)hash.ToHashCode()), 1u);
    }

    public readonly bool Equals(GraphicsPipelineStateDesc other)
    {
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

    public override readonly bool Equals(object? obj) => obj is GraphicsPipelineStateDesc other && Equals(other);

    public override readonly int GetHashCode() => unchecked((int)ToHash());

    internal static void AddRawShaderHash(ref HashCode hash, RawShader? shader)
    {
        hash.Add(shader is null ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(shader));
    }

    internal static bool RawShaderEquals(RawShader? lhs, RawShader? rhs)
    {
        return ReferenceEquals(lhs, rhs);
    }
}

/// <summary>
/// Description of compute pipeline state.
/// </summary>
public struct ComputePipelineStateDesc : IEquatable<ComputePipelineStateDesc>
{
    public string DebugName { get; set; } = string.Empty;

    /// <summary>
    /// Immutable Samplers.
    /// </summary>
    public ImmutableSamplersDesc Samplers;

    /// <summary>
    /// Compute shader.
    /// </summary>
    public RawShader? ComputeShader { get; set; }

    public ComputePipelineStateDesc()
    {
        DebugName = string.Empty;
    }

    public bool IsInitialized() => ComputeShader is not null;

    public readonly uint ToHash()
    {
        var hash = new HashCode();
        hash.Add(Samplers);
        GraphicsPipelineStateDesc.AddRawShaderHash(ref hash, ComputeShader);
        return SystemMath.Max(unchecked((uint)hash.ToHashCode()), 1u);
    }

    public readonly bool Equals(ComputePipelineStateDesc other)
    {
        return Samplers.Equals(other.Samplers)
            && GraphicsPipelineStateDesc.RawShaderEquals(ComputeShader, other.ComputeShader);
    }

    public override readonly bool Equals(object? obj) => obj is ComputePipelineStateDesc other && Equals(other);

    public override readonly int GetHashCode() => unchecked((int)ToHash());
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
        _graphicsDesc = desc;
        _hash = desc.ToHash();
    }

    public PipelineStateDesc(ComputePipelineStateDesc desc)
    {
        _computeDesc = desc;
        _hash = desc.ToHash();
    }

    public uint ToHash() => _hash;

    public PipelineStateType GetPipelineType() => _computeDesc is not null ? PipelineStateType.Compute : PipelineStateType.Graphics;

    public string GetDebugName() => _graphicsDesc?.DebugName ?? _computeDesc?.DebugName ?? string.Empty;

    public GraphicsPipelineStateDesc? AsGraphics() => _graphicsDesc;

    public ComputePipelineStateDesc? AsCompute() => _computeDesc;

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
