using RedBolt.Graphics;
using RedBolt.RenderPipeline;
namespace RedBolt.RenderAPI;

using System.Runtime.CompilerServices;

/// <summary>Shader resource group, range in array.</summary>
public record struct ShaderResourceRange(int First, int Second);

[InlineArray((int)RenderApiLimits.MaxVertexStreams)]
public struct RawVertexBufferArray
{
    private RawBuffer? _element0;
}

[InlineArray((int)RenderApiLimits.MaxShaderParameterGroups)]
public struct ConstantBufferRefArray
{
    private ConstantBufferCollectionRef _element0;
}

/// <summary>Description of draw command.</summary>
public struct DrawCommandDescription
{
    public PipelineState? PipelineState;

    public RawVertexBufferArray VertexBuffers;

    public RawBuffer? IndexBuffer;

    public ConstantBufferRefArray ConstantBuffers;

    public ShaderResourceRange ShaderResources;

    public ShaderResourceRange UnorderedAccessViews;

    /// <summary>Index of scissor rectangle. 0 if disabled.</summary>
    public int ScissorRect;

    /// <summary>Stencil reference value.</summary>
    public uint StencilRef;

    /// <summary>Draw call parameters.</summary>
    public int IndexStart;

    public int IndexCount;

    public int BaseVertexIndex;

    public int InstanceStart;

    public int InstanceCount;

    public Vector3i NumGroups;
}
