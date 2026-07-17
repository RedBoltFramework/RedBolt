using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class BatchStateCacheTests
{
    private sealed class BatchCallback : BatchStateCacheCallback
    {
        public PipelineState? State;
        public int CreateCount;
        public int PlaceholderCount;

        public PipelineState? CreateBatchPipelineState(BatchStateCreateKey key,
            BatchStateCreateContext context, PipelineStateOutputDesc outputDesc)
        {
            ++CreateCount;
            return State;
        }

        public PipelineState? CreateBatchPipelineStatePlaceholder(uint vertexStride,
            PipelineStateOutputDesc outputDesc)
        {
            ++PlaceholderCount;
            return State;
        }
    }

    private readonly HeadlessEngineFixture _fixture;

    public BatchStateCacheTests(HeadlessEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Batch_cache_reuses_and_invalidates_state_like_cpp()
    {
        using var geometry = new Geometry();
        using var material = new Material("Cache Material");
        using var technique = new Technique("Cache Technique");
        var pass = technique.CreatePass("base");
        using var state = new PipelineState(_fixture.PipelineStateCache, new PipelineStateDesc());
        var callback = new BatchCallback { State = state };
        var cache = new BatchStateCache();
        cache.SetOutputDesc(new PipelineStateOutputDesc());
        var key = new BatchStateCreateKey(1, 2, GeometryType.Static, geometry, material, pass,
            null, null, 0);

        Assert.Same(state, cache.GetOrCreatePipelineState(key, default, callback));
        Assert.Same(state, cache.GetPipelineState(key.LookupKey));
        Assert.Same(state, cache.GetOrCreatePipelineState(key, default, callback));
        Assert.Equal(1, callback.CreateCount);

        material.SetCullMode(CullMode.Clockwise);
        Assert.Null(cache.GetPipelineState(key.LookupKey));
        Assert.Same(state, cache.GetOrCreatePipelineState(key, default, callback));
        Assert.Equal(2, callback.CreateCount);

        cache.Invalidate();
        Assert.Null(cache.GetPipelineState(key.LookupKey));
    }

    [Fact]
    public void Placeholder_cache_keeps_invalid_state_without_recreating_it()
    {
        using var state = new PipelineState(_fixture.PipelineStateCache, new PipelineStateDesc());
        var callback = new BatchCallback { State = state };
        var cache = new BatchStateCache();
        cache.SetOutputDesc(new PipelineStateOutputDesc());

        Assert.Null(cache.GetOrCreatePlaceholderPipelineState(24, callback));
        Assert.Null(cache.GetOrCreatePlaceholderPipelineState(24, callback));
        Assert.Equal(1, callback.PlaceholderCount);
    }

}
