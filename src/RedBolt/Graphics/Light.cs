namespace RedBolt;

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

public readonly record struct CascadeParameters(Vector4 Splits, float FadeStart, float BiasAutoAdjust = 1.0f)
{
    public CascadeParameters(float split1, float split2, float split3, float split4, float fadeStart, float biasAutoAdjust = 1.0f)
        : this(new Vector4(split1, split2, split3, split4), fadeStart, biasAutoAdjust)
    {
    }

    public float GetShadowRange() => Math.Max(Math.Max(Splits.X, Splits.Y), Math.Max(Splits.Z, Splits.W));

    public CascadeParameters Validated() => new(
        new Vector4(Math.Max(Splits.X, 0.0f), Math.Max(Splits.Y, 0.0f), Math.Max(Splits.Z, 0.0f), Math.Max(Splits.W, 0.0f)),
        MathDefs.Clamp(FadeStart, MathDefs.Epsilon, 1.0f),
        BiasAutoAdjust);
}

public readonly record struct FocusParameters(bool Focus, bool NonUniform, bool AutoSize, float Quantize, float MinView)
{
    public FocusParameters Validated() => this with
    {
        Quantize = Math.Max(Quantize, Light.ShadowMinQuantize),
        MinView = Math.Max(MinView, Light.ShadowMinView)
    };
}

public sealed class Light : Drawable
{
    public const float ShadowMinQuantize = 0.1f;
    public const float ShadowMinView = 1.0f;
    public const int MaxLightSplits = 6;
    public const uint MaxCascadeSplits = 4;
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

    private LightType lightType = LightType.Point;
    private float range = DefaultRange;
    private float fov = DefaultLightFov;
    private float aspectRatio = 1.0f;
    private Texture? rampTexture;
    private Texture? shapeTexture;

    public Light()
        : base(DrawableFlags.Light)
    {
        RefreshBoundingBox();
    }

    public LightType LightType => lightType;

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

    public float Range => range;

    public float Fov => fov;

    public float AspectRatio => aspectRatio;

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
        lightType = type;
        RefreshBoundingBox();
    }

    public LightType GetLightType() => lightType;

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
        Color = color;
    }

    public Color GetColor() => Color;

    public void SetTemperature(float temperature)
    {
        Temperature = temperature;
    }

    public float GetTemperature() => Temperature;

    public void SetRadius(float radius)
    {
        Radius = Math.Max(radius, 0.0f);
    }

    public float GetRadius() => Radius;

    public void SetLength(float length)
    {
        Length = Math.Max(length, 0.0f);
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
        SpecularIntensity = Math.Max(intensity, 0.0f);
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

    public float GetEffectiveSpecularIntensity() => SpecularIntensity * Math.Abs(Brightness);

    public void SetRange(float range)
    {
        this.range = Math.Max(range, 0.0f);
        RefreshBoundingBox();
    }

    public float GetRange() => range;

    public void SetFov(float fov)
    {
        this.fov = MathDefs.Clamp(fov, 0.0f, MathDefs.MaxFov);
        MarkForUpdate();
    }

    public float GetFov() => fov;

    public void SetAspectRatio(float aspectRatio)
    {
        this.aspectRatio = Math.Max(aspectRatio, MathDefs.Epsilon);
        MarkForUpdate();
    }

    public float GetAspectRatio() => aspectRatio;

    public void SetFadeDistance(float distance)
    {
        FadeDistance = Math.Max(distance, 0.0f);
    }

    public float GetFadeDistance() => FadeDistance;

    public void SetShadowFadeDistance(float distance)
    {
        ShadowFadeDistance = Math.Max(distance, 0.0f);
    }

    public float GetShadowFadeDistance() => ShadowFadeDistance;

    public void SetShadowBias(BiasParameters parameters)
    {
        ShadowBias = new BiasParameters(
            MathDefs.Clamp(parameters.ConstantBias, -1.0f, 1.0f),
            MathDefs.Clamp(parameters.SlopeScaledBias, -16.0f, 16.0f),
            Math.Max(parameters.NormalOffset, 0.0f));
    }

    public BiasParameters GetShadowBias() => ShadowBias;

    public void SetShadowCascade(CascadeParameters parameters)
    {
        ShadowCascade = parameters.Validated();
    }

    public CascadeParameters GetShadowCascade() => ShadowCascade;

    public void SetShadowFocus(FocusParameters parameters)
    {
        ShadowFocus = parameters.Validated();
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
        ShadowMaxExtrusion = Math.Max(extrusion, 0.0f);
    }

    public float GetShadowMaxExtrusion() => ShadowMaxExtrusion;

    public void SetRampTexture(Texture? texture)
    {
        rampTexture = texture;
    }

    public Texture? GetRampTexture() => rampTexture;

    public void SetShapeTexture(Texture? texture)
    {
        shapeTexture = texture;
    }

    public Texture? GetShapeTexture() => shapeTexture;

    public float GetDistanceTo(Drawable drawable)
    {
        ArgumentNullException.ThrowIfNull(drawable);
        return lightType == LightType.Directional || !IsAttached ? 0.0f : drawable.GetWorldBoundingBox().DistanceToPoint(Node.WorldPosition);
    }

    public int GetNumShadowSplits()
    {
        var splits = ShadowCascade.Splits;
        var count = 0;
        if (splits.X > 0.0f)
            ++count;
        if (splits.Y > 0.0f)
            ++count;
        if (splits.Z > 0.0f)
            ++count;
        if (splits.W > 0.0f)
            ++count;
        return count;
    }

    public bool IsNegative() => GetEffectiveColor().SumRGB() < 0.0f;

    public (float Cutoff, float InverseCutoff) GetCutoffParams()
    {
        if (lightType != LightType.Spot)
            return (-2.0f, 1.0f);

        var cutoff = MathDefs.Cos(fov * 0.5f);
        return (cutoff, 1.0f / (1.0f - cutoff));
    }

    private void RefreshBoundingBox()
    {
        var size = lightType == LightType.Directional ? MathDefs.LargeValue : range;
        SetLocalBoundingBox(new BoundingBox(-size, size));
    }
}
