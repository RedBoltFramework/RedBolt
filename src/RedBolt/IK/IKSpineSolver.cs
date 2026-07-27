// Copyright (c) 2022-2023 the RedBolt project.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

using System;
using System.Collections.Generic;

using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Logging;
using RedBolt.Math;
using RedBolt.Scene;

namespace RedBolt.IK;

[Reflected(ObjectCategory.Category_IK, InheritAttributes = false)]
[Serialized(
    "Bone Names",
    typeof(StringVector),
    DefaultSource = nameof(GetDefaultBoneNames),
    Getter = nameof(GetBoneNames),
    Setter = nameof(SetBoneNames))]
public partial class IKSpineSolver : IKSolverComponent
{

    public IKSpineSolver()
    {
        _weightFunction = WeightFunction;
    }

    public override void Dispose()
    {
        base.Dispose();
    }


    public override void DrawDebugGeometry(DebugRenderer? debug, bool depthTest)
    {
        var segments = _chain.GetSegments();
        for (var i = 0; i < segments.Count; ++i)
        {
            var segment = segments[i];
            var isLastSegment = i == segments.Count - 1;
            DrawIKNode(debug!, segment.BeginNode!, isLastSegment);
            DrawIKSegment(debug!, segment.BeginNode!, segment.EndNode!);
            if (isLastSegment)
                DrawIKNode(debug!, segment.EndNode!, false);
        }

        if (_twistTarget is not null && _twistTarget.TryGetTarget(out var twistTarget))
            DrawIKTarget(debug!, twistTarget, true);
        if (_target is not null && _target.TryGetTarget(out var target))
            DrawIKTarget(debug!, target, false);
    }

    public void UpdateProperties()
    {
        UpdateTwistRotationOffset();
    }

    /// <summary>Attributes.</summary>
    public void SetBoneNames(StringVector names)
    {
        _boneNames = new StringVector(names);
        OnTreeDirty();
    }

    public StringVector GetBoneNames()
    {
        return _boneNames;
    }

    private static StringVector GetDefaultBoneNames() => new();

    [Serialized("Target Name", DefaultSource = nameof(_targetName))]
    public string TargetName { get => _targetName; set { _targetName = value; OnTreeDirty(); } }
    [Serialized("Twist Target Name", DefaultSource = nameof(_twistTargetName))]
    public string TwistTargetName { get => _twistTargetName; set { _twistTargetName = value; OnTreeDirty(); } }

    [Serialized("Position Weight", DefaultSource = nameof(_positionWeight))]
    public float PositionWeight { get => _positionWeight; set => _positionWeight = value; }
    [Serialized("Rotation Weight", DefaultSource = nameof(_rotationWeight))]
    public float RotationWeight { get => _rotationWeight; set => _rotationWeight = value; }
    [Serialized("Twist Weight", DefaultSource = nameof(_twistWeight))]
    public float TwistWeight { get => _twistWeight; set => _twistWeight = value; }
    [Serialized("Max Angle", DefaultSource = nameof(_maxAngle))]
    public float MaxAngle { get => _maxAngle; set => _maxAngle = value; }
    [Serialized("Bend Tweak", DefaultSource = nameof(_bendTweak))]
    public float BendTweak { get => _bendTweak; set => _bendTweak = value; }

    /// <summary>Implement IKSolverComponent.</summary>
    protected override bool InitializeNodes(IKNodeCache nodeCache)
    {
        var target = AddCheckedNode(nodeCache, _targetName);
        _target = target is not null ? new WeakReference<Node>(target) : null;
        if (target is null)
            return false;

        if (_boneNames.Count < 2)
        {
            Log.GetLogger().Error("Spine solver must have at least 2 bones");
            return false;
        }

        var chain = new IKSpineChain();
        for (var i = 0; i < _boneNames.Count; ++i)
        {
            var bone = AddSolverNode(nodeCache, _boneNames[i]);
            if (bone is null)
                return false;

            chain.AddNode(bone);
        }

        if (!string.IsNullOrEmpty(_twistTargetName))
        {
            var twistTarget = AddCheckedNode(nodeCache, _twistTargetName);
            _twistTarget = twistTarget is not null ? new WeakReference<Node>(twistTarget) : null;
            if (twistTarget is null)
                return false;
        }

        SetParentAsFrameOfReference(chain.GetNodes()[0]);
        _chain = chain;
        return true;
    }

    protected override void UpdateChainLengths(Transform inverseLocalFrameOfReference)
    {
        if (_twistRotationOffset == Quaternion.ZERO)
            UpdateTwistRotationOffset();

        _chain.UpdateLengths();

        var bones = _chain.GetNodes();
        while (_local.DefaultTransforms.Count < bones.Count)
            _local.DefaultTransforms.Add(new Transform());
        if (_local.DefaultTransforms.Count > bones.Count)
            _local.DefaultTransforms.RemoveRange(bones.Count, _local.DefaultTransforms.Count - bones.Count);
        for (var i = 0; i < bones.Count; ++i)
        {
            var bone = bones[i];
            _local.DefaultTransforms[i] = inverseLocalFrameOfReference
                * new Transform(bone.LocalOriginalPosition, bone.LocalOriginalRotation);
        }

        var baseDirection = (bones[1].LocalOriginalPosition - bones[0].LocalOriginalPosition).Normalized;
        _local.BaseDirection = inverseLocalFrameOfReference.Rotation * baseDirection;
        _local.ZeroTwistRotation = inverseLocalFrameOfReference.Rotation * _twistRotationOffset;
    }

