namespace RedBolt.Graphics;

using RedBolt.Container;
using RedBolt.Math;
using RedBolt.RenderAPI;

/// <summary>Vertex/index buffer lock state.</summary>
public enum LockState
{
    None = 0,
    Hardware,
    Shadow,
    Scratch
}

/// <summary>Hardcoded legacy vertex elements.</summary>
public enum LegacyVertexElement
{
    Position = 0,
    Normal,
    Color,
    TexCoord1,
    TexCoord2,
    CubeTexCoord1,
    CubeTexCoord2,
    Tangent,
    BlendWeights,
    BlendIndices,
    InstanceMatrix1,
    InstanceMatrix2,
    InstanceMatrix3,
    ObjectIndex,
    MaxLegacyVertexElements
}

/// <summary>Cubemap single image layout modes.</summary>
public enum CubeMapLayout
{
    Horizontal = 0,
    HorizontalNvidia,
    HorizontalCross,
    VerticalCross,
    Blender
}

/// <summary>Update mode for render surface viewports.</summary>
public enum RenderSurfaceUpdateMode
{
    ManualUpdate = 0,
    UpdateVisible,
    UpdateAlways
}

/// <summary>Billboard camera facing modes.</summary>
public enum FaceCameraMode
{
    None = 0,
    RotateXyz,
    RotateY,
    LookAtXyz,
    LookAtY,
    LookAtMixed,
    Direction,
    AxisAngle
}

/// <summary>Shadow type.</summary>
public enum ShadowQuality
{
    Simple16Bit = 0,
    Simple24Bit,
    Pcf16Bit,
    Pcf24Bit,
    Vsm,
    BlurVsm
}

public static class GraphicsDefs
{
    private static readonly string?[] TextureFilterModeNames =
    [
        "NEAREST",
        "BILINEAR",
        "TRILINEAR",
        "ANISOTROPIC",
        "NEAREST_ANISOTROPIC",
        "DEFAULT",
        null
    ];

    public static readonly StringHash VspAmbientStartColor = new("AmbientStartColor");
    public static readonly StringHash VspAmbientEndColor = new("AmbientEndColor");
    public static readonly StringHash VspBillboardRot = new("BillboardRot");
    public static readonly StringHash VspCameraPos = new("CameraPos");
    public static readonly StringHash VspClipPlane = new("ClipPlane");
    public static readonly StringHash VspNearClip = new("NearClip");
    public static readonly StringHash VspFarClip = new("FarClip");
    public static readonly StringHash VspDepthMode = new("DepthMode");
    public static readonly StringHash VspDeltaTime = new("DeltaTime");
    public static readonly StringHash VspElapsedTime = new("ElapsedTime");
    public static readonly StringHash VspFrustumSize = new("FrustumSize");
    public static readonly StringHash VspGBufferOffsets = new("GBufferOffsets");
    public static readonly StringHash VspLightDir = new("LightDir");
    public static readonly StringHash VspLightPos = new("LightPos");
    public static readonly StringHash VspNormalOffsetScale = new("NormalOffsetScale");
    public static readonly StringHash VspModel = new("Model");
    public static readonly StringHash VspView = new("View");
    public static readonly StringHash VspViewInv = new("ViewInv");
    public static readonly StringHash VspViewProj = new("ViewProj");
    public static readonly StringHash VspUOffset = new("UOffset");
    public static readonly StringHash VspVOffset = new("VOffset");
    public static readonly StringHash VspZone = new("Zone");
    public static readonly StringHash VspLightMatrices = new("LightMatrices");
    public static readonly StringHash VspSkinMatrices = new("SkinMatrices");
    public static readonly StringHash VspVertexLights = new("VertexLights");
    public static readonly StringHash VspLmOffset = new("LMOffset");
    public static readonly StringHash VspShar = new("SHAr");
    public static readonly StringHash VspShag = new("SHAg");
    public static readonly StringHash VspShab = new("SHAb");
    public static readonly StringHash VspShbr = new("SHBr");
    public static readonly StringHash VspShbg = new("SHBg");
    public static readonly StringHash VspShbb = new("SHBb");
    public static readonly StringHash VspShc = new("SHC");
    public static readonly StringHash VspAmbient = new("Ambient");
    public static readonly StringHash PspAmbientColor = new("AmbientColor");
    public static readonly StringHash PspCameraPos = new("CameraPosPS");
    public static readonly StringHash PspDeltaTime = new("DeltaTimePS");
    public static readonly StringHash PspDepthReconstruct = new("DepthReconstruct");
    public static readonly StringHash PspElapsedTime = new("ElapsedTimePS");
    public static readonly StringHash PspFogColor = new("FogColor");
    public static readonly StringHash PspFogParams = new("FogParams");
    public static readonly StringHash PspGBufferInvSize = new("GBufferInvSize");
    public static readonly StringHash PspLightColor = new("LightColor");
    public static readonly StringHash PspLightDir = new("LightDirPS");
    public static readonly StringHash PspLightPos = new("LightPosPS");
    public static readonly StringHash PspNormalOffsetScale = new("NormalOffsetScalePS");
    public static readonly StringHash PspMatDiffColor = new("MatDiffColor");
    public static readonly StringHash PspMatEmissiveColor = new("MatEmissiveColor");
    public static readonly StringHash PspMatEnvMapColor = new("MatEnvMapColor");
    public static readonly StringHash PspMatSpecColor = new("MatSpecColor");
    public static readonly StringHash PspNearClip = new("NearClipPS");
    public static readonly StringHash PspFarClip = new("FarClipPS");
    public static readonly StringHash PspShadowCubeAdjust = new("ShadowCubeAdjust");
    public static readonly StringHash PspShadowCubeUvBias = new("ShadowCubeUVBias");
    public static readonly StringHash PspShadowDepthFade = new("ShadowDepthFade");
    public static readonly StringHash PspShadowIntensity = new("ShadowIntensity");
    public static readonly StringHash PspShadowMapInvSize = new("ShadowMapInvSize");
    public static readonly StringHash PspShadowSplits = new("ShadowSplits");
    public static readonly StringHash PspLightMatrices = new("LightMatricesPS");
    public static readonly StringHash PspVsmShadowParams = new("VSMShadowParams");
    public static readonly StringHash PspRoughness = new("Roughness");
    public static readonly StringHash PspMetallic = new("Metallic");
    public static readonly StringHash PspLightRad = new("LightRad");
    public static readonly StringHash PspLightLength = new("LightLength");
    public static readonly StringHash PspZoneMin = new("ZoneMin");
    public static readonly StringHash PspZoneMax = new("ZoneMax");

