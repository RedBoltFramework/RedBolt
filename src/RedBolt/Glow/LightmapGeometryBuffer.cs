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

using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Logging;
using RedBolt.Math;
using RedBolt.RenderAPI;
using RedBolt.RenderPipeline;
using RedBolt.Resource;
using RedBolt.Scene;

/// <summary>Used for mapping between geometry ID in geometry buffer and actual geometry.</summary>
public struct GeometryIdToObjectMapping
{
    /// <summary>Index of the object in the array.</summary>
    public int ObjectIndex = int.MaxValue;
    /// <summary>Index of object geometry within the object.</summary>
    public int GeometryIndex = int.MaxValue;
    /// <summary>Index of geometry LOD.</summary>
    public int LodIndex = int.MaxValue;

    public GeometryIdToObjectMapping()
    {
    }

}

/// <summary>Lightmap seam description.</summary>
public struct LightmapSeam : ICloneable
{
    /// <summary>Edge on lightmap UV.</summary>
    public readonly Vector2f[] Positions;
    /// <summary>Other side of the edge on lightmap UV.</summary>
    public readonly Vector2f[] OtherPositions;

    public LightmapSeam()
    {
        Positions = new Vector2f[2];
        OtherPositions = new Vector2f[2];
    }

    /// <summary>Transform by scale and offset.</summary>
    public readonly LightmapSeam Transformed(in Vector2f scale, in Vector2f offset)
    {
        var result = new LightmapSeam();
        result.Positions[0] = Positions[0] * scale + offset;
        result.Positions[1] = Positions[1] * scale + offset;
        result.OtherPositions[0] = OtherPositions[0] * scale + offset;
        result.OtherPositions[1] = OtherPositions[1] * scale + offset;
        return result;
    }

    public readonly LightmapSeam Clone()
    {
        var clone = new LightmapSeam();
        Positions.CopyTo(clone.Positions, 0);
        OtherPositions.CopyTo(clone.OtherPositions, 0);
        return clone;
    }

    readonly object ICloneable.Clone() => Clone();
}

/// <summary>Baking scene for single lightmap chart.</summary>
public struct LightmapGeometryBakingScene : ICloneable
{
    /// <summary>Context.</summary>
    public Context? Context;
    /// <summary>Lightmap chart index.</summary>
    public int Index;
    /// <summary>Size of lightmap chart.</summary>
    public int LightmapSize;
    /// <summary>Baking scene.</summary>
    public Scene? Scene;
    /// <summary>Baking camera.</summary>
    public Camera? Camera;
    /// <summary>Lightmap seams.</summary>
    public List<LightmapSeam> Seams;

    public LightmapGeometryBakingScene()
    {
        Seams = [];
    }

    public readonly LightmapGeometryBakingScene Clone()
    {
        var clone = this;
        clone.Seams = new List<LightmapSeam>(Seams.Count);
        foreach (var seam in Seams)
            clone.Seams.Add(seam.Clone());
        return clone;
    }

    readonly object ICloneable.Clone() => Clone();
}

/// <summary>Baking scenes for the set of lightmap charts.</summary>
public struct LightmapGeometryBakingScenesArray : ICloneable
{
    /// <summary>Baking scenes.</summary>
    public List<LightmapGeometryBakingScene> BakingScenes;
    /// <summary>Geometry buffer ID to object mapping.</summary>
    public List<GeometryIdToObjectMapping> IdToObject;

    public LightmapGeometryBakingScenesArray()
    {
        BakingScenes = [];
        IdToObject = [];
    }

    public readonly LightmapGeometryBakingScenesArray Clone()
    {
        var clone = this;
        clone.BakingScenes = new List<LightmapGeometryBakingScene>(BakingScenes.Count);
        foreach (var bakingScene in BakingScenes)
            clone.BakingScenes.Add(bakingScene.Clone());
        clone.IdToObject = new List<GeometryIdToObjectMapping>(IdToObject);
        return clone;
    }

    readonly object ICloneable.Clone() => Clone();
}

/// <summary>Lightmap geometry buffer of lightmap chart.</summary>
public struct LightmapChartGeometryBuffer : ICloneable
{
    /// <summary>Lightmap chart index.</summary>
    public int Index;
    /// <summary>Size of lightmap chart.</summary>
    public int LightmapSize;