    protected override void SolveInternal(Transform frameOfReference, IKSettings settings, float timeStep)
    {
        EnsureInitialized();

        var bones = _chain.GetNodes();
        if (bones.Count < 2)
            return;

        // Store original rotation
        while (_originalBoneRotations.Count < bones.Count)
            _originalBoneRotations.Add(Quaternion.IDENTITY);
        if (_originalBoneRotations.Count > bones.Count)
            _originalBoneRotations.RemoveRange(bones.Count, _originalBoneRotations.Count - bones.Count);
        for (var i = 0; i < bones.Count; ++i)
            _originalBoneRotations[i] = bones[i].Rotation;

        // Solve rotations for full solver weight for position target
        SetOriginalTransforms(frameOfReference);
        var baseDirection = frameOfReference.Rotation * _local.BaseDirection;
        _target!.TryGetTarget(out var target);
        _chain.Solve(target!.WorldPosition, baseDirection, _maxAngle, settings, _weightFunction);

        // Interpolate rotation to apply solver weight
        for (var i = 0; i < bones.Count; ++i)
            bones[i].Rotation = _originalBoneRotations[i].Slerp(bones[i].Rotation, _positionWeight);

        // Solve rotations for partial solver weight for twist target
        var twistAngle = _twistTarget is not null && _twistTarget.TryGetTarget(out var twistTarget)
            ? GetTwistAngle(frameOfReference, _chain.GetSegments()[^1], twistTarget)
            : 0.0f;
        _chain.Twist(twistAngle * _twistWeight, settings);

        // Apply target rotation if needed
        if (_rotationWeight > 0.0f)
        {
            var lastBone = bones[^1];
            var targetRotation = target.WorldRotation * lastBone.LocalOriginalRotation;
            lastBone.Rotation = lastBone.Rotation.Slerp(targetRotation, _rotationWeight);
        }
    }

    private void EnsureInitialized()
    {
        if (_twistRotationOffset == Quaternion.ZERO)
            UpdateTwistRotationOffset();

        _positionWeight = MathDefs.Clamp(_positionWeight, 0.0f, 1.0f);
        _rotationWeight = MathDefs.Clamp(_rotationWeight, 0.0f, 1.0f);
        _twistWeight = MathDefs.Clamp(_twistWeight, 0.0f, 1.0f);
        _maxAngle = MathDefs.Clamp(_maxAngle, 0.0f, 180.0f);
    }

    private void UpdateTwistRotationOffset()
    {
        if (_boneNames.Count >= 2)
        {
            var twistBoneName = _boneNames[^2];
            var boneNode = Node.GetChild(twistBoneName, true);
            if (boneNode is not null)
                _twistRotationOffset = Node.WorldRotation.Inverse * boneNode.WorldRotation;
        }
    }

    private float WeightFunction(float x)
    {
        if (_bendTweak == 0.0f)
            return 1.0f;
        if (_bendTweak > 0.0f)
        {
            if (_bendTweak < 1.0f)
                return MathDefs.Lerp(1.0f - _bendTweak, 1.0f, x);
            return MathDefs.Pow(x, _bendTweak);
        }

        if (_bendTweak > -1.0f)
            return MathDefs.Lerp(1.0f - -_bendTweak, 1.0f, 1.0f - x);
        return MathDefs.Pow(1.0f - x, -_bendTweak);
    }

    private void SetOriginalTransforms(Transform frameOfReference)
    {
        var nodes = _chain.GetNodes();
        for (var i = 0; i < nodes.Count; ++i)
        {
            var bone = nodes[i];
            var defaultTransform = _local.DefaultTransforms[i];
            bone.Position = frameOfReference * defaultTransform.Position;
            bone.Rotation = frameOfReference * defaultTransform.Rotation;
        }
    }

    private float GetTwistAngle(Transform frameOfReference, IKNodeSegment segment, Node targetNode)
    {
        var zeroTwistBoneRotation = frameOfReference.Rotation * _local.ZeroTwistRotation;
        var targetBoneRotation = targetNode.WorldRotation * segment.BeginNode!.LocalOriginalRotation;
        var deltaRotation = targetBoneRotation * zeroTwistBoneRotation.Inverse;

        var direction = (segment.EndNode!.Position - segment.BeginNode.Position).Normalized;
        var (_, twist) = deltaRotation.ToSwingTwist(direction);
        var angle = twist.Angle;
        var sign = twist.Axis.DotProduct(direction) > 0.0f ? 1.0f : -1.0f;
        return sign * (angle > 180.0f ? angle - 360.0f : angle);
    }

    private static Quaternion GetDefaultTwistRotationOffset() => Quaternion.ZERO;

    private StringVector _boneNames = new StringVector();
    private string _twistTargetName = string.Empty;
    private string _targetName = string.Empty;

    private float _positionWeight = 1.0f;
    private float _rotationWeight;
    private float _twistWeight = 1.0f;
    private float _maxAngle = 90.0f;
    private float _bendTweak;
    // TODO: port
    // URHO3D_ACTION_STATIC_LABEL(
    //     "Update Properties", UpdateProperties, "Set properties below from current bone positions");
    /// <summary>This orientation of twist bone in object space is equivalent to having no twist.</summary>
    [Serialized("Twist Rotation Offset", DefaultSource = nameof(GetDefaultTwistRotationOffset))]
    private Quaternion _twistRotationOffset = Quaternion.ZERO;

    private IKSpineChain _chain = new();
    private WeakReference<Node>? _target;
    private WeakReference<Node>? _twistTarget;

    // TODO: Rename "local cache"
    private struct LocalCache
    {
        public LocalCache()
        {
        }

        public List<Transform> DefaultTransforms = [];
        public Vector3f BaseDirection;
        public Quaternion ZeroTwistRotation;
    }

    private LocalCache _local = new();

    private readonly List<Quaternion> _originalBoneRotations = [];
    private readonly IKSpineChain.WeightFunction _weightFunction;
}
