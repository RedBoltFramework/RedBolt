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
[Serialized(
    "Bone Names",
    typeof(StringVector),
    DefaultSource = nameof(GetDefaultBoneNames),
    Getter = nameof(GetBoneNames),
    Setter = nameof(SetBoneNames))]
public partial class IKChainSolver : IKSolverComponent
{

    public IKChainSolver()
    {
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

        if (_target is not null && _target.TryGetTarget(out var target))
            DrawIKTarget(debug!, target, false);
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
    public string TargetName
    {
        get => _targetName;
        set { _targetName = value; OnTreeDirty(); }
    }

    /// <summary>Implement IKSolverComponent.</summary>
    protected override bool InitializeNodes(IKNodeCache nodeCache)
    {
        var target = AddCheckedNode(nodeCache, _targetName);
        _target = target is not null ? new WeakReference<Node>(target) : null;
        if (target is null)
            return false;

        if (_boneNames.Count < 2)
            return false;

        var chain = new IKFabrikChain();
        for (var i = 0; i < _boneNames.Count; ++i)
        {
            var bone = AddSolverNode(nodeCache, _boneNames[i]);
            if (bone is null)
                return false;

            chain.AddNode(bone);
        }

        _chain = chain;
        return true;
    }

    protected override void UpdateChainLengths(Transform inverseLocalFrameOfReference)
    {
        _chain.UpdateLengths();
    }

    protected override void SolveInternal(Transform frameOfReference, IKSettings settings, float timeStep)
    {
        _target!.TryGetTarget(out var target);
        _chain.Solve(target!.WorldPosition, settings);
    }

    private StringVector _boneNames = new StringVector();
    private string _targetName = string.Empty;

    private IKFabrikChain _chain = new();
    private WeakReference<Node>? _target;
}
