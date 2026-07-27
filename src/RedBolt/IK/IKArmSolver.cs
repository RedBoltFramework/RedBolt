// Copyright (c) 2022-2023 the RedBolt project.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

using System;

using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.Scene;

namespace RedBolt.IK;

[Reflected(ObjectCategory.Category_IK, InheritAttributes = false)]
public partial class IKArmSolver : IKSolverComponent
{

    public IKArmSolver()
    {
    }

    public override void Dispose()
    {
        base.Dispose();
    }


    public override void DrawDebugGeometry(DebugRenderer? debug, bool depthTest)
    {
        var armBone = _chain.GetBeginNode();
        var forearmBone = _chain.GetMiddleNode();
        var handBone = _chain.GetEndNode();

        if (armBone is not null && forearmBone is not null && handBone is not null)
        {
            DrawIKNode(debug!, armBone, false);
            DrawIKNode(debug!, forearmBone, false);
            DrawIKNode(debug!, handBone, false);
            DrawIKSegment(debug!, armBone, forearmBone);
            DrawIKSegment(debug!, forearmBone, handBone);

            var currentBendDirection = _chain.GetCurrentChainRotation() * Node.WorldRotation * _bendDirection;
            DrawDirection(debug!, forearmBone.Position, currentBendDirection);
        }
        if (_target is not null && _target.TryGetTarget(out var target))
            DrawIKTarget(debug!, target, false);
    }

    /// <summary>Attributes.</summary>
    [Serialized("Arm Bone Name", DefaultSource = nameof(_armBoneName))]
    public string ArmBoneName { get => _armBoneName; set { _armBoneName = value; OnTreeDirty(); } }
    [Serialized("Forearm Bone Name", DefaultSource = nameof(_forearmBoneName))]
    public string ForearmBoneName { get => _forearmBoneName; set { _forearmBoneName = value; OnTreeDirty(); } }
    [Serialized("Hand Bone Name", DefaultSource = nameof(_handBoneName))]
    public string HandBoneName { get => _handBoneName; set { _handBoneName = value; OnTreeDirty(); } }

    [Serialized("Target Name", DefaultSource = nameof(_targetName))]
    public string TargetName { get => _targetName; set { _targetName = value; OnTreeDirty(); } }
    [Serialized("Bend Target Name", DefaultSource = nameof(_bendTargetName))]
    public string BendTargetName { get => _bendTargetName; set { _bendTargetName = value; OnTreeDirty(); } }

    [Serialized("Position Weight", DefaultSource = nameof(_positionWeight))]
    public float PositionWeight { get => _positionWeight; set => _positionWeight = value; }
    [Serialized("Rotation Weight", DefaultSource = nameof(_rotationWeight))]
    public float RotationWeight { get => _rotationWeight; set => _rotationWeight = value; }
    [Serialized("Bend Weight", DefaultSource = nameof(_bendWeight))]
    public float BendWeight { get => _bendWeight; set => _bendWeight = value; }
    [Serialized("Min Angle", DefaultSource = nameof(_minElbowAngle))]
    public float MinAngle { get => _minElbowAngle; set => _minElbowAngle = value; }
    [Serialized("Max Angle", DefaultSource = nameof(_maxElbowAngle))]
    public float MaxAngle { get => _maxElbowAngle; set => _maxElbowAngle = value; }
    [Serialized("Bend Direction", DefaultSource = nameof(_bendDirection))]
    public Vector3f BendDirection { get => _bendDirection; set => _bendDirection = value; }

    /// <summary>Implement IKSolverComponent.</summary>
    protected override bool InitializeNodes(IKNodeCache nodeCache)
    {
        var target = AddCheckedNode(nodeCache, _targetName);
        _target = target is not null ? new WeakReference<Node>(target) : null;
        if (target is null)
            return false;

        var bendTarget = AddCheckedNode(nodeCache, _bendTargetName);
        _bendTarget = bendTarget is not null ? new WeakReference<Node>(bendTarget) : null;

        var armBone = AddSolverNode(nodeCache, _armBoneName);
        if (armBone is null)
            return false;

        var forearmBone = AddSolverNode(nodeCache, _forearmBoneName);
        if (forearmBone is null)
            return false;

        var handBone = AddSolverNode(nodeCache, _handBoneName);
        if (handBone is null)
            return false;

        SetParentAsFrameOfReference(armBone);
        _chain.Initialize(armBone, forearmBone, handBone);
        return true;
    }

