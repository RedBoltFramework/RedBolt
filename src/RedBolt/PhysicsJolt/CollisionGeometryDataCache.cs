// Copyright (c) 2025-2026 the rbfx project.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.PhysicsJolt;

using System.Buffers.Binary;
using System.Runtime.InteropServices;

using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Logging;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.RenderAPI;

/// <summary>Base class for collision shape geometry data.</summary>
public class CollisionGeometryData : RedObject
{
    internal CollisionGeometryData(Vector3f[] vertices, uint[] indices)
    {
        Vertices = vertices;
        Indices = indices;
    }

    internal Vector3f[] Vertices { get; }
    internal uint[] Indices { get; }
}

/// <summary>Cache of collision geometry data.</summary>
public sealed class CollisionGeometryDataCache : RedObject
{
    private const int Vector3Size = 3 * sizeof(float);

    private sealed class CacheEntry(Model model)
    {
        public readonly WeakReference<Model> Model = new(model);
        public readonly List<WeakReference<CollisionGeometryData>?> Lods = [];
    }

    private readonly WorkQueue _workQueue;
    private readonly ShapeType _shapeType;
    private readonly object _mutex = new();
    private readonly List<CacheEntry> _cache = [];

    /// <summary>Construct a cache for one geometry shape type.</summary>
    public CollisionGeometryDataCache(ShapeType shapeType)
    {
        _workQueue = GetSubsystem<WorkQueue>();
        _shapeType = shapeType;
    }

    /// <summary>Return existing or create new collision geometry. Cached if applicable.</summary>
    public CollisionGeometryData GetOrCreateGeometry(Model model, uint lodLevel)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (GetCachedGeometry(model, lodLevel) is { } cachedGeometry)
            return cachedGeometry;

