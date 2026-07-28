namespace RedBolt.RenderAPI;

using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

using RedBolt.Bindings.Diligent;
using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Graphics;
using CullMode = global::RedBolt.Graphics.CullMode;
using FillMode = global::RedBolt.Graphics.FillMode;
using RedBolt.Input;
using RedBolt.Logging;
using RedBolt.RenderPipeline;

/// <summary>Cooked pipeline state.</summary>
public class PipelineState : DeviceObject
{
    private readonly IDFamily<PipelineState> _idFamily = new();
    private readonly WeakReference<PipelineStateCache>? _owner;
    private readonly PipelineStateDesc _desc;
    private IPipelineState? _handle;
    private IShaderResourceBinding? _shaderResourceBinding;
    private ShaderProgramReflection? _reflection;
    private readonly List<RawShader> _subscribedShaders = [];

    public PipelineState(PipelineStateCache owner, PipelineStateDesc desc)
    {
        _owner = new WeakReference<PipelineStateCache>(owner ?? throw new ArgumentNullException(nameof(owner)));
        _desc = desc ?? throw new ArgumentNullException(nameof(desc));
        SetDebugName($"{desc.GetDebugName()} #{desc.ToHash()}");
        CreateGPU();
    }

    public override void Dispose()
    {
        if (Thread.CurrentThread.ManagedThreadId != WorkQueue.GetMainThreadId())
        {
            Log.GetLogger().LogWarning("Pipeline state should be released only from main thread");
        }
        else if (_owner is not null && _owner.TryGetTarget(out var pipelineStateCache))
            pipelineStateCache.ReleasePipelineState(_desc);

        UnsubscribeFromShaders();
        DestroyGPU();
        _idFamily.Dispose();
        base.Dispose();
    }

    public uint GetObjectID() => _idFamily.GetObjectID();

    public static uint GetNextFreeObjectID() => IDFamily<PipelineState>.GetNextFreeObjectID();

    public void AcquireObjectID() => _idFamily.AcquireObjectID();

    public void ReleaseObjectID() => _idFamily.ReleaseObjectID();

    internal void ReleaseOwner(PipelineStateCache owner)
    {
        if (_owner is not null && _owner.TryGetTarget(out var pipelineStateCache) &&
            ReferenceEquals(owner, pipelineStateCache))
        {
            _owner.SetTarget(null!);
        }
    }

    public bool IsValid => _reflection is not null;

    public PipelineStateType GetPipelineType() => _desc.GetPipelineType();

    public PipelineStateDesc GetDesc() => _desc;

    public ShaderProgramReflection? GetReflection() => _reflection;

    public IPipelineState? GetHandle() => _handle;

    public IShaderResourceBinding? GetShaderResourceBinding() => _shaderResourceBinding;

    public override void Invalidate()
    {
        DestroyGPU();

        if (_renderDevice is not null)
            _renderDevice.QueuePipelineStateReload(this);
    }

    public override void Restore()
    {
        if (_handle is { IsNull: false })
            return;

        RestoreDependency(GetSubsystem<PipelineStateCache>());

        if (_desc.AsGraphics() is { } graphicsDesc)
        {
            RestoreDependency(graphicsDesc.VertexShader);
            RestoreDependency(graphicsDesc.PixelShader);
            RestoreDependency(graphicsDesc.GeometryShader);
            RestoreDependency(graphicsDesc.HullShader);
            RestoreDependency(graphicsDesc.DomainShader);
        }

        if (_desc.AsCompute() is { } computeDesc)
            RestoreDependency(computeDesc.ComputeShader);

        CreateGPU();
    }

    public override void Destroy()
    {
        DestroyGPU();
    }

    private void CreateGPU()
    {
        if (_desc.AsGraphics() is { } graphicsDesc)
            CreateGPU(graphicsDesc);
        else if (_desc.AsCompute() is { } computeDesc)
            CreateGPU(computeDesc);
    }

