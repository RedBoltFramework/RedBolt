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

using RedBolt.Graphics;
using RedBolt.Logging;
using RedBolt.Core;
using RedBolt.Math;
using RedBolt.Scene;

/// <summary>Light probe collection with extra data needed for baking.</summary>
public class LightProbeCollectionForBaking : LightProbeCollection, ICloneable
{
    /// <summary>Size is the same as number of probes.</summary>
    public readonly List<uint> LightMasks = [];
    /// <summary>Size is the same as number of probes.</summary>
    public readonly List<uint> BackgroundIds = [];

    private LightProbeCollectionForBaking(LightProbeCollectionForBaking source)
        : base(source)
    {
        LightMasks.AddRange(source.LightMasks);
        BackgroundIds.AddRange(source.BackgroundIds);
    }

    public LightProbeCollectionForBaking()
    {
    }

    public new object Clone() => new LightProbeCollectionForBaking(this);
}

/// <summary>Baking chunk. Contains everything needed to bake light for one chunk.</summary>
public sealed class BakedSceneChunk : IDisposable
{
    /// <summary>Lightmaps owned by this chunk.</summary>
    public List<int> Lightmaps { get; } = [];
    /// <summary>Direct lightmaps required to bake this chunk.</summary>
    public List<int> RequiredDirectLightmaps { get; } = [];

    /// <summary>Raytracer scene.</summary>
    public required RaytracerScene RaytracerScene { get; init; }
    /// <summary>Geometry buffers.</summary>
    public List<LightmapChartGeometryBuffer> GeometryBuffers { get; } = [];
    /// <summary>Geometry buffer ID to raytracer geometry ID mapping.</summary>
    public List<int> GeometryBufferToRaytracer { get; } = [];
    /// <summary>Lights to bake.</summary>
    public List<BakedLight> BakedLights { get; } = [];
    /// <summary>Light probes collection.</summary>
    public LightProbeCollectionForBaking LightProbesCollection { get; init; } = new();
    /// <summary>Number of unique light probe groups.</summary>
    public int NumUniqueLightProbes { get; init; }

    public void Dispose() => RaytracerScene.Dispose();

    /// <summary>Create baked scene chunk.</summary>
    public static BakedSceneChunk Create(Context context, BakedSceneCollector collector,
        Vector3i chunk, LightBakingSettings settings)
    {
        var uniqueGeometries = collector.GetUniqueGeometries(chunk);
        var uniqueLightProbeGroups = collector.GetUniqueLightProbeGroups(chunk);
        var lightsInChunk = collector.GetLightsInBoundingBox(chunk, collector.GetChunkBoundingBox(chunk));
        var lightProbeGroupsInChunk = CollectLightProbeGroups(
            collector, chunk, uniqueLightProbeGroups);
        var geometriesInChunk = CollectGeometries(collector, chunk, uniqueGeometries, lightsInChunk,
            settings.Incremental.DirectionalLightShadowDistance, settings.Incremental.IndirectPadding);

        var geometryBakingScenes = LightmapGeometryBuffer.GenerateLightmapGeometryBakingScenes(
            context, uniqueGeometries, checked((int)settings.Charting.LightmapSize), settings.GeometryBufferBaking);
        var geometryBuffers = LightmapGeometryBuffer.BakeLightmapGeometryBuffers(
            geometryBakingScenes.BakingScenes);
        foreach (var bakingScene in geometryBakingScenes.BakingScenes)
            bakingScene.Scene?.Dispose();

        var lightProbesCollection = new LightProbeCollectionForBaking();
        LightProbeGroup.CollectLightProbes(lightProbeGroupsInChunk, lightProbesCollection, null);
        for (var i = 0; i < lightProbeGroupsInChunk.Count; ++i)
        {
            var group = lightProbeGroupsInChunk[i];
            var zone = collector.GetLightProbeGroupZone(chunk, group)!;
            var numProbes = lightProbesCollection.Counts[i];
            var lightMask = zone.GetLightMask() & group.GetLightMask();
            var backgroundId = collector.GetZoneBackground(chunk, zone);
            for (var probe = 0; probe < numProbes; ++probe)
            {
                lightProbesCollection.LightMasks.Add(lightMask);
                lightProbesCollection.BackgroundIds.Add(backgroundId);
            }
        }

        var raytracerScene = RaytracerScene.Create(context, geometriesInChunk,
            checked((int)settings.GeometryBufferBaking.UVChannel), collector.GetBackgrounds()!);
        var geometryMapping = CreateGeometryMapping(geometryBakingScenes.IdToObject, raytracerScene.Geometries);

        foreach (var geometryBuffer in geometryBuffers)
        {
            LightTracer.PreprocessGeometryBuffer(geometryBuffer, raytracerScene, geometryMapping,
                settings.GeometryBufferPreprocessing);
            Helpers.ParallelFor(geometryBuffer.Positions.Count,
                checked((int)settings.GeometryBufferPreprocessing.NumTasks), (fromIndex, toIndex) =>
            {
                for (var index = fromIndex; index < toIndex; ++index)
                {
                    var geometryId = geometryBuffer.GeometryIds[index];
                    if (geometryId == 0)
                        continue;
                    var objectIndex = geometryBakingScenes.IdToObject[geometryId].ObjectIndex;
                    var geometry = uniqueGeometries[objectIndex];
                    if (geometry is Terrain terrain)
                    {
                        var numPatches = terrain.GetNumPatches();
                        var patchIndex = Vector2i.Min(numPatches / 2, numPatches - Vector2i.One);
                        var drawable = terrain.GetPatch(patchIndex.X, patchIndex.Y)!;
                        var zone = drawable.GetMutableCachedZone().Zone;
                        geometryBuffer.LightMasks[index] = drawable.GetLightMaskInZone();
                        geometryBuffer.BackgroundIds[index] = checked((int)collector.GetZoneBackground(chunk, zone));
                    }
                    else if (geometry is Drawable drawable)
                    {
                        var zone = drawable.GetMutableCachedZone().Zone;
                        geometryBuffer.LightMasks[index] = drawable.GetLightMaskInZone();
                        geometryBuffer.BackgroundIds[index] = checked((int)collector.GetZoneBackground(chunk, zone));
                    }
                }
            });
        }

        var result = new BakedSceneChunk
        {
            RaytracerScene = raytracerScene,
            LightProbesCollection = lightProbesCollection,
            NumUniqueLightProbes = uniqueLightProbeGroups.Count
        };
        result.Lightmaps.AddRange(geometryBuffers.Select(static geometryBuffer => geometryBuffer.Index));
        result.RequiredDirectLightmaps.AddRange(raytracerScene.Geometries
            .Where(static geometry => geometry.LightmapIndex >= 0)
            .Select(static geometry => geometry.LightmapIndex)
            .Distinct());
        result.GeometryBuffers.AddRange(geometryBuffers);
        result.GeometryBufferToRaytracer.AddRange(geometryMapping);
        result.BakedLights.AddRange(lightsInChunk.Select(static light => new BakedLight(light)));
        return result;
    }

