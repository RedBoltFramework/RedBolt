using RedBolt.Bindings.Diligent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RedBolt;

/// <summary>
/// Description of uniform buffer used by the shader program.
/// </summary>
public sealed class UniformBufferReflection
{
    private readonly List<IShaderResourceVariable> _variables = [];

    public uint Size { get; internal set; }

    public uint Hash { get; internal set; }

    public string InternalName { get; internal set; } = string.Empty;

    public IReadOnlyList<IShaderResourceVariable> Variables => _variables;

    internal void AddVariable(IShaderResourceVariable variable)
    {
        ArgumentNullException.ThrowIfNull(variable);
        if (!_variables.Contains(variable))
            _variables.Add(variable);
    }
}

/// <summary>
/// Description of specific uniform in the shader program.
/// </summary>
public readonly record struct UniformReflection(
    string Name,
    ShaderParameterGroup Group,
    uint Offset,
    uint Size,
    ShaderCodeVariableClass VariableClass,
    ShaderCodeBasicType BasicType,
    byte NumRows,
    byte NumColumns,
    uint ArraySize);

/// <summary>
/// Description of resource used by the shader program, excluding uniform buffers and samplers.
/// </summary>
public sealed class ShaderResourceReflection
{
    public string InternalName { get; internal set; } = string.Empty;

    public IShaderResourceVariable? Variable { get; internal set; }
}

/// <summary>
/// Description of shader program: uniform buffers, resources, etc.
/// </summary>
public sealed class ShaderProgramReflection
{
    private readonly UniformBufferReflection?[] _uniformBuffers = new UniformBufferReflection?[RenderApiLimits.MaxShaderParameterGroups];
    private readonly Dictionary<StringHash, UniformReflection> _uniforms = [];
    private readonly Dictionary<StringHash, ShaderResourceReflection> _shaderResources = [];
    private readonly Dictionary<StringHash, ShaderResourceReflection> _unorderedAccessViews = [];

    public ShaderProgramReflection()
    {
    }

/// <summary>
/// Create reflection from shaders.
/// It works only for GAPIs that can provide per-shader reflection data (this is everyone but old OpenGL).
/// </summary>
public ShaderProgramReflection(IEnumerable<RawShader?> shaders)
    {
        ArgumentNullException.ThrowIfNull(shaders);

        foreach (var shader in shaders)
        {
            if (shader?.GetHandle() is { IsNull: false } handle)
                ReflectNativeShader(handle);
        }

        RecalculateUniformHash();
    }

    /// <summary>
    /// Create reflection from shaders.
    /// It works only for GAPIs that can provide per-shader reflection data (this is everyone but old OpenGL).
    /// </summary>
    public ShaderProgramReflection(IEnumerable<IShader?> shaders)
    {
        ArgumentNullException.ThrowIfNull(shaders);

        foreach (var shader in shaders)
        {
            if (shader is { IsNull: false })
                ReflectNativeShader(shader);
        }

        RecalculateUniformHash();
    }

