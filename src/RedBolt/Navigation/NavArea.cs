//
// Copyright (c) 2008-2022 the Urho3D project.
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

namespace RedBolt.Navigation;

using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Logging;
using RedBolt.Math;
using RedBolt.Scene;

[Reflected(ObjectCategory.Category_Navigation, InheritAttributes = false)]
public partial class NavArea : Component
{
    private const int MaxNavAreaId = 255;
    private static readonly Vector3f DefaultBoundingBoxMin = new(-10.0f, -10.0f, -10.0f);
    private static readonly Vector3f DefaultBoundingBoxMax = new(10.0f, 10.0f, 10.0f);
    private const int DefaultAreaId = 0;

    private BoundingBox _boundingBox = new(DefaultBoundingBoxMin, DefaultBoundingBoxMax);
    private byte _areaID = DefaultAreaId;

    /// <summary>Construct.</summary>
    public NavArea()
    {
    }

    /// <summary>Destruct.</summary>
    public override void Dispose()
    {
        base.Dispose();
    }

    /// <summary>Render debug geometry for the bounds.</summary>
    public override void DrawDebugGeometry(DebugRenderer? debug, bool depthTest)
    {
        if (debug is not null && IsEnabledEffective)
        {
            var transform = Matrix3x4.FromTranslation(Node.GetWorldPosition());
            debug.AddBoundingBox(_boundingBox, transform, Color.Green, depthTest);
            debug.AddBoundingBox(_boundingBox, transform, new Color(0.0f, 1.0f, 0.0f, 0.15f), true, true);
        }
    }

    /// <summary>Get or set the area id for this volume.</summary>
    [Serialized("Area ID", DefaultSource = nameof(GetDefaultAreaId))]
    public int AreaID
    {
        get => _areaID;
        set
        {
            if (value > MaxNavAreaId)
                Log.GetLogger().Error("NavArea Area ID {AreaID} exceeds maximum value of {MaximumAreaID}", value, MaxNavAreaId);
            _areaID = unchecked((byte)value);
        }
    }

    /// <summary>Get or set the bounding box of this navigation area, in local space.</summary>
    public BoundingBox BoundingBox
    {
        get => _boundingBox;
        set => _boundingBox = value;
    }

    /// <summary>Get the bounds of this navigation area in world space.</summary>
    public BoundingBox WorldBoundingBox => _boundingBox.Transformed(Matrix3x4.FromTranslation(Node.GetWorldPosition()));

    private static Vector3f GetDefaultBoundingBoxMin() => DefaultBoundingBoxMin;
    private static Vector3f GetDefaultBoundingBoxMax() => DefaultBoundingBoxMax;
    private static int GetDefaultAreaId() => DefaultAreaId;
    [Serialized("Bounding Box Min", DefaultSource = nameof(GetDefaultBoundingBoxMin), Setter = nameof(SetBoundingBoxMin))]
    private Vector3f GetBoundingBoxMin() => _boundingBox.Min;
    [Serialized("Bounding Box Max", DefaultSource = nameof(GetDefaultBoundingBoxMax), Setter = nameof(SetBoundingBoxMax))]
    private Vector3f GetBoundingBoxMax() => _boundingBox.Max;
    private void SetBoundingBoxMin(Vector3f value) => _boundingBox.Min = value;
    private void SetBoundingBoxMax(Vector3f value) => _boundingBox.Max = value;
}