    private void SubscribeToShaders()
    {
        var shaders = _desc.AsGraphics() is { } graphics
            ? new RawShader?[] { graphics.VertexShader, graphics.PixelShader, graphics.DomainShader, graphics.HullShader, graphics.GeometryShader }
            : new RawShader?[] { _desc.AsCompute()?.ComputeShader };
        foreach (var shader in shaders)
        {
            if (shader is null)
                continue;
            shader.OnReloaded += OnShaderReloaded;
            _subscribedShaders.Add(shader);
        }
    }

    private void UnsubscribeFromShaders()
    {
        foreach (var shader in _subscribedShaders)
            shader.OnReloaded -= OnShaderReloaded;
        _subscribedShaders.Clear();
    }

    private void OnShaderReloaded() => Invalidate();

    private void CreateGPU(ComputePipelineStateDesc desc)
    {
        DestroyGPU();

        if (desc.ComputeShader is not null &&
            (desc.ComputeShader.GetHandle() is null || desc.ComputeShader.GetHandle()!.IsNull))
        {
            Log.GetLogger().LogError(
                "Failed to create PipelineState '{Name}' due to failed {Type} shader compilation",
                GetDebugName(), desc.ComputeShader.GetShaderType());
            return;
        }

        var device = _renderDevice!.GetRenderDevice()!;
        var isOpenGl = _renderDevice.GetBackend() == RenderBackend.OpenGL;
        var hasSeparableShaderPrograms =
            device.GetDeviceInfo().FeaturesSeparablePrograms != DeviceFeatureState.DeviceFeatureStateDisabled;
        System.Diagnostics.Debug.Assert(isOpenGl || hasSeparableShaderPrograms);
        var shader = desc.ComputeShader!.GetHandle()!;

        SubscribeToShaders();
        nint temporaryGlProgram = 0;
        try
        {
            ShaderProgramReflection reflection;
            if (hasSeparableShaderPrograms)
                reflection = new ShaderProgramReflection([desc.ComputeShader]);
            else
            {
                temporaryGlProgram = DiligentNativeBindings.CreateTemporaryGLProgram(
                    shader, null!, null!, null!, null!, false);
                reflection = new ShaderProgramReflection(temporaryGlProgram);
                DiligentNativeBindings.ReleaseTemporaryGLProgramHandle(temporaryGlProgram);
            }

            _reflection = reflection;
            using var samplers = new TemporaryImmutableSamplers(desc.Samplers, reflection, _renderDevice,
                RedBolt.Bindings.Diligent.ShaderType.ShaderTypeCompute);
            using var name = new CString(GetDebugName());
            using var nativeDesc = new RedBolt.Bindings.Diligent.PipelineStateDesc
            {
                Name = name.Pointer,
                PipelineType = RedBolt.Bindings.Diligent.PipelineType.PipelineTypeCompute,
                ResourceLayoutDefaultVariableType = ShaderResourceVariableType.ShaderResourceVariableTypeDynamic,
                ResourceLayoutImmutableSamplers = samplers.First!,
                ResourceLayoutNumImmutableSamplers = (uint)samplers.Count
            };
            using var createInfo = new ComputePipelineStateCreateInfo { PSODesc = nativeDesc, PCS = shader };
            createInfo.PPSOCache = GetSubsystem<PipelineStateCache>()!.GetHandle()!;
            device.CreateComputePipelineState(createInfo, out _handle);
            if (_handle is null || _handle.IsNull)
            {
                DestroyGPU();
                Log.GetLogger().LogError("Failed to create PipelineState '{Name}'", GetDebugName());
                return;
            }

            FinishCreation();
        }
        finally
        {
            if (temporaryGlProgram != 0)
                DiligentNativeBindings.DeleteTemporaryGLProgram(temporaryGlProgram);
        }
    }

