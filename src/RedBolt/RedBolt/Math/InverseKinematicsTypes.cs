using System.Runtime.InteropServices;

using RedBolt.Scene;
namespace RedBolt.Math;

/// <summary>
/// Aggregated settings of the IK solver.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
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
public class IKNode : ICloneable
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
        LocalOriginalPosition = new Vector3f();
        LocalOriginalRotation = Quaternion.IDENTITY;
        OriginalPosition = new Vector3f();
        OriginalRotation = Quaternion.IDENTITY;
        PreviousPosition = new Vector3f();
        PreviousRotation = Quaternion.IDENTITY;
        Position = new Vector3f();
        Rotation = Quaternion.IDENTITY;
    }

    public IKNode(Vector3f position, Quaternion rotation)
    {
        SetOriginalTransform(position, rotation, Matrix3x4.Identity);
    }

    public IKNode(IKNode node)
    {
        LocalOriginalPosition = node.LocalOriginalPosition;
        LocalOriginalRotation = node.LocalOriginalRotation;
        OriginalPosition = node.OriginalPosition;
        OriginalRotation = node.OriginalRotation;
        PreviousPosition = node.PreviousPosition;
        PreviousRotation = node.PreviousRotation;
        Position = node.Position;
        Rotation = node.Rotation;
        PositionDirty = node.PositionDirty;
        RotationDirty = node.RotationDirty;
    }

    public object Clone() => new IKNode(this);

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
[StructLayout(LayoutKind.Sequential)]
public struct IKNodeSegment
{
    public IKNode? BeginNode;
    public IKNode? EndNode;
    public float Length;

    /// <summary>
    /// Calculate rotation delta from positions, using previous positions as reference.
    /// </summary>
    public readonly Quaternion CalculateRotationDeltaFromPrevious()
    {
        var previousDirection = EndNode!.PreviousPosition - BeginNode!.PreviousPosition;
        var currentDirection = EndNode.Position - BeginNode.Position;
        return new Quaternion(previousDirection, currentDirection);
    }

    /// <summary>
    /// Calculate rotation delta from positions, using original positions as reference.
    /// </summary>
    public readonly Quaternion CalculateRotationDeltaFromOriginal()
    {
        var originalDirection = EndNode!.OriginalPosition - BeginNode!.OriginalPosition;
        var currentDirection = EndNode.Position - BeginNode.Position;
        return new Quaternion(originalDirection, currentDirection);
    }

    /// <summary>
    /// Calculate current rotation.
    /// </summary>
    public readonly Quaternion CalculateRotation(IKSettings settings)
    {
        if (settings.ContinuousRotations)
        {
            var delta = CalculateRotationDeltaFromPrevious();
            return delta * BeginNode!.PreviousRotation;
        }
        else
        {
            var delta = CalculateRotationDeltaFromOriginal();
            return delta * BeginNode!.OriginalRotation;
        }
    }

    /// <summary>
    /// Calculate current direction.
    /// </summary>
    public readonly Vector3f CalculateDirection()
    {
        return (EndNode!.Position - BeginNode!.Position).Normalized;
    }

    /// <summary>
    /// Update cached length.
    /// </summary>
    public void UpdateLength()
    {
        Length = (EndNode!.LocalOriginalPosition - BeginNode!.LocalOriginalPosition).Length;
    }

    /// <summary>
    /// Update current rotation for nodes.
    /// </summary>
    public void UpdateRotationInNodes(bool fromPrevious, bool isLastSegment)
    {
        if (fromPrevious)
        {
            var delta = CalculateRotationDeltaFromPrevious();
            BeginNode!.Rotation = delta * BeginNode.PreviousRotation;

            if (isLastSegment)
                EndNode!.Rotation = delta * EndNode.PreviousRotation;
        }
        else
        {
            var delta = CalculateRotationDeltaFromOriginal();
            BeginNode!.Rotation = delta * BeginNode.OriginalRotation;

            if (isLastSegment)
                EndNode!.Rotation = delta * EndNode.OriginalRotation;
        }

        BeginNode!.MarkRotationDirty();
        EndNode!.MarkRotationDirty();
    }

    /// <summary>
    /// Twist the segment around its direction.
    /// </summary>
    public void Twist(float angle, bool isLastSegment)
    {
        var rotation = new Quaternion(angle, CalculateDirection());
        BeginNode!.Rotation = rotation * BeginNode.Rotation;
        if (isLastSegment)
            EndNode!.Rotation = rotation * EndNode.Rotation;

        BeginNode.MarkRotationDirty();
        EndNode!.MarkRotationDirty();
    }
}