    internal ShaderProgramReflection(nint temporaryGlProgram)
    {
        var indexToGroup = new List<ShaderParameterGroup>();
        var numUniformBlocks = DiligentNativeBindings.GetTemporaryGLProgramUniformBlockCount(temporaryGlProgram);
        for (var uniformBlockIndex = 0u; uniformBlockIndex < numUniformBlocks; ++uniformBlockIndex)
        {
            var internalName = System.Text.Encoding.UTF8.GetString(
                DiligentNativeBindings.GetTemporaryGLProgramUniformBlockName(temporaryGlProgram, uniformBlockIndex));
            if (!TryParseConstantBufferName(internalName, out var group))
                continue;

            AddUniformBuffer(group, internalName,
                DiligentNativeBindings.GetTemporaryGLProgramUniformBlockDataSize(temporaryGlProgram, uniformBlockIndex));

            var blockIndex = DiligentNativeBindings.GetTemporaryGLProgramUniformBlockIndex(
                temporaryGlProgram, System.Text.Encoding.UTF8.GetBytes(internalName));
            while (indexToGroup.Count <= blockIndex)
                indexToGroup.Add((ShaderParameterGroup)RenderApiLimits.MaxShaderParameterGroups);
            indexToGroup[(int)blockIndex] = group;
        }

        var numUniforms = DiligentNativeBindings.GetTemporaryGLProgramUniformCount(temporaryGlProgram);
        for (var uniformIndex = 0u; uniformIndex < numUniforms; ++uniformIndex)
        {
            var internalName = System.Text.Encoding.UTF8.GetString(
                DiligentNativeBindings.GetTemporaryGLProgramUniformName(temporaryGlProgram, uniformIndex));
            if (TrySanitizeSrvName(internalName, out var resourceName))
            {
                AddShaderResource(resourceName, internalName);
                continue;
            }
            if (TrySanitizeUavName(internalName, out var unorderedAccessViewName))
            {
                AddUnorderedAccessView(unorderedAccessViewName, internalName);
                continue;
            }
            if (!TrySanitizeUniformName(internalName, out var uniformName))
                continue;

            var blockIndex = DiligentNativeBindings.GetTemporaryGLProgramUniformBlockIndex(
                temporaryGlProgram, uniformIndex);
            var blockOffset = DiligentNativeBindings.GetTemporaryGLProgramUniformBlockOffset(
                temporaryGlProgram, uniformIndex);
            if (blockIndex >= 0 && blockIndex < indexToGroup.Count)
            {
                var group = indexToGroup[blockIndex];
                if (group != (ShaderParameterGroup)RenderApiLimits.MaxShaderParameterGroups)
                {
                    AddUniform(new UniformReflection(uniformName, group, (uint)blockOffset,
                        DiligentNativeBindings.GetTemporaryGLProgramUniformSize(temporaryGlProgram, uniformIndex),
                        default, default, 0, 0, 0));
                }
            }
        }
        RecalculateUniformHash();
    }

    public UniformBufferReflection? GetUniformBuffer(ShaderParameterGroup group)
    {
        var index = (int)group;
        return index >= 0 && index < _uniformBuffers.Length ? _uniformBuffers[index] : null;
    }

    public UniformReflection? GetUniform(StringHash name)
    {
        return _uniforms.TryGetValue(name, out var uniform) ? uniform : null;
    }

    public ShaderResourceReflection? GetShaderResource(StringHash name)
    {
        return _shaderResources.TryGetValue(name, out var shaderResource) ? shaderResource : null;
    }

    public ShaderResourceReflection? GetUnorderedAccessView(StringHash name)
    {
        return _unorderedAccessViews.TryGetValue(name, out var unorderedAccessView) ? unorderedAccessView : null;
    }

    public IReadOnlyDictionary<StringHash, UniformReflection> GetUniforms() => _uniforms;

    public IReadOnlyDictionary<StringHash, ShaderResourceReflection> GetShaderResources() => _shaderResources;

    public IReadOnlyDictionary<StringHash, ShaderResourceReflection> GetUnorderedAccessViews() => _unorderedAccessViews;

    public void ConnectToShaderVariables(PipelineStateType pipelineType, IShaderResourceBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);

        var shaderTypes = GetShaderTypes(pipelineType);

        foreach (var uniformBuffer in _uniformBuffers)
        {
            if (uniformBuffer is null || uniformBuffer.Size == 0)
                continue;

            using var internalName = new CString(uniformBuffer.InternalName);
            unsafe
            {
                ref var internalNameRef = ref Unsafe.AsRef<sbyte>((void*)internalName.Pointer);
                foreach (var shaderType in shaderTypes)
                {
                    var shaderVariable = binding.GetVariableByName(ToNativeShaderType(shaderType), in internalNameRef);
                    if (shaderVariable is not null && !shaderVariable.IsNull)
                        uniformBuffer.AddVariable(shaderVariable);
                }
            }
        }

        foreach (var resource in _shaderResources.Values)
        {
            using var internalName = new CString(resource.InternalName);
            unsafe
            {
                ref var internalNameRef = ref Unsafe.AsRef<sbyte>((void*)internalName.Pointer);
                foreach (var shaderType in shaderTypes)
                {
                    var shaderVariable = binding.GetVariableByName(ToNativeShaderType(shaderType), in internalNameRef);
                    if (shaderVariable is not null && !shaderVariable.IsNull)
                    {
                        resource.Variable = shaderVariable;
                        break;
                    }
                }
            }
        }

