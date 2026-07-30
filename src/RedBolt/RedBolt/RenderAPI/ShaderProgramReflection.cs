namespace RedBolt.RenderAPI;

using SystemMath = global::System.Math;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using RedBolt.Bindings.Diligent;
using RedBolt.Container;
using RedBolt.Graphics;
using RedBolt.Logging;
using RedBolt.RenderPipeline;

/// <summary>
/// Description of uniform buffer used by the shader program.
/// </summary>
public sealed class UniformBufferReflection : IDisposable
{
    private readonly List<IShaderResourceVariable> _variables = [];

    public int Size { get; internal set; }

    public uint Hash { get; internal set; }

    public CString InternalName { get; internal set; } = new();

    public IReadOnlyList<IShaderResourceVariable> Variables => _variables;

    internal void AddVariable(IShaderResourceVariable variable)
    {
        _variables.Add(variable);
    }

    public void Dispose()
    {
        InternalName.Dispose();
    }
}

/// <summary>
/// Description of specific uniform in the shader program.
/// </summary>
public readonly record struct UniformReflection(
    StringHash NameHash,
    ShaderParameterGroup Group,
    int Offset,
    int Size,
    ShaderCodeVariableClass VariableClass,
    ShaderCodeBasicType BasicType,
    int NumRows,
    int NumColumns,
    int ArraySize);

/// <summary>
/// Description of resource used by the shader program, excluding uniform buffers and samplers.
/// </summary>
public sealed class ShaderResourceReflection : IDisposable
{
    public CString InternalName { get; internal set; } = new();

    public IShaderResourceVariable? Variable { get; internal set; }

    public void Dispose()
    {
        InternalName.Dispose();
    }
}

