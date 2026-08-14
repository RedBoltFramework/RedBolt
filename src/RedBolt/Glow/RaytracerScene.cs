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
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using RedBolt.Bindings.Embree;
using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Logging;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.Scene;

using RTCDevice = RedBolt.Bindings.Embree.RTCDeviceTy;
using RTCGeometry = RedBolt.Bindings.Embree.RTCGeometryTy;
using RTCScene = RedBolt.Bindings.Embree.RTCSceneTy;

/// <summary>Material of raytracing geometry.</summary>
public sealed class RaytracingGeometryMaterial
{
    public bool Opaque { get; init; }
    public Vector3f DiffuseColor { get; init; }
    public float Alpha { get; init; }

    public bool StoreUv { get; init; }
    public Vector4f UOffset { get; init; }
    public Vector4f VOffset { get; init; }

    public string DiffuseImageName { get; init; } = string.Empty;
    public Image? DiffuseImage { get; set; }

    public Vector2f ConvertUv(Vector2f uv)
    {
        var u = uv.DotProduct(UOffset.ToVector2()) + UOffset.W;
        var v = uv.DotProduct(VOffset.ToVector2()) + VOffset.W;
        return new Vector2f(u, v);
    }

    public Color SampleDiffuse(Vector2f uv)
    {
        var x = MathDefs.Clamp(MathDefs.RoundToInt(uv.X * DiffuseImage!.GetWidth()), 0, DiffuseImage.GetWidth() - 1);
        var y = MathDefs.Clamp(MathDefs.RoundToInt(uv.Y * DiffuseImage.GetHeight()), 0, DiffuseImage.GetHeight() - 1);
        return DiffuseImage.GetPixel(x, y);
    }
}

/// <summary>Geometry for ray tracing.</summary>
public sealed class RaytracerGeometry
{
    public int ObjectIndex { get; init; }
    public int GeometryIndex { get; init; }
    public int LodIndex { get; init; }
    public int NumLods { get; init; }
    public int LightmapIndex { get; init; }
    public int RaytracerGeometryId { get; set; } = -1;
    public required RTCGeometry EmbreeGeometry { get; init; }
    public required RaytracingGeometryMaterial Material { get; init; }
}

/// <summary>Scene for ray tracing.</summary>
public sealed class RaytracerScene : IDisposable
{
    public const uint LightmapUvAttribute = 0;
    public const uint NormalAttribute = 1;
    public const uint UvAttribute = 2;
    public const uint MaxAttributes = 3;

    public const uint PrimaryLodGeometry = 0x00000001;
    public const uint SecondaryLodGeometry = 0x00000002;
    public const uint DirectShadowOnlyGeometry = 0x00000004;
    public const uint AllGeometry = 0xffffffff;

    [ThreadStatic]
    private static Func<RTCHit, bool>? _activeFilter;

    private bool _disposed;

    private RaytracerScene(Context context, RTCDevice device, RTCScene scene,
        List<RaytracerGeometry> geometries, IReadOnlyList<BakedSceneBackground> backgrounds, float maxDistance)
    {
        Context = context;
        Device = device;
        Scene = scene;
        Geometries = geometries;
        Backgrounds = backgrounds;
        MaxDistance = maxDistance;
    }

    public Context Context { get; }
    public RTCDevice Device { get; }
    public RTCScene Scene { get; }
    public IReadOnlyList<RaytracerGeometry> Geometries { get; }
    public IReadOnlyList<BakedSceneBackground> Backgrounds { get; }
    public float MaxDistance { get; }

    public void Dispose()
    {
        if (_disposed)
            return;

        Embree.RtcReleaseScene(Scene);
        Embree.RtcReleaseDevice(Device);
        _disposed = true;
    }

