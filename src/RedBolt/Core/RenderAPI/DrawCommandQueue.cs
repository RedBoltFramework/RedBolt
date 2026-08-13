namespace RedBolt.RenderAPI;

using System.Diagnostics;
using SystemMath = global::System.Math;

using RedBolt.Bindings.Diligent;
using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Logging;
using RedBolt.Math;
using RedBolt.RenderPipeline;

/// <summary>Queue of draw commands.</summary>
public class DrawCommandQueue
{
    private readonly RenderDevice _renderDevice;

    /// <summary>Shader parameters data when constant buffers are used.</summary>
    private struct ConstantBuffersData
    {
        /// <summary>Constant buffers.</summary>
        public readonly ConstantBufferCollection Collection = new();

        /// <summary>Current constant buffer group.</summary>
        public ShaderParameterGroup CurrentGroup = (ShaderParameterGroup)RenderApiLimits.MaxShaderParameterGroups;
        /// <summary>Current pointer to constant buffer data.</summary>
        public Memory<byte> CurrentData;
        /// <summary>Current constant buffer layout hashes.</summary>
        public readonly uint[] CurrentHashes = new uint[RenderApiLimits.MaxShaderParameterGroups];

        public ConstantBuffersData()
        {
        }
    }

    private struct ShaderResourceData
    {
        public IShaderResourceVariable? Variable;
        public RawTexture? Texture;
        public RawTexture? BackupTexture;
        public TextureType Type;
        public ITextureView? View;
    }

    private struct UnorderedAccessViewData
    {
        public IShaderResourceVariable? Variable;
        public RawTexture? Texture;
        public ITextureView? View;
    }

    private ConstantBuffersData _constantBuffers = new();

    /// <summary>Whether to enable clip plane.</summary>
    private uint _clipPlaneMask;

    /// <summary>Shader resources.</summary>
    private readonly List<ShaderResourceData> _shaderResources = [];
    /// <summary>Unordered access views.</summary>
    private readonly List<UnorderedAccessViewData> _unorderedAccessViews = [];
    /// <summary>Scissor rects.</summary>
    private readonly List<IntRect> _scissorRects = [];
    /// <summary>Draw operations.</summary>
    private readonly List<DrawCommandDescription> _drawCommands = [];

    /// <summary>Current draw operation.</summary>
    private DrawCommandDescription _currentDrawCommand;
    /// <summary>Current shader resource group.</summary>
    private ShaderResourceRange _currentShaderResourceGroup;
    /// <summary>Current unordered access view group.</summary>
    private ShaderResourceRange _currentUnorderedAccessViewGroup;
    /// <summary>Current shader program reflection.</summary>
    private ShaderProgramReflection? _currentShaderProgramReflection;

    /// <summary>Temporary storage for executing draw commands.</summary>
    private struct Temporary
    {
        public readonly List<IBuffer?> UniformBuffers = [];
        public readonly List<ITextureView?> ShaderResourceViews = [];
        public readonly IBuffer?[] VertexBufferHandles = new IBuffer?[RenderApiLimits.MaxVertexStreams];
        public readonly ulong[] VertexBufferOffsets = new ulong[RenderApiLimits.MaxVertexStreams];

        public Temporary()
        {
        }
    }

    private Temporary _temp = new();

    /// <summary>Construct.</summary>
    public DrawCommandQueue(RenderDevice renderDevice)
    {
        _renderDevice = renderDevice;
    }

