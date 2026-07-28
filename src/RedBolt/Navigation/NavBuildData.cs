//
// Copyright (c) 2008-2022 the Urho3D project.
// Copyright (c) 2023-2025 the rbfx project.
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

using DotRecast.Core;
using DotRecast.Detour;
using DotRecast.Recast;

using RedBolt.Math;

namespace RedBolt.Navigation;

/// <summary>Navigation area stub.</summary>
public struct NavAreaStub
{
    /// <summary>Area bounding box.</summary>
    public BoundingBox Bounds;
    /// <summary>Area ID.</summary>
    public byte AreaID;
}

public sealed class DetourAllocation
{
    public DetourAllocation()
    {
    }

    public DetourAllocation(byte[] data)
    {
        Data = data;
        DataSize = data.Length;
    }

    public byte[]? Data { get; private set; }
    public int DataSize { get; private set; }

    public void Release()
    {
        Data = null;
        DataSize = 0;
    }

    public bool IsValid => DataSize != 0;

    public Span<byte> ToByteSpan() => Data;
}

/// <summary>Navigation build data.</summary>
public class NavBuildData : IDisposable
{
    public NavBuildData()
    {
        Context = new RcContext();
    }

    public virtual void Dispose()
    {
        CompactHeightField = null;
        HeightField = null;
    }

    /// <summary>Return whether the tile is empty and there is nothing to build.</summary>
    public bool IsEmpty => Vertices.Count == 0 || Indices.Count == 0;

    /// <summary>Tile index.</summary>
    public IntVector2 TileIndex;
    /// <summary>Volume of the tile, ignoring geometry.</summary>
    public BoundingBox TileColumn;
    /// <summary>Bounding box containing all geometry in the tile.</summary>
    public BoundingBox TileBoundingBox;
    /// <summary>Extended bounding box containing all geometry important for tile building.</summary>
    public BoundingBox CollectGeometryBoundingBox;

    /// <summary>Partition type.</summary>
    public NavmeshPartitionType PartitionType;
    /// <summary>Navigation agent height.</summary>
    public float AgentHeight;
    /// <summary>Navigation agent radius.</summary>
    public float AgentRadius;
    /// <summary>Navigation agent max vertical climb.</summary>
    public float AgentMaxClimb;
    /// <summary>Maximum number of links per portal when creating nav mesh tiles.</summary>
    public int PortalLinksCount;

    /// <summary>Recast configuration for building.</summary>
    public RcConfig? RecastConfig;

    /// <summary>Vertices from geometries.</summary>
    public readonly List<Vector3f> Vertices = [];
    /// <summary>Triangle indices from geometries.</summary>
    public readonly List<int> Indices = [];
    /// <summary>Triangle area IDs.</summary>
    public readonly List<byte> AreaIds = [];
    /// <summary>Offmesh connection vertices.</summary>
    public readonly List<Vector3f> OffMeshVertices = [];
    /// <summary>Offmesh connection radii.</summary>
    public readonly List<float> OffMeshRadii = [];
    /// <summary>Offmesh connection flags.</summary>
    public readonly List<ushort> OffMeshFlags = [];
    /// <summary>Offmesh connection areas.</summary>
    public readonly List<byte> OffMeshAreas = [];
    /// <summary>Offmesh connection direction.</summary>
    public readonly List<byte> OffMeshDir = [];
    /// <summary>Recast context.</summary>
    public RcContext Context;
    /// <summary>Recast heightfield.</summary>
    public RcHeightfield? HeightField;
    /// <summary>Recast compact heightfield.</summary>
    public RcCompactHeightfield? CompactHeightField;
    /// <summary>Pretransformed navigation areas, no correlation to the geometry above.</summary>
    public readonly List<NavAreaStub> NavAreas = [];

    /// <summary>Offset to be applied to the tile when it's done building.</summary>
    public IntVector2 PendingTileOffset;
    public int PendingOffsetY;
}

public sealed class SimpleNavBuildData : NavBuildData
{
    /// <summary>Constructor.</summary>
    public SimpleNavBuildData()
    {
    }

    /// <summary>Descturctor.</summary>
    public override void Dispose()
    {
        PolyMeshDetail = null;
        PolyMesh = null;
        ContourSet = null;
        TileData = null;
        base.Dispose();
    }

    /// <summary>Recast contour set.</summary>
    public RcContourSet? ContourSet;
    /// <summary>Recast poly mesh.</summary>
    public RcPolyMesh? PolyMesh;
    /// <summary>Recast detail poly mesh.</summary>
    public RcPolyMeshDetail? PolyMeshDetail;

    /// <summary>Compiled navigation mesh tile.</summary>
    public DtMeshData? TileData;
}

public sealed class DynamicNavBuildData : NavBuildData
{
    /// <summary>Constructor.</summary>
    public DynamicNavBuildData()
    {
    }

    /// <summary>Destructor.</summary>
    public override void Dispose()
    {
        HeightFieldLayers = null;
        TileData.Clear();
        base.Dispose();
    }

    /// <summary>Recast heightfield layer set.</summary>
    public RcHeightfieldLayerSet? HeightFieldLayers;

    /// <summary>Compiled navigation mesh tiles.</summary>
    public readonly List<byte[]> TileData = [];
}
