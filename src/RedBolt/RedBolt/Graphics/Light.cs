using Microsoft.Extensions.Logging;
using SystemMath = global::System.Math;

using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Logging;
using RedBolt.Math;
using RedBolt.Resource;
using ResourceType = global::RedBolt.Resource.Resource;
using RedBolt.Scene;
using SceneType = global::RedBolt.Scene.Scene;

namespace RedBolt.Graphics;

public enum LightMode
{
    Realtime,
    Mixed,
    Baked
}

public enum LightType
{
    Directional,
    Spot,
    Point
}

public enum LightImportance
{
    Auto,
    Important,
    NotImportant
}

/// <summary>Depth bias parameters. Used both by lights (for shadow mapping) and materials.</summary>
public struct BiasParameters
{
    /// <summary>Construct with initial values.</summary>
    public BiasParameters(float constantBias, float slopeScaledBias, float normalOffset = 0.0f)
    {
        ConstantBias = constantBias;
        SlopeScaledBias = slopeScaledBias;
        NormalOffset = normalOffset;
    }

    /// <summary>Validate parameters.</summary>
    public void Validate()
    {
        ConstantBias = MathDefs.Clamp(ConstantBias, -1.0f, 1.0f);
        SlopeScaledBias = MathDefs.Clamp(SlopeScaledBias, -16.0f, 16.0f);
        NormalOffset = SystemMath.Max(NormalOffset, 0.0f);
    }

    /// <summary>Constant bias.</summary>
    public float ConstantBias;

    /// <summary>Slope scaled bias.</summary>
    public float SlopeScaledBias;

    /// <summary>Normal offset multiplier.</summary>
    public float NormalOffset;
}

/// <summary>Cascaded shadow map parameters.</summary>
public struct CascadeParameters
{
    /// <summary>Construct with initial values.</summary>
    public CascadeParameters(float split1, float split2, float split3, float split4, float fadeStart, float biasAutoAdjust = 1.0f)
    {
        Splits = new Vector4f(split1, split2, split3, split4);
        FadeStart = fadeStart;
        BiasAutoAdjust = biasAutoAdjust;
    }

    /// <summary>Validate parameters.</summary>
    public void Validate()
    {
        Splits = new Vector4f(SystemMath.Max(Splits.X, 0.0f), SystemMath.Max(Splits.Y, 0.0f),
            SystemMath.Max(Splits.Z, 0.0f), SystemMath.Max(Splits.W, 0.0f));
        FadeStart = MathDefs.Clamp(FadeStart, MathDefs.Epsilon, 1.0f);
    }

    /// <summary>Return maximum shadow distance.</summary>
    public readonly float GetShadowRange() => SystemMath.Max(SystemMath.Max(Splits.X, Splits.Y), SystemMath.Max(Splits.Z, Splits.W));

    /// <summary>Cascade split distances.</summary>
    public Vector4f Splits;

    /// <summary>Start of shadow fade relative to maximum shadow distance.</summary>
    public float FadeStart;

    /// <summary>Automatic depth bias adjustment strength.</summary>
    public float BiasAutoAdjust;
}

/// <summary>Shadow map focusing parameters.</summary>
public struct FocusParameters
{
    /// <summary>Construct with initial values.</summary>
    public FocusParameters(bool focus, bool nonUniform, bool autoSize, float quantize, float minView)
    {
        Focus = focus;
        NonUniform = nonUniform;
        AutoSize = autoSize;
        Quantize = quantize;
        MinView = minView;
    }

    /// <summary>Validate parameters.</summary>
    public void Validate()
    {
        Quantize = SystemMath.Max(Quantize, Light.ShadowMinQuantize);
        MinView = SystemMath.Max(MinView, Light.ShadowMinView);
    }

    /// <summary>Focus flag.</summary>
    public bool Focus;

    /// <summary>Non-uniform focusing flag.</summary>
    public bool NonUniform;

    /// <summary>Auto-size focusing flag.</summary>
    public bool AutoSize;

    /// <summary>Focus quantization.</summary>
    public float Quantize;

    /// <summary>Minimum view size.</summary>
    public float MinView;
}

/// <summary>Light component.</summary>
public class LightBatchQueue
{
}

