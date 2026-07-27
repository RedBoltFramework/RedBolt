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
public partial class IKLimbSolver : IKSolverComponent
{

    public IKLimbSolver()
    {
    }

    public override void Dispose()
    {
        base.Dispose();
    }


    public override void DrawDebugGeometry(DebugRenderer? debug, bool depthTest)
    {
        var firstBone = _chain.GetBeginNode();
        var secondBone = _chain.GetMiddleNode();
        var thirdBone = _chain.GetEndNode();

        if (firstBone is not null && secondBone is not null && thirdBone is not null)
        {
            DrawIKNode(debug!, firstBone, false);
            DrawIKNode(debug!, secondBone, false);
            DrawIKNode(debug!, thirdBone, false);
            DrawIKSegment(debug!, firstBone, secondBone);
            DrawIKSegment(debug!, secondBone, thirdBone);

            var currentBendDirection = _chain.GetCurrentChainRotation() * Node.WorldRotation * _bendDirection;
            DrawDirection(debug!, secondBone.Position, currentBendDirection);
        }
        if (_target is not null && _target.TryGetTarget(out _))
            DrawIKTarget(debug!, _latestTargetPosition, Quaternion.IDENTITY, false);
        if (_bendTarget is not null && _bendTarget.TryGetTarget(out var bendTarget))
            DrawIKTarget(debug!, bendTarget, false);
    }

    /// <summary>Attributes.</summary>
    [Serialized("Bone 0 Name", DefaultSource = nameof(_firstBoneName))]
    public string FirstBoneName
    {
        get => _firstBoneName;
        set { _firstBoneName = value; OnTreeDirty(); }
    }

    [Serialized("Bone 1 Name", DefaultSource = nameof(_secondBoneName))]
    public string SecondBoneName
    {
        get => _secondBoneName;
        set { _secondBoneName = value; OnTreeDirty(); }
    }

    [Serialized("Bone 2 Name", DefaultSource = nameof(_thirdBoneName))]
    public string ThirdBoneName
    {
        get => _thirdBoneName;
        set { _thirdBoneName = value; OnTreeDirty(); }
    }

    [Serialized("Target Name", DefaultSource = nameof(_targetName))]
    public string TargetName
    {
        get => _targetName;
        set { _targetName = value; OnTreeDirty(); }
    }

    [Serialized("Bend Target Name", DefaultSource = nameof(_bendTargetName))]
    public string BendTargetName
    {
        get => _bendTargetName;
        set { _bendTargetName = value; OnTreeDirty(); }
    }

    [Serialized("Position Weight", DefaultSource = nameof(_positionWeight))]
    public float PositionWeight
    {
        get => _positionWeight;
        set => _positionWeight = value;
    }

    [Serialized("Rotation Weight", DefaultSource = nameof(_rotationWeight))]
    public float RotationWeight
    {
        get => _rotationWeight;
        set => _rotationWeight = value;
    }

    [Serialized("Bend Weight", DefaultSource = nameof(_bendWeight))]
    public float BendWeight
    {
        get => _bendWeight;
        set => _bendWeight = value;
    }

    [Serialized("Min Angle", DefaultSource = nameof(_minAngle))]
    public float MinAngle
    {
        get => _minAngle;
        set => _minAngle = value;
    }

    [Serialized("Max Angle", DefaultSource = nameof(_maxAngle))]
    public float MaxAngle
    {
        get => _maxAngle;
        set => _maxAngle = value;
    }

    [Serialized("Bend Direction", DefaultSource = nameof(_bendDirection))]
    public Vector3f BendDirection
    {
        get => _bendDirection;
        set => _bendDirection = value;
    }

    /// <summary>Implement IKSolverComponent.</summary>
    protected override bool InitializeNodes(IKNodeCache nodeCache)
    {
        var target = AddCheckedNode(nodeCache, _targetName);
        _target = target is not null ? new WeakReference<Node>(target) : null;
        if (target is null)
            return false;

        var bendTarget = AddCheckedNode(nodeCache, _bendTargetName);
        _bendTarget = bendTarget is not null ? new WeakReference<Node>(bendTarget) : null;

        var firstBone = AddSolverNode(nodeCache, _firstBoneName);
        if (firstBone is null)
            return false;

        var secondBone = AddSolverNode(nodeCache, _secondBoneName);
        if (secondBone is null)
            return false;

        var thirdBone = AddSolverNode(nodeCache, _thirdBoneName);
        if (thirdBone is null)
            return false;

        SetParentAsFrameOfReference(firstBone);
        _chain.Initialize(firstBone, secondBone, thirdBone);
        return true;
    }

