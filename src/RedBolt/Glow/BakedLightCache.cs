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