    internal static RTCIntersectArguments CreateIntersectArguments()
    {
        var result = new RTCIntersectArguments();
        Embree.RtcInitIntersectArguments(result);
        result.Filter = GetFilterFunctionPointer();
        result.Flags = RTCRayQueryFlags.RtcRayQueryFlagInvokeArgumentFilter;
        return result;
    }

    internal void Intersect(RTCRayHit rayHit, RTCIntersectArguments arguments, Func<RTCHit, bool> filter)
    {
        var previousFilter = _activeFilter;
        _activeFilter = filter;
        try
        {
            Embree.RtcIntersect1(Scene, rayHit, arguments);
        }
        finally
        {
            _activeFilter = previousFilter;
        }
    }

    private static unsafe nint GetFilterFunctionPointer() =>
        (nint)(delegate* unmanaged[Cdecl]<nint, void>)&FilterIntersection;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe void FilterIntersection(nint argumentsHandle)
    {
        var arguments = RTCFilterFunctionNArguments.CreateManagedWrapper(argumentsHandle, false);
        if (arguments.N != 1 || *(int*)arguments.Valid == 0)
            return;

        var hit = RTCHit.CreateManagedWrapper(arguments.Hit.Handle, false);
        if (_activeFilter is not null && !_activeFilter(hit))
            *(int*)arguments.Valid = 0;
    }

    /// <summary>Create scene for raytracing.</summary>
    public static RaytracerScene Create(Context context, IReadOnlyList<Component> geometries,
        int lightmapUvChannel, IReadOnlyList<BakedSceneBackground> backgrounds)
    {
        var parsedModels = ParseModels(geometries, lightmapUvChannel);
        var device = Embree.RtcNewDevice([]);
        var scene = Embree.RtcNewScene(device);
        Embree.RtcSetSceneFlags(scene, RTCSceneFlags.RtcSceneFlagFilterFunctionInArguments);

        var raytracerGeometries = new List<RaytracerGeometry>();
        for (var objectIndex = 0; objectIndex < geometries.Count; ++objectIndex)
        {
            var geometry = geometries[objectIndex];
            if (geometry is StaticModel staticModel && staticModel.GetModel() is { } model &&
                parsedModels.TryGetValue(model, out var modelView))
            {
                raytracerGeometries.AddRange(CreateForStaticModel(
                    device, modelView, staticModel, objectIndex, lightmapUvChannel));
            }
            else if (geometry is Terrain terrain)
            {
                raytracerGeometries.Add(CreateForTerrain(device, terrain, objectIndex, lightmapUvChannel));
            }
        }

        var geometryIndex = new List<RaytracerGeometry>();
        var diffuseImages = new Dictionary<string, Image?>();
        foreach (var raytracerGeometry in raytracerGeometries)
        {
            var geometryId = checked((int)Embree.RtcAttachGeometry(scene, raytracerGeometry.EmbreeGeometry));
            Embree.RtcReleaseGeometry(raytracerGeometry.EmbreeGeometry);

            while (geometryIndex.Count <= geometryId)
                geometryIndex.Add(null!);
            geometryIndex[geometryId] = raytracerGeometry;
            raytracerGeometry.RaytracerGeometryId = geometryId;
            diffuseImages.TryAdd(raytracerGeometry.Material.DiffuseImageName, null);
        }

        Embree.RtcCommitScene(scene);

        var cache = context.GetSubsystem<ResourceCache>()!;
        foreach (var name in diffuseImages.Keys.ToArray())
        {
            if (name.Length != 0)
                diffuseImages[name] = cache.Get<Image>(name).GetDecompressedImage();
        }

        foreach (var geometry in geometryIndex)
            geometry.Material.DiffuseImage = diffuseImages[geometry.Material.DiffuseImageName];

        var boundingBox = new BoundingBox();
        foreach (var geometry in geometries)
        {
            if (geometry is StaticModel staticModel)
                boundingBox.Merge(staticModel.GetWorldBoundingBox());
            else if (geometry is Terrain terrain)
                boundingBox.Merge(terrain.CalculateWorldBoundingBox());
        }

        var sceneSize = boundingBox.Size;
        var maxDistance = MathF.Max(sceneSize.X, MathF.Max(sceneSize.Y, sceneSize.Z));
        return new RaytracerScene(context, device, scene, geometryIndex, backgrounds, maxDistance);
    }

