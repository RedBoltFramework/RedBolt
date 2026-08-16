// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Graphics;

using Microsoft.Extensions.Logging;
using SystemMath = global::System.Math;

using RedBolt.Core;
using RedBolt.Logging;
using RedBolt.Math;
using RedBolt.RenderAPI;
using RedBolt.Scene;

/// <summary>Individually rendered part of a heightmap terrain.</summary>
[Reflected(InheritAttributes = false)]
public partial class TerrainPatch : Drawable
{
    private static readonly float LodConstant = 1.0f / 150.0f;

    /// <summary>Geometry.</summary>
    private Geometry? _geometry = new();
    /// <summary>Geometry that is locked to the max LOD level. Used for decals.</summary>
    private Geometry? _maxLodGeometry = new();
    /// <summary>Geometry that is used for occlusion.</summary>
    private Geometry? _occlusionGeometry = new();
    /// <summary>Vertex buffer.</summary>
    private VertexBuffer? _vertexBuffer = new();
    /// <summary>Parent terrain.</summary>
    private WeakReference<Terrain>? _owner;
    /// <summary>North neighbor patch.</summary>
    private WeakReference<TerrainPatch>? _north;
    /// <summary>South neighbor patch.</summary>
    private WeakReference<TerrainPatch>? _south;
    /// <summary>West neighbor patch.</summary>
    private WeakReference<TerrainPatch>? _west;
    /// <summary>East neighbor patch.</summary>
    private WeakReference<TerrainPatch>? _east;
    /// <summary>Geometrical error per LOD level.</summary>
    private readonly List<float> _lodErrors = [];
    /// <summary>Patch coordinates in the terrain. (0,0) is the northwest corner.</summary>
    private Vector2i _coordinates;
    /// <summary>Current LOD level.</summary>
    private uint _lodLevel;
    /// <summary>Whether the lightmap is enabled.</summary>
    private bool _bakeLightmap;
    /// <summary>Lightmap index.</summary>
    private uint _lightmapIndex;
    /// <summary>Lightmap scale and offset.</summary>
    private Vector4f _lightmapScaleOffset = new(1.0f, 1.0f, 0.0f, 0.0f);
    /// <summary>Construct.</summary>
    public TerrainPatch()
        : base(DrawableFlags.Geometry)
    {
        _geometry.SetVertexBuffer(0, _vertexBuffer);
        _maxLodGeometry.SetVertexBuffer(0, _vertexBuffer);
        _occlusionGeometry.SetVertexBuffer(0, _vertexBuffer);

        _batches.Add(new SourceBatch
        {
            Geometry = _geometry,
            GeometryType = GeometryType.StaticNoInstancing
        });
    }

    /// <summary>Destruct.</summary>
    public override void Dispose()
    {
        _lodErrors.Clear();
        _east = null;
        _west = null;
        _south = null;
        _north = null;
        _owner = null;
        _vertexBuffer = null;
        _occlusionGeometry = null;
        _maxLodGeometry = null;
        _geometry = null;
        base.Dispose();
    }


    /// <summary>Process octree raycast. May be called from a worker thread.</summary>
    public override void ProcessRayQuery(RayOctreeQuery query, List<RayQueryResult> results)
    {
        switch (query.Level)
        {
            case RayQueryLevel.RayAabb:
                base.ProcessRayQuery(query, results);
                break;

            case RayQueryLevel.RayObb:
            case RayQueryLevel.RayTriangle:
            {
                var worldTransform = Node.GetWorldTransform();
                var inverse = worldTransform.Inverse;
                var localRay = query.Ray.Transformed(inverse);
                var distanceAndNormal = localRay.HitDistanceAndNormal(BoundingBox);
                var distance = distanceAndNormal.Distance;
                var normal = (worldTransform * distanceAndNormal.Normal.ToVector4(0.0f)).Normalized;

                if (query.Level == RayQueryLevel.RayTriangle && distance < query.MaxDistance)
                {
                    distance = _geometry!.GetHitDistance(localRay, out var geometryNormal);
                    normal = (Node.GetWorldTransform() * geometryNormal.ToVector4(0.0f)).Normalized;
                }

                if (distance < query.MaxDistance)
                {
                    results.Add(new RayQueryResult
                    {
                        Position = query.Ray.Origin + distance * query.Ray.Direction,
                        Normal = normal,
                        Distance = distance,
                        Drawable = this,
                        Node = Node,
                        SubObject = MathDefs.MaxUnsigned
                    });
                }
                break;
            }

            case RayQueryLevel.RayTriangleUv:
                Log.GetLogger().LogWarning("RAY_TRIANGLE_UV query level is not supported for TerrainPatch component");
                break;
        }
    }