        foreach (var resource in _unorderedAccessViews.Values)
        {
            using var internalName = new CString(resource.InternalName);
            unsafe
            {
                ref var internalNameRef = ref Unsafe.AsRef<sbyte>((void*)internalName.Pointer);
                foreach (var shaderType in shaderTypes)
                {
                    var shaderVariable = binding.GetVariableByName(ToNativeShaderType(shaderType), in internalNameRef);
                    if (shaderVariable is not null && !shaderVariable.IsNull)
                    {
                        resource.Variable = shaderVariable;
                        break;
                    }
                }
            }
        }
    }

    internal static ShaderProgramReflection FromDesc(PipelineStateDesc desc)
    {
        ArgumentNullException.ThrowIfNull(desc);

        var graphicsDesc = desc.AsGraphics();
        if (graphicsDesc is { } graphics)
        {
            return new ShaderProgramReflection(
            [
                graphics.VertexShader,
                graphics.PixelShader,
                graphics.GeometryShader,
                graphics.HullShader,
                graphics.DomainShader
            ]);
        }

        var computeDesc = desc.AsCompute();
        return computeDesc is { } compute
            ? new ShaderProgramReflection([compute.ComputeShader])
            : new ShaderProgramReflection();
    }

    private void ReflectNativeShader(IShader shader)
    {
        var numResources = shader.GetResourceCount();
        for (uint resourceIndex = 0; resourceIndex < numResources; ++resourceIndex)
        {
            using var resourceDesc = new RedBolt.Bindings.Diligent.ShaderResourceDesc();
            shader.GetResourceDesc(resourceIndex, resourceDesc);

            var internalName = Marshal.PtrToStringUTF8(resourceDesc.Name) ?? string.Empty;
            switch (resourceDesc.Type)
            {
                case ShaderResourceType.ShaderResourceTypeConstantBuffer:
                    if (TryParseConstantBufferName(internalName, out var group))
                    {
                        using var bufferDesc = shader.GetConstantBufferDesc(resourceIndex);
                        ReflectUniformBuffer(group, internalName, bufferDesc);
                    }
                    break;

                case ShaderResourceType.ShaderResourceTypeTextureSrv:
                    if (TrySanitizeSrvName(internalName, out var shaderResourceName))
                        AddShaderResource(shaderResourceName, internalName);
                    break;

                case ShaderResourceType.ShaderResourceTypeTextureUav:
                    if (TrySanitizeUavName(internalName, out var unorderedAccessViewName))
                        AddUnorderedAccessView(unorderedAccessViewName, internalName);
                    break;
            }
        }
    }

    private void AddUniformBuffer(ShaderParameterGroup group, string internalName, uint size)
    {
        var index = (int)group;
        if (_uniformBuffers[index] is { } existingBuffer)
        {
            if (existingBuffer.Size != 0 && size != 0 && existingBuffer.Size != size)
                return;
            if (existingBuffer.Size == 0 && size != 0)
                existingBuffer.Size = size;
            return;
        }

        _uniformBuffers[index] = new UniformBufferReflection
        {
            InternalName = internalName,
            Size = size
        };
    }

    private void ReflectUniformBuffer(ShaderParameterGroup group, string internalName, ShaderCodeBufferDesc bufferDesc)
    {
        AddUniformBuffer(group, internalName, bufferDesc.Size);

        if (bufferDesc.NumVariables == 0u)
            return;

        for (uint variableIndex = 0; variableIndex < bufferDesc.NumVariables; ++variableIndex)
        {
            using var variable = bufferDesc.GetVariableAt(variableIndex);
            ReflectVariable(group, variable);
        }
    }

    private void ReflectVariable(ShaderParameterGroup group, ShaderCodeVariableDesc variable)
    {
        if (!TrySanitizeUniformName(variable.Name, out var name))
            return;

        var size = GetUniformSize(variable);
        if (size == 0u)
            return;

        AddUniform(new UniformReflection(
            name,
            group,
            variable.Offset,
            size,
            variable.Class,
            variable.BasicType,
            variable.NumRows,
            variable.NumColumns,
            variable.ArraySize));
    }

    private void AddUniform(UniformReflection uniform)
    {
        var nameHash = new StringHash(uniform.Name);
        if (_uniforms.TryGetValue(nameHash, out var existingUniform))
            return;

        _uniforms[nameHash] = uniform;
    }

    private void AddShaderResource(StringHash name, string internalName)
    {
        if (_shaderResources.ContainsKey(name))
            return;

        _shaderResources[name] = new ShaderResourceReflection
        {
            InternalName = internalName
        };
    }

    private void AddUnorderedAccessView(StringHash name, string internalName)
    {
        if (_unorderedAccessViews.ContainsKey(name))
            return;

        _unorderedAccessViews[name] = new ShaderResourceReflection
        {
            InternalName = internalName
        };
    }

    private void RecalculateUniformHash()
    {
        foreach (var uniformBuffer in _uniformBuffers)
        {
            if (uniformBuffer is null)
                continue;

            uniformBuffer.Hash = 0u;
            if (uniformBuffer.Size != 0u)
                uniformBuffer.Hash = CombineHash(uniformBuffer.Hash, uniformBuffer.Size);
        }

        foreach (var (nameHash, uniform) in _uniforms)
        {
            var uniformBuffer = GetUniformBuffer(uniform.Group);
            if (uniformBuffer is null)
                continue;

            uniformBuffer.Hash = CombineHash(uniformBuffer.Hash, nameHash.Value);
            uniformBuffer.Hash = CombineHash(uniformBuffer.Hash, uniform.Offset);
            uniformBuffer.Hash = CombineHash(uniformBuffer.Hash, uniform.Size);
            if (uniformBuffer.Hash == 0u)
                uniformBuffer.Hash = 1u;
        }
    }

    private static IReadOnlyList<ShaderType> GetShaderTypes(PipelineStateType pipelineType) => pipelineType switch
    {
        PipelineStateType.Graphics => [ShaderType.VS, ShaderType.PS, ShaderType.GS, ShaderType.HS, ShaderType.DS],
        PipelineStateType.Compute => [ShaderType.CS],
        _ => throw new ArgumentOutOfRangeException(nameof(pipelineType), pipelineType, null)
    };

    private static RedBolt.Bindings.Diligent.ShaderType ToNativeShaderType(ShaderType shaderType) => shaderType switch
    {
        ShaderType.VS => RedBolt.Bindings.Diligent.ShaderType.ShaderTypeVertex,
        ShaderType.PS => RedBolt.Bindings.Diligent.ShaderType.ShaderTypePixel,
        ShaderType.GS => RedBolt.Bindings.Diligent.ShaderType.ShaderTypeGeometry,
        ShaderType.HS => RedBolt.Bindings.Diligent.ShaderType.ShaderTypeHull,
        ShaderType.DS => RedBolt.Bindings.Diligent.ShaderType.ShaderTypeDomain,
        ShaderType.CS => RedBolt.Bindings.Diligent.ShaderType.ShaderTypeCompute,
        _ => throw new ArgumentOutOfRangeException(nameof(shaderType), shaderType, null)
    };

    private static bool TryParseConstantBufferName(string name, out ShaderParameterGroup group)
    {
        group = name switch
        {
            "Frame" => ShaderParameterGroup.Frame,
            "Camera" => ShaderParameterGroup.Camera,
            "Zone" => ShaderParameterGroup.Zone,
            "Light" => ShaderParameterGroup.Light,
            "Material" => ShaderParameterGroup.Material,
            "Object" => ShaderParameterGroup.Object,
            "Custom" => ShaderParameterGroup.Custom,
            _ => (ShaderParameterGroup)(-1)
        };
        return (int)group >= 0;
    }

    private static bool TrySanitizeUniformName(nint namePointer, out string name)
    {
        var internalName = Marshal.PtrToStringUTF8(namePointer) ?? string.Empty;
        return TrySanitizeUniformName(internalName, out name);
    }

    private static bool TrySanitizeUniformName(string internalName, out string name)
    {
        if (string.IsNullOrEmpty(internalName))
        {
            name = string.Empty;
            return false;
        }

        var subscriptIndex = internalName.IndexOf('[');
        if (subscriptIndex >= 0)
        {
            if (internalName.IndexOf("[0]", subscriptIndex, StringComparison.Ordinal) < 0)
            {
                name = string.Empty;
                return false;
            }

            internalName = internalName[..subscriptIndex];
        }

        var dotIndex = internalName.IndexOf('.');
        if (dotIndex >= 0)
            internalName = internalName[(dotIndex + 1)..];

        if (internalName.Length == 0 || internalName[0] != 'c')
        {
            name = string.Empty;
            return false;
        }

        name = internalName[1..];
        return true;
    }

    private static uint GetUniformSize(ShaderCodeVariableDesc variable)
    {
        if (variable.ArraySize > 1u)
        {
            return variable.Class switch
            {
                ShaderCodeVariableClass.ShaderCodeVariableClassScalar
                    => GetVectorArrayUniformSize(variable.BasicType, 1u, variable.ArraySize),
                ShaderCodeVariableClass.ShaderCodeVariableClassVector
                    => GetVectorArrayUniformSize(variable.BasicType, Math.Max(variable.NumColumns, variable.NumRows), variable.ArraySize),
                ShaderCodeVariableClass.ShaderCodeVariableClassMatrixColumns
                    => GetVectorArrayUniformSize(variable.BasicType, variable.NumRows, variable.ArraySize * variable.NumColumns),
                ShaderCodeVariableClass.ShaderCodeVariableClassMatrixRows
                    => GetVectorArrayUniformSize(variable.BasicType, variable.NumColumns, variable.ArraySize * variable.NumRows),
                _ => 0u
            };
        }

        return variable.Class switch
        {
            ShaderCodeVariableClass.ShaderCodeVariableClassScalar => GetScalarUniformSize(variable.BasicType),
            ShaderCodeVariableClass.ShaderCodeVariableClassVector => GetVectorUniformSize(variable.BasicType, Math.Max(variable.NumColumns, variable.NumRows)),
            ShaderCodeVariableClass.ShaderCodeVariableClassMatrixColumns => GetVectorArrayUniformSize(variable.BasicType, variable.NumRows, variable.NumColumns),
            ShaderCodeVariableClass.ShaderCodeVariableClassMatrixRows => GetVectorArrayUniformSize(variable.BasicType, variable.NumColumns, variable.NumRows),
            _ => 0u
        };
    }

    private static uint GetScalarUniformSize(ShaderCodeBasicType basicType) => basicType switch
    {
        ShaderCodeBasicType.ShaderCodeBasicTypeInt64 or
        ShaderCodeBasicType.ShaderCodeBasicTypeUint64 or
        ShaderCodeBasicType.ShaderCodeBasicTypeDouble => sizeof(double),
        _ => sizeof(float)
    };

    private static uint GetVectorUniformSize(ShaderCodeBasicType basicType, uint numElements)
    {
        return numElements switch
        {
            1u => GetScalarUniformSize(basicType),
            2u => 2u * GetScalarUniformSize(basicType),
            3u => 3u * GetScalarUniformSize(basicType),
            4u => 4u * GetScalarUniformSize(basicType),
            _ => 0u
        };
    }

    private static uint GetVectorArrayUniformSize(ShaderCodeBasicType basicType, uint numElements, uint arraySize)
    {
        const uint alignment = 4u * sizeof(float);
        var elementSize = GetVectorUniformSize(basicType, numElements);
        return arraySize * Align(elementSize, alignment);
    }

    private static uint Align(uint value, uint alignment)
    {
        return (value + alignment - 1u) / alignment * alignment;
    }

    private static bool TrySanitizeSrvName(string internalName, out StringHash name)
    {
        if (internalName.Length < 2 || internalName[0] != 's')
        {
            name = default;
            return false;
        }

        name = new StringHash(internalName[1..]);
        return true;
    }

    private static bool TrySanitizeUavName(string internalName, out StringHash name)
    {
        if (internalName.Length < 2 || internalName[0] != 'u')
        {
            name = default;
            return false;
        }

        name = new StringHash(internalName[1..]);
        return true;
    }

    private static uint CombineHash(uint lhs, uint rhs) => unchecked(lhs * 37u + rhs + 17u);

}
