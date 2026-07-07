namespace RedBolt;

public class RenderPool : Object
{
    private readonly RenderDevice renderDevice;
    private readonly Dictionary<ulong, RawBuffer> uniformBuffers = [];

    public RenderPool(RenderDevice renderDevice)
    {
        this.renderDevice = renderDevice ?? throw new ArgumentNullException(nameof(renderDevice));
    }

    public RenderPoolSettings Settings { get; } = new();

    public void Invalidate()
    {
        uniformBuffers.Clear();
    }

    public void Restore()
    {
    }

    public void OnFrameEnd()
    {
    }

    public RenderDevice GetRenderDevice() => renderDevice;

    public RawBuffer GetUniformBuffer(uint id, uint size)
    {
        var quantizedSize = (size + 511u) / 512u * 512u;
        var key = ((ulong)id << 32) | quantizedSize;
        if (!uniformBuffers.TryGetValue(key, out var uniformBuffer))
        {
            uniformBuffer = new RawBuffer(
                new RawBufferParams(BufferType.Uniform, quantizedSize, 0u, BufferFlag.Dynamic | BufferFlag.Discard));
            uniformBuffers[key] = uniformBuffer;
        }

        return uniformBuffer;
    }
}
