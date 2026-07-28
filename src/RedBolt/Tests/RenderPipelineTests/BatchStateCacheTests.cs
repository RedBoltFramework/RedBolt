namespace RedBolt.Tests;

using RedBolt;
using RedBolt.Graphics;
using CullMode = global::RedBolt.Graphics.CullMode;
using RedBolt.RenderAPI;
using ShaderType = global::RedBolt.RenderAPI.ShaderType;
using RedBolt.RenderPipeline;

[Collection(nameof(GraphicsEngineCollection))]
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

        public PipelineState? CreateBatchPipelineStatePlaceholder(int vertexStride,
            PipelineStateOutputDesc outputDesc)
        {
            ++PlaceholderCount;
            return State;
        }
    }

    private sealed class UIBatchCallback : UIBatchStateCacheCallback
    {
        public PipelineState? State;
        public int CreateCount;

        public PipelineState? CreateUIBatchPipelineState(UIBatchStateKey key, UIBatchStateCreateContext context)
        {
            ++CreateCount;
            return State;
        }
    }

    private readonly GraphicsEngineFixture _fixture;

    public BatchStateCacheTests(GraphicsEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Batch_cache_reuses_and_invalidates_state_like_cpp()
    {
        using var geometry = new Geometry();
        using var material = new Material { Name = "Cache Material" };
        using var technique = new Technique { Name = "Cache Technique" };
        var pass = technique.CreatePass("base");
        using var state = new PipelineState(_fixture.PipelineStateCache, CreateUnavailablePipelineStateDesc());
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
        using var state = new PipelineState(_fixture.PipelineStateCache, CreateUnavailablePipelineStateDesc());
        var callback = new BatchCallback { State = state };
        var cache = new BatchStateCache();
        cache.SetOutputDesc(new PipelineStateOutputDesc());

        Assert.Null(cache.GetOrCreatePlaceholderPipelineState(24, callback));
        Assert.Null(cache.GetOrCreatePlaceholderPipelineState(24, callback));
        Assert.Equal(1, callback.PlaceholderCount);
    }

    [Fact]
    public void Cache_shape_and_teardown_match_cpp()
    {
        Assert.False(typeof(BatchStateCache).IsSealed);
        Assert.True(typeof(CachedBatchState).IsValueType);
        Assert.Equal(typeof(bool), typeof(CachedBatchState).GetField(nameof(CachedBatchState.Invalidated))!.FieldType);
        Assert.Equal(typeof(int), typeof(BatchStateCreateContext).GetProperty(nameof(BatchStateCreateContext.SubpassIndex))!.PropertyType);
        Assert.Equal(typeof(int), typeof(BatchStateCreateContext).GetProperty(nameof(BatchStateCreateContext.ShadowSplitIndex))!.PropertyType);

        var key = new BatchStateLookupKey();
        var copy = key;
        key.DrawableHash = 1;
        Assert.Equal(0u, copy.DrawableHash);

        using var state = new PipelineState(_fixture.PipelineStateCache, CreateUnavailablePipelineStateDesc());
        var callback = new BatchCallback { State = state };
        var cache = new BatchStateCache();
        cache.SetOutputDesc(new PipelineStateOutputDesc());
        cache.GetOrCreatePlaceholderPipelineState(24, callback);

        cache.Dispose();

        var cacheEntries = typeof(BatchStateCache).GetField("_cache", System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.NonPublic)!.GetValue(cache) as System.Collections.ICollection;
        var placeholders = typeof(BatchStateCache).GetField("_placeholderCache", System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.NonPublic)!.GetValue(cache) as System.Collections.ICollection;
        Assert.NotNull(cacheEntries);
        Assert.NotNull(placeholders);
        Assert.Equal(0, cacheEntries.Count);
        Assert.Equal(0, placeholders.Count);
        Assert.Null(typeof(BatchStateCache).GetField("_outputDesc", System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.NonPublic)!.GetValue(cache));
    }

    [Fact]
    public void UI_batch_cache_reuses_and_invalidates_state_like_cpp()
    {
        using var material = new Material { Name = "UI Cache Material" };
        using var technique = new Technique { Name = "UI Cache Technique" };
        var pass = technique.CreatePass("base");
        using var state = new PipelineState(_fixture.PipelineStateCache, CreateUnavailablePipelineStateDesc());
        var callback = new UIBatchCallback { State = state };
        using var cache = new UIBatchStateCache();
        var key = new UIBatchStateKey(false, new PipelineStateOutputDesc(), material, pass, BlendMode.Alpha, 0);

        Assert.Same(state, cache.GetOrCreatePipelineState(key, default, callback));
        Assert.Same(state, cache.GetPipelineState(key));
        Assert.Same(state, cache.GetOrCreatePipelineState(key, default, callback));
        Assert.Equal(1, callback.CreateCount);

        material.SetCullMode(CullMode.Clockwise);
        Assert.Null(cache.GetPipelineState(key));
        Assert.Same(state, cache.GetOrCreatePipelineState(key, default, callback));
        Assert.Equal(2, callback.CreateCount);

        cache.Invalidate();
        Assert.Null(cache.GetPipelineState(key));
    }

    private static PipelineStateDesc CreateUnavailablePipelineStateDesc()
    {
        return new PipelineStateDesc(new GraphicsPipelineStateDesc
        {
            VertexShader = new RawShader(new ShaderBytecode(ShaderType.VS)),
            PixelShader = new RawShader(new ShaderBytecode(ShaderType.PS))
        });
    }

}
