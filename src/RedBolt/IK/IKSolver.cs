//
// Copyright (c) 2022-2022 the RedBolt project.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//

using System;
using System.Collections.Generic;

using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.Scene;

namespace RedBolt.IK;

[Reflected(ObjectCategory.Category_IK, InheritAttributes = false)]
[Serialized(
    "Is Enabled",
    typeof(bool),
    DefaultSource = nameof(GetEnabledDefault),
    Getter = nameof(GetEnabled),
    Setter = nameof(SetEnabled))]
public partial class IKSolver : LogicComponent
{

    public IKSolver()
    {
        SetUpdateEventMask(UpdateEvent.UsePostUpdate | UpdateEvent.UseWorldOriginUpdate);
    }

    public override void Dispose()
    {
        base.Dispose();
    }

    // TODO: port
    // URHO3D_ACTION_STATIC_LABEL("Set as origin", MarkSolversDirty,
    //     "Set current pose as original one. AnimatedModel skeleton is used if present.");

    /// <summary>Notify host component that the list of solvers is dirty and needs to be rebuilt.</summary>
    public void MarkSolversDirty()
    {
        _solversDirty = true;
    }

    /// <summary>Solve the IK forcibly.</summary>
    public void Solve(float timeStep)
    {
        if (IsChainTreeExpired())
            _solversDirty = true;

        if (_solversDirty)
        {
            _solversDirty = false;
            _solvers.Clear();
            var solvers = Node.FindComponents<IKSolverComponent>(
                ComponentSearchFlag.SelfOrChildrenRecursive | ComponentSearchFlag.Derived);
            for (var i = 0; i < solvers.Count; ++i)
                _solvers.Add(new WeakReference<IKSolverComponent>(solvers[i]));
            RebuildSolvers();
        }

        if (_solvers.Count == 0 || _solverNodes.Count == 0)
            return;

        SendIKEvent(true);

        if (_solveFromOriginal)
            UpdateOriginalTransforms();
        else
            SetOriginalTransforms();

        for (var i = 0; i < _solvers.Count; ++i)
        {
            if (_solvers[i].TryGetTarget(out var solver))
                solver.Solve(_settings, timeStep);
        }
        SendIKEvent(false);

        Node.GetComponent<AnimatedModel>()?.UpdateBoneBoundingBox();
    }

    /// <summary>Implement LogicComponent.</summary>
    public override void PostUpdate(float timeStep)
    {
        if (TryGetNode() is null)
            return;

        var scene = Scene!;

        // Cannot solve when paused if there's no AnimatedModel because it will disturb original pose.
        if (_solveWhenPaused && !Node.HasComponent<AnimatedModel>())
            _solveWhenPaused = false;

        if (scene.IsUpdateEnabled() || _solveWhenPaused)
            Solve(timeStep);
    }

    public override SceneUpdateEventType GetPostUpdateEvent()
    {
        return SceneUpdateEventType.Get<SceneEvents.SceneDrawableUpdateFinishedEvent>();
    }

    public override void UpdateWorldOrigin(IntVector3 oldOrigin, IntVector3 newOrigin, IntVector3 delta)
    {
        var entries = _solverNodes.Entries;
        for (var i = 0; i < entries.Count; ++i)
            entries[i].SolverNode.PreviousPosition -= delta.ToVector3();
        for (var i = 0; i < _solvers.Count; ++i)
        {
            if (_solvers[i].TryGetTarget(out var solver))
                solver.UpdateWorldOrigin(oldOrigin, newOrigin, delta);
        }
    }

    /// <summary>Attributes.</summary>
    [Serialized("Solve when Paused", DefaultSource = nameof(_solveWhenPaused))]
    public bool SolveWhenPaused
    {
        get => _solveWhenPaused;
        set => _solveWhenPaused = value;
    }

    [Serialized("Solve from Original", DefaultSource = nameof(_solveFromOriginal))]
    public bool SolveFromOriginal
    {
        get => _solveFromOriginal;
        set => _solveFromOriginal = value;
    }

    [Serialized("Continuous Rotation", DefaultSource = nameof(GetContinuousRotationDefault))]
    public bool ContinuousRotation
    {
        get => _settings.ContinuousRotations;
        set => _settings.ContinuousRotations = value;
    }

    /// <summary>Find bone data by Node.</summary>
    public IKNode? GetNodeData(Node node)
    {
        return _solverNodes.Get(node);
    }

    protected override void OnNodeSet(Node? previousNode, Node? currentNode)
    {
        if (previousNode is not null)
            previousNode.GetComponent<IKSolver>()?.MarkSolversDirty();
        if (currentNode is not null)
            currentNode.GetComponent<IKSolver>()?.MarkSolversDirty();
    }

    private static bool GetEnabledDefault() => true;

    private static bool GetContinuousRotationDefault() => false;

    private bool GetEnabled() => Enabled;

    private bool IsChainTreeExpired()
    {
        if (_solvers.Count != 0 && _solverNodes.Count == 0)
            return true;

        var entries = _solverNodes.Entries;
        for (var i = 0; i < entries.Count; ++i)
        {
            if (!entries[i].Node.TryGetTarget(out _))
                return true;
        }
        return false;
    }

    private void RebuildSolvers()
    {
        _solverNodes.Clear();
        if (_solvers.Count == 0)
            return;

        var solverNodes = new IKNodeCache();
        for (var i = 0; i < _solvers.Count; ++i)
        {
            if (!_solvers[i].TryGetTarget(out var solver) || !solver.Initialize(solverNodes))
                return;
        }

        _solverNodes = solverNodes;

        // Reset skeleton on initialization to get true initial pose.
        var animationController = Node.GetComponent<AnimationController>();
        if (animationController is not null && animationController.IsSkeletonReset())
            GetComponent<AnimatedModel>()?.GetSkeleton().Reset();

        SetOriginalTransforms();

        for (var i = 0; i < _solvers.Count; ++i)
        {
            if (_solvers[i].TryGetTarget(out var solver))
                solver.NotifyPositionsReady();
        }
    }

    private void SetOriginalTransforms()
    {
        var worldTransform = Node.GetWorldTransform();
        var entries = _solverNodes.Entries;
        for (var i = 0; i < entries.Count; ++i)
        {
            var entry = entries[i];
            if (entry.Node.TryGetTarget(out var node))
            {
                var localTransform = IKSolverComponent.GetParentTransform(node, Node);
                entry.SolverNode.SetOriginalTransform(localTransform.Translation, localTransform.Rotation, worldTransform);
            }
        }
    }

    private void UpdateOriginalTransforms()
    {
        var worldTransform = Node.GetWorldTransform();
        var entries = _solverNodes.Entries;
        for (var i = 0; i < entries.Count; ++i)
            entries[i].SolverNode.UpdateOriginalTransform(worldTransform);
    }

    private void SendIKEvent(bool preSolve)
    {
        if (preSolve)
        {
            var eventData = new IKEvents.IKPreSolveEvent
            {
                Node = Node,
                IKSolver = this
            };
            Node.SendEvent(ref eventData);
        }
        else
        {
            var eventData = new IKEvents.IKPostSolveEvent
            {
                Node = Node,
                IKSolver = this
            };
            Node.SendEvent(ref eventData);
        }
    }

    private bool _solveWhenPaused;
    private bool _solveFromOriginal = true;
    private IKSettings _settings = new();

    private bool _solversDirty;

    private readonly List<WeakReference<IKSolverComponent>> _solvers = [];

    private IKNodeCache _solverNodes = new();
}
