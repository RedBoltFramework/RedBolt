using Microsoft.Extensions.Logging;

namespace RedBolt;

/// <summary>
/// Description of shader program used for rendering.
///
/// Shader name may be different for different stages,
/// but this use case is obsolete and may not be fully supported by tools.
/// For best compatibility use same shader for all stages.
///
/// Shader defines should be the same for all stages, with one exception:
/// Defines that are ignored by the stage may be omitted in the stage defines list.
///
/// These restrictions are imposed to simplify possible shader preprocessing.
///
/// TODO: Consider replacing define string with tokenized define lists
/// </summary>
public struct ShaderProgramDesc : ICloneable
{
    public string[] ShaderName { get; set; }
    public string[] ShaderDefines { get; set; }
    public string CommonShaderDefines { get; set; }

    /// <summary>Hints about what the shader program is</summary>
    public bool IsInstancingUsed { get; set; }

    public ShaderProgramDesc()
    {
        ShaderName = new string[(int)ShaderType.MaxShaderTypes];
        ShaderDefines = new string[(int)ShaderType.MaxShaderTypes];
        CommonShaderDefines = string.Empty;
        IsInstancingUsed = false;
        Clear();
    }

    private ShaderProgramDesc(string[] shaderName, string[] shaderDefines,
        string commonShaderDefines, bool isInstancingUsed)
    {
        ShaderName = shaderName;
        ShaderDefines = shaderDefines;
        CommonShaderDefines = commonShaderDefines;
        IsInstancingUsed = isInstancingUsed;
    }

    public readonly object Clone() => new ShaderProgramDesc(
        (string[])ShaderName.Clone(), (string[])ShaderDefines.Clone(),
        CommonShaderDefines, IsInstancingUsed);

    public void Clear()
    {
        Array.Fill(ShaderName, string.Empty);
        Array.Fill(ShaderDefines, string.Empty);
        CommonShaderDefines = string.Empty;
        IsInstancingUsed = false;
    }

    public void AddCommonShaderDefines(string defines)
    {
        CommonShaderDefines += defines;
        if (!defines.EndsWith(' '))
            CommonShaderDefines += ' ';
    }

    public void AddShaderDefines(ShaderType type, string defines)
    {
        ShaderDefines[(int)type] += defines;
        if (!defines.EndsWith(' '))
            ShaderDefines[(int)type] += ' ';
    }
}

/// <summary>Generates shader program descritpions for scene and light volume batches.</summary>
public class ShaderProgramCompositor : RedObject
{
    private static readonly string[] GeometryDefines =
    [
        "URHO3D_GEOMETRY_STATIC ", "URHO3D_GEOMETRY_SKINNED ", "URHO3D_GEOMETRY_STATIC ",
        "URHO3D_GEOMETRY_BILLBOARD ", "URHO3D_GEOMETRY_DIRBILLBOARD ",
        "URHO3D_GEOMETRY_TRAIL_FACE_CAMERA ", "URHO3D_GEOMETRY_TRAIL_BONE ", "URHO3D_GEOMETRY_STATIC "
    ];

    private static readonly string[] LightTypeDefines =
    [
        "URHO3D_LIGHT_DIRECTIONAL ", "URHO3D_LIGHT_SPOT ", "URHO3D_LIGHT_POINT "
    ];

    private static readonly string[] AmbientModeDefines =
    [
        "URHO3D_AMBIENT_CONSTANT ", "URHO3D_AMBIENT_FLAT ", "URHO3D_AMBIENT_DIRECTIONAL "
    ];

    private ShaderProgramCompositorSettings _settings = new();
    private bool _isCameraOrthographic;
    private bool _isCameraClipped;
    private bool _isCameraReversed;
    private bool _linearColorSpace;

    public ShaderProgramCompositor()
    {
    }

    public void SetSettings(in ShaderProgramCompositorSettings settings)
    {
        _settings = settings;
    }

