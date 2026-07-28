//
// Copyright (c) 2008-2022 the Urho3D project.
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

using System.IO;
using Math = System.Math;

using Microsoft.Extensions.Logging;

using DotRecast.Core;
using DotRecast.Detour;
using DotRecast.Detour.TileCache;
using DotRecast.Detour.TileCache.Io;
using DotRecast.Detour.TileCache.Io.Compress;
using DotRecast.Recast;

using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.IO;
using RedBolt.Logging;
using RedBolt.Math;
using RedBolt.Scene;
using SceneType = RedBolt.Scene.Scene;

namespace RedBolt.Navigation;

[Reflected(ObjectCategory.Category_Navigation)]
public partial class DynamicNavigationMesh : NavigationMesh
{
    /// <summary>Version of compiled navigation data. Navigation data should be discarded and rebuilt on mismatch.</summary>
    public new const int NavigationDataVersion = 1;

    private const int DefaultMaxObstacles = 1024;
    private const int DefaultMaxLayers = 16;

    /// <summary>Detour tile cache instance that works with the nav mesh.</summary>
    private DtTileCache? _tileCache;
    /// <summary>Used by dtTileCache to allocate blocks of memory.</summary>
    private readonly DtTileCacheAlloc _allocator = new();
    /// <summary>Used by dtTileCache to compress the original tiles to use when reconstructing for changes.</summary>
    private readonly IRcCompressor _compressor = DtTileCacheFastLzCompressor.Shared;
    /// <summary>Mesh processor used by Detour, in this case a 'pass-through' processor.</summary>
    private readonly MeshProcess _meshProcessor;
    /// <summary>Maximum number of obstacle objects allowed.</summary>
    private int _maxObstacles = DefaultMaxObstacles;
    /// <summary>Maximum number of layers that are allowed to be constructed.</summary>
    private int _maxLayers = DefaultMaxLayers;
    /// <summary>Debug draw Obstacles.</summary>
    private bool _drawObstacles;
    /// <summary>Queue of tiles to be built.</summary>
    private readonly List<IntVector2> _tileQueue = [];
    /// <summary>Queue of obstacle updates.</summary>
    private readonly List<ObstacleUpdate> _obstacleQueue = [];

    /// <summary>Constructor.</summary>
    public DynamicNavigationMesh()
    {
        _partitionType = NavmeshPartitionType.Monotone;
        _meshProcessor = new MeshProcess(this);
    }

    /// <summary>Destructor.</summary>
    public override void Dispose()
    {
        ReleaseNavigationMesh();
        base.Dispose();
    }

    /// <summary>Offset internal tile cache data by tile offset and Y.</summary>
    public void OffsetCacheTile(Span<byte> tileData, IntVector2 tileOffset, int offsetY)
    {
        DtTileCacheLayerHeader layerHeader;
        try
        {
            layerHeader = DtTileCacheLayerHeaderReader.Read(new RcByteBuffer(tileData.ToArray()), true);
        }
        catch (Exception exception) when (exception is IOException or IndexOutOfRangeException)
        {
            Log.GetLogger().Error("Cannot offset dtCompressedTile: data is corrupted");
            return;
        }

        var offset = tileOffset.ToVector2() * (_tileSize * _cellSize);
        layerHeader.tx += tileOffset.X;
        layerHeader.ty += tileOffset.Y;
        layerHeader.bmin.X += offset.X;
        layerHeader.bmin.Y += offsetY;
        layerHeader.bmin.Z += offset.Y;
        layerHeader.bmax.X += offset.X;
        layerHeader.bmax.Y += offsetY;
        layerHeader.bmax.Z += offset.Y;

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        new DtTileCacheLayerHeaderWriter().Write(writer, layerHeader, RcByteOrder.LITTLE_ENDIAN, true);
        writer.Flush();
        stream.GetBuffer().AsSpan(0, checked((int)stream.Length)).CopyTo(tileData);
    }

