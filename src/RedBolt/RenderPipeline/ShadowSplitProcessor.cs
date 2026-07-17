namespace RedBolt;

/// <summary>
/// Manages single shadow split parameters and shadow casters.
/// Spot lights always have one split.
/// Directions lights have one split per cascade.
/// Point lights always have six splits.
/// </summary>
public class ShadowSplitProcessor
{
    /// <summary>Immutable.</summary>
    private readonly LightProcessor _lightProcessor;
    private readonly Light _light;
    private readonly uint _splitIndex;
    private readonly RenderBackend _renderBackend;

    /// <summary>Internal cached objects.</summary>
    private Node? _shadowCameraNode;
    private Camera? _shadowCamera;

    /// <summary>Frame-specific objects.</summary>
    private FloatRange _cascadeZRange;
    private FloatRange _focusedCascadeZRange;
    private readonly List<Drawable> _shadowCasters = [];

    private ShadowMapRegion _shadowMap;
    private float _shadowMapWorldSpaceTexelSize;
    private uint _shadowMapPadding;

    /// <summary>Shadow caster batches.</summary>
    private readonly List<PipelineBatch> _unsortedShadowBatches = [];
    private readonly List<PipelineBatchByState> _sortedShadowBatches = [];
    private PipelineBatchGroup<PipelineBatchByState> _shadowBatches;

    public ShadowSplitProcessor(LightProcessor owner, uint splitIndex)
    {
        _lightProcessor = owner ?? throw new ArgumentNullException(nameof(owner));
        _light = owner.GetLight();
        _splitIndex = splitIndex;
        _renderBackend = default;
        _ = _renderBackend;
        _ = _focusedCascadeZRange;
        _ = _shadowCameraNode;
    }

    public void Dispose()
    {
        _shadowCasters.Clear();
        _unsortedShadowBatches.Clear();
        _sortedShadowBatches.Clear();
        _shadowCamera = null;
        _shadowCameraNode = null;
    }

    /// <summary>Initialize split parameters.</summary>
    public void InitializeDirectional(DrawableProcessor drawableProcessor,
        FloatRange splitRange, IReadOnlyList<Drawable> litGeometries)
    {
        // TODO: port complete C++ ShadowSplitProcessor::InitializeDirectional
        _ = drawableProcessor;
        _ = splitRange;
        _ = litGeometries;
    }

    public void InitializeSpot()
    {
        // TODO: port complete C++ ShadowSplitProcessor::InitializeSpot
    }

    public void InitializePoint(CubeMapFace face)
    {
        // TODO: port complete C++ ShadowSplitProcessor::InitializePoint
        _ = face;
    }

    /// <summary>Process shadow casters.</summary>
    public void ProcessDirectionalShadowCasters(DrawableProcessor drawableProcessor, List<Drawable> shadowCastersBuffer)
    {
        // TODO: port complete C++ ShadowSplitProcessor::ProcessDirectionalShadowCasters
        _ = drawableProcessor;
        _ = shadowCastersBuffer;
    }

    public void ProcessSpotShadowCasters(DrawableProcessor drawableProcessor, IReadOnlyList<Drawable> shadowCasterCandidates)
    {
        // TODO: port complete C++ ShadowSplitProcessor::ProcessSpotShadowCasters
        _ = drawableProcessor;
        _ = shadowCasterCandidates;
    }

    public void ProcessPointShadowCasters(DrawableProcessor drawableProcessor, IReadOnlyList<Drawable> shadowCasterCandidates)
    {
        // TODO: port complete C++ ShadowSplitProcessor::ProcessPointShadowCasters
        _ = drawableProcessor;
        _ = shadowCasterCandidates;
    }

    public void FinalizeShadow(ShadowMapRegion shadowMap, uint pcfKernelSize)
    {
        // TODO: port complete C++ ShadowSplitProcessor::FinalizeShadow
        _shadowMap = shadowMap;
        _ = pcfKernelSize;
    }

    public void FinalizeShadowBatches()
    {
        BatchCompositor.FillSortKeys(_sortedShadowBatches, _unsortedShadowBatches);
        _sortedShadowBatches.Sort();
        _shadowBatches = new PipelineBatchGroup<PipelineBatchByState>
        {
            Batches = _sortedShadowBatches.ToArray()
        };
    }

    /// <summary>Return immutable.</summary>
    public LightProcessor GetLightProcessor() => _lightProcessor;

    public Light GetLight() => _light;

    public uint GetSplitIndex() => _splitIndex;

    /// <summary>Return values are valid after shadow casters are processed.</summary>
    public IReadOnlyList<Drawable> GetShadowCasters() => _shadowCasters;

    public bool HasShadowCasters() => _shadowCasters.Count > 0;

    /// <summary>Return values are valid after shadow map is finalized.</summary>
    public Matrix4 GetWorldToShadowSpaceMatrix(float subPixelOffset)
    {
        // TODO: port complete C++ ShadowSplitProcessor::GetWorldToShadowSpaceMatrix
        _ = subPixelOffset;
        return default;
    }

    public ShadowMapRegion GetShadowMap() => _shadowMap;

    public float GetShadowMapTexelSizeInWorldSpace() => _shadowMapWorldSpaceTexelSize;

    public uint GetShadowMapPadding() => _shadowMapPadding;

    public FloatRange GetCascadeZRange() => _cascadeZRange;

    public Camera? GetShadowCamera() => _shadowCamera;

    /// <summary>Used by BatchRenderingContext until full split processing is available.</summary>
    public void SetShadowCamera(Camera? camera) => _shadowCamera = camera;

    public List<PipelineBatch> GetMutableUnsortedShadowBatches() => _unsortedShadowBatches;

    public ref PipelineBatchGroup<PipelineBatchByState> GetMutableShadowBatches() => ref _shadowBatches;

    public PipelineBatchGroup<PipelineBatchByState> GetShadowBatches() => _shadowBatches;
}