    private static Dictionary<Model, ModelView> ParseModels(IReadOnlyList<Component> geometries, int uvChannel)
    {
        var modelRequirements = new Dictionary<Model, bool>();
        foreach (var geometry in geometries)
        {
            if (geometry is not StaticModel staticModel || staticModel.GetModel() is not { } model)
                continue;

            var directShadowOnly = !staticModel.GetBakeLightmapEffective();
            modelRequirements[model] = modelRequirements.GetValueOrDefault(model) && !directShadowOnly;
        }

        var result = new Dictionary<Model, ModelView>();
        foreach (var (model, needLightmapUvAndNormal) in modelRequirements)
        {
            var modelView = new ModelView();
            if (!modelView.ImportModel(model))
                continue;

            var valid = true;
            foreach (var geometryView in modelView.GetGeometries())
            {
                foreach (var geometryLodView in geometryView.Lods)
                {
                    var vertexFormat = geometryLodView.VertexFormat;
                    var missingPosition = vertexFormat.Position == ModelVertexFormat.Undefined;
                    var missingNormal = vertexFormat.Normal == ModelVertexFormat.Undefined;
                    var missingUv = vertexFormat.Uv[uvChannel] == ModelVertexFormat.Undefined;
                    if (missingPosition || (needLightmapUvAndNormal && (missingNormal || missingUv)))
                    {
                        Log.GetLogger().LogError("Model '{Model}' doesn't have required vertex attributes", model.GetName());
                        valid = false;
                    }

                    if (geometryLodView.PrimitiveType != PrimitiveType.TriangleList)
                    {
                        Log.GetLogger().LogError("Model '{Model}' has unsupported primitive types", model.GetName());
                        valid = false;
                    }
                }
            }

            if (valid)
                result.Add(model, modelView);
        }
        return result;
    }

    private static IEnumerable<RaytracerGeometry> CreateForStaticModel(RTCDevice device, ModelView modelView,
        StaticModel staticModel, int objectIndex, int lightmapUvChannel)
    {
        var renderer = staticModel.GetSubsystem<Renderer>();
        var node = staticModel.Node!;
        var scaleOffset = staticModel.GetLightmapScaleOffset();
        var directShadowsOnly = !staticModel.GetBakeLightmapEffective();
        var lightmapIndex = directShadowsOnly ? -1 : checked((int)staticModel.GetLightmapIndex());

        var geometries = modelView.GetGeometries();
        for (var geometryIndex = 0; geometryIndex < geometries.Count; ++geometryIndex)
        {
            var material = staticModel.GetMaterial((uint)geometryIndex) ?? renderer.GetDefaultMaterial()!;
            var geometryView = geometries[geometryIndex];
            for (var lodIndex = 0; lodIndex < geometryView.Lods.Count; ++lodIndex)
            {
                var raytracingMaterial = CreateMaterial(material);
                var embreeGeometry = CreateForGeometryView(device, node.GetName(), node.GetWorldTransform(),
                    node.GetWorldRotation(), geometryView.Lods[lodIndex], raytracingMaterial,
                    directShadowsOnly, lodIndex == 0, lightmapUvChannel,
                    scaleOffset.ToVector2(), new Vector2f(scaleOffset.Z, scaleOffset.W));

                yield return new RaytracerGeometry
                {
                    ObjectIndex = objectIndex,
                    GeometryIndex = geometryIndex,
                    LodIndex = lodIndex,
                    NumLods = geometryView.Lods.Count,
                    LightmapIndex = lightmapIndex,
                    EmbreeGeometry = embreeGeometry,
                    Material = raytracingMaterial
                };
            }
        }
    }