    private void CreateGPU(GraphicsPipelineStateDesc desc)
    {
        DestroyGPU();

        foreach (var shader in new RawShader?[]
                 { desc.VertexShader, desc.PixelShader, desc.DomainShader, desc.HullShader, desc.GeometryShader })
        {
            if (shader is not null && (shader.GetHandle() is null || shader.GetHandle()!.IsNull))
            {
                Log.GetLogger().LogError(
                    "Failed to create PipelineState '{Name}' due to failed {Type} shader compilation",
                    GetDebugName(), shader.GetShaderType());
                return;
            }
        }

        var device = _renderDevice!.GetRenderDevice()!;
        var vertexShader = desc.VertexShader?.GetHandle();
        var pixelShader = desc.PixelShader?.GetHandle();
        SubscribeToShaders();
        TemporaryLayoutArray? layout = null;
        nint temporaryGlProgram = 0;
        try
        {
            var isOpenGl = _renderDevice.GetBackend() == RenderBackend.OpenGL;
            var hasSeparableShaderPrograms = device.GetDeviceInfo().FeaturesSeparablePrograms != DeviceFeatureState.DeviceFeatureStateDisabled;
            System.Diagnostics.Debug.Assert(isOpenGl || hasSeparableShaderPrograms);
            var vertexAttributes = desc.VertexShader!.GetBytecode().VertexAttributes;
            if (isOpenGl)
            {
                temporaryGlProgram = DiligentNativeBindings.CreateTemporaryGLProgram(vertexShader!, pixelShader!,
                    desc.DomainShader?.GetHandle()!, desc.HullShader?.GetHandle()!, desc.GeometryShader?.GetHandle()!,
                    hasSeparableShaderPrograms);
                vertexAttributes = [];
                var numAttributes = DiligentNativeBindings.GetTemporaryGLProgramVertexAttributeCount(temporaryGlProgram);
                for (var i = 0; i < (int)numAttributes; ++i)
                {
                    var attributeName = System.Text.Encoding.UTF8.GetString(
                        DiligentNativeBindings.GetTemporaryGLProgramVertexAttributeName(temporaryGlProgram, (uint)i));
                    var attribute = RenderAPIUtils.ParseVertexAttribute(attributeName);
                    if (attribute is not null)
                    {
                        vertexAttributes.Add(attribute.Value with
                        {
                            InputIndex = checked((int)DiligentNativeBindings.GetTemporaryGLProgramVertexAttributeLocation(
                                temporaryGlProgram, (uint)i))
                        });
                    }
                    else
                        Log.GetLogger().LogWarning("Unknown vertex element semantic: {Name}", attributeName);
                }
            }
            var shaders = new RawShader?[] { desc.VertexShader, desc.PixelShader, desc.DomainShader, desc.HullShader, desc.GeometryShader };
            var reflection = isOpenGl && !hasSeparableShaderPrograms
                ? new ShaderProgramReflection(temporaryGlProgram)
                : new ShaderProgramReflection(shaders);
            _reflection = reflection;
            if (temporaryGlProgram != 0)
                DiligentNativeBindings.ReleaseTemporaryGLProgramHandle(temporaryGlProgram);
            if (desc.InputLayout.Size != 0)
            {
                Span<InputLayoutElementDesc> elements = stackalloc InputLayoutElementDesc[desc.InputLayout.Size];
                for (var i = 0; i < desc.InputLayout.Size; ++i)
                    elements[i] = desc.InputLayout.Elements[i];
                layout = new TemporaryLayoutArray(elements, vertexAttributes);
            }
            using var samplers = new TemporaryImmutableSamplers(desc.Samplers, reflection, _renderDevice!,
                RedBolt.Bindings.Diligent.ShaderType.ShaderTypeAllGraphics);
            using var name = new CString(GetDebugName());
            using var nativeDesc = new RedBolt.Bindings.Diligent.PipelineStateDesc
            {
                Name = name.Pointer,
                PipelineType = RedBolt.Bindings.Diligent.PipelineType.PipelineTypeGraphics,
                ResourceLayoutDefaultVariableType = ShaderResourceVariableType.ShaderResourceVariableTypeDynamic,
                ResourceLayoutImmutableSamplers = samplers.First!,
                ResourceLayoutNumImmutableSamplers = (uint)samplers.Count
            };
            var depthBits = desc.Output.DepthStencilFormat ==
                RedBolt.Bindings.Diligent.TextureFormat.TexFormatD16Unorm ? 16 : 24;
            var scaledDepthBias = isOpenGl ? 0 : (int)(desc.ConstantDepthBias * (1 << depthBits));
            using var createInfo = new GraphicsPipelineStateCreateInfo
            {
                PSODesc = nativeDesc,
                PVS = vertexShader!,
                PPS = pixelShader!,
                PDS = desc.DomainShader?.GetHandle()!,
                PHS = desc.HullShader?.GetHandle()!,
                PGS = desc.GeometryShader?.GetHandle()!,
                GraphicsPipelineNumRenderTargets = (byte)desc.Output.NumRenderTargets,
                GraphicsPipelineNumViewports = 1,
                GraphicsPipelinePrimitiveTopology = ToNative(desc.PrimitiveType),
                GraphicsPipelineDSVFormat = desc.Output.DepthStencilFormat,
                GraphicsPipelineSmplDescCount = (byte)desc.Output.MultiSample,
                GraphicsPipelineReadOnlyDSV = desc.ReadOnlyDepth,
                GraphicsPipelineBlendDescAlphaToCoverageEnable = desc.AlphaToCoverageEnabled,
                GraphicsPipelineDepthStencilDescDepthEnable = true,
                GraphicsPipelineDepthStencilDescDepthWriteEnable = desc.DepthWriteEnabled,
                GraphicsPipelineDepthStencilDescDepthFunc = ToNative(desc.DepthCompareFunction),
                GraphicsPipelineDepthStencilDescStencilEnable = desc.StencilTestEnabled,
                GraphicsPipelineDepthStencilDescStencilReadMask = (byte)desc.StencilCompareMask,
                GraphicsPipelineDepthStencilDescStencilWriteMask = (byte)desc.StencilWriteMask,
                GraphicsPipelineRasterizerDescFillMode = ToNative(desc.FillMode),
                GraphicsPipelineRasterizerDescCullMode = ToNative(desc.CullMode),
                GraphicsPipelineRasterizerDescFrontCounterClockwise = false,
                GraphicsPipelineRasterizerDescDepthBias = scaledDepthBias,
                GraphicsPipelineRasterizerDescSlopeScaledDepthBias = desc.SlopeScaledDepthBias,
                GraphicsPipelineRasterizerDescDepthClipEnable = true,
                GraphicsPipelineRasterizerDescScissorEnable = desc.ScissorTestEnabled,
                GraphicsPipelineRasterizerDescAntialiasedLineEnable = !isOpenGl && desc.LineAntiAlias
            };
            if (layout is not null)
            {
                createInfo.GraphicsPipelineInputLayoutLayoutElements = layout.First;
                createInfo.GraphicsPipelineInputLayoutNumElements = (uint)layout.Count;
            }
            for (var i = 0; i < desc.Output.NumRenderTargets; ++i)
                createInfo.GraphicsPipelineRTVFormats[i] = desc.Output.RenderTargetFormats[i];

            ConfigureBlendState(createInfo, desc.BlendMode, desc.ColorWriteEnabled);
            ConfigureStencilState(createInfo, desc);
            createInfo.PPSOCache = GetSubsystem<PipelineStateCache>()!.GetHandle()!;
            device.CreateGraphicsPipelineState(createInfo, out _handle);
            if (_handle is null || _handle.IsNull)
            {
                DestroyGPU();
                Log.GetLogger().LogError("Failed to create PipelineState '{Name}'", GetDebugName());
                return;
            }
            if (isOpenGl &&
                !DiligentNativeBindings.BindTemporaryGLProgramVertexAttributes(temporaryGlProgram, _handle))
            {
                DestroyGPU();
                Log.GetLogger().LogError(
                    "Failed to create PipelineState '{Name}' due to OpenGL program linking error",
                    GetDebugName());
                return;
            }
            FinishCreation();
        }
        finally
        {
            layout?.Dispose();
            if (temporaryGlProgram != 0)
                DiligentNativeBindings.DeleteTemporaryGLProgram(temporaryGlProgram);
        }
    }

