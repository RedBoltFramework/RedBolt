namespace RedBolt;

/// <summary>
/// Cooked light parameters used by shader binding.
/// </summary>
public struct CookedLightParams
{
    /// <summary>Common light parameters.</summary>
    public Vector3f Direction;
    public Vector3f Position;
    public float InverseRange;
    public Vector3f EffectiveColorInGammaSpace;
    public Vector3f EffectiveColorInLinearSpace;
    public float EffectiveSpecularIntensity;

    public float SpotCutoff;
    public float InverseSpotCutoff;

    public float VolumetricRadius;
    public float VolumetricLength;

    /// <summary>Matrix for light shape (aka light cookie).</summary>
    public Matrix4 LightShapeMatrix;

    /// <summary>
    /// Shadow matrices for each split (for directional lights).
    /// Light matrix and shadow matrix (for spot lights).
    /// Light matrix (for point lights).
    /// </summary>
    public Matrix4[] LightMatrices;
    public uint NumLightMatrices;

    /// <summary>Shadow mapping parameters.</summary>
    public Vector4f ShadowCubeAdjust;
    public Vector4f ShadowDepthFade;
    public Vector4f ShadowIntensity;
    public Vector2f ShadowMapInvSize;
    public Vector2f ShadowCubeUVBias;
    public Vector4f ShadowSplitDistances;

    public float[] ShadowNormalBias;
    public float[] ShadowDepthBiasMultiplier;

    public Texture2D? ShadowMap;
    public Texture? LightRamp;
    public Texture? LightShape;

    /// <summary>Return light color in appropriate color space.</summary>
    public readonly Vector3f GetColor(bool isLinear) =>
        isLinear ? EffectiveColorInLinearSpace : EffectiveColorInGammaSpace;
}

/// <summary>
/// Manages light parameters, lit geometries, shadow splits and shadow casters.
/// </summary>
public partial class LightProcessor
{
    /// <summary>Number of frames for shadow splits expiration.</summary>
    public const uint NumSplitFramesToLive = 600;

    private readonly Light _light;
    private readonly List<ShadowSplitProcessor> _splits = [];
    private uint _splitRemainingTimeToLive;
    private readonly RenderBackend _renderBackend;

    /// <summary>Parameters extracted from light settings.</summary>
    private bool _isShadowRequested;
    private uint _numSplitsRequested;

    /// <summary>Processing results.</summary>
    private bool _cameraIsInsideLightVolume;
    private uint _numActiveSplits;
    private int _shadowMapSplitSize;
    private Vector2i _shadowMapSize;
    private bool _hasLitGeometries;
    private bool _hasForwardLitGeometries;

    /// <summary>
    /// Point and spot lights: only forward lit geometries.
    /// Directional lights: all lit geometries, for shadow focusing.
    /// </summary>
    private readonly List<Drawable> _litGeometries = [];

    /// <summary>
    /// Point and spot lights: all possible shadow casters.
    /// Directional lights: temporary buffer for split queries.
    /// </summary>
    private readonly List<Drawable> _shadowCasterCandidates = [];

    /// <summary>Accumulative shadow map region containing all the splits.</summary>
    private ShadowMapRegion _shadowMap;

    private CookedLightParams _cookedParams;

    /// <summary>Pipeline state hashes.</summary>
    private uint _forwardLitBatchHash;
    private uint _lightVolumeBatchHash;
    private readonly uint[] _shadowBatchStateHashes = new uint[Light.MaxLightSplits];

    public LightProcessor(Light light)
    {
        _light = light ?? throw new ArgumentNullException(nameof(light));
        _renderBackend = light.GetSubsystem<Graphics>()?.GetRenderBackend() ?? default;
        _cookedParams.LightShapeMatrix = Matrix4.Identity;
        _cookedParams.LightMatrices = new Matrix4[Light.MaxCascadeSplits];
        for (var i = 0; i < _cookedParams.LightMatrices.Length; ++i)
            _cookedParams.LightMatrices[i] = Matrix4.Identity;
        _cookedParams.ShadowNormalBias = new float[Light.MaxLightSplits];
        _cookedParams.ShadowDepthBiasMultiplier = new float[Light.MaxLightSplits];
        for (var i = 0; i < _cookedParams.ShadowDepthBiasMultiplier.Length; ++i)
            _cookedParams.ShadowDepthBiasMultiplier[i] = 1.0f;
    }

