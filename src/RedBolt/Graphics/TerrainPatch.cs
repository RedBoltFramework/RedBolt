using Microsoft.Extensions.Logging;

namespace RedBolt;

/// <summary>Individually rendered part of a heightmap terrain.</summary>
public class TerrainPatch : Drawable
{
    private static readonly float LodConstant = 1.0f / 150.0f;

    /// <summary>Geometry.</summary>
    private readonly Geometry _geometry = new();
    /// <summary>Geometry that is locked to the max LOD level. Used for decals.</summary>
    private readonly Geometry _maxLodGeometry = new();
    /// <summary>Geometry that is used for occlusion.</summary>
    private readonly Geometry _occlusionGeometry = new();
    /// <summary>Vertex buffer.</summary>
    private readonly VertexBuffer _vertexBuffer = new();
    /// <summary>Parent terrain.</summary>
    private Terrain? _owner;
    /// <summary>North neighbor patch.</summary>
    private TerrainPatch? _north;
    /// <summary>South neighbor patch.</summary>
    private TerrainPatch? _south;
    /// <summary>West neighbor patch.</summary>
    private TerrainPatch? _west;
    /// <summary>East neighbor patch.</summary>
    private TerrainPatch? _east;
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
    private readonly Matrix3x4[] _worldTransform = [Matrix3x4.Identity];

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

    /// <summary>Register object factory.</summary>
    public static void RegisterObject(Context context) => context.AddFactoryReflection<TerrainPatch>();

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
                    distance = _geometry.GetHitDistance(localRay, out var geometryNormal);
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
                Log.Logger.LogWarning("RAY_TRIANGLE_UV query level is not supported for TerrainPatch component");
                break;
        }
    }

    /// <summary>Calculate distance and prepare batches for rendering.</summary>
    public override void UpdateBatches(CommonFrameInfo frame)
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
    public override void UpdateGeometry(CommonFrameInfo frame)
    {
        if (_vertexBuffer.GetRawBuffer()?.IsDataLost() == true)
        {
            if (_owner is not null)
                _owner.CreatePatchGeometry(this);
            else
                _vertexBuffer.GetRawBuffer()?.ClearDataLost();
        }

        _owner?.UpdatePatchLod(this);
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
        return _occlusionGeometry.GetIndexCount() / 3;
    }

    /// <summary>Draw to occlusion buffer. Return true if did not run out of triangles.</summary>
    public override bool DrawOcclusion(OcclusionBuffer? buffer)
    {
        if (buffer is null)
            return true;

        // Check that the material is suitable for occlusion (default material always is) and set culling mode
        var material = _batches[0].Material;
        if (material is not null)
        {
            if (!material.GetOcclusion())
                return true;
            buffer.SetCullMode(material.GetCullMode());
        }
        else
            buffer.SetCullMode(CullMode.CounterClockwise);

        _occlusionGeometry.GetRawDataShared(out var vertexData, out var vertexSize, out var indexData, out var indexSize, out var elements);
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
    public void SetOwner(Terrain? terrain) => _owner = terrain;

    /// <summary>Set neighbor patches.</summary>
    public void SetNeighbors(TerrainPatch? north, TerrainPatch? south, TerrainPatch? west, TerrainPatch? east)
    {
        _north = north;
        _south = south;
        _west = west;
        _east = east;
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
        if (IsAttached)
            OnMarkedDirty(Node);
    }

    /// <summary>Set patch coordinates.</summary>
    public void SetCoordinates(Vector2i coordinates) => _coordinates = coordinates;

    /// <summary>Reset to LOD level 0.</summary>
    public void ResetLod() => _lodLevel = 0;

    public Geometry GetGeometry() => _geometry;
    public Geometry GetMaxLodGeometry() => _maxLodGeometry;
    public Geometry GetOcclusionGeometry() => _occlusionGeometry;
    public VertexBuffer GetVertexBuffer() => _vertexBuffer;
    public Terrain? GetOwner() => _owner;
    public TerrainPatch? GetNorthPatch() => _north;
    public TerrainPatch? GetSouthPatch() => _south;
    public TerrainPatch? GetWestPatch() => _west;
    public TerrainPatch? GetEastPatch() => _east;
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
        if (_north is not null)
            lodLevel = Math.Min(lodLevel, _north.GetLodLevel() + 1);
        if (_south is not null)
            lodLevel = Math.Min(lodLevel, _south.GetLodLevel() + 1);
        if (_west is not null)
            lodLevel = Math.Min(lodLevel, _west.GetLodLevel() + 1);
        if (_east is not null)
            lodLevel = Math.Min(lodLevel, _east.GetLodLevel() + 1);
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
