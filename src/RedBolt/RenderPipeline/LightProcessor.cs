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
    private RenderBackend _renderBackend;

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
        _cookedParams.LightMatrices = new Matrix4[Light.MaxCascadeSplits];
        _cookedParams.ShadowNormalBias = new float[Light.MaxLightSplits];
        _cookedParams.ShadowDepthBiasMultiplier = new float[Light.MaxLightSplits];
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
        // TODO: port complete C++ LightProcessor::BeginUpdate
        _ = drawableProcessor;
        _ = callback;
        _ = _isShadowRequested;
        _ = _numSplitsRequested;
        _ = _splitRemainingTimeToLive;
        _ = _renderBackend;
        _ = _shadowMapSplitSize;
        _ = _shadowCasterCandidates;
    }

    /// <summary>Update light in worker thread.</summary>
    public void Update(DrawableProcessor drawableProcessor, LightProcessorCallback callback)
    {
        // TODO: port complete C++ LightProcessor::Update
        _ = drawableProcessor;
        _ = callback;
    }

    /// <summary>End update from main thread.</summary>
    public void EndUpdate(DrawableProcessor drawableProcessor, LightProcessorCallback callback, uint pcfKernelSize)
    {
        // TODO: port complete C++ LightProcessor::EndUpdate
        _ = drawableProcessor;
        _ = callback;
        _ = pcfKernelSize;
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
        // TODO: port complete C++ LightProcessorCache::Update
        _timeAccumulator += timeStep;
        _ = _settings;
        _ = _currentGeneration;
        _ = _timeAccumulator;
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
