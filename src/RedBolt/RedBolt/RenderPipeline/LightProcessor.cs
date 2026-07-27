using SystemMath = global::System.Math;
using System.Runtime.CompilerServices;

using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.RenderAPI;
using RedBolt.Scene;

namespace RedBolt.RenderPipeline;

/// <summary>
/// Cooked light parameters used by shader binding.
/// </summary>
public struct CookedLightParams
{
    [InlineArray(Light.MaxCascadeSplits)]
    public struct LightMatrixContainer
    {
        private Matrix4 _element0;
    }

    [InlineArray(Light.MaxLightSplits)]
    public struct ShadowParameterContainer
    {
        private float _element0;
    }

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
    public LightMatrixContainer LightMatrices;
    public int NumLightMatrices;

    /// <summary>Shadow mapping parameters.</summary>
    public Vector4f ShadowCubeAdjust;
    public Vector4f ShadowDepthFade;
    public Vector4f ShadowIntensity;
    public Vector2f ShadowMapInvSize;
    public Vector2f ShadowCubeUVBias;
    public Vector4f ShadowSplitDistances;

    public ShadowParameterContainer ShadowNormalBias;
    public ShadowParameterContainer ShadowDepthBiasMultiplier;

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
public class LightProcessor : IDisposable
{
    /// <summary>Number of frames for shadow splits expiration.</summary>
    public const int NumSplitFramesToLive = 600;

    [InlineArray(Light.MaxCascadeSplits)]
    private struct ActiveSplitContainer
    {
        private FloatRange _element0;
    }

    private struct ActiveSplits
    {
        public ActiveSplitContainer Splits;
        public int Count;
    }

    private readonly WeakReference<Light> _light;
    private readonly List<ShadowSplitProcessor> _splits = [];
    private int _splitRemainingTimeToLive;
    private readonly RenderBackend _renderBackend;

    /// <summary>Parameters extracted from light settings.</summary>
    private bool _isShadowRequested;
    private int _numSplitsRequested;

    /// <summary>Processing results.</summary>
    private bool _cameraIsInsideLightVolume;
    private int _numActiveSplits;
    private int _shadowMapSplitSize;
    private Vector2i _shadowMapSize;
    private bool _hasLitGeometries;
    private bool _hasForwardLitGeometries;

    /// <summary>
    /// Point and spot lights: only forward lit geometries.
    /// Directional lights: all lit geometries, for shadow focusing.
    /// </summary>
    private readonly List<Drawable> _litGeometries = [];
    private PointLightGeometryQuery? _pointLightGeometryQuery;
    private SpotLightGeometryQuery? _spotLightGeometryQuery;
    private readonly Frustum _distanceFrustum = new();

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
        _light = new WeakReference<Light>(light);
        _renderBackend = light.GetSubsystem<GraphicsSystem>()!.GetRenderBackend();
        _cookedParams.LightShapeMatrix = Matrix4.Identity;
        for (var i = 0; i < Light.MaxCascadeSplits; ++i)
            _cookedParams.LightMatrices[i] = Matrix4.Identity;
    }

    public void Dispose()
    {
        _cookedParams = default;
        _shadowMap = default;
        _shadowCasterCandidates.Clear();
        _litGeometries.Clear();
        for (var i = _splits.Count - 1; i >= 0; --i)
            _splits[i].Dispose();
        _splits.Clear();
    }

    /// <summary>Begin update from main thread.</summary>
    public void BeginUpdate(DrawableProcessor drawableProcessor, LightProcessorCallback callback)
    {
        // Clear temporary containers
        _litGeometries.Clear();
        _shadowCasterCandidates.Clear();
        _shadowMap = default;

        // Initialize shadow
        _isShadowRequested = callback.IsLightShadowed(Light);
        _numSplitsRequested = _isShadowRequested ? CalculateNumSplits(Light) : 0;

        // Update splits
        if (_splits.Count <= _numSplitsRequested)
        {
            // Allocate splits and reset timer immediately
            _splitRemainingTimeToLive = NumSplitFramesToLive;
            while (_splits.Count < _numSplitsRequested)
                _splits.Add(new ShadowSplitProcessor(this, _splits.Count));
        }
        else
        {
            // Deallocate splits by timeout
            --_splitRemainingTimeToLive;
            if (_splitRemainingTimeToLive == 0)
            {
                while (_splits.Count > _numSplitsRequested)
                {
                    _splits[^1].Dispose();
                    _splits.RemoveAt(_splits.Count - 1);
                }
            }
        }
    }