    private void FinishCreation()
    {
        _handle!.CreateShaderResourceBinding(out _shaderResourceBinding, true);
        _reflection!.ConnectToShaderVariables(GetPipelineType(), _shaderResourceBinding!);
    }

    private void DestroyGPU()
    {
        ReleaseIfNotNull(_handle);
        ReleaseIfNotNull(_shaderResourceBinding);
        _handle = null;
        _shaderResourceBinding = null;
        _reflection = null;
    }

    private static void ConfigureStencilState(GraphicsPipelineStateCreateInfo ci, GraphicsPipelineStateDesc desc)
    {
        var fail = ToNative(desc.StencilOperationOnStencilFailed);
        var depthFail = ToNative(desc.StencilOperationOnDepthFailed);
        var pass = ToNative(desc.StencilOperationOnPassed);
        var compare = ToNative(desc.StencilCompareFunction);
        ci.GraphicsPipelineDepthStencilDescFrontFaceStencilFailOp = fail;
        ci.GraphicsPipelineDepthStencilDescFrontFaceStencilDepthFailOp = depthFail;
        ci.GraphicsPipelineDepthStencilDescFrontFaceStencilPassOp = pass;
        ci.GraphicsPipelineDepthStencilDescFrontFaceStencilFunc = compare;
        ci.GraphicsPipelineDepthStencilDescBackFaceStencilFailOp = fail;
        ci.GraphicsPipelineDepthStencilDescBackFaceStencilDepthFailOp = depthFail;
        ci.GraphicsPipelineDepthStencilDescBackFaceStencilPassOp = pass;
        ci.GraphicsPipelineDepthStencilDescBackFaceStencilFunc = compare;
    }

