// Copyright (c) 2025-2026 the rbfx project.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.CSG;

using System;
using System.Buffers.Binary;
using System.Collections.Generic;

using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.RenderAPI;
using RedBolt.Resource;

public enum CsgOperation
{
    Union,
    DifferenceAB,
    Intersection,
}

public static partial class Csg
{
    private static uint PackSharedKey(uint meshId, uint geometryIndex)
    {
        return (meshId << 16) | (geometryIndex & 0xffffu);
    }

    private static (uint MeshId, uint GeometryIndex) UnpackSharedKey(uint sharedKey)
    {
        var meshId = (sharedKey >> 16) & 0xffffu;
        var geometryIndex = sharedKey & 0xffffu;
        return (meshId, geometryIndex);
    }

    private static bool IsSupportedCsgVertexElement(VertexElement element)
    {
        if (element.StepRate != 0)
            return false; // Per-instance data is not supported.

        switch (element.Semantic)
        {
            case VertexElementSemantic.Position:
            case VertexElementSemantic.Normal:
            case VertexElementSemantic.Binormal:
            case VertexElementSemantic.Tangent:
            case VertexElementSemantic.TexCoord:
            case VertexElementSemantic.Color:
                return true;

            case VertexElementSemantic.BlendWeights:
            case VertexElementSemantic.BlendIndices:
            case VertexElementSemantic.ObjectIndex:
            default:
                return false;
        }
    }

    private static List<VertexElement> GetSupportedVertexElements(Geometry? sourceGeometry)
    {
        var elements = new List<VertexElement>();

        if (sourceGeometry is not null)
        {
            sourceGeometry.GetRawDataShared(out var vertexData, out _, out _, out _, out var sourceElements);

            if (vertexData is not null && sourceElements is not null)
            {
                elements.EnsureCapacity(sourceElements.Count);
                foreach (var element in sourceElements)
                {
                    if (IsSupportedCsgVertexElement(element))
                        elements.Add(element);
                }
            }
        }
        else
        {
            elements.Add(new VertexElement(VertexElementType.Vector3, VertexElementSemantic.Position));
        }

        VertexBuffer.UpdateOffsets(elements);
        return elements;
    }

    private static byte[] PackVerticesToLayout(IReadOnlyList<ModelVertex> vertices,
        IReadOnlyList<VertexElement> elements)
    {
        // `ModelVertex` is documented to be equivalent to an array of Vector4. This function relies on that.
        // Keep these checks close to the usage to fail loudly if ModelVertex layout changes.
        var vertexCount = checked((uint)vertices.Count);
        var vertexSize = VertexBuffer.GetVertexSize(elements);

        var packed = new byte[checked((int)(vertexCount * vertexSize))];
        if (vertexCount == 0)
            return packed;

        var sourceUnpacked = new Vector4f[checked(vertices.Count * ModelVertex.VertexElements.Length)];
        for (var i = 0; i < vertices.Count; ++i)
            vertices[i].Data().CopyTo(sourceUnpacked, i * ModelVertex.VertexElements.Length);

        var destinationUnpacked = new Vector4f[checked(vertices.Count * elements.Count)];
        VertexBuffer.ShuffleUnpackedVertexData(vertexCount, sourceUnpacked, ModelVertex.VertexElements,
            destinationUnpacked, elements, true);

        var sourceStride = checked((uint)(sizeof(float) * 4 * elements.Count));
        for (var elementIndex = 0; elementIndex < elements.Count; ++elementIndex)
        {
            VertexBuffer.PackVertexData(destinationUnpacked.AsSpan(elementIndex), sourceStride, packed, vertexSize,
                elements[elementIndex], 0, vertexCount);
        }

        return packed;
    }