    /// <summary>Geometry buffer.</summary>
    public List<Vector3f> Positions;
    public List<Vector3f> SmoothPositions;
    public List<Vector3f> SmoothNormals;
    public List<Vector3f> FaceNormals;
    public List<int> GeometryIds;
    public List<uint> LightMasks;
    public List<int> BackgroundIds;
    public List<float> TexelRadiuses;
    public List<Vector3f> Albedo;
    public List<Vector3f> Emission;

    /// <summary>Lightmap seams.</summary>
    public List<LightmapSeam> Seams;

    /// <summary>Construct default.</summary>
    public LightmapChartGeometryBuffer()
    {
        Positions = [];
        SmoothPositions = [];
        SmoothNormals = [];
        FaceNormals = [];
        GeometryIds = [];
        LightMasks = [];
        BackgroundIds = [];
        TexelRadiuses = [];
        Albedo = [];
        Emission = [];
        Seams = [];
    }

    /// <summary>Construct valid.</summary>
    public LightmapChartGeometryBuffer(int index, int size)
        : this()
    {
        Index = index;
        LightmapSize = size;
        var numTexels = LightmapSize * LightmapSize;
        for (var i = 0; i < numTexels; ++i)
        {
            Positions.Add(Vector3f.Zero);
            SmoothPositions.Add(Vector3f.Zero);
            SmoothNormals.Add(Vector3f.Zero);
            FaceNormals.Add(Vector3f.Zero);
            GeometryIds.Add(0);
            LightMasks.Add(0);
            BackgroundIds.Add(0);
            TexelRadiuses.Add(0.0f);
            Albedo.Add(Vector3f.Zero);
            Emission.Add(Vector3f.Zero);
        }
    }

    /// <summary>Convert index to location.</summary>
    public readonly Vector2i IndexToLocation(int index)
    {
        return new Vector2i(index % LightmapSize, index / LightmapSize);
    }

    /// <summary>Returns whether the location is valid.</summary>
    public readonly bool IsValidLocation(in Vector2i location)
    {
        return 0 <= location.X && location.X < LightmapSize
            && 0 <= location.Y && location.Y < LightmapSize;
    }

    /// <summary>Convert location to index.</summary>
    public readonly int LocationToIndex(in Vector2i location)
    {
        return location.X + LightmapSize * location.Y;
    }

    public readonly LightmapChartGeometryBuffer Clone()
    {
        var clone = this;
        clone.Positions = new List<Vector3f>(Positions);
        clone.SmoothPositions = new List<Vector3f>(SmoothPositions);
        clone.SmoothNormals = new List<Vector3f>(SmoothNormals);
        clone.FaceNormals = new List<Vector3f>(FaceNormals);
        clone.GeometryIds = new List<int>(GeometryIds);
        clone.LightMasks = new List<uint>(LightMasks);
        clone.BackgroundIds = new List<int>(BackgroundIds);
        clone.TexelRadiuses = new List<float>(TexelRadiuses);
        clone.Albedo = new List<Vector3f>(Albedo);
        clone.Emission = new List<Vector3f>(Emission);
        clone.Seams = new List<LightmapSeam>(Seams.Count);
        foreach (var seam in Seams)
            clone.Seams.Add(seam.Clone());
        return clone;
    }

    readonly object ICloneable.Clone() => Clone();
}

public static class LightmapGeometryBuffer
{
    private static readonly Vector2f[] MultiTapOffsets =
    [
        new(1.0f, 1.0f), new(1.0f, -1.0f), new(-1.0f, 1.0f), new(-1.0f, -1.0f),
        new(1.0f, 0.5f), new(1.0f, -0.5f), new(-1.0f, 0.5f), new(-1.0f, -0.5f),
        new(0.5f, 1.0f), new(0.5f, -1.0f), new(-0.5f, 1.0f), new(-0.5f, -1.0f),
        new(1.0f, 0.0f), new(-1.0f, 0.0f), new(0.0f, 1.0f), new(0.0f, -1.0f),
        new(0.5f, 0.5f), new(0.5f, -0.5f), new(-0.5f, 0.5f), new(-0.5f, -0.5f),
        new(0.5f, 0.0f), new(-0.5f, 0.0f), new(0.0f, 0.5f), new(0.0f, -0.5f),
        Vector2f.Zero
    ];

    private readonly record struct Edge(int First, int Second)
    {
        public static Edge Ordered(int first, int second) => first < second
            ? new Edge(first, second) : new Edge(second, first);
    }

