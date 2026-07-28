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

using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.Scene;

namespace RedBolt.Navigation;

/// <summary>A link between otherwise unconnected regions of the navigation mesh.</summary>
[Reflected(ObjectCategory.Category_Navigation, InheritAttributes = false)]
public partial class OffMeshConnection : Component
{
    private const float DefaultRadius = 1.0f;
    private const uint DefaultMaskFlag = 1;
    private const uint DefaultArea = 1;

    private WeakReference<Node>? _endPoint;
    [Serialized(
        "Endpoint NodeID",
        Mode = AttributeMode.Default | AttributeMode.NodeId,
        Setter = nameof(SetEndPointID),
        Order = 1
    )]
    private int _endPointID;
    private float _radius = DefaultRadius;
    private bool _bidirectional = true;
    private bool _endPointDirty;
    private uint _mask = DefaultMaskFlag;
    private uint _areaId = DefaultArea;

    /// <summary>Construct.</summary>
    public OffMeshConnection()
    {
    }

    /// <summary>Destruct.</summary>
    public override void Dispose()
    {
        _endPoint = null;
        base.Dispose();
    }

    /// <summary>Apply attribute changes that can not be applied immediately. Called after scene load or a network update.</summary>
    public override void ApplyAttributes()
    {
        if (_endPointDirty)
        {
            var scene = Scene;
            var endPoint = scene?.GetNode(unchecked((uint)_endPointID));
            _endPoint = endPoint is not null ? new WeakReference<Node>(endPoint) : null;
            _endPointDirty = false;
        }
    }

    /// <summary>Visualize the component as debug geometry.</summary>
    public override void DrawDebugGeometry(DebugRenderer? debug, bool depthTest)
    {
        if (_node is null || _endPoint is null || !_endPoint.TryGetTarget(out var endPoint))
            return;

        var start = _node.GetWorldPosition();
        var end = endPoint.GetWorldPosition();

        debug!.AddSphere(new Sphere(start, _radius), Color.White, depthTest);
        debug.AddSphere(new Sphere(end, _radius), Color.White, depthTest);
        debug.AddLine(start, end, Color.White, depthTest);
    }

    /// <summary>Set endpoint node.</summary>
    /// <summary>Return endpoint node.</summary>
    public Node? EndPoint
    {
        get => _endPoint is not null && _endPoint.TryGetTarget(out var endPoint) ? endPoint : null;
        set
        {
            _endPoint = value is not null ? new WeakReference<Node>(value) : null;
            _endPointID = value is not null ? checked((int)value.Id) : 0;
        }
    }

    /// <summary>Set radius.</summary>
    /// <summary>Return radius.</summary>
    [Serialized(
        "Radius",
        Order = 1001
    )]
    public float Radius
    {
        get => _radius;
        set => _radius = value;
    }

    /// <summary>Set bidirectional flag. Default true.</summary>
    /// <summary>Return whether is bidirectional.</summary>
    [Serialized(
        "Bidirectional",
        Order = 1002
    )]
    public bool Bidirectional
    {
        get => _bidirectional;
        set => _bidirectional = value;
    }

    /// <summary>Set a user assigned mask.</summary>
    /// <summary>Return the user assigned mask.</summary>
    [Serialized(
        "Flags Mask",
        Order = 1003
    )]
    public uint Mask
    {
        get => _mask;
        set => _mask = value;
    }

    /// <summary>Sets the assigned area Id for the connection.</summary>
    /// <summary>Return the user assigned area ID.</summary>
    [Serialized(
        "Area Type",
        Order = 1004
    )]
    public uint AreaID
    {
        get => _areaId;
        set => _areaId = value;
    }

    /// <summary>Mark end point dirty.</summary>
    private void MarkEndPointDirty()
    {
        _endPointDirty = true;
    }

    private static bool GetEnabledDefault() => true;
    private static int GetEndPointIDDefault() => 0;
    private static float GetRadiusDefault() => DefaultRadius;
    private static bool GetBidirectionalDefault() => true;
    private static uint GetMaskDefault() => DefaultMaskFlag;
    private static uint GetAreaIdDefault() => DefaultArea;
    [Serialized(
        "Is Enabled",
        DefaultSource = nameof(GetEnabledDefault),
        Setter = nameof(SetEnabled),
        Order = 0
    )]
    private bool GetEnabled() => Enabled;
    private int GetEndPointID() => _endPointID;
    private void SetEndPointID(int value)
    {
        _endPointID = value;
        MarkEndPointDirty();
    }
}