    /// <summary>Extract CSG polygons from a <see cref="Model"/> operand.</summary>
    /// <remarks>
    /// <paramref name="worldTransform"/> is applied to vertex positions (and normals/tangents) during extraction.
    /// <paramref name="meshId"/> is encoded into <see cref="CsgPolygon.Shared"/> as
    /// <c>(meshId &lt;&lt; 16) | geometryIndex</c>.
    /// </remarks>
    public static List<CsgPolygon> BuildPolygonsFromModel(Model? model, Matrix3x4 worldTransform, uint meshId = 0)
    {
        var polygons = new List<CsgPolygon>();
        if (model is null)
            return polygons;

        var basis = worldTransform.Matrix3;
        var basisOk = !basis.IsNaN && !basis.IsInf && MathF.Abs(basis.Determinant) > MathDefs.Epsilon;
        var normalMatrix = basisOk ? basis.Inverse.Transpose : Matrix3.Identity;

        var numGeometries = model.GetNumGeometries();
        for (var i = 0u; i < numGeometries; ++i)
        {
            var geometry = model.GetGeometry(i, 0);
            if (geometry is null)
                continue;

            geometry.GetRawDataShared(out var vertexData, out var vertexSize, out var indexData, out var indexSize,
                out var elements);
            if (vertexData is null || elements is null)
                continue;

            // Element lookup table
            var elementPointers = new VertexElement?[((int)VertexElementSemantic.ObjectIndex + 1) * 4];
            foreach (var element in elements)
                elementPointers[((int)element.Semantic << 2) + element.Index] = element;

            var indexCount = geometry.GetIndexCount();
            for (var j = 0u; j < indexCount; j += 3)
            {
                uint i0;
                uint i1;
                uint i2;
                if (indexData is null)
                {
                    i0 = j;
                    i1 = j + 1;
                    i2 = j + 2;
                }
                else if (indexSize == 2)
                {
                    i0 = BinaryPrimitives.ReadUInt16LittleEndian(indexData.AsSpan(checked((int)(j * indexSize))));
                    i1 = BinaryPrimitives.ReadUInt16LittleEndian(indexData.AsSpan(checked((int)((j + 1) * indexSize))));
                    i2 = BinaryPrimitives.ReadUInt16LittleEndian(indexData.AsSpan(checked((int)((j + 2) * indexSize))));
                }
                else
                {
                    i0 = BinaryPrimitives.ReadUInt32LittleEndian(indexData.AsSpan(checked((int)(j * indexSize))));
                    i1 = BinaryPrimitives.ReadUInt32LittleEndian(indexData.AsSpan(checked((int)((j + 1) * indexSize))));
                    i2 = BinaryPrimitives.ReadUInt32LittleEndian(indexData.AsSpan(checked((int)((j + 2) * indexSize))));
                }

                var vertices = new List<ModelVertex>(3);
                foreach (var index in new[] { i0, i1, i2 })
                {
                    var vertex = new ModelVertex();

                    // Position is required.
                    if (elementPointers[(int)VertexElementSemantic.Position << 2] is { } element)
                    {
                        var position4 = Vector4f.Zero;
                        VertexBuffer.UnpackVertexData(vertexData, vertexSize, element, index, 1,
                            new Span<Vector4f>(ref position4), checked((uint)(sizeof(float) * 4)));
                        vertex.SetPosition(worldTransform * position4.ToVector3());
                    }

                    // Normal.
                    if (elementPointers[(int)VertexElementSemantic.Normal << 2] is { } normalElement)
                    {
                        var normal4 = Vector4f.Zero;
                        VertexBuffer.UnpackVertexData(vertexData, vertexSize, normalElement, index, 1,
                            new Span<Vector4f>(ref normal4), checked((uint)(sizeof(float) * 4)));
                        var transformedNormal = normalMatrix * normal4.ToVector3();
                        vertex.SetNormal(transformedNormal.NormalizedOrDefault(Vector3f.Up));
                    }

                    // Tangent.
                    if (elementPointers[(int)VertexElementSemantic.Tangent << 2] is { } tangentElement)
                    {
                        var tangent4 = Vector4f.Zero;
                        VertexBuffer.UnpackVertexData(vertexData, vertexSize, tangentElement, index, 1,
                            new Span<Vector4f>(ref tangent4), checked((uint)(sizeof(float) * 4)));
                        var transformedTangent = (basis * tangent4.ToVector3()).NormalizedOrDefault(Vector3f.Right);
                        vertex.Tangent = new Vector4f(transformedTangent, tangent4.W);
                    }

                    // Binormal.
                    if (elementPointers[(int)VertexElementSemantic.Binormal << 2] is { } binormalElement)
                    {
                        var binormal4 = Vector4f.Zero;
                        VertexBuffer.UnpackVertexData(vertexData, vertexSize, binormalElement, index, 1,
                            new Span<Vector4f>(ref binormal4), checked((uint)(sizeof(float) * 4)));
                        var transformedBinormal = (basis * binormal4.ToVector3()).NormalizedOrDefault(Vector3f.Up);
                        vertex.Binormal = new Vector4f(transformedBinormal, 0.0f);
                    }

                    // Colors and UVs.
                    for (byte colorIndex = 0; colorIndex < ModelVertex.MaxColors; ++colorIndex)
                    {
                        if (elementPointers[((int)VertexElementSemantic.Color << 2) + colorIndex] is { } colorElement)
                        {
                            VertexBuffer.UnpackVertexData(vertexData, vertexSize, colorElement, index, 1,
                                vertex.Color.AsSpan(colorIndex), checked((uint)(sizeof(float) * 4)));
                        }
                    }

                    for (byte uvIndex = 0; uvIndex < ModelVertex.MaxUVs; ++uvIndex)
                    {
                        if (elementPointers[((int)VertexElementSemantic.TexCoord << 2) + uvIndex] is { } uvElement)
                        {
                            VertexBuffer.UnpackVertexData(vertexData, vertexSize, uvElement, index, 1,
                                vertex.Uv.AsSpan(uvIndex), checked((uint)(sizeof(float) * 4)));
                        }
                    }

                    // Ignore animation/instancing data (blend weights/indices, per-instance elements).

                    vertices.Add(vertex);
                }

                polygons.Add(new CsgPolygon(vertices, PackSharedKey(meshId, i)));
            }
        }

        return polygons;
    }