    protected override void UpdateChainLengths(Transform inverseLocalFrameOfReference)
    {
        _chain.UpdateLengths();

        _local.BendDirection = inverseLocalFrameOfReference.Rotation * _bendDirection;
        _local.TargetDirection = inverseLocalFrameOfReference.Rotation
            * (_chain.GetEndNode().LocalOriginalPosition - _chain.GetBeginNode().LocalOriginalPosition).Normalized;
    }

    protected override void SolveInternal(Transform frameOfReference, IKSettings settings, float timeStep)
    {
        EnsureInitialized();

        // Store original rotation
        var firstBone = _chain.GetBeginNode();
        var secondBone = _chain.GetMiddleNode();
        var thirdBone = _chain.GetEndNode();

        var firstBoneRotation = firstBone.Rotation;
        var secondBoneRotation = secondBone.Rotation;
        var thirdBoneRotation = thirdBone.Rotation;

        // Solve rotations for full solver weight
        _latestTargetPosition = GetTargetPosition();
        var (originalDirection, currentDirection) = CalculateBendDirections(frameOfReference, _latestTargetPosition);

        _chain.Solve(_latestTargetPosition, originalDirection, currentDirection, _minAngle, _maxAngle);

        // Interpolate rotation to apply solver weight
        firstBone.Rotation = firstBoneRotation.Slerp(firstBone.Rotation, _positionWeight);
        secondBone.Rotation = secondBoneRotation.Slerp(secondBone.Rotation, _positionWeight);
        thirdBone.Rotation = thirdBoneRotation.Slerp(thirdBone.Rotation, _positionWeight);

        // Apply target rotation if needed
        if (_rotationWeight > 0.0f)
        {
            _target!.TryGetTarget(out var target);
            var targetRotation = target!.WorldRotation * thirdBone.LocalOriginalRotation;
            thirdBone.Rotation = thirdBone.Rotation.Slerp(targetRotation, _rotationWeight);
        }
    }

    private void EnsureInitialized()
    {
        _positionWeight = MathDefs.Clamp(_positionWeight, 0.0f, 1.0f);
        _rotationWeight = MathDefs.Clamp(_rotationWeight, 0.0f, 1.0f);
        _bendWeight = MathDefs.Clamp(_bendWeight, 0.0f, 1.0f);
        _minAngle = MathDefs.Clamp(_minAngle, 0.0f, 180.0f);
        _maxAngle = MathDefs.Clamp(_maxAngle, _minAngle, 180.0f);
    }

    private Vector3f GetTargetPosition()
    {
        var firstBone = _chain.GetBeginNode();

        const float minDistance = 0.001f;
        var maxDistance = GetMaxDistance(_chain, _maxAngle);
        var origin = firstBone.Position;
        _target!.TryGetTarget(out var targetNode);
        var target = targetNode!.WorldPosition;
        return origin + (target - origin).ReNormalized(minDistance, maxDistance);
    }

    private (Vector3f OriginalDirection, Vector3f CurrentDirection) CalculateBendDirections(
        Transform frameOfReference, Vector3f targetPosition)
    {
        Node? bendTarget = null;
        _bendTarget?.TryGetTarget(out bendTarget);
        var parameters = new BendCalculationParams
        {
            ParentNodeRotation = Node.WorldRotation,
            StartPosition = _chain.GetBeginNode().Position,
            TargetPosition = targetPosition,
            TargetDirectionInLocalSpace = _local.TargetDirection,
            BendDirectionInNodeSpace = _bendDirection,
            BendDirectionInLocalSpace = _local.BendDirection,
            BendTarget = bendTarget,
            BendTargetWeight = _bendWeight
        };

        return CalculateBendDirectionsInternal(frameOfReference, parameters);
    }

    private string _firstBoneName = string.Empty;
    private string _secondBoneName = string.Empty;
    private string _thirdBoneName = string.Empty;

    private string _targetName = string.Empty;
    private string _bendTargetName = string.Empty;

    private float _positionWeight = 1.0f;
    private float _rotationWeight;
    private float _bendWeight = 1.0f;
    private float _minAngle;
    private float _maxAngle = 180.0f;
    private Vector3f _bendDirection = Vector3f.Forward;

    private readonly IKTrigonometricChain _chain = new();
    private WeakReference<Node>? _target;
    private WeakReference<Node>? _bendTarget;

    private struct LocalCache
    {
        public Vector3f BendDirection;
        public Vector3f TargetDirection;
    }

    private LocalCache _local;

    private Vector3f _latestTargetPosition;
}