    protected override void UpdateChainLengths(Transform inverseLocalFrameOfReference)
    {
        _chain.UpdateLengths();

        _local.BendDirection = inverseLocalFrameOfReference.Rotation * _bendDirection;
        _local.TargetDirection = inverseLocalFrameOfReference.Rotation
            * (_chain.GetEndNode().LocalOriginalPosition - _chain.GetBeginNode().LocalOriginalPosition).Normalized;

        _local.ArmOffset = inverseLocalFrameOfReference * _chain.GetBeginNode().LocalOriginalPosition;
        _local.ArmRotation = inverseLocalFrameOfReference * _chain.GetBeginNode().LocalOriginalRotation;
    }

    protected override void SolveInternal(Transform frameOfReference, IKSettings settings, float timeStep)
    {
        EnsureInitialized();

        // Store original rotation
        var armBone = _chain.GetBeginNode();
        var forearmBone = _chain.GetMiddleNode();
        var handBone = _chain.GetEndNode();

        var armBoneRotation = armBone.Rotation;
        var forearmBoneRotation = forearmBone.Rotation;
        var handBoneRotation = handBone.Rotation;

        // Solve rotations for full solver weight
        armBone.Position = frameOfReference * _local.ArmOffset;
        armBone.Rotation = frameOfReference * _local.ArmRotation;

        _target!.TryGetTarget(out var target);
        var handTargetPosition = target!.WorldPosition;
        var (originalDirection, currentDirection) = CalculateBendDirections(frameOfReference, handTargetPosition);
        _chain.Solve(handTargetPosition, originalDirection, currentDirection, _minElbowAngle, _maxElbowAngle);

        // Interpolate rotation to apply solver weight
        armBone.Rotation = armBoneRotation.Slerp(armBone.Rotation, _positionWeight);
        forearmBone.Rotation = forearmBoneRotation.Slerp(forearmBone.Rotation, _positionWeight);
        handBone.Rotation = handBoneRotation.Slerp(handBone.Rotation, _positionWeight);

        // Apply target rotation if needed
        if (_rotationWeight > 0.0f)
        {
            var targetRotation = target.WorldRotation * handBone.LocalOriginalRotation;
            handBone.Rotation = handBone.Rotation.Slerp(targetRotation, _rotationWeight);
        }
    }

    private void EnsureInitialized()
    {
        _positionWeight = MathDefs.Clamp(_positionWeight, 0.0f, 1.0f);
        _rotationWeight = MathDefs.Clamp(_rotationWeight, 0.0f, 1.0f);
        _bendWeight = MathDefs.Clamp(_bendWeight, 0.0f, 1.0f);
        _minElbowAngle = MathDefs.Clamp(_minElbowAngle, 0.0f, 180.0f);
        _maxElbowAngle = MathDefs.Clamp(_maxElbowAngle, 0.0f, 180.0f);
    }

    private (Vector3f OriginalDirection, Vector3f CurrentDirection) CalculateBendDirections(
        Transform frameOfReference, Vector3f handTargetPosition)
    {
        var parameters = new BendCalculationParams
        {
            ParentNodeRotation = Node.WorldRotation,
            StartPosition = _chain.GetBeginNode().Position,
            TargetPosition = handTargetPosition,
            TargetDirectionInLocalSpace = _local.TargetDirection,
            BendDirectionInNodeSpace = _bendDirection,
            BendDirectionInLocalSpace = _local.BendDirection,
            BendTarget = _bendTarget is not null && _bendTarget.TryGetTarget(out var bendTarget) ? bendTarget : null,
            BendTargetWeight = _bendWeight
        };

        return CalculateBendDirectionsInternal(frameOfReference, parameters);
    }

    /// <summary>Attributes.</summary>
    private string _armBoneName = string.Empty;
    private string _forearmBoneName = string.Empty;
    private string _handBoneName = string.Empty;

    private string _targetName = string.Empty;
    private string _bendTargetName = string.Empty;

    private float _positionWeight = 1.0f;
    private float _rotationWeight;
    private float _bendWeight = 1.0f;
    private float _minElbowAngle;
    private float _maxElbowAngle = 180.0f;
    private Vector3f _bendDirection = Vector3f.Forward;

    /// <summary>IK nodes and effectors.</summary>
    private readonly IKTrigonometricChain _chain = new();
    private WeakReference<Node>? _target;
    private WeakReference<Node>? _bendTarget;

    private struct LocalCache
    {
        public Vector3f BendDirection;
        public Vector3f TargetDirection;

        public Vector3f ArmOffset;
        public Quaternion ArmRotation;
    }

    private LocalCache _local;
}