        var geometry = CreateCollisionGeometryData(_shapeType, model, lodLevel);
        if (!HasDynamicBuffers(model, lodLevel))
        {
            StoreCachedGeometry(model, lodLevel, geometry);

            var weakSelf = new WeakReference<CollisionGeometryDataCache>(this);
            var weakModel = new WeakReference<Model>(model);
            _workQueue.PostTaskForMainThread(() =>
            {
                if (weakSelf.TryGetTarget(out var self) && weakModel.TryGetTarget(out var targetModel))
                    self.SubscribeToReload(targetModel);
            });
        }
        return geometry;
    }

    /// <summary>Return cached geometry. Does not create new geometry.</summary>
    public CollisionGeometryData? GetCachedGeometry(Model model, uint lodLevel)
    {
        ArgumentNullException.ThrowIfNull(model);
        lock (_mutex)
        {
            var entry = FindEntry(model);
            if (entry is null || lodLevel >= entry.Lods.Count)
                return null;
            return entry.Lods[(int)lodLevel]?.TryGetTarget(out var geometry) == true ? geometry : null;
        }
    }

    /// <summary>Release cache entry for model.</summary>
    public void ReleaseCachedGeometry(Model model)
    {
        ArgumentNullException.ThrowIfNull(model);
        lock (_mutex)
        {
            for (var i = _cache.Count - 1; i >= 0; --i)
            {
                if (!_cache[i].Model.TryGetTarget(out var cachedModel) || ReferenceEquals(cachedModel, model))
                    _cache.RemoveAt(i);
            }
        }
    }

    /// <summary>Prune cache of dead entries.</summary>
    public void Prune()
    {
        lock (_mutex)
        {
            for (var i = _cache.Count - 1; i >= 0; --i)
            {
                var entry = _cache[i];
                if (!entry.Model.TryGetTarget(out _) || entry.Lods.All(
                        weakGeometry => weakGeometry?.TryGetTarget(out _) != true))
                    _cache.RemoveAt(i);
            }
        }
    }

    private static bool HasDynamicBuffers(Model model, uint lodLevel)
    {
        for (var i = 0u; i < model.GetNumGeometries(); ++i)
        {
            var geometry = model.GetGeometry(i, lodLevel);
            if (geometry is null)
                continue;
            if (geometry.GetVertexBuffers().Any(buffer => buffer?.IsDynamic == true))
                return true;
            if (geometry.GetIndexBuffer()?.IsDynamic() == true)
                return true;
        }
        return false;
    }

    private CacheEntry? FindEntry(Model model)
    {
        for (var i = _cache.Count - 1; i >= 0; --i)
        {
            if (!_cache[i].Model.TryGetTarget(out var cachedModel))
            {
                _cache.RemoveAt(i);
                continue;
            }
            if (ReferenceEquals(cachedModel, model))
                return _cache[i];
        }
        return null;
    }

    private void StoreCachedGeometry(Model model, uint lodLevel, CollisionGeometryData geometry)
    {
        lock (_mutex)
        {
            var entry = FindEntry(model);
            if (entry is null)
            {
                entry = new CacheEntry(model);
                _cache.Add(entry);
            }
            while (entry.Lods.Count <= lodLevel)
                entry.Lods.Add(null);
            entry.Lods[(int)lodLevel] = new WeakReference<CollisionGeometryData>(geometry);
        }
    }

    private void SubscribeToReload(Model model)
    {
        SubscribeToEvent<ResourceEvents.ReloadFinished>(model, HandleReloadFinished);
    }

    private void HandleReloadFinished(ref ResourceEvents.ReloadFinished eventData)
    {
        if (GetEventSender() is Model model)
            ReleaseCachedGeometry(model);
    }

    private static CollisionGeometryData CreateCollisionGeometryData(
        ShapeType shapeType, Model model, uint lodLevel)
    {
        return shapeType == ShapeType.ConvexHull
            ? CreateConvexGeometry(model, lodLevel)
            : CreateTriangleGeometry(model, lodLevel);
    }

    internal static CollisionGeometryData CreateCollisionGeometryData(ShapeType shapeType, CustomGeometry custom)
    {
        ArgumentNullException.ThrowIfNull(custom);
        var vertices = custom.GetVertices().SelectMany(group => group).Select(vertex => vertex.Position).ToArray();
        var indices = shapeType == ShapeType.ConvexHull
            ? []
            : Enumerable.Range(0, vertices.Length).Select(index => checked((uint)index)).ToArray();
        return new CollisionGeometryData(vertices, indices);
    }

    private static CollisionGeometryData CreateTriangleGeometry(Model model, uint lodLevel)
    {
        var vertices = new List<Vector3f>();
        var indices = new List<uint>();
        for (var i = 0u; i < model.GetNumGeometries(); ++i)
        {
            var geometry = model.GetGeometry(i, lodLevel);
            if (geometry is null)
            {
                Log.GetLogger().Warning("Skipping null geometry for triangle mesh collision");
                continue;
            }

            geometry.GetRawDataShared(out var vertexData, out var vertexSize, out var indexData, out var indexSize,
                out var elements);
            var positionElement = elements is null ? null : VertexBuffer.GetElement(
                elements, VertexElementType.Vector3, VertexElementSemantic.Position);
            if (vertexData is null || indexData is null || positionElement?.Offset != 0 ||
                vertexSize < Vector3Size || indexSize is not (2 or 4))
            {
                Log.GetLogger().Warning(
                    "Skipping geometry with no or unsuitable CPU-side geometry data for triangle mesh collision");
                continue;
            }

            var indexStart = geometry.GetIndexStart();
            var indexCount = geometry.GetIndexCount();
            for (var j = 0u; j < indexCount; ++j)
            {
                var sourceIndexOffset = checked((int)((indexStart + j) * indexSize));
                if (sourceIndexOffset < 0 || sourceIndexOffset + indexSize > indexData.Length)
                    break;
                var sourceIndex = indexSize == 2
                    ? BinaryPrimitives.ReadUInt16LittleEndian(indexData.AsSpan(sourceIndexOffset, 2))
                    : BinaryPrimitives.ReadUInt32LittleEndian(indexData.AsSpan(sourceIndexOffset, 4));
                var vertexOffset = checked((int)(sourceIndex * vertexSize));
                if (vertexOffset < 0 || vertexOffset + Vector3Size > vertexData.Length)
                    continue;
                vertices.Add(MemoryMarshal.Read<Vector3f>(vertexData.AsSpan(vertexOffset)));
                indices.Add(checked((uint)indices.Count));
            }
        }
        return new CollisionGeometryData(vertices.ToArray(), indices.ToArray());
    }

    private static CollisionGeometryData CreateConvexGeometry(Model model, uint lodLevel)
    {
        var vertices = new List<Vector3f>();
        for (var i = 0u; i < model.GetNumGeometries(); ++i)
        {
            var geometry = model.GetGeometry(i, lodLevel);
            if (geometry is null)
            {
                Log.GetLogger().Warning("Skipping null geometry for convex hull collision");
                continue;
            }

            geometry.GetRawDataShared(out var vertexData, out var vertexSize, out _, out _, out var elements);
            var positionElement = elements is null ? null : VertexBuffer.GetElement(
                elements, VertexElementType.Vector3, VertexElementSemantic.Position);
            if (vertexData is null || positionElement?.Offset != 0 || vertexSize < Vector3Size)
            {
                Log.GetLogger().Warning(
                    "Skipping geometry with no or unsuitable CPU-side geometry data for convex hull collision");
                continue;
            }

            var vertexStart = geometry.GetVertexStart();
            var vertexCount = geometry.GetVertexCount();
            for (var j = 0u; j < vertexCount; ++j)
            {
                var vertexOffset = checked((int)((vertexStart + j) * vertexSize));
                if (vertexOffset < 0 || vertexOffset + Vector3Size > vertexData.Length)
                    break;
                vertices.Add(MemoryMarshal.Read<Vector3f>(vertexData.AsSpan(vertexOffset)));
            }
        }
        return new CollisionGeometryData(vertices.ToArray(), []);
    }
}
