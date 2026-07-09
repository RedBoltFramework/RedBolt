namespace RedBolt;

/// <summary>Queue of draw commands.</summary>
public class DrawCommandQueue : Object
{
    private readonly RenderDevice renderDevice;
    /// <summary>Scissor rects.</summary>
    private readonly List<IntRect> scissorRects = [];
    /// <summary>Draw operations.</summary>
    private readonly List<DrawCommandDescription> drawCommands = [];
    /// <summary>Current draw operation.</summary>
    private DrawCommandDescription currentDrawCommand = new();
    /// <summary>Whether to enable clip plane.</summary>
    private uint clipPlaneMask;

    /// <summary>Construct.</summary>
    public DrawCommandQueue(RenderDevice renderDevice)
    {
        this.renderDevice = renderDevice ?? throw new ArgumentNullException(nameof(renderDevice));
        Reset();
    }

    /// <summary>Reset queue.</summary>
    public void Reset()
    {
        clipPlaneMask = 0;
        currentDrawCommand = new DrawCommandDescription();
        drawCommands.Clear();
        scissorRects.Clear();
        scissorRects.Add(IntRect.Zero);
    }

    /// <summary>Set clip plane enabled for all draw commands in the queue.</summary>
    public void SetClipPlaneMask(uint mask)
    {
        clipPlaneMask = mask;
    }

    /// <summary>Set pipeline state. Must be called first.</summary>
    public void SetPipelineState(PipelineState pipelineState)
    {
        currentDrawCommand.PipelineState = pipelineState ?? throw new ArgumentNullException(nameof(pipelineState));
    }

    /// <summary>Set stencil reference value.</summary>
    public void SetStencilRef(uint @ref)
    {
        currentDrawCommand.StencilRef = @ref;
    }

    /// <summary>Set scissor rect.</summary>
    public void SetScissorRect(IntRect scissorRect)
    {
        if (scissorRects.Count > 1 && scissorRects[^1] == scissorRect)
            return;

        currentDrawCommand.ScissorRect = checked((uint)scissorRects.Count);
        scissorRects.Add(scissorRect);
    }

    /// <summary>Set vertex buffers.</summary>
    public void SetVertexBuffers(ReadOnlySpan<RawBuffer?> buffers)
    {
        if (buffers.Length > RenderApiLimits.MaxVertexStreams)
            throw new ArgumentOutOfRangeException(nameof(buffers));

        Array.Clear(currentDrawCommand.VertexBuffers, 0, currentDrawCommand.VertexBuffers.Length);
        buffers.CopyTo(currentDrawCommand.VertexBuffers);
    }

    /// <summary>Set index buffer.</summary>
    public void SetIndexBuffer(RawBuffer? buffer)
    {
        currentDrawCommand.IndexBuffer = buffer;
    }

    /// <summary>Enqueue draw non-indexed geometry.</summary>
    public void Draw(uint vertexStart, uint vertexCount)
    {
        ValidateGraphicsDraw(indexed: false, vertexCount);
        currentDrawCommand.IndexStart = vertexStart;
        currentDrawCommand.IndexCount = vertexCount;
        currentDrawCommand.BaseVertexIndex = 0;
        currentDrawCommand.InstanceStart = 0;
        currentDrawCommand.InstanceCount = 0;
        EnqueueCurrentCommand();
    }

    /// <summary>Enqueue draw indexed geometry.</summary>
    public void DrawIndexed(uint indexStart, uint indexCount)
    {
        ValidateGraphicsDraw(indexed: true, indexCount);
        currentDrawCommand.IndexStart = indexStart;
        currentDrawCommand.IndexCount = indexCount;
        currentDrawCommand.BaseVertexIndex = 0;
        currentDrawCommand.InstanceStart = 0;
        currentDrawCommand.InstanceCount = 0;
        EnqueueCurrentCommand();
    }

    /// <summary>Enqueue draw indexed geometry with vertex index offset.</summary>
    public void DrawIndexed(uint indexStart, uint indexCount, uint baseVertexIndex)
    {
        ValidateGraphicsDraw(indexed: true, indexCount);
        currentDrawCommand.IndexStart = indexStart;
        currentDrawCommand.IndexCount = indexCount;
        currentDrawCommand.BaseVertexIndex = baseVertexIndex;
        currentDrawCommand.InstanceStart = 0;
        currentDrawCommand.InstanceCount = 0;
        EnqueueCurrentCommand();
    }

