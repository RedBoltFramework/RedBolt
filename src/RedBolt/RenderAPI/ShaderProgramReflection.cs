using RedBolt.Bindings.Diligent;
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

        foreach (var shaderType in GetShaderTypes(pipelineType))
        {
            var variables = GetShaderVariables(binding, shaderType);
            foreach (var uniformBuffer in _uniformBuffers)
            {
                if (uniformBuffer is null || string.IsNullOrWhiteSpace(uniformBuffer.InternalName))
                    continue;

                if (variables.TryGetValue(uniformBuffer.InternalName, out var variable))
                    uniformBuffer.AddVariable(variable);
            }

            ConnectResources(_shaderResources, variables);
            ConnectResources(_unorderedAccessViews, variables);
        }
    }

    internal static ShaderProgramReflection FromDesc(PipelineStateDesc desc)
    {
        ArgumentNullException.ThrowIfNull(desc);

        var graphicsDesc = desc.AsGraphics();
        if (graphicsDesc is not null)
        {
            return new ShaderProgramReflection(
            [
                graphicsDesc.VertexShader,
                graphicsDesc.PixelShader,
                graphicsDesc.GeometryShader,
                graphicsDesc.HullShader,
                graphicsDesc.DomainShader
            ]);
        }

        var computeDesc = desc.AsCompute();
        return computeDesc is not null
            ? new ShaderProgramReflection([computeDesc.ComputeShader])
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

    private static Dictionary<string, IShaderResourceVariable> GetShaderVariables(IShaderResourceBinding binding, ShaderType shaderType)
    {
        var result = new Dictionary<string, IShaderResourceVariable>(StringComparer.Ordinal);
        var nativeShaderType = ToNativeShaderType(shaderType);
        var numVariables = binding.GetVariableCount(nativeShaderType);
        for (uint variableIndex = 0; variableIndex < numVariables; ++variableIndex)
        {
            var variable = binding.GetVariableByIndex(nativeShaderType, variableIndex);
            if (variable is null || variable.IsNull)
                continue;

            using var resourceDesc = new RedBolt.Bindings.Diligent.ShaderResourceDesc();
            variable.GetResourceDesc(resourceDesc);
            var internalName = Marshal.PtrToStringUTF8(resourceDesc.Name);
            if (!string.IsNullOrWhiteSpace(internalName) && !result.ContainsKey(internalName))
                result[internalName] = variable;
        }

        return result;
    }

    private static void ConnectResources(
        Dictionary<StringHash, ShaderResourceReflection> resources,
        IReadOnlyDictionary<string, IShaderResourceVariable> variables)
    {
        foreach (var resource in resources.Values)
        {
            if (resource.Variable is not null || string.IsNullOrWhiteSpace(resource.InternalName))
                continue;

            if (variables.TryGetValue(resource.InternalName, out var variable))
                resource.Variable = variable;
        }
    }

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
        if (string.IsNullOrEmpty(internalName))
        {
            name = string.Empty;
            return false;
        }

        var index = internalName.IndexOf('c');
        if (index < 0 || index + 1 >= internalName.Length)
        {
            name = string.Empty;
            return false;
        }

        name = internalName[(index + 1)..];
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
