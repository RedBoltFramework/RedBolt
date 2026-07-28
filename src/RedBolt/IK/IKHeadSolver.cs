// Copyright (c) 2022-2023 the RedBolt project.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

using System;

using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Logging;
using RedBolt.Math;
using RedBolt.Scene;

namespace RedBolt.IK;

[Reflected(ObjectCategory.Category_IK, InheritAttributes = false)]
public partial class IKHeadSolver : IKSolverComponent
{

    public IKHeadSolver()
    {
    }

    public override void Dispose()
    {
        base.Dispose();
    }


    public override void DrawDebugGeometry(DebugRenderer? debug, bool depthTest)
    {
        var neckBone = _neckSegment.BeginNode;
        var headBone = _neckSegment.EndNode;

        if (neckBone is not null && headBone is not null)
        {
            DrawIKNode(debug!, neckBone, false);
            DrawIKNode(debug!, headBone, false);
            DrawIKSegment(debug!, neckBone, headBone);

            var eyeRay = GetEyeRay();
            DrawDirection(debug!, eyeRay.Origin, eyeRay.Direction, true, false);
        }
        if (_target is not null && _target.TryGetTarget(out var target))
            DrawIKTarget(debug!, target, true);
        if (_lookAtTarget is not null && _lookAtTarget.TryGetTarget(out var lookAtTarget))
            DrawIKTarget(debug!, lookAtTarget, false);
    }

    /// <summary>Attributes.</summary>
    [Serialized("Neck Bone Name")]
    public string NeckBoneName { get => _neckBoneName; set { _neckBoneName = value; OnTreeDirty(); } }
    [Serialized("Head Bone Name")]
    public string HeadBoneName { get => _headBoneName; set { _headBoneName = value; OnTreeDirty(); } }

    [Serialized("Target Name")]
    public string TargetName { get => _targetName; set { _targetName = value; OnTreeDirty(); } }
    [Serialized("Look At Target Name")]
    public string LookAtTargetName { get => _lookAtTargetName; set { _lookAtTargetName = value; OnTreeDirty(); } }

    [Serialized(
        "Position Weight",
        Setter = nameof(SetPositionWeightAttribute)
    )]
    public float PositionWeight { get => _positionWeight; set => _positionWeight = value; }
    [Serialized(
        "Rotation Weight",
        Setter = nameof(SetRotationWeightAttribute)
    )]
    public float RotationWeight { get => _rotationWeight; set => _rotationWeight = value; }
    [Serialized(
        "Direction Weight",
        Setter = nameof(SetDirectionWeightAttribute)
    )]
    public float DirectionWeight { get => _directionWeight; set => _directionWeight = value; }
    [Serialized(
        "Look At Weight",
        Setter = nameof(SetLookAtWeightAttribute)
    )]
    public float LookAtWeight { get => _lookAtWeight; set => _lookAtWeight = value; }
    [Serialized(
        "Eye Direction",
        Setter = nameof(SetEyeDirectionAttribute)
    )]
    public Vector3f EyeDirection { get => _eyeDirection; set => _eyeDirection = value; }
    [Serialized(
        "Eye Offset",
        Setter = nameof(SetEyeOffsetAttribute)
    )]
    public Vector3f EyeOffset { get => _eyeOffset; set => _eyeOffset = value; }
    [Serialized(
        "Neck Weight",
        Setter = nameof(SetNeckWeightAttribute)
    )]
    public float NeckWeight { get => _neckWeight; set => _neckWeight = value; }

    private void SetPositionWeightAttribute(float value)
    {
        _positionWeight = value;
        OnTreeDirty();
    }

    private void SetRotationWeightAttribute(float value)
    {
        _rotationWeight = value;
        OnTreeDirty();
    }

    private void SetDirectionWeightAttribute(float value)
    {
        _directionWeight = value;
        OnTreeDirty();
    }

    private void SetLookAtWeightAttribute(float value)
    {
        _lookAtWeight = value;
        OnTreeDirty();
    }

