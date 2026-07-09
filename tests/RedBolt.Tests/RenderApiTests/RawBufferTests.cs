using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class RawBufferTests
{
    private readonly HeadlessEngineFixture _fixture;

    public RawBufferTests(HeadlessEngineFixture fixture)
    {
        this._fixture = fixture;
    }

    [Fact]
    public void Raw_buffer_can_exist_as_shadowed_data_without_initialized_graphics()
    {
        var buffer = new RawBuffer(new RawBufferParams(BufferType.Uniform, 32u, 0u, BufferFlag.Shadowed));
        var data = Enumerable.Range(0, 32).Select(static value => (byte)value).ToArray();

        buffer.Update(data);

        Assert.Null(buffer.GetHandle());
        Assert.True(buffer.IsShadowed());
        Assert.Equal(32u, buffer.GetSize());
        Assert.Equal(BufferType.Uniform, buffer.GetBufferType());
        Assert.Equal(data, buffer.GetShadowData());
    }

    [Fact]
    public void Render_pool_caches_quantized_uniform_buffers()
    {
        var renderPool = _fixture.RenderDevice.GetRenderPool();

        var first = renderPool.GetUniformBuffer(3u, 513u);
        var second = renderPool.GetUniformBuffer(3u, 600u);
        var third = renderPool.GetUniformBuffer(4u, 513u);

        Assert.Same(first, second);
        Assert.NotSame(first, third);
        Assert.Equal(1024u, first.GetSize());

        renderPool.Invalidate();

        var recreated = renderPool.GetUniformBuffer(3u, 513u);
        Assert.NotSame(first, recreated);
    }

    [Fact]
    public void Raw_buffer_creates_native_uniform_buffer_when_graphics_is_initialized()
    {
        WithInitializedGraphics(() =>
        {
            var buffer = new RawBuffer(new RawBufferParams(BufferType.Uniform, 64u, 0u, BufferFlag.Dynamic | BufferFlag.Discard));
            var payload = Enumerable.Range(0, 64).Select(static value => (byte)value).ToArray();

            buffer.Update(payload);

            Assert.NotNull(buffer.GetHandle());
            Assert.False(buffer.GetHandle()!.IsNull);
            Assert.Equal(payload, buffer.GetShadowData());

            Assert.True(_fixture.RenderDevice.EmulateLossAndRestore());
            Assert.NotNull(buffer.GetHandle());
            Assert.False(buffer.GetHandle()!.IsNull);
            Assert.Equal(payload, buffer.GetShadowData());
        });
    }

    private void WithInitializedGraphics(Action action)
    {
        var parameters = new EngineParameters
        {
            ApplicationName = "RedBolt.Tests.RawBufferTests",
            Headless = false,
            WindowWidth = 64,
            WindowHeight = 64
        };

        _fixture.Input.Initialize(parameters);
        _fixture.Graphics.Initialize(parameters, _fixture.Input);
        try
        {
            action();
        }
        finally
        {
            _fixture.Graphics.Shutdown();
            _fixture.Input.Shutdown();
        }
    }
}