    /// <summary>Compute CSG boolean operation between two BSP operands and return resulting polygons.</summary>
    /// <remarks><paramref name="epsilon"/> is used consistently for BSP clipping in this operation.</remarks>
    public static List<CsgPolygon> BooleanOperation(CsgBsp bspA, CsgBsp bspB, CsgOperation operation,
        float epsilon = CsgCommon.DefaultEpsilon)
    {
        var resultPolygons = new List<CsgPolygon>();

        switch (operation)
        {
            case CsgOperation.Union:
            {
                var aOutB = bspB.Clip(bspA.AllPolygons(), CsgClipMode.ClipToOutside, epsilon);
                var bOutA = bspA.Clip(bspB.AllPolygons(), CsgClipMode.ClipToOutside, epsilon);

                resultPolygons.AddRange(aOutB);
                resultPolygons.AddRange(bOutA);
                break;
            }
            case CsgOperation.DifferenceAB:
            {
                var aOutB = bspB.Clip(bspA.AllPolygons(), CsgClipMode.ClipToOutside, epsilon);
                var bInA = bspA.Clip(bspB.AllPolygonsInverted(), CsgClipMode.ClipToInside, epsilon);

                resultPolygons.AddRange(aOutB);
                resultPolygons.AddRange(bInA);
                break;
            }
            case CsgOperation.Intersection:
            {
                var aInB = bspB.Clip(bspA.AllPolygons(), CsgClipMode.ClipToInside, epsilon);
                var bInA = bspA.Clip(bspB.AllPolygons(), CsgClipMode.ClipToInside, epsilon);

                resultPolygons.AddRange(aInB);
                resultPolygons.AddRange(bInA);
                break;
            }
        }

        return resultPolygons;
    }

    /// <summary>Build BSP tree from a <see cref="Model"/> operand.</summary>
    /// <remarks>
    /// <paramref name="worldTransform"/> is applied to vertex positions (and normals/tangents) during extraction.
    /// <paramref name="meshId"/> is encoded into <see cref="CsgPolygon.Shared"/> as
    /// <c>(meshId &lt;&lt; 16) | geometryIndex</c>. Returns empty BSP if extraction fails.
    /// </remarks>
    public static CsgBsp BuildBspFromModel(Model? model, Matrix3x4 worldTransform, uint meshId = 0,
        float epsilon = CsgCommon.DefaultEpsilon)
    {
        if (model is null)
            return new CsgBsp();

        var polygons = BuildPolygonsFromModel(model, worldTransform, meshId);
        if (polygons.Count == 0)
            return new CsgBsp();

        var result = new CsgBsp();
        result.Build(polygons, epsilon);
        return result;
    }

    /// <summary>Build output <see cref="Model"/> from triangulated CSG result.</summary>
    /// <remarks>
    /// <paramref name="sourceModels"/> index corresponds to source mesh ID decoded from
    /// <see cref="CsgPolygon.Shared"/>. <paramref name="sourceMaterials"/> index corresponds to source mesh ID,
    /// and resource list index corresponds to source geometry index decoded from <see cref="CsgPolygon.Shared"/>.
    /// </remarks>
    public static Model BuildModel(CsgTriangulatedModel triangulated, IReadOnlyList<Model?> sourceModels,
        IReadOnlyList<ResourceRefList?> sourceMaterials)
    {
        ResourceRefList? outMaterials = null;
        return BuildModel(triangulated, sourceModels, sourceMaterials, ref outMaterials);
    }