    /// <summary>Enqueue draw instanced geometry.</summary>
    public void DrawInstanced(uint vertexStart, uint vertexCount, uint instanceStart, uint instanceCount)
    {
        ValidateGraphicsDraw(indexed: false, vertexCount);
        currentDrawCommand.IndexStart = vertexStart;
        currentDrawCommand.IndexCount = vertexCount;
        currentDrawCommand.BaseVertexIndex = 0;
        currentDrawCommand.InstanceStart = instanceStart;
        currentDrawCommand.InstanceCount = instanceCount;
        EnqueueCurrentCommand();
    }

    /// <summary>Enqueue draw indexed, instanced geometry.</summary>
    public void DrawIndexedInstanced(uint indexStart, uint indexCount, uint instanceStart, uint instanceCount)
    {
        ValidateGraphicsDraw(indexed: true, indexCount);
        currentDrawCommand.IndexStart = indexStart;
        currentDrawCommand.IndexCount = indexCount;
        currentDrawCommand.BaseVertexIndex = 0;
        currentDrawCommand.InstanceStart = instanceStart;
        currentDrawCommand.InstanceCount = instanceCount;
        EnqueueCurrentCommand();
    }

    /// <summary>Enqueue draw indexed, instanced geometry with vertex index offset.</summary>
    public void DrawIndexedInstanced(uint indexStart, uint indexCount, uint baseVertexIndex, uint instanceStart, uint instanceCount)
    {
        ValidateGraphicsDraw(indexed: true, indexCount);
        currentDrawCommand.IndexStart = indexStart;
        currentDrawCommand.IndexCount = indexCount;
        currentDrawCommand.BaseVertexIndex = baseVertexIndex;
        currentDrawCommand.InstanceStart = instanceStart;
        currentDrawCommand.InstanceCount = instanceCount;
        EnqueueCurrentCommand();
    }

    /// <summary>Dispatch compute shader.</summary>
    public void Dispatch(IntVector3 numGroups)
    {
        var pipelineState = currentDrawCommand.PipelineState ?? throw new InvalidOperationException("Pipeline state must be set before dispatch.");
        if (pipelineState.GetPipelineType() != PipelineStateType.Compute)
            throw new InvalidOperationException("Dispatch requires a compute pipeline state.");

        currentDrawCommand.NumGroups = numGroups;
        EnqueueCurrentCommand();
    }

    public uint GetClipPlaneMask() => clipPlaneMask;

    public RenderDevice GetRenderDevice() => renderDevice;

    /// <summary>Execute commands in the queue.</summary>
    public virtual void ExecuteInContext(RenderContext renderContext)
    {
        ArgumentNullException.ThrowIfNull(renderContext);
        if (drawCommands.Count == 0)
            return;

        renderContext.SetClipPlaneMask(clipPlaneMask);
        var stats = renderContext.GetMutableStats();
        foreach (var command in drawCommands)
        {
            var pipelineState = command.PipelineState;
            if (pipelineState is null)
                continue;

            switch (pipelineState.GetPipelineType())
            {
                case PipelineStateType.Graphics:
                    var instances = Math.Max(1u, command.InstanceCount);
                    stats.NumDraws += 1u;
                    stats.NumPrimitives += command.IndexCount * instances;
                    break;

                case PipelineStateType.Compute:
                    stats.NumDispatches += 1u;
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    private void ValidateGraphicsDraw(bool indexed, uint count)
    {
        var pipelineState = currentDrawCommand.PipelineState ?? throw new InvalidOperationException("Pipeline state must be set before drawing.");
        if (pipelineState.GetPipelineType() != PipelineStateType.Graphics)
            throw new InvalidOperationException("Draw requires a graphics pipeline state.");
        if (indexed != (currentDrawCommand.IndexBuffer is not null))
            throw new InvalidOperationException(indexed
                ? "Indexed draw requires an index buffer."
                : "Non-indexed draw requires no index buffer.");
        if (count == 0)
            throw new ArgumentOutOfRangeException(nameof(count));
    }

    private void EnqueueCurrentCommand()
    {
        drawCommands.Add(new DrawCommandDescription(currentDrawCommand));
    }
}
