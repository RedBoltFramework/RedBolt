using System.Reflection;
using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class RenderPoolTests
{
    private readonly HeadlessEngineFixture _fixture;

    public RenderPoolTests(HeadlessEngineFixture fixture)
    {
        this._fixture = fixture;
    }

    [Fact]
    public void Render_pool_allocates_aligned_scratch_buffers_and_reuses_base_block_after_release()
    {
        var renderPool = _fixture.RenderDevice.GetRenderPool();

        var first = renderPool.AllocateScratchBuffer(1u);
        var second = renderPool.AllocateScratchBuffer(17u);

        Assert.NotEqual(nint.Zero, first);
        Assert.NotEqual(nint.Zero, second);
        Assert.Equal(0, ((long)first) & 0xF);
        Assert.Equal(0, ((long)second) & 0xF);
        Assert.True((long)second > (long)first);

        renderPool.ReleaseScratchBuffer(first);
        renderPool.ReleaseScratchBuffer(second);

        var reused = renderPool.AllocateScratchBuffer(1u);
        Assert.Equal(first, reused);

        renderPool.ReleaseScratchBuffer(reused);
    }

    [Fact]
    public void Render_pool_grows_base_scratch_buffer_after_temporary_overflow_is_released()
    {
        var renderPool = _fixture.RenderDevice.GetRenderPool();
        var initialCapacity = GetScratchBufferCapacity(renderPool);

        var temporary = renderPool.AllocateScratchBuffer(initialCapacity + 16u);
        Assert.NotEqual(nint.Zero, temporary);

        renderPool.ReleaseScratchBuffer(temporary);

        Assert.True(GetScratchBufferCapacity(renderPool) > initialCapacity);
    }

    [Fact]
    public void Optional_raw_texture_rtv_wraps_nullable_render_target_view()
    {
        OptionalRawTextureRTV empty = default;
        OptionalRawTextureRTV filled = RenderTargetView.SwapChainColor(_fixture.RenderDevice);

        Assert.False(empty.HasValue);
        Assert.Null(empty.GetValueOrDefault());

        Assert.True(filled.HasValue);
        Assert.Equal(RenderTargetViewType.SwapChainColor, filled.Value.Type);
        Assert.Equal(RenderTargetViewType.SwapChainColor, ((RenderTargetView?)filled)!.Value.Type);
    }

    [Fact]
    public void Render_pool_invalidation_only_discards_uniform_buffers()
    {
        var renderPool = _fixture.RenderDevice.GetRenderPool();
        var parameters = new RawTextureParams(TextureType.Texture2D, TextureFormat.TexFormatRgba8Unorm, TextureFlag.None,
            new Vector3i(8, 8, 1));
        var texture = renderPool.GetTexture(parameters, (nint)17);
        var uniformBuffer = renderPool.GetUniformBuffer(2u, 64u);

        renderPool.Invalidate();

        Assert.Same(texture, renderPool.GetTexture(parameters, (nint)17));
        Assert.NotSame(uniformBuffer, renderPool.GetUniformBuffer(2u, 64u));
    }

    [Fact]
    public void Render_pool_recycling_does_not_allocate_when_cache_shape_is_stable()
    {
        var renderPool = _fixture.RenderDevice.GetRenderPool();
        var parameters = new RawTextureParams(TextureType.Texture2D, TextureFormat.TexFormatRgba8Unorm, TextureFlag.None,
            new Vector3i(8, 8, 1));
        _ = renderPool.GetTexture(parameters, (nint)29);
        renderPool.RecycleTextures();

        var before = GC.GetAllocatedBytesForCurrentThread();
        renderPool.RecycleTextures();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }

    private static uint GetScratchBufferCapacity(RenderPool renderPool)
    {
        var field = typeof(RenderPool).GetField("_scratchBufferCapacity", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("RenderPool.scratchBufferCapacity was not found.");
        return Assert.IsType<uint>(field.GetValue(renderPool));
    }
}