    /// <summary>Build output <see cref="Model"/> from triangulated CSG result.</summary>
    public static Model BuildModel(CsgTriangulatedModel triangulated, IReadOnlyList<Model?> sourceModels,
        IReadOnlyList<ResourceRefList?> sourceMaterials, ref ResourceRefList? outMaterials)
    {
        if (outMaterials is not null)
            outMaterials = new ResourceRefList(ObjectReflection<Material>.TypeId);

        if (triangulated.Geometries.Count == 0 || !triangulated.HasAnyVertex)
            return new Model();

        var modelVertexBuffers = new List<VertexBuffer>(triangulated.Geometries.Count);
        var modelIndexBuffers = new List<IndexBuffer>(triangulated.Geometries.Count);
        var modelGeometries = new List<Geometry>(triangulated.Geometries.Count);

        foreach (var source in triangulated.Geometries)
        {
            var vertexCount = checked((uint)source.Vertices.Count);
            if (vertexCount == 0 || source.IndexData.Count == 0)
                continue;

            var (meshId, geometryIndex) = UnpackSharedKey(source.Shared);

            var sourceModel = meshId < sourceModels.Count ? sourceModels[(int)meshId] : null;
            var sourceGeometry = sourceModel?.GetGeometry(geometryIndex, 0);
            var vertexBufferElements = GetSupportedVertexElements(sourceGeometry);

            var vertexBuffer = new VertexBuffer();
            vertexBuffer.SetShadowed(true);
            if (!vertexBuffer.SetSize(vertexCount, vertexBufferElements))
                continue;

            var packedVertices = PackVerticesToLayout(source.Vertices, vertexBufferElements);
            vertexBuffer.Update(packedVertices);

            var largeIndices = vertexCount > 0xffffu;
            var indexBuffer = new IndexBuffer();
            indexBuffer.SetShadowed(true);
            indexBuffer.SetSize(checked((uint)source.IndexData.Count), largeIndices);
            indexBuffer.SetUnpackedData(source.IndexData);

            var geometry = new Geometry();
            geometry.SetNumVertexBuffers(1);
            geometry.SetVertexBuffer(0, vertexBuffer);
            geometry.SetIndexBuffer(indexBuffer);
            geometry.SetDrawRange(PrimitiveType.TriangleList, 0, indexBuffer.GetIndexCount(), 0,
                vertexBuffer.GetVertexCount());

            modelVertexBuffers.Add(vertexBuffer);
            modelIndexBuffers.Add(indexBuffer);
            modelGeometries.Add(geometry);

            if (outMaterials is not null)
            {
                var output = outMaterials.Value;
                var sourceMaterialList = meshId < sourceMaterials.Count ? sourceMaterials[(int)meshId] : null;
                if (sourceMaterialList is not null && geometryIndex < sourceMaterialList.Value.Names.Count)
                    output.Names.Add(sourceMaterialList.Value.Names[(int)geometryIndex]);
                else
                    output.Names.Add(string.Empty);
                outMaterials = output;
            }
        }

        if (modelGeometries.Count == 0)
            return new Model();

        var outModel = new Model();
        outModel.SetVertexBuffers(modelVertexBuffers, [], []);
        outModel.SetIndexBuffers(modelIndexBuffers);
        outModel.SetNumGeometries(checked((uint)modelGeometries.Count));
        for (var i = 0; i < modelGeometries.Count; ++i)
            outModel.SetGeometry(checked((uint)i), 0, modelGeometries[i]);
        outModel.SetBoundingBox(triangulated.BoundingBox);

        return outModel;
    }

    /// <summary>Register CSG library objects.</summary>
    public static void RegisterCsgLibrary(Context context)
    {
        context.RegisterAssembly(typeof(Csg).Assembly);
    }

    // TODO: port
    // void RegisterCsgLibrary(PluginApplication& plugin)
    // {
    //     plugin.RegisterObject<CsgComponent>();
    //     plugin.RegisterObject<CsgBrush>();
    //     plugin.RegisterObject<CsgRoot>();
    // }
}