    /// <summary>Reset queue.</summary>
    public void Reset()
    {
        _clipPlaneMask = 0;

        // Reset state accumulators
        _currentDrawCommand = default;
        _currentShaderResourceGroup = default;
        _currentUnorderedAccessViewGroup = default;
        _currentShaderProgramReflection = null;

        // Clear shader parameters
        _constantBuffers.Collection.ClearAndInitialize(checked((int)_renderDevice.GetCaps().ConstantBufferOffsetAlignment));
        _constantBuffers.CurrentData = default;
        Array.Clear(_constantBuffers.CurrentHashes);

        for (var i = 0; i < RenderApiLimits.MaxShaderParameterGroups; ++i)
            _currentDrawCommand.ConstantBuffers[i] = default;

        // Clear arrays and draw commands
        _shaderResources.Clear();
        _unorderedAccessViews.Clear();
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
        Debug.Assert(pipelineState is not null);

        _currentDrawCommand.PipelineState = pipelineState;
        _currentShaderProgramReflection = pipelineState.GetReflection();
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

        _currentDrawCommand.ScissorRect = _scissorRects.Count;
        _scissorRects.Add(scissorRect);
    }

    /// <summary>Begin shader parameter group. All parameters shall be set for each draw command.</summary>
    public bool BeginShaderParameterGroup(ShaderParameterGroup group, bool differentFromPrevious = false)
    {
        var index = (int)group;
        var uniformBuffer = _currentShaderProgramReflection!.GetUniformBuffer(group);
        if (uniformBuffer is null)
        {
            // If contents changed, forget cached constant buffer
            if (differentFromPrevious)
                _constantBuffers.CurrentHashes[index] = 0;
            return false;
        }

        // If data and/or layout changed, rebuild block
        if (differentFromPrevious || uniformBuffer.Hash != _constantBuffers.CurrentHashes[index])
        {
            var refAndData = _constantBuffers.Collection.AddBlock(checked((int)uniformBuffer.Size));

            _currentDrawCommand.ConstantBuffers[index] = refAndData.Ref;
            _constantBuffers.CurrentData = refAndData.Data;
            _constantBuffers.CurrentHashes[index] = uniformBuffer.Hash;
            _constantBuffers.CurrentGroup = group;
            return true;
        }

        return false;
    }

    /// <summary>Add shader parameter. Shall be called only if BeginShaderParameterGroup returned true.</summary>
    public void AddShaderParameter(StringHash name, in Variant value)
    {
        if (TryGetShaderParameterDestination(name, out var destination, out var expectedSize)
            && !ConstantBufferCollection.StoreParameter(destination, in value))
            LogUnexpectedShaderParameterType(name, expectedSize);
    }

    /// <summary>Add shader parameter. Shall be called only if BeginShaderParameterGroup returned true.</summary>
    public void AddShaderParameter<T>(StringHash name, in T value) where T : unmanaged
    {
        if (TryGetShaderParameterDestination(name, out var destination, out var expectedSize)
            && !ConstantBufferCollection.StoreParameter(destination, in value))
            LogUnexpectedShaderParameterType(name, expectedSize);
    }

    /// <summary>Add shader parameter. Shall be called only if BeginShaderParameterGroup returned true.</summary>
    public void AddShaderParameter(StringHash name, in Matrix3 value)
    {
        if (TryGetShaderParameterDestination(name, out var destination, out var expectedSize)
            && !ConstantBufferCollection.StoreParameter(destination, in value))
            LogUnexpectedShaderParameterType(name, expectedSize);
    }

    /// <summary>Add shader parameter. Shall be called only if BeginShaderParameterGroup returned true.</summary>
    public void AddShaderParameter(StringHash name, in Matrix3x4 value)
    {
        if (TryGetShaderParameterDestination(name, out var destination, out var expectedSize)
            && !ConstantBufferCollection.StoreParameter(destination, in value))
            LogUnexpectedShaderParameterType(name, expectedSize);
    }

    /// <summary>Add shader parameter. Shall be called only if BeginShaderParameterGroup returned true.</summary>
    public void AddShaderParameter(StringHash name, ReadOnlySpan<Vector4f> values)
    {
        if (TryGetShaderParameterDestination(name, out var destination, out var expectedSize)
            && !ConstantBufferCollection.StoreParameter(destination, values))
            LogUnexpectedShaderParameterType(name, expectedSize);
    }