    private static RaytracerGeometry CreateForTerrain(RTCDevice device, Terrain terrain,
        int objectIndex, int lightmapUvChannel)
    {
        var renderer = terrain.GetSubsystem<Renderer>();
        var material = CreateMaterial(terrain.GetMaterial() ?? renderer.GetDefaultMaterial()!);
        var directShadowsOnly = !terrain.GetBakeLightmapEffective();
        var lightmapIndex = directShadowsOnly ? -1 : checked((int)terrain.GetLightmapIndex());
        var scaleOffset = terrain.GetLightmapScaleOffset();

        return new RaytracerGeometry
        {
            ObjectIndex = objectIndex,
            GeometryIndex = 0,
            LodIndex = 0,
            NumLods = 1,
            LightmapIndex = lightmapIndex,
            EmbreeGeometry = CreateForTerrainGeometry(device, terrain, material, directShadowsOnly,
                lightmapUvChannel, scaleOffset.ToVector2(), new Vector2f(scaleOffset.Z, scaleOffset.W)),
            Material = material
        };
    }

    private static RaytracingGeometryMaterial CreateMaterial(Material material)
    {
        var opaque = Helpers.IsMaterialOpaque(material);
        var diffuseColor = Color.White;
        var uOffset = Vector4f.Zero;
        var vOffset = Vector4f.Zero;
        Texture? diffuseTexture = null;
        if (!opaque)
        {
            diffuseColor = Helpers.GetMaterialDiffuseColor(material);
            diffuseTexture = Helpers.GetMaterialDiffuseTexture(material, ref uOffset, ref vOffset);
        }

        return new RaytracingGeometryMaterial
        {
            Opaque = opaque,
            DiffuseColor = diffuseColor.ToVector3(),
            Alpha = diffuseColor.A,
            StoreUv = diffuseTexture is not null,
            UOffset = uOffset,
            VOffset = vOffset,
            DiffuseImageName = diffuseTexture?.GetName() ?? string.Empty
        };
    }