    /// <summary>Offset serialized tile data.</summary>
    public override void OffsetTileData(Span<byte> tileData, IntVector3 delta)
    {
        var tileOffset = NavigationUtils.CalculateTileOffset(delta, _tileSize, _cellSize);
        if (tileOffset is null)
            return;

        using var buffer = new MemoryBuffer(tileData.ToArray());
        while (!buffer.IsEof())
        {
            var readPosition = buffer.Position;
            var tileCacheLayer = NavigationUtils.ReadDetourBuffer(buffer);
            if (!tileCacheLayer.IsValid || tileCacheLayer.Data is null)
                break;

            OffsetCacheTile(tileCacheLayer.Data, tileOffset.Value.TileOffset, tileOffset.Value.OffsetY);
            buffer.Seek(readPosition);
            NavigationUtils.WriteDetourBuffer(buffer, tileCacheLayer);
        }
    }

    /// <summary>Return tile data.</summary>
    public override byte[] GetTileData(IntVector2 tileIndex)
    {
        using var result = new VectorBuffer();
        if (_tileCache is null)
            return [];

        var tiles = _tileCache.GetTilesAt(tileIndex.X, tileIndex.Y);
        for (var i = 0; i < tiles.Count; ++i)
        {
            var tile = _tileCache.GetTileByRef(tiles[i]);
            if (tile?.header is not null)
                WriteTile(result, tileIndex.X, tileIndex.Y, tile.header.tlayer);
        }
        return result.Buffer.ToArray();
    }

    /// <summary>Return whether the Obstacle is touching the given tile.</summary>
    public bool IsObstacleInTile(Obstacle obstacle, IntVector2 tileIndex)
    {
        var tileBoundingBox = GetTileBoundingBoxColumn(tileIndex);
        var obstaclePosition = obstacle.Node.GetWorldPosition();
        return tileBoundingBox.DistanceToPoint(obstaclePosition) < obstacle.Radius;
    }

    /// <summary>Add tile to navigation mesh.</summary>
    public override bool AddTile(byte[] tileData)
    {
        using var buffer = new MemoryBuffer(tileData);
        return ReadTiles(buffer, false);
    }

    /// <summary>Remove tile from navigation mesh.</summary>
    public override void RemoveTile(IntVector2 tileIndex)
    {
        if (_navMesh is null)
            return;

        var existing = _tileCache!.GetTilesAt(tileIndex.X, tileIndex.Y);
        for (var i = 0; i < existing.Count; ++i)
            _tileCache.RemoveTile(existing[i]);

        base.RemoveTile(tileIndex);
    }

    /// <summary>Remove all tiles from navigation mesh.</summary>
    public override void RemoveAllTiles()
    {
        var numTiles = _tileCache!.GetTileCount();
        for (var i = 0; i < numTiles; ++i)
        {
            var tile = _tileCache.GetTile(i);
            if (tile.header is not null)
                _tileCache.RemoveTile(_tileCache.GetTileRef(tile));
        }

        base.RemoveAllTiles();
    }

    /// <summary>Visualize the component as debug geometry.</summary>
    public override void DrawDebugGeometry(DebugRenderer? debug, bool depthTest)
    {
        if (debug is null || _navMesh is null || _node is null)
            return;

        for (var i = 0; i < _navMesh.GetMaxTiles(); ++i)
            DrawDebugTileGeometry(debug, depthTest, i);

        var scene = Scene;
        if (scene is not null)
        {
            // Draw Obstacle components
            if (_drawObstacles)
            {
                var obstacles = scene.GetChildrenWithComponent<Obstacle>(true);
                for (var i = 0; i < obstacles.Count; ++i)
                {
                    var obstacle = obstacles[i].GetComponent<Obstacle>();
                    if (obstacle is not null && obstacle.IsEnabledEffective)
                        obstacle.DrawDebugGeometry(debug, depthTest);
                }
            }

            // Draw OffMeshConnection components
            if (_drawOffMeshConnections)
            {
                var connections = scene.GetChildrenWithComponent<OffMeshConnection>(true);
                for (var i = 0; i < connections.Count; ++i)
                {
                    var connection = connections[i].GetComponent<OffMeshConnection>();
                    if (connection is not null && connection.IsEnabledEffective)
                        connection.DrawDebugGeometry(debug, depthTest);
                }
            }

            // Draw NavArea components
            if (_drawNavAreas)
            {
                var areas = scene.GetChildrenWithComponent<NavArea>(true);
                for (var i = 0; i < areas.Count; ++i)
                {
                    var area = areas[i].GetComponent<NavArea>();
                    if (area is not null && area.IsEnabledEffective)
                        area.DrawDebugGeometry(debug, depthTest);
                }
            }
        }
    }