[Reflected(ObjectCategory.Category_Scene)]
[Serialized(
    "Is Enabled",
    typeof(bool),
    DefaultSource = nameof(GetIsEnabledAttributeDefault),
    Getter = nameof(Enabled),
    Setter = nameof(SetEnabled)
)]
[Serialized(
    "Light Type",
    typeof(LightType),
    DefaultSource = nameof(GetLightTypeAttributeDefault),
    Getter = nameof(GetLightType),
    Setter = nameof(SetLightType)
)]
[Serialized(
    "Light Importance",
    typeof(LightImportance),
    DefaultSource = nameof(GetLightImportanceAttributeDefault),
    Getter = nameof(GetLightImportance),
    Setter = nameof(SetLightImportance)
)]
[Serialized(
    "Light Mode",
    typeof(LightMode),
    DefaultSource = nameof(GetLightModeAttributeDefault),
    Getter = nameof(GetLightMode),
    Setter = nameof(SetLightMode)
)]
[Serialized(
    "Color",
    typeof(Color),
    AccessorType = typeof(ColorAttributeAccessor),
    DefaultSource = nameof(GetColorAttributeDefault)
)]
[Serialized(
    "Specular Intensity",
    typeof(float),
    DefaultSource = nameof(GetSpecularIntensityAttributeDefault),
    Getter = nameof(GetSpecularIntensity),
    Setter = nameof(SetSpecularIntensity)
)]
[Serialized(
    "Brightness Multiplier",
    typeof(float),
    DefaultSource = nameof(GetBrightnessMultiplierAttributeDefault),
    Getter = nameof(GetBrightness),
    Setter = nameof(SetBrightness)
)]
[Serialized(
    "Indirect Brightness",
    typeof(float),
    DefaultSource = nameof(GetIndirectBrightnessAttributeDefault),
    Getter = nameof(GetIndirectBrightness),
    Setter = nameof(SetIndirectBrightness)
)]
[Serialized(
    "Temperature",
    typeof(float),
    DefaultSource = nameof(GetTemperatureAttributeDefault),
    Getter = nameof(GetTemperature),
    Setter = nameof(SetTemperature)
)]
[Serialized(
    "Use Physical Values",
    typeof(bool),
    DefaultSource = nameof(GetUsePhysicalValuesAttributeDefault),
    Getter = nameof(GetUsePhysicalValues),
    Setter = nameof(SetUsePhysicalValues)
)]
[Serialized(
    "Radius",
    typeof(float),
    DefaultSource = nameof(GetRadiusAttributeDefault),
    Getter = nameof(GetRadius),
    Setter = nameof(SetRadius)
)]
[Serialized(
    "Length",
    typeof(float),
    DefaultSource = nameof(GetLengthAttributeDefault),
    Getter = nameof(GetLength),
    Setter = nameof(SetLength)
)]
[Serialized(
    "Range",
    typeof(float),
    DefaultSource = nameof(GetRangeAttributeDefault),
    Getter = nameof(GetRange),
    Setter = nameof(SetRange)
)]
[Serialized(
    "Spot FOV",
    typeof(float),
    DefaultSource = nameof(GetSpotFOVAttributeDefault),
    Getter = nameof(GetFov),
    Setter = nameof(SetFov)
)]
[Serialized(
    "Spot Aspect Ratio",
    typeof(float),
    DefaultSource = nameof(GetSpotAspectRatioAttributeDefault),
    Getter = nameof(GetAspectRatio),
    Setter = nameof(SetAspectRatio)
)]
[Serialized(
    "Attenuation Texture",
    typeof(ResourceRef),
    DefaultSource = nameof(GetAttenuationTextureAttributeDefault),
    Getter = nameof(GetRampTextureAttr),
    Setter = nameof(SetRampTextureAttr)
)]
[Serialized(
    "Light Shape Texture",
    typeof(ResourceRef),
    DefaultSource = nameof(GetLightShapeTextureAttributeDefault),
    Getter = nameof(GetShapeTextureAttr),
    Setter = nameof(SetShapeTextureAttr)
)]
[Serialized(
    "Can Be Occluded",
    typeof(bool),
    DefaultSource = nameof(GetCanBeOccludedAttributeDefault),
    Getter = nameof(IsOccludee),
    Setter = nameof(SetOccludee)
)]
[Serialized(
    "Cast Shadows",
    typeof(bool),
    DefaultSource = nameof(GetCastShadowsAttributeDefault),
    Getter = nameof(GetCastShadows),
    Setter = nameof(SetCastShadows)
)]
[Serialized(
    "Draw Distance",
    typeof(float),
    DefaultSource = nameof(GetDrawDistanceAttributeDefault),
    Getter = nameof(GetDrawDistance),
    Setter = nameof(SetDrawDistance)
)]
[Serialized(
    "Fade Distance",
    typeof(float),
    DefaultSource = nameof(GetFadeDistanceAttributeDefault),
    Getter = nameof(GetFadeDistance),
    Setter = nameof(SetFadeDistance)
)]
[Serialized(
    "Shadow Distance",
    typeof(float),
    DefaultSource = nameof(GetShadowDistanceAttributeDefault),
    Getter = nameof(GetShadowDistance),
    Setter = nameof(SetShadowDistance)
)]
[Serialized(
    "Shadow Fade Distance",
    typeof(float),
    DefaultSource = nameof(GetShadowFadeDistanceAttributeDefault),
    Getter = nameof(GetShadowFadeDistance),
    Setter = nameof(SetShadowFadeDistance)
)]
[Serialized(
    "Shadow Intensity",
    typeof(float),
    DefaultSource = nameof(GetShadowIntensityAttributeDefault),
    Getter = nameof(GetShadowIntensity),
    Setter = nameof(SetShadowIntensity)
)]
[Serialized(
    "Shadow Resolution",
    typeof(float),
    DefaultSource = nameof(GetShadowResolutionAttributeDefault),
    Getter = nameof(GetShadowResolution),
    Setter = nameof(SetShadowResolution)
)]
[Serialized(
    "Focus To Scene",
    typeof(bool),
    DefaultSource = nameof(GetFocusToSceneAttributeDefault),
    Getter = nameof(GetSerializedFocusToScene),
    Setter = nameof(SetSerializedFocusToScene)
)]
[Serialized(
    "Non-uniform View",
    typeof(bool),
    DefaultSource = nameof(GetNonUniformViewAttributeDefault),
    Getter = nameof(GetSerializedNonUniformView),
    Setter = nameof(SetSerializedNonUniformView)
)]
[Serialized(
    "Auto-Reduce Size",
    typeof(bool),
    DefaultSource = nameof(GetAutoReduceSizeAttributeDefault),
    Getter = nameof(GetSerializedAutoReduceSize),
    Setter = nameof(SetSerializedAutoReduceSize)
)]
[Serialized(
    "CSM Splits",
    typeof(Vector4f),
    DefaultSource = nameof(GetCSMSplitsAttributeDefault),
    Getter = nameof(GetSerializedCSMSplits),
    Setter = nameof(SetSerializedCSMSplits)
)]
[Serialized(
    "CSM Fade Start",
    typeof(float),
    DefaultSource = nameof(GetCSMFadeStartAttributeDefault),
    Getter = nameof(GetSerializedCSMFadeStart),
    Setter = nameof(SetSerializedCSMFadeStart)
)]
[Serialized(
    "CSM Bias Auto Adjust",
    typeof(float),
    DefaultSource = nameof(GetCSMBiasAutoAdjustAttributeDefault),
    Getter = nameof(GetSerializedCSMBiasAutoAdjust),
    Setter = nameof(SetSerializedCSMBiasAutoAdjust)
)]
[Serialized(
    "View Size Quantize",
    typeof(float),
    DefaultSource = nameof(GetViewSizeQuantizeAttributeDefault),
    Getter = nameof(GetSerializedViewSizeQuantize),
    Setter = nameof(SetSerializedViewSizeQuantize)
)]
[Serialized(
    "View Size Minimum",
    typeof(float),
    DefaultSource = nameof(GetViewSizeMinimumAttributeDefault),
    Getter = nameof(GetSerializedViewSizeMinimum),
    Setter = nameof(SetSerializedViewSizeMinimum)
)]
[Serialized(
    "Depth Constant Bias",
    typeof(float),
    DefaultSource = nameof(GetDepthConstantBiasAttributeDefault),
    Getter = nameof(GetSerializedDepthConstantBias),
    Setter = nameof(SetSerializedDepthConstantBias)
)]
[Serialized(
    "Depth Slope Bias",
    typeof(float),
    DefaultSource = nameof(GetDepthSlopeBiasAttributeDefault),
    Getter = nameof(GetSerializedDepthSlopeBias),
    Setter = nameof(SetSerializedDepthSlopeBias)
)]
[Serialized(
    "Normal Offset",
    typeof(float),
    DefaultSource = nameof(GetNormalOffsetAttributeDefault),
    Getter = nameof(GetSerializedNormalOffset),
    Setter = nameof(SetSerializedNormalOffset)
)]
[Serialized(
    "Near/Farclip Ratio",
    typeof(float),
    DefaultSource = nameof(GetNearFarclipRatioAttributeDefault),
    Getter = nameof(GetShadowNearFarRatio),
    Setter = nameof(SetShadowNearFarRatio)
)]
[Serialized(
    "Max Extrusion",
    typeof(float),
    DefaultSource = nameof(GetMaxExtrusionAttributeDefault),
    Getter = nameof(GetShadowMaxExtrusion),
    Setter = nameof(SetShadowMaxExtrusion)
)]
[Serialized(
    "View Mask",
    typeof(uint),
    DefaultSource = nameof(GetViewMaskAttributeDefault),
    Getter = nameof(GetViewMask),
    Setter = nameof(SetViewMask)
)]
[Serialized(
    "Light Mask",
    typeof(uint),
    DefaultSource = nameof(GetLightMaskAttributeDefault),
    Getter = nameof(GetLightMask),
    Setter = nameof(SetLightMask)
)]
public partial class Light : Drawable
{
    private sealed class ColorAttributeAccessor : AttributeAccessor
    {
        public override void Get(Serializable ptr, ref Variant dest)
        {
            dest = new Variant(((Light)ptr).GetColor());
        }

