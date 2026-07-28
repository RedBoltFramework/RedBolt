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

using RedBolt.Math;
using RedBolt.Resource;

/// <summary>Scene background description.</summary>
public struct BakedSceneBackground
{
    public float Intensity;
    public Color Color;
    public ImageCube? Image;

    public BakedSceneBackground()
    {
        Color = new Color();
    }

    public readonly Vector3f SampleLinear(Vector3f direction)
    {
        return Intensity * SampleGammaInternal(direction).GammaToLinear().ToVector3();
    }

    private readonly Color SampleGammaInternal(Vector3f direction)
    {
        return Image is not null ? Image.SampleNearest(direction) : Color;
    }
}
