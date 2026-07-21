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

namespace RedBolt;

/// <summary>Spline for creating smooth movement based on Speed along a set of Control Points modified by the Interpolation Mode.</summary>
public class SplinePath : Component
{
    private static readonly StringVector ControlPointsStructureElementNames = new()
    {
        "Control Point Count",
        "   NodeID"
    };

    /// <summary>The Control Points of the Spline.</summary>
    private readonly Spline _spline = new(InterpolationMode.BezierCurve);
    /// <summary>The Speed of movement along the Spline.</summary>
    private float _speed = 1.0f;
    /// <summary>Amount of time that has elapsed while moving.</summary>
    private float _elapsedTime;
    /// <summary>The fraction of the SplinePath covered.</summary>
    private float _traveled;
    /// <summary>The length of the SplinePath.</summary>
    private float _length;
    /// <summary>Whether the Control Point IDs are dirty.</summary>
    private bool _dirty;
    /// <summary>Node to be moved along the SplinePath.</summary>
    private WeakReference<Node>? _controlledNode;
    /// <summary>Control Points for the SplinePath.</summary>
    private readonly List<WeakReference<Node>> _controlPoints = [];
    /// <summary>Control Point ID's for the SplinePath.</summary>
    private readonly VariantVector _controlPointIdsAttr = [];
    /// <summary>Controlled ID for the SplinePath.</summary>
    private uint _controlledIdAttr;

    /// <summary>Construct an Empty SplinePath.</summary>
    public SplinePath()
    {
        RegisterAttribute(
            "Interpolation Mode",
            typeof(InterpolationMode),
            self => ((SplinePath)self).InterpolationMode,
            (self, value) => ((SplinePath)self).InterpolationMode = value is InterpolationMode typed ? typed : InterpolationMode.BezierCurve,
            InterpolationMode.BezierCurve);
        RegisterAttribute(
            "Speed",
            typeof(float),
            self => ((SplinePath)self).Speed,
            (self, value) => ((SplinePath)self).Speed = value is float typed ? typed : 1.0f,
            1.0f);
        RegisterAttribute(
            "Traveled",
            typeof(float),
            self => ((SplinePath)self)._traveled,
            (self, value) => ((SplinePath)self)._traveled = value is float typed ? typed : 0.0f,
            0.0f,
            AttributeMode.Default | AttributeMode.NoEdit);
        RegisterAttribute(
            "Elapsed Time",
            typeof(float),
            self => ((SplinePath)self)._elapsedTime,
            (self, value) => ((SplinePath)self)._elapsedTime = value is float typed ? typed : 0.0f,
            0.0f,
            AttributeMode.Default | AttributeMode.NoEdit);
        RegisterAttribute(
            "Controlled",
            typeof(uint),
            self => ((SplinePath)self).ControlledIdAttr,
            (self, value) => ((SplinePath)self).ControlledIdAttr = value is uint typed ? typed : 0u,
            0u,
            AttributeMode.Default | AttributeMode.NodeId);
        using (var metadata = new Variant(ControlPointsStructureElementNames))
        {
            RegisterAttribute(
                "Control Points",
                typeof(VariantVector),
                self => ((SplinePath)self).ControlPointIdsAttr,
                (self, value) => ((SplinePath)self).ControlPointIdsAttr = (VariantVector)value!,
                new VariantVector(),
                AttributeMode.Default | AttributeMode.NodeIdVector)
                .SetMetadata(AttributeMetadata.VectorStructElements, metadata);
        }

        UpdateNodeIds();
    }

    /// <summary>Destructor.</summary>
    public override void Dispose()
    {
        for (var i = _controlPoints.Count - 1; i >= 0; --i)
        {
            if (_controlPoints[i].TryGetTarget(out var point))
                point.RemoveListener(this);
        }

        _controlPointIdsAttr.Dispose();
        _controlPoints.Clear();
        _controlledNode = null;
        _spline.Dispose();
        base.Dispose();
    }

    /// <summary>Register object factory.</summary>
    public static void RegisterObject(Context context)
    {
        context.AddFactoryReflection<SplinePath>(ObjectCategory.Category_Logic);
    }