    private static unsafe RTCGeometry CreateForGeometryView(RTCDevice device, string nodeName,
        Matrix3x4 worldTransform, Quaternion worldRotation, GeometryLODView geometry,
        RaytracingGeometryMaterial material, bool directShadowsOnly, bool primaryLod,
        int lightmapUvChannel, Vector2f lightmapUvScale, Vector2f lightmapUvOffset)
    {
        var result = Embree.RtcNewGeometry(device, RTCGeometryType.RtcGeometryTypeTriangle);
        Embree.RtcSetGeometryVertexAttributeCount(result, MaxAttributes);

        var vertices = new Span<float>((void*)Embree.RtcSetNewGeometryBuffer(result,
            RTCBufferType.RtcBufferTypeVertex, 0, RTCFormat.RtcFormatFloat3,
            (nuint)(sizeof(float) * 3), (nuint)geometry.Vertices.Count), geometry.Vertices.Count * 3);

        Span<float> lightmapUvs = default;
        Span<float> smoothNormals = default;
        if (!directShadowsOnly && primaryLod)
        {
            lightmapUvs = new Span<float>((void*)Embree.RtcSetNewGeometryBuffer(result,
                RTCBufferType.RtcBufferTypeVertexAttribute, LightmapUvAttribute, RTCFormat.RtcFormatFloat2,
                (nuint)(sizeof(float) * 2), (nuint)geometry.Vertices.Count), geometry.Vertices.Count * 2);
            smoothNormals = new Span<float>((void*)Embree.RtcSetNewGeometryBuffer(result,
                RTCBufferType.RtcBufferTypeVertexAttribute, NormalAttribute, RTCFormat.RtcFormatFloat3,
                (nuint)(sizeof(float) * 3), (nuint)geometry.Vertices.Count), geometry.Vertices.Count * 3);
        }

        Span<float> uvs = default;
        if (material.StoreUv)
        {
            uvs = new Span<float>((void*)Embree.RtcSetNewGeometryBuffer(result,
                RTCBufferType.RtcBufferTypeVertexAttribute, UvAttribute, RTCFormat.RtcFormatFloat2,
                (nuint)(sizeof(float) * 2), (nuint)geometry.Vertices.Count), geometry.Vertices.Count * 2);
        }

        var errorReported = false;
        for (var i = 0; i < geometry.Vertices.Count; ++i)
        {
            var sourceVertex = geometry.Vertices[i];
            var worldPosition = worldTransform * sourceVertex.Position.ToVector3();
            vertices[i * 3] = worldPosition.X;
            vertices[i * 3 + 1] = worldPosition.Y;
            vertices[i * 3 + 2] = worldPosition.Z;

            if (!lightmapUvs.IsEmpty)
            {
                var uv = sourceVertex.Uv[lightmapUvChannel].ToVector2() * lightmapUvScale + lightmapUvOffset;
                var clamped = Vector2f.Max(Vector2f.Zero, Vector2f.Min(uv, Vector2f.One));
                if (!errorReported && uv != clamped)
                {
                    errorReported = true;
                    Log.GetLogger().LogWarning("Lightmap UVs for node {Node} are clamped, lighting may be incorrect", nodeName);
                }
                lightmapUvs[i * 2] = clamped.X;
                lightmapUvs[i * 2 + 1] = clamped.Y;

                var normal = worldRotation * sourceVertex.Normal.ToVector3();
                smoothNormals[i * 3] = normal.X;
                smoothNormals[i * 3 + 1] = normal.Y;
                smoothNormals[i * 3 + 2] = normal.Z;
            }

            if (!uvs.IsEmpty)
            {
                var uv = material.ConvertUv(sourceVertex.Uv[0].ToVector2());
                var clamped = Vector2f.Max(Vector2f.Zero, Vector2f.Min(uv, Vector2f.One));
                if (!errorReported && uv != clamped)
                {
                    errorReported = true;
                    Log.GetLogger().LogWarning("UVs for node {Node} are clamped, lighting may be incorrect", nodeName);
                }
                uvs[i * 2] = clamped.X;
                uvs[i * 2 + 1] = clamped.Y;
            }
        }

        var indices = new Span<uint>((void*)Embree.RtcSetNewGeometryBuffer(result,
            RTCBufferType.RtcBufferTypeIndex, 0, RTCFormat.RtcFormatUint3,
            (nuint)(sizeof(uint) * 3), (nuint)(geometry.Indices.Count / 3)), geometry.Indices.Count);
        geometry.Indices.CopyTo(indices);

        Embree.RtcSetGeometryMask(result, GetGeometryMask(directShadowsOnly, primaryLod));
        Embree.RtcCommitGeometry(result);
        return result;
    }

