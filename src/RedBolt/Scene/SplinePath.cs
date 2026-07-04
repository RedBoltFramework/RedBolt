namespace RedBolt;

public sealed class SplinePath : Component
{
    private readonly Spline spline = new(InterpolationMode.BezierCurve);
    private readonly List<Node?> controlPoints = [];
    private readonly List<uint> controlPointIdsAttr = [0u];
    private float speed = 1.0f;
    private float elapsedTime;
    private float traveled;
    private float length;
    private bool dirty;
    private Node? controlledNode;
    private uint controlledIdAttr;

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
            self => ((SplinePath)self).traveled,
            (self, value) => ((SplinePath)self).SetPosition(value is float typed ? typed : 0.0f),
            0.0f,
            AttributeMode.Default | AttributeMode.NoEdit);
        RegisterAttribute(
            "Elapsed Time",
            typeof(float),
            self => ((SplinePath)self).elapsedTime,
            (self, value) => ((SplinePath)self).elapsedTime = value is float typed ? typed : 0.0f,
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
        if (!dirty)
            return;

        controlPoints.Clear();
        spline.Clear();

        var scene = Scene;
        if (scene is not null)
        {
            for (var i = 1; i < controlPointIdsAttr.Count; ++i)
            {
                var node = scene.GetNode(controlPointIdsAttr[i]);
                controlPoints.Add(node);
                if (node is not null)
                    spline.AddKnot(node.GetWorldPosition());
            }

            controlledNode = scene.GetNode(controlledIdAttr);
        }

        CalculateLength();
        dirty = false;
    }

    public void AddControlPoint(Node? point, uint index = uint.MaxValue)
    {
        if (point is null)
            return;

        var insertIndex = (int)Math.Min(index, (uint)controlPoints.Count);
        controlPoints.Insert(insertIndex, point);
        RefreshSplineKnots();
        UpdateNodeIds();
        CalculateLength();
    }

    public void RemoveControlPoint(Node? point)
    {
        if (point is null)
            return;

        controlPoints.Remove(point);
        RefreshSplineKnots();
        UpdateNodeIds();
        CalculateLength();
    }

    public void ClearControlPoints()
    {
        controlPoints.Clear();
        spline.Clear();
        UpdateNodeIds();
        CalculateLength();
    }

    public void SetInterpolationMode(InterpolationMode interpolationMode)
    {
        spline.SetInterpolationMode(interpolationMode);
        CalculateLength();
    }

    public InterpolationMode GetInterpolationMode() => spline.InterpolationMode;

    public void SetSpeed(float speed)
    {
        this.speed = speed;
    }

    public float GetSpeed() => speed;

    public float GetLength() => length;

    public Vector3 GetPosition() => GetPoint(traveled);

    public void SetPosition(float factor)
    {
        traveled = MathDefs.Clamp(factor, 0.0f, 1.0f);
    }

    public void SetControlledNode(Node? controlled)
    {
        controlledNode = controlled;
        controlledIdAttr = controlled?.Id ?? 0u;
    }

    public Node? GetControlledNode() => controlledNode;

    public Vector3 GetPoint(float factor)
    {
        RefreshSplineKnots();
        return spline.GetPoint(MathDefs.Clamp(factor, 0.0f, 1.0f)) as Vector3? ?? Vector3.Zero;
    }

    public void Move(float timeStep)
    {
        if (traveled >= 1.0f || length <= 0.0f || controlledNode is null)
            return;

        elapsedTime += timeStep;
        var distanceCovered = elapsedTime * speed;
        traveled = distanceCovered / length;
        controlledNode.SetWorldPosition(GetPoint(traveled));
    }

    public void Reset()
    {
        traveled = 0.0f;
        elapsedTime = 0.0f;
    }

    public bool IsFinished() => traveled >= 1.0f;

    public void SetControlPointIdsAttr(IReadOnlyList<uint> value)
    {
        controlPointIdsAttr.Clear();
        if (value.Count == 0)
        {
            controlPointIdsAttr.Add(0u);
        }
        else
        {
            controlPointIdsAttr.Add(value[0]);
            for (var i = 1; i < value.Count; ++i)
                controlPointIdsAttr.Add(value[i]);
        }
        dirty = true;
    }

    public IReadOnlyList<uint> GetControlPointIdsAttr() => controlPointIdsAttr;

    public void SetControlledIdAttr(uint value)
    {
        if (value > 0u && value < uint.MaxValue)
            controlledIdAttr = value;
        dirty = true;
    }

    public uint GetControlledIdAttr() => controlledIdAttr;

    protected internal override void OnMarkedDirty(Node node)
    {
        if (controlPoints.Contains(node))
            CalculateLength();
    }

    protected internal override void OnNodeSetEnabled(Node node)
    {
        if (controlPoints.Contains(node))
            CalculateLength();
    }

    private void UpdateNodeIds()
    {
        controlPointIdsAttr.Clear();
        controlPointIdsAttr.Add((uint)controlPoints.Count);
        foreach (var controlPoint in controlPoints)
            controlPointIdsAttr.Add(controlPoint?.Id ?? 0u);
        controlledIdAttr = controlledNode?.Id ?? controlledIdAttr;
    }

    private void CalculateLength()
    {
        length = 0.0f;
        RefreshSplineKnots();
        if (controlPoints.Count < 2)
            return;

        var previous = GetPoint(0.0f);
        for (var i = 1; i <= 100; ++i)
        {
            var current = GetPoint(i / 100.0f);
            length += previous.DistanceToPoint(current);
            previous = current;
        }
    }

    private void RefreshSplineKnots()
    {
        spline.Clear();
        foreach (var controlPoint in controlPoints)
        {
            if (controlPoint is not null && controlPoint.IsEnabled)
                spline.AddKnot(controlPoint.GetWorldPosition());
        }
    }
}