    public void SetFrameSettings(CameraProcessor cameraProcessor, bool linearColorSpace)
    {
        _isCameraOrthographic = cameraProcessor.IsCameraOrthographic;
        _isCameraClipped = cameraProcessor.IsCameraClipped;
        _isCameraReversed = cameraProcessor.IsCameraReversed;
        _linearColorSpace = linearColorSpace;
    }

    /// <summary>Process batches</summary>
    public void ProcessUserBatch(ref ShaderProgramDesc result, DrawableProcessorPassFlag flags,
        Drawable drawable, Geometry geometry, GeometryType geometryType, Material material, Pass pass,
        Light? light, bool hasShadow, BatchCompositorSubpass subpass)
    {
        SetupShaders(ref result, pass);
        ApplyCommonDefines(ref result, flags, pass);
        ApplyGeometryVertexDefines(ref result, flags, geometry, geometryType);

        ApplyLayoutVertexAndCommonDefinesForUserPass(ref result, geometry.GetVertexBuffer(0)!);
        ApplyMaterialPixelDefinesForUserPass(ref result, material, pass);

        ApplyNormalTangentSpaceDefines(ref result, geometryType, geometry.GetVertexBuffer(0)!);

        if (_isCameraClipped)
            result.AddShaderDefines(ShaderType.VS, "URHO3D_CLIP_PLANE");

        var isDeferred = subpass == BatchCompositorSubpass.Deferred;
        var isDepthOnly = (flags & DrawableProcessorPassFlag.DepthOnlyPass) != 0;
        if (subpass == BatchCompositorSubpass.Light)
            result.AddCommonShaderDefines("URHO3D_ADDITIVE_LIGHT_PASS");
        else if ((flags & DrawableProcessorPassFlag.HasAmbientLighting) != 0)
            ApplyAmbientLightingVertexAndCommonDefinesForUserPass(ref result, drawable, isDeferred);

        if (isDeferred)
            result.AddCommonShaderDefines("URHO3D_NUM_RENDER_TARGETS=4");
        else if (isDepthOnly)
            result.AddCommonShaderDefines("URHO3D_NUM_RENDER_TARGETS=0");

        if (light is not null)
            ApplyPixelLightPixelAndCommonDefines(ref result, light, hasShadow, material.GetSpecular());
    }

    public void ProcessShadowBatch(ref ShaderProgramDesc result, Geometry geometry, GeometryType geometryType,
        Material material, Pass pass, Light light)
    {
        const DrawableProcessorPassFlag flags = DrawableProcessorPassFlag.DepthOnlyPass;
        SetupShaders(ref result, pass);
        ApplyCommonDefines(ref result, flags, pass);
        ApplyGeometryVertexDefines(ref result, flags, geometry, geometryType);
        ApplyDefinesForShadowPass(ref result, light, geometry.GetVertexBuffer(0)!, material, pass);
    }

    public void ProcessLightVolumeBatch(ref ShaderProgramDesc result, Geometry geometry, GeometryType geometryType,
        Pass pass, Light light, bool hasShadow)
    {
        const DrawableProcessorPassFlag flags = DrawableProcessorPassFlag.DisableInstancing;
        SetupShaders(ref result, pass);
        ApplyCommonDefines(ref result, flags, pass);
        ApplyGeometryVertexDefines(ref result, flags, geometry, geometryType);
        ApplyPixelLightPixelAndCommonDefines(ref result, light, hasShadow, true);
        ApplyDefinesForLightVolumePass(ref result);
    }

    private void SetupShaders(ref ShaderProgramDesc result, Pass pass)
    {
        result.ShaderName[(int)ShaderType.VS] = "v2/" + pass.VertexShader;
        result.ShaderName[(int)ShaderType.PS] = "v2/" + pass.PixelShader;
    }