    /// <summary>Calculate distance and prepare batches for rendering.</summary>
    public override void UpdateBatches(FrameInfo frame)
    {
        _worldTransform[0] = Node.GetWorldTransform();
        SetDistance(frame.Camera!.GetDistance(GetWorldBoundingBox().Center));

        var scale = _worldTransform[0].Scale.DotProduct(GraphicsDefs.DotScale);
        SetLodDistance(frame.Camera.GetLodDistance(GetDistance(), scale, LodBias));

        var batch = _batches[0];
        batch.Distance = GetDistance();
        batch.WorldTransform = _worldTransform;
        _batches[0] = batch;

        uint newLodLevel = 0;
        for (var i = 0; i < _lodErrors.Count; ++i)
        {
            if (_lodErrors[i] / GetLodDistance() > LodConstant)
                break;
            newLodLevel = (uint)i;
        }

        _lodLevel = GetCorrectedLodLevel(newLodLevel);
    }

    /// <summary>Prepare geometry for rendering.</summary>
    public override void UpdateGeometry(FrameInfo frame)
    {
        if (_vertexBuffer!.GetRawBuffer()?.IsDataLost() == true)
        {
            if (_owner is not null && _owner.TryGetTarget(out var owner))
                owner.CreatePatchGeometry(this);
            else
                _vertexBuffer.GetRawBuffer()?.ClearDataLost();
        }

        if (_owner is not null && _owner.TryGetTarget(out var currentOwner))
            currentOwner.UpdatePatchLod(this);
    }

    /// <summary>Return whether a geometry update is necessary.</summary>
    public override UpdateGeometryType GetUpdateGeometryType() =>
        // Because there is a latency in starting worker thread updates, and the update of terrain patch LOD should not take
        // much time, always update in the main thread
        UpdateGeometryType.MainThread;

    /// <summary>Return the geometry for a specific LOD level.</summary>
    public override Geometry? GetLodGeometry(uint batchIndex, uint level) =>
        level == 0 ? _maxLodGeometry : _geometry;

    /// <summary>Return number of occlusion geometry triangles.</summary>
    public override uint GetNumOccluderTriangles()
    {
        // Check that the material is suitable for occlusion (default material always is)
        var mat = _batches[0].Material;
        if (mat is not null && !mat.GetOcclusion())
            return 0;
        return _occlusionGeometry!.GetIndexCount() / 3;
    }

    /// <summary>Draw to occlusion buffer. Return true if did not run out of triangles.</summary>
    public override bool DrawOcclusion(OcclusionBuffer? buffer)
    {
        // Check that the material is suitable for occlusion (default material always is) and set culling mode
        var material = _batches[0].Material;
        if (material is not null)
        {
            if (!material.GetOcclusion())
                return true;
            buffer!.SetCullMode(material.GetCullMode());
        }
        else
            buffer!.SetCullMode(CullMode.CounterClockwise);

        _occlusionGeometry!.GetRawDataShared(out var vertexData, out var vertexSize, out var indexData, out var indexSize, out var elements);
        // Check for valid geometry data
        if (vertexData is null || indexData is null || elements is null ||
            VertexBuffer.GetElementOffset(elements, VertexElementType.Vector3, VertexElementSemantic.Position) != 0)
            return false;

        // Draw and check for running out of triangles
        return buffer.AddTriangles(Node.GetWorldTransform(), vertexData, vertexSize, indexData, indexSize,
            _occlusionGeometry.GetIndexStart(), _occlusionGeometry.GetIndexCount());
    }

    /// <summary>Visualize the component as debug geometry.</summary>
    public override void DrawDebugGeometry(DebugRenderer? debug, bool depthTest)
    {
        base.DrawDebugGeometry(debug, depthTest);

        // TODO: This debug geometry is way too heavy to render
#if false
#endif
    }

    /// <summary>Set owner terrain.</summary>
    public void SetOwner(Terrain? terrain) => _owner = terrain is null ? null : new WeakReference<Terrain>(terrain);

