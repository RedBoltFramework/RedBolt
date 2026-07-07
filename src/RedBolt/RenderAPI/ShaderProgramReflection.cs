using System.Runtime.InteropServices;
using RedBolt.Bindings.Diligent;

namespace RedBolt;

public sealed class UniformBufferReflection
{
    private readonly List<IShaderResourceVariable> variables = [];

    public uint Size { get; internal set; }

    public uint Hash { get; internal set; }

    public string InternalName { get; internal set; } = string.Empty;

    public IReadOnlyList<IShaderResourceVariable> Variables => variables;

    internal void AddVariable(IShaderResourceVariable variable)
    {
        ArgumentNullException.ThrowIfNull(variable);
        if (!variables.Contains(variable))
            variables.Add(variable);
    }
}

public readonly record struct UniformReflection(ShaderParameterGroup Group, uint Offset, uint Size);

public sealed class ShaderResourceReflection
{
    public string InternalName { get; internal set; } = string.Empty;

    public IShaderResourceVariable? Variable { get; internal set; }
}

public sealed class ShaderProgramReflection
{
    private readonly UniformBufferReflection?[] uniformBuffers = new UniformBufferReflection?[RenderApiLimits.MaxShaderParameterGroups];
    private readonly Dictionary<StringHash, UniformReflection> uniforms = [];
    private readonly Dictionary<StringHash, ShaderResourceReflection> shaderResources = [];
    private readonly Dictionary<StringHash, ShaderResourceReflection> unorderedAccessViews = [];

    public ShaderProgramReflection()
    {
    }

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

    public UniformBufferReflection? GetUniformBuffer(ShaderParameterGroup group)
    {
        var index = (int)group;
        return index >= 0 && index < uniformBuffers.Length ? uniformBuffers[index] : null;
    }

    public UniformReflection? GetUniform(StringHash name)
    {
        return uniforms.TryGetValue(name, out var uniform) ? uniform : null;
    }

    public ShaderResourceReflection? GetShaderResource(StringHash name)
    {
        return shaderResources.TryGetValue(name, out var shaderResource) ? shaderResource : null;
    }

    public ShaderResourceReflection? GetUnorderedAccessView(StringHash name)
    {
        return unorderedAccessViews.TryGetValue(name, out var unorderedAccessView) ? unorderedAccessView : null;
    }

    public IReadOnlyDictionary<StringHash, UniformReflection> GetUniforms() => uniforms;

    public IReadOnlyDictionary<StringHash, ShaderResourceReflection> GetShaderResources() => shaderResources;

    public IReadOnlyDictionary<StringHash, ShaderResourceReflection> GetUnorderedAccessViews() => unorderedAccessViews;

    public void ConnectToShaderVariables(PipelineStateType pipelineType, IShaderResourceBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);

        foreach (var shaderType in GetShaderTypes(pipelineType))
        {
            var variables = GetShaderVariables(binding, shaderType);
            foreach (var uniformBuffer in uniformBuffers)
            {
                if (uniformBuffer is null || string.IsNullOrWhiteSpace(uniformBuffer.InternalName))
                    continue;

                if (variables.TryGetValue(uniformBuffer.InternalName, out var variable))
                    uniformBuffer.AddVariable(variable);
            }

            ConnectResources(shaderResources, variables);
            ConnectResources(unorderedAccessViews, variables);
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
                        AddUniformBuffer(group, internalName, bufferDesc.Size);
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
        if (uniformBuffers[index] is { } existingBuffer)
        {
            if (existingBuffer.Size == 0 && size != 0)
                existingBuffer.Size = size;
            return;
        }

        uniformBuffers[index] = new UniformBufferReflection
        {
            InternalName = internalName,
            Size = size
        };
    }

    private void AddShaderResource(StringHash name, string internalName)
    {
        if (shaderResources.ContainsKey(name))
            return;

        shaderResources[name] = new ShaderResourceReflection
        {
            InternalName = internalName
        };
    }

    private void AddUnorderedAccessView(StringHash name, string internalName)
    {
        if (unorderedAccessViews.ContainsKey(name))
            return;

        unorderedAccessViews[name] = new ShaderResourceReflection
        {
            InternalName = internalName
        };
    }

    private void RecalculateUniformHash()
    {
        foreach (var uniformBuffer in uniformBuffers)
        {
            if (uniformBuffer is null)
                continue;

            uniformBuffer.Hash = uniformBuffer.Size != 0 ? CombineHash(0u, uniformBuffer.Size) : 0u;
            if (uniformBuffer.Hash == 0)
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