    private static void ConfigureBlendState(GraphicsPipelineStateCreateInfo ci, BlendMode mode, bool colorWrite)
    {
        using var blend = new RenderTargetBlendDesc
        {
            BlendEnable = mode != BlendMode.Replace,
            SrcBlend = mode switch { BlendMode.Multiply => BlendFactor.BlendFactorDestColor, BlendMode.Alpha or BlendMode.AddAlpha or BlendMode.SubtractAlpha or BlendMode.Decal => BlendFactor.BlendFactorSrcAlpha, _ => BlendFactor.BlendFactorOne },
            DestBlend = mode switch { BlendMode.Replace or BlendMode.Multiply => BlendFactor.BlendFactorZero, BlendMode.Alpha or BlendMode.PremulAlpha or BlendMode.Decal => BlendFactor.BlendFactorInvSrcAlpha, BlendMode.InvDestAlpha => BlendFactor.BlendFactorDestAlpha, _ => BlendFactor.BlendFactorOne },
            SrcBlendAlpha = mode switch { BlendMode.Multiply => BlendFactor.BlendFactorDestColor, BlendMode.Alpha or BlendMode.AddAlpha or BlendMode.SubtractAlpha => BlendFactor.BlendFactorSrcAlpha, BlendMode.Decal => BlendFactor.BlendFactorZero, _ => BlendFactor.BlendFactorOne },
            DestBlendAlpha = mode switch { BlendMode.Replace or BlendMode.Multiply => BlendFactor.BlendFactorZero, BlendMode.Alpha or BlendMode.PremulAlpha => BlendFactor.BlendFactorInvSrcAlpha, BlendMode.InvDestAlpha => BlendFactor.BlendFactorDestAlpha, _ => BlendFactor.BlendFactorOne },
            BlendOp = mode is BlendMode.Subtract or BlendMode.SubtractAlpha ? BlendOperation.BlendOperationRevSubtract : BlendOperation.BlendOperationAdd,
            BlendOpAlpha = mode is BlendMode.Subtract or BlendMode.SubtractAlpha ? BlendOperation.BlendOperationRevSubtract : BlendOperation.BlendOperationAdd,
            RenderTargetWriteMask = colorWrite ? ColorMask.ColorMaskAll : ColorMask.ColorMaskNone
        };
        using var empty = new RenderTargetBlendDesc();
        using var targets = new global::SharpCpp.NativeArray<RenderTargetBlendDesc>([blend, empty, empty, empty, empty, empty, empty, empty]);
        ci.GraphicsPipelineBlendDescIndependentBlendEnable = false;
        ci.GraphicsPipelineBlendDescRenderTargets = targets;
    }

