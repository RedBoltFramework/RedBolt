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

public readonly record struct CascadeParameters(Vector4f Splits, float FadeStart, float BiasAutoAdjust = 1.0f)
{
    public CascadeParameters(float split1, float split2, float split3, float split4, float fadeStart, float biasAutoAdjust = 1.0f)
        : this(new Vector4f(split1, split2, split3, split4), fadeStart, biasAutoAdjust)
    {
    }

    public float GetShadowRange() => Math.Max(Math.Max(Splits.X, Splits.Y), Math.Max(Splits.Z, Splits.W));

    public CascadeParameters Validated() => new(
        new Vector4f(Math.Max(Splits.X, 0.0f), Math.Max(Splits.Y, 0.0f), Math.Max(Splits.Z, 0.0f), Math.Max(Splits.W, 0.0f)),
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

    private LightType _lightType = LightType.Point;
    private float _range = DefaultRange;
    private float _fov = DefaultLightFov;
    private float _aspectRatio = 1.0f;
    private Texture? _rampTexture;
    private Texture? _shapeTexture;
    private Matrix3x4 _volumeTransform = Matrix3x4.Identity;

    public Light()
        : base(DrawableFlags.Light)
    {
        RefreshBoundingBox();
    }

    public void DrawDebugGeometry(DebugRenderer? debug, bool depthTest)
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

    // TODO: port: void UpdateBatches(const FrameInfo& frame) override;
    // The managed render pipeline does not expose the legacy FrameInfo camera contract yet.

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
        RefreshBoundingBox();
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
        this._range = Math.Max(range, 0.0f);
        RefreshBoundingBox();
    }

    public float GetRange() => _range;

    public void SetFov(float fov)
    {
        this._fov = MathDefs.Clamp(fov, 0.0f, MathDefs.MaxFov);
        MarkForUpdate();
    }

    public float GetFov() => _fov;

    public void SetAspectRatio(float aspectRatio)
    {
        this._aspectRatio = Math.Max(aspectRatio, MathDefs.Epsilon);
        MarkForUpdate();
    }

    public float GetAspectRatio() => _aspectRatio;

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
        var transform = IsAttached
            ? new Matrix3x4(Node.WorldPosition, Node.WorldRotation, 1.0f)
            : Matrix3x4.Identity;
        var frustum = new Frustum();
        frustum.Define(_fov, _aspectRatio, 1.0f, MathDefs.MinNearClip, _range, transform);
        return frustum;
    }

    /// <summary>Return spotlight frustum in the specified view space.</summary>
    public Frustum GetViewSpaceFrustum(Matrix3x4 view)
    {
        var transform = IsAttached
            ? new Matrix3x4(Node.WorldPosition, Node.WorldRotation, 1.0f)
            : Matrix3x4.Identity;
        var frustum = new Frustum();
        frustum.Define(_fov, _aspectRatio, 1.0f, MathDefs.MinNearClip, _range, view * transform);
        return frustum;
    }

    public Matrix3x4 GetVolumeTransform(Camera? camera)
    {
        if (!IsAttached)
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
                _volumeTransform = new Matrix3x4(Node.WorldPosition, Quaternion.Identity, _range);
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
        var cache = GetSubsystem<ResourceCache>();
        _rampTexture = string.IsNullOrEmpty(value.Name) ? null : cache?.GetResourceOrNull<Texture>(value.Name);
    }

    public ResourceRef GetRampTextureAttr() => Resource.GetResourceRef(_rampTexture, new StringHash("Texture2D"));

    public void SetShapeTextureAttr(ResourceRef value)
    {
        var cache = GetSubsystem<ResourceCache>();
        _shapeTexture = string.IsNullOrEmpty(value.Name) ? null : cache?.GetResourceOrNull<Texture>(value.Name);
    }

    public ResourceRef GetShapeTextureAttr()
    {
        if (_shapeTexture is not null)
        {
            var textureType = _lightType == LightType.Point ? "TextureCube" : "Texture2D";
            return Resource.GetResourceRef(_shapeTexture, new StringHash(textureType));
        }

        return new ResourceRef(new StringHash("Texture"), string.Empty);
    }

    public float GetDistanceTo(Drawable drawable)
    {
        ArgumentNullException.ThrowIfNull(drawable);
        return _lightType == LightType.Directional || !IsAttached ? 0.0f : drawable.GetWorldBoundingBox().DistanceToPoint(Node.WorldPosition);
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

        return Math.Min(count, (int)MaxCascadeSplits);
    }

    public bool IsNegative() => GetEffectiveColor().SumRGB() < 0.0f;

    /// <summary>Set sort value based on intensity and view distance.</summary>
    public void SetIntensitySortValue(float distance)
    {
        if (!IsNegative())
        {
            if (_lightType != LightType.Directional)
                SetSortValue(Math.Max(distance, MathDefs.MinNearClip) / GetIntensityDivisor());
            else
                SetSortValue(MathDefs.Epsilon / GetIntensityDivisor());
        }
        else
        {
            if (_lightType != LightType.Directional)
                SetSortValue(-Math.Max(distance, MathDefs.MinNearClip) * GetIntensityDivisor());
            else
                SetSortValue(-MathDefs.LargeValue * GetIntensityDivisor());
        }
    }

    /// <summary>Return a divisor value based on intensity for calculating the sort value.</summary>
    public float GetIntensityDivisor(float attenuation = 1.0f)
    {
        return Math.Max(GetEffectiveColor().SumRGB(), 0.0f) * attenuation + MathDefs.Epsilon;
    }

    public (float Cutoff, float InverseCutoff) GetCutoffParams()
    {
        if (_lightType != LightType.Spot)
            return (-2.0f, 1.0f);

        var cutoff = MathDefs.Cos(_fov * 0.5f);
        return (cutoff, 1.0f / (1.0f - cutoff));
    }

    /// <summary>Process octree raycast.</summary>
    public void ProcessRayQuery(RayOctreeQuery query)
    {
        if (_lightType == LightType.Directional)
            return;

        var distance = query.MaxDistance;
        switch (query.Level)
        {
            case RayQueryLevel.RayAabb:
                distance = query.Ray.HitDistance(GetWorldBoundingBox());
                break;
            case RayQueryLevel.RayObb:
                distance = HitDistanceObb(query.Ray);
                break;
            case RayQueryLevel.RayTriangle:
                distance = _lightType == LightType.Spot
                    ? query.Ray.HitDistance(GetFrustum())
                    : query.Ray.HitDistance(new Sphere(Node.WorldPosition, _range));
                break;
            case RayQueryLevel.RayTriangleUv:
                return;
        }

        if (float.IsPositiveInfinity(distance) || distance >= query.MaxDistance)
            return;

        query.Result.Add(new RayQueryResult
        {
            Position = query.Ray.Origin + query.Ray.Direction * distance,
            Normal = -query.Ray.Direction,
            Distance = distance,
            Drawable = this,
            Node = IsAttached ? Node : null,
            SubObject = uint.MaxValue
        });
    }

    private void RefreshBoundingBox()
    {
        var size = _lightType == LightType.Directional ? MathDefs.LargeValue : _range;
        SetLocalBoundingBox(new BoundingBox(-size, size));
    }

    private float HitDistanceObb(Ray ray)
    {
        if (!IsAttached)
            return ray.HitDistance(BoundingBox);

        var localOrigin = Node.WorldToLocal(ray.Origin);
        var localEnd = Node.WorldToLocal(ray.Origin + ray.Direction);
        var localDirection = localEnd - localOrigin;
        if (localDirection.LengthSquared <= MathDefs.Epsilon * MathDefs.Epsilon)
            return float.PositiveInfinity;

        var localRay = new Ray(localOrigin, localDirection);
        var localDistance = localRay.HitDistance(BoundingBox);
        if (float.IsPositiveInfinity(localDistance))
            return localDistance;

        var localHit = localRay.Origin + localRay.Direction * localDistance;
        return (Node.LocalToWorld(localHit) - ray.Origin).Length;
    }

    protected override void OnWorldBoundingBoxUpdate()
    {
        switch (_lightType)
        {
            case LightType.Directional:
                SetWorldBoundingBox(new BoundingBox(-MathDefs.LargeValue, MathDefs.LargeValue));
                break;

            case LightType.Spot:
                SetWorldBoundingBox(new BoundingBox(GetFrustum().Vertices));
                break;

            case LightType.Point:
                var center = Node.WorldPosition;
                var edge = new Vector3f(_range, _range, _range);
                SetWorldBoundingBox(new BoundingBox(center - edge, center + edge));
                break;
        }
    }
}