    private void SetEyeDirectionAttribute(Vector3f value)
    {
        _eyeDirection = value;
        OnTreeDirty();
    }

    private void SetEyeOffsetAttribute(Vector3f value)
    {
        _eyeOffset = value;
        OnTreeDirty();
    }

    private void SetNeckWeightAttribute(float value)
    {
        _neckWeight = value;
        OnTreeDirty();
    }

    /// <summary>Implement IKSolverComponent.</summary>
    protected override bool InitializeNodes(IKNodeCache nodeCache)
    {
        var target = AddCheckedNode(nodeCache, _targetName);
        _target = target is not null ? new WeakReference<Node>(target) : null;
        var lookAtTarget = AddCheckedNode(nodeCache, _lookAtTargetName);
        _lookAtTarget = lookAtTarget is not null ? new WeakReference<Node>(lookAtTarget) : null;
        if (target is null && lookAtTarget is null)
        {
            Log.GetLogger().Error("IKHeadSolver: Either head or look at target must be specified");
            return false;
        }

        var neckBone = AddSolverNode(nodeCache, _neckBoneName);
        if (neckBone is null)
            return false;

        var headBone = AddSolverNode(nodeCache, _headBoneName);
        if (headBone is null)
            return false;

        SetParentAsFrameOfReference(neckBone);
        _neckChain.Initialize(neckBone);
        _headChain.Initialize(headBone);
        _neckSegment = new IKNodeSegment { BeginNode = neckBone, EndNode = headBone };
        return true;
    }

    protected override void UpdateChainLengths(Transform inverseLocalFrameOfReference)
    {
        _neckSegment.UpdateLength();

        var neckBone = _neckSegment.BeginNode!;
        var headBone = _neckSegment.EndNode!;

        _local.DefaultNeckTransform = inverseLocalFrameOfReference
            * new Transform(neckBone.LocalOriginalPosition, neckBone.LocalOriginalRotation);
        _local.DefaultHeadTransform = inverseLocalFrameOfReference
            * new Transform(headBone.LocalOriginalPosition, headBone.LocalOriginalRotation);

        _neckChain.SetLocalEyeTransform(_eyeOffset, _eyeDirection);
        _headChain.SetLocalEyeTransform(_eyeOffset, _eyeDirection);
    }

    protected override void SolveInternal(Transform frameOfReference, IKSettings settings, float timeStep)
    {
        EnsureInitialized();

        if (_target is not null && _target.TryGetTarget(out _) && _positionWeight > 0.0f)
            SolvePosition();

        if (_target is not null && _target.TryGetTarget(out _) && _rotationWeight > 0.0f)
            SolveRotation();

        if (_target is not null && _target.TryGetTarget(out _) && _directionWeight > 0.0f)
            SolveDirection();

        if (_lookAtTarget is not null && _lookAtTarget.TryGetTarget(out _) && _lookAtWeight > 0.0f)
            SolveLookAt(frameOfReference, settings);
    }

    private void EnsureInitialized()
    {
        _positionWeight = MathDefs.Clamp(_positionWeight, 0.0f, 1.0f);
        _rotationWeight = MathDefs.Clamp(_rotationWeight, 0.0f, 1.0f);
        _directionWeight = MathDefs.Clamp(_directionWeight, 0.0f, 1.0f);
        _lookAtWeight = MathDefs.Clamp(_lookAtWeight, 0.0f, 1.0f);
        _neckWeight = MathDefs.Clamp(_neckWeight, 0.0f, 1.0f);
    }

    private void SolvePosition()
    {
        var neckBone = _neckSegment.BeginNode!;
        var headBone = _neckSegment.EndNode!;

        _target!.TryGetTarget(out var target);
        var targetPosition = target!.WorldPosition;
        var rotation = new Quaternion(headBone.Position - neckBone.Position, targetPosition - neckBone.Position);
        var scaledRotation = Quaternion.IDENTITY.Slerp(rotation, _positionWeight);

        neckBone.RotateAround(neckBone.Position, scaledRotation);
        headBone.RotateAround(neckBone.Position, scaledRotation);

        neckBone.MarkRotationDirty();
        headBone.MarkRotationDirty();
    }