/// <summary>
/// Description of shader program: uniform buffers, resources, etc.
/// </summary>
public sealed class ShaderProgramReflection : IDisposable
{
    private readonly UniformBufferReflection?[] _uniformBuffers = new UniformBufferReflection?[RenderApiLimits.MaxShaderParameterGroups];
    private readonly Dictionary<StringHash, UniformReflection> _uniforms = [];
    private readonly Dictionary<StringHash, ShaderResourceReflection> _shaderResources = [];
    private readonly Dictionary<StringHash, ShaderResourceReflection> _unorderedAccessViews = [];
    private readonly Dictionary<StringHash, string> _uniformNames = [];

/// <summary>
/// Create reflection from shaders.
/// It works only for GAPIs that can provide per-shader reflection data (this is everyone but old OpenGL).
/// </summary>
public ShaderProgramReflection(IEnumerable<RawShader?> shaders)
    {
        ArgumentNullException.ThrowIfNull(shaders);

        try
        {
            foreach (var shader in shaders)
            {
                if (shader?.GetHandle() is { IsNull: false } handle)
                    ReflectNativeShader(handle);
            }

            RecalculateUniformHash();
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public ShaderProgramReflection(nint temporaryGlProgram)
    {
        try
        {
            var indexToGroup = new List<ShaderParameterGroup>();
            var numUniformBlocks = DiligentNativeBindings.GetTemporaryGLProgramUniformBlockCount(temporaryGlProgram);
            for (var uniformBlockIndex = 0; uniformBlockIndex < numUniformBlocks; ++uniformBlockIndex)
            {
                var internalName =
                    DiligentNativeBindings.GetTemporaryGLProgramUniformBlockName(temporaryGlProgram, (uint)uniformBlockIndex);
                if (!TryParseConstantBufferName(internalName, out var group))
                {
                    Log.GetLogger().Warning("Unknown constant buffer '{0}' is ignored",
                        System.Text.Encoding.UTF8.GetString(internalName));
                    continue;
                }

                AddUniformBuffer(group, internalName,
                    checked((int)DiligentNativeBindings.GetTemporaryGLProgramUniformBlockDataSize(temporaryGlProgram, (uint)uniformBlockIndex)));

                var blockIndex = DiligentNativeBindings.GetTemporaryGLProgramUniformBlockIndex(
                    temporaryGlProgram, internalName);
                while (indexToGroup.Count <= blockIndex)
                    indexToGroup.Add((ShaderParameterGroup)RenderApiLimits.MaxShaderParameterGroups);
                indexToGroup[(int)blockIndex] = group;
            }

            var numUniforms = DiligentNativeBindings.GetTemporaryGLProgramUniformCount(temporaryGlProgram);
            for (var uniformIndex = 0; uniformIndex < numUniforms; ++uniformIndex)
            {
                var internalName = System.Text.Encoding.UTF8.GetString(
                    DiligentNativeBindings.GetTemporaryGLProgramUniformName(temporaryGlProgram, (uint)uniformIndex));
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
                if (!TrySanitizeGlUniformName(internalName, out var uniformName))
                    continue;

                var blockIndex = DiligentNativeBindings.GetTemporaryGLProgramUniformBlockIndex(
                    temporaryGlProgram, (uint)uniformIndex);
                var blockOffset = DiligentNativeBindings.GetTemporaryGLProgramUniformBlockOffset(
                    temporaryGlProgram, (uint)uniformIndex);
                if (blockIndex >= 0 && blockIndex < indexToGroup.Count)
                {
                    var group = indexToGroup[blockIndex];
                    if (group != (ShaderParameterGroup)RenderApiLimits.MaxShaderParameterGroups)
                    {
                        AddUniform(uniformName, group, blockOffset,
                            checked((int)DiligentNativeBindings.GetTemporaryGLProgramUniformSize(temporaryGlProgram, (uint)uniformIndex)),
                            default, default, 0, 0, 0);
                    }
                }
            }
            RecalculateUniformHash();
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public UniformBufferReflection? GetUniformBuffer(ShaderParameterGroup group)
    {
        var index = (int)group;
        if (index < 0 || index >= _uniformBuffers.Length || _uniformBuffers[index]?.Size == 0)
            return null;
        return _uniformBuffers[index];
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

    public IReadOnlyDictionary<StringHash, UniformReflection> Uniforms => _uniforms;

    public IReadOnlyDictionary<StringHash, ShaderResourceReflection> ShaderResources => _shaderResources;

    public IReadOnlyDictionary<StringHash, ShaderResourceReflection> UnorderedAccessViews => _unorderedAccessViews;

    internal string GetUniformName(StringHash name) => _uniformNames[name];

    public void Dispose()
    {
        foreach (var uniformBuffer in _uniformBuffers)
            uniformBuffer?.Dispose();
        foreach (var resource in _shaderResources.Values)
            resource.Dispose();
        foreach (var resource in _unorderedAccessViews.Values)
            resource.Dispose();
    }

    public void ConnectToShaderVariables(PipelineStateType pipelineType, IShaderResourceBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);

        var shaderTypes = GetShaderTypes(pipelineType);

        foreach (var uniformBuffer in _uniformBuffers)
        {
            if (uniformBuffer is null || uniformBuffer.Size == 0)
                continue;

            unsafe
            {
                ref var internalNameRef = ref Unsafe.AsRef<sbyte>((void*)uniformBuffer.InternalName.Pointer);
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
            unsafe
            {
                ref var internalNameRef = ref Unsafe.AsRef<sbyte>((void*)resource.InternalName.Pointer);
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
            unsafe
            {
                ref var internalNameRef = ref Unsafe.AsRef<sbyte>((void*)resource.InternalName.Pointer);
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

    private void ReflectNativeShader(IShader shader)
    {
        var numResources = shader.GetResourceCount();
        for (var resourceIndex = 0; resourceIndex < numResources; ++resourceIndex)
        {
            using var resourceDesc = new RedBolt.Bindings.Diligent.ShaderResourceDesc();
            shader.GetResourceDesc((uint)resourceIndex, resourceDesc);

            var internalName = Marshal.PtrToStringUTF8(resourceDesc.Name) ?? string.Empty;
            switch (resourceDesc.Type)
            {
                case ShaderResourceType.ShaderResourceTypeConstantBuffer:
                    using (var bufferDesc = shader.GetConstantBufferDesc((uint)resourceIndex))
                        ReflectUniformBuffer(internalName, bufferDesc);
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

    private void AddUniformBuffer(ShaderParameterGroup group, string internalName, int size)
    {
        var index = (int)group;
        if (_uniformBuffers[index] is { } existingBuffer)
        {
            if (existingBuffer.Size != size)
                Log.GetLogger().Warning("Uniform buffer #{0} has inconsistent size in different stages", group);
            if (!existingBuffer.InternalName.Equals(internalName))
                Log.GetLogger().Warning("Uniform buffer #{0} has inconsistent name in different stages", group);
            return;
        }

        _uniformBuffers[index] = new UniformBufferReflection
        {
            InternalName = new CString(internalName),
            Size = size
        };
    }

    private void AddUniformBuffer(ShaderParameterGroup group, ReadOnlySpan<byte> internalName, int size)
    {
        var index = (int)group;
        if (_uniformBuffers[index] is { } existingBuffer)
        {
            if (existingBuffer.Size != size)
                Log.GetLogger().Warning("Uniform buffer #{0} has inconsistent size in different stages", group);
            if (!existingBuffer.InternalName.Equals(internalName))
                Log.GetLogger().Warning("Uniform buffer #{0} has inconsistent name in different stages", group);
            return;
        }

        _uniformBuffers[index] = new UniformBufferReflection
        {
            InternalName = new CString(internalName),
            Size = size
        };
    }

    private void ReflectUniformBuffer(string internalName, ShaderCodeBufferDesc bufferDesc)
    {
        if (!TryParseConstantBufferName(internalName, out var group))
        {
            Log.GetLogger().Warning("Unknown constant buffer '{0}' is ignored", internalName);
            return;
        }

        AddUniformBuffer(group, internalName, checked((int)bufferDesc.Size));

        if (bufferDesc.NumVariables == 0u)
            return;

        for (var variableIndex = 0; variableIndex < bufferDesc.NumVariables; ++variableIndex)
        {
            using var variable = bufferDesc.GetVariableAt((uint)variableIndex);
            ReflectVariable(group, variable);
        }
    }

    private void ReflectVariable(ShaderParameterGroup group, ShaderCodeVariableDesc variable)
    {
        if (!TrySanitizeUniformName(variable.Name, out var name))
        {
            Log.GetLogger().Warning("Cannot parse uniform with name '{0}'", Marshal.PtrToStringUTF8(variable.Name) ?? string.Empty);
            return;
        }

        var size = GetUniformSize(variable);
        if (size == 0)
        {
            Log.GetLogger().Warning("Cannot deduce the size of the uniform '{0}'", name);
            return;
        }

        AddUniform(name, group, checked((int)variable.Offset), size, variable.Class, variable.BasicType,
            variable.NumRows, variable.NumColumns, checked((int)variable.ArraySize));
    }

    private void AddUniform(string name, ShaderParameterGroup group, int offset, int size,
        ShaderCodeVariableClass variableClass, ShaderCodeBasicType basicType, int numRows, int numColumns, int arraySize)
    {
        var nameHash = new StringHash(name);
        if (_uniforms.TryGetValue(nameHash, out var existingUniform))
        {
            if (existingUniform.Size != size)
                Log.GetLogger().Warning("Uniform '{0}' has inconsistent size in different stages", name);
            if (existingUniform.Offset != offset)
                Log.GetLogger().Warning("Uniform '{0}' has inconsistent offset in different stages", name);
            if (existingUniform.Group != group)
                Log.GetLogger().Warning("Uniform '{0}' has inconsistent owner in different stages", name);
            return;
        }

        _uniforms[nameHash] = new UniformReflection(nameHash, group, offset, size, variableClass, basicType,
            numRows, numColumns, arraySize);
        _uniformNames[nameHash] = name;
    }

    private void AddShaderResource(StringHash name, string internalName)
    {
        if (_shaderResources.ContainsKey(name))
        {
            Log.GetLogger().Warning("Shader resource '{0}' is referenced by multiple shader stages", internalName);
            return;
        }

        _shaderResources[name] = new ShaderResourceReflection
        {
            InternalName = new CString(internalName)
        };
    }

    private void AddUnorderedAccessView(StringHash name, string internalName)
    {
        if (_unorderedAccessViews.ContainsKey(name))
        {
            Log.GetLogger().Warning("Unordered access view '{0}' is referenced by multiple shader stages", internalName);
            return;
        }

        _unorderedAccessViews[name] = new ShaderResourceReflection
        {
            InternalName = new CString(internalName)
        };
    }

    private void RecalculateUniformHash()
    {
        foreach (var uniformBuffer in _uniformBuffers)
        {
            if (uniformBuffer is null)
                continue;

            uniformBuffer.Hash = 0u;
            if (uniformBuffer.Size != 0)
                uniformBuffer.Hash = CombineHash(uniformBuffer.Hash, checked((uint)uniformBuffer.Size));
        }

        foreach (var (nameHash, uniform) in _uniforms)
        {
            var uniformBuffer = GetUniformBuffer(uniform.Group);
            if (uniformBuffer is null)
                continue;

            uniformBuffer.Hash = CombineHash(uniformBuffer.Hash, nameHash.Value);
            uniformBuffer.Hash = CombineHash(uniformBuffer.Hash, checked((uint)uniform.Offset));
            uniformBuffer.Hash = CombineHash(uniformBuffer.Hash, checked((uint)uniform.Size));
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

    private static bool TryParseConstantBufferName(ReadOnlySpan<byte> name, out ShaderParameterGroup group)
    {
        group = name switch
        {
            _ when name.SequenceEqual("Frame"u8) => ShaderParameterGroup.Frame,
            _ when name.SequenceEqual("Camera"u8) => ShaderParameterGroup.Camera,
            _ when name.SequenceEqual("Zone"u8) => ShaderParameterGroup.Zone,
            _ when name.SequenceEqual("Light"u8) => ShaderParameterGroup.Light,
            _ when name.SequenceEqual("Material"u8) => ShaderParameterGroup.Material,
            _ when name.SequenceEqual("Object"u8) => ShaderParameterGroup.Object,
            _ when name.SequenceEqual("Custom"u8) => ShaderParameterGroup.Custom,
            _ => (ShaderParameterGroup)(-1)
        };
        return (int)group >= 0;
    }

    private static bool TrySanitizeUniformName(nint namePointer, out string name)
    {
        var internalName = Marshal.PtrToStringUTF8(namePointer) ?? string.Empty;
        var constantPrefix = internalName.IndexOf('c');
        if (constantPrefix < 0 || constantPrefix + 1 == internalName.Length)
        {
            name = string.Empty;
            return false;
        }

        name = internalName[(constantPrefix + 1)..];
        return true;
    }

    private static bool TrySanitizeGlUniformName(string internalName, out string name)
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

    private static int GetUniformSize(ShaderCodeVariableDesc variable)
    {
        if (variable.ArraySize > 1u)
        {
            return variable.Class switch
            {
                ShaderCodeVariableClass.ShaderCodeVariableClassScalar
                    => GetVectorArrayUniformSize(variable.BasicType, 1, checked((int)variable.ArraySize)),
                ShaderCodeVariableClass.ShaderCodeVariableClassVector
                    => GetVectorArrayUniformSize(variable.BasicType, SystemMath.Max(variable.NumColumns, variable.NumRows), checked((int)variable.ArraySize)),
                ShaderCodeVariableClass.ShaderCodeVariableClassMatrixColumns
                    => GetVectorArrayUniformSize(variable.BasicType, variable.NumRows, checked((int)(variable.ArraySize * variable.NumColumns))),
                ShaderCodeVariableClass.ShaderCodeVariableClassMatrixRows
                    => GetVectorArrayUniformSize(variable.BasicType, variable.NumColumns, checked((int)(variable.ArraySize * variable.NumRows))),
                _ => 0
            };
        }

        return variable.Class switch
        {
            ShaderCodeVariableClass.ShaderCodeVariableClassScalar => GetScalarUniformSize(variable.BasicType),
            ShaderCodeVariableClass.ShaderCodeVariableClassVector => GetVectorUniformSize(variable.BasicType, SystemMath.Max(variable.NumColumns, variable.NumRows)),
            ShaderCodeVariableClass.ShaderCodeVariableClassMatrixColumns => GetVectorArrayUniformSize(variable.BasicType, variable.NumRows, variable.NumColumns),
            ShaderCodeVariableClass.ShaderCodeVariableClassMatrixRows => GetVectorArrayUniformSize(variable.BasicType, variable.NumColumns, variable.NumRows),
            _ => 0
        };
    }

    private static int GetScalarUniformSize(ShaderCodeBasicType basicType) => basicType switch
    {
        ShaderCodeBasicType.ShaderCodeBasicTypeInt64 or
        ShaderCodeBasicType.ShaderCodeBasicTypeUint64 or
        ShaderCodeBasicType.ShaderCodeBasicTypeDouble => sizeof(double),
        _ => sizeof(float)
    };

    private static int GetVectorUniformSize(ShaderCodeBasicType basicType, int numElements)
    {
        return numElements switch
        {
            1 => GetScalarUniformSize(basicType),
            2 => 2 * GetScalarUniformSize(basicType),
            3 => 3 * GetScalarUniformSize(basicType),
            4 => 4 * GetScalarUniformSize(basicType),
            _ => 0
        };
    }

    private static int GetVectorArrayUniformSize(ShaderCodeBasicType basicType, int numElements, int arraySize)
    {
        const int alignment = 4 * sizeof(float);
        var elementSize = GetVectorUniformSize(basicType, numElements);
        return arraySize * Align(elementSize, alignment);
    }

    private static int GetMatrixUniformSize(ShaderCodeBasicType basicType, int innerSize, int outerSize)
    {
        return outerSize * GetVectorUniformSize(basicType, innerSize);
    }

    private static int Align(int value, int alignment)
    {
        return (value + alignment - 1) / alignment * alignment;
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
