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

using System;
using System.Collections.Generic;

using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Math;
using SceneType = global::RedBolt.Scene.Scene;

namespace RedBolt.Glow;

/// <summary>Used for mapping between geometry ID in geometry buffer and actual geometry.</summary>
public struct GeometryIdToObjectMapping
{
    /// <summary>Index of the object in the array.</summary>
    public int ObjectIndex = int.MaxValue;
    /// <summary>Index of object geometry within the object.</summary>
    public int GeometryIndex = int.MaxValue;
    /// <summary>Index of geometry LOD.</summary>
    public int LodIndex = int.MaxValue;

    public GeometryIdToObjectMapping()
    {
    }

}

/// <summary>Lightmap seam description.</summary>
public struct LightmapSeam : ICloneable
{
    /// <summary>Edge on lightmap UV.</summary>
    public readonly Vector2f[] Positions;
    /// <summary>Other side of the edge on lightmap UV.</summary>
    public readonly Vector2f[] OtherPositions;

    public LightmapSeam()
    {
        Positions = new Vector2f[2];
        OtherPositions = new Vector2f[2];
    }

    /// <summary>Transform by scale and offset.</summary>
    public readonly LightmapSeam Transformed(in Vector2f scale, in Vector2f offset)
    {
        var result = new LightmapSeam();
        result.Positions[0] = Positions[0] * scale + offset;
        result.Positions[1] = Positions[1] * scale + offset;
        result.OtherPositions[0] = OtherPositions[0] * scale + offset;
        result.OtherPositions[1] = OtherPositions[1] * scale + offset;
        return result;
    }

    public readonly LightmapSeam Clone()
    {
        var clone = new LightmapSeam();
        Positions.CopyTo(clone.Positions, 0);
        OtherPositions.CopyTo(clone.OtherPositions, 0);
        return clone;
    }

    readonly object ICloneable.Clone() => Clone();
}

/// <summary>Baking scene for single lightmap chart.</summary>
public struct LightmapGeometryBakingScene : ICloneable
{
    /// <summary>Context.</summary>
    public Context? Context;
    /// <summary>Lightmap chart index.</summary>
    public int Index;
    /// <summary>Size of lightmap chart.</summary>
    public int LightmapSize;
    /// <summary>Baking scene.</summary>
    public SceneType? Scene;
    /// <summary>Baking camera.</summary>
    public Camera? Camera;
    /// <summary>Lightmap seams.</summary>
    public List<LightmapSeam> Seams;

    public LightmapGeometryBakingScene()
    {
        Seams = [];
    }

    public readonly LightmapGeometryBakingScene Clone()
    {
        var clone = this;
        clone.Seams = new List<LightmapSeam>(Seams.Count);
        foreach (var seam in Seams)
            clone.Seams.Add(seam.Clone());
        return clone;
    }

    readonly object ICloneable.Clone() => Clone();
}

/// <summary>Baking scenes for the set of lightmap charts.</summary>
public struct LightmapGeometryBakingScenesArray : ICloneable
{
    /// <summary>Baking scenes.</summary>
    public List<LightmapGeometryBakingScene> BakingScenes;
    /// <summary>Geometry buffer ID to object mapping.</summary>
    public List<GeometryIdToObjectMapping> IdToObject;

    public LightmapGeometryBakingScenesArray()
    {
        BakingScenes = [];
        IdToObject = [];
    }

    public readonly LightmapGeometryBakingScenesArray Clone()
    {
        var clone = this;
        clone.BakingScenes = new List<LightmapGeometryBakingScene>(BakingScenes.Count);
        foreach (var bakingScene in BakingScenes)
            clone.BakingScenes.Add(bakingScene.Clone());
        clone.IdToObject = new List<GeometryIdToObjectMapping>(IdToObject);
        return clone;
    }

    readonly object ICloneable.Clone() => Clone();
}

/// <summary>Lightmap geometry buffer of lightmap chart.</summary>
public struct LightmapChartGeometryBuffer : ICloneable
{
    /// <summary>Lightmap chart index.</summary>
    public int Index;
    /// <summary>Size of lightmap chart.</summary>
    public int LightmapSize;

    /// <summary>Geometry buffer.</summary>
    public List<Vector3f> Positions;
    public List<Vector3f> SmoothPositions; // TODO: Implement
    public List<Vector3f> SmoothNormals;
    public List<Vector3f> FaceNormals;
    public List<int> GeometryIds;
    public List<uint> LightMasks;
    public List<int> BackgroundIds;
    public List<float> TexelRadiuses;
    public List<Vector3f> Albedo;
    public List<Vector3f> Emission;

    /// <summary>Lightmap seams.</summary>
    public List<LightmapSeam> Seams;

    /// <summary>Construct default.</summary>
    public LightmapChartGeometryBuffer()
    {
        Positions = [];
        SmoothPositions = [];
        SmoothNormals = [];
        FaceNormals = [];
        GeometryIds = [];
        LightMasks = [];
        BackgroundIds = [];
        TexelRadiuses = [];
        Albedo = [];
        Emission = [];
        Seams = [];
    }

    /// <summary>Construct valid.</summary>
    public LightmapChartGeometryBuffer(int index, int size)
        : this()
    {
        Index = index;
        LightmapSize = size;
        var numTexels = LightmapSize * LightmapSize;
        for (var i = 0; i < numTexels; ++i)
        {
            Positions.Add(Vector3f.Zero);
            SmoothPositions.Add(Vector3f.Zero);
            SmoothNormals.Add(Vector3f.Zero);
            FaceNormals.Add(Vector3f.Zero);
            GeometryIds.Add(0);
            LightMasks.Add(0);
            BackgroundIds.Add(0);
            TexelRadiuses.Add(0.0f);
            Albedo.Add(Vector3f.Zero);
            Emission.Add(Vector3f.Zero);
        }
    }

    /// <summary>Convert index to location.</summary>
    public readonly Vector2i IndexToLocation(int index)
    {
        return new Vector2i(index % LightmapSize, index / LightmapSize);
    }

    /// <summary>Returns whether the location is valid.</summary>
    public readonly bool IsValidLocation(in Vector2i location)
    {
        return 0 <= location.X && location.X < LightmapSize
            && 0 <= location.Y && location.Y < LightmapSize;
    }

    /// <summary>Convert location to index.</summary>
    public readonly int LocationToIndex(in Vector2i location)
    {
        return location.X + LightmapSize * location.Y;
    }

    public readonly LightmapChartGeometryBuffer Clone()
    {
        var clone = this;
        clone.Positions = new List<Vector3f>(Positions);
        clone.SmoothPositions = new List<Vector3f>(SmoothPositions);
        clone.SmoothNormals = new List<Vector3f>(SmoothNormals);
        clone.FaceNormals = new List<Vector3f>(FaceNormals);
        clone.GeometryIds = new List<int>(GeometryIds);
        clone.LightMasks = new List<uint>(LightMasks);
        clone.BackgroundIds = new List<int>(BackgroundIds);
        clone.TexelRadiuses = new List<float>(TexelRadiuses);
        clone.Albedo = new List<Vector3f>(Albedo);
        clone.Emission = new List<Vector3f>(Emission);
        clone.Seams = new List<LightmapSeam>(Seams.Count);
        foreach (var seam in Seams)
            clone.Seams.Add(seam.Clone());
        return clone;
    }

    readonly object ICloneable.Clone() => Clone();
}