    public static readonly Vector3f DotScale = new(1.0f / 3.0f, 1.0f / 3.0f, 1.0f / 3.0f);

    public const int MaxRenderTargets = (int)RenderApiLimits.MaxRenderTargets;
    public const int MaxVertexStreams = (int)RenderApiLimits.MaxVertexStreams;

    private static readonly VertexElement[] LegacyVertexElementsStorage =
    [
        new(VertexElementType.Vector3, VertexElementSemantic.Position),
        new(VertexElementType.Vector3, VertexElementSemantic.Normal),
        new(VertexElementType.UByte4Norm, VertexElementSemantic.Color),
        new(VertexElementType.Vector2, VertexElementSemantic.TexCoord, 0),
        new(VertexElementType.Vector2, VertexElementSemantic.TexCoord, 1),
        new(VertexElementType.Vector3, VertexElementSemantic.TexCoord, 0),
        new(VertexElementType.Vector3, VertexElementSemantic.TexCoord, 1),
        new(VertexElementType.Vector4, VertexElementSemantic.Tangent),
        new(VertexElementType.Vector4, VertexElementSemantic.BlendWeights),
        new(VertexElementType.UByte4, VertexElementSemantic.BlendIndices),
        new(VertexElementType.Vector4, VertexElementSemantic.TexCoord, 4, 1),
        new(VertexElementType.Vector4, VertexElementSemantic.TexCoord, 5, 1),
        new(VertexElementType.Vector4, VertexElementSemantic.TexCoord, 6, 1),
        new(VertexElementType.Int, VertexElementSemantic.ObjectIndex)
    ];

    /// <summary>Vertex element definitions for the legacy elements.</summary>
    public static ReadOnlySpan<VertexElement> LegacyVertexElements => LegacyVertexElementsStorage;

    private static readonly uint[] ElementTypeSizesStorage =
    [
        sizeof(int),
        sizeof(float),
        2u * sizeof(float),
        3u * sizeof(float),
        4u * sizeof(float),
        sizeof(uint),
        sizeof(uint)
    ];

    /// <summary>Sizes of vertex element types.</summary>
    public static ReadOnlySpan<uint> ElementTypeSizes => ElementTypeSizesStorage;
}
