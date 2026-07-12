using Microsoft.Extensions.Logging;

namespace RedBolt;

public static class GraphicsUtils
{
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

    // TODO: port: VertexBuffer does not yet expose the RawBuffer objects required by DrawCommandQueue.
    // URHO3D_API void InitializeInputLayoutAndPrimitiveType(
    //     GraphicsPipelineStateDesc& desc, const Geometry* geometry, VertexBuffer* instancingBuffer = nullptr);
    //
    // void InitializeInputLayoutAndPrimitiveType(
    //     GraphicsPipelineStateDesc& desc, const Geometry* geometry, VertexBuffer* instancingBuffer)
    // {
    //     const VertexBufferArray vertexBuffers = ToVertexBufferArray(geometry, instancingBuffer);
    //     InitializeInputLayout(desc.inputLayout_, vertexBuffers);
    //     desc.primitiveType_ = geometry->GetPrimitiveType();
    // }

    // TODO: port: VertexBuffer does not yet expose the RawBuffer objects required by DrawCommandQueue.
    // inline void SetBuffersFromGeometry(
    //     DrawCommandQueue& drawQueue, const Geometry* geometry, VertexBuffer* instancingBuffer = nullptr)
    // {
    //     const auto& vertexBuffers = geometry->GetVertexBuffers();
    //     IndexBuffer* indexBuffer = geometry->GetIndexBuffer();
    //
    //     if (vertexBuffers.size() + (instancingBuffer ? 1 : 0) > MaxVertexStreams)
    //     {
    //         URHO3D_ASSERT(false, "Too many vertex buffers");
    //         return;
    //     }
    //
    //     RawVertexBufferArray mergedVertexBuffers{};
    //     for (unsigned i = 0; i < vertexBuffers.size(); ++i)
    //         mergedVertexBuffers[i] = vertexBuffers[i];
    //     if (instancingBuffer)
    //         mergedVertexBuffers[vertexBuffers.size()] = instancingBuffer;
    //
    //     drawQueue.SetVertexBuffers(mergedVertexBuffers);
    //     drawQueue.SetIndexBuffer(indexBuffer);
    // }

    // TODO: port: Texture does not yet expose the RawTexture objects required by DrawCommandQueue.
    // inline void AddShaderResource(DrawCommandQueue& drawQueue, StringHash name, Texture* texture)
    // {
    //     if (!texture)
    //     {
    //         URHO3D_ASSERTLOG(false, "Trying to add null texture to DrawCommandQueue");
    //         return;
    //     }
    //
    //     drawQueue.AddShaderResource(name, texture, texture->GetBackupTexture());
    // }

    // TODO: port: Texture does not yet expose the RawTexture objects required by DrawCommandQueue.
    // inline void AddNullableShaderResource(
    //     DrawCommandQueue& drawQueue, StringHash name, TextureType type, Texture* texture)
    // {
    //     drawQueue.AddNullableShaderResource(name, type, texture, texture ? texture->GetBackupTexture() : nullptr);
    // }
}