        public override void Set(Serializable ptr, Variant src)
        {
            ((Light)ptr).SetColor(src.GetColor());
        }
    }

    public const float ShadowMinQuantize = 0.1f;
    public const float ShadowMinView = 1.0f;
    public const int MaxLightSplits = 6;
    public const int MaxCascadeSplits = 4;
    public const float DefaultRange = 10.0f;
    public const float DefaultLightFov = 30.0f;
    public const float DefaultSpecularIntensity = 1.0f;
    public const float DefaultBrightness = 1.0f;
    public const float DefaultConstantBias = 0.0002f;
    public const float DefaultSlopeScaledBias = 0.5f;
    public const float DefaultNormalOffset = 0.0f;
    public const float DefaultBiasAutoAdjust = 1.0f;
    public const float DefaultShadowFadeStart = 0.8f;
    public const float DefaultShadowQuantize = 0.5f;
    public const float DefaultShadowMinView = 3.0f;
    public const float DefaultShadowNearFarRatio = 0.002f;
    public const float DefaultShadowMaxExtrusion = 1000.0f;
    public const float DefaultShadowSplit = 1000.0f;
    public const float DefaultTemperature = 6590.0f;
    public const LightType DefaultLightType = LightType.Point;
    public const LightImportance DefaultLightImportance = LightImportance.Auto;

    private LightType _lightType = LightType.Point;
    private float _range = DefaultRange;
    private float _fov = DefaultLightFov;
    private float _aspectRatio = 1.0f;
    private Texture? _rampTexture;
    private Texture? _shapeTexture;
    private LightBatchQueue? _lightQueue;
    private Matrix3x4 _volumeTransform = Matrix3x4.Identity;
    private readonly Frustum _worldBoundingFrustum = new();

    public Light()
        : base(DrawableFlags.Light)
    {
    }


    /// <summary>Destruct.</summary>
    public override void Dispose()
    {
        _shapeTexture = null;
        _rampTexture = null;
        base.Dispose();
    }

