using RedBolt.Core;
using RedBolt.RenderAPI;

namespace RedBolt.RenderPipeline;

/// <summary>
/// Cache for static set of pipeline states.
/// It is possible to reuse same pipeline state for different outputs.
/// </summary>
public sealed class StaticPipelineStateCache : RedObject
{
    private sealed class PerOutputCache
    {
        public PipelineStateOutputDesc OutputDesc { get; set; } = new();
        public List<PipelineState?> PipelineStates { get; } = [];
    }

    private readonly Dictionary<GraphicsPipelineStateDesc, StaticPipelineStateId> _descriptionToIndex = [];
    private readonly List<GraphicsPipelineStateDesc> _descriptions = [];
    private readonly Dictionary<uint, PerOutputCache> _caches = [];

    /// <summary>
    /// Construct.
    /// </summary>
    public StaticPipelineStateCache()
    {
    }

    /// <summary>
    /// Invalidate all pipeline states.
    /// </summary>
    public void Invalidate()
    {
        _caches.Clear();
    }

    /// <summary>
    /// Create new pipeline state.
    /// </summary>
    public StaticPipelineStateId CreateState(GraphicsPipelineStateDesc description)
    {
        if (_descriptionToIndex.TryGetValue(description, out var existingIndex))
            return existingIndex;

        var index = (StaticPipelineStateId)(_descriptions.Count + 1);
        var storedDescription = description;
        _descriptions.Add(storedDescription);
        _descriptionToIndex.Add(storedDescription, index);
        return index;
    }

    /// <summary>
    /// Get or create pipeline state for given ID and output layout.
    /// </summary>
    public PipelineState? GetState(StaticPipelineStateId id, PipelineStateOutputDesc outputDescription)
    {
        var index = (int)id - 1;
        if (id == StaticPipelineStateId.Invalid || index >= _descriptions.Count)
        {
            System.Diagnostics.Debug.Assert(false, "StaticPipelineStateCache.GetState is called with invalid id");
            return null;
        }

        var outputHash = outputDescription.ToHash();
        if (_caches.TryGetValue(outputHash, out var existingCache)
            && index < existingCache.PipelineStates.Count
            && existingCache.PipelineStates[index] is { } existingState)
            return existingState;

        if (!_caches.TryGetValue(outputHash, out var cache))
        {
            cache = new PerOutputCache();
            _caches.Add(outputHash, cache);
        }
        cache.OutputDesc = outputDescription;

        var pipelineDescription = _descriptions[index];
        pipelineDescription.Output = outputDescription;

        var pipelineStateCache = GetSubsystem<PipelineStateCache>();
        var pipelineState = pipelineStateCache?.GetGraphicsPipelineState(pipelineDescription);

        while (cache.PipelineStates.Count <= index)
            cache.PipelineStates.Add(null);
        cache.PipelineStates[index] = pipelineState;

        return pipelineState;
    }

    public override void Dispose()
    {
        _caches.Clear();
        _descriptions.Clear();
        _descriptionToIndex.Clear();
        base.Dispose();
    }
}