    /// <summary>Add debug geometry to the debug renderer.</summary>
    public new void DrawDebugGeometry(bool depthTest)
    {
        var scene = Scene;
        if (scene is not null)
        {
            var debug = scene.GetComponent<DebugRenderer>();
            if (debug is not null)
                DrawDebugGeometry(debug, depthTest);
        }
    }

    /// <summary>Set navigation data attribute.</summary>
    public override void SetNavigationDataAttr(byte[] value)
    {
        ReleaseNavigationMesh();
        if (value.Length == 0)
            return;

        using var buffer = new MemoryBuffer(value);

        // Keep the header the same as the old data format to check for validity.
        buffer.ReadBoundingBox();
        var unused0 = buffer.ReadInt();
        var unused1 = buffer.ReadInt();
        var version = buffer.ReadInt();
        if (unused0 != 0 || unused1 != 0 || version != NavigationDataVersion)
        {
            Log.GetLogger().LogWarning("Incompatible navigation data format, please rebuild navigation data");
            return;
        }

        var navMeshParameters = ReadNavMeshParameters(buffer);
        _navMesh = new DtNavMesh();
        if (_navMesh.Init(navMeshParameters, DtDetour.DT_VERTS_PER_POLYGON).Failed())
        {
            Log.GetLogger().Error("Could not initialize navigation mesh");
            ReleaseNavigationMesh();
            return;
        }

        var tileCacheParameters = ReadTileCacheParameters(buffer);
        _tileCache = new DtTileCache(tileCacheParameters,
            new DtTileCacheStorageParams(RcByteOrder.LITTLE_ENDIAN, true),
            _navMesh, _compressor, _meshProcessor);

        ReadTiles(buffer, true);
        // \todo Shall we send E_NAVIGATION_MESH_REBUILT here?
    }

    /// <summary>Return navigation data attribute.</summary>
    public override byte[] GetNavigationDataAttr()
    {
        using var result = new VectorBuffer();
        if (_navMesh is not null && _tileCache is not null)
        {
            result.WriteBoundingBox(new BoundingBox());
            result.WriteInt(0);
            result.WriteInt(0);
            result.WriteInt(NavigationDataVersion);

            WriteNavMeshParameters(result, _navMesh.GetParams());
            WriteTileCacheParameters(result, _tileCache.GetParams());

            for (var i = 0; i < _navMesh.GetMaxTiles(); ++i)
            {
                var tile = _navMesh.GetTile(i);
                if (tile.data?.header is null)
                    continue;
                WriteTile(result, tile.data.header.x, tile.data.header.y, tile.data.header.layer);
            }
        }
        return result.Buffer.ToArray();
    }

    /// <summary>Return or set the maximum number of obstacles allowed.</summary>
    [Serialized("Max Obstacles", DefaultSource = nameof(GetDefaultMaxObstacles))]
    public int MaxObstacles
    {
        get => _maxObstacles;
        set => _maxObstacles = value;
    }

    /// <summary>Return or set the maximum number of layers permitted to build.</summary>
    [Serialized("Max Layers", DefaultSource = nameof(GetDefaultMaxLayers))]
    public int MaximumLayers
    {
        get => _maxLayers;
        set
        {
            // Set 3 as a minimum due to the tendency of layers to be constructed inside the hollow space of stacked objects
            // That behavior is unlikely to be expected by the end user
            _maxLayers = global::System.Math.Max(3, global::System.Math.Min(value, MaxLayers));
        }
    }

