// Copyright (c) 2022-2023 the RedBolt project.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.IK;

using System;

using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.Scene;

[Reflected(ObjectCategory.Category_IK, InheritAttributes = false)]
public partial class IKRotateTo : IKSolverComponent
{

    public IKRotateTo()
    {
    }

    public override void Dispose()
    {
        base.Dispose();
    }


    public override void DrawDebugGeometry(DebugRenderer? debug, bool depthTest)
    {
        if (_chain.BeginNode is not null && _chain.EndNode is not null)
        {
            DrawIKNode(debug!, _chain.BeginNode, false);
            DrawIKNode(debug!, _chain.EndNode, false);
            DrawIKSegment(debug!, _chain.BeginNode, _chain.EndNode);
        }
        if (_target is not null && _target.TryGetTarget(out _))
            DrawIKTarget(debug!, _latestTargetPosition, Quaternion.IDENTITY, false);
    }

    /// <summary>Attributes.</summary>
    [Serialized("Bone 0 Name")]
    public string FirstBoneName
    {
        get => _firstBoneName;
        set
        {
            _firstBoneName = value;
            OnTreeDirty();
        }
    }

    [Serialized("Bone 1 Name")]
    public string SecondBoneName
    {
        get => _secondBoneName;
        set
        {
            _secondBoneName = value;
            OnTreeDirty();
        }
    }

    [Serialized("Target Name")]
    public string TargetName
    {
        get => _targetName;
        set
        {
            _targetName = value;
            OnTreeDirty();
        }
    }

    [Serialized("Weight")]
    public float Weight
    {
        get => _weight;
        set => _weight = value;
    }

    /// <summary>Implement IKSolverComponent.</summary>
    protected override bool InitializeNodes(IKNodeCache nodeCache)
    {
        var target = AddCheckedNode(nodeCache, _targetName);
        _target = target is not null ? new WeakReference<Node>(target) : null;
        if (target is null)
            return false;

        var firstBone = AddSolverNode(nodeCache, _firstBoneName);
        if (firstBone is null)
            return false;

        var secondBone = AddSolverNode(nodeCache, _secondBoneName);
        if (secondBone is null)
            return false;

        SetParentAsFrameOfReference(firstBone);
        _chain = new IKNodeSegment { BeginNode = firstBone, EndNode = secondBone };
        return true;
    }

    protected override void UpdateChainLengths(Transform inverseLocalFrameOfReference)
    {
        _chain.UpdateLength();
    }

    protected override void SolveInternal(Transform frameOfReference, IKSettings settings, float timeStep)
    {
        EnsureInitialized();

        // Store original rotation
        var firstBone = _chain.BeginNode!;
        var secondBone = _chain.EndNode!;

        var firstBoneRotation = firstBone.Rotation;
        var secondBoneRotation = secondBone.Rotation;

        // Solve rotations for full solver weight
        _target!.TryGetTarget(out var target);
        _latestTargetPosition = target!.WorldPosition;

        var pos0 = firstBone.Position;
        var oldPos1 = secondBone.Position;
        var oldDirection = (oldPos1 - pos0).Normalized;
        var newDirection = (_latestTargetPosition - pos0).Normalized;
        var rotation = new Quaternion(oldDirection, newDirection);
        firstBone.RotateAround(pos0, rotation);
        secondBone.RotateAround(pos0, rotation);

        // Interpolate rotation to apply solver weight
        firstBone.Rotation = firstBoneRotation.Slerp(firstBone.Rotation, _weight);
        secondBone.Rotation = secondBoneRotation.Slerp(secondBone.Rotation, _weight);
    }

    private void EnsureInitialized()
    {
        _weight = MathDefs.Clamp(_weight, 0.0f, 1.0f);
    }

    private string _firstBoneName = string.Empty;
    private string _secondBoneName = string.Empty;

    private string _targetName = string.Empty;

    private float _weight = 1.0f;

    private IKNodeSegment _chain;
    private WeakReference<Node>? _target;

    private Vector3f _latestTargetPosition;
}