    /// <summary>Add shader parameter. Shall be called only if BeginShaderParameterGroup returned true.</summary>
    public void AddShaderParameter(StringHash name, ReadOnlySpan<Matrix3x4> values)
    {
        if (TryGetShaderParameterDestination(name, out var destination, out var expectedSize)
            && !ConstantBufferCollection.StoreParameter(destination, values))
            LogUnexpectedShaderParameterType(name, expectedSize);
    }

    /// <summary>Add shader parameter. Shall be called only if BeginShaderParameterGroup returned true.</summary>
    public void AddShaderParameter(StringHash name, ReadOnlySpan<Matrix4> values)
    {
        if (TryGetShaderParameterDestination(name, out var destination, out var expectedSize)
            && !ConstantBufferCollection.StoreParameter(destination, values))
            LogUnexpectedShaderParameterType(name, expectedSize);
    }

    /// <summary>Commit shader parameter group. Shall be called only if BeginShaderParameterGroup returned true.</summary>
    public void CommitShaderParameterGroup(ShaderParameterGroup group)
    {
        // All data is already stored, nothing to do
        _constantBuffers.CurrentGroup = (ShaderParameterGroup)RenderApiLimits.MaxShaderParameterGroups;
    }

    /// <summary>Add shader resource with specific view.</summary>
    public void AddShaderResource(StringHash name, ITextureView textureView)
    {
        var reflection = _currentShaderProgramReflection!.GetShaderResource(name);
        if (reflection?.Variable is null) return;
        _shaderResources.Add(new ShaderResourceData
        {
            Variable = reflection.Variable,
            Type = TextureType.Count,
            View = textureView
        });
        _currentShaderResourceGroup = _currentShaderResourceGroup with { Second = _shaderResources.Count };
    }

    /// <summary>Add non-null shader resource.</summary>
    public void AddShaderResource(StringHash name, RawTexture texture, RawTexture? backupTexture = null)
    {
        AddNullableShaderResource(name, texture.GetParams().Type, texture, backupTexture);
    }

    /// <summary>Add nullable shader resource.</summary>
    public void AddNullableShaderResource(StringHash name, TextureType type, RawTexture? texture, RawTexture? backupTexture = null)
    {
        var reflection = _currentShaderProgramReflection!.GetShaderResource(name);
        if (reflection?.Variable is null) return;
        _shaderResources.Add(new ShaderResourceData
        {
            Variable = reflection.Variable,
            Texture = texture,
            BackupTexture = backupTexture,
            Type = type
        });
        _currentShaderResourceGroup = _currentShaderResourceGroup with { Second = _shaderResources.Count };
    }

    /// <summary>Commit shader resources added since previous commit.</summary>
    public void CommitShaderResources()
    {
        _currentDrawCommand.ShaderResources = _currentShaderResourceGroup;
        var end = _shaderResources.Count;
        _currentShaderResourceGroup = new(end, end);
    }

    /// <summary>Add unordered access view.</summary>
    public void AddUnorderedAccessView(StringHash name, RawTexture texture, RawTextureUAVKey key)
    {
        var reflection = _currentShaderProgramReflection!.GetUnorderedAccessView(name);
        if (reflection?.Variable is null) return;
        var view = texture.GetUAV(key);
        if (view is null)
        {
            Debug.Assert(false, "Requested UAV for texture does not exist");
            return;
        }
        _unorderedAccessViews.Add(new UnorderedAccessViewData
        {
            Variable = reflection.Variable,
            Texture = texture,
            View = view
        });
        _currentUnorderedAccessViewGroup = _currentUnorderedAccessViewGroup with { Second = _unorderedAccessViews.Count };
    }

    /// <summary>Commit unordered access views added since previous commit.</summary>
    public void CommitUnorderedAccessViews()
    {
        _currentDrawCommand.UnorderedAccessViews = _currentUnorderedAccessViewGroup;
        var end = _unorderedAccessViews.Count;
        _currentUnorderedAccessViewGroup = new(end, end);
    }

