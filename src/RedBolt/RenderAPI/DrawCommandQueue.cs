namespace RedBolt;

using RedBolt.Bindings.Diligent;

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
    private readonly ConstantBufferCollection _constantBufferCollection = new();
    private readonly uint[] _constantBufferHashes = new uint[RenderApiLimits.MaxShaderParameterGroups];
    private Memory<byte> _currentConstantBufferData;
    private ShaderParameterGroup? _currentShaderParameterGroup;
    private ShaderProgramReflection? _currentShaderProgramReflection;
    private readonly List<ShaderResourceData> _shaderResources = [];
    private readonly List<UnorderedAccessViewData> _unorderedAccessViews = [];
    private ShaderResourceRange _currentShaderResourceGroup;
    private ShaderResourceRange _currentUnorderedAccessViewGroup;

    private readonly record struct ShaderResourceData(
        IShaderResourceVariable Variable, TextureType? Type, RawTexture? Texture, RawTexture? BackupTexture, ITextureView? View);
    private readonly record struct UnorderedAccessViewData(IShaderResourceVariable Variable, RawTexture Texture, ITextureView View);

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
        _currentShaderProgramReflection = null;
        _currentShaderParameterGroup = null;
        _currentConstantBufferData = default;
        Array.Clear(_constantBufferHashes);
        _constantBufferCollection.ClearAndInitialize(Math.Max(1u, _renderDevice.GetCaps().ConstantBufferOffsetAlignment));
        _shaderResources.Clear();
        _unorderedAccessViews.Clear();
        _currentShaderResourceGroup = default;
        _currentUnorderedAccessViewGroup = default;
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
        _currentShaderProgramReflection = pipelineState.GetReflection();
    }

    public bool BeginShaderParameterGroup(ShaderParameterGroup group, bool differentFromPrevious = false)
    {
        var index = (int)group;
        if ((uint)index >= RenderApiLimits.MaxShaderParameterGroups)
            throw new ArgumentOutOfRangeException(nameof(group));
        var uniformBuffer = _currentShaderProgramReflection?.GetUniformBuffer(group);
        if (uniformBuffer is null)
        {
            if (differentFromPrevious) _constantBufferHashes[index] = 0;
            return false;
        }
        if (!differentFromPrevious && uniformBuffer.Hash == _constantBufferHashes[index])
            return false;

        var block = _constantBufferCollection.AddBlock(uniformBuffer.Size);
        _currentDrawCommand.ConstantBuffers[index] = block.Ref;
        _currentConstantBufferData = block.Data;
        _constantBufferHashes[index] = uniformBuffer.Hash;
        _currentShaderParameterGroup = group;
        return true;
    }

    public bool AddShaderParameter(StringHash name, Variant value)
        => AddShaderParameterCore(name, span => ConstantBufferCollection.StoreParameter(span, value));

    public bool AddShaderParameter<T>(StringHash name, T value) where T : unmanaged
        => AddShaderParameterCore(name, span => ConstantBufferCollection.StoreParameter(span, value));

    public bool AddShaderParameter(StringHash name, Matrix3 value)
        => AddShaderParameterCore(name, span => ConstantBufferCollection.StoreParameter(span, value));

    public bool AddShaderParameter(StringHash name, Matrix3x4 value)
        => AddShaderParameterCore(name, span => ConstantBufferCollection.StoreParameter(span, value));

    public bool AddShaderParameter(StringHash name, ReadOnlySpan<Vector4f> values)
    {
        return TryGetShaderParameterDestination(name, out var destination)
            && ConstantBufferCollection.StoreParameter(destination, values);
    }

    public bool AddShaderParameter(StringHash name, ReadOnlySpan<Matrix3x4> values)
    {
        return TryGetShaderParameterDestination(name, out var destination)
            && ConstantBufferCollection.StoreParameter(destination, values);
    }

    public bool AddShaderParameter(StringHash name, ReadOnlySpan<Matrix4> values)
    {
        return TryGetShaderParameterDestination(name, out var destination)
            && ConstantBufferCollection.StoreParameter(destination, values);
    }

    public void CommitShaderParameterGroup(ShaderParameterGroup group)
    {
        if (_currentShaderParameterGroup != group)
            throw new InvalidOperationException("Shader parameter group is not active.");
        _currentShaderParameterGroup = null;
        _currentConstantBufferData = default;
    }

    public void AddShaderResource(StringHash name, ITextureView textureView)
    {
        ArgumentNullException.ThrowIfNull(textureView);
        var reflection = _currentShaderProgramReflection?.GetShaderResource(name);
        if (reflection?.Variable is null) return;
        _shaderResources.Add(new(reflection.Variable, null, null, null, textureView));
        _currentShaderResourceGroup = _currentShaderResourceGroup with { Second = checked((uint)_shaderResources.Count) };
    }

    public void AddShaderResource(StringHash name, RawTexture texture, RawTexture? backupTexture = null)
    {
        ArgumentNullException.ThrowIfNull(texture);
        AddNullableShaderResource(name, texture.GetParams().Type, texture, backupTexture);
    }

    public void AddNullableShaderResource(StringHash name, TextureType type, RawTexture? texture, RawTexture? backupTexture = null)
    {
        var reflection = _currentShaderProgramReflection?.GetShaderResource(name);
        if (reflection?.Variable is null) return;
        _shaderResources.Add(new(reflection.Variable, type, texture, backupTexture, null));
        _currentShaderResourceGroup = _currentShaderResourceGroup with { Second = checked((uint)_shaderResources.Count) };
    }

    public void CommitShaderResources()
    {
        _currentDrawCommand.ShaderResources = _currentShaderResourceGroup;
        var end = checked((uint)_shaderResources.Count);
        _currentShaderResourceGroup = new(end, end);
    }

    public void AddUnorderedAccessView(StringHash name, RawTexture texture, RawTextureUAVKey key)
    {
        ArgumentNullException.ThrowIfNull(texture);
        var reflection = _currentShaderProgramReflection?.GetUnorderedAccessView(name);
        if (reflection?.Variable is null) return;
        var view = texture.GetUAV(key) ?? texture.CreateUAV(key)
            ?? throw new InvalidOperationException("Requested UAV for texture does not exist.");
        _unorderedAccessViews.Add(new(reflection.Variable, texture, view));
        _currentUnorderedAccessViewGroup = _currentUnorderedAccessViewGroup with { Second = checked((uint)_unorderedAccessViews.Count) };
    }

    public void CommitUnorderedAccessViews()
    {
        _currentDrawCommand.UnorderedAccessViews = _currentUnorderedAccessViewGroup;
        var end = checked((uint)_unorderedAccessViews.Count);
        _currentUnorderedAccessViewGroup = new(end, end);
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

    public ConstantBufferCollection GetConstantBufferCollection() => _constantBufferCollection;

    /// <summary>Execute commands in the queue.</summary>
    public virtual void ExecuteInContext(RenderContext renderContext)
    {
        ArgumentNullException.ThrowIfNull(renderContext);
        if (_drawCommands.Count == 0)
            return;

        renderContext.SetClipPlaneMask(_clipPlaneMask);
        var stats = renderContext.GetMutableStats();
        var uniformBuffers = UploadConstantBuffers(renderContext.GetRenderPool());
        foreach (var command in _drawCommands)
        {
            var pipelineState = command.PipelineState;
            var reflection = pipelineState?.GetReflection();
            if (pipelineState is null || !pipelineState.IsValid || reflection is null)
                continue;

            BindShaderResources(renderContext, command.ShaderResources);
            BindUnorderedAccessViews(command.UnorderedAccessViews);
            BindConstantBuffers(reflection, command, uniformBuffers);

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

    private delegate bool StoreShaderParameter(Span<byte> destination);

    private bool AddShaderParameterCore(StringHash name, StoreShaderParameter store)
    {
        return TryGetShaderParameterDestination(name, out var destination) && store(destination);
    }

    private bool TryGetShaderParameterDestination(StringHash name, out Span<byte> destination)
    {
        var parameter = _currentShaderProgramReflection?.GetUniform(name);
        if (parameter is null) { destination = default; return false; }
        if (_currentShaderParameterGroup != parameter.Value.Group)
            throw new InvalidOperationException($"Shader parameter '{name}' belongs to group {parameter.Value.Group}, not {_currentShaderParameterGroup}.");
        var offset = checked((int)parameter.Value.Offset);
        var size = checked((int)parameter.Value.Size);
        if (offset + size > _currentConstantBufferData.Length)
            throw new InvalidOperationException("Shader parameter exceeds reflected constant-buffer bounds.");
        destination = _currentConstantBufferData.Span.Slice(offset, size);
        return true;
    }

    private void BindShaderResources(RenderContext renderContext, ShaderResourceRange range)
    {
        for (var i = range.First; i < range.Second; ++i)
        {
            var resource = _shaderResources[(int)i];
            var view = resource.View;
            if (view is null)
            {
                var texture = resource.Texture;
                if (texture is not null && renderContext.IsBoundAsRenderTarget(texture.GetTexture()!)) texture = null;
                texture ??= resource.BackupTexture;
                view = texture?.GetHandle()?.GetDefaultView(TextureViewType.TextureViewShaderResource);
            }
            if (view is not null) resource.Variable.Set(view);
        }
    }

    private void BindUnorderedAccessViews(ShaderResourceRange range)
    {
        for (var i = range.First; i < range.Second; ++i)
        {
            var resource = _unorderedAccessViews[(int)i];
            resource.Variable.Set(resource.View);
        }
    }

    private RawBuffer?[] UploadConstantBuffers(RenderPool renderPool)
    {
        var result = new RawBuffer?[_constantBufferCollection.GetNumBuffers()];
        for (var i = 0u; i < (uint)result.Length; ++i)
        {
            var usedSize = _constantBufferCollection.GetBufferSize(i);
            if (usedSize == 0) continue;
            var buffer = renderPool.GetUniformBuffer(i, _constantBufferCollection.GetGPUBufferSize(i));
            buffer.Update(_constantBufferCollection.GetBufferData(i).Span, usedSize);
            result[(int)i] = buffer;
        }
        return result;
    }

    private static void BindConstantBuffers(
        ShaderProgramReflection reflection, DrawCommandDescription command, RawBuffer?[] uniformBuffers)
    {
        for (var i = 0; i < RenderApiLimits.MaxShaderParameterGroups; ++i)
        {
            var bufferReflection = reflection.GetUniformBuffer((ShaderParameterGroup)i);
            var bufferRef = command.ConstantBuffers[i];
            if (bufferReflection is null || bufferRef.Size == 0 || bufferRef.Index >= uniformBuffers.Length)
                continue;
            var handle = uniformBuffers[(int)bufferRef.Index]?.GetHandle();
            if (handle is null) continue;
            foreach (var variable in bufferReflection.Variables)
                variable.SetBufferRange(handle, bufferRef.Offset, bufferRef.Size);
        }
    }

    private void EnqueueCurrentCommand()
    {
        _drawCommands.Add(new DrawCommandDescription(_currentDrawCommand));
    }
}
