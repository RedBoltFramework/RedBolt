namespace RedBolt;

/// <summary>Queue of draw commands.</summary>
public class DrawCommandQueue : Object
{
    private readonly RenderDevice _renderDevice;
    /// <summary>Scissor rects.</summary>
    private readonly List<IntRect> _scissorRects = [];
    /// <summary>Draw operations.</summary>
    private readonly List<DrawCommandDescription> _drawCommands = [];
    /// <summary>Current draw operation.</summary>
    private DrawCommandDescription _currentDrawCommand = new();
    /// <summary>Whether to enable clip plane.</summary>
    private uint _clipPlaneMask;

    /// <summary>Construct.</summary>
    public DrawCommandQueue(RenderDevice renderDevice)
    {
        this._renderDevice = renderDevice ?? throw new ArgumentNullException(nameof(renderDevice));
        Reset();
    }

    /// <summary>Reset queue.</summary>
    public void Reset()
    {
        _clipPlaneMask = 0;
        _currentDrawCommand = new DrawCommandDescription();
        _drawCommands.Clear();
        _scissorRects.Clear();
        _scissorRects.Add(IntRect.Zero);
    }

    /// <summary>Set clip plane enabled for all draw commands in the queue.</summary>
    public void SetClipPlaneMask(uint mask)
    {
        _clipPlaneMask = mask;
    }

    /// <summary>Set pipeline state. Must be called first.</summary>
    public void SetPipelineState(PipelineState pipelineState)
    {
        _currentDrawCommand.PipelineState = pipelineState ?? throw new ArgumentNullException(nameof(pipelineState));
    }

    /// <summary>Set stencil reference value.</summary>
    public void SetStencilRef(uint @ref)
    {
        _currentDrawCommand.StencilRef = @ref;
    }

    /// <summary>Set scissor rect.</summary>
    public void SetScissorRect(IntRect scissorRect)
    {
        if (_scissorRects.Count > 1 && _scissorRects[^1] == scissorRect)
            return;

        _currentDrawCommand.ScissorRect = checked((uint)_scissorRects.Count);
        _scissorRects.Add(scissorRect);
    }

    /// <summary>Set vertex buffers.</summary>
    public void SetVertexBuffers(ReadOnlySpan<RawBuffer?> buffers)
    {
        if (buffers.Length > RenderApiLimits.MaxVertexStreams)
            throw new ArgumentOutOfRangeException(nameof(buffers));

        Array.Clear(_currentDrawCommand.VertexBuffers, 0, _currentDrawCommand.VertexBuffers.Length);
        buffers.CopyTo(_currentDrawCommand.VertexBuffers);
    }

    /// <summary>Set index buffer.</summary>
    public void SetIndexBuffer(RawBuffer? buffer)
    {
        _currentDrawCommand.IndexBuffer = buffer;
    }

    /// <summary>Enqueue draw non-indexed geometry.</summary>
    public void Draw(uint vertexStart, uint vertexCount)
    {
        ValidateGraphicsDraw(indexed: false, vertexCount);
        _currentDrawCommand.IndexStart = vertexStart;
        _currentDrawCommand.IndexCount = vertexCount;
        _currentDrawCommand.BaseVertexIndex = 0;
        _currentDrawCommand.InstanceStart = 0;
        _currentDrawCommand.InstanceCount = 0;
        EnqueueCurrentCommand();
    }

    /// <summary>Enqueue draw indexed geometry.</summary>
    public void DrawIndexed(uint indexStart, uint indexCount)
    {
        ValidateGraphicsDraw(indexed: true, indexCount);
        _currentDrawCommand.IndexStart = indexStart;
        _currentDrawCommand.IndexCount = indexCount;
        _currentDrawCommand.BaseVertexIndex = 0;
        _currentDrawCommand.InstanceStart = 0;
        _currentDrawCommand.InstanceCount = 0;
        EnqueueCurrentCommand();
    }