    /// <summary>Draw debug geometry for Obstacles.</summary>
    /// <summary>Return whether to draw Obstacles.</summary>
    [Serialized("Draw Obstacles", DefaultSource = nameof(GetDefaultDrawObstacles))]
    public bool DrawObstacles
    {
        get => _drawObstacles;
        set => _drawObstacles = value;
    }

    /// <summary>Override NavigationMesh.</summary>
    protected override bool AllocateMesh(int maxTiles)
    {
        if (!base.AllocateMesh(maxTiles * _maxLayers))
            return false;

        var tileCacheParameters = new DtTileCacheParams
        {
            ch = _cellHeight,
            cs = _cellSize,
            width = _tileSize,
            height = _tileSize,
            maxSimplificationError = _edgeMaxError,
            maxTiles = maxTiles * _maxLayers,
            maxObstacles = _maxObstacles,
            walkableClimb = _agentMaxClimb,
            walkableHeight = _agentHeight,
            walkableRadius = _agentRadius
        };

        _tileCache = new DtTileCache(tileCacheParameters,
            new DtTileCacheStorageParams(RcByteOrder.LITTLE_ENDIAN, true),
            _navMesh!, _compressor, _meshProcessor);

        // No need to scan for obstacles here because there are no tiles yet
        return true;
    }

    protected override bool RebuildMesh()
    {
        if (!base.RebuildMesh())
            return false;

        // Scan for obstacles to insert into us
        var obstacles = Scene!.GetChildrenWithComponent<Obstacle>(true);
        for (var i = 0; i < obstacles.Count; ++i)
        {
            var obstacle = obstacles[i].GetComponent<Obstacle>();
            if (obstacle is not null && obstacle.IsEnabledEffective)
                AddObstacle(obstacle);
        }
        return true;
    }

    protected override Func<NavBuildData, bool> GetTileBuilder()
    {
        return build =>
        {
            if (!BuildCompactHeightField(build))
                return false;
            if (!BuildDynamicTileData((DynamicNavBuildData)build))
                return false;
            return true;
        };
    }

    protected override NavBuildData CreateTileBuildData(
        IReadOnlyList<NavigationGeometryInfo> geometryList, IntVector2 tileIndex)
    {
        var build = new DynamicNavBuildData();
        InitializeBuildData(build, tileIndex, geometryList);
        return build;
    }

    protected override bool ReplaceTileData(NavBuildData build)
    {
        var tileIndex = build.TileIndex + build.PendingTileOffset;
        var tileData = ((DynamicNavBuildData)build).TileData;

        for (var i = 0; i < tileData.Count; ++i)
            OffsetCacheTile(tileData[i], build.PendingTileOffset, build.PendingOffsetY);

        // Remove existing tiles from tile cache and navigation mesh
        var tilesToRemove = _tileCache!.GetTilesAt(tileIndex.X, tileIndex.Y);
        for (var i = 0; i < tilesToRemove.Count; ++i)
        {
            _tileCache.RemoveTile(tilesToRemove[i]);
            _navMesh!.RemoveTile(_navMesh.GetTileRefAt(tileIndex.X, tileIndex.Y, i));
        }

        // Add new tiles into tile cache and rebuild navigation mesh
        var result = false;
        for (var i = 0; i < tileData.Count; ++i)
        {
            var tileRef = _tileCache.AddTile(tileData[i], DtCompressedTileFlags.DT_COMPRESSEDTILE_FREE_DATA);
            if (tileRef == 0)
            {
                Log.GetLogger().Error("Failed to add tile into tile cache");
                continue;
            }

            _tileCache.BuildNavMeshTile(tileRef);
            result = true;
        }
        return result;
    }