    private void SolveRotation()
    {
        var headBone = _neckSegment.EndNode!;

        _target!.TryGetTarget(out var target);
        var targetRotation = target!.WorldRotation * headBone.LocalOriginalRotation;

        headBone.Rotation = headBone.Rotation.Slerp(targetRotation, _rotationWeight);

        headBone.MarkRotationDirty();
    }

    private void SolveDirection()
    {
        var headBone = _neckSegment.EndNode!;

        _target!.TryGetTarget(out var target);
        var direction = target!.WorldDirection;
        var rotation = _headChain.SolveLookTo(direction);
        var scaledRotation = Quaternion.IDENTITY.Slerp(rotation, _directionWeight);

        headBone.Rotation = scaledRotation * headBone.Rotation;

        headBone.MarkRotationDirty();
    }

    private void SolveLookAt(Transform frameOfReference, IKSettings settings)
    {
        // Store original rotation
        var neckBone = _neckSegment.BeginNode!;
        var headBone = _neckSegment.EndNode!;

        var neckBoneRotation = neckBone.Rotation;
        var headBoneRotation = headBone.Rotation;

        // Reset transforms before solving
        neckBone.Rotation = frameOfReference * _local.DefaultNeckTransform.Rotation;
        headBone.Position = frameOfReference * _local.DefaultHeadTransform.Position;
        headBone.Rotation = frameOfReference * _local.DefaultHeadTransform.Rotation;
        neckBone.StorePreviousTransform();
        headBone.StorePreviousTransform();

        _lookAtTarget!.TryGetTarget(out var lookAtTargetNode);
        var lookAtTarget = lookAtTargetNode!.WorldPosition;

        var neckRotation = _neckChain.SolveLookAt(lookAtTarget, settings);
        var neckRotationWeighted = Quaternion.IDENTITY.Slerp(neckRotation, _neckWeight);
        neckBone.Rotation = neckRotationWeighted * neckBone.Rotation;
        headBone.RotateAround(neckBone.Position, neckRotationWeighted);

        var headRotation = _headChain.SolveLookAt(lookAtTarget, settings);
        headBone.Rotation = headRotation * headBone.Rotation;

        neckBone.MarkRotationDirty();
        headBone.MarkRotationDirty();

        // Interpolate rotation to apply solver weight
        neckBone.Rotation = neckBoneRotation.Slerp(neckBone.Rotation, _lookAtWeight);
        headBone.Rotation = headBoneRotation.Slerp(headBone.Rotation, _lookAtWeight);
    }

    private Ray GetEyeRay()
    {
        var headBone = _neckSegment.EndNode!;
        var origin = headBone.Position + headBone.Rotation * _headChain.GetLocalEyeOffset();
        var direction = headBone.Rotation * _headChain.GetLocalEyeDirection();
        return new Ray(origin, direction);
    }

    private string _neckBoneName = string.Empty;
    private string _headBoneName = string.Empty;
    private string _targetName = string.Empty;
    private string _lookAtTargetName = string.Empty;

    private float _positionWeight = 1.0f;
    private float _rotationWeight;
    private float _directionWeight = 1.0f;
    private float _lookAtWeight;
    private Vector3f _eyeDirection = Vector3f.Forward;
    private Vector3f _eyeOffset;
    private float _neckWeight = 0.5f;

    private IKNodeSegment _neckSegment;
    private readonly IKEyeChain _neckChain = new();
    private readonly IKEyeChain _headChain = new();

    private WeakReference<Node>? _target;
    private WeakReference<Node>? _lookAtTarget;

    private struct LocalCache
    {
        public Transform DefaultNeckTransform;
        public Transform DefaultHeadTransform;
    }

    private LocalCache _local;
}