    /// <summary>Enqueue draw indexed geometry with vertex index offset.</summary>
    public void DrawIndexed(uint indexStart, uint indexCount, uint baseVertexIndex)
    {
        ValidateGraphicsDraw(indexed: true, indexCount);
        _currentDrawCommand.IndexStart = indexStart;
        _currentDrawCommand.IndexCount = indexCount;
        _currentDrawCommand.BaseVertexIndex = baseVertexIndex;
        _currentDrawCommand.InstanceStart = 0;
        _currentDrawCommand.InstanceCount = 0;
        EnqueueCurrentCommand();
    }

    /// <summary>Enqueue draw instanced geometry.</summary>
    public void DrawInstanced(uint vertexStart, uint vertexCount, uint instanceStart, uint instanceCount)
    {
        ValidateGraphicsDraw(indexed: false, vertexCount);
        _currentDrawCommand.IndexStart = vertexStart;
        _currentDrawCommand.IndexCount = vertexCount;
        _currentDrawCommand.BaseVertexIndex = 0;
        _currentDrawCommand.InstanceStart = instanceStart;
        _currentDrawCommand.InstanceCount = instanceCount;
        EnqueueCurrentCommand();
    }

    /// <summary>Enqueue draw indexed, instanced geometry.</summary>
    public void DrawIndexedInstanced(uint indexStart, uint indexCount, uint instanceStart, uint instanceCount)
    {
        ValidateGraphicsDraw(indexed: true, indexCount);
        _currentDrawCommand.IndexStart = indexStart;
        _currentDrawCommand.IndexCount = indexCount;
        _currentDrawCommand.BaseVertexIndex = 0;
        _currentDrawCommand.InstanceStart = instanceStart;
        _currentDrawCommand.InstanceCount = instanceCount;
        EnqueueCurrentCommand();
    }

    /// <summary>Enqueue draw indexed, instanced geometry with vertex index offset.</summary>
    public void DrawIndexedInstanced(uint indexStart, uint indexCount, uint baseVertexIndex, uint instanceStart, uint instanceCount)
    {
        ValidateGraphicsDraw(indexed: true, indexCount);
        _currentDrawCommand.IndexStart = indexStart;
        _currentDrawCommand.IndexCount = indexCount;
        _currentDrawCommand.BaseVertexIndex = baseVertexIndex;
        _currentDrawCommand.InstanceStart = instanceStart;
        _currentDrawCommand.InstanceCount = instanceCount;
        EnqueueCurrentCommand();
    }

    /// <summary>Dispatch compute shader.</summary>
    public void Dispatch(Vector3i numGroups)
    {
        var pipelineState = _currentDrawCommand.PipelineState ?? throw new InvalidOperationException("Pipeline state must be set before dispatch.");
        if (pipelineState.GetPipelineType() != PipelineStateType.Compute)
            throw new InvalidOperationException("Dispatch requires a compute pipeline state.");

        _currentDrawCommand.NumGroups = numGroups;
        EnqueueCurrentCommand();
    }

    public uint GetClipPlaneMask() => _clipPlaneMask;

    public RenderDevice GetRenderDevice() => _renderDevice;

    /// <summary>Execute commands in the queue.</summary>
    public virtual void ExecuteInContext(RenderContext renderContext)
    {
        ArgumentNullException.ThrowIfNull(renderContext);
        if (_drawCommands.Count == 0)
            return;

        renderContext.SetClipPlaneMask(_clipPlaneMask);
        var stats = renderContext.GetMutableStats();
        foreach (var command in _drawCommands)
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
        var pipelineState = _currentDrawCommand.PipelineState ?? throw new InvalidOperationException("Pipeline state must be set before drawing.");
        if (pipelineState.GetPipelineType() != PipelineStateType.Graphics)
            throw new InvalidOperationException("Draw requires a graphics pipeline state.");
        if (indexed != (_currentDrawCommand.IndexBuffer is not null))
            throw new InvalidOperationException(indexed
                ? "Indexed draw requires an index buffer."
                : "Non-indexed draw requires no index buffer.");
        if (count == 0)
            throw new ArgumentOutOfRangeException(nameof(count));
    }

    private void EnqueueCurrentCommand()
    {
        _drawCommands.Add(new DrawCommandDescription(_currentDrawCommand));
    }
}