    private static PrimitiveTopology ToNative(PrimitiveType value) => value switch
    {
        PrimitiveType.TriangleList => PrimitiveTopology.PrimitiveTopologyTriangleList,
        PrimitiveType.LineList => PrimitiveTopology.PrimitiveTopologyLineList,
        PrimitiveType.PointList => PrimitiveTopology.PrimitiveTopologyPointList,
        PrimitiveType.TriangleStrip => PrimitiveTopology.PrimitiveTopologyTriangleStrip,
        PrimitiveType.LineStrip => PrimitiveTopology.PrimitiveTopologyLineStrip,
        _ => PrimitiveTopology.PrimitiveTopologyUndefined
    };
    private static ComparisonFunction ToNative(CompareMode value) => value switch
    {
        CompareMode.Always => ComparisonFunction.ComparisonFuncAlways, CompareMode.Equal => ComparisonFunction.ComparisonFuncEqual,
        CompareMode.NotEqual => ComparisonFunction.ComparisonFuncNotEqual, CompareMode.Less => ComparisonFunction.ComparisonFuncLess,
        CompareMode.LessEqual => ComparisonFunction.ComparisonFuncLessEqual, CompareMode.Greater => ComparisonFunction.ComparisonFuncGreater,
        CompareMode.GreaterEqual => ComparisonFunction.ComparisonFuncGreaterEqual, _ => ComparisonFunction.ComparisonFuncUnknown
    };

    private static RedBolt.Bindings.Diligent.StencilOp ToNative(StencilOp value) => value switch
    {
        StencilOp.Keep => RedBolt.Bindings.Diligent.StencilOp.StencilOpKeep, StencilOp.Zero => RedBolt.Bindings.Diligent.StencilOp.StencilOpZero,
        StencilOp.Ref => RedBolt.Bindings.Diligent.StencilOp.StencilOpReplace, StencilOp.Incr => RedBolt.Bindings.Diligent.StencilOp.StencilOpIncrWrap,
        StencilOp.Decr => RedBolt.Bindings.Diligent.StencilOp.StencilOpDecrWrap, _ => RedBolt.Bindings.Diligent.StencilOp.StencilOpUndefined
    };
    private static RedBolt.Bindings.Diligent.FillMode ToNative(FillMode value) => value == FillMode.Solid ? RedBolt.Bindings.Diligent.FillMode.FillModeSolid : RedBolt.Bindings.Diligent.FillMode.FillModeWireframe;
    private static RedBolt.Bindings.Diligent.CullMode ToNative(CullMode value) => value switch
    {
        CullMode.None => RedBolt.Bindings.Diligent.CullMode.CullModeNone, CullMode.CounterClockwise => RedBolt.Bindings.Diligent.CullMode.CullModeBack,
        CullMode.Clockwise => RedBolt.Bindings.Diligent.CullMode.CullModeFront, _ => RedBolt.Bindings.Diligent.CullMode.CullModeUndefined
    };