    public override void DrawDebugGeometry(DebugRenderer? debug, bool depthTest)
    {
        var color = GetEffectiveColor();

        if (debug is not null && IsEnabledEffective)
        {
            switch (_lightType)
            {
                case LightType.Directional:
                {
                    var start = Node.GetWorldPosition();
                    var end = start + Node.GetWorldDirection() * 10.0f;
                    for (var i = -1; i < 2; ++i)
                    {
                        for (var j = -1; j < 2; ++j)
                        {
                            var offset = Vector3f.Up * (5.0f * i) + Vector3f.Right * (5.0f * j);
                            debug.AddSphere(new Sphere(start + offset, 0.1f), color, depthTest);
                            debug.AddLine(start + offset, end + offset, color, depthTest);
                        }
                    }
                    break;
                }

                case LightType.Spot:
                    debug.AddFrustum(GetFrustum(), color, depthTest);
                    break;

                case LightType.Point:
                    debug.AddSphere(new Sphere(Node.GetWorldPosition(), _range), color, depthTest);
                    break;
            }
        }
    }

    public override void UpdateBatches(FrameInfo frame)
    {
        switch (_lightType)
        {
            case LightType.Directional:
                // Directional light affects the whole scene, so it is always "closest"
                SetDistance(0.0f);
                break;

            default:
                SetDistance(frame.Camera!.GetDistance(Node.WorldPosition));
                break;
        }
    }

    public LightType LightType => _lightType;

    public LightImportance LightImportance { get; private set; } = LightImportance.Auto;

    public LightMode LightMode { get; private set; } = LightMode.Realtime;

    public Color Color { get; private set; } = Color.White;

    public float Temperature { get; private set; } = DefaultTemperature;

    public float Radius { get; private set; }

    public float Length { get; private set; }

    public bool UsePhysicalValues { get; private set; }

    public float SpecularIntensity { get; private set; } = DefaultSpecularIntensity;

    public float Brightness { get; private set; } = DefaultBrightness;

    public float IndirectBrightness { get; private set; } = 1.0f;

    public float Range => _range;

    public float Fov => _fov;

    public float AspectRatio => _aspectRatio;

    public float FadeDistance { get; private set; }

    public float ShadowFadeDistance { get; private set; }

    public BiasParameters ShadowBias { get; private set; } = new(DefaultConstantBias, DefaultSlopeScaledBias, DefaultNormalOffset);

    public CascadeParameters ShadowCascade { get; private set; } = new(DefaultShadowSplit, 0.0f, 0.0f, 0.0f, DefaultShadowFadeStart, DefaultBiasAutoAdjust);

    public FocusParameters ShadowFocus { get; private set; } = new(true, true, true, DefaultShadowQuantize, DefaultShadowMinView);

    public float ShadowIntensity { get; private set; }

    public float ShadowResolution { get; private set; } = 1.0f;

    public float ShadowNearFarRatio { get; private set; } = DefaultShadowNearFarRatio;

    public float ShadowMaxExtrusion { get; private set; } = DefaultShadowMaxExtrusion;

    public void SetLightType(LightType type)
    {
        _lightType = type;
        OnMarkedDirty(Node!);
    }

    public LightType GetLightType() => _lightType;

    public void SetLightImportance(LightImportance importance)
    {
        LightImportance = importance;
    }

    public LightImportance GetLightImportance() => LightImportance;

    public void SetLightMode(LightMode mode)
    {
        LightMode = mode;
    }

    public LightMode GetLightMode() => LightMode;

    public uint GetLightMaskEffective() => LightMode == LightMode.Baked ? 0u : GetLightMask();

    public bool GetPerVertex() => LightImportance == LightImportance.NotImportant;

    public void SetColor(Color color)
    {
        Color = new Color(color.R, color.G, color.B, 1.0f);
    }

    public Color GetColor() => Color;

    public void SetTemperature(float temperature)
    {
        Temperature = MathDefs.Clamp(temperature, 1000.0f, 10000.0f);
    }

    public float GetTemperature() => Temperature;

    public void SetRadius(float radius)
    {
        Radius = radius;
    }

    public float GetRadius() => Radius;

    public void SetLength(float length)
    {
        Length = length;
    }

    public float GetLength() => Length;

    public void SetUsePhysicalValues(bool enable)
    {
        UsePhysicalValues = enable;
    }

    public bool GetUsePhysicalValues() => UsePhysicalValues;

    public Color GetColorFromTemperature()
    {
        var u = (0.860117757f + 1.54118254e-4f * Temperature + 1.28641212e-7f * Temperature * Temperature) /
            (1.0f + 8.42420235e-4f * Temperature + 7.08145163e-7f * Temperature * Temperature);
        var v = (0.317398726f + 4.22806245e-5f * Temperature + 4.20481691e-8f * Temperature * Temperature) /
            (1.0f - 2.89741816e-5f * Temperature + 1.61456053e-7f * Temperature * Temperature);
        var x = 3.0f * u / (2.0f * u - 8.0f * v + 4.0f);
        var y = 2.0f * v / (2.0f * u - 8.0f * v + 4.0f);
        var z = 1.0f - x - y;
        var xScaled = x / y;
        var zScaled = z / y;
        return new Color(
            3.2404542f * xScaled - 1.5371385f - 0.4985314f * zScaled,
            -0.9692660f * xScaled + 1.8760108f + 0.0415560f * zScaled,
            0.0556434f * xScaled - 0.2040259f + 1.0572252f * zScaled);
    }

    public void SetSpecularIntensity(float intensity)
    {
        SpecularIntensity = SystemMath.Max(intensity, 0.0f);
    }

    public float GetSpecularIntensity() => SpecularIntensity;

