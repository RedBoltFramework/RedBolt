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

using System.Buffers.Binary;
using System.IO;
using Math = System.Math;

using Microsoft.Extensions.Logging;

using DotRecast.Core;
using DotRecast.Core.Numerics;
using DotRecast.Detour;
using DotRecast.Detour.Io;
using DotRecast.Recast;

using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.IO;
using RedBolt.Logging;
using RedBolt.Math;
using RedBolt.RenderAPI;
using RedBolt.Scene;
using SceneType = RedBolt.Scene.Scene;

namespace RedBolt.Navigation;

/// <summary>A flag representing the type of path point- none, the start of a path segment, the end of one, or an off-mesh connection.</summary>
[Flags]
public enum NavigationPathPointFlag
{
    None = 0,
    Start = 0x01,
    End = 0x02,
    OffMesh = 0x04
}

public struct NavigationPathPoint
{
    /// <summary>World-space position of the path point.</summary>
    public Vector3f Position;
    /// <summary>Detour flag.</summary>
    public NavigationPathPointFlag Flag;
    /// <summary>Detour area ID.</summary>
    public byte AreaID;
}

/// <summary>Navigation mesh component. Collects the navigation geometry from child nodes with the Navigable component and responds to path queries.</summary>
[Reflected(ObjectCategory.Category_Navigation, InheritAttributes = false)]
public partial class NavigationMesh : Component
{
    /// <summary>Version of compiled navigation data. Navigation data should be discarded and rebuilt on mismatch.</summary>
    public const int NavigationDataVersion = 2;
    /// <summary>Default maximum number of tiles.</summary>
    public const int DefaultMaxTiles = 256;
    /// <summary>Maximum number of layers in the single tile.</summary>
    public const int MaxLayers = 255;

    private const int DefaultTileSize = 120;
    private const float DefaultCellSize = 0.3f;
    private const float DefaultCellHeight = 0.2f;
    private const float DefaultAgentHeight = 2.0f;
    private const float DefaultAgentRadius = 0.6f;
    private const float DefaultAgentMaxClimb = 0.9f;
    private const float DefaultAgentMaxSlope = 45.0f;
    private const float DefaultRegionMinSize = 8.0f;
    private const float DefaultRegionMergeSize = 20.0f;
    private const float DefaultEdgeMaxLength = 12.0f;
    private const float DefaultEdgeMaxError = 1.3f;
    private const float DefaultDetailSampleDistance = 6.0f;
    private const float DefaultDetailSampleMaxError = 1.0f;
    private const int DefaultMaxPortalLinks = 2;
    private const int MaxPolys = 2048;

    /// <summary>Identifying name for this navigation mesh.</summary>
    protected string _meshName = string.Empty;
    /// <summary>Detour navigation mesh.</summary>
    protected DtNavMesh? _navMesh;
    internal DtNavMesh? DetourNavMesh => _navMesh;
    /// <summary>Detour navigation mesh query.</summary>
    protected DtNavMeshQuery? _navMeshQuery;
    /// <summary>Detour navigation mesh query filter.</summary>
    protected readonly NavigationQueryFilter _queryFilter = new();
    /// <summary>Temporary data for finding a path.</summary>
    protected readonly long[] _pathPolys = new long[MaxPolys];
    protected readonly DtStraightPath[] _pathPoints = new DtStraightPath[MaxPolys];
    /// <summary>Maximum number of tiles.</summary>
    protected int _maxTiles = DefaultMaxTiles;
    /// <summary>Tile size.</summary>
    protected int _tileSize = DefaultTileSize;
    /// <summary>Cell size.</summary>
    protected float _cellSize = DefaultCellSize;
    /// <summary>Cell height.</summary>
    protected float _cellHeight = DefaultCellHeight;
    /// <summary>Total height range of the navigation mesh, in world space.</summary>
    protected Vector2f _heightRange;
    /// <summary>Navigation agent height.</summary>
    protected float _agentHeight = DefaultAgentHeight;
    /// <summary>Navigation agent radius.</summary>
    protected float _agentRadius = DefaultAgentRadius;
    /// <summary>Navigation agent max vertical climb.</summary>
    protected float _agentMaxClimb = DefaultAgentMaxClimb;
    /// <summary>Navigation agent max slope.</summary>
    protected float _agentMaxSlope = DefaultAgentMaxSlope;
    /// <summary>Maximum number of links per portal used in navigation mesh creation.</summary>
    protected int _maxPortalLinks = DefaultMaxPortalLinks;
    /// <summary>Region minimum size.</summary>
    protected float _regionMinSize = DefaultRegionMinSize;
    /// <summary>Region merge size.</summary>
    protected float _regionMergeSize = DefaultRegionMergeSize;
    /// <summary>Edge max length.</summary>
    protected float _edgeMaxLength = DefaultEdgeMaxLength;
    /// <summary>Edge max error.</summary>
    protected float _edgeMaxError = DefaultEdgeMaxError;
    /// <summary>Detail sampling distance.</summary>
    protected float _detailSampleDistance = DefaultDetailSampleDistance;
    /// <summary>Detail sampling maximum error.</summary>
    protected float _detailSampleMaxError = DefaultDetailSampleMaxError;
    /// <summary>Bounding box padding.</summary>
    protected Vector3f _padding = Vector3f.One;
    /// <summary>Type of the heightfield partitioning.</summary>
    protected NavmeshPartitionType _partitionType = NavmeshPartitionType.Watershed;
    /// <summary>Keep internal build resources for debug draw modes.</summary>
    protected bool _keepInterResults;
    /// <summary>Debug draw OffMeshConnection components.</summary>
    protected bool _drawOffMeshConnections;
    /// <summary>Debug draw NavArea components.</summary>
    protected bool _drawNavAreas;
    /// <summary>NavAreas for this NavMesh.</summary>
    protected readonly List<WeakReference<NavArea>> _areas = [];
    /// <summary>Current asynchronous tile build.</summary>
    protected readonly Dictionary<IntVector2, NavBuildData> _asyncTileBuilds = [];

    private readonly RcRand _random = new();

    /// <summary>Construct.</summary>
    public NavigationMesh()
    {
    }

    /// <summary>Destruct.</summary>
    public override void Dispose()
    {
        ReleaseNavigationMesh();
        base.Dispose();
    }