    /// <summary>Update light in worker thread.</summary>
    public void Update(DrawableProcessor drawableProcessor, LightProcessorCallback callback)
    {
        var frameInfo = drawableProcessor.FrameInfo;
        var octree = frameInfo.Octree!;
        var cullCamera = frameInfo.Camera!;

        var lightType = Light.GetLightType();

        // Check if light volume contains camera
        _cameraIsInsideLightVolume = EstimateDistanceToCamera(cullCamera, Light) <= cullCamera.GetNearClip() * 2.0f;

        // Query lit geometries (and shadow casters for spot and point lights)
        switch (lightType)
        {
            case LightType.Spot:
            {
                var query = _spotLightGeometryQuery;
                if (query is null)
                {
                    query = new SpotLightGeometryQuery(_litGeometries,
                        _isShadowRequested ? _shadowCasterCandidates : null,
                        drawableProcessor, Light, cullCamera.GetPrimaryViewMask(), cullCamera.GetShadowViewMask());
                    _spotLightGeometryQuery = query;
                }
                else
                {
                    query.Reset(_litGeometries, _isShadowRequested ? _shadowCasterCandidates : null,
                        drawableProcessor, Light, cullCamera.GetPrimaryViewMask(), cullCamera.GetShadowViewMask());
                }
                octree.GetDrawables(query);
                _hasLitGeometries = query.HasLitGeometries;
                _hasForwardLitGeometries = _litGeometries.Count > 0;
                break;
            }
            case LightType.Point:
            {
                var query = _pointLightGeometryQuery;
                if (query is null)
                {
                    query = new PointLightGeometryQuery(_litGeometries,
                        _isShadowRequested ? _shadowCasterCandidates : null,
                        drawableProcessor, Light, cullCamera.GetPrimaryViewMask(), cullCamera.GetShadowViewMask());
                    _pointLightGeometryQuery = query;
                }
                else
                {
                    query.Reset(_litGeometries, _isShadowRequested ? _shadowCasterCandidates : null,
                        drawableProcessor, Light, cullCamera.GetPrimaryViewMask(), cullCamera.GetShadowViewMask());
                }
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
                var lightMask = Light.GetLightMask();
                var geometryBuckets = drawableProcessor.GeometryBuckets;
                for (var outerIndex = 0; outerIndex < geometryBuckets.Count; ++outerIndex)
                {
                    var geometryBucket = geometryBuckets[outerIndex];
                    for (var innerIndex = 0; innerIndex < geometryBucket.Count; ++innerIndex)
                    {
                        var drawable = geometryBucket[innerIndex];
                        var drawableIndex = checked((int)drawable.GetDrawableIndex());
                        var flags = drawableProcessor.GetGeometryRenderFlags(drawableIndex);
                        var isLit = (flags & GeometryRenderFlag.Lit) != 0;
                        var isForwardLit = (flags & GeometryRenderFlag.ForwardLit) != 0;

                        _hasLitGeometries = _hasLitGeometries || isLit;
                        _hasForwardLitGeometries = _hasForwardLitGeometries || isForwardLit;

                        if (isLit && (drawable.GetLightMaskInZone() & lightMask) != 0)
                            _litGeometries.Add(drawable);
                    }
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

        for (var i = 0; i < _numActiveSplits; ++i)
        {
            switch (lightType)
            {
                case LightType.Spot:
                    _splits[i].ProcessSpotShadowCasters(drawableProcessor, _shadowCasterCandidates);
                    break;
                case LightType.Point:
                    _splits[i].ProcessPointShadowCasters(drawableProcessor, _shadowCasterCandidates);
                    break;
                case LightType.Directional:
                    _splits[i].ProcessDirectionalShadowCasters(drawableProcessor, _shadowCasterCandidates);
                    break;
            }
        }

        var hasShadowCaster = false;
        for (var i = 0; i < _numActiveSplits; ++i)
        {
            if (_splits[i].HasShadowCasters)
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
        _shadowMapSplitSize = callback.GetShadowMapSize(Light, _numActiveSplits);
        var grid = GetNumSplitsInGrid();
        _shadowMapSize = new Vector2i(_shadowMapSplitSize * grid.X, _shadowMapSplitSize * grid.Y);
    }

    /// <summary>End update from main thread.</summary>
    public void EndUpdate(DrawableProcessor drawableProcessor, LightProcessorCallback callback, int pcfKernelSize)
    {
        // Allocate shadow map
        if (_numActiveSplits > 0)
        {
            _shadowMap = callback.AllocateTransientShadowMap(_shadowMapSize);
            if (!(bool)_shadowMap)
                _numActiveSplits = 0;
            else
            {
                var grid = GetNumSplitsInGrid();
                for (var i = 0; i < _numActiveSplits; ++i)
                    _splits[i].FinalizeShadow(_shadowMap.GetSplit(i, grid), pcfKernelSize);
            }
        }

        var cullCamera = drawableProcessor.FrameInfo.Camera!;
        CookShaderParameters(cullCamera, drawableProcessor.Settings);
        UpdateHashes();
    }

    /// <summary>Return pipeline state hashes.</summary>
    public uint ForwardLitHash => _forwardLitBatchHash;

    public uint GetShadowHash(int splitIndex) => _shadowBatchStateHashes[splitIndex];

    public uint LightVolumeHash => _lightVolumeBatchHash;

    /// <summary>Return values are always valid.</summary>
    public Light Light
    {
        get
        {
            _light.TryGetTarget(out var light);
            return light!;
        }
    }

    /// <summary>Return values are valid after threaded update.</summary>
    public IReadOnlyList<Drawable> LitGeometries => _litGeometries;

    public bool HasForwardLitGeometries => _hasForwardLitGeometries;

    public bool HasLitGeometries => _hasLitGeometries;

    public bool DoesOverlapCamera => _cameraIsInsideLightVolume;

    public bool HasShadow => _numActiveSplits != 0;

    public Vector2i ShadowMapSize => _numActiveSplits != 0 ? _shadowMapSize : Vector2i.Zero;

    public int NumSplits => _numActiveSplits;

    /// <summary>Return values are valid after update is finished.</summary>
    public ShadowSplitProcessor GetSplit(int splitIndex) => _splits[splitIndex];

    public ShadowSplitProcessor GetMutableSplit(int splitIndex) => _splits[splitIndex];

    public ReadOnlySpan<ShadowSplitProcessor> Splits =>
        System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_splits)[.._numActiveSplits];

    public Span<ShadowSplitProcessor> MutableSplits =>
        System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_splits)[.._numActiveSplits];

    public ShadowMapRegion ShadowMap => _shadowMap;

    public ref readonly CookedLightParams Params => ref _cookedParams;

    private void InitializeShadowSplits(DrawableProcessor drawableProcessor)
    {
        /// Setup splits
        switch (Light.GetLightType())
        {
            case LightType.Directional:
            {
                var frameInfo = drawableProcessor.FrameInfo;
                var cullCamera = frameInfo.Camera!;
                var activeSplits = GetActiveSplits(Light, cullCamera.GetNearClip(), cullCamera.GetFarClip());

                _numActiveSplits = activeSplits.Count;
                for (var i = 0; i < activeSplits.Count; ++i)
                    _splits[i].InitializeDirectional(drawableProcessor, activeSplits.Splits[i], _litGeometries);
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
                _numActiveSplits = (int)CubeMapFace.MaxCubeMapFaces;
                for (var i = 0; i < (int)CubeMapFace.MaxCubeMapFaces; ++i)
                    _splits[i].InitializePoint((CubeMapFace)i);
                break;
            }
        }
    }

    private void CookShaderParameters(Camera cullCamera, DrawableProcessorSettings settings)
    {
        var lightNode = Light.Node!;

        var lightType = Light.GetLightType();

        // Setup resources
        _cookedParams.ShadowMap = _shadowMap.Texture;
        _cookedParams.LightRamp = Light.GetRampTexture();
        _cookedParams.LightShape = Light.GetShapeTexture();

        // Setup common shader parameters
        _cookedParams.Position = lightNode.GetWorldPosition();
        _cookedParams.Direction = lightNode.GetWorldRotation() * Vector3f.Back;
        _cookedParams.InverseRange = lightType == LightType.Directional
            ? 0.0f
            : 1.0f / SystemMath.Max(Light.GetRange(), MathDefs.Epsilon);
        _cookedParams.VolumetricRadius = Light.GetRadius();
        _cookedParams.VolumetricLength = Light.GetLength();

        // Negative lights will use subtract blending, so use absolute RGB values
        var fade = GetLightFade(Light);
        var effectiveColor = Light.GetEffectiveColor().Abs();
        if (Light.GetUsePhysicalValues())
        {
            _cookedParams.EffectiveColorInLinearSpace = fade * effectiveColor.ToVector3();
            _cookedParams.EffectiveColorInGammaSpace = fade * effectiveColor.LinearToGamma().ToVector3();
        }
        else
        {
            _cookedParams.EffectiveColorInGammaSpace = fade * effectiveColor.ToVector3();
            _cookedParams.EffectiveColorInLinearSpace = fade * effectiveColor.GammaToLinear().ToVector3();
        }

        _cookedParams.EffectiveSpecularIntensity = fade * Light.GetEffectiveSpecularIntensity();

        // Setup vertex light parameters
        var cutoffParams = Light.GetCutoffParams();
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
                    _cookedParams.LightShapeMatrix = CalculateSpotMatrix(Light, _renderBackend);
                    break;
                case LightType.Point:
                    _cookedParams.LightShapeMatrix = lightNode.GetWorldTransform().Inverse.Matrix4;
                    break;
            }
        }

        // Skip the rest if no shadow
        if (!(bool)_shadowMap)
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
                for (var splitIndex = 0; splitIndex < _numActiveSplits; ++splitIndex)
                    _cookedParams.LightMatrices[splitIndex] =
                        _splits[splitIndex].GetWorldToShadowSpaceMatrix(subPixelOffset);
                break;

            case LightType.Spot:
                _cookedParams.NumLightMatrices = 1;
                _cookedParams.LightMatrices[0] = _splits[0].GetWorldToShadowSpaceMatrix(subPixelOffset);
                break;

            case LightType.Point:
            {
                var splitViewport = _splits[0].ShadowMap.Rect;
                var cubeShadowMapPadding = _splits[0].ShadowMapPadding;
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
            var shadowCamera = _splits[0].ShadowCamera;
            var nearClip = shadowCamera.GetNearClip();
            var farClip = shadowCamera.GetFarClip();
            var q = farClip / (farClip - nearClip);
            var r = -q * nearClip;

            var parameters = Light.GetShadowCascade();
            var viewFarClip = cullCamera.GetFarClip();
            var shadowRange = parameters.GetShadowRange();
            var fadeStart = parameters.FadeStart * shadowRange / viewFarClip;
            var fadeEnd = shadowRange / viewFarClip;
            var fadeRange = fadeEnd - fadeStart;

            _cookedParams.ShadowDepthFade = new Vector4f(q, r, fadeStart, 1.0f / fadeRange);
        }

        {
            var intensity = Light.GetShadowIntensity();
            var fadeStart = Light.GetShadowFadeDistance();
            var fadeEnd = Light.GetShadowDistance();
            if (fadeStart > 0.0f && fadeEnd > 0.0f && fadeEnd > fadeStart)
                intensity = MathDefs.Lerp(intensity, 1.0f,
                    MathDefs.Clamp((Light.GetDistance() - fadeStart) / (fadeEnd - fadeStart), 0.0f, 1.0f));

            // Include number of samples for PCF 1x1 and 2x2 only, bigger PCFs need non-uniform factors
            var samples = settings.PcfKernelSize == 2 ? 4.0f : 1.0f;
            _cookedParams.ShadowIntensity = new Vector4f((1.0f - intensity) / samples, intensity, 0.0f, 0.0f);
        }

        Span<float> splitDistances = stackalloc float[4];
        splitDistances.Fill(MathDefs.LargeValue);
        if (lightType == LightType.Directional)
        {
            for (var i = 0; i < _numActiveSplits; ++i)
                splitDistances[i] = _splits[i].CascadeZRange.Second / cullCamera.GetFarClip();
        }
        _cookedParams.ShadowSplitDistances = new Vector4f(splitDistances);

        for (var i = 0; i < Light.MaxLightSplits; ++i)
            _cookedParams.ShadowDepthBiasMultiplier[i] = 1.0f;
        if (Light.GetLightType() == LightType.Directional)
        {
            var biasAutoAdjust = Light.GetShadowCascade().BiasAutoAdjust;
            var firstSplitDepthRange = _splits[0].ShadowCamera.GetFarClip();
            for (var i = 1; i < _numActiveSplits; ++i)
            {
                var splitDepthRange = _splits[i].ShadowCamera.GetFarClip();
                var splitScale = SystemMath.Max(1.0f, splitDepthRange / firstSplitDepthRange);
                var multiplier = 1.0f + (splitScale - 1.0f) * biasAutoAdjust;
                _cookedParams.ShadowDepthBiasMultiplier[i] = MathDefs.SnapRound(multiplier, 0.1f);
            }
        }

        var normalOffset = Light.GetShadowBias().NormalOffset * settings.NormalOffsetScale;
        for (var i = 0; i < _numActiveSplits; ++i)
            _cookedParams.ShadowNormalBias[i] =
                _splits[i].ShadowMapTexelSizeInWorldSpace * normalOffset;
    }