    private void ApplyCommonDefines(ref ShaderProgramDesc result, DrawableProcessorPassFlag flags, Pass pass)
    {
        var caps = GetSubsystem<RenderDevice>()!.GetCaps();
        var canReadDepth = _settings.RenderBufferManager.ReadableDepth && caps.ReadOnlyDepth;

        if (_isCameraReversed)
            result.AddCommonShaderDefines("URHO3D_CAMERA_REVERSED");

        if ((flags & DrawableProcessorPassFlag.DepthOnlyPass) == 0)
        {
            if (_settings.SceneProcessor.CubemapBoxProjection)
                result.AddCommonShaderDefines("URHO3D_BOX_PROJECTION");
            if (_settings.SceneProcessor.LinearReflectionMaps)
                result.AddCommonShaderDefines("URHO3D_LINEAR_SPACE_REFLECTIONS");
            if (_linearColorSpace)
                result.AddCommonShaderDefines("URHO3D_GAMMA_CORRECTION");

            switch (_settings.SceneProcessor.SpecularQuality)
            {
                case SpecularQuality.Simple:
                    result.AddCommonShaderDefines("URHO3D_SPECULAR=1");
                    break;
                case SpecularQuality.Antialiased:
                    result.AddCommonShaderDefines("URHO3D_SPECULAR=2");
                    break;
            }

            if (_settings.SceneProcessor.ReflectionQuality == ReflectionQuality.Vertex)
                result.AddCommonShaderDefines("URHO3D_VERTEX_REFLECTION");
        }

        if ((flags & DrawableProcessorPassFlag.NeedReadableDepth) != 0 && canReadDepth)
        {
            result.AddCommonShaderDefines("URHO3D_HAS_READABLE_DEPTH");
            if (_isCameraOrthographic)
                result.AddCommonShaderDefines("URHO3D_ORTHOGRAPHIC_DEPTH");
        }
        if ((flags & DrawableProcessorPassFlag.StereoInstancing) != 0)
            result.AddCommonShaderDefines("URHO3D_XR");

        result.AddShaderDefines(ShaderType.VS, pass.GetEffectiveVertexShaderDefines());
        result.AddShaderDefines(ShaderType.PS, pass.GetEffectivePixelShaderDefines());
    }

    private void ApplyGeometryVertexDefines(ref ShaderProgramDesc result, DrawableProcessorPassFlag flags,
        Geometry geometry, GeometryType geometryType)
    {
        result.IsInstancingUsed = IsInstancingUsed(flags, geometry, geometryType);
        if (result.IsInstancingUsed)
            result.AddShaderDefines(ShaderType.VS, "URHO3D_INSTANCING");

        var geometryTypeIndex = (int)geometryType;
        result.AddShaderDefines(ShaderType.VS, geometryTypeIndex < GeometryDefines.Length
            ? GeometryDefines[geometryTypeIndex]
            : $"URHO3D_GEOMETRY_CUSTOM={geometryTypeIndex} ");

        if (geometryType == GeometryType.Skinned)
            result.AddShaderDefines(ShaderType.VS, $"URHO3D_MAXBONES={Graphics.GetMaxBones()} ");
    }

    private void ApplyPixelLightPixelAndCommonDefines(ref ShaderProgramDesc result, Light light, bool hasShadow,
        bool materialHasSpecular)
    {
        if (light.GetShapeTexture() is not null)
            result.AddCommonShaderDefines("URHO3D_LIGHT_CUSTOM_SHAPE");
        if (light.GetRampTexture() is not null)
            result.AddShaderDefines(ShaderType.PS, "URHO3D_LIGHT_CUSTOM_RAMP");

        result.AddCommonShaderDefines(LightTypeDefines[(int)light.GetLightType()]);

        if (hasShadow)
        {
            var maxCascades = light.GetLightType() == LightType.Directional
                ? checked((int)Light.MaxCascadeSplits) : 1;
            result.AddCommonShaderDefines("URHO3D_HAS_SHADOW");
            if (maxCascades > 1)
                result.AddCommonShaderDefines($"URHO3D_MAX_SHADOW_CASCADES={maxCascades}");
            if (_settings.ShadowMapAllocator.EnableVarianceShadowMaps)
                result.AddCommonShaderDefines("URHO3D_VARIANCE_SHADOW_MAP");
            else
                result.AddCommonShaderDefines($"URHO3D_SHADOW_PCF_SIZE={_settings.SceneProcessor.PcfKernelSize}");
        }
    }

