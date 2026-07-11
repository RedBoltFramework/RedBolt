namespace RedBolt;

/// <summary>Trigonometric two-segment IK chain.</summary>
public sealed class IKTrigonometricChain
{
    private readonly IKNodeSegment[] _segments = new IKNodeSegment[2];
    private Quaternion _currentChainRotation = Quaternion.Identity;

    public void Initialize(IKNode node1, IKNode node2, IKNode node3)
    {
        _segments[0] = new IKNodeSegment { BeginNode = node1, EndNode = node2 };
        _segments[1] = new IKNodeSegment { BeginNode = node2, EndNode = node3 };
    }

    public void UpdateLengths() { _segments[0].UpdateLength(); _segments[1].UpdateLength(); }

    /// <summary>Return rotation of the entire chain.</summary>
    public static Quaternion CalculateRotation(Vector3f originalPos0, Vector3f originalPos2, Vector3f originalDirection,
        Vector3f currentPos0, Vector3f currentPos2, Vector3f currentDirection)
    {
        var originalTargetDirection = (originalPos2 - originalPos0).Normalized;
        var currentTargetDirection = (currentPos2 - currentPos0).Normalized;
        var swing = new Quaternion(originalTargetDirection, currentTargetDirection);
        var originalBendDirection = (swing * originalDirection).Orthogonalize(currentTargetDirection);
        var currentBendDirection = currentDirection.Orthogonalize(currentTargetDirection);
        var bendDirectionDelta = new Quaternion(originalBendDirection, currentBendDirection);
        var twist = bendDirectionDelta.ToSwingTwist(currentTargetDirection).Twist;
        return twist * swing;
    }

    /// <summary>Return positions of second and third bones.</summary>
    public static (Vector3f NewPos1, Vector3f NewPos2) Solve(Vector3f pos0, float len01, float len12,
        Vector3f target, Vector3f bendDirection, float minAngle, float maxAngle)
    {
        var minLen02 = MathF.Sqrt(len01 * len01 + len12 * len12 - 2 * len01 * len12 * MathDefs.Cos(minAngle));
        var maxLen02 = MathF.Sqrt(len01 * len01 + len12 * len12 - 2 * len01 * len12 * MathDefs.Cos(maxAngle));
        var len02 = MathDefs.Clamp((target - pos0).Length, minLen02, maxLen02);
        var newPos2 = (target - pos0).ReNormalized(len02, len02) + pos0;
        var firstAxis = (newPos2 - pos0).NormalizedOrDefault(Vector3f.Down);
        var secondAxis = bendDirection.Orthogonalize(firstAxis);
        var cosAngle = MathDefs.Clamp((len01 * len01 + len02 * len02 - len12 * len12) / (2.0f * len01 * len02), -1.0f, 1.0f);
        var sinAngle = MathF.Sqrt(1.0f - cosAngle * cosAngle);
        var newPos1 = pos0 + (firstAxis * cosAngle + secondAxis * sinAngle) * len01;
        return (newPos1, newPos2);
    }

    public void Solve(Vector3f target, Vector3f originalDirection, Vector3f currentDirection, float minAngle, float maxAngle)
    {
        ResetChainToOriginal();
        var pos0 = _segments[0].BeginNode!.Position;
        var (newPos1, newPos2) = Solve(pos0, _segments[0].Length, _segments[1].Length, target, currentDirection, minAngle, maxAngle);
        _currentChainRotation = CalculateRotation(pos0, _segments[1].EndNode!.Position, originalDirection, pos0, target, currentDirection);
        _segments[0].BeginNode!.RotateAround(pos0, _currentChainRotation);
        _segments[1].BeginNode!.RotateAround(pos0, _currentChainRotation);
        _segments[1].EndNode!.RotateAround(pos0, _currentChainRotation);
        var first = new Quaternion(_segments[0].CalculateDirection(), newPos1 - pos0);
        _segments[0].BeginNode!.RotateAround(pos0, first); _segments[1].BeginNode!.RotateAround(pos0, first); _segments[1].EndNode!.RotateAround(pos0, first);
        var second = new Quaternion(_segments[1].CalculateDirection(), newPos2 - newPos1);
        _segments[1].BeginNode!.RotateAround(newPos1, second); _segments[1].EndNode!.RotateAround(newPos1, second);
        _segments[1].BeginNode!.Position = newPos1; _segments[1].EndNode!.Position = newPos2;
    }

    public IKNode GetBeginNode() => _segments[0].BeginNode!;
    public IKNode GetMiddleNode() => _segments[1].BeginNode!;
    public IKNode GetEndNode() => _segments[1].EndNode!;
    public float GetFirstLength() => _segments[0].Length;
    public float GetSecondLength() => _segments[1].Length;
    public Quaternion GetCurrentChainRotation() => _currentChainRotation;

    private void ResetChainToOriginal()
    {
        var offset = _segments[0].BeginNode!.Position - _segments[0].BeginNode.OriginalPosition;
        _segments[0].BeginNode.ResetOriginalTransform(); _segments[1].BeginNode!.ResetOriginalTransform(); _segments[1].EndNode!.ResetOriginalTransform();
        _segments[0].BeginNode.Position += offset; _segments[1].BeginNode.Position += offset; _segments[1].EndNode.Position += offset;
    }
}

/// <summary>Two-segment IK chain for eyes.</summary>
public sealed class IKEyeChain
{
    private IKNode? _rootNode;
    private Vector3f _eyeOffset;
    private Vector3f _eyeDirection;
    public void Initialize(IKNode rootNode) => _rootNode = rootNode;
    public void SetEyeTransform(Vector3f eyeOffset, Vector3f eyeDirection) { _eyeOffset = eyeOffset; _eyeDirection = eyeDirection; }
    public void SetLocalEyeTransform(Vector3f eyeOffset, Vector3f eyeDirection) { var inverse = _rootNode!.LocalOriginalRotation.Inverse; _eyeOffset = inverse * eyeOffset; _eyeDirection = inverse * eyeDirection; }
    public void SetWorldEyeTransform(Vector3f eyeOffset, Vector3f eyeDirection) { var inverse = _rootNode!.Rotation.Inverse; _eyeOffset = inverse * eyeOffset; _eyeDirection = inverse * eyeDirection; }
    public Quaternion SolveLookAt(Vector3f lookAtTarget, IKSettings settings)
    {
        var parent = new Transform(_rootNode!.Position, _rootNode.Rotation); var next = parent;
        for (uint i = 0; i < settings.MaxIterations; ++i) { var initial = next.Rotation * _eyeDirection; var desired = lookAtTarget - next * _eyeOffset; next.Rotation = new Quaternion(initial, desired) * next.Rotation; }
        return next.Rotation * parent.Rotation.Inverse;
    }
    public Quaternion SolveLookTo(Vector3f lookToDirection) => new Quaternion(_rootNode!.Rotation * _eyeDirection, lookToDirection);
    public Vector3f GetLocalEyeOffset() => _eyeOffset;
    public Vector3f GetLocalEyeDirection() => _eyeDirection;
}