    private static List<Component> CollectGeometries(BakedSceneCollector collector, Vector3i chunk,
        IReadOnlyList<Component> uniqueGeometries, IReadOnlyList<Light> lights,
        float directionalLightShadowDistance, float indirectPadding)
    {
        var receiverBounds = collector.GetChunkBoundingBox(chunk);
        var relevantGeometries = new HashSet<Component>();
        foreach (var light in lights)
        {
            if (light.GetLightType() == LightType.Directional)
            {
                var frustum = CalculateDirectionalLightFrustum(receiverBounds,
                    light.Node!.GetWorldDirection(), directionalLightShadowDistance, 0.0f);
                relevantGeometries.UnionWith(collector.GetGeometriesInFrustum(chunk, frustum));
            }
            else
            {
                var extendedBounds = new BoundingBox(receiverBounds);
                extendedBounds.Merge(light.Node!.GetWorldPosition());
                var shadowCasterBounds = new BoundingBox(light.GetWorldBoundingBox());
                shadowCasterBounds.Clip(extendedBounds);
                relevantGeometries.UnionWith(collector.GetGeometriesInBoundingBox(chunk, shadowCasterBounds));
            }
        }

        var indirectBounds = new BoundingBox(receiverBounds);
        indirectBounds.Min -= Vector3f.One * indirectPadding;
        indirectBounds.Max += Vector3f.One * indirectPadding;
        relevantGeometries.UnionWith(collector.GetGeometriesInBoundingBox(chunk, indirectBounds));
        relevantGeometries.ExceptWith(uniqueGeometries);
        return [.. uniqueGeometries, .. relevantGeometries];
    }

    private static List<LightProbeGroup> CollectLightProbeGroups(BakedSceneCollector collector,
        Vector3i chunk, IReadOnlyList<LightProbeGroup> uniqueGroups)
    {
        var groups = new HashSet<LightProbeGroup>(collector.GetLightProbeGroupsInBoundingBox(
            chunk, collector.GetChunkBoundingBox(chunk)));
        groups.ExceptWith(uniqueGroups);
        return [.. uniqueGroups, .. groups];
    }

    private static List<int> CreateGeometryMapping(IReadOnlyList<GeometryIdToObjectMapping> idToObject,
        IReadOnlyList<RaytracerGeometry> raytracerGeometries)
    {
        var sorted = raytracerGeometries.OrderBy(static geometry => geometry.ObjectIndex)
            .ThenBy(static geometry => geometry.GeometryIndex)
            .ThenBy(static geometry => geometry.LodIndex).ToList();
        var matching = idToObject.Count <= sorted.Count + 1;
        if (matching)
        {
            for (var i = 1; i < idToObject.Count; ++i)
            {
                var geometry = sorted[i - 1];
                var mapping = idToObject[i];
                if (geometry.ObjectIndex != mapping.ObjectIndex || geometry.GeometryIndex != mapping.GeometryIndex
                    || geometry.LodIndex != mapping.LodIndex)
                {
                    matching = false;
                    break;
                }
            }
        }
        if (!matching)
            Log.GetLogger().LogError("Cannot match raytracer geometries with lightmap G-Buffer");

        var result = Enumerable.Repeat(-1, idToObject.Count).ToList();
        if (matching)
        {
            for (var i = 1; i < idToObject.Count; ++i)
                result[i] = sorted[i - 1].RaytracerGeometryId;
        }
        return result;
    }

    private static Frustum CalculateDirectionalLightFrustum(BoundingBox boundingBox,
        Vector3f lightDirection, float distance, float angle)
    {
        var rotation = new Quaternion(Vector3f.Down, lightDirection);
        var widthPadding = distance * MathDefs.Sin(angle);
        var lightSpaceBounds = boundingBox.Transformed(rotation.Inverse.RotationMatrix);
        lightSpaceBounds.Min.X -= widthPadding;
        lightSpaceBounds.Min.Z -= widthPadding;
        lightSpaceBounds.Max.X += widthPadding;
        lightSpaceBounds.Max.Z += widthPadding;
        lightSpaceBounds.Max.Y += distance;
        var frustum = new Frustum();
        frustum.Define(lightSpaceBounds, new Matrix3x4(rotation.RotationMatrix));
        return frustum;
    }
}