    private static (uint Components, RedBolt.Bindings.Diligent.ValueType Type, bool Normalized) GetElementType(VertexElementType type) => type switch
    {
        VertexElementType.Int => (1, RedBolt.Bindings.Diligent.ValueType.VtInt32, false), VertexElementType.Float => (1, RedBolt.Bindings.Diligent.ValueType.VtFloat32, false),
        VertexElementType.Vector2 => (2, RedBolt.Bindings.Diligent.ValueType.VtFloat32, false), VertexElementType.Vector3 => (3, RedBolt.Bindings.Diligent.ValueType.VtFloat32, false),
        VertexElementType.Vector4 => (4, RedBolt.Bindings.Diligent.ValueType.VtFloat32, false), VertexElementType.UByte4 => (4, RedBolt.Bindings.Diligent.ValueType.VtUint8, false),
        VertexElementType.UByte4Norm => (4, RedBolt.Bindings.Diligent.ValueType.VtUint8, true), _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    private sealed class TemporaryLayoutArray : IDisposable
    {
        private readonly nint _memory;
        private readonly CString _semantic = new("ATTRIB");
        public TemporaryLayoutArray(ReadOnlySpan<InputLayoutElementDesc> elements,
            IReadOnlyList<VertexShaderAttribute> attributes)
        {
            var inputIndices = new int[elements.Length];
            Array.Fill(inputIndices, -1);
            foreach (var attribute in attributes)
            {
                var isFound = false;
                for (var i = elements.Length - 1; i >= 0; --i)
                {
                    if (elements[i].ElementSemantic == attribute.Semantic &&
                        elements[i].ElementSemanticIndex == attribute.SemanticIndex)
                    {
                        inputIndices[i] = attribute.InputIndex;
                        isFound = true;
                        break;
                    }
                }

                if (!isFound)
                {
                    Log.GetLogger().LogError(
                        "Attribute #{Index} with semantics '{Semantic}{SemanticIndex}' is not found in the vertex layout",
                        attribute.InputIndex, RenderAPIUtils.ToShaderInputName(attribute.Semantic),
                        attribute.SemanticIndex);
                }
            }
            Count = 0;
            foreach (var inputIndex in inputIndices)
            {
                if (inputIndex != -1)
                    ++Count;
            }
            var elementSize = (int)DiligentNativeBindings.GetEngineFactoryOpenGL().GetAPIInfo().LayoutElementSize;
            _memory = Marshal.AllocHGlobal((int)Count * elementSize);
            var destinationIndex = 0;
            for (var i = 0; i < elements.Length; ++i)
            {
                if (inputIndices[i] == -1)
                    continue;
                using var native = LayoutElement.CreateManagedWrapper(
                    _memory + destinationIndex++ * elementSize, false);
                var source = elements[i];
                var type = GetElementType(source.ElementType);
                native.HLSLSemantic = _semantic.Pointer; native.InputIndex = checked((uint)inputIndices[i]); native.BufferSlot = checked((uint)source.BufferIndex);
                native.NumComponents = type.Components; native.ValueType = type.Type; native.IsNormalized = type.Normalized;
                native.RelativeOffset = checked((uint)source.ElementOffset); native.Stride = checked((uint)source.BufferStride);
                native.Frequency = source.InstanceStepRate != 0 ? InputElementFrequency.InputElementFrequencyPerInstance : InputElementFrequency.InputElementFrequencyPerVertex;
                native.InstanceDataStepRate = checked((uint)source.InstanceStepRate);
            }
            First = LayoutElement.CreateManagedWrapper(_memory, false);
        }
        public LayoutElement First { get; }
        public int Count { get; }
        public void Dispose()
        {
            First.Dispose();
            _semantic.Dispose();
            Marshal.FreeHGlobal(_memory);
        }
    }

    private sealed class TemporaryImmutableSamplers : IDisposable
    {
        private readonly CString[] _names;
        private readonly SamplerDesc[] _descriptions;
        private readonly ImmutableSamplerDesc[] _samplers;
        private readonly IntPtr _memory;

        public TemporaryImmutableSamplers(ImmutableSamplersDesc overrides, ShaderProgramReflection reflection,
            RenderDevice renderDevice, RedBolt.Bindings.Diligent.ShaderType stages)
        {
            var resources = reflection.ShaderResources.ToArray();
            Count = resources.Length;
            _names = new CString[resources.Length];
            _descriptions = new SamplerDesc[resources.Length];
            _samplers = new ImmutableSamplerDesc[resources.Length];
            if (resources.Length == 0)
                return;

            var immutableSamplerDescSize = (int)DiligentNativeBindings.GetEngineFactoryOpenGL().GetAPIInfo().ImmutableSamplerDescSize;
            _memory = Marshal.AllocHGlobal(checked(immutableSamplerDescSize * resources.Length));
            for (var i = 0; i < resources.Length; ++i)
            {
                var pair = resources[i];
                var source = FindSampler(overrides, pair.Key);
                if (source is null)
                {
                    source = SamplerStateDesc.Bilinear();
                    Log.GetLogger().LogWarning(
                        "Default sampler is used for resource '{Name}'", pair.Value.InternalName);
                }
                _names[i] = new CString(pair.Value.InternalName);
                _descriptions[i] = CreateSamplerDesc(source.Value, renderDevice);
                _samplers[i] = ImmutableSamplerDesc.CreateManagedWrapper(
                    IntPtr.Add(_memory, immutableSamplerDescSize * i), false);
                _samplers[i].ShaderStages = stages;
                _samplers[i].SamplerOrTextureName = _names[i].Pointer;
                _samplers[i].Desc = _descriptions[i];
            }
            First = _samplers[0];
        }

        public ImmutableSamplerDesc? First { get; }
        public int Count { get; }

        public void Dispose()
        {
            foreach (var sampler in _samplers) sampler?.Dispose();
            foreach (var description in _descriptions) description?.Dispose();
            foreach (var name in _names) name?.Dispose();
            if (_memory != IntPtr.Zero)
                Marshal.FreeHGlobal(_memory);
        }

        private static SamplerStateDesc? FindSampler(ImmutableSamplersDesc overrides, StringHash name)
        {
            for (var i = 0; i < overrides.Size; ++i)
            {
                if (overrides.Names[i] == name)
                    return overrides.Descriptions[i];
            }
            return null;
        }

        private static SamplerDesc CreateSamplerDesc(SamplerStateDesc source, RenderDevice renderDevice)
        {
            var filter = source.FilterMode == TextureFilterMode.Default
                ? renderDevice.GetDefaultTextureFilterMode()
                : source.FilterMode;
            var compareIndex = source.ShadowCompare ? 1 : 0;
            var minMag = filter switch
            {
                TextureFilterMode.Nearest or TextureFilterMode.NearestAnisotropic => FilterType.FilterTypePoint,
                TextureFilterMode.Anisotropic => FilterType.FilterTypeAnisotropic,
                _ => FilterType.FilterTypeLinear
            };
            var mip = filter switch
            {
                TextureFilterMode.Nearest or TextureFilterMode.Bilinear => FilterType.FilterTypePoint,
                TextureFilterMode.Anisotropic => FilterType.FilterTypeAnisotropic,
                _ => FilterType.FilterTypeLinear
            };
            if (compareIndex != 0)
            {
                minMag = minMag switch
                {
                    FilterType.FilterTypePoint => FilterType.FilterTypeComparisonPoint,
                    FilterType.FilterTypeAnisotropic => FilterType.FilterTypeComparisonAnisotropic,
                    _ => FilterType.FilterTypeComparisonLinear
                };
                if (filter != TextureFilterMode.NearestAnisotropic)
                {
                    mip = mip switch
                    {
                        FilterType.FilterTypePoint => FilterType.FilterTypeComparisonPoint,
                        FilterType.FilterTypeAnisotropic => FilterType.FilterTypeComparisonAnisotropic,
                        _ => FilterType.FilterTypeComparisonLinear
                    };
                }
            }
            return new SamplerDesc
            {
                MinFilter = minMag,
                MagFilter = minMag,
                MipFilter = mip,
                AddressU = ToNative(source[TextureCoordinate.U]),
                AddressV = ToNative(source[TextureCoordinate.V]),
                AddressW = ToNative(source[TextureCoordinate.W]),
                MaxAnisotropy = source.Anisotropy != 0
                    ? source.Anisotropy
                    : (uint)renderDevice.GetDefaultTextureAnisotropy(),
                ComparisonFunc = ComparisonFunction.ComparisonFuncLessEqual,
                MinLOD = source.MinLod,
                MaxLOD = source.MaxLod
            };
        }

        private static RedBolt.Bindings.Diligent.TextureAddressMode ToNative(TextureAddressMode value) => value switch
        {
            TextureAddressMode.Wrap => RedBolt.Bindings.Diligent.TextureAddressMode.TextureAddressWrap,
            TextureAddressMode.Mirror => RedBolt.Bindings.Diligent.TextureAddressMode.TextureAddressMirror,
            TextureAddressMode.Clamp => RedBolt.Bindings.Diligent.TextureAddressMode.TextureAddressClamp,
            _ => RedBolt.Bindings.Diligent.TextureAddressMode.TextureAddressUnknown
        };
    }

    private static void ReleaseIfNotNull(IObject? value) { if (value is { IsNull: false }) _ = value.Release(); }
}
