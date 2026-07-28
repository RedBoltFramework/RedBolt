namespace RedBolt.Tests;

using RedBolt;
using RedBolt.Engine;
using RedBolt.RenderAPI;

[Collection(nameof(GraphicsEngineCollection))]
public sealed class RawBufferTests
{
    private readonly GraphicsEngineFixture _fixture;

    public RawBufferTests(GraphicsEngineFixture fixture)
    {
        this._fixture = fixture;
    }

    [Fact]
    public void Raw_buffer_types_match_native_value_and_reference_semantics()
    {
        Assert.True(typeof(RawBufferParams).IsValueType);
        Assert.False(typeof(RawBuffer).IsValueType);
        Assert.Equal(typeof(int), typeof(RawBufferParams).GetProperty(nameof(RawBufferParams.Size))!.PropertyType);
        Assert.Equal(typeof(int), typeof(RawBufferParams).GetProperty(nameof(RawBufferParams.Stride))!.PropertyType);
    }

    [Fact]
    public void Raw_buffer_dispose_runs_native_destructor_order()
    {
        var buffer = new RawBuffer(new RawBufferParams(BufferType.Vertex, 16, 4, BufferFlag.Shadowed));

        buffer.Dispose();

        Assert.Null(buffer.GetHandle());
        Assert.Empty(buffer.GetShadowData());
        Assert.Null(buffer.GetRenderDevice());
    }

    [Fact]
    public void Render_pool_caches_quantized_uniform_buffers()
    {
        var renderPool = _fixture.RenderDevice.GetRenderPool();

        var first = renderPool.GetUniformBuffer(3, 513);
        var second = renderPool.GetUniformBuffer(3, 600);
        var third = renderPool.GetUniformBuffer(4, 513);

        Assert.Same(first, second);
        Assert.NotSame(first, third);
        Assert.Equal(1024, first.GetSize());

        renderPool.Invalidate();

        var recreated = renderPool.GetUniformBuffer(3, 513);
        Assert.NotSame(first, recreated);
    }

    [Fact]
    public void Raw_buffer_creates_native_uniform_buffer_when_graphics_is_initialized()
    {
        WithInitializedGraphics(() =>
        {
            var buffer = new RawBuffer(new RawBufferParams(BufferType.Uniform, 64, 0, BufferFlag.Dynamic | BufferFlag.Discard));
            var payload = Enumerable.Range(0, 64).Select(static value => (byte)value).ToArray();

            buffer.Update(payload);

            Assert.NotNull(buffer.GetHandle());
            Assert.False(buffer.GetHandle()!.IsNull);
            Assert.False(buffer.IsShadowed());
            Assert.Empty(buffer.GetShadowData());

            Assert.True(_fixture.RenderDevice.EmulateLossAndRestore());
            Assert.NotNull(buffer.GetHandle());
            Assert.False(buffer.GetHandle()!.IsNull);
            Assert.True(buffer.IsDataLost());
            Assert.Empty(buffer.GetShadowData());
        });
    }

    [Fact]
    public void Raw_buffer_allows_postponed_immutable_creation()
    {
        var buffer = new TestRawBuffer();

        Assert.True(buffer.CreateForTest(new RawBufferParams(BufferType.Vertex, 32, 4, BufferFlag.Immutable)));
    }

    [Fact]
    public void Raw_buffer_maps_static_gpu_storage_without_managed_allocations()
    {
        WithInitializedGraphics(() =>
        {
            var buffer = new RawBuffer(new RawBufferParams(BufferType.Vertex, 64, 4, BufferFlag.None));

            var warmup = buffer.Map();
            warmup.Fill(1);
            buffer.Unmap();

            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 32; ++i)
            {
                var mapped = buffer.Map();
                mapped[0] = (byte)i;
                buffer.Unmap();
            }
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.Equal(0, allocated);
            Assert.False(buffer.IsLocked());
        });
    }

    [Fact]
    public void Raw_buffer_maps_shadow_storage_and_uploads_on_unmap()
    {
        var buffer = new RawBuffer(new RawBufferParams(BufferType.Vertex, 16, 4, BufferFlag.Shadowed));

        var mapped = buffer.Map();
        mapped.Fill(0x5a);
        buffer.Unmap();

        Assert.All(buffer.GetShadowData(), static value => Assert.Equal(0x5a, value));
    }

    private sealed class TestRawBuffer : RawBuffer
    {
        public bool CreateForTest(RawBufferParams parameters, byte[]? data = null) => Create(parameters, data);
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

        _fixture.InitializeGraphics(parameters);
        try
        {
            action();
        }
        finally
        {
            _fixture.ResetGraphics();
        }
    }
}

[Collection(nameof(HeadlessEngineCollection))]
public sealed class HeadlessRawBufferTests
{
    public HeadlessRawBufferTests(HeadlessEngineFixture fixture)
    {
    }

    [Fact]
    public void Raw_buffer_can_exist_as_shadowed_data_without_initialized_graphics()
    {
        var buffer = new RawBuffer(new RawBufferParams(BufferType.Uniform, 32, 0, BufferFlag.Shadowed));
        var data = Enumerable.Range(0, 32).Select(static value => (byte)value).ToArray();

        buffer.Update(data);

        Assert.Null(buffer.GetHandle());
        Assert.True(buffer.IsShadowed());
        Assert.Equal(32, buffer.GetSize());
        Assert.Equal(BufferType.Uniform, buffer.GetBufferType());
        Assert.Equal(data, buffer.GetShadowData());
    }
}