    public void SetBrightness(float brightness)
    {
        Brightness = brightness;
    }

    public float GetBrightness() => Brightness;

    public void SetIndirectBrightness(float indirectBrightness)
    {
        IndirectBrightness = indirectBrightness;
    }

    public float GetIndirectBrightness() => IndirectBrightness;

    public Color GetEffectiveColor()
    {
        if (UsePhysicalValues)
        {
            var tempColor = GetColorFromTemperature();
            var energy = Brightness * 64.0f / 10000.0f;
            return new Color(tempColor.R * Color.R * energy, tempColor.G * Color.G * energy, tempColor.B * Color.B * energy, 1.0f);
        }

        return new Color(Color * Brightness, 1.0f);
    }

    public float GetEffectiveSpecularIntensity() => SpecularIntensity * SystemMath.Abs(Brightness);

    public void SetRange(float range)
    {
        this._range = SystemMath.Max(range, 0.0f);
        OnMarkedDirty(Node!);
    }

    public float GetRange() => _range;

    public void SetFov(float fov)
    {
        this._fov = MathDefs.Clamp(fov, 0.0f, MathDefs.MaxFov);
        OnMarkedDirty(Node!);
    }

    public float GetFov() => _fov;

    public void SetAspectRatio(float aspectRatio)
    {
        this._aspectRatio = SystemMath.Max(aspectRatio, MathDefs.Epsilon);
        OnMarkedDirty(Node!);
    }

    public float GetAspectRatio() => _aspectRatio;

    public void SetFadeDistance(float distance)
    {
        FadeDistance = SystemMath.Max(distance, 0.0f);
    }

    public float GetFadeDistance() => FadeDistance;

    public void SetShadowFadeDistance(float distance)
    {
        ShadowFadeDistance = SystemMath.Max(distance, 0.0f);
    }

    public float GetShadowFadeDistance() => ShadowFadeDistance;

    public void SetShadowBias(BiasParameters parameters)
    {
        parameters.Validate();
        ShadowBias = parameters;
    }

    public BiasParameters GetShadowBias() => ShadowBias;

    public void SetShadowCascade(CascadeParameters parameters)
    {
        parameters.Validate();
        ShadowCascade = parameters;
    }

    public CascadeParameters GetShadowCascade() => ShadowCascade;

    public void SetShadowFocus(FocusParameters parameters)
    {
        parameters.Validate();
        ShadowFocus = parameters;
    }

    public FocusParameters GetShadowFocus() => ShadowFocus;

    public void SetShadowIntensity(float intensity)
    {
        ShadowIntensity = MathDefs.Clamp(intensity, 0.0f, 1.0f);
    }

    public float GetShadowIntensity() => ShadowIntensity;

    public void SetShadowResolution(float resolution)
    {
        ShadowResolution = MathDefs.Clamp(resolution, 0.125f, 1.0f);
    }

    public float GetShadowResolution() => ShadowResolution;

    public void SetShadowNearFarRatio(float nearFarRatio)
    {
        ShadowNearFarRatio = MathDefs.Clamp(nearFarRatio, 0.0f, 0.5f);
    }

    public float GetShadowNearFarRatio() => ShadowNearFarRatio;

    public void SetShadowMaxExtrusion(float extrusion)
    {
        ShadowMaxExtrusion = SystemMath.Max(extrusion, 0.0f);
    }

    public float GetShadowMaxExtrusion() => ShadowMaxExtrusion;

    public void SetRampTexture(Texture? texture)
    {
        _rampTexture = texture;
    }

    public Texture? GetRampTexture() => _rampTexture;

    public void SetShapeTexture(Texture? texture)
    {
        _shapeTexture = texture;
    }

    public Texture? GetShapeTexture() => _shapeTexture;

    /// <summary>Return spotlight frustum.</summary>
    public Frustum GetFrustum()
    {
        var frustum = new Frustum();
        GetFrustum(frustum);
        return frustum;
    }

    internal void GetFrustum(Frustum frustum)
    {
        var transform = TryGetNode() is not null
            ? new Matrix3x4(Node.WorldPosition, Node.WorldRotation, 1.0f)
            : Matrix3x4.Identity;
        frustum.Define(_fov, _aspectRatio, 1.0f, MathDefs.MinNearClip, _range, transform);
    }

    /// <summary>Return spotlight frustum in the specified view space.</summary>
    public Frustum GetViewSpaceFrustum(Matrix3x4 view)
    {
        var frustum = new Frustum();
        GetViewSpaceFrustum(view, frustum);
        return frustum;
    }

    internal void GetViewSpaceFrustum(Matrix3x4 view, Frustum frustum)
    {
        var transform = TryGetNode() is not null
            ? new Matrix3x4(Node.WorldPosition, Node.WorldRotation, 1.0f)
            : Matrix3x4.Identity;
        frustum.Define(_fov, _aspectRatio, 1.0f, MathDefs.MinNearClip, _range, view * transform);
    }

    public Matrix3x4 GetVolumeTransform(Camera? camera)
    {
        if (TryGetNode() is null)
            return Matrix3x4.Identity;

        switch (_lightType)
        {
            case LightType.Directional:
                ArgumentNullException.ThrowIfNull(camera);
                _volumeTransform = GetFullscreenQuadTransform(camera);
                break;

            case LightType.Spot:
            {
                var yScale = MathF.Tan(_fov * MathDefs.DegToRad * 0.5f) * _range;
                var xScale = _aspectRatio * yScale;
                _volumeTransform = new Matrix3x4(
                    Node.WorldPosition,
                    Node.WorldRotation,
                    new Vector3f(xScale, yScale, _range));
                break;
            }

            case LightType.Point:
                _volumeTransform = new Matrix3x4(Node.WorldPosition, Quaternion.IDENTITY, _range);
                break;
        }

        return _volumeTransform;
    }

