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

using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.Scene;

/// <summary>Interface of scene collector for light baking.</summary>
/// <remarks>Objects may be loaded and unloaded even if scene is locked if it doesn't affect the outcome.</remarks>
public abstract class BakedSceneCollector : IDisposable
{
    /// <summary>Destruct.</summary>
    public virtual void Dispose()
    {
    }

    /// <summary>Called before everything else. Scene objects must stay unchanged after this call.</summary>
    public abstract void LockScene(Scene? scene, Vector3f chunkSize);
    /// <summary>Return all scene chunks.</summary>
    public abstract List<Vector3i> GetChunks();
    /// <summary>Return all scene backgrounds. [0] is expected to be pitch-black background.</summary>
    public abstract IReadOnlyList<BakedSceneBackground>? GetBackgrounds();

    /// <summary>Return unique geometries within chunk.</summary>
    public abstract List<Component> GetUniqueGeometries(Vector3i chunkIndex);
    /// <summary>Called when geometries were changed externally.</summary>
    public abstract void CommitGeometries(Vector3i chunkIndex);
    /// <summary>Return unique light probe groups within chunk. Order of groups must stay the same for each call.</summary>
    public abstract List<LightProbeGroup> GetUniqueLightProbeGroups(Vector3i chunkIndex);
    /// <summary>Return zone that corresponds to light probe group within chunk.</summary>
    public abstract Zone? GetLightProbeGroupZone(Vector3i chunkIndex, LightProbeGroup? lightProbeGroup);
    /// <summary>Return background index for zone within chunk.</summary>
    public abstract uint GetZoneBackground(Vector3i chunkIndex, Zone? zone);

    /// <summary>Return bounding box of unique nodes of the chunk.</summary>
    public abstract BoundingBox GetChunkBoundingBox(Vector3i chunkIndex);
    /// <summary>Return lights intersecting given volume.</summary>
    public abstract List<Light> GetLightsInBoundingBox(Vector3i chunkIndex, BoundingBox boundingBox);
    /// <summary>Return geometries intersecting given volume.</summary>
    public abstract List<Component> GetGeometriesInBoundingBox(Vector3i chunkIndex, BoundingBox boundingBox);
    /// <summary>Return light probe groups intersecting given volume.</summary>
    public abstract List<LightProbeGroup> GetLightProbeGroupsInBoundingBox(Vector3i chunkIndex, BoundingBox boundingBox);
    /// <summary>Return geometries intersecting given frustum. The frustum is guaranteed to contain specified chunk.</summary>
    public abstract List<Component> GetGeometriesInFrustum(Vector3i chunkIndex, Frustum frustum);

    /// <summary>Called after everything else. Scene objects must stay unchanged until this call.</summary>
    public abstract void UnlockScene();
}

/// <summary>Standard scene collector for light baking.</summary>
public class DefaultBakedSceneCollector : BakedSceneCollector
{
    public override void Dispose()
    {
        UnlockScene();
        base.Dispose();
    }

