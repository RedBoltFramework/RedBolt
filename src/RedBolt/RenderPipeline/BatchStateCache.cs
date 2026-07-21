namespace RedBolt;

public sealed class BatchStateCache
{
    private sealed class CachedBatchState
    {
        public int Invalidated = 1;
        public PipelineState? PipelineState;
        public uint GeometryHash;
        public uint MaterialHash;
        public uint PassHash;
    }

    private PipelineStateOutputDesc? _outputDesc;
    private readonly Dictionary<BatchStateLookupKey, CachedBatchState> _cache = [];
    private readonly Dictionary<int, PipelineState?> _placeholderCache = [];

    public void Invalidate() => _cache.Clear();

    public void SetOutputDesc(PipelineStateOutputDesc outputDesc)
    {
        if (_outputDesc?.Equals(outputDesc) != true)
        {
            _outputDesc = outputDesc;
            Invalidate();
        }
    }

    public PipelineState? GetPipelineState(BatchStateLookupKey key)
    {
        System.Diagnostics.Debug.Assert(_outputDesc is not null);
        if (!_cache.TryGetValue(key, out var entry) || Volatile.Read(ref entry.Invalidated) != 0)
            return null;
        if (entry.PipelineState is null
            || key.Geometry.GetPipelineStateHash() != entry.GeometryHash
            || key.Material.GetPipelineStateHash() != entry.MaterialHash
            || key.Pass.GetPipelineStateHash() != entry.PassHash)
        {
            Volatile.Write(ref entry.Invalidated, 1);
            return null;
        }
        return entry.PipelineState;
    }

    public PipelineState? GetOrCreatePipelineState(BatchStateCreateKey key,
        BatchStateCreateContext context, BatchStateCacheCallback callback)
    {
        System.Diagnostics.Debug.Assert(_outputDesc is not null);
        var lookupKey = key.LookupKey;
        if (!_cache.TryGetValue(lookupKey, out var entry))
        {
            entry = new CachedBatchState();
            _cache.Add(lookupKey, entry);
        }
        if (entry.PipelineState is null || Volatile.Read(ref entry.Invalidated) != 0
            || key.Geometry.GetPipelineStateHash() != entry.GeometryHash
            || key.Material.GetPipelineStateHash() != entry.MaterialHash
            || key.Pass.GetPipelineStateHash() != entry.PassHash)
        {
            entry.PipelineState = callback.CreateBatchPipelineState(key, context, _outputDesc!.Value);
            entry.GeometryHash = key.Geometry.GetPipelineStateHash();
            entry.MaterialHash = key.Material.GetPipelineStateHash();
            entry.PassHash = key.Pass.GetPipelineStateHash();
            Volatile.Write(ref entry.Invalidated, 0);
        }
        return entry.PipelineState;
    }

    public PipelineState? GetOrCreatePlaceholderPipelineState(int vertexStride, BatchStateCacheCallback callback)
    {
        if (!_placeholderCache.TryGetValue(vertexStride, out var entry) || entry is null)
        {
            entry = callback.CreateBatchPipelineStatePlaceholder(vertexStride, _outputDesc!.Value);
            _placeholderCache[vertexStride] = entry;
        }
        return entry?.IsValid == true ? entry : null;
    }
}