    public static Matrix3x4 GetFullscreenQuadTransform(Camera camera)
    {
        ArgumentNullException.ThrowIfNull(camera);
        camera.GetFrustumSize(out _, out var farSize);
        var quadTransform = Matrix3x4.Identity;
        quadTransform.SetTranslation(new Vector3f(0.0f, 0.0f, (camera.GetNearClip() + camera.GetFarClip()) * 0.5f));
        quadTransform.SetScale(new Vector3f(farSize.X, farSize.Y, 1.0f));
        return camera.GetEffectiveWorldTransform() * quadTransform;
    }

    public void SetRampTextureAttr(ResourceRef value)
    {
        var cache = GetSubsystem<ResourceCache>()!;
        _rampTexture = string.IsNullOrEmpty(value.Name) ? null : cache.GetResource(value.Type, value.Name) as Texture;
    }

    public ResourceRef GetRampTextureAttr() => ResourceType.GetResourceRef(_rampTexture, new StringHash("Texture2D"));

    public void SetShapeTextureAttr(ResourceRef value)
    {
        var cache = GetSubsystem<ResourceCache>()!;
        _shapeTexture = string.IsNullOrEmpty(value.Name) ? null : cache.GetResource(value.Type, value.Name) as Texture;
    }

    public ResourceRef GetShapeTextureAttr()
    {
        if (_shapeTexture is not null)
        {
            var textureType = _lightType == LightType.Point ? "TextureCube" : "Texture2D";
            return ResourceType.GetResourceRef(_shapeTexture, new StringHash(textureType));
        }

        return new ResourceRef(new StringHash("Texture"), string.Empty);
    }

    public float GetDistanceTo(Drawable drawable)
    {
        ArgumentNullException.ThrowIfNull(drawable);
        return _lightType == LightType.Directional || TryGetNode() is null ? 0.0f : drawable.GetWorldBoundingBox().DistanceToPoint(Node.WorldPosition);
    }

    public int GetNumShadowSplits()
    {
        var splits = ShadowCascade.Splits;
        var count = 1;
        if (splits.Y > splits.X)
        {
            ++count;
            if (splits.Z > splits.Y)
            {
                ++count;
                if (splits.W > splits.Z)
                    ++count;
            }
        }

        return SystemMath.Min(count, (int)MaxCascadeSplits);
    }

    public void SetLightQueue(LightBatchQueue? queue)
    {
        _lightQueue = queue;
    }

    public LightBatchQueue? GetLightQueue() => _lightQueue;

    public bool IsNegative() => GetEffectiveColor().SumRGB() < 0.0f;

    /// <summary>Set sort value based on intensity and view distance.</summary>
    public void SetIntensitySortValue(float distance)
    {
        if (!IsNegative())
        {
            if (_lightType != LightType.Directional)
                SetSortValue(SystemMath.Max(distance, MathDefs.MinNearClip) / GetIntensityDivisor());
            else
                SetSortValue(MathDefs.Epsilon / GetIntensityDivisor());
        }
        else
        {
            if (_lightType != LightType.Directional)
                SetSortValue(-SystemMath.Max(distance, MathDefs.MinNearClip) * GetIntensityDivisor());
            else
                SetSortValue(-MathDefs.LargeValue * GetIntensityDivisor());
        }
    }

    public void SetIntensitySortValue(BoundingBox box)
    {
        // When sorting lights for object's maximum light cap, give priority based on attenuation and intensity
        switch (_lightType)
        {
            case LightType.Directional:
                SetSortValue(1.0f / GetIntensityDivisor());
                break;

            case LightType.Spot:
            {
                var centerPosition = box.Center;
                var lightPosition = Node.WorldPosition;
                var lightDirection = Node.WorldDirection;
                var lightRay = new Ray(lightPosition, lightDirection);

                var centerProjection = lightRay.Project(centerPosition);
                var centerDistance = (centerProjection - lightPosition).Length;
                var centerRay = new Ray(centerProjection, centerPosition - centerProjection);
                var centerAngle = centerRay.HitDistance(box) / centerDistance;

                var cornerPosition = centerPosition + box.HalfSize * new Vector3f(
                    centerPosition.X < centerProjection.X ? 1.0f : -1.0f,
                    centerPosition.Y < centerProjection.Y ? 1.0f : -1.0f,
                    centerPosition.Z < centerProjection.Z ? 1.0f : -1.0f);
                var cornerProjection = lightRay.Project(cornerPosition);
                var cornerDistance = (cornerProjection - lightPosition).Length;
                var cornerAngle = (cornerPosition - cornerProjection).Length / cornerDistance;

                var spotAngle = SystemMath.Min(centerAngle, cornerAngle);
                var maxAngle = MathF.Tan(_fov * MathDefs.DegToRad * 0.5f);
                var spotFactor = SystemMath.Min(spotAngle / maxAngle, 1.0f);
                // We do not know the actual range attenuation ramp, so take only spot attenuation into account
                var attenuation = SystemMath.Max(1.0f - spotFactor * spotFactor, MathDefs.Epsilon);
                SetSortValue(1.0f / GetIntensityDivisor(attenuation));
                break;
            }

            case LightType.Point:
            {
                var centerPosition = box.Center;
                var lightPosition = Node.WorldPosition;
                var lightDirection = (centerPosition - lightPosition).Normalized;
                var lightRay = new Ray(lightPosition, lightDirection);
                var distance = lightRay.HitDistance(box);
                var normalizedDistance = distance / _range;
                var attenuation = SystemMath.Max(1.0f - normalizedDistance * normalizedDistance, MathDefs.Epsilon);
                SetSortValue(1.0f / GetIntensityDivisor(attenuation));
                break;
            }
        }
    }

