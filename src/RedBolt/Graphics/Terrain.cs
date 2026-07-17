namespace RedBolt;

/// <summary>
/// Heightmap terrain component.
/// </summary>
/// <remarks>
/// Full C++ Terrain body (heightmap load, patch creation, neighbors, attributes) is ported incrementally.
/// CreatePatchGeometry / UpdatePatchLod are required by TerrainPatch and are implemented to match C++
/// once surrounding heightmap state is available; they currently no-op until SetHeightMap rebuilds state.
/// </remarks>
public class Terrain : Component
{
    // Stitch flags (C++ anonymous enum in Terrain.cpp)
    private const uint StitchNorth = 1;
    private const uint StitchSouth = 2;
    private const uint StitchWest = 4;
    private const uint StitchEast = 8;

    private int _patchSize = 32;
    private Vector3f _spacing = Vector3f.One;
    private Vector2i _numVertices;
    private Vector2i _numPatches;
    private uint _maxLodLevels = 4;
    private uint _occlusionLodLevel = uint.MaxValue;
    private uint _numLodLevels = 1;
    private bool _smoothing;
    private bool _bakeLightmap;
    private readonly List<(uint Start, uint Count)> _drawRanges = [];
    private IndexBuffer? _indexBuffer;
    // // TODO: port remaining C++ Terrain fields and methods (height data, neighbors, materials, etc.)

    /// <summary>Construct.</summary>
    public Terrain()
    {
    }

    /// <summary>Register object factory.</summary>
    public static void RegisterObject(Context context) =>
        context.AddFactoryReflection<Terrain>(ObjectCategory.Category_Geometry);

    /// <summary>
    /// Rebuild the patch geometry when its vertex buffer has been lost.
    /// Full C++ implementation depends on heightmap state; see Terrain.cpp CreatePatchGeometry.
    /// </summary>
    public void CreatePatchGeometry(TerrainPatch patch)
    {
        // // TODO: port full CreatePatchGeometry body once heightmap state is ported
        ArgumentNullException.ThrowIfNull(patch);
        patch.ResetLod();
    }

    /// <summary>
    /// Update a patch's draw range for the current LOD / neighbor stitching.
    /// </summary>
    public void UpdatePatchLod(TerrainPatch patch)
    {
        ArgumentNullException.ThrowIfNull(patch);
        var geometry = patch.GetGeometry();

        // All LOD levels except the coarsest have 16 versions for stitching
        var lodLevel = patch.GetLodLevel();
        var drawRangeIndex = lodLevel << 4;
        if (lodLevel < _numLodLevels - 1)
        {
            var north = patch.GetNorthPatch();
            var south = patch.GetSouthPatch();
            var west = patch.GetWestPatch();
            var east = patch.GetEastPatch();

            if (north is not null && north.GetLodLevel() > lodLevel)
                drawRangeIndex |= StitchNorth;
            if (south is not null && south.GetLodLevel() > lodLevel)
                drawRangeIndex |= StitchSouth;
            if (west is not null && west.GetLodLevel() > lodLevel)
                drawRangeIndex |= StitchWest;
            if (east is not null && east.GetLodLevel() > lodLevel)
                drawRangeIndex |= StitchEast;
        }

        if (drawRangeIndex < (uint)_drawRanges.Count)
        {
            var range = _drawRanges[(int)drawRangeIndex];
            geometry.SetDrawRange(PrimitiveType.TriangleList, range.Start, range.Count, false);
        }
    }

    public int GetPatchSize() => _patchSize;
    public Vector3f GetSpacing() => _spacing;
    public Vector2i GetNumVertices() => _numVertices;
    public Vector2i GetNumPatches() => _numPatches;
    public uint GetMaxLodLevels() => _maxLodLevels;
    public uint GetOcclusionLodLevel() => _occlusionLodLevel;
    public bool GetSmoothing() => _smoothing;
}
