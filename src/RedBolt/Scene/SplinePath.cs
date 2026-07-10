namespace RedBolt;

public sealed class SplinePath : Component
{
    private readonly Spline _spline = new(InterpolationMode.BezierCurve);
    private readonly List<Node?> _controlPoints = [];
    private readonly List<uint> _controlPointIdsAttr = [0u];
    private float _speed = 1.0f;
    private float _elapsedTime;
    private float _traveled;
    private float _length;
    private bool _dirty;
    private Node? _controlledNode;
    private uint _controlledIdAttr;

    public SplinePath()
    {
        RegisterAttribute(
            "Interpolation Mode",
            typeof(InterpolationMode),
            self => ((SplinePath)self).GetInterpolationMode(),
            (self, value) => ((SplinePath)self).SetInterpolationMode(value is InterpolationMode typed ? typed : InterpolationMode.BezierCurve),
            InterpolationMode.BezierCurve);
        RegisterAttribute(
            "Speed",
            typeof(float),
            self => ((SplinePath)self).GetSpeed(),
            (self, value) => ((SplinePath)self).SetSpeed(value is float typed ? typed : 1.0f),
            1.0f);
        RegisterAttribute(
            "Traveled",
            typeof(float),
            self => ((SplinePath)self)._traveled,
            (self, value) => ((SplinePath)self).SetPosition(value is float typed ? typed : 0.0f),
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
            self => ((SplinePath)self).GetControlledIdAttr(),
            (self, value) => ((SplinePath)self).SetControlledIdAttr(value is uint typed ? typed : 0u),
            0u);
        RegisterAttribute(
            "Control Points",
            typeof(IReadOnlyList<uint>),
            self => ((SplinePath)self).GetControlPointIdsAttr(),
            (self, value) => ((SplinePath)self).SetControlPointIdsAttr(value as IReadOnlyList<uint> ?? Array.Empty<uint>()),
            Array.Empty<uint>());
        UpdateNodeIds();
    }

    public override void ApplyAttributes()
    {
        if (!_dirty)
            return;

        _controlPoints.Clear();
        _spline.Clear();

        var scene = Scene;
        if (scene is not null)
        {
            for (var i = 1; i < _controlPointIdsAttr.Count; ++i)
            {
                var node = scene.GetNode(_controlPointIdsAttr[i]);
                _controlPoints.Add(node);
                if (node is not null)
                    _spline.AddKnot(node.GetWorldPosition());
            }

            _controlledNode = scene.GetNode(_controlledIdAttr);
        }

        CalculateLength();
        _dirty = false;
    }

    public void AddControlPoint(Node? point, uint index = uint.MaxValue)
    {
        if (point is null)
            return;

        var insertIndex = (int)Math.Min(index, (uint)_controlPoints.Count);
        _controlPoints.Insert(insertIndex, point);
        RefreshSplineKnots();
        UpdateNodeIds();
        CalculateLength();
    }

    public void RemoveControlPoint(Node? point)
    {
        if (point is null)
            return;

        _controlPoints.Remove(point);
        RefreshSplineKnots();
        UpdateNodeIds();
        CalculateLength();
    }

    public void ClearControlPoints()
    {
        _controlPoints.Clear();
        _spline.Clear();
        UpdateNodeIds();
        CalculateLength();
    }

    public void SetInterpolationMode(InterpolationMode interpolationMode)
    {
        _spline.SetInterpolationMode(interpolationMode);
        CalculateLength();
    }

    public InterpolationMode GetInterpolationMode() => _spline.GetInterpolationMode();


    public void SetSpeed(float speed)
    {
        this._speed = speed;
    }

    public float GetSpeed() => _speed;

    public float GetLength() => _length;

    public Vector3f GetPosition() => GetPoint(_traveled);

    public void SetPosition(float factor)
    {
        _traveled = MathDefs.Clamp(factor, 0.0f, 1.0f);
    }

    public void SetControlledNode(Node? controlled)
    {
        _controlledNode = controlled;
        _controlledIdAttr = controlled?.Id ?? 0u;
    }

    public Node? GetControlledNode() => _controlledNode;

    public Vector3f GetPoint(float factor)
    {
        RefreshSplineKnots();
        var point = _spline.GetPoint(MathDefs.Clamp(factor, 0.0f, 1.0f));
        return point.Type == VariantType.Vector3 ? point.GetVector3() : Vector3f.Zero;
    }

    public void Move(float timeStep)
    {
        if (_traveled >= 1.0f || _length <= 0.0f || _controlledNode is null)
            return;

        _elapsedTime += timeStep;
        var distanceCovered = _elapsedTime * _speed;
        _traveled = distanceCovered / _length;
        _controlledNode.SetWorldPosition(GetPoint(_traveled));
    }

    public void Reset()
    {
        _traveled = 0.0f;
        _elapsedTime = 0.0f;
    }

    public bool IsFinished() => _traveled >= 1.0f;

    public void SetControlPointIdsAttr(IReadOnlyList<uint> value)
    {
        _controlPointIdsAttr.Clear();
        if (value.Count == 0)
        {
            _controlPointIdsAttr.Add(0u);
        }
        else
        {
            _controlPointIdsAttr.Add(value[0]);
            for (var i = 1; i < value.Count; ++i)
                _controlPointIdsAttr.Add(value[i]);
        }
        _dirty = true;
    }

    public IReadOnlyList<uint> GetControlPointIdsAttr() => _controlPointIdsAttr;

    public void SetControlledIdAttr(uint value)
    {
        if (value > 0u && value < uint.MaxValue)
            _controlledIdAttr = value;
        _dirty = true;
    }

    public uint GetControlledIdAttr() => _controlledIdAttr;

    protected internal override void OnMarkedDirty(Node node)
    {
        if (_controlPoints.Contains(node))
            CalculateLength();
    }

    protected internal override void OnNodeSetEnabled(Node node)
    {
        if (_controlPoints.Contains(node))
            CalculateLength();
    }

    private void UpdateNodeIds()
    {
        _controlPointIdsAttr.Clear();
        _controlPointIdsAttr.Add((uint)_controlPoints.Count);
        foreach (var controlPoint in _controlPoints)
            _controlPointIdsAttr.Add(controlPoint?.Id ?? 0u);
        _controlledIdAttr = _controlledNode?.Id ?? _controlledIdAttr;
    }

    private void CalculateLength()
    {
        _length = 0.0f;
        RefreshSplineKnots();
        if (_controlPoints.Count < 2)
            return;

        var previous = GetPoint(0.0f);
        for (var i = 1; i <= 100; ++i)
        {
            var current = GetPoint(i / 100.0f);
            _length += previous.DistanceToPoint(current);
            previous = current;
        }
    }

    private void RefreshSplineKnots()
    {
        _spline.Clear();
        foreach (var controlPoint in _controlPoints)
        {
            if (controlPoint is not null && controlPoint.IsEnabled)
                _spline.AddKnot(controlPoint.GetWorldPosition());
        }
    }
}