    private void ApplyNormalTangentSpaceDefines(ref ShaderProgramDesc result, GeometryType geometryType,
        VertexBuffer vertexBuffer)
    {
        var (hasNormal, hasTangent) = IsNormalAndTangentAvailable(geometryType, vertexBuffer);
        if (hasNormal)
            result.AddCommonShaderDefines("URHO3D_VERTEX_NORMAL_AVAILABLE");
        if (hasTangent)
            result.AddCommonShaderDefines("URHO3D_VERTEX_TANGENT_AVAILABLE");
    }

    private void ApplyLayoutVertexAndCommonDefinesForUserPass(ref ShaderProgramDesc result,
        VertexBuffer vertexBuffer)
    {
        if (vertexBuffer.HasElement(VertexElementSemantic.Normal))
            result.AddShaderDefines(ShaderType.VS, "URHO3D_VERTEX_HAS_NORMAL");
        if (vertexBuffer.HasElement(VertexElementSemantic.Tangent))
            result.AddShaderDefines(ShaderType.VS, "URHO3D_VERTEX_HAS_TANGENT");
        if (vertexBuffer.HasElement(VertexElementSemantic.TexCoord, 0))
            result.AddShaderDefines(ShaderType.VS, "URHO3D_VERTEX_HAS_TEXCOORD0");
        if (vertexBuffer.HasElement(VertexElementSemantic.TexCoord, 1))
            result.AddShaderDefines(ShaderType.VS, "URHO3D_VERTEX_HAS_TEXCOORD1");
        if (vertexBuffer.HasElement(VertexElementSemantic.Color))
            result.AddCommonShaderDefines("URHO3D_VERTEX_HAS_COLOR");
    }

    private void ApplyMaterialPixelDefinesForUserPass(ref ShaderProgramDesc result, Material material, Pass pass)
    {
        foreach (var name in pass.VertexTextureDefines)
            AddTextureDefine(ref result, ShaderType.VS, material, name);
        foreach (var name in pass.PixelTextureDefines)
            AddTextureDefine(ref result, ShaderType.PS, material, name);
    }

    private void ApplyAmbientLightingVertexAndCommonDefinesForUserPass(ref ShaderProgramDesc result,
        Drawable drawable, bool isGeometryBufferPass)
    {
        result.AddCommonShaderDefines("URHO3D_AMBIENT_PASS");
        if (isGeometryBufferPass)
            result.AddCommonShaderDefines("URHO3D_GBUFFER_PASS");
        else if (_settings.SceneProcessor.MaxVertexLights > 0)
            result.AddCommonShaderDefines($"URHO3D_NUM_VERTEX_LIGHTS={_settings.SceneProcessor.MaxVertexLights}");

        if (drawable.GetGlobalIlluminationType() == GlobalIlluminationType.UseLightMap)
            result.AddCommonShaderDefines("URHO3D_HAS_LIGHTMAP");
        if (drawable.GetReflectionMode() >= ReflectionMode.BlendProbes)
            result.AddCommonShaderDefines("URHO3D_BLEND_REFLECTIONS");

        result.AddShaderDefines(ShaderType.VS, AmbientModeDefines[(int)_settings.SceneProcessor.AmbientMode]);
    }