    /// <summary>Set vertex buffers.</summary>
    public void SetVertexBuffers(RawVertexBufferArray buffers)
    {
        _currentDrawCommand.VertexBuffers = buffers;
    }

    /// <summary>Set index buffer.</summary>
    public void SetIndexBuffer(RawBuffer? buffer)
    {
        _currentDrawCommand.IndexBuffer = buffer;
    }

    /// <summary>Enqueue draw non-indexed geometry.</summary>
    public void Draw(int vertexStart, int vertexCount)
    {
        Debug.Assert(_currentDrawCommand.PipelineState!.GetPipelineType() == PipelineStateType.Graphics);
        Debug.Assert(_currentDrawCommand.IndexBuffer is null);
        Debug.Assert(vertexCount > 0);

        _currentDrawCommand.IndexStart = vertexStart;
        _currentDrawCommand.IndexCount = vertexCount;
        _currentDrawCommand.BaseVertexIndex = 0;
        _currentDrawCommand.InstanceStart = 0;
        _currentDrawCommand.InstanceCount = 0;
        _drawCommands.Add(_currentDrawCommand);
    }

    /// <summary>Enqueue draw indexed geometry.</summary>
    public void DrawIndexed(int indexStart, int indexCount)
    {
        Debug.Assert(_currentDrawCommand.PipelineState!.GetPipelineType() == PipelineStateType.Graphics);
        Debug.Assert(_currentDrawCommand.IndexBuffer is not null);
        Debug.Assert(indexCount > 0);

        _currentDrawCommand.IndexStart = indexStart;
        _currentDrawCommand.IndexCount = indexCount;
        _currentDrawCommand.BaseVertexIndex = 0;
        _currentDrawCommand.InstanceStart = 0;
        _currentDrawCommand.InstanceCount = 0;
        _drawCommands.Add(_currentDrawCommand);
    }

    /// <summary>Enqueue draw indexed geometry with vertex index offset.</summary>
    public void DrawIndexed(int indexStart, int indexCount, int baseVertexIndex)
    {
        Debug.Assert(_currentDrawCommand.PipelineState!.GetPipelineType() == PipelineStateType.Graphics);
        Debug.Assert(_currentDrawCommand.IndexBuffer is not null);
        Debug.Assert(indexCount > 0);

        _currentDrawCommand.IndexStart = indexStart;
        _currentDrawCommand.IndexCount = indexCount;
        _currentDrawCommand.BaseVertexIndex = baseVertexIndex;
        _currentDrawCommand.InstanceStart = 0;
        _currentDrawCommand.InstanceCount = 0;
        _drawCommands.Add(_currentDrawCommand);
    }

    /// <summary>Enqueue draw instanced geometry.</summary>
    public void DrawInstanced(int vertexStart, int vertexCount, int instanceStart, int instanceCount)
    {
        Debug.Assert(_currentDrawCommand.PipelineState!.GetPipelineType() == PipelineStateType.Graphics);
        Debug.Assert(_currentDrawCommand.IndexBuffer is null);
        Debug.Assert(vertexCount > 0);

        _currentDrawCommand.IndexStart = vertexStart;
        _currentDrawCommand.IndexCount = vertexCount;
        _currentDrawCommand.BaseVertexIndex = 0;
        _currentDrawCommand.InstanceStart = instanceStart;
        _currentDrawCommand.InstanceCount = instanceCount;
        _drawCommands.Add(_currentDrawCommand);
    }