    public void Dispose()
    {
        _splits.Clear();
        _litGeometries.Clear();
        _shadowCasterCandidates.Clear();
    }

    /// <summary>Begin update from main thread.</summary>
    public void BeginUpdate(DrawableProcessor drawableProcessor, LightProcessorCallback callback)
    {
        ArgumentNullException.ThrowIfNull(drawableProcessor);
        ArgumentNullException.ThrowIfNull(callback);

        // Clear temporary containers
        _litGeometries.Clear();
        _shadowCasterCandidates.Clear();
        _shadowMap = default;

        // Initialize shadow
        _isShadowRequested = callback.IsLightShadowed(_light);
        _numSplitsRequested = _isShadowRequested ? CalculateNumSplits(_light) : 0;

        // Update splits
        if ((uint)_splits.Count <= _numSplitsRequested)
        {
            // Allocate splits and reset timer immediately
            _splitRemainingTimeToLive = NumSplitFramesToLive;
            while ((uint)_splits.Count < _numSplitsRequested)
                _splits.Add(new ShadowSplitProcessor(this, (uint)_splits.Count));
        }
        else
        {
            // Deallocate splits by timeout
            --_splitRemainingTimeToLive;
            if (_splitRemainingTimeToLive == 0)
            {
                while ((uint)_splits.Count > _numSplitsRequested)
                    _splits.RemoveAt(_splits.Count - 1);
            }
        }
    }

    /// <summary>Update light in worker thread.</summary>
    public void Update(DrawableProcessor drawableProcessor, LightProcessorCallback callback)
    {
        ArgumentNullException.ThrowIfNull(drawableProcessor);
        ArgumentNullException.ThrowIfNull(callback);

        var frameInfo = drawableProcessor.GetFrameInfo();
        var octree = frameInfo.Octree;
        var cullCamera = frameInfo.Camera;
        if (octree is null || cullCamera is null)
            return;

        var lightType = _light.GetLightType();

        // Check if light volume contains camera
        _cameraIsInsideLightVolume = EstimateDistanceToCamera(cullCamera, _light) <= cullCamera.GetNearClip() * 2.0f;

        // Query lit geometries (and shadow casters for spot and point lights)
        switch (lightType)
        {
            case LightType.Spot:
            {
                var query = new SpotLightGeometryQuery(_litGeometries,
                    _isShadowRequested ? _shadowCasterCandidates : null,
                    drawableProcessor, _light, cullCamera.GetPrimaryViewMask(), cullCamera.GetShadowViewMask());
                octree.GetDrawables(query);
                _hasLitGeometries = query.HasLitGeometries;
                _hasForwardLitGeometries = _litGeometries.Count > 0;
                break;
            }
            case LightType.Point:
            {
                var query = new PointLightGeometryQuery(_litGeometries,
                    _isShadowRequested ? _shadowCasterCandidates : null,
                    drawableProcessor, _light, cullCamera.GetPrimaryViewMask(), cullCamera.GetShadowViewMask());
                octree.GetDrawables(query);
                _hasLitGeometries = query.HasLitGeometries;
                _hasForwardLitGeometries = _litGeometries.Count > 0;
                break;
            }
            case LightType.Directional:
            {
                _cameraIsInsideLightVolume = true;
                _hasLitGeometries = false;
                _hasForwardLitGeometries = false;
                var lightMask = _light.GetLightMask();
                foreach (var drawable in drawableProcessor.GetGeometries())
                {
                    if (drawable is null)
                        continue;
                    var drawableIndex = drawable.GetDrawableIndex();
                    var flags = drawableProcessor.GetGeometryRenderFlags(drawableIndex);
                    var isLit = (flags & GeometryRenderFlag.Lit) != 0;
                    var isForwardLit = (flags & GeometryRenderFlag.ForwardLit) != 0;

                    _hasLitGeometries = _hasLitGeometries || isLit;
                    _hasForwardLitGeometries = _hasForwardLitGeometries || isForwardLit;

                    if (isLit && (drawable.GetLightMaskInZone() & lightMask) != 0)
                        _litGeometries.Add(drawable);
                }

                break;
            }
        }

        // Update shadows
        if (!_isShadowRequested)
        {
            _numActiveSplits = 0;
            return;
        }

        InitializeShadowSplits(drawableProcessor);

        for (var i = 0u; i < _numActiveSplits; ++i)
        {
            switch (lightType)
            {
                case LightType.Spot:
                    _splits[(int)i].ProcessSpotShadowCasters(drawableProcessor, _shadowCasterCandidates);
                    break;
                case LightType.Point:
                    _splits[(int)i].ProcessPointShadowCasters(drawableProcessor, _shadowCasterCandidates);
                    break;
                case LightType.Directional:
                    _splits[(int)i].ProcessDirectionalShadowCasters(drawableProcessor, _shadowCasterCandidates);
                    break;
            }
        }

        var hasShadowCaster = false;
        for (var i = 0; i < (int)_numActiveSplits; ++i)
        {
            if (_splits[i].HasShadowCasters())
            {
                hasShadowCaster = true;
                break;
            }
        }

        if (!hasShadowCaster)
        {
            _numActiveSplits = 0;
            return;
        }

        // Evaluate split shadow map size
        _shadowMapSplitSize = (int)callback.GetShadowMapSize(_light, _numActiveSplits);
        var grid = GetNumSplitsInGrid();
        _shadowMapSize = new Vector2i(_shadowMapSplitSize * grid.X, _shadowMapSplitSize * grid.Y);
    }