    private void UpdateHashes()
    {
        var biasParameters = Light.GetShadowBias();

        uint commonHash = 0;
        CombineHash(ref commonHash, (uint)Light.GetLightType());
        CombineHash(ref commonHash, Light.IsNegative() ? 1u : 0u);
        CombineHash(ref commonHash, HasShadow ? 1u : 0u);
        CombineHash(ref commonHash, Light.GetShapeTexture()?.GetSamplerStateDesc().ToHash() ?? 0u);
        CombineHash(ref commonHash, Light.GetRampTexture()?.GetSamplerStateDesc().ToHash() ?? 0u);
        CombineHash(ref commonHash, Light.GetSpecularIntensity() > 0.0f ? 1u : 0u);
        CombineHash(ref commonHash, biasParameters.NormalOffset > 0.0f ? 1u : 0u);
        CombineHash(ref commonHash, MakeHash(biasParameters.ConstantBias));
        CombineHash(ref commonHash, MakeHash(biasParameters.SlopeScaledBias));
        CombineHash(ref commonHash, Light.GetLightMaskEffective() & Drawable.PortableLightMask);

        _forwardLitBatchHash = commonHash;

        _lightVolumeBatchHash = commonHash;
        CombineHash(ref _lightVolumeBatchHash, _cameraIsInsideLightVolume ? 1u : 0u);

        if (Light.GetLightType() != LightType.Directional)
            Array.Fill(_shadowBatchStateHashes, commonHash);
        else
        {
            for (var i = 0; i < _numActiveSplits; ++i)
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
            return SystemMath.Min(1.0f - (light.GetDistance() - fadeStart) / (fadeEnd - fadeStart), 1.0f);
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
            0, 0, 1.0f / SystemMath.Max(light.GetRange(), MathDefs.Epsilon), 0,
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

    private static int CalculateNumSplits(Light light) =>
        light.GetLightType() switch
        {
            LightType.Spot => 1,
            LightType.Point => (int)CubeMapFace.MaxCubeMapFaces,
            LightType.Directional => light.GetNumShadowSplits(),
            _ => 0
        };

    private static ActiveSplits GetActiveSplits(Light light, float nearClip, float farClip)
    {
        var cascade = light.GetShadowCascade();
        var numSplits = light.GetNumShadowSplits();
        var result = new ActiveSplits();

        var nearSplit = nearClip;
        for (var i = 0; i < numSplits; ++i)
        {
            // Stop if split is completely beyond camera far clip
            if (nearSplit > farClip)
                break;

            var farSplit = SystemMath.Min(farClip, cascade.Splits[i]);
            if (farSplit <= nearSplit)
                break;

            result.Splits[result.Count++] = new FloatRange(nearSplit, farSplit);
            nearSplit = farSplit;
        }

        return result;
    }

    private float EstimateDistanceToCamera(Camera cullCamera, Light light)
    {
        var cameraPos = cullCamera.Node!.GetWorldPosition();
        switch (light.GetLightType())
        {
            case LightType.Directional:
                return 0.0f;
            case LightType.Point:
                return new Sphere(light.Node!.GetWorldPosition(), light.GetRange() * 1.25f).Distance(cameraPos);
            case LightType.Spot:
                light.GetFrustum(_distanceFrustum);
                return _distanceFrustum.Distance(cameraPos);
            default:
                return 0.0f;
        }
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
public class LightProcessorCache : IDisposable
{
    private sealed class CachedLightProcessor
    {
        public required WeakReference<Light> Light;
        public required LightProcessor LightProcessor;
        public uint LastUsedGeneration;
    }

    private LightProcessorCacheSettings _settings = new();
    private float _timeAccumulator;
    private uint _currentGeneration;
    private readonly ConditionalWeakTable<Light, CachedLightProcessor> _cacheByLight = new();
    private readonly List<CachedLightProcessor> _cache = [];

    public LightProcessorCache()
    {
    }

    public void Dispose()
    {
        _cacheByLight.Clear();
        for (var i = _cache.Count - 1; i >= 0; --i)
            _cache[i].LightProcessor.Dispose();
        _cache.Clear();
    }

    public void SetSettings(LightProcessorCacheSettings settings) => _settings = new LightProcessorCacheSettings
    {
        Budget = settings.Budget,
        NormalTimeToLive = settings.NormalTimeToLive,
        AggressiveTimeToLive = settings.AggressiveTimeToLive
    };

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
            for (var i = _cache.Count - 1; i >= 0; --i)
            {
                var entry = _cache[i];
                var age = _currentGeneration - entry.LastUsedGeneration;
                var isAlive = entry.Light.TryGetTarget(out var light);
                if (isAlive && age <= maxAge)
                    continue;

                if (isAlive)
                    _cacheByLight.Remove(light!);
                entry.LightProcessor.Dispose();
                _cache.RemoveAt(i);
            }
        }
    }

    /// <summary>Should be called on every frame to notify LRU cache.</summary>
    public LightProcessor GetLightProcessor(Light light)
    {
        if (_cacheByLight.TryGetValue(light, out var cached))
        {
            cached.LastUsedGeneration = _currentGeneration;
            return cached.LightProcessor;
        }

        cached = new CachedLightProcessor
        {
            Light = new WeakReference<Light>(light),
            LightProcessor = new LightProcessor(light),
            LastUsedGeneration = _currentGeneration
        };
        _cacheByLight.Add(light, cached);
        _cache.Add(cached);
        return cached.LightProcessor;
    }
}
