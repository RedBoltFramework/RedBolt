using System.Buffers;
using Microsoft.Extensions.Logging;

namespace RedBolt;

public static class GraphicsUtils
{
    private static VertexBuffer?[] ToVertexBufferArray(Geometry geometry, VertexBuffer? instancingBuffer)
    {
        var vertexBuffers = new VertexBuffer?[RenderApiLimits.MaxVertexStreams];
        var numVertexBuffers = geometry.GetNumVertexBuffers() + (instancingBuffer is null ? 0u : 1u);
        if (numVertexBuffers > RenderApiLimits.MaxVertexStreams)
        {
            Log.Logger.LogError(
                "Too many vertex buffers: PipelineState cannot handle more than {MaxVertexStreams}",
                RenderApiLimits.MaxVertexStreams);
            return vertexBuffers;
        }

        for (var index = 0u; index < geometry.GetNumVertexBuffers(); ++index)
            vertexBuffers[checked((int)index)] = geometry.GetVertexBuffer(index);

        if (instancingBuffer is not null)
            vertexBuffers[checked((int)(numVertexBuffers - 1))] = instancingBuffer;

        return vertexBuffers;
    }

    public static void InitializeInputLayout(InputLayoutDesc desc, IReadOnlyList<VertexBuffer?> vertexBuffers)
    {
        Span<InputLayoutElementDesc> layoutElements = stackalloc InputLayoutElementDesc[(int)RenderApiLimits.MaxNumVertexElements];
        uint size = 0;

        for (uint bufferIndex = 0; bufferIndex < RenderApiLimits.MaxVertexStreams && bufferIndex < vertexBuffers.Count; ++bufferIndex)
        {
            VertexBuffer? vertexBuffer = vertexBuffers[(int)bufferIndex];
            if (vertexBuffer is null)
                continue;

            var elements = vertexBuffer.GetElements();
            var numElements = Math.Min(elements.Count, checked((int)(RenderApiLimits.MaxNumVertexElements - size)));
            if (numElements > 0)
            {
                for (var i = 0; i < numElements; ++i)
                {
                    VertexElement vertexElement = elements[i];
                    layoutElements[checked((int)size) + i] = new InputLayoutElementDesc(
                        bufferIndex,
                        vertexBuffer.GetVertexSize(),
                        vertexElement.Offset,
                        vertexElement.StepRate,
                        vertexElement.Type,
                        vertexElement.Semantic,
                        vertexElement.Index);
                }

                size += (uint)numElements;
            }

            if (elements.Count != numElements)
            {
                Log.Logger.LogWarning("Too many vertex elements: PipelineState cannot handle more than {MaxNumVertexElements}",
                    RenderApiLimits.MaxNumVertexElements);
            }
        }

        desc.SetElements(layoutElements[..checked((int)size)]);
    }

    public static void InitializeInputLayoutAndPrimitiveType(
        GraphicsPipelineStateDesc desc, Geometry geometry, VertexBuffer? instancingBuffer = null)
    {
        var vertexBuffers = ToVertexBufferArray(geometry, instancingBuffer);

        InitializeInputLayout(desc.InputLayout, vertexBuffers);
        desc.PrimitiveType = geometry.GetPrimitiveType();
    }

    public static void SetBuffersFromGeometry(
        DrawCommandQueue drawQueue, Geometry geometry, VertexBuffer? instancingBuffer = null)
    {
        var vertexBuffers = geometry.GetVertexBuffers();
        if (vertexBuffers.Count + (instancingBuffer is null ? 0 : 1) > RenderApiLimits.MaxVertexStreams)
        {
            Log.Logger.LogError("Too many vertex buffers");
            return;
        }

        var mergedVertexBuffers = ArrayPool<RawBuffer?>.Shared.Rent((int)RenderApiLimits.MaxVertexStreams);
        try
        {
            Array.Clear(mergedVertexBuffers, 0, (int)RenderApiLimits.MaxVertexStreams);
            for (var i = 0; i < vertexBuffers.Count; ++i)
                mergedVertexBuffers[i] = vertexBuffers[i]?.GetRawBuffer();
            if (instancingBuffer is not null)
                mergedVertexBuffers[vertexBuffers.Count] = instancingBuffer.GetRawBuffer();

            drawQueue.SetVertexBuffers(mergedVertexBuffers.AsSpan(0, (int)RenderApiLimits.MaxVertexStreams));
            drawQueue.SetIndexBuffer(geometry.GetIndexBuffer()?.GetRawBuffer());
        }
        finally
        {
            Array.Clear(mergedVertexBuffers, 0, (int)RenderApiLimits.MaxVertexStreams);
            ArrayPool<RawBuffer?>.Shared.Return(mergedVertexBuffers);
        }
    }

    public static void AddShaderResource(DrawCommandQueue drawQueue, StringHash name, Texture? texture)
    {
        ArgumentNullException.ThrowIfNull(drawQueue);
        if (texture is null)
            return;

        drawQueue.AddShaderResource(
            name,
            texture.GetRawTexture()!,
            texture.GetBackupTexture()?.GetRawTexture());
    }

    public static void AddNullableShaderResource(
        DrawCommandQueue drawQueue, StringHash name, TextureType type, Texture? texture)
    {
        ArgumentNullException.ThrowIfNull(drawQueue);

        drawQueue.AddNullableShaderResource(
            name,
            type,
            texture?.GetRawTexture(),
            texture?.GetBackupTexture()?.GetRawTexture());
    }
}