    /// <summary>Return a divisor value based on intensity for calculating the sort value.</summary>
    public float GetIntensityDivisor(float attenuation = 1.0f)
    {
        return SystemMath.Max(GetEffectiveColor().SumRGB(), 0.0f) * attenuation + MathDefs.Epsilon;
    }

    public (float Cutoff, float InverseCutoff) GetCutoffParams()
    {
        if (_lightType != LightType.Spot)
            return (-2.0f, 1.0f);

        var cutoff = MathDefs.Cos(_fov * 0.5f);
        return (cutoff, 1.0f / (1.0f - cutoff));
    }

    /// <summary>Process octree raycast.</summary>
    public override void ProcessRayQuery(RayOctreeQuery query, List<RayQueryResult> results)
    {
        if (_lightType == LightType.Directional)
            return;

        var distance = query.MaxDistance;
        switch (query.Level)
        {
            case RayQueryLevel.RayAabb:
                base.ProcessRayQuery(query, results);
                return;
            case RayQueryLevel.RayObb:
            {
                var inverse = Node.GetWorldTransform().Inverse;
                var localRay = query.Ray.Transformed(inverse);
                distance = localRay.HitDistance(GetWorldBoundingBox().Transformed(inverse));
                break;
            }
            case RayQueryLevel.RayTriangle:
                distance = _lightType == LightType.Spot
                    ? query.Ray.HitDistance(GetFrustum())
                    : query.Ray.HitDistance(new Sphere(Node.WorldPosition, _range));
                break;
            case RayQueryLevel.RayTriangleUv:
                Log.GetLogger().LogWarning("RAY_TRIANGLE_UV query level is not supported for Light component");
                return;
        }

        if (float.IsPositiveInfinity(distance) || distance >= query.MaxDistance)
            return;

        results.Add(new RayQueryResult
        {
            Position = query.Ray.Origin + query.Ray.Direction * distance,
            Normal = -query.Ray.Direction,
            Distance = distance,
            Drawable = this,
            Node = Node,
            SubObject = uint.MaxValue
        });
    }

    protected override void OnWorldBoundingBoxUpdate()
    {
        switch (_lightType)
        {
            case LightType.Directional:
                SetWorldBoundingBox(new BoundingBox(-MathDefs.LargeValue, MathDefs.LargeValue));
                break;

            case LightType.Spot:
                GetFrustum(_worldBoundingFrustum);
                SetWorldBoundingBox(new BoundingBox(_worldBoundingFrustum.Vertices));
                break;

            case LightType.Point:
                var center = Node.WorldPosition;
                var edge = new Vector3f(_range, _range, _range);
                SetWorldBoundingBox(new BoundingBox(center - edge, center + edge));
                break;
        }
    }

    private static object? GetIsEnabledAttributeDefault() => true;

    private static object? GetLightTypeAttributeDefault() => DefaultLightType;

    private static object? GetLightImportanceAttributeDefault() => DefaultLightImportance;

    private static object? GetLightModeAttributeDefault() => LightMode.Realtime;

    private static object? GetColorAttributeDefault() => Color.White;

    private static object? GetSpecularIntensityAttributeDefault() => DefaultSpecularIntensity;

    private static object? GetBrightnessMultiplierAttributeDefault() => DefaultBrightness;

    private static object? GetIndirectBrightnessAttributeDefault() => 1.0f;

    private static object? GetTemperatureAttributeDefault() => DefaultTemperature;

    private static object? GetUsePhysicalValuesAttributeDefault() => false;

    private static object? GetRadiusAttributeDefault() => 0.0f;

    private static object? GetLengthAttributeDefault() => 0.0f;

    private static object? GetRangeAttributeDefault() => DefaultRange;

    private static object? GetSpotFOVAttributeDefault() => DefaultLightFov;

    private static object? GetSpotAspectRatioAttributeDefault() => 1.0f;

    private static object? GetAttenuationTextureAttributeDefault() => new ResourceRef(new StringHash(nameof(Texture2D)));

    private static object? GetLightShapeTextureAttributeDefault() => new ResourceRef(new StringHash(nameof(Texture)));

    private static object? GetCanBeOccludedAttributeDefault() => true;

    private static object? GetCastShadowsAttributeDefault() => false;

    private static object? GetDrawDistanceAttributeDefault() => 0.0f;

    private static object? GetFadeDistanceAttributeDefault() => 0.0f;

    private static object? GetShadowDistanceAttributeDefault() => 0.0f;

    private static object? GetShadowFadeDistanceAttributeDefault() => 0.0f;

    private static object? GetShadowIntensityAttributeDefault() => 0.0f;

    private static object? GetShadowResolutionAttributeDefault() => 1.0f;

    private static object? GetFocusToSceneAttributeDefault() => true;

    private object? GetSerializedFocusToScene()
    {
        Serializable self = this;
        return ((Light)self).GetShadowFocus().Focus;
    }

    private void SetSerializedFocusToScene(object? value)
    {
        Serializable self = this;
        var light = (Light)self;
        var parameters = light.GetShadowFocus();
        parameters.Focus = value is bool enabled && enabled;
        light.SetShadowFocus(parameters);
    }

