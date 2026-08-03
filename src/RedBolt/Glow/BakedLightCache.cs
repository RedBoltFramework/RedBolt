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

using RedBolt.Math;

/// <summary>Baked lightmap data.</summary>
public struct BakedLightmap : ICloneable
{
    /// <summary>Construct default.</summary>
    public BakedLightmap()
    {
        Lightmap = [];
    }

    /// <summary>Construct.</summary>
    public BakedLightmap(int lightmapSize)
    {
        LightmapSize = lightmapSize;
        Lightmap = new List<Vector3f>(lightmapSize * lightmapSize);
        for (var i = 0; i < lightmapSize * lightmapSize; ++i)
            Lightmap.Add(Vector3f.Zero);
    }

    public readonly BakedLightmap Clone()
    {
        var clone = this;
        clone.Lightmap = new List<Vector3f>(Lightmap);
        return clone;
    }

    readonly object ICloneable.Clone() => Clone();

    /// <summary>Size of lightmap.</summary>
    public int LightmapSize;
    /// <summary>Lightmap data, in linear space.</summary>
    public List<Vector3f> Lightmap;
}

/// <summary>Lightmap cache interface.</summary>
public abstract class BakedLightCache : IDisposable
{
    public abstract void Dispose();
    /// <summary>Store baked scene chunk in the cache.</summary>
    public abstract void StoreBakedChunk(Vector3i chunk, BakedSceneChunk bakedChunk);
    /// <summary>Load baked scene chunk.</summary>
    public abstract BakedSceneChunk? LoadBakedChunk(Vector3i chunk);

    /// <summary>Store direct light for the lightmap chart.</summary>
    public abstract void StoreDirectLight(int lightmapIndex, LightmapChartBakedDirect bakedDirect);
    /// <summary>Load direct light for the lightmap chart.</summary>
    public abstract LightmapChartBakedDirect? LoadDirectLight(int lightmapIndex);

    /// <summary>Store baked lightmap.</summary>
    public abstract void StoreLightmap(int lightmapIndex, BakedLightmap bakedLightmap);
    /// <summary>Load baked lightmap.</summary>
    public abstract BakedLightmap? LoadLightmap(int lightmapIndex);
}

/// <summary>Memory lightmap cache.</summary>
public sealed class BakedLightMemoryCache : BakedLightCache
{
    private readonly Dictionary<Vector3i, BakedSceneChunk> _bakedChunks = [];
    private readonly Dictionary<int, LightmapChartBakedDirect> _directLight = [];
    private readonly Dictionary<int, BakedLightmap> _lightmaps = [];

    public override void Dispose()
    {
        foreach (var chunk in _bakedChunks.Values)
            chunk.Dispose();
        _bakedChunks.Clear();
        _directLight.Clear();
        _lightmaps.Clear();
    }

    public override void StoreBakedChunk(Vector3i chunk, BakedSceneChunk bakedChunk)
    {
        if (_bakedChunks.TryGetValue(chunk, out var previous))
            previous.Dispose();
        _bakedChunks[chunk] = bakedChunk;
    }

    public override BakedSceneChunk? LoadBakedChunk(Vector3i chunk) =>
        _bakedChunks.TryGetValue(chunk, out var value) ? value : null;

    public override void StoreDirectLight(int lightmapIndex, LightmapChartBakedDirect bakedDirect) =>
        _directLight[lightmapIndex] = bakedDirect;

    public override LightmapChartBakedDirect? LoadDirectLight(int lightmapIndex) =>
        _directLight.TryGetValue(lightmapIndex, out var value) ? value : null;

    public override void StoreLightmap(int lightmapIndex, BakedLightmap bakedLightmap) =>
        _lightmaps[lightmapIndex] = bakedLightmap;

    public override BakedLightmap? LoadLightmap(int lightmapIndex) =>
        _lightmaps.TryGetValue(lightmapIndex, out var value) ? value : null;
}