    private static List<LightmapSeam> CollectModelSeams(Model model, int uvChannel)
    {
        var modelView = new ModelView();
        if (!modelView.ImportModel(model))
            return [];
        var sharedLightmapUv = modelView.GetMetadata(LightmapUVGenerationSettings.LightmapSharedUV).GetBool();
        var positionEpsilonSquared = MathDefs.LargeEpsilon * MathDefs.LargeEpsilon;
        var normalEpsilonSquared = positionEpsilonSquared;
        var uvEpsilonSquared = positionEpsilonSquared;
        var boundingBox = modelView.CalculateBoundingBox();
        var hashStep = Vector3f.Max(boundingBox.Size / MathDefs.LargeValue,
            Vector3f.One * MathDefs.LargeEpsilon);
        Vector3i ComputeHash(Vector3f position) => Vector3f.FloorToInt((position - boundingBox.Min) / hashStep);

        var seams = new List<LightmapSeam>();
        foreach (var geometry in modelView.GetGeometries())
        {
            foreach (var lod in geometry.Lods)
            {
                var format = lod.VertexFormat;
                if (format.Position == ModelVertexFormat.Undefined || format.Normal == ModelVertexFormat.Undefined
                    || format.Uv[uvChannel] == ModelVertexFormat.Undefined)
                    return [];

                var edges = new HashSet<Edge>();
                for (var faceIndex = 0; faceIndex < lod.Indices.Count / 3; ++faceIndex)
                {
                    var a = checked((int)lod.Indices[faceIndex * 3]);
                    var b = checked((int)lod.Indices[faceIndex * 3 + 1]);
                    var c = checked((int)lod.Indices[faceIndex * 3 + 2]);
                    edges.Add(Edge.Ordered(a, b));
                    edges.Add(Edge.Ordered(b, c));
                    edges.Add(Edge.Ordered(c, a));
                }

                var edgeHash = new Dictionary<Vector3i, HashSet<Edge>>();
                foreach (var edge in edges)
                {
                    foreach (var index in new[] { edge.First, edge.Second })
                    {
                        var hash = ComputeHash(lod.Vertices[index].Position.ToVector3());
                        if (!edgeHash.TryGetValue(hash, out var bucket))
                            edgeHash.Add(hash, bucket = []);
                        bucket.Add(edge);
                    }
                }

                foreach (var edge in edges)
                {
                    var candidates = new HashSet<Edge>();
                    foreach (var index in new[] { edge.First, edge.Second })
                    {
                        var hash = ComputeHash(lod.Vertices[index].Position.ToVector3());
                        for (var z = -1; z <= 1; ++z)
                        for (var y = -1; y <= 1; ++y)
                        for (var x = -1; x <= 1; ++x)
                        {
                            if (edgeHash.TryGetValue(hash + new Vector3i(x, y, z), out var bucket))
                                candidates.UnionWith(bucket);
                        }
                    }

                    var edgePosition0 = lod.Vertices[edge.First].Position.ToVector3();
                    var edgePosition1 = lod.Vertices[edge.Second].Position.ToVector3();
                    var edgeNormal0 = lod.Vertices[edge.First].Normal.ToVector3();
                    var edgeNormal1 = lod.Vertices[edge.Second].Normal.ToVector3();
                    var edgeUv0 = lod.Vertices[edge.First].Uv[uvChannel].ToVector2();
                    var edgeUv1 = lod.Vertices[edge.Second].Uv[uvChannel].ToVector2();

                    foreach (var sourceCandidate in candidates)
                    {
                        if (sourceCandidate == edge)
                            continue;
                        var candidate = sourceCandidate;
                        if ((lod.Vertices[candidate.First].Position.ToVector3() - edgePosition1).LengthSquared
                            < positionEpsilonSquared)
                            candidate = new Edge(candidate.Second, candidate.First);

                        var candidatePosition0 = lod.Vertices[candidate.First].Position.ToVector3();
                        var candidatePosition1 = lod.Vertices[candidate.Second].Position.ToVector3();
                        var candidateNormal0 = lod.Vertices[candidate.First].Normal.ToVector3();
                        var candidateNormal1 = lod.Vertices[candidate.Second].Normal.ToVector3();
                        var candidateUv0 = lod.Vertices[candidate.First].Uv[uvChannel].ToVector2();
                        var candidateUv1 = lod.Vertices[candidate.Second].Uv[uvChannel].ToVector2();
                        if ((edgePosition0 - candidatePosition0).LengthSquared >= positionEpsilonSquared
                            || (edgePosition1 - candidatePosition1).LengthSquared >= positionEpsilonSquared
                            || (edgeNormal0 - candidateNormal0).LengthSquared >= normalEpsilonSquared
                            || (edgeNormal1 - candidateNormal1).LengthSquared >= normalEpsilonSquared)
                            continue;
                        if ((edgeUv0 - candidateUv0).LengthSquared < uvEpsilonSquared
                            && (edgeUv1 - candidateUv1).LengthSquared < uvEpsilonSquared)
                            continue;

                        var edgeUvDelta = (edgeUv1 - edgeUv0).ToVector3();
                        var collinear0 = edgeUvDelta.CrossProduct((candidateUv0 - edgeUv0).ToVector3()).LengthSquared
                            < uvEpsilonSquared;
                        var collinear1 = edgeUvDelta.CrossProduct((candidateUv1 - edgeUv0).ToVector3()).LengthSquared
                            < uvEpsilonSquared;
                        if (collinear0 && collinear1)
                            continue;

                        var seam = new LightmapSeam();
                        seam.Positions[0] = edgeUv0;
                        seam.Positions[1] = edgeUv1;
                        seam.OtherPositions[0] = candidateUv0;
                        seam.OtherPositions[1] = candidateUv1;
                        seams.Add(seam);
                    }
                }

                if (sharedLightmapUv)
                    break;
            }
        }
        return seams;
    }

