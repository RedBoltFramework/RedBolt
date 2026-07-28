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
public partial class IKIdentitySolver : IKSolverComponent
{

    public IKIdentitySolver()
    {
    }

    public override void Dispose()
    {
        base.Dispose();
    }


    public override void DrawDebugGeometry(DebugRenderer? debug, bool depthTest)
    {
        if (_boneNode is not null)
            DrawIKNode(debug!, _boneNode, true);
        if (_target is not null && _target.TryGetTarget(out var target))
            DrawIKTarget(debug!, target, true);
    }

    public void UpdateProperties()
    {
        UpdateRotationOffset();
    }

    /// <summary>Attributes.</summary>
    [Serialized("Bone Name")]
    public string BoneName
    {
        get => _boneName;
        set
        {
            _boneName = value;
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

    // TODO: port
    // URHO3D_ACTION_STATIC_LABEL(
    //     "Update Properties", UpdateProperties, "Set properties below from current bone positions");
    [Serialized("Rotation Offset")]
    public Quaternion RotationOffset
    {
        get => _rotationOffset;
        set => _rotationOffset = value;
    }

    /// <summary>Implement IKSolverComponent.</summary>
    protected override bool InitializeNodes(IKNodeCache nodeCache)
    {
        var target = AddCheckedNode(nodeCache, _targetName);
        _target = target is not null ? new WeakReference<Node>(target) : null;
        if (target is null)
            return false;

        _boneNode = AddSolverNode(nodeCache, _boneName);
        if (_boneNode is null)
            return false;

        return true;
    }

    protected override void UpdateChainLengths(Transform inverseLocalFrameOfReference)
    {
    }

    protected override void SolveInternal(Transform frameOfReference, IKSettings settings, float timeStep)
    {
        EnsureInitialized();

        _target!.TryGetTarget(out var target);
        _boneNode!.Position = target!.WorldPosition;
        _boneNode.Rotation = target.WorldRotation * _rotationOffset;

        _boneNode.MarkPositionDirty();
        _boneNode.MarkRotationDirty();
    }


    private void EnsureInitialized()
    {
        if (_rotationOffset == Quaternion.ZERO)
            UpdateRotationOffset();
    }

    private void UpdateRotationOffset()
    {
        var boneNode = Node.GetChild(_boneName, true);
        if (boneNode is not null)
            _rotationOffset = Node.WorldRotation.Inverse * boneNode.WorldRotation;
    }

    private string _boneName = string.Empty;
    private string _targetName = string.Empty;
    private Quaternion _rotationOffset = Quaternion.ZERO;

    private IKNode? _boneNode;
    private WeakReference<Node>? _target;
}