    private static unsafe RTCGeometry CreateForTerrainGeometry(RTCDevice device, Terrain terrain,
        RaytracingGeometryMaterial material, bool directShadowsOnly, int lightmapUvChannel,
        Vector2f lightmapUvScale, Vector2f lightmapUvOffset)
    {
        _ = lightmapUvChannel;
        var terrainSize = terrain.GetNumVertices();
        var numPatches = terrain.GetNumPatches();
        var patchSize = terrain.GetPatchSize();
        var numVertices = terrainSize.X * terrainSize.Y;
        var numQuads = numPatches.X * numPatches.Y * patchSize * patchSize;

        var result = Embree.RtcNewGeometry(device, RTCGeometryType.RtcGeometryTypeTriangle);
        Embree.RtcSetGeometryVertexAttributeCount(result, MaxAttributes);
        var vertices = new Span<float>((void*)Embree.RtcSetNewGeometryBuffer(result,
            RTCBufferType.RtcBufferTypeVertex, 0, RTCFormat.RtcFormatFloat3,
            (nuint)(sizeof(float) * 3), (nuint)numVertices), numVertices * 3);

        Span<float> lightmapUvs = default;
        Span<float> smoothNormals = default;
        if (!directShadowsOnly)
        {
            lightmapUvs = new Span<float>((void*)Embree.RtcSetNewGeometryBuffer(result,
                RTCBufferType.RtcBufferTypeVertexAttribute, LightmapUvAttribute, RTCFormat.RtcFormatFloat2,
                (nuint)(sizeof(float) * 2), (nuint)numVertices), numVertices * 2);
            smoothNormals = new Span<float>((void*)Embree.RtcSetNewGeometryBuffer(result,
                RTCBufferType.RtcBufferTypeVertexAttribute, NormalAttribute, RTCFormat.RtcFormatFloat3,
                (nuint)(sizeof(float) * 3), (nuint)numVertices), numVertices * 3);
        }

        Span<float> uvs = default;
        if (material.StoreUv)
        {
            uvs = new Span<float>((void*)Embree.RtcSetNewGeometryBuffer(result,
                RTCBufferType.RtcBufferTypeVertexAttribute, UvAttribute, RTCFormat.RtcFormatFloat2,
                (nuint)(sizeof(float) * 2), (nuint)numVertices), numVertices * 2);
        }

        for (var i = 0; i < numVertices; ++i)
        {
            var x = i % terrainSize.X;
            var y = terrainSize.Y - i / terrainSize.X - 1;
            var worldPosition = terrain.HeightMapToWorld(new Vector2i(x, y));
            var uv = terrain.HeightMapToUV(new Vector2i(x, y));
            vertices[i * 3] = worldPosition.X;
            vertices[i * 3 + 1] = worldPosition.Y;
            vertices[i * 3 + 2] = worldPosition.Z;

            if (!lightmapUvs.IsEmpty)
            {
                var lightmapUv = uv * lightmapUvScale + lightmapUvOffset;
                lightmapUvs[i * 2] = lightmapUv.X;
                lightmapUvs[i * 2 + 1] = lightmapUv.Y;
                var normal = terrain.GetNormal(worldPosition);
                smoothNormals[i * 3] = normal.X;
                smoothNormals[i * 3 + 1] = normal.Y;
                smoothNormals[i * 3 + 2] = normal.Z;
            }

            if (!uvs.IsEmpty)
            {
                var materialUv = material.ConvertUv(uv);
                uvs[i * 2] = materialUv.X;
                uvs[i * 2 + 1] = materialUv.Y;
            }
        }

        var indices = new Span<uint>((void*)Embree.RtcSetNewGeometryBuffer(result,
            RTCBufferType.RtcBufferTypeIndex, 0, RTCFormat.RtcFormatUint3,
            (nuint)(sizeof(uint) * 3), (nuint)(numQuads * 2)), numQuads * 6);
        var index = 0;
        var row = terrainSize.X;
        for (uint z = 0; z < numPatches.Y * patchSize; ++z)
        {
            for (uint x = 0; x < numPatches.X * patchSize; ++x)
            {
                indices[index++] = (z + 1) * (uint)row + x;
                indices[index++] = z * (uint)row + x + 1;
                indices[index++] = z * (uint)row + x;
                indices[index++] = (z + 1) * (uint)row + x;
                indices[index++] = (z + 1) * (uint)row + x + 1;
                indices[index++] = z * (uint)row + x + 1;
            }
        }

        Embree.RtcSetGeometryMask(result, GetGeometryMask(directShadowsOnly, true));
        Embree.RtcCommitGeometry(result);
        return result;
    }

    private static uint GetGeometryMask(bool directShadowsOnly, bool primaryLod) => directShadowsOnly
        ? DirectShadowOnlyGeometry
        : primaryLod ? PrimaryLodGeometry : SecondaryLodGeometry;
}
