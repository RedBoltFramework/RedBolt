//
// Copyright (c) 2017-2020 the rbfx project.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//

namespace RedBolt.Glow;

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using SharpCpp;
using SharpCpp.Support;

using RedBolt.Bindings.XAtlas;
using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.RenderAPI;

/// <summary>Lightmap UV generation settings.</summary>
public struct LightmapUVGenerationSettings
{
    /// <summary>Metadata key for lightmap size.</summary>
    public static readonly string LightmapSizeKey = "LightmapSize";
    /// <summary>Metadata key for lightmap density.</summary>
    public static readonly string LightmapDensityKey = "LightmapDensity";
    /// <summary>Metadata key for shared lightmap UV flag.</summary>
    public static readonly string LightmapSharedUV = "LightmapSharedUV";

    /// <summary>Texels per unit.</summary>
    public float TexelPerUnit;
    /// <summary>UV channel to write. 2nd channel by default.</summary>
    public int UvChannel;

    public LightmapUVGenerationSettings()
    {
        TexelPerUnit = 10;
        UvChannel = 1;
    }
}

public static class LightmapUVGenerator
{
    /// <summary>Generate lightmap UVs for the model.</summary>
    public static unsafe bool GenerateLightmapUV(ModelView modelView, in LightmapUVGenerationSettings settings)
    {
        return GenerateLightmapUV(modelView, settings, false);
    }

    private static unsafe bool GenerateLightmapUV(ModelView modelView, LightmapUVGenerationSettings settings,
        bool forceMeshAddFailure)
    {
        // Create atlas
        var atlas = XAtlasNativeBindings.Create();
        try
        {
            // Fill input mesh
            // TODO: Do something more clever about connectivity
            var sourceGeometries = modelView.GetGeometries();
            var meshToGeometryLodMapping = new List<(int GeometryIndex, int LodIndex)>();
            var geometryIndex = 0;
            foreach (var geometryView in sourceGeometries)
            {
                var lodIndex = 0;
                foreach (var geometryLodView in geometryView.Lods)
                {
                    if (geometryLodView.Vertices.Count != 0 && geometryLodView.PrimitiveType == PrimitiveType.TriangleList)
                    {
                        if (geometryLodView.Indices.Count % 3 != 0)
                            return false;

                        foreach (var index in geometryLodView.Indices)
                        {
                            if (index >= geometryLodView.Vertices.Count)
                                return false;
                        }

                        using var meshDecl = new MeshDecl();
                        var positions = new Vector4f[geometryLodView.Vertices.Count];
                        var normals = new Vector4f[geometryLodView.Vertices.Count];
                        for (var vertexIndex = 0; vertexIndex < geometryLodView.Vertices.Count; ++vertexIndex)
                        {
                            positions[vertexIndex] = geometryLodView.Vertices[vertexIndex].Position;
                            normals[vertexIndex] = geometryLodView.Vertices[vertexIndex].Normal;
                        }

                        fixed (Vector4f* positionData = positions)
                        fixed (Vector4f* normalData = normals)
                        fixed (uint* indexData = CollectionsMarshal.AsSpan(geometryLodView.Indices))
                        {
                            meshDecl.VertexCount = (uint)geometryLodView.Vertices.Count;
                            meshDecl.VertexPositionData = (nint)positionData;
                            meshDecl.VertexPositionStride = (uint)sizeof(Vector4f);
                            meshDecl.VertexNormalData = (nint)normalData;
                            meshDecl.VertexNormalStride = (uint)sizeof(Vector4f);
                            meshDecl.IndexData = (nint)indexData;
                            meshDecl.IndexCount = (uint)geometryLodView.Indices.Count;
                            meshDecl.IndexFormat = IndexFormat.UInt32;

                            if (!TryAddMesh(atlas, meshDecl, forceMeshAddFailure))
                                return false;
                        }

                        meshToGeometryLodMapping.Add((geometryIndex, lodIndex));
                    }

                    ++lodIndex;
                }

                ++geometryIndex;
            }

            // Generate things
            using var packOptions = new PackOptions();
            packOptions.Padding = 1;
            packOptions.TexelsPerUnit = settings.TexelPerUnit;

            XAtlasNativeBindings.AddMeshJoin(atlas);
            using (var chartOptions = new ChartOptions())
                XAtlasNativeBindings.Generate(atlas, chartOptions, nint.Zero, packOptions);

            // Copy output
            var atlasSize = new Vector2i((int)atlas.Width, (int)atlas.Height);

            var uScale = 1.0f / atlas.Width;
            var vScale = 1.0f / atlas.Height;

            nint meshesHandle;
            using (var meshesPointer = atlas.Meshes)
                meshesHandle = meshesPointer.Handle;
            using var meshes = NativeArray<Mesh>.CreateManagedWrapper(
                new NativeArrayAbi(meshesHandle, atlas.MeshCount), false);
            for (var meshIndex = 0; meshIndex < (int)atlas.MeshCount; ++meshIndex)
            {
                var geometryLodMapping = meshToGeometryLodMapping[meshIndex];
                geometryIndex = geometryLodMapping.GeometryIndex;
                var lodIndex = geometryLodMapping.LodIndex;
                var geometryView = sourceGeometries[geometryIndex];
                var geometryLodView = geometryView.Lods[lodIndex];
                using var mesh = meshes[meshIndex];

                nint verticesHandle;
                using (var verticesPointer = mesh.VertexArray)
                    verticesHandle = verticesPointer.Handle;
                using var vertices = NativeArray<Vertex>.CreateManagedWrapper(
                    new NativeArrayAbi(verticesHandle, mesh.VertexCount), false);
                var newVertices = new List<ModelVertex>();
                for (var vertexIndex = 0; vertexIndex < (int)mesh.VertexCount; ++vertexIndex)
                {
                    using var vertex = vertices[vertexIndex];
                    var newVertex = geometryLodView.Vertices[(int)vertex.Xref].Clone();
                    using var uv = vertex.Uv;
                    newVertex.Uv[settings.UvChannel].X = uScale * uv[0];
                    newVertex.Uv[settings.UvChannel].Y = vScale * uv[1];
                    newVertices.Add(newVertex);
                }

                var newIndices = new List<uint>();
                var indexArray = new ReadOnlySpan<uint>((void*)mesh.IndexArray, (int)mesh.IndexCount);
                for (var index = 0; index < indexArray.Length; ++index)
                    newIndices.Add(indexArray[index]);

                geometryLodView.Vertices = newVertices;
                geometryLodView.Indices = newIndices;
                geometryLodView.VertexFormat.Uv[settings.UvChannel] = VertexElementType.Vector2;
                geometryView.Lods[lodIndex] = geometryLodView;
                sourceGeometries[geometryIndex] = geometryView;
            }

            // Finalize
            using (var value = new Variant(atlasSize))
                modelView.AddMetadata(LightmapUVGenerationSettings.LightmapSizeKey, value);
            using (var value = new Variant(settings.TexelPerUnit))
                modelView.AddMetadata(LightmapUVGenerationSettings.LightmapDensityKey, value);
            using (var value = new Variant(false))
                modelView.AddMetadata(LightmapUVGenerationSettings.LightmapSharedUV, value);

            return true;
        }
        finally
        {
            XAtlasNativeBindings.Destroy(atlas);
            atlas.Dispose();
        }
    }

    private static bool TryAddMesh(Atlas atlas, MeshDecl meshDecl, bool forceFailure)
    {
        var error = forceFailure
            ? (AddMeshError)int.MaxValue
            : XAtlasNativeBindings.AddMesh(atlas, meshDecl);
        return error == AddMeshError.Success;
    }
}