    public static LightmapGeometryBakingScenesArray GenerateLightmapGeometryBakingScenes(Context context,
        IReadOnlyList<Component> geometries, int lightmapSize, LightmapGeometryBakingSettings settings)
    {
        var texelSize = Vector2f.One / lightmapSize;
        var scaledAndConstBias = new Vector2f(settings.ScaledPositionBias, settings.ConstantPositionBias);
        var bakingMaterial = context.GetSubsystem<ResourceCache>()!.GetResourceOrNull<Material>(settings.MaterialName);
        if (bakingMaterial is null)
        {
            Log.GetLogger().LogError("Cannot load material '{Material}'", settings.MaterialName);
            return new LightmapGeometryBakingScenesArray();
        }

        var modelSeams = new Dictionary<Model, List<LightmapSeam>>();
        foreach (var geometry in geometries)
        {
            if (geometry is StaticModel staticModel && staticModel.GetModel() is { } model)
                modelSeams.TryAdd(model, CollectModelSeams(model, checked((int)settings.UVChannel)));
        }

        var result = new LightmapGeometryBakingScenesArray();
        result.IdToObject.Add(new GeometryIdToObjectMapping());
        var bakingScenes = new Dictionary<int, LightmapGeometryBakingScene>();
        for (var objectIndex = 0; objectIndex < geometries.Count; ++objectIndex)
        {
            var geometry = geometries[objectIndex];
            var node = geometry.Node!;
            var lightmapIndex = Helpers.GetLightmapIndex(geometry);
            var scaleOffset = Helpers.GetLightmapScaleOffset(geometry);
            var scale = scaleOffset.ToVector2();
            var offset = new Vector2f(scaleOffset.Z, scaleOffset.W);
            if (!bakingScenes.TryGetValue(lightmapIndex, out var bakingScene))
            {
                var scene = new Scene();
                scene.CreateComponent<Octree>();
                var cameraNode = scene.CreateChild();
                cameraNode.SetPosition(Vector3f.Back * MathDefs.LargeValue);
                var camera = cameraNode.CreateComponent<Camera>();
                camera.SetFarClip(MathDefs.LargeValue * 2.0f);
                camera.SetOrthographic(true);
                camera.SetOrthoSize(MathDefs.LargeValue * 2.0f);
                bakingScene = new LightmapGeometryBakingScene
                {
                    Context = context,
                    Index = lightmapIndex,
                    LightmapSize = lightmapSize,
                    Scene = scene,
                    Camera = camera
                };
            }

            if (geometry is StaticModel staticModel)
            {
                var bakingNode = bakingScene.Scene!.CreateChild();
                bakingNode.SetPosition(node.GetWorldPosition());
                bakingNode.SetRotation(node.GetWorldRotation());
                bakingNode.SetScale(node.GetWorldScale());
                foreach (var seam in modelSeams[staticModel.GetModel()!])
                    bakingScene.Seams.Add(seam.Transformed(scale, offset));
                var staticModelForLightmap = bakingNode.CreateComponent<StaticModelForLightmap>();
                result.IdToObject.AddRange(staticModelForLightmap.Initialize(objectIndex, staticModel,
                    bakingMaterial, result.IdToObject.Count, MultiTapOffsets, texelSize,
                    scaleOffset, scaledAndConstBias));
            }
            else if (geometry is Terrain terrain)
            {
                for (var tap = 0; tap < MultiTapOffsets.Length; ++tap)
                {
                    var bakingNode = bakingScene.Scene!.CreateChild();
                    bakingNode.SetPosition(node.GetWorldPosition());
                    bakingNode.SetRotation(node.GetWorldRotation());
                    bakingNode.SetScale(node.GetWorldScale());
                    var terrainForLightmap = bakingNode.CreateComponent<Terrain>();
                    terrainForLightmap.SetMaxLodLevels(1);
                    terrainForLightmap.SetSpacing(terrain.GetSpacing());
                    terrainForLightmap.SetPatchSize(terrain.GetPatchSize());
                    terrainForLightmap.SetSmoothing(terrain.GetSmoothing());
                    terrainForLightmap.SetBakeLightmap(true);
                    terrainForLightmap.SetScaleInLightmap(0.0f);
                    terrainForLightmap.SetHeightMap(terrain.GetHeightMap());
                    var material = Helpers.CreateBakingMaterial(bakingMaterial, terrain.GetMaterial(),
                        scaleOffset, tap, MultiTapOffsets.Length, MultiTapOffsets[tap] * texelSize,
                        result.IdToObject.Count, scaledAndConstBias);
                    terrainForLightmap.SetMaterial(material);
                }
                result.IdToObject.Add(new GeometryIdToObjectMapping
                {
                    ObjectIndex = objectIndex,
                    GeometryIndex = 0,
                    LodIndex = 0
                });
            }
            bakingScenes[lightmapIndex] = bakingScene;
        }

        result.BakingScenes.AddRange(bakingScenes.Values);
        return result;
    }