    protected override void OffsetTilesGeometry(IntVector2 tileOffset, int offsetY)
    {
        base.OffsetTilesGeometry(tileOffset, offsetY);

        var tiles = new List<byte[]>();
        for (var i = 0; i < _tileCache!.GetTileCount(); ++i)
        {
            var meshTile = _tileCache.GetTile(i);
            if (meshTile.data is not null)
            {
                tiles.Add(meshTile.data);
                _tileCache.RemoveTile(_tileCache.GetTileRef(meshTile));
            }
        }

        for (var i = 0; i < tiles.Count; ++i)
        {
            OffsetCacheTile(tiles[i], -tileOffset, -offsetY);
            _tileCache.AddTile(tiles[i], DtCompressedTileFlags.DT_COMPRESSEDTILE_FREE_DATA);
        }
    }

    protected override void OnSceneSet(SceneType? previousScene, SceneType? scene)
    {
        base.OnSceneSet(previousScene, scene);

        // Subscribe to the scene subsystem update, which will trigger the tile cache to update the nav mesh
        if (scene is not null)
            SubscribeToEvent<SceneEvents.SceneSubsystemUpdateEvent>(scene, HandleSceneSubsystemUpdate);
        else
            UnsubscribeFromEvent<SceneEvents.SceneSubsystemUpdateEvent>();
    }

    /// <summary>Return queued update related to the Obstacle (existing or new).</summary>
    protected ObstacleUpdate GetObstacleUpdate(Obstacle obstacle)
    {
        for (var i = 0; i < _obstacleQueue.Count; ++i)
        {
            if (_obstacleQueue[i].Obstacle.TryGetTarget(out var queuedObstacle) && ReferenceEquals(queuedObstacle, obstacle))
                return _obstacleQueue[i];
        }

        var obstacleParameters = _tileCache!.GetObstacleByRef(obstacle.ObstacleID);
        var update = new ObstacleUpdate(obstacle)
        {
            OldObstacleId = obstacleParameters is not null && obstacleParameters.state != DtObstacleState.DT_OBSTACLE_EMPTY
                ? obstacle.ObstacleID
                : 0
        };
        _obstacleQueue.Add(update);
        return update;
    }

    /// <summary>Used by Obstacle class to add itself to the tile cache.</summary>
    protected internal void AddObstacle(Obstacle obstacle)
    {
        if (_tileCache is not null)
        {
            var update = GetObstacleUpdate(obstacle);
            update.Removed = false;
            // "Added" event is postponed until update is processed.
            update.SendEvents = true;
        }
    }

    /// <summary>Used by Obstacle class to update itself.</summary>
    protected internal void ObstacleChanged(Obstacle obstacle)
    {
        if (_tileCache is not null)
            GetObstacleUpdate(obstacle);
    }

    /// <summary>Used by Obstacle class to remove itself from the tile cache.</summary>
    protected internal void RemoveObstacle(Obstacle obstacle)
    {
        if (_tileCache is not null)
        {
            var update = GetObstacleUpdate(obstacle);
            update.Removed = true;

            // "Removed" event is sent immediately.
            if (obstacle.ObstacleID != 0)
            {
                obstacle.ObstacleID = 0;
                var eventData = new NavigationEvents.NavigationObstacleRemovedEvent
                {
                    Node = obstacle.Node,
                    Obstacle = obstacle,
                    Position = obstacle.Node.GetWorldPosition(),
                    Radius = obstacle.Radius,
                    Height = obstacle.Height
                };
                SendEvent(ref eventData);
            }
        }
    }

