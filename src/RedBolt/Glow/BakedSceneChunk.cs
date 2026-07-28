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

using RedBolt.Graphics;

/// <summary>Light probe collection with extra data needed for baking.</summary>
public class LightProbeCollectionForBaking : LightProbeCollection, ICloneable
{
    /// <summary>Size is the same as number of probes.</summary>
    public readonly List<uint> LightMasks = [];
    /// <summary>Size is the same as number of probes.</summary>
    public readonly List<uint> BackgroundIds = [];

    private LightProbeCollectionForBaking(LightProbeCollectionForBaking source)
        : base(source)
    {
        LightMasks.AddRange(source.LightMasks);
        BackgroundIds.AddRange(source.BackgroundIds);
    }

    public LightProbeCollectionForBaking()
    {
    }

    public new object Clone() => new LightProbeCollectionForBaking(this);
}