    public static LightmapChartGeometryBuffer BakeLightmapGeometryBuffer(LightmapGeometryBakingScene bakingScene)
    {
        var graphics = bakingScene.Context!.GetSubsystem<Graphics>()!;
        if (!graphics.BeginFrame())
        {
            Log.GetLogger().LogError("Failed to begin lightmap geometry buffer rendering");
            return new LightmapChartGeometryBuffer();
        }

        try
        {
            var result = new LightmapChartGeometryBuffer(bakingScene.Index, bakingScene.LightmapSize);
            using var viewport = new Viewport(bakingScene.Scene, bakingScene.Camera);
            using var view = new LightmapRenderPipelineView();
            view.RenderGeometryBuffer(viewport, result.LightmapSize);
            var pixelCount = result.LightmapSize * result.LightmapSize;
            var buffer = new Vector4f[pixelCount];

            ReadTexture(view.PositionBuffer!, buffer);
            for (var i = 0; i < pixelCount; ++i)
            {
                result.Positions[i] = buffer[i].ToVector3();
                result.GeometryIds[i] = checked((int)buffer[i].W);
            }
            ReadTexture(view.SmoothPositionBuffer!, buffer);
            for (var i = 0; i < pixelCount; ++i)
            {
                result.SmoothPositions[i] = buffer[i].ToVector3();
                result.TexelRadiuses[i] = buffer[i].W;
            }
            ReadTexture(view.FaceNormalBuffer!, buffer);
            for (var i = 0; i < pixelCount; ++i)
                result.FaceNormals[i] = buffer[i].ToVector3();
            ReadTexture(view.SmoothNormalBuffer!, buffer);
            for (var i = 0; i < pixelCount; ++i)
                result.SmoothNormals[i] = buffer[i].ToVector3();
            ReadTexture(view.AlbedoBuffer!, buffer);
            for (var i = 0; i < pixelCount; ++i)
                result.Albedo[i] = buffer[i].ToVector3();
            ReadTexture(view.EmissionBuffer!, buffer);
            for (var i = 0; i < pixelCount; ++i)
                result.Emission[i] = buffer[i].ToVector3();
            result.Seams.AddRange(bakingScene.Seams.Select(static seam => seam.Clone()));
            return result;
        }
        finally
        {
            graphics.EndFrame();
        }
    }

    public static List<LightmapChartGeometryBuffer> BakeLightmapGeometryBuffers(
        IReadOnlyList<LightmapGeometryBakingScene> bakingScenes)
    {
        var result = new List<LightmapChartGeometryBuffer>(bakingScenes.Count);
        foreach (var bakingScene in bakingScenes)
            result.Add(BakeLightmapGeometryBuffer(bakingScene));
        return result;
    }

    private static void ReadTexture(RawTexture texture, Vector4f[] destination)
    {
        var bytes = MemoryMarshal.AsBytes(destination.AsSpan());
        texture.Read(0, 0, bytes);
    }
}