    /// <summary>Process Obstacle update. Tile cache should have sufficient space.</summary>
    protected void ProcessObstacleUpdate(ObstacleUpdate update)
    {
        if (update.OldObstacleId != 0)
            _tileCache!.RemoveObstacle(update.OldObstacleId);

        if (!update.Removed && update.Obstacle.TryGetTarget(out var obstacle))
        {
            var position = obstacle.Node.GetWorldPosition();
            var obstacleRef = _tileCache!.AddObstacle(
                new DotRecast.Core.Numerics.RcVec3f(position.X, position.Y, position.Z),
                obstacle.Radius, obstacle.Height);
            if (obstacleRef == 0)
            {
                Log.GetLogger().Error("Failed to add obstacle");
                return;
            }

            obstacle.ObstacleID = obstacleRef;
            if (update.SendEvents)
            {
                var eventData = new NavigationEvents.NavigationObstacleAddedEvent
                {
                    Node = obstacle.Node,
                    Obstacle = obstacle,
                    Position = obstacle.Node.GetWorldPosition(),
                    Radius = obstacle.Radius,
                    Height = obstacle.Height
                };
                SendEvent(ref eventData);
            }
        }
    }

    /// <summary>Off-mesh connections to be rebuilt in the mesh processor.</summary>
    protected List<OffMeshConnection> CollectOffMeshConnections(BoundingBox bounds)
    {
        var connections = Node.FindComponents<OffMeshConnection>();
        var result = new List<OffMeshConnection>();
        for (var i = 0; i < connections.Count; ++i)
        {
            var connection = connections[i];
            if (connection.IsEnabledEffective && connection.EndPoint is not null)
                result.Add(connection);
        }
        return result;
    }

    /// <summary>Release the navigation mesh, query, and tile cache.</summary>
    protected override void ReleaseNavigationMesh()
    {
        base.ReleaseNavigationMesh();
        ReleaseTileCache();
    }

    /// <summary>Build dynamic navigation mesh tile.</summary>
    protected static bool BuildDynamicTileData(DynamicNavBuildData build)
    {
        var config = build.RecastConfig!;
        RcLayers.BuildHeightfieldLayers(build.Context, build.CompactHeightField!, config.BorderSize,
            config.WalkableHeight, out var heightFieldLayers);
        build.HeightFieldLayers = heightFieldLayers;

        for (var i = 0; i < heightFieldLayers.layers.Length; ++i)
        {
            var layer = heightFieldLayers.layers[i];
            var header = new DtTileCacheLayerHeader
            {
                magic = DtTileCacheLayerHeader.DT_TILECACHE_MAGIC,
                version = DtTileCacheLayerHeader.DT_TILECACHE_VERSION,
                tx = build.TileIndex.X,
                ty = build.TileIndex.Y,
                tlayer = i,
                bmin = layer.bmin,
                bmax = layer.bmax,
                width = layer.width,
                height = layer.height,
                minx = layer.minx,
                maxx = layer.maxx,
                miny = layer.miny,
                maxy = layer.maxy,
                hmin = layer.hmin,
                hmax = layer.hmax
            };

            var data = DtTileCacheBuilder.CompressTileCacheLayer(header, layer.heights, layer.areas, layer.cons,
                RcByteOrder.LITTLE_ENDIAN, true, DtTileCacheFastLzCompressor.Shared);
            if (data is null)
            {
                Log.GetLogger().Error("Failed to build tile cache layers");
                build.TileData.Clear();
                return false;
            }
            build.TileData.Add(data);
        }
        return true;
    }

    private void HandleSceneSubsystemUpdate(ref SceneEvents.SceneSubsystemUpdateEvent eventData)
    {
        if (_tileCache is not null && _navMesh is not null && IsEnabledEffective)
            UpdateTileCache();
    }

    /// <summary>Write tiles data.</summary>
    private void WriteTile(Serializer destination, int x, int z, int layer)
    {
        var tiles = _tileCache!.GetTilesAt(x, z);
        for (var i = 0; i < tiles.Count; ++i)
        {
            var tile = _tileCache.GetTileByRef(tiles[i]);
            if (tile?.header is not null && tile.header.tlayer == layer && tile.data.Length != 0)
            {
                NavigationUtils.WriteDetourBuffer(destination, tile.data);
                return;
            }
        }
    }