    /// <summary>Enqueue draw indexed, instanced geometry.</summary>
    public void DrawIndexedInstanced(int indexStart, int indexCount, int instanceStart, int instanceCount)
    {
        Debug.Assert(_currentDrawCommand.PipelineState!.GetPipelineType() == PipelineStateType.Graphics);
        Debug.Assert(_currentDrawCommand.IndexBuffer is not null);
        Debug.Assert(indexCount > 0);

        _currentDrawCommand.IndexStart = indexStart;
        _currentDrawCommand.IndexCount = indexCount;
        _currentDrawCommand.BaseVertexIndex = 0;
        _currentDrawCommand.InstanceStart = instanceStart;
        _currentDrawCommand.InstanceCount = instanceCount;
        _drawCommands.Add(_currentDrawCommand);
    }

    /// <summary>Enqueue draw indexed, instanced geometry with vertex index offset.</summary>
    public void DrawIndexedInstanced(int indexStart, int indexCount, int baseVertexIndex, int instanceStart, int instanceCount)
    {
        Debug.Assert(_currentDrawCommand.PipelineState!.GetPipelineType() == PipelineStateType.Graphics);
        Debug.Assert(_currentDrawCommand.IndexBuffer is not null);
        Debug.Assert(indexCount > 0);

        _currentDrawCommand.IndexStart = indexStart;
        _currentDrawCommand.IndexCount = indexCount;
        _currentDrawCommand.BaseVertexIndex = baseVertexIndex;
        _currentDrawCommand.InstanceStart = instanceStart;
        _currentDrawCommand.InstanceCount = instanceCount;
        _drawCommands.Add(_currentDrawCommand);
    }

    /// <summary>Dispatch compute shader.</summary>
    public void Dispatch(Vector3i numGroups)
    {
        Debug.Assert(_currentDrawCommand.PipelineState!.GetPipelineType() == PipelineStateType.Compute);

        _currentDrawCommand.NumGroups = numGroups;
        _drawCommands.Add(_currentDrawCommand);
    }