    // TODO: port
    // URHO3D_ACTION_STATIC_LABEL("Clear!", Clear, "Clears navigation mesh data");
    // URHO3D_ACTION_STATIC_LABEL("Rebuild!", Rebuild, "Rebuilds navigation mesh and adjusts maximum number of tiles");
    // URHO3D_ACTION_STATIC_LABEL("Allocate!", Allocate, "Allocates empty navigation mesh with specified maximum number of tiles");

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
                for (var i = 0; i < _areas.Count; ++i)
                {
                    if (_areas[i].TryGetTarget(out var area) && area.IsEnabledEffective)
                        area.DrawDebugGeometry(debug, depthTest);
                }
            }
        }
    }

    /// <summary>Return or set the given name of this navigation mesh.</summary>
    public string MeshName
    {
        get => _meshName;
        set => _meshName = value;
    }

    /// <summary>Return or set maximum number of tiles.</summary>
    [Serialized("Max Tiles")]
    public int MaxTiles
    {
        get => _maxTiles;
        set => _maxTiles = global::System.Math.Max(value, 1);
    }

    /// <summary>Return or set tile size.</summary>
    [Serialized("Tile Size")]
    public int TileSize
    {
        get => _tileSize;
        set => _tileSize = global::System.Math.Max(value, 16);
    }

    /// <summary>Return or set cell size.</summary>
    [Serialized("Cell Size")]
    public float CellSize
    {
        get => _cellSize;
        set => _cellSize = MathF.Max(value, MathDefs.Epsilon);
    }

    /// <summary>Return or set cell height.</summary>
    [Serialized("Cell Height")]
    public float CellHeight
    {
        get => _cellHeight;
        set => _cellHeight = MathF.Max(value, MathDefs.Epsilon);
    }

    /// <summary>Return or set min and max height of the navigation mesh, i.e. min and max Y value in world space.</summary>
    [Serialized("Mesh Height Range")]
    public Vector2f HeightRange
    {
        get => _heightRange;
        set => _heightRange = value;
    }

    /// <summary>Return whether the height range is valid.</summary>
    public bool IsHeightRangeValid => _heightRange.X < _heightRange.Y;

    /// <summary>Return or set navigation agent height.</summary>
    [Serialized("Agent Height")]
    public float AgentHeight
    {
        get => _agentHeight;
        set => _agentHeight = MathF.Max(value, MathDefs.Epsilon);
    }

    /// <summary>Return or set navigation agent radius.</summary>
    [Serialized("Agent Radius")]
    public float AgentRadius
    {
        get => _agentRadius;
        set => _agentRadius = MathF.Max(value, MathDefs.Epsilon);
    }

    /// <summary>Return or set navigation agent max vertical climb.</summary>
    [Serialized("Agent Max Climb")]
    public float AgentMaxClimb
    {
        get => _agentMaxClimb;
        set => _agentMaxClimb = MathF.Max(value, MathDefs.Epsilon);
    }

    /// <summary>Return or set navigation agent max slope.</summary>
    [Serialized("Agent Max Slope")]
    public float AgentMaxSlope
    {
        get => _agentMaxSlope;
        set => _agentMaxSlope = MathF.Max(value, 0.0f);
    }

    /// <summary>Return or set region minimum size.</summary>
    [Serialized("Region Min Size")]
    public float RegionMinSize
    {
        get => _regionMinSize;
        set => _regionMinSize = MathF.Max(value, MathDefs.Epsilon);
    }

    /// <summary>Return or set region merge size.</summary>
    [Serialized("Region Merge Size")]
    public float RegionMergeSize
    {
        get => _regionMergeSize;
        set => _regionMergeSize = MathF.Max(value, MathDefs.Epsilon);
    }

    /// <summary>Return or set edge max length.</summary>
    [Serialized("Edge Max Length")]
    public float EdgeMaxLength
    {
        get => _edgeMaxLength;
        set => _edgeMaxLength = MathF.Max(value, MathDefs.Epsilon);
    }

    /// <summary>Return or set edge max error.</summary>
    [Serialized("Edge Max Error")]
    public float EdgeMaxError
    {
        get => _edgeMaxError;
        set => _edgeMaxError = MathF.Max(value, MathDefs.Epsilon);
    }

    /// <summary>Return or set the maximum number of links per portal used in navigation mesh creation.</summary>
    [Serialized("Portal Links Count")]
    public int MaxPortalLinks
    {
        get => _maxPortalLinks;
        set => _maxPortalLinks = global::System.Math.Max(value, 1);
    }

    /// <summary>Return or set detail sampling distance.</summary>
    [Serialized("Detail Sample Distance")]
    public float DetailSampleDistance
    {
        get => _detailSampleDistance;
        set => _detailSampleDistance = MathF.Max(value, MathDefs.Epsilon);
    }

    /// <summary>Return or set detail sampling maximum error.</summary>
    [Serialized("Detail Sample Max Error")]
    public float DetailSampleMaxError
    {
        get => _detailSampleMaxError;
        set => _detailSampleMaxError = MathF.Max(value, MathDefs.Epsilon);
    }

    /// <summary>Return or set navigation mesh bounding box padding.</summary>
    [Serialized("Bounding Box Padding")]
    public Vector3f Padding
    {
        get => _padding;
        set => _padding = value;
    }

    /// <summary>Return whether has been initialized with valid navigation data.</summary>
    public bool IsInitialized => _navMesh is not null;

    /// <summary>Return or set the partition type used for polygon generation.</summary>
    [Serialized(
        "Partition Type",
        DefaultSource = nameof(GetDefaultPartitionType),
        EnumNames = ["watershed", "monotone"]
    )]
    public NavmeshPartitionType PartitionType
    {
        get => _partitionType;
        set => _partitionType = value;
    }

    /// <summary>Set or return navigation data attribute.</summary>
    [Serialized(
        "Navigation Data",
        DefaultSource = nameof(GetDefaultNavigationData),
        Mode = AttributeMode.Default | AttributeMode.NoEdit
    )]
    public virtual byte[] NavigationData
    {
        get => GetNavigationDataAttr();
        set => SetNavigationDataAttr(value);
    }

    /// <summary>Draw debug geometry for OffMeshConnection components.</summary>
    /// <summary>Return whether to draw OffMeshConnection components.</summary>
    [Serialized("Draw OffMeshConnections")]
    public bool DrawOffMeshConnections
    {
        get => _drawOffMeshConnections;
        set => _drawOffMeshConnections = value;
    }

    /// <summary>Draw debug geometry for NavArea components.</summary>
    /// <summary>Return whether to draw NavArea components.</summary>
    [Serialized("Draw NavAreas")]
    public bool DrawNavAreas
    {
        get => _drawNavAreas;
        set => _drawNavAreas = value;
    }

    /// <summary>Set the cost of an area.</summary>
    public void SetAreaCost(uint areaID, float cost)
    {
        _queryFilter.SetAreaCost(checked((int)areaID), cost);
    }

    /// <summary>Get the current cost of an area.</summary>
    public float GetAreaCost(uint areaID)
    {
        return _queryFilter.GetAreaCost(checked((int)areaID));
    }

    /// <summary>Clear navigation mesh data.</summary>
    public void Clear()
    {
        ReleaseNavigationMesh();
    }

    /// <summary>Allocate the navigation mesh without building any tiles. Return true if successful.</summary>
    public bool Allocate()
    {
        if (!AllocateMesh(_maxTiles))
            return false;

        Log.GetLogger().LogDebug("Allocated empty navigation mesh with max {MaxTiles} tiles", _maxTiles);
        SendRebuildEvent();
        return true;
    }

    /// <summary>Rebuild part of the navigation mesh contained by the world-space bounding box. Return true if successful.</summary>
    public bool BuildTilesInRegion(BoundingBox boundingBox)
    {
        var beginTileIndex = GetTileIndex(boundingBox.Min);
        var endTileIndex = GetTileIndex(boundingBox.Max);
        return BuildTiles(beginTileIndex, endTileIndex);
    }

    /// <summary>Rebuild part of the navigation mesh in the rectangular area. Return true if successful.</summary>
    public bool BuildTiles(IntVector2 from, IntVector2 to)
    {
        if (_node is null)
            return false;

        if (_navMesh is null)
        {
            Log.GetLogger().Error("Navigation mesh must first be built or allocated before it can be partially rebuilt");
            return false;
        }

        var geometryList = new List<NavigationGeometryInfo>();
        CollectGeometries(geometryList);

        var numTiles = BuildTilesFromGeometry(geometryList, from, to);
        Log.GetLogger().LogDebug("Rebuilt {NumTiles} tiles of the navigation mesh", numTiles);

        for (var z = from.Y; z <= to.Y; ++z)
        {
            for (var x = from.X; x <= to.X; ++x)
                SendTileAddedEvent(new IntVector2(x, z));
        }

        return true;
    }

    /// <summary>Rebuild part of the navigation mesh in the rectangular area. Task may be completed asynchronously.</summary>
    /// <summary>Callback is invoked for each tile in range.</summary>
    public void BuildTilesAsync(IntVector2 from, IntVector2 to, Action<IntVector2, bool>? callback = null)
    {
        if (_node is null)
            return;

        if (_navMesh is null)
        {
            Log.GetLogger().Error("Navigation mesh must first be built or allocated before it can be partially rebuilt");
            return;
        }

        var geometryList = new List<NavigationGeometryInfo>();
        CollectGeometries(geometryList);
        BuildTilesFromGeometryAsync(geometryList, from, to, callback);
    }

    /// <summary>Cancel asynchronous tile build, if any.</summary>
    public void CancelTileBuild(IntVector2 tileIndex)
    {
        _asyncTileBuilds.Remove(tileIndex);
    }

    /// <summary>Rebuild the navigation mesh allocating sufficient maximum number of tiles. Return true if successful.</summary>
    public bool Rebuild()
    {
        if (!RebuildMesh())
            return false;

        Log.GetLogger().LogDebug("Built navigation mesh with max {MaxTiles} tiles", _maxTiles);
        SendRebuildEvent();
        return true;
    }

    /// <summary>Offset internal mesh tile data by tile offset and Y.</summary>
    public void OffsetMeshTile(Span<byte> tileData, IntVector2 tileOffset, int offsetY)
    {
        var meshTile = DeserializeMeshData(tileData.ToArray());
        OffsetMeshTile(meshTile, tileOffset, offsetY);
        var result = SerializeMeshData(meshTile);
        result.CopyTo(tileData);
    }

    /// <summary>Offset serialized tile data.</summary>
    public virtual void OffsetTileData(Span<byte> tileData, IntVector3 delta)
    {
        var tileOffset = NavigationUtils.CalculateTileOffset(delta, _tileSize, _cellSize);
        if (tileOffset is null)
            return;

        using var buffer = new MemoryBuffer(tileData.ToArray());
        var meshTile = NavigationUtils.ReadDetourBuffer(buffer);
        if (!meshTile.IsValid || meshTile.Data is null)
            return;

        OffsetMeshTile(meshTile.Data, tileOffset.Value.TileOffset, tileOffset.Value.OffsetY);

        buffer.Seek(0);
        NavigationUtils.WriteDetourBuffer(buffer, meshTile);
        meshTile.Data.AsSpan().CopyTo(tileData);
    }

    /// <summary>Enumerate all tiles.</summary>
    public IReadOnlyList<IntVector2> GetAllTileIndices()
    {
        var result = new List<IntVector2>();
        for (var i = 0; i < _navMesh!.GetMaxTiles(); ++i)
        {
            var tile = _navMesh.GetTile(i);
            if (tile.data?.header is null)
                continue;
            result.Add(new IntVector2(tile.data.header.x, tile.data.header.y));
        }
        return result;
    }

    /// <summary>Return tile data.</summary>
    public virtual byte[] GetTileData(IntVector2 tileIndex)
    {
        if (_navMesh is null)
            return [];

        var tiles = new DtMeshTile[1];
        if (_navMesh.GetTilesAt(tileIndex.X, tileIndex.Y, tiles, 1) == 0 || tiles[0]?.data is null)
            return [];

        using var result = new VectorBuffer();
        NavigationUtils.WriteDetourBuffer(result, SerializeMeshData(tiles[0].data));
        return result.Buffer.ToArray();
    }

    /// <summary>Add tile to navigation mesh.</summary>
    public virtual bool AddTile(byte[] tileData)
    {
        using var buffer = new MemoryBuffer(tileData);
        return ReadTile(buffer, false);
    }

    /// <summary>Remove tile from navigation mesh.</summary>
    public virtual void RemoveTile(IntVector2 tileIndex)
    {
        if (_navMesh is null)
            return;

        CancelTileBuild(tileIndex);

        var tilesToRemove = new DtMeshTile[MaxLayers];
        var numTilesToRemove = _navMesh.GetTilesAt(tileIndex.X, tileIndex.Y, tilesToRemove, MaxLayers);
        for (var i = 0; i < numTilesToRemove; ++i)
        {
            var tileRef = _navMesh.GetTileRefAt(tileIndex.X, tileIndex.Y, tilesToRemove[i].data.header.layer);
            _navMesh.RemoveTile(tileRef);
        }

        // Send event
        if (numTilesToRemove > 0)
        {
            var eventData = new NavigationEvents.NavigationTileRemovedEvent
            {
                Node = Node,
                Mesh = this,
                Tile = tileIndex
            };
            SendEvent(ref eventData);
        }
    }

    /// <summary>Remove all tiles from navigation mesh.</summary>
    public virtual void RemoveAllTiles()
    {
        _asyncTileBuilds.Clear();

        var navMesh = _navMesh!;
        for (var i = 0; i < navMesh.GetMaxTiles(); ++i)
        {
            var tile = navMesh.GetTile(i);
            if (tile.data?.header is not null)
                navMesh.RemoveTile(navMesh.GetTileRef(tile));
        }

        // Send event
        var eventData = new NavigationEvents.NavigationAllTilesRemovedEvent
        {
            Node = Node,
            Mesh = this
        };
        SendEvent(ref eventData);
    }

    /// <summary>Return whether the navigation mesh has tile.</summary>
    public bool HasTile(IntVector2 tileIndex)
    {
        if (_navMesh is null)
            return false;
        var tile = new DtMeshTile[1];
        return _navMesh.GetTilesAt(tileIndex.X, tileIndex.Y, tile, 1) != 0;
    }

    /// <summary>Return bounding box of the tile in the world space. Y coordinate spans from -infinity to infinity.</summary>
    public BoundingBox GetTileBoundingBoxColumn(IntVector2 tileIndex)
    {
        var heightRange = IsHeightRangeValid
            ? _heightRange
            : new Vector2f(-MathDefs.LargeValue, MathDefs.LargeValue);
        var tileEdgeLength = _tileSize * _cellSize;
        var minPosition = new Vector3f(tileIndex.X * tileEdgeLength, heightRange.X, tileIndex.Y * tileEdgeLength);
        var maxPosition = new Vector3f((tileIndex.X + 1) * tileEdgeLength, heightRange.Y, (tileIndex.Y + 1) * tileEdgeLength);
        return new BoundingBox(minPosition, maxPosition);
    }

    /// <summary>Return index of the tile at the position.</summary>
    public IntVector2 GetTileIndex(Vector3f position)
    {
        var tileEdgeLength = _tileSize * _cellSize;
        return Vector2f.FloorToInt(position.ToXZ() / tileEdgeLength);
    }

    /// <summary>Find the nearest point on the navigation mesh to a given point. Extents specifies how far out from the specified point to check along each axis.</summary>
    public Vector3f FindNearestPoint(Vector3f point, Vector3f? extents = null, IDtQueryFilter? filter = null)
    {
        return FindNearestPoint(point, extents ?? Vector3f.One, filter, out _);
    }

    public Vector3f FindNearestPoint(Vector3f point, Vector3f extents, IDtQueryFilter? filter, out long nearestRef)
    {
        nearestRef = 0;
        if (!InitializeQuery())
            return point;

        var transform = Node.GetWorldTransform();
        var inverse = transform.Inverse;
        var localPoint = inverse * point;
        _navMeshQuery!.FindNearestPoly(ToRc(localPoint), ToRc(extents), filter ?? _queryFilter,
            out nearestRef, out var nearestPoint, out _);
        return nearestRef != 0 ? transform * FromRc(nearestPoint) : point;
    }

    /// <summary>Try to move along the surface from one point to another.</summary>
    public Vector3f MoveAlongSurface(Vector3f start, Vector3f end, Vector3f? extents = null, int maxVisited = 3,
        IDtQueryFilter? filter = null)
    {
        if (!InitializeQuery())
            return end;

        var transform = Node.GetWorldTransform();
        var inverse = transform.Inverse;
        var localStart = inverse * start;
        var localEnd = inverse * end;
        var queryFilter = filter ?? _queryFilter;
        _navMeshQuery!.FindNearestPoly(ToRc(localStart), ToRc(extents ?? Vector3f.One), queryFilter,
            out var startRef, out _, out _);
        if (startRef == 0)
            return end;

        maxVisited = global::System.Math.Max(maxVisited, 0);
        var visited = new long[maxVisited];
        _navMeshQuery.MoveAlongSurface(startRef, ToRc(localStart), ToRc(localEnd), queryFilter,
            out var resultPos, visited, out _, maxVisited);
        return transform * FromRc(resultPos);
    }

    /// <summary>Find a path between world space points. Return non-empty list of points if successful. Extents specifies how far off the navigation mesh the points can be.</summary>
    public void FindPath(ICollection<Vector3f> destination, Vector3f start, Vector3f end, Vector3f? extents = null,
        IDtQueryFilter? filter = null)
    {
        var navPathPoints = new List<NavigationPathPoint>();
        FindPath(navPathPoints, start, end, extents, filter);

        destination.Clear();
        for (var i = 0; i < navPathPoints.Count; ++i)
            destination.Add(navPathPoints[i].Position);
    }

    /// <summary>Find a path between world space points. Return non-empty list of navigation path points if successful. Extents specifies how far off the navigation mesh the points can be.</summary>
    public void FindPath(ICollection<NavigationPathPoint> destination, Vector3f start, Vector3f end,
        Vector3f? extents = null, IDtQueryFilter? filter = null)
    {
        destination.Clear();
        if (!InitializeQuery())
            return;

        // Navigation data is in local space. Transform path points from world to local
        var transform = Node.GetWorldTransform();
        var inverse = transform.Inverse;
        var localStart = inverse * start;
        var localEnd = inverse * end;
        var queryFilter = filter ?? _queryFilter;
        _navMeshQuery!.FindNearestPoly(ToRc(localStart), ToRc(extents ?? Vector3f.One), queryFilter,
            out var startRef, out _, out _);
        _navMeshQuery.FindNearestPoly(ToRc(localEnd), ToRc(extents ?? Vector3f.One), queryFilter,
            out var endRef, out _, out _);

        if (startRef == 0 || endRef == 0)
            return;

        _navMeshQuery.FindPath(startRef, endRef, ToRc(localStart), ToRc(localEnd), queryFilter,
            _pathPolys, out var numPolys, MaxPolys);
        if (numPolys == 0)
            return;

        var actualLocalEnd = ToRc(localEnd);

        // If full path was not found, clamp end point to the end polygon
        if (_pathPolys[numPolys - 1] != endRef)
            _navMeshQuery.ClosestPointOnPoly(_pathPolys[numPolys - 1], ToRc(localEnd), out actualLocalEnd, out _);

        _navMeshQuery.FindStraightPath(ToRc(localStart), actualLocalEnd, _pathPolys, numPolys,
            _pathPoints, out var numPathPoints, MaxPolys, 0);

        // Transform path result back to world space
        for (var i = 0; i < numPathPoints; ++i)
        {
            var point = new NavigationPathPoint
            {
                Position = transform * FromRc(_pathPoints[i].pos),
                Flag = (NavigationPathPointFlag)_pathPoints[i].flags
            };

            // Walk through all NavAreas and find nearest
            uint nearestNavAreaID = 0;
            var nearestDistance = MathDefs.LargeValue;
            for (var j = 0; j < _areas.Count; ++j)
            {
                if (_areas[j].TryGetTarget(out var area) && area.IsEnabledEffective)
                {
                    var boundingBox = area.WorldBoundingBox;
                    if (boundingBox.IsInside(point.Position) == Intersection.Inside)
                    {
                        var areaWorldCenter = area.Node.GetWorldPosition();
                        var distance = (areaWorldCenter - point.Position).LengthSquared;
                        if (distance < nearestDistance)
                        {
                            nearestDistance = distance;
                            nearestNavAreaID = checked((uint)area.AreaID);
                        }
                    }
                }
            }
            point.AreaID = unchecked((byte)nearestNavAreaID);
            destination.Add(point);
        }
    }

    /// <summary>Return a random point on the navigation mesh.</summary>
    public Vector3f GetRandomPoint(IDtQueryFilter? filter = null)
    {
        return GetRandomPoint(filter, out _);
    }

    public Vector3f GetRandomPoint(IDtQueryFilter? filter, out long randomRef)
    {
        randomRef = 0;
        if (!InitializeQuery())
            return Vector3f.Zero;

        _navMeshQuery!.FindRandomPoint(filter ?? _queryFilter, _random, out randomRef, out var point);
        return Node.GetWorldTransform() * FromRc(point);
    }

    /// <summary>Return a random point on the navigation mesh within a circle. The circle radius is only a guideline and in practice the returned point may be further away.</summary>
    public Vector3f GetRandomPointInCircle(Vector3f center, float radius, Vector3f? extents = null,
        IDtQueryFilter? filter = null)
    {
        return GetRandomPointInCircle(center, radius, extents ?? Vector3f.One, filter, out _);
    }

    public Vector3f GetRandomPointInCircle(Vector3f center, float radius, Vector3f extents,
        IDtQueryFilter? filter, out long randomRef)
    {
        randomRef = 0;
        if (!InitializeQuery())
            return center;

        var transform = Node.GetWorldTransform();
        var inverse = transform.Inverse;
        var localCenter = inverse * center;
        var queryFilter = filter ?? _queryFilter;
        _navMeshQuery!.FindNearestPoly(ToRc(localCenter), ToRc(extents), queryFilter,
            out var startRef, out _, out _);
        if (startRef == 0)
            return center;

        _navMeshQuery.FindRandomPointAroundCircle(startRef, ToRc(localCenter), radius, queryFilter, _random,
            out randomRef, out var point);
        return transform * FromRc(point);
    }

    /// <summary>Return distance to wall from a point. Maximum search radius must be specified.</summary>
    public float GetDistanceToWall(Vector3f point, float radius, Vector3f? extents = null,
        IDtQueryFilter? filter = null)
    {
        return GetDistanceToWall(point, radius, extents ?? Vector3f.One, filter, out _, out _);
    }

    public float GetDistanceToWall(Vector3f point, float radius, Vector3f extents, IDtQueryFilter? filter,
        out Vector3f hitPos, out Vector3f hitNormal)
    {
        hitPos = Vector3f.Zero;
        hitNormal = Vector3f.Down;
        if (!InitializeQuery())
            return radius;

        var inverse = Node.GetWorldTransform().Inverse;
        var localPoint = inverse * point;
        var queryFilter = filter ?? _queryFilter;
        _navMeshQuery!.FindNearestPoly(ToRc(localPoint), ToRc(extents), queryFilter,
            out var startRef, out _, out _);
        if (startRef == 0)
            return radius;

        _navMeshQuery.FindDistanceToWall(startRef, ToRc(localPoint), radius, queryFilter,
            out var hitDistance, out var resultPosition, out var resultNormal);
        hitPos = FromRc(resultPosition);
        hitNormal = FromRc(resultNormal);
        return hitDistance;
    }

    /// <summary>Perform a walkability raycast on the navigation mesh between start and end and return the point where a wall was hit, or the end point if no walls.</summary>
    public Vector3f Raycast(Vector3f start, Vector3f end, Vector3f? extents = null, IDtQueryFilter? filter = null)
    {
        return Raycast(start, end, extents ?? Vector3f.One, filter, out _);
    }

    public Vector3f Raycast(Vector3f start, Vector3f end, Vector3f extents, IDtQueryFilter? filter,
        out Vector3f hitNormal)
    {
        hitNormal = Vector3f.Down;
        if (!InitializeQuery())
            return end;

        var transform = Node.GetWorldTransform();
        var inverse = transform.Inverse;
        var localStart = inverse * start;
        var localEnd = inverse * end;
        var queryFilter = filter ?? _queryFilter;
        _navMeshQuery!.FindNearestPoly(ToRc(localStart), ToRc(extents), queryFilter,
            out var startRef, out _, out _);
        if (startRef == 0)
            return end;

        _navMeshQuery.Raycast(startRef, ToRc(localStart), ToRc(localEnd), queryFilter,
            out var t, out var normal, _pathPolys, out _, MaxPolys);
        hitNormal = FromRc(normal);
        if (t == float.MaxValue)
            t = 1.0f;
        return start.Lerp(end, t);
    }

    /// <summary>Add debug geometry to the debug renderer.</summary>
    public void DrawDebugGeometry(bool depthTest)
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
    public virtual void SetNavigationDataAttr(byte[] value)
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

        var parameters = new DtNavMeshParams
        {
            tileWidth = buffer.ReadFloat(),
            tileHeight = buffer.ReadFloat(),
            maxTiles = buffer.ReadInt(),
            maxPolys = buffer.ReadInt()
        };

        if (!MathDefs.Equals(parameters.tileWidth, _cellSize * _tileSize))
        {
            Log.GetLogger().LogWarning("Incompatible navigation mesh parameters, please rebuild navigation data");
            return;
        }

        _navMesh = new DtNavMesh();
        if (_navMesh.Init(parameters, DtDetour.DT_VERTS_PER_POLYGON).Failed())
        {
            Log.GetLogger().Error("Could not initialize navigation mesh");
            ReleaseNavigationMesh();
            return;
        }

        var numTiles = 0;
        while (!buffer.IsEof())
        {
            if (ReadTile(buffer, true))
                ++numTiles;
            else
                return;
        }

        Log.GetLogger().LogDebug("Created navigation mesh with {NumTiles} tiles from serialized data", numTiles);
        // \todo Shall we send E_NAVIGATION_MESH_REBUILT here?
    }

    /// <summary>Return navigation data attribute.</summary>
    public virtual byte[] GetNavigationDataAttr()
    {
        using var result = new VectorBuffer();
        if (_navMesh is not null)
        {
            result.WriteBoundingBox(new BoundingBox());
            result.WriteInt(0);
            result.WriteInt(0);
            result.WriteInt(NavigationDataVersion);

            ref readonly var parameters = ref _navMesh.GetParams();
            result.WriteFloat(parameters.tileWidth);
            result.WriteFloat(parameters.tileHeight);
            result.WriteInt(parameters.maxTiles);
            result.WriteInt(parameters.maxPolys);

            for (var i = 0; i < _navMesh.GetMaxTiles(); ++i)
            {
                var tile = _navMesh.GetTile(i);
                if (tile.data?.header is null)
                    continue;
                NavigationUtils.WriteDetourBuffer(result, SerializeMeshData(tile.data));
            }
        }
        return result.Buffer.ToArray();
    }

    protected override void OnSceneSet(SceneType? previousScene, SceneType? scene)
    {
        base.OnSceneSet(previousScene, scene);
        if (previousScene is not null)
            UnsubscribeFromEvent<SceneEvents.WorldOriginUpdateEvent>();
        if (scene is not null)
            SubscribeToEvent<SceneEvents.WorldOriginUpdateEvent>(scene, HandleWorldOriginUpdate);
    }

    /// <summary>Allocate the navigation mesh without building any tiles. Return true if successful.</summary>
    protected virtual bool AllocateMesh(int maxTiles)
    {
        // Release existing navigation data
        ReleaseNavigationMesh();
        if (_node is null)
            return false;

        // Calculate max number of polygons, 22 bits available to identify both tile & polygon within tile
        var tileBits = MathDefs.LogBaseTwo(checked((uint)maxTiles));
        var maxPolys = 1 << checked(22 - (int)tileBits);
        var tileEdgeLength = _tileSize * _cellSize;
        var parameters = new DtNavMeshParams
        {
            tileWidth = tileEdgeLength,
            tileHeight = tileEdgeLength,
            maxTiles = maxTiles,
            maxPolys = maxPolys
        };

        _navMesh = new DtNavMesh();
        if (_navMesh.Init(parameters, DtDetour.DT_VERTS_PER_POLYGON).Failed())
        {
            Log.GetLogger().Error("Could not initialize navigation mesh");
            ReleaseNavigationMesh();
            return false;
        }
        return true;
    }

    /// <summary>Rebuild the navigation mesh allocating sufficient maximum number of tiles. Return true if successful.</summary>
    protected virtual bool RebuildMesh()
    {
        // Collect geometry and update dimensions
        var geometryList = new List<NavigationGeometryInfo>();
        CollectGeometries(geometryList);
        var boundingBox = NavigationUtils.CalculateBoundingBox(geometryList, _padding);
        var maxTiles = NavigationUtils.CalculateMaxTiles(boundingBox, _tileSize, _cellSize);
        _maxTiles = maxTiles != 0 ? maxTiles : DefaultMaxTiles;

        // Allocate navigation mesh
        if (!AllocateMesh(_maxTiles))
            return false;

        // Build navigation mesh
        var beginTileIndex = GetTileIndex(boundingBox.Min);
        var endTileIndex = GetTileIndex(boundingBox.Max);
        BuildTilesFromGeometry(geometryList, beginTileIndex, endTileIndex);
        return true;
    }

    /// <summary>Returns functor that builds tile data. The functor may be called from worker thread.</summary>
    protected virtual Func<NavBuildData, bool> GetTileBuilder()
    {
        return build =>
        {
            if (!BuildCompactHeightField(build))
                return false;
            if (!BuildSimpleTileData((SimpleNavBuildData)build))
                return false;
            return true;
        };
    }

    /// <summary>Collect data for tile building.</summary>
    protected virtual NavBuildData CreateTileBuildData(
        IReadOnlyList<NavigationGeometryInfo> geometryList, IntVector2 tileIndex)
    {
        var build = new SimpleNavBuildData();
        InitializeBuildData(build, tileIndex, geometryList);
        return build;
    }

    /// <summary>Replace tile data in navigation mesh.</summary>
    protected virtual bool ReplaceTileData(NavBuildData build)
    {
        var tileIndex = build.TileIndex + build.PendingTileOffset;
        var tileData = ((SimpleNavBuildData)build).TileData!;
        OffsetMeshTile(tileData, build.PendingTileOffset, build.PendingOffsetY);
        _navMesh!.RemoveTile(_navMesh.GetTileRefAt(tileIndex.X, tileIndex.Y, 0));
        if (_navMesh.AddTile(tileData, DtTileFlags.DT_TILE_FREE_DATA, 0, out _).Failed())
        {
            Log.GetLogger().Error("Failed to add navigation mesh tile");
            return false;
        }
        ((SimpleNavBuildData)build).TileData = null;
        return true;
    }

    /// <summary>Offset geometry of all tiles by a given offset.</summary>
    protected virtual void OffsetTilesGeometry(IntVector2 tileOffset, int offsetY)
    {
        foreach (var buildData in _asyncTileBuilds.Values)
        {
            buildData.PendingTileOffset -= tileOffset;
            buildData.PendingOffsetY -= offsetY;
        }

        var tiles = new List<DtMeshData>();
        for (var i = 0; i < _navMesh!.GetMaxTiles(); ++i)
        {
            var meshTile = _navMesh.GetTile(i);
            if (meshTile.data is not null)
            {
                tiles.Add(meshTile.data);
                _navMesh.RemoveTile(_navMesh.GetTileRef(meshTile));
            }
        }

        foreach (var tileData in tiles)
        {
            OffsetMeshTile(tileData, -tileOffset, -offsetY);
            _navMesh.AddTile(tileData, DtTileFlags.DT_TILE_FREE_DATA, 0, out _);
        }
    }

    /// <summary>Build mesh tiles from the geometry data. Return number of tiles built.</summary>
    protected int BuildTilesFromGeometry(
        IReadOnlyList<NavigationGeometryInfo> geometryList, IntVector2 from, IntVector2 to)
    {
        var numTiles = 0;
        var builder = GetTileBuilder();
        for (var z = from.Y; z <= to.Y; ++z)
        {
            for (var x = from.X; x <= to.X; ++x)
            {
                var tileIndex = new IntVector2(x, z);
                CancelTileBuild(tileIndex);
                using var build = CreateTileBuildData(geometryList, tileIndex);
                if (build.IsEmpty)
                    continue;
                if (!builder(build))
                    continue;
                if (!ReplaceTileData(build))
                    continue;
                ++numTiles;
            }
        }
        return numTiles;
    }

    /// <summary>Schedule mesh tile building.</summary>
    protected void BuildTilesFromGeometryAsync(IReadOnlyList<NavigationGeometryInfo> geometryList,
        IntVector2 from, IntVector2 to, Action<IntVector2, bool>? callback)
    {
        var workQueue = this.GetSubsystem<WorkQueue>();
        var builder = GetTileBuilder();
        var weakSelf = new WeakReference<NavigationMesh>(this);

        for (var z = from.Y; z <= to.Y; ++z)
        {
            for (var x = from.X; x <= to.X; ++x)
            {
                var tileIndex = new IntVector2(x, z);
                var build = CreateTileBuildData(geometryList, tileIndex);
                if (build.IsEmpty)
                {
                    build.Dispose();
                    callback?.Invoke(tileIndex, true);
                    continue;
                }

                _asyncTileBuilds[tileIndex] = build;
                workQueue.PostTask(() =>
                {
                    var success = builder(build);
                    workQueue.PostDelayedTaskForMainThread(() =>
                    {
                        if (success && weakSelf.TryGetTarget(out var self))
                            self.CompleteAsyncTileBuild(build);
                        callback?.Invoke(build.TileIndex, success);
                    });
                });
            }
        }
    }

    /// <summary>Complete mesh tile building.</summary>
    protected void CompleteAsyncTileBuild(NavBuildData build)
    {
        if (!_asyncTileBuilds.TryGetValue(build.TileIndex, out var currentBuild)
            || !ReferenceEquals(currentBuild, build))
            return;

        _asyncTileBuilds.Remove(build.TileIndex);
        ReplaceTileData(build);
        SendTileAddedEvent(build.TileIndex);
        build.Dispose();
    }

    /// <summary>Send rebuild event.</summary>
    protected void SendRebuildEvent()
    {
        var eventData = new NavigationEvents.NavigationMeshRebuiltEvent
        {
            Node = Node,
            Mesh = this
        };
        SendEvent(ref eventData);
    }

    /// <summary>Send tile added event.</summary>
    protected void SendTileAddedEvent(IntVector2 tileIndex)
    {
        var eventData = new NavigationEvents.NavigationTileAddedEvent
        {
            Node = Node,
            Mesh = this,
            Tile = tileIndex
        };
        SendEvent(ref eventData);
    }

    /// <summary>Collect geometry from under Navigable components.</summary>
    protected void CollectGeometries(List<NavigationGeometryInfo> geometryList)
    {
        // Get Navigable components from child nodes, not from whole scene. This makes it possible to partition
        // the scene into several navigation meshes
        var navigables = Node.FindComponents<Navigable>();
        var processedNodes = new HashSet<Node>();
        for (var i = 0; i < navigables.Count; ++i)
        {
            if (navigables[i].IsEnabledEffective)
            {
                CollectGeometries(
                    geometryList, navigables[i], navigables[i].Node, processedNodes, navigables[i].Recursive);
            }
        }

        // Get offmesh connections
        var inverse = Node.GetWorldTransform().Inverse;
        var connections = Node.FindComponents<OffMeshConnection>();
        for (var i = 0; i < connections.Count; ++i)
        {
            var connection = connections[i];
            if (connection.IsEnabledEffective && connection.EndPoint is not null)
            {
                var transform = connection.Node.GetWorldTransform();
                geometryList.Add(new NavigationGeometryInfo
                {
                    Component = connection,
                    BoundingBox = new BoundingBox(new Sphere(transform.Translation, connection.Radius)).Transformed(inverse),
                    AreaId = NavigationDefs.DeduceAreaId
                });
            }
        }

        // Get nav area volumes
        var navAreas = Node.FindComponents<NavArea>();
        _areas.Clear();
        for (var i = 0; i < navAreas.Count; ++i)
        {
            var area = navAreas[i];
            if (area.IsEnabledEffective)
            {
                geometryList.Add(new NavigationGeometryInfo
                {
                    Component = area,
                    BoundingBox = area.WorldBoundingBox,
                    AreaId = NavigationDefs.DeduceAreaId
                });
                _areas.Add(new WeakReference<NavArea>(area));
            }
        }
    }

    /// <summary>Visit nodes and collect navigable geometry.</summary>
    protected void CollectGeometries(List<NavigationGeometryInfo> geometryList, Navigable navigable,
        Node node, HashSet<Node> processedNodes, bool recursive)
    {
        // Make sure nodes are not included twice
        if (processedNodes.Contains(node))
            return;
        // Exclude obstacles and crowd agents from consideration
        if (node.GetComponent<Obstacle>() is not null || node.GetComponent<CrowdAgent>() is not null)
            return;
        processedNodes.Add(node);

        var inverse = Node.GetWorldTransform().Inverse;
        var drawables = node.FindComponents<Drawable>(ComponentSearchFlag.Self | ComponentSearchFlag.Derived);
        for (var i = 0; i < drawables.Count; ++i)
        {
            /// \todo Evaluate whether should handle other types. Now StaticModel & TerrainPatch are supported, others skipped
            var drawable = drawables[i];
            if (!drawable.IsEnabledEffective)
                continue;

            uint lodLevel;
            if (drawable.GetType() == typeof(StaticModel))
                lodLevel = ((StaticModel)drawable).GetOcclusionLodLevel();
            else if (drawable.GetType() == typeof(TerrainPatch))
                lodLevel = 0;
            else
                continue;

            geometryList.Add(new NavigationGeometryInfo
            {
                Component = drawable,
                LodLevel = lodLevel,
                Transform = inverse * node.GetWorldTransform(),
                BoundingBox = drawable.GetWorldBoundingBox().Transformed(inverse),
                AreaId = navigable.EffectiveAreaId
            });
        }

        if (recursive)
        {
            var children = node.Children;
            for (var i = 0; i < children.Count; ++i)
                CollectGeometries(geometryList, navigable, children[i], processedNodes, recursive);
        }
    }

    /// <summary>Initialize navigation build data from component configuration.</summary>
    protected void InitializeBuildData(
        NavBuildData build, IntVector2 tileIndex, IReadOnlyList<NavigationGeometryInfo> geometryList)
    {
        build.TileIndex = tileIndex;
        build.TileColumn = GetTileBoundingBoxColumn(tileIndex);
        build.TileBoundingBox = IsHeightRangeValid
            ? build.TileColumn
            : NavigationUtils.CalculateTileBoundingBox(geometryList, build.TileColumn);

        build.PartitionType = _partitionType;
        build.AgentHeight = _agentHeight;
        build.AgentRadius = _agentRadius;
        build.AgentMaxClimb = _agentMaxClimb;
        build.PortalLinksCount = _maxPortalLinks;

        var borderSize = MathDefs.CeilToInt(_agentRadius / _cellSize) + 3;
        var minRegionArea = MathF.Floor(MathF.Sqrt(_regionMinSize)) * _cellSize * _cellSize;
        var mergeRegionArea = MathF.Floor(MathF.Sqrt(_regionMergeSize)) * _cellSize * _cellSize;
        var partition = _partitionType == NavmeshPartitionType.Watershed
            ? RcPartition.WATERSHED
            : RcPartition.MONOTONE;
        build.RecastConfig = new RcConfig(
            true, _tileSize, _tileSize, borderSize, partition,
            _cellSize, _cellHeight, _agentMaxSlope, _agentHeight, _agentRadius, _agentMaxClimb,
            minRegionArea, mergeRegionArea, _edgeMaxLength, _edgeMaxError, 6,
            _detailSampleDistance, _detailSampleMaxError,
            true, true, true, new RcAreaModification(RcRecast.RC_WALKABLE_AREA), true);

        var padding = new Vector3f(borderSize * _cellSize, _padding.Y, borderSize * _cellSize);
        build.CollectGeometryBoundingBox = build.TileBoundingBox.Padded(padding);
        CollectTileGeometry(build, Node.GetWorldTransform(), geometryList, build.CollectGeometryBoundingBox);
    }

    /// <summary>Ensure that the navigation mesh query is initialized. Return true if successful.</summary>
    protected bool InitializeQuery()
    {
        if (_navMesh is null || _node is null)
            return false;
        if (_navMeshQuery is not null)
            return true;
        _navMeshQuery = new DtNavMeshQuery(_navMesh);
        return true;
    }

    internal bool InitializeQueryForCrowd() => InitializeQuery();

    /// <summary>Release the navigation mesh and the query.</summary>
    protected virtual void ReleaseNavigationMesh()
    {
        _navMesh = null;
        _navMeshQuery = null;
        foreach (var build in _asyncTileBuilds.Values)
            build.Dispose();
        _asyncTileBuilds.Clear();
    }

    /// <summary>Draw debug geometry for single tile.</summary>
    protected void DrawDebugTileGeometry(DebugRenderer debug, bool depthTest, int tileIndex)
    {
        var polygonEdgeColor = new Color(1.0f, 1.0f, 0.0f, 0.5f);
        var polygonLinkColor = new Color(0.0f, 1.0f, 0.0f, 0.5f);
        var polygonBadLinkColor = new Color(1.0f, 0.0f, 0.0f, 0.5f);

        if (tileIndex >= _navMesh!.GetMaxTiles())
            return;

        var tile = _navMesh.GetTile(tileIndex);
        if (tile.data?.header is null)
            return;

        var worldTransform = Node.GetWorldTransform();
        for (var polyIndex = 0; polyIndex < tile.data.header.polyCount; ++polyIndex)
        {
            var poly = tile.data.polys[polyIndex];
            var polyCenter = GetPolygonCenter(tile, poly);
            var polyRef = DtDetour.EncodePolyId(tile.salt, tileIndex, polyIndex);

            for (var i = 0; i < poly.vertCount; ++i)
            {
                var firstVertex = GetPolygonVertex(tile, poly, i);
                var secondVertex = GetPolygonVertex(tile, poly, (i + 1) % poly.vertCount);
                debug.AddLine(worldTransform * firstVertex, worldTransform * secondVertex, polygonEdgeColor, depthTest);
            }

            for (var link = poly.firstLink; link != DtDetour.DT_NULL_LINK; link = tile.links[link].next)
            {
                var linkData = tile.links[link];
                if (_navMesh.GetTileAndPolyByRef(linkData.refs, out var otherTile, out var otherPoly).Failed())
                    continue;

                var isBadLink = true;
                for (var otherLink = otherPoly.firstLink; otherLink != DtDetour.DT_NULL_LINK;
                     otherLink = otherTile.links[otherLink].next)
                {
                    if (otherTile.links[otherLink].refs == polyRef)
                    {
                        isBadLink = false;
                        break;
                    }
                }

                var otherPolyCenter = GetPolygonCenter(otherTile, otherPoly);
                var color = !isBadLink ? polygonLinkColor : polygonBadLinkColor;
                debug.AddLine(worldTransform * polyCenter, worldTransform * otherPolyCenter, color, depthTest);
            }
        }
    }

    /// <summary>Get geometry data within a bounding box.</summary>
    protected static void CollectTileGeometry(NavBuildData build, Matrix3x4 rootTransform,
        IReadOnlyList<NavigationGeometryInfo> geometryList, BoundingBox box)
    {
        var inverse = rootTransform.Inverse;
        for (var i = 0; i < geometryList.Count; ++i)
        {
            var geometryInfo = geometryList[i];
            if (box.IsInsideFast(geometryInfo.BoundingBox) == Intersection.Outside)
                continue;

            var transform = geometryInfo.Transform;
            if (geometryInfo.Component is OffMeshConnection connection)
            {
                var start = inverse * connection.Node.GetWorldPosition();
                var end = inverse * connection.EndPoint!.GetWorldPosition();
                build.OffMeshVertices.Add(start);
                build.OffMeshVertices.Add(end);
                build.OffMeshRadii.Add(connection.Radius);
                build.OffMeshFlags.Add(unchecked((ushort)connection.Mask));
                build.OffMeshAreas.Add(unchecked((byte)connection.AreaID));
                build.OffMeshDir.Add(unchecked((byte)(connection.Bidirectional ? DtDetour.DT_OFFMESH_CON_BIDIR : 0)));
                continue;
            }
            if (geometryInfo.Component is NavArea area)
            {
                build.NavAreas.Add(new NavAreaStub
                {
                    AreaID = unchecked((byte)area.AreaID),
                    Bounds = area.WorldBoundingBox
                });
                continue;
            }

            if (geometryInfo.Component is Drawable drawable)
            {
                var batches = drawable.GetBatches();
                for (var j = 0; j < batches.Count; ++j)
                {
                    AddTriMeshGeometry(
                        build, drawable.GetLodGeometry(checked((uint)j), geometryInfo.LodLevel),
                        transform, geometryInfo.AreaId);
                }
            }
        }
    }

    /// <summary>Add a triangle mesh to the geometry data.</summary>
    protected static void AddTriMeshGeometry(
        NavBuildData build, Geometry? geometry, Matrix3x4 transform, byte areaId)
    {
        if (geometry is null)
            return;

        geometry.GetRawDataShared(out var vertexData, out var vertexSize, out var indexData, out var indexSize, out var elements);
        if (vertexData is null || indexData is null || elements is null
            || VertexBuffer.GetElementOffset(elements, VertexElementType.Vector3, VertexElementSemantic.Position) != 0)
            return;

        var srcIndexStart = checked((int)geometry.IndexStart);
        var srcIndexCount = checked((int)geometry.IndexCount);
        var srcVertexStart = checked((int)geometry.VertexStart);
        var srcVertexCount = checked((int)geometry.VertexCount);
        if (srcIndexCount == 0)
            return;

        var destVertexStart = build.Vertices.Count;
        for (var k = srcVertexStart; k < srcVertexStart + srcVertexCount; ++k)
        {
            var offset = checked(k * (int)vertexSize);
            var vertex = new Vector3f(
                BitConverter.ToSingle(vertexData.AsSpan(offset, sizeof(float))),
                BitConverter.ToSingle(vertexData.AsSpan(offset + sizeof(float), sizeof(float))),
                BitConverter.ToSingle(vertexData.AsSpan(offset + sizeof(float) * 2, sizeof(float))));
            build.Vertices.Add(transform * vertex);
        }

        // Copy remapped indices
        for (var i = 0; i < srcIndexCount; ++i)
        {
            var offset = checked((srcIndexStart + i) * (int)indexSize);
            var index = indexSize == sizeof(ushort)
                ? BinaryPrimitives.ReadUInt16LittleEndian(indexData.AsSpan(offset, sizeof(ushort)))
                : checked((int)BinaryPrimitives.ReadUInt32LittleEndian(indexData.AsSpan(offset, sizeof(uint))));
            build.Indices.Add(index - srcVertexStart + destVertexStart);
        }

        for (var i = 0; i < srcIndexCount / 3; ++i)
            build.AreaIds.Add(areaId);
    }

    /// <summary>Build compact heightfield.</summary>
    protected static bool BuildCompactHeightField(NavBuildData build)
    {
        var config = build.RecastConfig!;
        var vertices = new float[build.Vertices.Count * 3];
        for (var i = 0; i < build.Vertices.Count; ++i)
        {
            vertices[i * 3] = build.Vertices[i].X;
            vertices[i * 3 + 1] = build.Vertices[i].Y;
            vertices[i * 3 + 2] = build.Vertices[i].Z;
        }
        var indices = build.Indices.ToArray();
        var areas = build.AreaIds.ToArray();
        NavigationUtils.DeduceAreaIds(config.WalkableSlopeAngle, vertices, indices, indices.Length / 3, areas);
        var areaIds = new int[areas.Length];
        for (var i = 0; i < areas.Length; ++i)
            areaIds[i] = areas[i];

        var bounds = build.CollectGeometryBoundingBox;
        build.HeightField = new RcHeightfield(
            config.TileSizeX + config.BorderSize * 2,
            config.TileSizeZ + config.BorderSize * 2,
            ToRc(bounds.Min), ToRc(bounds.Max), config.Cs, config.Ch, config.BorderSize);
        RcRasterizations.RasterizeTriangles(build.Context, vertices, indices, areaIds, indices.Length / 3,
            build.HeightField, config.WalkableClimb);
        RcFilters.FilterLowHangingWalkableObstacles(build.Context, config.WalkableClimb, build.HeightField);
        RcFilters.FilterWalkableLowHeightSpans(build.Context, config.WalkableHeight, build.HeightField);
        RcFilters.FilterLedgeSpans(build.Context, config.WalkableHeight, config.WalkableClimb, build.HeightField);

        build.CompactHeightField = RcCompacts.BuildCompactHeightfield(
            build.Context, config.WalkableHeight, config.WalkableClimb, build.HeightField);
        RcAreas.ErodeWalkableArea(build.Context, config.WalkableRadius, build.CompactHeightField);

        // Mark area volumes
        for (var i = 0; i < build.NavAreas.Count; ++i)
        {
            var area = build.NavAreas[i];
            RcAreas.MarkBoxArea(build.Context, ToArray(area.Bounds.Min), ToArray(area.Bounds.Max),
                new RcAreaModification(area.AreaID), build.CompactHeightField);
        }

        if (build.PartitionType == NavmeshPartitionType.Watershed)
        {
            RcRegions.BuildDistanceField(build.Context, build.CompactHeightField);
            RcRegions.BuildRegions(build.Context, build.CompactHeightField, config.MinRegionArea, config.MergeRegionArea);
        }
        else
            RcRegions.BuildRegionsMonotone(build.Context, build.CompactHeightField, config.MinRegionArea, config.MergeRegionArea);

        return true;
    }

    /// <summary>Build simple navigation mesh tile.</summary>
    protected static bool BuildSimpleTileData(SimpleNavBuildData build)
    {
        var config = build.RecastConfig!;
        build.ContourSet = RcContours.BuildContours(build.Context, build.CompactHeightField!,
            config.MaxSimplificationError, config.MaxEdgeLen, RcBuildContoursFlags.RC_CONTOUR_TESS_WALL_EDGES);
        build.PolyMesh = RcMeshs.BuildPolyMesh(build.Context, build.ContourSet, config.MaxVertsPerPoly);
        build.PolyMeshDetail = RcMeshDetails.BuildPolyMeshDetail(build.Context, build.PolyMesh,
            build.CompactHeightField!, config.DetailSampleDist, config.DetailSampleMaxError);
        if (build.PolyMeshDetail is null)
        {
            Log.GetLogger().Error("Could not build detail mesh");
            return false;
        }

        // Set polygon flags
        /// \todo Assignment of flags from navigation areas?
        for (var i = 0; i < build.PolyMesh.npolys; ++i)
        {
            if (build.PolyMesh.areas[i] != RcRecast.RC_NULL_AREA)
                build.PolyMesh.flags[i] = 0x1;
        }

        var parameters = new DtNavMeshCreateParams
        {
            verts = build.PolyMesh.verts,
            vertCount = build.PolyMesh.nverts,
            polys = build.PolyMesh.polys,
            polyAreas = build.PolyMesh.areas,
            polyFlags = build.PolyMesh.flags,
            polyCount = build.PolyMesh.npolys,
            nvp = build.PolyMesh.nvp,
            detailMeshes = build.PolyMeshDetail.meshes,
            detailVerts = build.PolyMeshDetail.verts,
            detailVertsCount = build.PolyMeshDetail.nverts,
            detailTris = build.PolyMeshDetail.tris,
            detailTriCount = build.PolyMeshDetail.ntris,
            walkableHeight = build.AgentHeight,
            walkableRadius = build.AgentRadius,
            walkableClimb = build.AgentMaxClimb,
            tileX = build.TileIndex.X,
            tileZ = build.TileIndex.Y,
            bmin = build.PolyMesh.bmin,
            bmax = build.PolyMesh.bmax,
            cs = config.Cs,
            ch = config.Ch,
            buildBvTree = true
        };

        // Add off-mesh connections if have them
        if (build.OffMeshRadii.Count != 0)
        {
            parameters.offMeshConCount = build.OffMeshRadii.Count;
            parameters.offMeshConVerts = new float[build.OffMeshVertices.Count * 3];
            for (var i = 0; i < build.OffMeshVertices.Count; ++i)
            {
                parameters.offMeshConVerts[i * 3] = build.OffMeshVertices[i].X;
                parameters.offMeshConVerts[i * 3 + 1] = build.OffMeshVertices[i].Y;
                parameters.offMeshConVerts[i * 3 + 2] = build.OffMeshVertices[i].Z;
            }
            parameters.offMeshConRad = build.OffMeshRadii.ToArray();
            parameters.offMeshConFlags = build.OffMeshFlags.Select(value => (int)value).ToArray();
            parameters.offMeshConAreas = build.OffMeshAreas.Select(value => (int)value).ToArray();
            parameters.offMeshConDir = build.OffMeshDir.Select(value => (int)value).ToArray();
        }

        build.TileData = DtNavMeshBuilder.CreateNavMeshData(parameters);
        if (build.TileData is null)
        {
            Log.GetLogger().Error("Could not build navigation mesh tile data");
            return false;
        }
        return true;
    }

    private bool ReadTile(Deserializer source, bool silent)
    {
        var buffer = NavigationUtils.ReadDetourBuffer(source);
        if (!buffer.IsValid || buffer.Data is null)
        {
            Log.GetLogger().Error("Could not allocate data for navigation mesh tile");
            return false;
        }

        DtMeshData meshData;
        try
        {
            meshData = DeserializeMeshData(buffer.Data);
        }
        catch (IOException)
        {
            Log.GetLogger().Error("Failed to add navigation mesh tile");
            return false;
        }

        if (_navMesh!.AddTile(meshData, DtTileFlags.DT_TILE_FREE_DATA, 0, out _).Failed())
        {
            Log.GetLogger().Error("Failed to add navigation mesh tile");
            return false;
        }

        var tileIndex = new IntVector2(meshData.header.x, meshData.header.y);
        CancelTileBuild(tileIndex);
        if (!silent)
            SendTileAddedEvent(tileIndex);
        return true;
    }

    private void HandleWorldOriginUpdate(ref SceneEvents.WorldOriginUpdateEvent eventData)
    {
        if (_navMesh is null)
            return;
        var tileOffset = NavigationUtils.CalculateTileOffset(eventData.Delta, _tileSize, _cellSize);
        if (tileOffset is null)
            return;
        OffsetTilesGeometry(tileOffset.Value.TileOffset, tileOffset.Value.OffsetY);
    }

    private void OffsetMeshTile(DtMeshData tileData, IntVector2 tileOffset, int offsetY)
    {
        var offset = tileOffset.ToVector2() * (_tileSize * _cellSize);
        var header = tileData.header;
        header.x += tileOffset.X;
        header.y += tileOffset.Y;
        header.bmin.X += offset.X;
        header.bmin.Y += offsetY;
        header.bmin.Z += offset.Y;
        header.bmax.X += offset.X;
        header.bmax.Y += offsetY;
        header.bmax.Z += offset.Y;

        for (var i = 0; i < header.vertCount; ++i)
        {
            tileData.verts[i * 3] += offset.X;
            tileData.verts[i * 3 + 1] += offsetY;
            tileData.verts[i * 3 + 2] += offset.Y;
        }
        for (var i = 0; i < header.detailVertCount; ++i)
        {
            tileData.detailVerts[i * 3] += offset.X;
            tileData.detailVerts[i * 3 + 1] += offsetY;
            tileData.detailVerts[i * 3 + 2] += offset.Y;
        }
        for (var i = 0; i < header.offMeshConCount; ++i)
        {
            for (var j = 0; j < 2; ++j)
            {
                tileData.offMeshCons[i].pos[j].X += offset.X;
                tileData.offMeshCons[i].pos[j].Y += offsetY;
                tileData.offMeshCons[i].pos[j].Z += offset.Y;
            }
        }
    }

    private static byte[] SerializeMeshData(DtMeshData meshData)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        new DtMeshDataWriter().Write(writer, meshData, RcByteOrder.LITTLE_ENDIAN, true);
        writer.Flush();
        return stream.ToArray();
    }

    private static DtMeshData DeserializeMeshData(byte[] data)
    {
        return new DtMeshDataReader().Read(new RcByteBuffer(data), DtDetour.DT_VERTS_PER_POLYGON);
    }

    private static RcVec3f ToRc(Vector3f value) => new(value.X, value.Y, value.Z);
    private static Vector3f FromRc(RcVec3f value) => new(value.X, value.Y, value.Z);
    private static float[] ToArray(Vector3f value) => [value.X, value.Y, value.Z];

    private static Vector3f GetPolygonVertex(DtMeshTile tile, DtPoly poly, int index)
    {
        var vertex = poly.verts[index] * 3;
        return new Vector3f(tile.data.verts[vertex], tile.data.verts[vertex + 1], tile.data.verts[vertex + 2]);
    }

    private static Vector3f GetPolygonCenter(DtMeshTile tile, DtPoly poly)
    {
        var center = Vector3f.Zero;
        for (var i = 0; i < poly.vertCount; ++i)
            center += GetPolygonVertex(tile, poly, i);
        return center / poly.vertCount;
    }

    private static byte[] GetDefaultNavigationData() => [];
    private static NavmeshPartitionType GetDefaultPartitionType() => NavmeshPartitionType.Watershed;

    protected sealed class NavigationQueryFilter : IDtQueryFilter
    {
        private readonly float[] _areaCost = Enumerable.Repeat(1.0f, DtDetour.DT_MAX_AREAS).ToArray();
        private int _includeFlags = 0xffff;
        private int _excludeFlags;

        public bool PassFilter(long refs, DtMeshTile tile, DtPoly poly)
        {
            return (poly.flags & _includeFlags) != 0 && (poly.flags & _excludeFlags) == 0;
        }

        public float GetCost(RcVec3f pa, RcVec3f pb, long previousRef, DtMeshTile previousTile,
            DtPoly previousPoly, long currentRef, DtMeshTile currentTile, DtPoly currentPoly,
            long nextRef, DtMeshTile nextTile, DtPoly nextPoly)
        {
            return RcVec3f.Distance(pa, pb) * _areaCost[currentPoly.GetArea()];
        }

        public void SetAreaCost(int areaID, float cost) => _areaCost[areaID] = cost;
        public float GetAreaCost(int areaID) => _areaCost[areaID];
        public int GetIncludeFlags() => _includeFlags;
        public void SetIncludeFlags(int flags) => _includeFlags = flags;
        public int GetExcludeFlags() => _excludeFlags;
        public void SetExcludeFlags(int flags) => _excludeFlags = flags;
    }
}

public static class Navigation
{
    /// <summary>Register Navigation library objects.</summary>
    public static void RegisterNavigationLibrary(Context context)
    {
        context.RegisterAssembly(typeof(Navigation).Assembly);
    }
}
