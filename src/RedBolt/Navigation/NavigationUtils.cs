// Copyright (c) 2024-2024 the rbfx project.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

using RedBolt.IO;
using RedBolt.Logging;
using RedBolt.Math;
using RedBolt.Scene;

namespace RedBolt.Navigation;

public struct NavigationGeometryInfo
{
    /// <summary>Geometry component.</summary>
    public Component? Component;
    /// <summary>Geometry LOD level if applicable.</summary>
    public uint LodLevel;
    /// <summary>Transform relative to the navigation mesh root node.</summary>
    public Matrix3x4 Transform;
    /// <summary>Bounding box relative to the navigation mesh root node.</summary>
    public BoundingBox BoundingBox;
    /// <summary>Area ID.</summary>
    public byte AreaId;
}

public static class NavigationUtils
{
    public static DetourAllocation ReadDetourBuffer(Deserializer source)
    {
        var dataSize = source.ReadInt();
        var data = new byte[dataSize];
        source.Read(data);
        return new DetourAllocation(data);
    }

    public static void WriteDetourBuffer(Serializer destination, DetourAllocation buffer)
    {
        WriteDetourBuffer(destination, buffer.Data.AsSpan(0, buffer.DataSize));
    }

    public static void WriteDetourBuffer(Serializer destination, ReadOnlySpan<byte> buffer)
    {
        destination.WriteInt(buffer.Length);
        if (buffer.Length != 0)
            destination.Write(buffer);
    }

    /// <summary>Calculate tile offset.</summary>
    public static (IntVector2 TileOffset, int OffsetY)? CalculateTileOffset(IntVector3 delta, int tileSize, float cellSize)
    {
        var tileEdgeLength = MathDefs.RoundToInt(tileSize * cellSize);
        if (tileEdgeLength <= 0 || !MathDefs.Equals(tileSize * cellSize, tileEdgeLength))
        {
            Log.GetLogger().Error("Tile width should be integer for NavigationMesh rebase: {TileSize} * {CellSize} = {Width}",
                tileSize, cellSize, tileSize * cellSize);
            return null;
        }

        if (delta.X % tileEdgeLength != 0 || delta.Z % tileEdgeLength != 0)
        {
            Log.GetLogger().Error("Delta must be a multiple of navigation mesh tile width for NavigationMesh rebase: {Delta}/{Width}",
                delta, tileEdgeLength);
            return null;
        }

        var tileDelta = delta / tileEdgeLength;
        return (new IntVector2(tileDelta.X, tileDelta.Z), delta.Y);
    }

    /// <summary>Calculate bounding box of given geometry.</summary>
    public static BoundingBox CalculateBoundingBox(IReadOnlyList<NavigationGeometryInfo> geometryList, Vector3f padding)
    {
        var result = new BoundingBox();
        for (var i = 0; i < geometryList.Count; ++i)
            result.Merge(geometryList[i].BoundingBox);

        result.Min -= padding;
        result.Max += padding;

        return result;
    }

    /// <summary>Calculate bounding box of given geometry within the given tile volume.</summary>
    public static BoundingBox CalculateTileBoundingBox(
        IReadOnlyList<NavigationGeometryInfo> geometryList, BoundingBox tileColumn)
    {
        var result = tileColumn;
        result.Min.Y = MathDefs.LargeValue;
        result.Max.Y = -MathDefs.LargeValue;

        foreach (var info in geometryList)
        {
            if (info.BoundingBox.IsInside(tileColumn) == Intersection.Outside)
                continue;

            result.Min.Y = MathF.Min(result.Min.Y, info.BoundingBox.Min.Y);
            result.Max.Y = MathF.Max(result.Max.Y, info.BoundingBox.Max.Y);
        }

        // Repair empty bounding box
        if (result.Max.Y < result.Min.Y)
        {
            result.Min.Y = 0.0f;
            result.Max.Y = 0.0f;
        }

        return result;
    }

    /// <summary>Calculate maximum number of tiles required to contain the given bounding box.</summary>
    public static int CalculateMaxTiles(BoundingBox boundingBox, int tileSize, float cellSize)
    {
        if (!boundingBox.Defined)
            return 0;

        var tileEdgeLength = tileSize * cellSize;
        var beginTileIndex = Vector2f.FloorToInt(boundingBox.Min.ToXZ() / tileEdgeLength);
        var endTileIndex = Vector2f.FloorToInt(boundingBox.Max.ToXZ() / tileEdgeLength);
        var numTiles = endTileIndex - beginTileIndex + IntVector2.One;

        return checked((int)MathDefs.NextPowerOfTwo(checked((uint)(numTiles.X * numTiles.Y))));
    }

    /// <summary>Deduce area ID when applicable for walkable triangles.</summary>
    public static void DeduceAreaIds(
        float walkableSlopeAngle, ReadOnlySpan<float> vertices, ReadOnlySpan<int> triangles, int numTriangles, Span<byte> areas)
    {
        const byte walkableArea = 63;
        var walkableThreshold = MathF.Cos(walkableSlopeAngle / 180.0f * MathF.PI);
        for (var i = 0; i < numTriangles; ++i)
        {
            if (areas[i] != NavigationDefs.DeduceAreaId)
                continue;

            var triangle = triangles.Slice(i * 3, 3);
            var vertexIndex0 = triangle[0] * 3;
            var vertexIndex1 = triangle[1] * 3;
            var vertexIndex2 = triangle[2] * 3;
            var vertex0 = new Vector3f(vertices[vertexIndex0], vertices[vertexIndex0 + 1], vertices[vertexIndex0 + 2]);
            var vertex1 = new Vector3f(vertices[vertexIndex1], vertices[vertexIndex1 + 1], vertices[vertexIndex1 + 2]);
            var vertex2 = new Vector3f(vertices[vertexIndex2], vertices[vertexIndex2 + 1], vertices[vertexIndex2 + 2]);
            var normal = (vertex1 - vertex0).CrossProduct(vertex2 - vertex0).Normalized;
            areas[i] = normal.Y > walkableThreshold ? walkableArea : (byte)0;
        }
    }
}