    /// <summary>Execute commands in the queue.</summary>
    public void ExecuteInContext(RenderContext renderContext)
    {
        if (_drawCommands.Count == 0)
            return;

        var renderPool = renderContext.GetRenderPool();
        var deviceContext = renderContext.GetHandle()!;
        ref var stats = ref renderContext.GetMutableStats();

        var backend = renderContext.GetRenderDevice().GetBackend();
        var caps = renderContext.GetRenderDevice().GetCaps();

        // Update constant buffers to store all shader parameters for queue
        var numUniformBuffers = _constantBuffers.Collection.GetNumBuffers();
        while (_temp.UniformBuffers.Count < numUniformBuffers)
            _temp.UniformBuffers.Add(null);
        if (_temp.UniformBuffers.Count > numUniformBuffers)
            _temp.UniformBuffers.RemoveRange(numUniformBuffers, _temp.UniformBuffers.Count - numUniformBuffers);
        for (var i = 0; i < numUniformBuffers; ++i)
        {
            var size = _constantBuffers.Collection.GetGPUBufferSize(i);
            var uniformBuffer = renderPool.GetUniformBuffer(i, size);
            uniformBuffer.Update(_constantBuffers.Collection.GetBufferData(i).Span);
            _temp.UniformBuffers[i] = uniformBuffer.GetHandle();
        }

        // Update shader resources
        while (_temp.ShaderResourceViews.Count < _shaderResources.Count)
            _temp.ShaderResourceViews.Add(null);
        if (_temp.ShaderResourceViews.Count > _shaderResources.Count)
            _temp.ShaderResourceViews.RemoveRange(
                _shaderResources.Count, _temp.ShaderResourceViews.Count - _shaderResources.Count);
        for (var i = 0; i < _shaderResources.Count; ++i)
        {
            var data = _shaderResources[i];

            if (data.View is not null)
            {
                _temp.ShaderResourceViews[i] = data.View;
                continue;
            }

            var texture = GetReadableTexture(renderContext, data.Type, data.Texture, data.BackupTexture);
            if (texture.GetResolveDirty())
                texture.Resolve();
            if (texture.GetLevelsDirty())
                texture.GenerateLevels();

            _temp.ShaderResourceViews[i] = texture.GetHandles().ShaderResourceView;
        }

        // Cached current state
        PipelineState? currentPipelineState = null;
        IShaderResourceBinding? currentShaderResourceBinding = null;
        ShaderProgramReflection? currentShaderReflection = null;
        RawBuffer? currentIndexBuffer = null;
        RawVertexBufferArray currentVertexBuffers = default;
        ShaderResourceRange currentShaderResources = default;
        ShaderResourceRange currentUnorderedAccessViews = default;
        var currentScissorRect = int.MaxValue;
        uint? currentStencilRef = null;

        // Set common state
        var blendFactors = BlendFactors.One;
        IDeviceContext_SetBlendFactors(deviceContext, in blendFactors);
        renderContext.SetClipPlaneMask(_clipPlaneMask);

        foreach (var cmd in _drawCommands)
        {
            if (cmd.BaseVertexIndex != 0 && !caps.DrawBaseVertex)
            {
                Log.GetLogger().Warning("Base vertex index is not supported by current graphics API");
                continue;
            }

            // Set pipeline state
            if (cmd.PipelineState != currentPipelineState)
            {
                var pipelineState = cmd.PipelineState!;
                var pipelineHandle = pipelineState.GetHandle();
                if (pipelineHandle is null)
                    continue; // We shouldn't get here

                // Skip this pipeline if something goes wrong.
                IDeviceContext_SetPipelineState(deviceContext, pipelineHandle);
                currentPipelineState = pipelineState;
                currentShaderResourceBinding = pipelineState.GetShaderResourceBinding();
                currentShaderReflection = pipelineState.GetReflection();

                // Reset current shader resources because mapping can be different
                currentShaderResources = default;
                currentUnorderedAccessViews = default;
            }

            // Set scissor
            if (cmd.ScissorRect != currentScissorRect)
            {
                var scissorRect = _scissorRects[cmd.ScissorRect];
                var internalRect = renderContext.GetScissorRect();
                internalRect.Left = scissorRect.Left;
                internalRect.Top = scissorRect.Top;
                internalRect.Right = scissorRect.Right;
                internalRect.Bottom = scissorRect.Bottom;

                IDeviceContext_SetScissorRects(deviceContext, 1u, internalRect, 0u, 0u);
                currentScissorRect = cmd.ScissorRect;
            }

            // Set stencil ref
            if (cmd.StencilRef != currentStencilRef)
            {
                IDeviceContext_SetStencilRef(deviceContext, cmd.StencilRef);
                currentStencilRef = cmd.StencilRef;
            }

            // Set index buffer
            if (cmd.IndexBuffer != currentIndexBuffer)
            {
                if (cmd.IndexBuffer is not null)
                    cmd.IndexBuffer.Resolve();

                var indexBufferHandle = cmd.IndexBuffer?.GetHandle();
                IDeviceContext_SetIndexBuffer(deviceContext, indexBufferHandle!, 0u,
                    ResourceStateTransitionMode.ResourceStateTransitionModeTransition);

                currentIndexBuffer = cmd.IndexBuffer;
            }

            // Set vertex buffers
            if (!VertexBuffersEqual(cmd.VertexBuffers, currentVertexBuffers)
                || (cmd.InstanceCount != 0 && !caps.DrawBaseInstance))
            {
                for (var i = 0; i < RenderApiLimits.MaxVertexStreams; ++i)
                {
                    var vertexBuffer = cmd.VertexBuffers[i];
                    if (vertexBuffer is null)
                    {
                        _temp.VertexBufferHandles[i] = null;
                        _temp.VertexBufferOffsets[i] = 0;
                        continue;
                    }

                    vertexBuffer.Resolve();

                    var needInstanceOffset = !caps.DrawBaseInstance
                        && (vertexBuffer.GetFlags() & BufferFlag.PerInstanceData) != 0;

                    _temp.VertexBufferHandles[i] = vertexBuffer.GetHandle();
                    _temp.VertexBufferOffsets[i] = needInstanceOffset
                        ? checked((ulong)cmd.InstanceStart * (ulong)vertexBuffer.GetStride())
                        : 0;
                }

                IDeviceContext_SetVertexBuffers(deviceContext, 0u, RenderApiLimits.MaxVertexStreams,
                    _temp.VertexBufferHandles,
                    _temp.VertexBufferOffsets, ResourceStateTransitionMode.ResourceStateTransitionModeTransition,
                    (SetVertexBuffersFlags)0);

                Array.Clear(_temp.VertexBufferHandles);
                Array.Clear(_temp.VertexBufferOffsets);
                currentVertexBuffers = cmd.VertexBuffers;
            }

            // Set resources
            if (currentShaderResources != cmd.ShaderResources)
            {
                for (var i = cmd.ShaderResources.First; i < cmd.ShaderResources.Second; ++i)
                    IShaderResourceVariable_Set(_shaderResources[i].Variable!,
                        _temp.ShaderResourceViews[i]!, SetShaderResourceFlags.SetShaderResourceFlagNone);

                currentShaderResources = cmd.ShaderResources;
            }

            if (currentUnorderedAccessViews != cmd.UnorderedAccessViews)
            {
                for (var i = cmd.UnorderedAccessViews.First; i < cmd.UnorderedAccessViews.Second; ++i)
                {
                    var data = _unorderedAccessViews[i];

                    var texture = data.Texture!;
                    if (texture.GetResolveDirty())
                        texture.Resolve();
                    if (texture.GetLevelsDirty())
                        texture.GenerateLevels();

                    IShaderResourceVariable_Set(data.Variable!, data.View!,
                        SetShaderResourceFlags.SetShaderResourceFlagNone);
                }

                currentUnorderedAccessViews = cmd.UnorderedAccessViews;
            }

            for (var i = 0; i < RenderApiLimits.MaxShaderParameterGroups; ++i)
            {
                var group = (ShaderParameterGroup)i;
                var uniformBufferReflection = currentShaderReflection!.GetUniformBuffer(group);
                if (uniformBufferReflection is null)
                    continue;

                var uniformBuffer = _temp.UniformBuffers[cmd.ConstantBuffers[i].Index]!;
                var variables = uniformBufferReflection.Variables;
                for (var variableIndex = 0; variableIndex < variables.Count; ++variableIndex)
                {
                    IShaderResourceVariable_SetBufferRange(variables[variableIndex], uniformBuffer,
                        checked((nuint)cmd.ConstantBuffers[i].Offset),
                        checked((nuint)cmd.ConstantBuffers[i].Size), 0,
                        SetShaderResourceFlags.SetShaderResourceFlagNone);
                }
            }

            IDeviceContext_CommitShaderResources(deviceContext, currentShaderResourceBinding!,
                ResourceStateTransitionMode.ResourceStateTransitionModeTransition);

            if (currentPipelineState!.GetPipelineType() == PipelineStateType.Graphics)
            {
                if (currentIndexBuffer is not null)
                {
                    var drawAttrs = renderContext.GetDrawIndexedAttribs();
                    drawAttrs.NumIndices = checked((uint)cmd.IndexCount);
                    drawAttrs.NumInstances = checked((uint)SystemMath.Max(1, cmd.InstanceCount));
                    drawAttrs.FirstIndexLocation = checked((uint)cmd.IndexStart);
                    drawAttrs.FirstInstanceLocation = caps.DrawBaseInstance ? checked((uint)cmd.InstanceStart) : 0u;
                    drawAttrs.BaseVertex = checked((uint)cmd.BaseVertexIndex);
                    drawAttrs.Flags = DrawFlags.DrawFlagVerifyAll;
                    drawAttrs.IndexType = currentIndexBuffer.GetStride() == 2u
                        ? RedBolt.Bindings.Diligent.ValueType.VtUint16
                        : RedBolt.Bindings.Diligent.ValueType.VtUint32;

                    IDeviceContext_DrawIndexed(deviceContext, drawAttrs);

                    stats.NumDraws += 1;
                    stats.NumPrimitives += checked((int)(drawAttrs.NumIndices * drawAttrs.NumInstances));
                }
                else
                {
                    var drawAttrs = renderContext.GetDrawAttribs();
                    drawAttrs.NumVertices = checked((uint)cmd.IndexCount);
                    drawAttrs.NumInstances = checked((uint)SystemMath.Max(1, cmd.InstanceCount));
                    drawAttrs.StartVertexLocation = checked((uint)cmd.IndexStart);
                    drawAttrs.FirstInstanceLocation = caps.DrawBaseInstance ? checked((uint)cmd.InstanceStart) : 0u;
                    drawAttrs.Flags = DrawFlags.DrawFlagVerifyAll;

                    IDeviceContext_Draw(deviceContext, drawAttrs);

                    stats.NumDraws += 1;
                    stats.NumPrimitives += checked((int)(drawAttrs.NumVertices * drawAttrs.NumInstances));
                }
            }
            else if (currentPipelineState.GetPipelineType() == PipelineStateType.Compute)
            {
                var dispatchAttrs = renderContext.GetDispatchComputeAttribs();
                dispatchAttrs.ThreadGroupCountX = (uint)cmd.NumGroups.X;
                dispatchAttrs.ThreadGroupCountY = (uint)cmd.NumGroups.Y;
                dispatchAttrs.ThreadGroupCountZ = (uint)cmd.NumGroups.Z;
                IDeviceContext_DispatchCompute(deviceContext, dispatchAttrs);

                stats.NumDispatches += 1;
            }
        }
    }

