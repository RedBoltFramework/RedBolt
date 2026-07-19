namespace RedBolt;

/// <summary>Trigonometric two-segment IK chain.</summary>
public class IKTrigonometricChain : ICloneable
{
    public IKTrigonometricChain()
    {
    }

    public IKTrigonometricChain(IKTrigonometricChain chain)
    {
        _segments[0] = chain._segments[0];
        _segments[1] = chain._segments[1];
        _currentChainRotation = chain._currentChainRotation;
    }

    public object Clone() => new IKTrigonometricChain(this);

    public void Initialize(IKNode node1, IKNode node2, IKNode node3)
    {
        _segments[0] = new IKNodeSegment { BeginNode = node1, EndNode = node2 };
        _segments[1] = new IKNodeSegment { BeginNode = node2, EndNode = node3 };
    }

    public void UpdateLengths()
    {
        _segments[0].UpdateLength();
        _segments[1].UpdateLength();
    }

    public void Solve(Vector3f target, Vector3f originalDirection,
        Vector3f currentDirection, float minAngle, float maxAngle)
    {
        ResetChainToOriginal();

        // Solve chain positions
        var pos0 = _segments[0].BeginNode!.Position;
        var len01 = _segments[0].Length;
        var len12 = _segments[1].Length;
        var (newPos1, newPos2) = Solve(pos0, len01, len12, target, currentDirection, minAngle, maxAngle);

        // Calculate base chain rotation
        _currentChainRotation = CalculateRotation(
            pos0, _segments[1].EndNode!.Position, originalDirection, pos0, target, currentDirection);

        _segments[0].BeginNode!.RotateAround(pos0, _currentChainRotation);
        _segments[1].BeginNode!.RotateAround(pos0, _currentChainRotation);
        _segments[1].EndNode!.RotateAround(pos0, _currentChainRotation);

        // Rotate segments in the chain
        var firstSegmentRotation = new Quaternion(_segments[0].CalculateDirection(), newPos1 - pos0);
        _segments[0].BeginNode!.RotateAround(pos0, firstSegmentRotation);
        _segments[1].BeginNode!.RotateAround(pos0, firstSegmentRotation);
        _segments[1].EndNode!.RotateAround(pos0, firstSegmentRotation);

        var secondSegmentRotation = new Quaternion(_segments[1].CalculateDirection(), newPos2 - newPos1);
        _segments[1].BeginNode!.RotateAround(newPos1, secondSegmentRotation);
        _segments[1].EndNode!.RotateAround(newPos1, secondSegmentRotation);

        _segments[1].BeginNode!.Position = newPos1;
        _segments[1].EndNode!.Position = newPos2;
    }

    /// <summary>Return rotation of the entire chain.</summary>
    public static Quaternion CalculateRotation(
        Vector3f originalPos0, Vector3f originalPos2, Vector3f originalDirection,
        Vector3f currentPos0, Vector3f currentPos2, Vector3f currentDirection)
    {
        // Calculate swing
        var originalTargetDirection = (originalPos2 - originalPos0).Normalized;
        var currentTargetDirection = (currentPos2 - currentPos0).Normalized;
        var swing = new Quaternion(originalTargetDirection, currentTargetDirection);

        // Calculate twist
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

        // This is angle between begin-to-middle and begin-to-end vectors.
        var cosAngle = MathDefs.Clamp(
            (len01 * len01 + len02 * len02 - len12 * len12) / (2.0f * len01 * len02), -1.0f, 1.0f);
        var sinAngle = MathF.Sqrt(1.0f - cosAngle * cosAngle);

        var newPos1 = pos0 + (firstAxis * cosAngle + secondAxis * sinAngle) * len01;
        return (newPos1, newPos2);
    }

    public IKNode GetBeginNode() => _segments[0].BeginNode!;
    public IKNode GetMiddleNode() => _segments[1].BeginNode!;
    public IKNode GetEndNode() => _segments[1].EndNode!;
    public float GetFirstLength() => _segments[0].Length;
    public float GetSecondLength() => _segments[1].Length;
    public Quaternion GetCurrentChainRotation() => _currentChainRotation;

    private void ResetChainToOriginal()
    {
        var initialOffset = _segments[0].BeginNode!.Position - _segments[0].BeginNode.OriginalPosition;

        _segments[0].BeginNode.ResetOriginalTransform();
        _segments[1].BeginNode!.ResetOriginalTransform();
        _segments[1].EndNode!.ResetOriginalTransform();

        _segments[0].BeginNode.Position += initialOffset;
        _segments[1].BeginNode.Position += initialOffset;
        _segments[1].EndNode.Position += initialOffset;
    }

    private readonly IKNodeSegment[] _segments = new IKNodeSegment[2];
    private Quaternion _currentChainRotation = Quaternion.Identity;
}

/// <summary>Two-segment IK chain for eyes.</summary>
public class IKEyeChain : ICloneable
{
    public IKEyeChain()
    {
    }

    public IKEyeChain(IKEyeChain chain)
    {
        _rootNode = chain._rootNode;
        _eyeOffset = chain._eyeOffset;
        _eyeDirection = chain._eyeDirection;
    }

    public object Clone() => new IKEyeChain(this);

    public void Initialize(IKNode rootNode)
    {
        _rootNode = rootNode;
    }

    public void SetEyeTransform(Vector3f eyeOffset, Vector3f eyeDirection)
    {
        _eyeOffset = eyeOffset;
        _eyeDirection = eyeDirection;
    }

    public void SetLocalEyeTransform(Vector3f eyeOffset, Vector3f eyeDirection)
    {
        _eyeOffset = _rootNode!.LocalOriginalRotation.Inverse * eyeOffset;
        _eyeDirection = _rootNode.LocalOriginalRotation.Inverse * eyeDirection;
    }

    public void SetWorldEyeTransform(Vector3f eyeOffset, Vector3f eyeDirection)
    {
        _eyeOffset = _rootNode!.Rotation.Inverse * eyeOffset;
        _eyeDirection = _rootNode.Rotation.Inverse * eyeDirection;
    }

    public Quaternion SolveLookAt(Vector3f lookAtTarget, IKSettings settings)
    {
        var parentTransform = new Transform(_rootNode!.Position, _rootNode.Rotation);
        var newTransform = parentTransform;
        for (uint i = 0; i < settings.MaxIterations; ++i)
        {
            var initialEyeDirection = newTransform.Rotation * _eyeDirection;
            var desiredEyeRotation = lookAtTarget - newTransform * _eyeOffset;
            var rotation = new Quaternion(initialEyeDirection, desiredEyeRotation);
            newTransform.Rotation = rotation * newTransform.Rotation;
        }
        return newTransform.Rotation * parentTransform.Rotation.Inverse;
    }

    public Quaternion SolveLookTo(Vector3f lookToDirection)
    {
        var parentTransform = new Transform(_rootNode!.Position, _rootNode.Rotation);
        var initialEyeDirection = parentTransform.Rotation * _eyeDirection;
        return new Quaternion(initialEyeDirection, lookToDirection);
    }

    public Vector3f GetLocalEyeOffset() => _eyeOffset;
    public Vector3f GetLocalEyeDirection() => _eyeDirection;

    private IKNode? _rootNode;
    private Vector3f _eyeOffset;
    private Vector3f _eyeDirection;
}
