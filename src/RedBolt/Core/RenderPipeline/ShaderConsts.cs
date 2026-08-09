namespace RedBolt.RenderPipeline;

using RedBolt.Container;
using RedBolt.Graphics;

/// <summary>
/// Built-in shader consts.
/// See _Uniforms.glsl for descriptions.
/// </summary>
public static class ShaderConsts
{
    public static readonly ConstString Frame_DeltaTime = new("DeltaTime");
    public static readonly ConstString Frame_ElapsedTime = new("ElapsedTime");

    public static readonly ConstString Camera_View = new("View");
    public static readonly ConstString Camera_ViewInv = new("ViewInv");
    public static readonly ConstString Camera_ViewProj = new("ViewProj");
    public static readonly ConstString Camera_ClipPlane = new("ClipPlane");
    public static readonly ConstString Camera_CameraPos = new("CameraPos");
    public static readonly ConstString Camera_NearClip = new("NearClip");
    public static readonly ConstString Camera_FarClip = new("FarClip");
    public static readonly ConstString Camera_DepthMode = new("DepthMode");
    public static readonly ConstString Camera_FrustumSize = new("FrustumSize");
    public static readonly ConstString Camera_GBufferOffsets = new("GBufferOffsets");
    public static readonly ConstString Camera_DepthReconstruct = new("DepthReconstruct");
    public static readonly ConstString Camera_GBufferInvSize = new("GBufferInvSize");
    public static readonly ConstString Camera_AmbientColor = new("AmbientColor");
    public static readonly ConstString Camera_FogParams = new("FogParams");
    public static readonly ConstString Camera_FogColor = new("FogColor");
    public static readonly ConstString Camera_NormalOffsetScale = new("NormalOffsetScale");

    public static readonly ConstString Zone_CubemapCenter0 = new("CubemapCenter0");
    public static readonly ConstString Zone_CubemapCenter1 = new("CubemapCenter1");
    public static readonly ConstString Zone_ProjectionBoxMin0 = new("ProjectionBoxMin0");
    public static readonly ConstString Zone_ProjectionBoxMin1 = new("ProjectionBoxMin1");
    public static readonly ConstString Zone_ProjectionBoxMax0 = new("ProjectionBoxMax0");
    public static readonly ConstString Zone_ProjectionBoxMax1 = new("ProjectionBoxMax1");
    public static readonly ConstString Zone_RoughnessToLODFactor0 = new("RoughnessToLODFactor0");
    public static readonly ConstString Zone_RoughnessToLODFactor1 = new("RoughnessToLODFactor1");
    public static readonly ConstString Zone_ReflectionBlendFactor = new("ReflectionBlendFactor");

    public static readonly ConstString Light_LightPos = new("LightPos");
    public static readonly ConstString Light_LightDir = new("LightDir");
    public static readonly ConstString Light_SpotAngle = new("SpotAngle");
    public static readonly ConstString Light_LightShapeMatrix = new("LightShapeMatrix");
    public static readonly ConstString Light_VertexLights = new("VertexLights");
    public static readonly ConstString Light_LightMatrices = new("LightMatrices");
    public static readonly ConstString Light_LightColor = new("LightColor");
    public static readonly ConstString Light_ShadowCubeAdjust = new("ShadowCubeAdjust");
    public static readonly ConstString Light_ShadowCubeUVBias = new("ShadowCubeUVBias");
    public static readonly ConstString Light_ShadowDepthFade = new("ShadowDepthFade");
    public static readonly ConstString Light_ShadowIntensity = new("ShadowIntensity");
    public static readonly ConstString Light_ShadowMapInvSize = new("ShadowMapInvSize");
    public static readonly ConstString Light_ShadowSplits = new("ShadowSplits");
    public static readonly ConstString Light_VSMShadowParams = new("VSMShadowParams");
    public static readonly ConstString Light_LightRad = new("LightRad");
    public static readonly ConstString Light_LightLength = new("LightLength");

    /// <summary>
    /// Serialized in Material, do not rename.
    /// </summary>
    public static readonly ConstString Material_UOffset = new("UOffset");
    public static readonly ConstString Material_VOffset = new("VOffset");
    public static readonly ConstString Material_LMOffset = new("LMOffset");
    public static readonly ConstString Material_MatDiffColor = new("MatDiffColor");
    public static readonly ConstString Material_MatEmissiveColor = new("MatEmissiveColor");
    public static readonly ConstString Material_Roughness = new("Roughness");
    public static readonly ConstString Material_MatEnvMapColor = new("MatEnvMapColor");
    public static readonly ConstString Material_Metallic = new("Metallic");
    public static readonly ConstString Material_MatSpecColor = new("MatSpecColor");
    public static readonly ConstString Material_FadeOffsetScale = new("FadeOffsetScale");
    public static readonly ConstString Material_NormalScale = new("NormalScale");
    public static readonly ConstString Material_DielectricReflectance = new("DielectricReflectance");

    public static readonly ConstString Custom_OutlineColor = new("OutlineColor");

    public static readonly ConstString Object_Model = new("Model");
    public static readonly ConstString Object_SHAr = new("SHAr");
    public static readonly ConstString Object_SHAg = new("SHAg");
    public static readonly ConstString Object_SHAb = new("SHAb");
    public static readonly ConstString Object_SHBr = new("SHBr");
    public static readonly ConstString Object_SHBg = new("SHBg");
    public static readonly ConstString Object_SHBb = new("SHBb");
    public static readonly ConstString Object_SHC = new("SHC");
    public static readonly ConstString Object_Ambient = new("Ambient");
    public static readonly ConstString Object_BillboardRot = new("BillboardRot");
    public static readonly ConstString Object_SkinMatrices = new("SkinMatrices");
}

/// <summary>
/// Built-in shader resources.
/// See _Samplers.glsl for descriptions.
/// </summary>
public static class ShaderResources
{
    public static readonly ConstString Albedo = new("Albedo");
    public static readonly ConstString Normal = new("Normal");
    public static readonly ConstString Properties = new("Properties");
    public static readonly ConstString Emission = new("Emission");
    public static readonly ConstString Reflection0 = new("Reflection0");
    public static readonly ConstString Reflection1 = new("Reflection1");
    public static readonly ConstString LightRamp = new("LightRamp");
    public static readonly ConstString LightShape = new("LightShape");
    public static readonly ConstString ShadowMap = new("ShadowMap");
    public static readonly ConstString DepthBuffer = new("DepthBuffer");
    public static readonly ConstString HarfBuzzAtlas = new("HarfBuzzAtlas");
}
