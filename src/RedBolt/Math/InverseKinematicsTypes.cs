// C++: matches C++ within allowed deviations (port of InverseKinematics.h types: IKNode, IKNodeSegment, IKSettings).
namespace RedBolt;

/// <summary>
/// Aggregated settings of the IK solver.
/// </summary>
public struct IKSettings
{
    public uint MaxIterations;
    public float Tolerance;

    /// <summary>
    /// Whether to consider node rotations from the previous frame when solving.
    /// Results in smoother motion, but may cause rotation bleeding over time.
    /// </summary>
    public bool ContinuousRotations;

    public IKSettings()
    {
        MaxIterations = 10;
        Tolerance = 0.001f;
        ContinuousRotations = false;
    }
}

/// <summary>
/// Singular node of the IK chain.
/// Node should be used only once within a chain.
/// However, node may belong to multiple independet chains.
/// Positions and rotations are in world space.
/// </summary>
public sealed class IKNode
{
    public Vector3f LocalOriginalPosition;
    public Quaternion LocalOriginalRotation;
    public Vector3f OriginalPosition;
    public Quaternion OriginalRotation;
    public Vector3f PreviousPosition;
    public Quaternion PreviousRotation;
    public Vector3f Position;
    public Quaternion Rotation;
    public bool PositionDirty;
    public bool RotationDirty;

    public IKNode()
    {
    }

    public IKNode(Vector3f position, Quaternion rotation)
    {
        SetOriginalTransform(position, rotation, Matrix3x4.Identity);
    }

    public void SetOriginalTransform(Vector3f position, Quaternion rotation, Matrix3x4 worldTransform)
    {
        var worldRotation = worldTransform.Rotation;
        LocalOriginalPosition = position;
        LocalOriginalRotation = rotation;
        OriginalPosition = worldTransform * position;
        OriginalRotation = worldRotation * rotation;
        Position = worldTransform * position;
        Rotation = worldRotation * rotation;
        PreviousPosition = worldTransform * position;
        PreviousRotation = worldRotation * rotation;
    }

    public void UpdateOriginalTransform(Matrix3x4 worldTransform)
    {
        OriginalPosition = worldTransform * LocalOriginalPosition;
        OriginalRotation = worldTransform.Rotation * LocalOriginalRotation;
    }

    public void RotateAround(Vector3f point, Quaternion rotation)
    {
        Position = rotation * (Position - point) + point;
        Rotation = rotation * Rotation;
        RotationDirty = true;
    }

    public void ResetOriginalTransform()
    {
        Position = OriginalPosition;
        Rotation = OriginalRotation;
    }

    public void StorePreviousTransform()
    {
        PreviousPosition = Position;
        PreviousRotation = Rotation;
        PositionDirty = false;
        RotationDirty = false;
    }

    public void MarkPositionDirty() => PositionDirty = true;

    public void MarkRotationDirty() => RotationDirty = true;
}

/// <summary>
/// Fixed-length segment that consists of two nodes.
/// </summary>
public struct IKNodeSegment
{
    public IKNode? BeginNode;
    public IKNode? EndNode;
    public float Length;

    /// <summary>
    /// Calculate rotation delta from the previous frame.
    /// </summary>
    public Quaternion CalculateRotationDeltaFromPrevious()
    {
        var beginNode = BeginNode!;
        var endNode = EndNode!;
        var previousDirection = endNode.PreviousPosition - beginNode.PreviousPosition;
        var currentDirection = endNode.Position - beginNode.Position;
        return new Quaternion(previousDirection, currentDirection);
    }

    /// <summary>
    /// Calculate rotation delta from the original pose.
    /// </summary>
    public Quaternion CalculateRotationDeltaFromOriginal()
    {
        var beginNode = BeginNode!;
        var endNode = EndNode!;
        var originalDirection = endNode.OriginalPosition - beginNode.OriginalPosition;
        var currentDirection = endNode.Position - beginNode.Position;
        return new Quaternion(originalDirection, currentDirection);
    }

    /// <summary>
    /// Calculate rotation of the segment according to the settings.
    /// </summary>
    public Quaternion CalculateRotation(IKSettings settings)
    {
        var beginNode = BeginNode!;
        return settings.ContinuousRotations
            ? CalculateRotationDeltaFromPrevious() * beginNode.PreviousRotation
            : CalculateRotationDeltaFromOriginal() * beginNode.OriginalRotation;
    }

    /// <summary>
    /// Calculate current segment direction.
    /// </summary>
    public Vector3f CalculateDirection()
    {
        var beginNode = BeginNode!;
        var endNode = EndNode!;
        return (endNode.Position - beginNode.Position).Normalized;
    }

    /// <summary>
    /// Update segment length from the original local node positions.
    /// </summary>
    public void UpdateLength()
    {
        var beginNode = BeginNode!;
        var endNode = EndNode!;
        Length = (endNode.LocalOriginalPosition - beginNode.LocalOriginalPosition).Length;
    }

    /// <summary>
    /// Update rotations in segment nodes.
    /// </summary>
    public void UpdateRotationInNodes(bool fromPrevious, bool isLastSegment)
    {
        var beginNode = BeginNode!;
        var endNode = EndNode!;
        var rotation = fromPrevious ? CalculateRotationDeltaFromPrevious() : CalculateRotationDeltaFromOriginal();
        beginNode.Rotation = rotation * (fromPrevious ? beginNode.PreviousRotation : beginNode.OriginalRotation);
        if (isLastSegment)
            endNode.Rotation = rotation * (fromPrevious ? endNode.PreviousRotation : endNode.OriginalRotation);
        beginNode.MarkRotationDirty();
        endNode.MarkRotationDirty();
    }

    /// <summary>
    /// Twist segment nodes around the current segment direction.
    /// </summary>
    public void Twist(float angle, bool isLastSegment)
    {
        var beginNode = BeginNode!;
        var endNode = EndNode!;
        var rotation = new Quaternion(angle, CalculateDirection());
        beginNode.Rotation = rotation * beginNode.Rotation;
        if (isLastSegment)
            endNode.Rotation = rotation * endNode.Rotation;
        beginNode.MarkRotationDirty();
        endNode.MarkRotationDirty();
    }
}