    /// <summary>End update from main thread.</summary>
    public void EndUpdate(DrawableProcessor drawableProcessor, LightProcessorCallback callback, uint pcfKernelSize)
    {
        ArgumentNullException.ThrowIfNull(drawableProcessor);
        ArgumentNullException.ThrowIfNull(callback);

        // Allocate shadow map
        if (_numActiveSplits > 0)
        {
            _shadowMap = callback.AllocateTransientShadowMap(_shadowMapSize);
            if (_shadowMap.IsEmpty)
                _numActiveSplits = 0;
            else
            {
                var grid = GetNumSplitsInGrid();
                for (var i = 0u; i < _numActiveSplits; ++i)
                    _splits[(int)i].FinalizeShadow(_shadowMap.GetSplit(i, grid), pcfKernelSize);
            }
        }

        var cullCamera = drawableProcessor.GetFrameInfo().Camera;
        if (cullCamera is not null)
            CookShaderParameters(cullCamera, drawableProcessor.GetSettings());
        UpdateHashes();
    }

    /// <summary>Return pipeline state hashes.</summary>
    public uint GetForwardLitHash() => _forwardLitBatchHash;

    public uint GetShadowHash(uint splitIndex) =>
        splitIndex < (uint)_shadowBatchStateHashes.Length ? _shadowBatchStateHashes[splitIndex] : 0;

    public uint GetLightVolumeHash() => _lightVolumeBatchHash;

    /// <summary>Return values are always valid.</summary>
    public Light GetLight() => _light;

    /// <summary>Return values are valid after threaded update.</summary>
    public IReadOnlyList<Drawable> GetLitGeometries() => _litGeometries;

    public bool HasForwardLitGeometries() => _hasForwardLitGeometries;

    public bool HasLitGeometries() => _hasLitGeometries;

    public bool DoesOverlapCamera() => _cameraIsInsideLightVolume;

    public bool HasShadow() => _numActiveSplits != 0;

    public Vector2i GetShadowMapSize() => _numActiveSplits != 0 ? _shadowMapSize : Vector2i.Zero;

    public uint GetNumSplits() => _numActiveSplits;

    /// <summary>Return values are valid after update is finished.</summary>
    public ShadowSplitProcessor GetSplit(uint splitIndex) => _splits[(int)splitIndex];

    public ShadowSplitProcessor GetMutableSplit(uint splitIndex) => _splits[(int)splitIndex];