    private void ApplyDefinesForShadowPass(ref ShaderProgramDesc result, Light light, VertexBuffer vertexBuffer,
        Material material, Pass pass)
    {
        foreach (var name in pass.VertexTextureDefines)
            AddTextureDefine(ref result, ShaderType.VS, material, name);
        foreach (var name in pass.PixelTextureDefines)
            AddTextureDefine(ref result, ShaderType.PS, material, name);

        // This is not strictly true for unusual geometry types that generate normals from other attributes,
        // but it's good enough for average shadow caster with usual geometry.
        if (vertexBuffer.HasElement(VertexElementSemantic.Normal))
            result.AddShaderDefines(ShaderType.VS, "URHO3D_VERTEX_HAS_NORMAL URHO3D_VERTEX_NORMAL_AVAILABLE");
        if (light.GetShadowBias().NormalOffset > 0.0f)
            result.AddShaderDefines(ShaderType.VS, "URHO3D_SHADOW_NORMAL_OFFSET");
        if (pass.IsAlphaMask && vertexBuffer.HasElement(VertexElementSemantic.TexCoord, 0))
            result.AddShaderDefines(ShaderType.VS, "URHO3D_VERTEX_HAS_TEXCOORD0");

        result.AddCommonShaderDefines("URHO3D_SHADOW_PASS");
        if (_settings.ShadowMapAllocator.EnableVarianceShadowMaps)
            result.AddCommonShaderDefines("URHO3D_VARIANCE_SHADOW_MAP");
        else
            result.AddCommonShaderDefines("URHO3D_NUM_RENDER_TARGETS=0");
    }

    private void ApplyDefinesForLightVolumePass(ref ShaderProgramDesc result)
    {
        result.AddCommonShaderDefines("URHO3D_LIGHT_VOLUME_PASS");
        if (_isCameraOrthographic)
            result.AddCommonShaderDefines("URHO3D_ORTHOGRAPHIC_DEPTH");
        if (_settings.SceneProcessor.LightingMode == DirectLightingMode.DeferredPBR)
            result.AddCommonShaderDefines("URHO3D_PHYSICAL_MATERIAL");
    }

    private bool IsInstancingUsed(DrawableProcessorPassFlag flags, Geometry geometry, GeometryType geometryType)
    {
        return (flags & DrawableProcessorPassFlag.DisableInstancing) == 0
            && _settings.InstancingBuffer.EnableInstancing
            && geometry.IsInstanced(geometryType);
    }

    private (bool, bool) IsNormalAndTangentAvailable(GeometryType geometryType, VertexBuffer vertexBuffer)
    {
        return geometryType switch
        {
            GeometryType.Billboard or GeometryType.DirectionalBillboard or GeometryType.TrailFaceCamera or GeometryType.TrailBone
                => (true, true),
            GeometryType.Static or GeometryType.Skinned or GeometryType.Instanced or GeometryType.StaticNoInstancing
                => (vertexBuffer.HasElement(VertexElementSemantic.Normal),
                    vertexBuffer.HasElement(VertexElementSemantic.Tangent)),
            _ => (true, true)
        };
    }

    private static int GetTextureColorSpaceHint(bool linearInput, bool srgbTexture)
    {
        return (linearInput ? 1 : 0) + (srgbTexture ? 1 : 0);
    }

    private static void AddTextureDefine(ref ShaderProgramDesc result, ShaderType shaderType, Material material,
        string name)
    {
        var texture = material.GetTexture(name);
        if (texture is null)
            return;

        var hint = GetTextureColorSpaceHint(texture.GetLinear(), texture.GetSRGB());
        if (hint > 1)
        {
            Log.GetLogger().Log(Microsoft.Extensions.Logging.LogLevel.Warning, default, $"Texture {material.GetName()} cannot be both sRGB and Linear",
                null, static (state, _) => state);
        }
        result.AddShaderDefines(shaderType, $"URHO3D_TEXTURE_{name.ToUpperInvariant()}={Math.Min(1, hint) + 1}");
    }

}