    /// <summary>Apply Attributes to the SplinePath.</summary>
    public override void ApplyAttributes()
    {
        if (!_dirty)
            return;

        // Remove all old instance nodes before searching for new. Can not call RemoveAllInstances() as that would modify
        // the ID list on its own
        for (var i = 0; i < _controlPoints.Count; ++i)
        {
            if (_controlPoints[i].TryGetTarget(out var point))
                point.RemoveListener(this);
        }

        _controlPoints.Clear();
        _spline.Clear();

        var scene = Scene;

        if (scene is not null)
        {
            // The first index stores the number of IDs redundantly. This is for editing
            for (var i = 1; i < _controlPointIdsAttr.Count; ++i)
            {
                var point = scene.GetNode(_controlPointIdsAttr[i].GetUInt());
                if (point is not null)
                {
                    point.AddListener(this);
                    _controlPoints.Add(new WeakReference<Node>(point));
                    using var knot = new Variant(point.GetWorldPosition());
                    _spline.AddKnot(knot);
                }
            }

            var controlled = scene.GetNode(_controlledIdAttr);
            if (controlled is not null)
                _controlledNode = new WeakReference<Node>(controlled);
        }

        CalculateLength();
        _dirty = false;
    }

    /// <summary>Draw the Debug Geometry.</summary>
    public override void DrawDebugGeometry(DebugRenderer? debug, bool depthTest)
    {
        if (debug is not null && Node is not null && IsEnabledEffective)
        {
            if (_spline.GetKnots().Count > 1)
            {
                using var firstPoint = _spline.GetPoint(0.0f);
                var a = firstPoint.GetVector3();
                for (var i = 1; i <= 100; ++i)
                {
                    using var point = _spline.GetPoint(i / 100.0f);
                    var b = point.GetVector3();
                    debug.AddLine(a, b, Color.Green);
                    a = b;
                }
            }

            for (var i = 0; i < _controlPoints.Count; ++i)
            {
                if (_controlPoints[i].TryGetTarget(out var point))
                    debug.AddNode(point);
            }

            if (_controlledNode is not null && _controlledNode.TryGetTarget(out var controlled))
                debug.AddNode(controlled);
        }
    }

    /// <summary>Add a Node to the SplinePath as a Control Point.</summary>
    public void AddControlPoint(Node? point, int index = int.MaxValue)
    {
        if (point is null)
            return;

        point.AddListener(this);
        index = Math.Clamp(index, 0, _controlPoints.Count);
        _controlPoints.Insert(index, new WeakReference<Node>(point));
        using var knot = new Variant(point.GetWorldPosition());
        _spline.AddKnot(knot, index);

        UpdateNodeIds();
        CalculateLength();
    }

    /// <summary>Remove a Node Control Point from the SplinePath.</summary>
    public void RemoveControlPoint(Node? point)
    {
        if (point is null)
            return;

        point.RemoveListener(this);

        for (var i = 0; i < _controlPoints.Count; ++i)
        {
            if (_controlPoints[i].TryGetTarget(out var controlPoint) && ReferenceEquals(controlPoint, point))
            {
                _controlPoints.RemoveAt(i);
                _spline.RemoveKnot(i);
                break;
            }
        }

        UpdateNodeIds();
        CalculateLength();
    }

    /// <summary>Clear the Control Points from the SplinePath.</summary>
    public void ClearControlPoints()
    {
        for (var i = 0; i < _controlPoints.Count; ++i)
        {
            if (_controlPoints[i].TryGetTarget(out var point))
                point.RemoveListener(this);
        }

        _controlPoints.Clear();
        _spline.Clear();

        UpdateNodeIds();
        CalculateLength();
    }

    /// <summary>Set the Interpolation Mode.</summary>
    public InterpolationMode InterpolationMode
    {
        get => _spline.GetInterpolationMode();
        set
        {
            _spline.SetInterpolationMode(value);
            CalculateLength();
        }
    }

    /// <summary>Set the movement Speed.</summary>
    public float Speed
    {
        get => _speed;
        set => _speed = value;
    }

    /// <summary>Set the controlled Node's position on the SplinePath.</summary>
    public void SetPosition(float factor)
    {
        var t = factor;

        if (t < 0.0f)
            t = 0.0f;
        else if (t > 1.0f)
            t = 1.0f;

        _traveled = t;
    }

    /// <summary>Set the Node to be moved along the SplinePath.</summary>
    public Node? ControlledNode
    {
        get => _controlledNode is not null && _controlledNode.TryGetTarget(out var controlled) ? controlled : null;
        set
        {
            if (value is not null)
                _controlledNode = new WeakReference<Node>(value);
        }
    }

    /// <summary>Get the length of SplinePath.</summary>
    public float Length => _length;

    /// <summary>Get the parent Node's last position on the spline.</summary>
    public Vector3f Position => GetPoint(_traveled);