    public ReadOnlySpan<ShadowSplitProcessor> GetSplits() =>
        System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_splits)[..(int)_numActiveSplits];

    public Span<ShadowSplitProcessor> GetMutableSplits() =>
        System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_splits)[..(int)_numActiveSplits];

    public ShadowMapRegion GetShadowMap() => _shadowMap;

    public ref readonly CookedLightParams GetParams() => ref _cookedParams;

    private void InitializeShadowSplits(DrawableProcessor drawableProcessor)
    {
        /// Setup splits
        switch (_light.GetLightType())
        {
            case LightType.Directional:
            {
                var frameInfo = drawableProcessor.GetFrameInfo();
                var cullCamera = frameInfo.Camera!;
                var activeSplits = GetActiveSplits(_light, cullCamera.GetNearClip(), cullCamera.GetFarClip());

                _numActiveSplits = (uint)activeSplits.Count;
                for (var i = 0; i < activeSplits.Count; ++i)
                    _splits[i].InitializeDirectional(drawableProcessor, activeSplits[i], _litGeometries);
                break;
            }
            case LightType.Spot:
            {
                _numActiveSplits = 1;
                _splits[0].InitializeSpot();
                break;
            }
            case LightType.Point:
            {
                _numActiveSplits = (uint)CubeMapFace.MaxCubeMapFaces;
                for (var i = 0; i < (int)CubeMapFace.MaxCubeMapFaces; ++i)
                    _splits[i].InitializePoint((CubeMapFace)i);
                break;
            }
        }
    }

    private void CookShaderParameters(Camera cullCamera, DrawableProcessorSettings settings)
    {
        var lightNode = _light.Node;
        if (lightNode is null)
            return;

        var lightType = _light.GetLightType();

        // Setup resources
        _cookedParams.ShadowMap = _shadowMap.Texture;
        _cookedParams.LightRamp = _light.GetRampTexture();
        _cookedParams.LightShape = _light.GetShapeTexture();

        // Setup common shader parameters
        _cookedParams.Position = lightNode.GetWorldPosition();
        _cookedParams.Direction = lightNode.GetWorldRotation() * Vector3f.Back;
        _cookedParams.InverseRange = lightType == LightType.Directional
            ? 0.0f
            : 1.0f / Math.Max(_light.GetRange(), MathDefs.Epsilon);
        _cookedParams.VolumetricRadius = _light.GetRadius();
        _cookedParams.VolumetricLength = _light.GetLength();

        // Negative lights will use subtract blending, so use absolute RGB values
        var fade = GetLightFade(_light);
        var effectiveColor = _light.GetEffectiveColor().Abs();
        if (_light.GetUsePhysicalValues())
        {
            _cookedParams.EffectiveColorInLinearSpace = fade * effectiveColor.ToVector3();
            _cookedParams.EffectiveColorInGammaSpace = fade * effectiveColor.LinearToGamma().ToVector3();
        }
        else
        {
            _cookedParams.EffectiveColorInGammaSpace = fade * effectiveColor.ToVector3();
            _cookedParams.EffectiveColorInLinearSpace = fade * effectiveColor.GammaToLinear().ToVector3();
        }

        _cookedParams.EffectiveSpecularIntensity = fade * _light.GetEffectiveSpecularIntensity();

        // Setup vertex light parameters
        var cutoffParams = _light.GetCutoffParams();
        _cookedParams.SpotCutoff = cutoffParams.Cutoff;
        _cookedParams.InverseSpotCutoff = cutoffParams.InverseCutoff;

        _cookedParams.NumLightMatrices = 0;
        if (_cookedParams.LightShape is not null)
        {
            switch (lightType)
            {
                case LightType.Directional:
                    _cookedParams.LightShapeMatrix = Matrix4.Identity;
                    break;
                case LightType.Spot:
                    _cookedParams.LightShapeMatrix = CalculateSpotMatrix(_light, _renderBackend);
                    break;
                case LightType.Point:
                    _cookedParams.LightShapeMatrix = lightNode.GetWorldTransform().Inverse.Matrix4;
                    break;
            }
        }

        // Skip the rest if no shadow
        if (_shadowMap.IsEmpty || _numActiveSplits == 0)
            return;

        // Add subpixel offset if PCF kernel is even
        var subPixelOffset = settings.PcfKernelSize % 2 == 0 ? 0.5f : 0.0f;

        // Initialize size of shadow map
        var textureSizeX = (float)_shadowMap.Texture!.Width;
        var textureSizeY = (float)_shadowMap.Texture.Height;
        _cookedParams.ShadowMapInvSize = new Vector2f(1.0f / textureSizeX, 1.0f / textureSizeY);

        _cookedParams.ShadowCubeUVBias = Vector2f.Zero;
        _cookedParams.ShadowCubeAdjust = default;
        switch (lightType)
        {
            case LightType.Directional:
                _cookedParams.NumLightMatrices = Light.MaxCascadeSplits;
                for (var splitIndex = 0u; splitIndex < _numActiveSplits; ++splitIndex)
                    _cookedParams.LightMatrices[splitIndex] =
                        _splits[(int)splitIndex].GetWorldToShadowSpaceMatrix(subPixelOffset);
                break;

            case LightType.Spot:
                _cookedParams.NumLightMatrices = 1;
                _cookedParams.LightMatrices[0] = _splits[0].GetWorldToShadowSpaceMatrix(subPixelOffset);
                break;

            case LightType.Point:
            {
                var splitViewport = _splits[0].GetShadowMap().Rect;
                var cubeShadowMapPadding = _splits[0].GetShadowMapPadding();
                var viewportSizeX = (float)splitViewport.Width;
                var viewportSizeY = (float)splitViewport.Height;
                var viewportOffsetX = (float)splitViewport.Left;
                var viewportOffsetY = (float)splitViewport.Top;
                var relativeViewportSize = new Vector2f(viewportSizeX / textureSizeX, viewportSizeY / textureSizeY);
                var relativeViewportOffset = new Vector2f(viewportOffsetX / textureSizeX, viewportOffsetY / textureSizeY);
                _cookedParams.ShadowCubeUVBias =
                    Vector2f.One - 2.0f * cubeShadowMapPadding * _cookedParams.ShadowMapInvSize / relativeViewportSize;

                if (_renderBackend == RenderBackend.OpenGL)
                {
                    var scale = relativeViewportSize * new Vector2f(1, -1);
                    var offset = new Vector2f(0, 1) + relativeViewportOffset * new Vector2f(1, -1);
                    _cookedParams.ShadowCubeAdjust = new Vector4f(scale, offset);
                }
                else
                {
                    _cookedParams.ShadowCubeAdjust = new Vector4f(relativeViewportSize, relativeViewportOffset);
                }

                break;
            }
        }

        {
            // Calculate shadow camera depth parameters for point light shadows and shadow fade parameters for
            //  directional light shadows, stored in the same uniform
            var shadowCamera = _splits[0].GetShadowCamera();
            var nearClip = shadowCamera.GetNearClip();
            var farClip = shadowCamera.GetFarClip();
            var q = farClip / (farClip - nearClip);
            var r = -q * nearClip;

            var parameters = _light.GetShadowCascade();
            var viewFarClip = cullCamera.GetFarClip();
            var shadowRange = parameters.GetShadowRange();
            var fadeStart = parameters.FadeStart * shadowRange / viewFarClip;
            var fadeEnd = shadowRange / viewFarClip;
            var fadeRange = fadeEnd - fadeStart;

            _cookedParams.ShadowDepthFade = new Vector4f(q, r, fadeStart, 1.0f / fadeRange);
        }

        {
            var intensity = _light.GetShadowIntensity();
            var fadeStart = _light.GetShadowFadeDistance();
            var fadeEnd = _light.GetShadowDistance();
            if (fadeStart > 0.0f && fadeEnd > 0.0f && fadeEnd > fadeStart)
                intensity = MathDefs.Lerp(intensity, 1.0f,
                    MathDefs.Clamp((_light.GetDistance() - fadeStart) / (fadeEnd - fadeStart), 0.0f, 1.0f));

            // Include number of samples for PCF 1x1 and 2x2 only, bigger PCFs need non-uniform factors
            var samples = settings.PcfKernelSize == 2 ? 4.0f : 1.0f;
            _cookedParams.ShadowIntensity = new Vector4f((1.0f - intensity) / samples, intensity, 0.0f, 0.0f);
        }

        var splitDistances = new[] { MathDefs.LargeValue, MathDefs.LargeValue, MathDefs.LargeValue, MathDefs.LargeValue };
        if (lightType == LightType.Directional)
        {
            for (var i = 0u; i < _numActiveSplits; ++i)
                splitDistances[i] = _splits[(int)i].GetCascadeZRange().Second / cullCamera.GetFarClip();
        }

        _cookedParams.ShadowSplitDistances = new Vector4f(splitDistances);

        Array.Fill(_cookedParams.ShadowDepthBiasMultiplier, 1.0f);
        if (_light.GetLightType() == LightType.Directional)
        {
            var biasAutoAdjust = _light.GetShadowCascade().BiasAutoAdjust;
            var firstSplitDepthRange = _splits[0].GetShadowCamera().GetFarClip();
            for (var i = 1u; i < _numActiveSplits; ++i)
            {
                var splitDepthRange = _splits[(int)i].GetShadowCamera().GetFarClip();
                var splitScale = Math.Max(1.0f, splitDepthRange / firstSplitDepthRange);
                var multiplier = 1.0f + (splitScale - 1.0f) * biasAutoAdjust;
                _cookedParams.ShadowDepthBiasMultiplier[i] = MathDefs.SnapRound(multiplier, 0.1f);
            }
        }

        var normalOffset = _light.GetShadowBias().NormalOffset * settings.NormalOffsetScale;
        for (var i = 0u; i < _numActiveSplits; ++i)
            _cookedParams.ShadowNormalBias[i] =
                _splits[(int)i].GetShadowMapTexelSizeInWorldSpace() * normalOffset;
    }

    private void UpdateHashes()
    {
        var biasParameters = _light.GetShadowBias();

        uint commonHash = 0;
        CombineHash(ref commonHash, (uint)_light.GetLightType());
        CombineHash(ref commonHash, _light.IsNegative() ? 1u : 0u);
        CombineHash(ref commonHash, HasShadow() ? 1u : 0u);
        CombineHash(ref commonHash, _light.GetShapeTexture()?.GetSamplerStateDesc().ToHash() ?? 0u);
        CombineHash(ref commonHash, _light.GetRampTexture()?.GetSamplerStateDesc().ToHash() ?? 0u);
        CombineHash(ref commonHash, _light.GetEffectiveSpecularIntensity() > 0.0f ? 1u : 0u);
        CombineHash(ref commonHash, biasParameters.NormalOffset > 0.0f ? 1u : 0u);
        CombineHash(ref commonHash, MakeHash(biasParameters.ConstantBias));
        CombineHash(ref commonHash, MakeHash(biasParameters.SlopeScaledBias));
        CombineHash(ref commonHash, _light.GetLightMaskEffective() & Drawable.PortableLightMask);

        _forwardLitBatchHash = commonHash;

        _lightVolumeBatchHash = commonHash;
        CombineHash(ref _lightVolumeBatchHash, _cameraIsInsideLightVolume ? 1u : 0u);

        if (_light.GetLightType() != LightType.Directional)
            Array.Fill(_shadowBatchStateHashes, commonHash);
        else
        {
            for (var i = 0u; i < _numActiveSplits; ++i)
            {
                _shadowBatchStateHashes[i] = commonHash;
                CombineHash(ref _shadowBatchStateHashes[i],
                    MakeHash(100.0f * _cookedParams.ShadowDepthBiasMultiplier[i]));
            }
        }
    }

    private Vector2i GetNumSplitsInGrid()
    {
        if (_numActiveSplits == 1)
            return new Vector2i(1, 1);
        if (_numActiveSplits == 2)
            return new Vector2i(2, 1);
        if (_numActiveSplits < 6)
            return new Vector2i(2, 2);
        return new Vector2i(3, 2);
    }

    private static float GetLightFade(Light light)
    {
        var fadeStart = light.GetFadeDistance();
        var fadeEnd = light.GetDrawDistance();
        if (light.GetLightType() != LightType.Directional && fadeEnd > 0.0f && fadeStart > 0.0f && fadeStart < fadeEnd)
            return Math.Min(1.0f - (light.GetDistance() - fadeStart) / (fadeEnd - fadeStart), 1.0f);
        return 1.0f;
    }

    private static Matrix4 CalculateSpotMatrix(Light light, RenderBackend renderBackend)
    {
        var lightNode = light.Node!;
        var spotView = new Matrix3x4(lightNode.GetWorldPosition(), lightNode.GetWorldRotation(), 1.0f).Inverse.Matrix4;

        // Make the projected light slightly smaller than the shadow map to prevent light spill
        var h = 1.005f / MathF.Tan(light.GetFov() * MathDefs.DegToRad * 0.5f);
        var w = h / light.GetAspectRatio();
        var spotProj = new Matrix4(
            w, 0, 0, 0,
            0, h, 0, 0,
            0, 0, 1.0f / Math.Max(light.GetRange(), MathDefs.Epsilon), 0,
            0, 0, 1.0f, 0);

        Matrix4 texAdjust;
        if (renderBackend == RenderBackend.OpenGL)
        {
            texAdjust = Matrix4.Identity;
            // Translation then scale via composition matching C++ SetTranslation/SetScale
            texAdjust = new Matrix4(
                0.5f, 0, 0, 0.5f,
                0, -0.5f, 0, 0.5f,
                0, 0, 0.5f, 0.5f,
                0, 0, 0, 1.0f);
        }
        else
        {
            texAdjust = new Matrix4(
                0.5f, 0, 0, 0.5f,
                0, -0.5f, 0, 0.5f,
                0, 0, 1.0f, 0.0f,
                0, 0, 0, 1.0f);
        }

        return texAdjust * spotProj * spotView;
    }

    private static uint CalculateNumSplits(Light light) =>
        light.GetLightType() switch
        {
            LightType.Spot => 1,
            LightType.Point => (uint)CubeMapFace.MaxCubeMapFaces,
            LightType.Directional => (uint)light.GetNumShadowSplits(),
            _ => 0
        };

    private static List<FloatRange> GetActiveSplits(Light light, float nearClip, float farClip)
    {
        var cascade = light.GetShadowCascade();
        var numSplits = light.GetNumShadowSplits();
        var result = new List<FloatRange>((int)Light.MaxCascadeSplits);

        var nearSplit = nearClip;
        for (var i = 0; i < numSplits; ++i)
        {
            // Stop if split is completely beyond camera far clip
            if (nearSplit > farClip)
                break;

            var farSplit = Math.Min(farClip, cascade.Splits[i]);
            if (farSplit <= nearSplit)
                break;

            result.Add(new FloatRange(nearSplit, farSplit));
            nearSplit = farSplit;
        }

        return result;
    }

    private static float EstimateDistanceToCamera(Camera cullCamera, Light light)
    {
        var cameraPos = cullCamera.Node!.GetWorldPosition();
        return light.GetLightType() switch
        {
            LightType.Directional => 0.0f,
            LightType.Point => new Sphere(light.Node!.GetWorldPosition(), light.GetRange() * 1.25f).Distance(cameraPos),
            LightType.Spot => light.GetFrustum().Distance(cameraPos),
            _ => 0.0f
        };
    }

    private static void CombineHash(ref uint result, uint hash) =>
        result ^= hash + 0x9e3779b9u + (result << 6) + (result >> 2);

    private static uint MakeHash(float value)
    {
        var bits = BitConverter.SingleToUInt32Bits(value);
        return bits;
    }
}