    /// <summary>BakedSceneCollector implementation.</summary>
    public override void LockScene(Scene? scene, Vector3f chunkSize)
    {
        _scene = scene;
        _chunkSize = chunkSize;
        _octree = scene!.GetComponent<Octree>();

        // Estimate dimensions
        var children = scene.GetChildren(true);
        _boundingBox = CalculateLightmappedSceneBoundingBox(children);
        _chunkGridDimension = Vector3i.Max(Vector3i.One, Vector3f.RoundToInt(_boundingBox.Size / _chunkSize));
        var maxChunk = _chunkGridDimension - Vector3i.One;

        // Collect light probe groups
        scene.GetComponents(_lightProbeGroups);
        scene.GetComponents(_zones);

        // Collect nodes
        var drawablesToBeUpdated = new List<Drawable>();

        foreach (var node in children)
        {
            var position = node.GetWorldPosition();
            var index = (position - _boundingBox.Min) / _boundingBox.Size * _chunkGridDimension.ToVector3();
            var chunk = Vector3i.Min(Vector3i.Max(Vector3i.Zero, Vector3f.FloorToInt(index)), maxChunk);
            if (!_chunks.TryGetValue(chunk, out var chunkData))
                chunkData = new ChunkData();

            var staticModels = node.GetComponents<StaticModel>();
            var terrains = node.GetComponents<Terrain>();
            var lightProbeGroups = node.GetComponents<LightProbeGroup>();
            var lights = node.GetComponents<Light>();

            foreach (var staticModel in staticModels)
            {
                if (staticModel.IsEnabledEffective && staticModel.GetBakeLightmapEffective())
                {
                    chunkData.Geometries.Add(staticModel);
                    chunkData.BoundingBox.Merge(staticModel.GetWorldBoundingBox());
                    drawablesToBeUpdated.Add(staticModel);
                }
            }

            foreach (var terrain in terrains)
            {
                if (terrain.IsEnabledEffective && terrain.GetBakeLightmapEffective())
                {
                    chunkData.Geometries.Add(terrain);
                    chunkData.BoundingBox.Merge(terrain.CalculateWorldBoundingBox());
                    var numPatches = terrain.GetNumPatches();
                    for (var i = 0; i < numPatches.X * numPatches.Y; ++i)
                        drawablesToBeUpdated.Add(terrain.GetPatch((uint)i)!);
                }
            }

            foreach (var lightProbeGroup in lightProbeGroups)
            {
                if (lightProbeGroup.IsEnabledEffective)
                {
                    chunkData.LightProbeGroups.Add(lightProbeGroup);
                    chunkData.BoundingBox.Merge(lightProbeGroup.GetWorldBoundingBox());
                }
            }

            foreach (var light in lights)
            {
                if (light.GetLightMode() != LightMode.Realtime)
                    drawablesToBeUpdated.Add(light);
            }

            _chunks[chunk] = chunkData;
        }

        // Force zone updates for drawables
        foreach (var drawable in drawablesToBeUpdated)
        {
            drawable.GetMutableCachedZone() = _octree!.QueryZone(drawable.Node!.GetWorldPosition(), drawable.GetZoneMask());
        }

        // Prepare backgrounds
        var backgrounds = new List<BakedSceneBackground>
        {
            new()
        };

        foreach (var zone in _zones)
        {
            // Non-static zones always have black background
            if (!zone.IsBackgroundStatic())
            {
                _zoneToBackgroundMap.TryAdd(zone, 0);
                continue;
            }

            var background = new BakedSceneBackground();
            if (zone.GetZoneTexture() is { } texture)
            {
                var cache = _scene!.GetSubsystem<ResourceCache>()!;
                background.Image = cache.GetResourceOrNull<ImageCube>(texture.GetName());
                if (background.Image is not null)
                {
                    var bestLevel = background.Image.GetSphericalHarmonicsMipLevel();
                    background.Image = background.Image.GetDecompressedImageLevel(bestLevel);
                }
            }
            background.Color = background.Image is not null ? Color.White : zone.GetFogColor();
            background.Intensity = zone.GetBackgroundBrightness();

            _zoneToBackgroundMap.TryAdd(zone, (uint)backgrounds.Count);
            backgrounds.Add(background);
        }

        _backgrounds = backgrounds.AsReadOnly();
    }

    public override List<Vector3i> GetChunks()
    {
        return new List<Vector3i>(_chunks.Keys);
    }

    public override IReadOnlyList<BakedSceneBackground>? GetBackgrounds()
    {
        return _backgrounds;
    }

    public override List<Component> GetUniqueGeometries(Vector3i chunkIndex)
    {
        if (_chunks.TryGetValue(chunkIndex, out var chunkData))
            return new List<Component>(chunkData.Geometries);
        return [];
    }

    public override void CommitGeometries(Vector3i chunkIndex)
    {
    }

    public override List<LightProbeGroup> GetUniqueLightProbeGroups(Vector3i chunkIndex)
    {
        if (_chunks.TryGetValue(chunkIndex, out var chunkData))
            return new List<LightProbeGroup>(chunkData.LightProbeGroups);
        return [];
    }

    public override Zone? GetLightProbeGroupZone(Vector3i chunkIndex, LightProbeGroup? lightProbeGroup)
    {
        return _octree!.QueryZone(lightProbeGroup!.GetWorldBoundingBox().Center, lightProbeGroup.GetZoneMask()).Zone;
    }

    public override uint GetZoneBackground(Vector3i chunkIndex, Zone? zone)
    {
        return zone is not null && _zoneToBackgroundMap.TryGetValue(zone, out var backgroundIndex) ? backgroundIndex : 0;
    }

    public override BoundingBox GetChunkBoundingBox(Vector3i chunkIndex)
    {
        if (_chunks.TryGetValue(chunkIndex, out var chunkData))
            return chunkData.BoundingBox;
        return new BoundingBox();
    }

    public override List<Light> GetLightsInBoundingBox(Vector3i chunkIndex, BoundingBox boundingBox)
    {
        // Query drawables
        var drawables = new List<Drawable>();
        using var query = new BoxOctreeQuery(drawables, boundingBox, DrawableFlags.Light);
        _octree!.GetDrawables(query);

        // Collect lights
        var lights = new List<Light>();
        foreach (var drawable in drawables)
        {
            if (drawable is Light light)
            {
                if (light.GetLightMode() != LightMode.Realtime)
                    lights.Add(light);
            }
        }
        return lights;
    }

    public override List<Component> GetGeometriesInBoundingBox(Vector3i chunkIndex, BoundingBox boundingBox)
    {
        // Query drawables
        var drawables = new List<Drawable>();
        using var query = new BoxOctreeQuery(drawables, boundingBox, DrawableFlags.Geometry);
        _octree!.GetDrawables(query);

        // Collect static models
        return CollectGeometriesFromDrawables(new List<Drawable>(drawables));
    }