    /// <summary>Get a point on the SplinePath from 0.f to 1.f where 0 is the start and 1 is the end.</summary>
    public Vector3f GetPoint(float factor)
    {
        using var point = _spline.GetPoint(factor);
        return point.GetVector3();
    }

    /// <summary>Move the controlled Node to the next position along the SplinePath based off the Speed value.</summary>
    public void Move(float timeStep)
    {
        if (_traveled >= 1.0f || _length <= 0.0f || _controlledNode is null || !_controlledNode.TryGetTarget(out var controlled))
            return;

        _elapsedTime += timeStep;

        // Calculate where we should be on the spline based on length, speed and time. If that is less than the set traveled_ don't move till caught up.
        var distanceCovered = _elapsedTime * _speed;
        _traveled = distanceCovered / _length;

        controlled.SetWorldPosition(GetPoint(_traveled));
    }

    /// <summary>Reset movement along the path.</summary>
    public void Reset()
    {
        _traveled = 0.0f;
        _elapsedTime = 0.0f;
    }

    /// <summary>Returns whether the movement along the SplinePath is complete.</summary>
    public bool IsFinished => _traveled >= 1.0f;

    /// <summary>Set Control Point Node IDs attribute.</summary>
    public VariantVector ControlPointIdsAttr
    {
        get => _controlPointIdsAttr;
        set
        {
            // Just remember the node IDs. They need to go through the SceneResolver, and we actually find the nodes during
            // ApplyAttributes()
            _controlPointIdsAttr.Dispose();
            if (value.Count != 0)
            {
                var index = 0;
                var numInstances = value[index++].GetUInt();
                // Prevent crash on entering negative value in the editor
                if (numInstances > int.MaxValue)
                    numInstances = 0;

                _controlPointIdsAttr.Add(new Variant(numInstances));
                for (var i = 0; i < (int)numInstances; ++i)
                {
                    // If vector contains less IDs than should, fill the rest with zeros
                    _controlPointIdsAttr.Add(index < value.Count
                        ? new Variant(value[index++].GetUInt())
                        : new Variant(0u));
                }

                _dirty = true;
            }
            else
            {
                _controlPointIdsAttr.Add(new Variant(0u));
                _dirty = true;
            }
        }
    }

    /// <summary>Set Controlled Node ID attribute.</summary>
    public uint ControlledIdAttr
    {
        get => _controlledIdAttr;
        set
        {
            if (value > 0 && value < uint.MaxValue)
                _controlledIdAttr = value;

            _dirty = true;
        }
    }

    /// <summary>Listener to manage Control Point movement.</summary>
    protected internal override void OnMarkedDirty(Node point)
    {
        for (var i = 0; i < _controlPoints.Count; ++i)
        {
            if (_controlPoints[i].TryGetTarget(out var controlPoint) && ReferenceEquals(controlPoint, point))
            {
                using var knot = new Variant(point.GetWorldPosition());
                _spline.SetKnot(knot, i);
                break;
            }
        }

        CalculateLength();
    }

    /// <summary>Listener to manage Control Point enabling.</summary>
    protected internal override void OnNodeSetEnabled(Node point)
    {
        for (var i = 0; i < _controlPoints.Count; ++i)
        {
            if (_controlPoints[i].TryGetTarget(out var controlPoint) && ReferenceEquals(controlPoint, point))
            {
                if (point.IsEnabled)
                {
                    using var knot = new Variant(point.GetWorldPosition());
                    _spline.AddKnot(knot, i);
                }
                else
                    _spline.RemoveKnot(i);

                break;
            }
        }

        CalculateLength();
    }

    /// <summary>Update the Node IDs of the Control Points.</summary>
    private void UpdateNodeIds()
    {
        var numInstances = _controlPoints.Count;

        _controlPointIdsAttr.Dispose();
        _controlPointIdsAttr.Add(new Variant((uint)numInstances));

        for (var i = 0; i < numInstances; ++i)
        {
            var nodeId = _controlPoints[i].TryGetTarget(out var point) ? point.Id : 0u;
            _controlPointIdsAttr.Add(new Variant(nodeId));
        }
    }

    /// <summary>Calculate the length of the SplinePath. Used for movement calculations.</summary>
    private void CalculateLength()
    {
        if (_spline.GetKnots().Count <= 0)
            return;

        _length = 0.0f;

        using var firstKnot = _spline.GetKnot(0);
        var a = firstKnot.GetVector3();
        for (var i = 0; i <= 1000; ++i)
        {
            using var point = _spline.GetPoint(i / 1000.0f);
            var b = point.GetVector3();
            _length += Math.Abs((a - b).Length);
            a = b;
        }
    }
}