    /// <summary>Read tiles data to the navigation mesh.</summary>
    private bool ReadTiles(Deserializer source, bool silent)
    {
        _tileQueue.Clear();
        while (!source.IsEof())
        {
            var buffer = NavigationUtils.ReadDetourBuffer(source);
            if (!buffer.IsValid || buffer.Data is null)
            {
                Log.GetLogger().Error("Could not allocate data for navigation mesh tile");
                return false;
            }

            var tileRef = _tileCache!.AddTile(buffer.Data, DtCompressedTileFlags.DT_COMPRESSEDTILE_FREE_DATA);
            if (tileRef == 0)
            {
                Log.GetLogger().Error("Failed to add tile");
                return false;
            }

            var header = _tileCache.GetTileByRef(tileRef).header;
            var tileIndex = new IntVector2(header.tx, header.ty);
            if (_tileQueue.Count == 0 || _tileQueue[^1] != tileIndex)
                _tileQueue.Add(tileIndex);
        }

        for (var i = 0; i < _tileQueue.Count; ++i)
        {
            var tiles = _tileCache!.GetTilesAt(_tileQueue[i].X, _tileQueue[i].Y);
            for (var j = 0; j < tiles.Count; ++j)
                _tileCache.BuildNavMeshTile(tiles[j]);
            CancelTileBuild(_tileQueue[i]);
        }

        // Send event
        if (!silent)
        {
            for (var i = 0; i < _tileQueue.Count; ++i)
                SendTileAddedEvent(_tileQueue[i]);
        }
        return true;
    }

    /// <summary>Free the tile cache.</summary>
    private void ReleaseTileCache()
    {
        _tileCache = null;
        _obstacleQueue.Clear();
    }

    /// <summary>Ensure the tile cache is fully updated.</summary>
    /// <summary>It is suboptimal when multiple obstacles added at once, but it is the most stable solution.</summary>
    private void UpdateTileCache()
    {
        while (_obstacleQueue.Count != 0)
        {
            const int maxTileUpdates = 64;
            const int maxTouchedTiles = 8;
            var maxObstacleUpdates = global::System.Math.Max(1, maxTileUpdates / maxTouchedTiles / 2);
            for (var i = 0; i < maxObstacleUpdates && _obstacleQueue.Count != 0; ++i)
            {
                var lastIndex = _obstacleQueue.Count - 1;
                ProcessObstacleUpdate(_obstacleQueue[lastIndex]);
                _obstacleQueue.RemoveAt(lastIndex);
            }

            while (!_tileCache!.Update())
            {
            }
        }
    }

    private static void WriteNavMeshParameters(Serializer destination, in DtNavMeshParams parameters)
    {
        destination.WriteVector3(new Vector3f(parameters.orig.X, parameters.orig.Y, parameters.orig.Z));
        destination.WriteFloat(parameters.tileWidth);
        destination.WriteFloat(parameters.tileHeight);
        destination.WriteInt(parameters.maxTiles);
        destination.WriteInt(parameters.maxPolys);
    }

    private static DtNavMeshParams ReadNavMeshParameters(Deserializer source)
    {
        var origin = source.ReadVector3();
        return new DtNavMeshParams
        {
            orig = new DotRecast.Core.Numerics.RcVec3f(origin.X, origin.Y, origin.Z),
            tileWidth = source.ReadFloat(),
            tileHeight = source.ReadFloat(),
            maxTiles = source.ReadInt(),
            maxPolys = source.ReadInt()
        };
    }

    private static void WriteTileCacheParameters(Serializer destination, in DtTileCacheParams parameters)
    {
        destination.WriteVector3(new Vector3f(parameters.orig.X, parameters.orig.Y, parameters.orig.Z));
        destination.WriteFloat(parameters.cs);
        destination.WriteFloat(parameters.ch);
        destination.WriteInt(parameters.width);
        destination.WriteInt(parameters.height);
        destination.WriteFloat(parameters.walkableHeight);
        destination.WriteFloat(parameters.walkableRadius);
        destination.WriteFloat(parameters.walkableClimb);
        destination.WriteFloat(parameters.maxSimplificationError);
        destination.WriteInt(parameters.maxTiles);
        destination.WriteInt(parameters.maxObstacles);
    }