/// <summary>
/// Cache of light processors.
/// </summary>
public sealed class LightProcessorCache
{
    private struct CachedLightProcessor
    {
        public LightProcessor? LightProcessor;
        public uint LastUsedGeneration;
    }

    private LightProcessorCacheSettings _settings = new();
    private float _timeAccumulator;
    private uint _currentGeneration;
    private readonly Dictionary<Light, CachedLightProcessor> _cache = [];

    public LightProcessorCache()
    {
    }

    public void Dispose()
    {
        foreach (var entry in _cache.Values)
            entry.LightProcessor?.Dispose();
        _cache.Clear();
    }

    public void SetSettings(LightProcessorCacheSettings settings) => _settings = settings;

    public void Update(float timeStep)
    {
        var generationChanged = false;
        _timeAccumulator += timeStep;
        while (_timeAccumulator >= 1.0f)
        {
            ++_currentGeneration;
            _timeAccumulator -= 1.0f;
            generationChanged = true;
        }

        if (generationChanged)
        {
            var maxAge = (uint)_cache.Count <= _settings.Budget
                ? _settings.NormalTimeToLive
                : _settings.AggressiveTimeToLive;
            var toRemove = new List<Light>();
            foreach (var (light, entry) in _cache)
            {
                var age = _currentGeneration - entry.LastUsedGeneration;
                if (age > maxAge)
                    toRemove.Add(light);
            }

            foreach (var light in toRemove)
            {
                if (_cache.TryGetValue(light, out var entry))
                    entry.LightProcessor?.Dispose();
                _cache.Remove(light);
            }
        }
    }

    /// <summary>Should be called on every frame to notify LRU cache.</summary>
    public LightProcessor GetLightProcessor(Light light)
    {
        ArgumentNullException.ThrowIfNull(light);
        if (_cache.TryGetValue(light, out var cached) && cached.LightProcessor is not null)
        {
            cached.LastUsedGeneration = _currentGeneration;
            _cache[light] = cached;
            return cached.LightProcessor;
        }

        var processor = new LightProcessor(light);
        _cache[light] = new CachedLightProcessor
        {
            LightProcessor = processor,
            LastUsedGeneration = _currentGeneration
        };
        return processor;
    }
}
