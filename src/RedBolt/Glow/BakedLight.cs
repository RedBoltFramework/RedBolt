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

using RedBolt.Graphics;
using RedBolt.Math;

namespace RedBolt.Glow;

/// <summary>Baked light description.</summary>
public struct BakedLight
{
    /// <summary>Construct default.</summary>
    public BakedLight()
    {
    }

    /// <summary>Construct valid.</summary>
    public BakedLight(Light light)
    {
        LightType = light.GetLightType();
        LightMode = light.GetLightMode();
        LightMask = light.GetLightMask();
        Color = light.GetEffectiveColor().GammaToLinear();
        IndirectBrightness = light.GetIndirectBrightness();
        Fov = light.GetFov();
        Cutoff = MathDefs.Cos(Fov * 0.5f);
        Distance = light.GetRange();
        Radius = LightType != LightType.Directional ? light.GetRadius() : 0.0f;
        Angle = LightType == LightType.Directional ? light.GetRadius() : 0.0f;
        HalfAngleTan = MathDefs.Tan(Angle / 2.0f);
        Position = light.Node!.GetWorldPosition();
        Direction = light.Node!.GetWorldDirection();
        Rotation = light.Node!.GetWorldRotation();
    }

    /// <summary>Light type.</summary>
    public LightType LightType;
    /// <summary>Light mode.</summary>
    public LightMode LightMode;
    public uint LightMask;
    /// <summary>Light color.</summary>
    public Color Color;
    /// <summary>Indirect brightness.</summary>
    public float IndirectBrightness;
    /// <summary>FOV angle (for spot lights).</summary>
    public float Fov;
    /// <summary>Cutoff aka Cos(FOV * 0.5) (for spot lights).</summary>
    public float Cutoff;
    /// <summary>Light distance (for spot and point lights).</summary>
    public float Distance;
    /// <summary>Light radius (for spot and point lights).</summary>
    public float Radius;
    /// <summary>Light angle (for directional light).</summary>
    public float Angle;
    /// <summary>Tangent of half light angle.</summary>
    public float HalfAngleTan;
    /// <summary>Position.</summary>
    public Vector3f Position;
    /// <summary>Direction.</summary>
    public Vector3f Direction;
    /// <summary>Rotation.</summary>
    public Quaternion Rotation;
}