    private static DtTileCacheParams ReadTileCacheParameters(Deserializer source)
    {
        var origin = source.ReadVector3();
        return new DtTileCacheParams
        {
            orig = new DotRecast.Core.Numerics.RcVec3f(origin.X, origin.Y, origin.Z),
            cs = source.ReadFloat(),
            ch = source.ReadFloat(),
            width = source.ReadInt(),
            height = source.ReadInt(),
            walkableHeight = source.ReadFloat(),
            walkableRadius = source.ReadFloat(),
            walkableClimb = source.ReadFloat(),
            maxSimplificationError = source.ReadFloat(),
            maxTiles = source.ReadInt(),
            maxObstacles = source.ReadInt()
        };
    }

    private static int GetDefaultMaxObstacles() => DefaultMaxObstacles;
    private static int GetDefaultMaxLayers() => DefaultMaxLayers;
    private static bool GetDefaultDrawObstacles() => false;

    protected sealed class ObstacleUpdate
    {
        public ObstacleUpdate(Obstacle obstacle)
        {
            Obstacle = new WeakReference<Obstacle>(obstacle);
        }

        public WeakReference<Obstacle> Obstacle;
        public long OldObstacleId;
        public bool Removed;
        public bool SendEvents;
    }

    private sealed class MeshProcess : IDtTileCacheMeshProcess
    {
        private readonly DynamicNavigationMesh _owner;

        public MeshProcess(DynamicNavigationMesh owner)
        {
            _owner = owner;
        }

        public void Process(DtNavMeshCreateParams parameters)
        {
            // Update poly flags from areas.
            // \todo Assignment of flags from areas?
            for (var i = 0; i < parameters.polyCount; ++i)
            {
                if (parameters.polyAreas[i] != RcRecast.RC_NULL_AREA)
                    parameters.polyFlags[i] = RcRecast.RC_WALKABLE_AREA;
            }

            var bounds = new BoundingBox(
                new Vector3f(parameters.bmin.X, parameters.bmin.Y, parameters.bmin.Z),
                new Vector3f(parameters.bmin.X, parameters.bmin.Y, parameters.bmin.Z));

            // collect off-mesh connections
            var offMeshConnections = _owner.CollectOffMeshConnections(bounds);
            if (offMeshConnections.Count != 0)
            {
                var inverse = _owner.Node.GetWorldTransform().Inverse;
                parameters.offMeshConCount = offMeshConnections.Count;
                parameters.offMeshConVerts = new float[offMeshConnections.Count * 6];
                parameters.offMeshConRad = new float[offMeshConnections.Count];
                parameters.offMeshConFlags = new int[offMeshConnections.Count];
                parameters.offMeshConAreas = new int[offMeshConnections.Count];
                parameters.offMeshConDir = new int[offMeshConnections.Count];
                for (var i = 0; i < offMeshConnections.Count; ++i)
                {
                    var connection = offMeshConnections[i];
                    var start = inverse * connection.Node.GetWorldPosition();
                    var end = inverse * connection.EndPoint!.GetWorldPosition();
                    parameters.offMeshConVerts[i * 6] = start.X;
                    parameters.offMeshConVerts[i * 6 + 1] = start.Y;
                    parameters.offMeshConVerts[i * 6 + 2] = start.Z;
                    parameters.offMeshConVerts[i * 6 + 3] = end.X;
                    parameters.offMeshConVerts[i * 6 + 4] = end.Y;
                    parameters.offMeshConVerts[i * 6 + 5] = end.Z;
                    parameters.offMeshConRad[i] = connection.Radius;
                    parameters.offMeshConFlags[i] = unchecked((ushort)connection.Mask);
                    parameters.offMeshConAreas[i] = unchecked((byte)connection.AreaID);
                    parameters.offMeshConDir[i] = connection.Bidirectional ? DtDetour.DT_OFFMESH_CON_BIDIR : 0;
                }
            }
        }
    }
}
