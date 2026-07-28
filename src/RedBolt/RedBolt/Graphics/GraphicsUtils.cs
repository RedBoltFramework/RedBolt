namespace RedBolt.Graphics;

using Microsoft.Extensions.Logging;
using System.Buffers;
using SystemMath = global::System.Math;

using RedBolt.Container;
using RedBolt.Logging;
using RedBolt.RenderAPI;

public static class GraphicsUtils
{
    private static void ToVertexBufferArray(
        Geometry geometry, VertexBuffer? instancingBuffer, Span<VertexBuffer?> vertexBuffers)
    {
        vertexBuffers.Clear();
        var numVertexBuffers = geometry.GetNumVertexBuffers() + (instancingBuffer is null ? 0u : 1u);
        if (numVertexBuffers > RenderApiLimits.MaxVertexStreams)
        {
            Log.GetLogger().LogError(
                "Too many vertex buffers: PipelineState cannot handle more than {MaxVertexStreams}",
                RenderApiLimits.MaxVertexStreams);
            return;
        }

        for (var index = 0u; index < geometry.GetNumVertexBuffers(); ++index)
            vertexBuffers[checked((int)index)] = geometry.GetVertexBuffer(index);

        if (instancingBuffer is not null)
            vertexBuffers[checked((int)(numVertexBuffers - 1))] = instancingBuffer;

    }

    public static void InitializeInputLayout(ref InputLayoutDesc desc, ReadOnlySpan<VertexBuffer?> vertexBuffers)
    {
        Span<InputLayoutElementDesc> layoutElements = stackalloc InputLayoutElementDesc[RenderApiLimits.MaxNumVertexElements];
        var size = 0;

        for (var bufferIndex = 0; bufferIndex < RenderApiLimits.MaxVertexStreams && bufferIndex < vertexBuffers.Length; ++bufferIndex)
        {
            VertexBuffer? vertexBuffer = vertexBuffers[(int)bufferIndex];
            if (vertexBuffer is null)
                continue;

            var elements = vertexBuffer.GetElements();
            var numElements = SystemMath.Min(elements.Count, RenderApiLimits.MaxNumVertexElements - size);
            if (numElements > 0)
            {
                for (var i = 0; i < numElements; ++i)
                {
                    VertexElement vertexElement = elements[i];
                    layoutElements[size + i] = new InputLayoutElementDesc(
                        bufferIndex,
                        checked((int)vertexBuffer.GetVertexSize()),
                        checked((int)vertexElement.Offset),
                        checked((int)vertexElement.StepRate),
                        vertexElement.Type,
                        vertexElement.Semantic,
                        vertexElement.Index);
                }

                size += numElements;
            }

            if (elements.Count != numElements)
            {
                Log.GetLogger().LogWarning("Too many vertex elements: PipelineState cannot handle more than {MaxNumVertexElements}",
                    RenderApiLimits.MaxNumVertexElements);
            }
        }

        desc.Size = size;
        layoutElements[..size].CopyTo(desc.Elements);
    }

    public static void InitializeInputLayoutAndPrimitiveType(
        ref GraphicsPipelineStateDesc desc, Geometry geometry, VertexBuffer? instancingBuffer = null)
    {
        var vertexBuffers = ArrayPool<VertexBuffer?>.Shared.Rent((int)RenderApiLimits.MaxVertexStreams);
        try
        {
            var vertexBufferSpan = vertexBuffers.AsSpan(0, (int)RenderApiLimits.MaxVertexStreams);
            ToVertexBufferArray(geometry, instancingBuffer, vertexBufferSpan);
            InitializeInputLayout(ref desc.InputLayout, vertexBufferSpan);
            desc.PrimitiveType = geometry.GetPrimitiveType();
        }
        finally
        {
            Array.Clear(vertexBuffers, 0, (int)RenderApiLimits.MaxVertexStreams);
            ArrayPool<VertexBuffer?>.Shared.Return(vertexBuffers);
        }
    }

    public static void SetBuffersFromGeometry(
        DrawCommandQueue drawQueue, Geometry geometry, VertexBuffer? instancingBuffer = null)
    {
        var vertexBuffers = geometry.GetVertexBuffers();
        if (vertexBuffers.Count + (instancingBuffer is null ? 0 : 1) > RenderApiLimits.MaxVertexStreams)
        {
            Log.GetLogger().LogError("Too many vertex buffers");
            return;
        }

        RawVertexBufferArray mergedVertexBuffers = default;
        for (var i = 0; i < vertexBuffers.Count; ++i)
            mergedVertexBuffers[i] = vertexBuffers[i]?.GetRawBuffer();
        if (instancingBuffer is not null)
            mergedVertexBuffers[vertexBuffers.Count] = instancingBuffer.GetRawBuffer();

        drawQueue.SetVertexBuffers(mergedVertexBuffers);
        drawQueue.SetIndexBuffer(geometry.GetIndexBuffer()?.GetRawBuffer());
    }

    public static void AddShaderResource(DrawCommandQueue drawQueue, StringHash name, Texture? texture)
    {
        if (texture is null)
        {
            Log.GetLogger().LogError("Trying to add null texture to DrawCommandQueue");
            return;
        }

        drawQueue.AddShaderResource(
            name,
            texture.GetRawTexture()!,
            texture.GetBackupTexture()?.GetRawTexture());
    }

    public static void AddNullableShaderResource(
        DrawCommandQueue drawQueue, StringHash name, TextureType type, Texture? texture)
    {
        drawQueue.AddNullableShaderResource(
            name,
            type,
            texture?.GetRawTexture(),
            texture?.GetBackupTexture()?.GetRawTexture());
    }
}