    public override List<LightProbeGroup> GetLightProbeGroupsInBoundingBox(
        Vector3i chunkIndex, BoundingBox boundingBox)
    {
        var groups = new List<LightProbeGroup>();
        foreach (var group in _lightProbeGroups)
        {
            if (group.IsEnabledEffective && group.GetWorldBoundingBox().IsInside(boundingBox) != Intersection.Outside)
                groups.Add(group);
        }
        return groups;
    }

    public override List<Component> GetGeometriesInFrustum(Vector3i chunkIndex, Frustum frustum)
    {
        // Query drawables
        var drawables = new List<Drawable>();
        using var query = new FrustumOctreeQuery(drawables, frustum, DrawableFlags.Geometry);
        _octree!.GetDrawables(query);

        return CollectGeometriesFromDrawables(new List<Drawable>(drawables));
    }

    public override void UnlockScene()
    {
        _scene = null;
        _chunkSize = Vector3f.Zero;
        _boundingBox = new BoundingBox();
        _chunkGridDimension = Vector3i.Zero;
        _octree = null;
        _chunks.Clear();
    }

    /// <summary>Filter drawables and return relevant components.</summary>
    private List<Component> CollectGeometriesFromDrawables(List<Drawable> drawables)
    {
        var geometries = new List<Component>();
        var terrains = new HashSet<Terrain>();
        foreach (var drawable in drawables)
        {
            if (drawable is StaticModel staticModel)
            {
                if (staticModel.GetBakeLightmap())
                    geometries.Add(staticModel);
            }
            if (drawable is TerrainPatch terrainPatch)
            {
                var parentNode = terrainPatch.Node!.GetParent()!;
                if (parentNode.GetComponent<Terrain>() is { } terrain)
                {
                    if (terrain.GetBakeLightmap())
                        terrains.Add(terrain);
                }
            }
        }

        foreach (var terrain in terrains)
            geometries.Add(terrain);

        return geometries;
    }

    /// <summary>Chunk data.</summary>
    private struct ChunkData
    {
        /// <summary>Unique geometries.</summary>
        public readonly List<Component> Geometries = [];
        /// <summary>Unique light probe groups.</summary>
        public readonly List<LightProbeGroup> LightProbeGroups = [];
        /// <summary>Bounding box.</summary>
        public BoundingBox BoundingBox = new();

        public ChunkData()
        {
        }
    }

    private static BoundingBox CalculateLightmappedSceneBoundingBox(IReadOnlyList<Node> nodes)
    {
        var boundingBox = new BoundingBox();
        foreach (var node in nodes)
        {
            var staticModels = node.GetComponents<StaticModel>();
            var terrains = node.GetComponents<Terrain>();
            var lightProbeGroups = node.GetComponents<LightProbeGroup>();

            foreach (var staticModel in staticModels)
            {
                if (staticModel.IsEnabledEffective && staticModel.GetBakeLightmap())
                    boundingBox.Merge(staticModel.GetWorldBoundingBox());
            }

            foreach (var terrain in terrains)
            {
                if (terrain.IsEnabledEffective && terrain.GetBakeLightmap())
                    boundingBox.Merge(terrain.CalculateWorldBoundingBox());
            }

            foreach (var lightProbeGroup in lightProbeGroups)
            {
                if (lightProbeGroup.IsEnabledEffective)
                    boundingBox.Merge(lightProbeGroup.GetWorldBoundingBox());
            }
        }

        // Pad bounding box
        var size = boundingBox.Size;
        if (size.X < MathDefs.Epsilon)
            boundingBox.Max.X += MathDefs.LargeEpsilon;
        if (size.Y < MathDefs.Epsilon)
            boundingBox.Max.Y += MathDefs.LargeEpsilon;
        if (size.Z < MathDefs.Epsilon)
            boundingBox.Max.Z += MathDefs.LargeEpsilon;

        return boundingBox;
    }

    /// <summary>Scene.</summary>
    private Scene? _scene;
    /// <summary>Chunk size.</summary>
    private Vector3f _chunkSize;
    /// <summary>Bounding box of the scene.</summary>
    private BoundingBox _boundingBox = new();
    /// <summary>Dimensions of chunk grid.</summary>
    private Vector3i _chunkGridDimension;
    /// <summary>Scene Octree.</summary>
    private Octree? _octree;

    /// <summary>All relevant objects within scene.</summary>
    private readonly Dictionary<Vector3i, ChunkData> _chunks = [];
    private readonly List<LightProbeGroup> _lightProbeGroups = [];
    private readonly List<Zone> _zones = [];

    /// <summary>Baking backgrounds.</summary>
    private IReadOnlyList<BakedSceneBackground>? _backgrounds;
    private readonly Dictionary<Zone, uint> _zoneToBackgroundMap = [];
}