    private static bool VertexBuffersEqual(in RawVertexBufferArray left, in RawVertexBufferArray right)
    {
        for (var i = 0; i < RenderApiLimits.MaxVertexStreams; ++i)
        {
            if (left[i] != right[i])
                return false;
        }

        return true;
    }

    private bool TryGetShaderParameterDestination(
        StringHash name, out Span<byte> destination, out int expectedSize)
    {
        var parameter = _currentShaderProgramReflection!.GetUniform(name);
        if (parameter is null)
        {
            destination = default;
            expectedSize = 0;
            return false;
        }
        if (_constantBuffers.CurrentGroup != parameter.Value.Group)
        {
            Log.GetLogger().Error(
                "Shader parameter #{Hash} '{Name}' shall be stored in group {CurrentGroup} instead of group {ExpectedGroup}",
                name.Value, name.Reverse(), _constantBuffers.CurrentGroup, parameter.Value.Group);
            destination = default;
            expectedSize = 0;
            return false;
        }
        var offset = checked((int)parameter.Value.Offset);
        expectedSize = checked((int)parameter.Value.Size);
        destination = _constantBuffers.CurrentData.Span.Slice(offset, expectedSize);
        return true;
    }

    private static void LogUnexpectedShaderParameterType(StringHash name, int expectedSize)
    {
        Log.GetLogger().Error(
            "Shader parameter #{Hash} '{Name}' has unexpected type, {ExpectedSize} bytes expected",
            name.Value, name.Reverse(), expectedSize);
    }

    private static RawTexture GetReadableTexture(
        RenderContext renderContext, TextureType type, RawTexture? texture, RawTexture? backupTexture)
    {
        if (texture is not null && !renderContext.IsBoundAsRenderTarget(texture))
            return texture;
        else if (backupTexture is not null && !renderContext.IsBoundAsRenderTarget(backupTexture))
            return backupTexture;
        else
            return renderContext.GetRenderDevice().GetDefaultTexture(type)!;
    }
}