    private static object? GetNonUniformViewAttributeDefault() => true;

    private object? GetSerializedNonUniformView()
    {
        Serializable self = this;
        return ((Light)self).GetShadowFocus().NonUniform;
    }

    private void SetSerializedNonUniformView(object? value)
    {
        Serializable self = this;
        var light = (Light)self;
        var parameters = light.GetShadowFocus();
        parameters.NonUniform = value is bool enabled && enabled;
        light.SetShadowFocus(parameters);
    }

    private static object? GetAutoReduceSizeAttributeDefault() => true;

    private object? GetSerializedAutoReduceSize()
    {
        Serializable self = this;
        return ((Light)self).GetShadowFocus().AutoSize;
    }

    private void SetSerializedAutoReduceSize(object? value)
    {
        Serializable self = this;
        var light = (Light)self;
        var parameters = light.GetShadowFocus();
        parameters.AutoSize = value is bool enabled && enabled;
        light.SetShadowFocus(parameters);
    }

    private static object? GetCSMSplitsAttributeDefault() => new Vector4f(DefaultShadowSplit, 0.0f, 0.0f, 0.0f);

    private object? GetSerializedCSMSplits()
    {
        Serializable self = this;
        return ((Light)self).GetShadowCascade().Splits;
    }

    private void SetSerializedCSMSplits(object? value)
    {
        Serializable self = this;
        var light = (Light)self;
        var parameters = light.GetShadowCascade();
        parameters.Splits = (Vector4f)value!;
        light.SetShadowCascade(parameters);
    }

    private static object? GetCSMFadeStartAttributeDefault() => DefaultShadowFadeStart;

    private object? GetSerializedCSMFadeStart()
    {
        Serializable self = this;
        return ((Light)self).GetShadowCascade().FadeStart;
    }

    private void SetSerializedCSMFadeStart(object? value)
    {
        Serializable self = this;
        var light = (Light)self;
        var parameters = light.GetShadowCascade();
        parameters.FadeStart = (float)value!;
        light.SetShadowCascade(parameters);
    }

    private static object? GetCSMBiasAutoAdjustAttributeDefault() => DefaultBiasAutoAdjust;

    private object? GetSerializedCSMBiasAutoAdjust()
    {
        Serializable self = this;
        return ((Light)self).GetShadowCascade().BiasAutoAdjust;
    }

    private void SetSerializedCSMBiasAutoAdjust(object? value)
    {
        Serializable self = this;
        var light = (Light)self;
        var parameters = light.GetShadowCascade();
        parameters.BiasAutoAdjust = (float)value!;
        light.SetShadowCascade(parameters);
    }

    private static object? GetViewSizeQuantizeAttributeDefault() => DefaultShadowQuantize;

    private object? GetSerializedViewSizeQuantize()
    {
        Serializable self = this;
        return ((Light)self).GetShadowFocus().Quantize;
    }

    private void SetSerializedViewSizeQuantize(object? value)
    {
        Serializable self = this;
        var light = (Light)self;
        var parameters = light.GetShadowFocus();
        parameters.Quantize = (float)value!;
        light.SetShadowFocus(parameters);
    }

    private static object? GetViewSizeMinimumAttributeDefault() => DefaultShadowMinView;

    private object? GetSerializedViewSizeMinimum()
    {
        Serializable self = this;
        return ((Light)self).GetShadowFocus().MinView;
    }

    private void SetSerializedViewSizeMinimum(object? value)
    {
        Serializable self = this;
        var light = (Light)self;
        var parameters = light.GetShadowFocus();
        parameters.MinView = (float)value!;
        light.SetShadowFocus(parameters);
    }

    private static object? GetDepthConstantBiasAttributeDefault() => DefaultConstantBias;

    private object? GetSerializedDepthConstantBias()
    {
        Serializable self = this;
        return ((Light)self).GetShadowBias().ConstantBias;
    }

    private void SetSerializedDepthConstantBias(object? value)
    {
        Serializable self = this;
        var light = (Light)self;
        var parameters = light.GetShadowBias();
        parameters.ConstantBias = (float)value!;
        light.SetShadowBias(parameters);
    }

    private static object? GetDepthSlopeBiasAttributeDefault() => DefaultSlopeScaledBias;

    private object? GetSerializedDepthSlopeBias()
    {
        Serializable self = this;
        return ((Light)self).GetShadowBias().SlopeScaledBias;
    }

    private void SetSerializedDepthSlopeBias(object? value)
    {
        Serializable self = this;
        var light = (Light)self;
        var parameters = light.GetShadowBias();
        parameters.SlopeScaledBias = (float)value!;
        light.SetShadowBias(parameters);
    }

    private static object? GetNormalOffsetAttributeDefault() => DefaultNormalOffset;

    private object? GetSerializedNormalOffset()
    {
        Serializable self = this;
        return ((Light)self).GetShadowBias().NormalOffset;
    }

    private void SetSerializedNormalOffset(object? value)
    {
        Serializable self = this;
        var light = (Light)self;
        var parameters = light.GetShadowBias();
        parameters.NormalOffset = (float)value!;
        light.SetShadowBias(parameters);
    }

    private static object? GetNearFarclipRatioAttributeDefault() => DefaultShadowNearFarRatio;

    private static object? GetMaxExtrusionAttributeDefault() => DefaultShadowMaxExtrusion;

    private static object? GetViewMaskAttributeDefault() => DefaultViewMask;

    private static object? GetLightMaskAttributeDefault() => DefaultLightMask;
}
