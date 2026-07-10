namespace RedBolt;

/// <summary>Shader resource group, range in array.</summary>
public readonly record struct ShaderResourceRange(uint First, uint Second);

/// <summary>Description of draw command.</summary>
public sealed class DrawCommandDescription
{
    public PipelineState? PipelineState { get; set; }

    public RawBuffer?[] VertexBuffers { get; } = new RawBuffer?[RenderApiLimits.MaxVertexStreams];

    public RawBuffer? IndexBuffer { get; set; }

    public ConstantBufferCollectionRef[] ConstantBuffers { get; } = new ConstantBufferCollectionRef[RenderApiLimits.MaxShaderParameterGroups];

    public ShaderResourceRange ShaderResources { get; set; }

    public ShaderResourceRange UnorderedAccessViews { get; set; }

    /// <summary>Index of scissor rectangle. 0 if disabled.</summary>
    public uint ScissorRect { get; set; }

    /// <summary>Stencil reference value.</summary>
    public uint StencilRef { get; set; }

    public uint IndexStart { get; set; }

    public uint IndexCount { get; set; }

    public uint BaseVertexIndex { get; set; }

    public uint InstanceStart { get; set; }

    public uint InstanceCount { get; set; }

    public Vector3i NumGroups { get; set; }

    public DrawCommandDescription()
    {
    }

    public DrawCommandDescription(DrawCommandDescription? other)
    {
        if (other is null)
            return;

        PipelineState = other.PipelineState;
        Array.Copy(other.VertexBuffers, VertexBuffers, other.VertexBuffers.Length);
        IndexBuffer = other.IndexBuffer;
        Array.Copy(other.ConstantBuffers, ConstantBuffers, other.ConstantBuffers.Length);
        ShaderResources = other.ShaderResources;
        UnorderedAccessViews = other.UnorderedAccessViews;
        ScissorRect = other.ScissorRect;
        StencilRef = other.StencilRef;
        IndexStart = other.IndexStart;
        IndexCount = other.IndexCount;
        BaseVertexIndex = other.BaseVertexIndex;
        InstanceStart = other.InstanceStart;
        InstanceCount = other.InstanceCount;
        NumGroups = other.NumGroups;
    }
}