    /// <summary>Set neighbor patches.</summary>
    public void SetNeighbors(TerrainPatch? north, TerrainPatch? south, TerrainPatch? west, TerrainPatch? east)
    {
        _north = north is null ? null : new WeakReference<TerrainPatch>(north);
        _south = south is null ? null : new WeakReference<TerrainPatch>(south);
        _west = west is null ? null : new WeakReference<TerrainPatch>(west);
        _east = east is null ? null : new WeakReference<TerrainPatch>(east);
    }

    /// <summary>Set material.</summary>
    public void SetMaterial(Material? material)
    {
        var batch = _batches[0];
        batch.Material = material;
        _batches[0] = batch;
    }

    /// <summary>Set local-space bounding box.</summary>
    public void SetBoundingBox(BoundingBox box)
    {
        SetLocalBoundingBox(box);
        if (TryGetNode() is not null)
            OnMarkedDirty(Node);
    }

    /// <summary>Set patch coordinates.</summary>
    public void SetCoordinates(Vector2i coordinates) => _coordinates = coordinates;

    /// <summary>Reset to LOD level 0.</summary>
    public void ResetLod() => _lodLevel = 0;

    public Geometry GetGeometry() => _geometry!;
    public Geometry GetMaxLodGeometry() => _maxLodGeometry!;
    public Geometry GetOcclusionGeometry() => _occlusionGeometry!;
    public VertexBuffer GetVertexBuffer() => _vertexBuffer!;
    public Terrain? GetOwner() => _owner is not null && _owner.TryGetTarget(out var owner) ? owner : null;
    public TerrainPatch? GetNorthPatch() => _north is not null && _north.TryGetTarget(out var patch) ? patch : null;
    public TerrainPatch? GetSouthPatch() => _south is not null && _south.TryGetTarget(out var patch) ? patch : null;
    public TerrainPatch? GetWestPatch() => _west is not null && _west.TryGetTarget(out var patch) ? patch : null;
    public TerrainPatch? GetEastPatch() => _east is not null && _east.TryGetTarget(out var patch) ? patch : null;
    public List<float> GetLodErrors() => _lodErrors;
    public Vector2i GetCoordinates() => _coordinates;
    public uint GetLodLevel() => _lodLevel;

    public void SetBakeLightmap(bool bakeLightmap)
    {
        _bakeLightmap = bakeLightmap;
        UpdateBatchesLightmaps();
    }

    public bool GetBakeLightmap() => _bakeLightmap;

    public void SetLightmapIndex(uint idx)
    {
        _lightmapIndex = idx;
        UpdateBatchesLightmaps();
    }

    public uint GetLightmapIndex() => _lightmapIndex;

    public void SetLightmapScaleOffset(Vector4f scaleOffset)
    {
        _lightmapScaleOffset = scaleOffset;
        UpdateBatchesLightmaps();
    }

    public Vector4f GetLightmapScaleOffset() => _lightmapScaleOffset;

    /// <summary>Recalculate the world-space bounding box.</summary>
    protected override void OnWorldBoundingBoxUpdate()
    {
        SetWorldBoundingBox(BoundingBox.Transformed(Node.GetWorldTransform()));
    }

    /// <summary>Return a corrected LOD level to ensure stitching can work correctly.</summary>
    private uint GetCorrectedLodLevel(uint lodLevel)
    {
        if (_north is not null && _north.TryGetTarget(out var north))
            lodLevel = SystemMath.Min(lodLevel, north.GetLodLevel() + 1);
        if (_south is not null && _south.TryGetTarget(out var south))
            lodLevel = SystemMath.Min(lodLevel, south.GetLodLevel() + 1);
        if (_west is not null && _west.TryGetTarget(out var west))
            lodLevel = SystemMath.Min(lodLevel, west.GetLodLevel() + 1);
        if (_east is not null && _east.TryGetTarget(out var east))
            lodLevel = SystemMath.Min(lodLevel, east.GetLodLevel() + 1);
        return lodLevel;
    }

    /// <summary>Update lightmaps in batches.</summary>
    private void UpdateBatchesLightmaps()
    {
        var batch = _batches[0];
        if (_bakeLightmap)
        {
            batch.LightmapIndex = _lightmapIndex;
            batch.LightmapScaleOffset = _lightmapScaleOffset;
        }
        else
        {
            batch.LightmapIndex = 0;
            batch.LightmapScaleOffset = null;
        }
        _batches[0] = batch;
    }
}
