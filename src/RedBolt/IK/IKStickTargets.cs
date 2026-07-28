// Copyright (c) 2022-2023 the RedBolt project.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

using System;
using System.Collections.Generic;

using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.Scene;

namespace RedBolt.IK;

[Reflected(ObjectCategory.Category_IK, InheritAttributes = false)]
public partial class IKStickTargets : IKSolverComponent
{
    public const float DefaultPositionThreshold = 0.3f;
    public const float DefaultRotationThreshold = 45.0f;
    public const float DefaultTimeThreshold = 0.8f;
    public const float DefaultRecoverTime = 0.2f;


    public IKStickTargets()
    {
    }

    public override void Dispose()
    {
        base.Dispose();
    }


    public override void DrawDebugGeometry(DebugRenderer? debug, bool depthTest)
    {
    }

    /// <summary>Attributes.</summary>
    public void SetTargetNames(StringVector names)
    {
        _targetNames = new StringVector(names);
        OnTreeDirty();
    }

    public StringVector GetTargetNames()
    {
        return _targetNames;
    }


    [Serialized(
        "Is Active",
        Order = 1
    )]
    public bool IsActive { get => _isActive; set => _isActive = value; }
    [Serialized(
        "Is Position Sticky",
        Order = 2
    )]
    public bool IsPositionSticky { get => _isPositionSticky; set => _isPositionSticky = value; }
    [Serialized(
        "Is Rotation Sticky",
        Order = 3
    )]
    public bool IsRotationSticky { get => _isRotationSticky; set => _isRotationSticky = value; }
    [Serialized(
        "Position Threshold",
        Order = 4
    )]
    public float PositionThreshold { get => _positionThreshold; set => _positionThreshold = value; }
    [Serialized(
        "Rotation Threshold",
        Order = 5
    )]
    public float RotationThreshold { get => _rotationThreshold; set => _rotationThreshold = value; }
    [Serialized(
        "Time Threshold",
        Order = 6
    )]
    public float TimeThreshold { get => _timeThreshold; set => _timeThreshold = value; }
    [Serialized(
        "Recover Time",
        Order = 7
    )]
    public float RecoverTime { get => _recoverTime; set => _recoverTime = value; }
    [Serialized(
        "Min Target Distances",
        Order = 8
    )]
    public float MinTargetDistance { get => _minTargetDistance; set => _minTargetDistance = value; }
    [Serialized(
        "Max Simultaneous Recoveries",
        Order = 9
    )]
    public int MaxSimultaneousRecoveries { get => _maxSimultaneousRecoveries; set => _maxSimultaneousRecoveries = value; }
    [Serialized(
        "Base World Velocity",
        Order = 10
    )]
    public Vector3f BaseWorldVelocity { get => _baseWorldVelocity; set => _baseWorldVelocity = value; }

    /// <summary>Implement IKSolverComponent.</summary>
    protected override bool InitializeNodes(IKNodeCache nodeCache)
    {
        var targets = new List<TargetInfo>();
        for (var i = 0; i < _targetNames.Count; ++i)
        {
            var targetNode = AddCheckedNode(nodeCache, _targetNames[i]);
            if (targetNode is null)
                return false;

            targets.Add(new TargetInfo(targetNode));
        }

        _targets = targets;
        return true;
    }

    protected override void UpdateChainLengths(Transform inverseLocalFrameOfReference)
    {
    }

    protected override void SolveInternal(Transform frameOfReference, IKSettings settings, float timeStep)
    {
        CollectDesiredWorldTransforms();
        ApplyWorldMovement(timeStep);
        UpdateOverrideWeights(timeStep);
        UpdateStuckTimers(timeStep);
        ApplyDeactivation();
        ApplyActivation();
        UpdateRecovery();
        CommitWorldTransforms();
    }

    protected override void UpdateWorldOriginInternal(Vector3f delta)
    {
        for (var i = 0; i < _targets.Count; ++i)
        {
            var targetInfo = _targets[i];
            targetInfo.DesiredWorldTransform.Position -= delta;
            if (targetInfo.OverrideWorldTransform.HasValue)
            {
                var transform = targetInfo.OverrideWorldTransform.Value;
                transform.Position -= delta;
                targetInfo.OverrideWorldTransform = transform;
            }
            _targets[i] = targetInfo;
        }
    }

    private enum TargetState
    {
        Inactive,
        Stuck,
        Recovering,
    }

    private struct TargetInfo
    {
        public TargetInfo(Node node)
        {
            Node = new WeakReference<Node>(node);
        }

        public WeakReference<Node> Node;

        public TargetState State;
        public Transform DesiredWorldTransform;

        public Transform? OverrideWorldTransform;
        public float OverrideWeight;
        public float StuckTimer;

        public void Stick()
        {
            State = TargetState.Stuck;
            StuckTimer = 0.0f;
            OverrideToCurrent();
        }

        public void OverrideToCurrent()
        {
            OverrideWorldTransform = GetCurrentTransform();
            OverrideWeight = 1.0f;
        }

        public void SubtractWeight(float delta)
        {
            OverrideWeight = MathF.Max(OverrideWeight - delta, 0.0f);
        }

        public readonly bool IsEffectivelyInactive()
        {
            return State == TargetState.Inactive && OverrideWeight == 0.0f;
        }

        public readonly Transform GetCurrentTransform()
        {
            if (!OverrideWorldTransform.HasValue || OverrideWeight == 0.0f)
                return DesiredWorldTransform;
            return DesiredWorldTransform.Lerp(OverrideWorldTransform.Value, OverrideWeight);
        }

        public readonly float GetStuckPositionError()
        {
            if (!OverrideWorldTransform.HasValue || State != TargetState.Stuck)
                return 0.0f;

            return (OverrideWorldTransform.Value.Position - DesiredWorldTransform.Position).Length;
        }

        public readonly float GetStuckRotationError()
        {
            if (!OverrideWorldTransform.HasValue || State != TargetState.Stuck)
                return 0.0f;

            return (OverrideWorldTransform.Value.Rotation * DesiredWorldTransform.Rotation.Inverse).Angle;
        }

        public readonly float GetStuckTime()
        {
            if (!OverrideWorldTransform.HasValue || State != TargetState.Stuck)
                return 0.0f;

            return StuckTimer;
        }
    }

    private void CollectDesiredWorldTransforms()
    {
        for (var i = 0; i < _targets.Count; ++i)
        {
            var info = _targets[i];
            info.Node.TryGetTarget(out var node);
            info.DesiredWorldTransform = new Transform(node!.WorldPosition, node.WorldRotation);
            _targets[i] = info;
        }
    }

    private void ApplyWorldMovement(float timeStep)
    {
        for (var i = 0; i < _targets.Count; ++i)
        {
            var info = _targets[i];
            if (!info.IsEffectivelyInactive() && info.OverrideWorldTransform.HasValue)
            {
                var transform = info.OverrideWorldTransform.Value;
                transform.Position += _baseWorldVelocity * timeStep;
                info.OverrideWorldTransform = transform;
            }
            _targets[i] = info;
        }
    }

    private void UpdateOverrideWeights(float timeStep)
    {
        for (var i = 0; i < _targets.Count; ++i)
        {
            var info = _targets[i];
            if (info.State == TargetState.Inactive || info.State == TargetState.Recovering)
                info.SubtractWeight(timeStep / _recoverTime);
            _targets[i] = info;
        }
    }

    private void UpdateStuckTimers(float timeStep)
    {
        for (var i = 0; i < _targets.Count; ++i)
        {
            var info = _targets[i];
            if (info.State == TargetState.Stuck)
                info.StuckTimer += timeStep;
            _targets[i] = info;
        }
    }

    private void ApplyDeactivation()
    {
        for (var i = 0; i < _targets.Count; ++i)
        {
            var info = _targets[i];
            if (_isActive)
                continue;

            if (info.State != TargetState.Inactive)
            {
                info.State = TargetState.Inactive;
                info.OverrideToCurrent();
            }
            _targets[i] = info;
        }
    }

    private void ApplyActivation()
    {
        for (var i = 0; i < _targets.Count; ++i)
        {
            var info = _targets[i];
            if (!_isActive)
                continue;

            if (info.State == TargetState.Inactive)
                info.Stick();
            _targets[i] = info;
        }
    }

    private void UpdateRecovery()
    {
        // Stop recoveries first, count how many are still going
        var numOngoingRecoveries = 0;
        for (var i = 0; i < _targets.Count; ++i)
        {
            var info = _targets[i];
            if (info.State == TargetState.Recovering)
            {
                var isCompleted = info.OverrideWeight == 0.0f;
                var shouldAbort = _minTargetDistance > 0.0f
                    && GetDistanceToNearestStuckTarget(info.GetCurrentTransform().Position) < _minTargetDistance;

                if (isCompleted || shouldAbort)
                    info.Stick();
                else
                    ++numOngoingRecoveries;
            }
            _targets[i] = info;
        }

        // Start new recoveries
        var numTargets = _targets.Count;
        var startIndex = _recoveryStartIndex;
        for (var i = 0; i < numTargets; ++i)
        {
            // Out of budget
            if (_maxSimultaneousRecoveries > 0 && numOngoingRecoveries >= _maxSimultaneousRecoveries)
                break;

            var index = (i + startIndex) % numTargets;
            var info = _targets[index];

            var isPositionExpired = info.GetStuckPositionError() > _positionThreshold;
            var isRotationExpired = info.GetStuckRotationError() > _rotationThreshold;
            var isTimedOut = _timeThreshold > 0.0f && info.GetStuckTime() > _timeThreshold;

            if (isPositionExpired || isRotationExpired || isTimedOut)
            {
                info.State = TargetState.Recovering;
                ++numOngoingRecoveries;

                // Next time we start from the next target
                _recoveryStartIndex = i + 1;
            }
            _targets[index] = info;
        }
    }

    private void CommitWorldTransforms()
    {
        for (var i = 0; i < _targets.Count; ++i)
        {
            var info = _targets[i];
            if (info.OverrideWeight > 0.0f && info.OverrideWorldTransform.HasValue)
            {
                var transform = info.GetCurrentTransform();
                info.Node.TryGetTarget(out var node);
                if (_isPositionSticky)
                    node!.SetWorldPosition(transform.Position);
                if (_isRotationSticky)
                    node!.SetWorldRotation(transform.Rotation);
            }
        }
    }

    private float GetDistanceToNearestStuckTarget(Vector3f worldPosition)
    {
        var minDistance = float.PositiveInfinity;
        for (var i = 0; i < _targets.Count; ++i)
        {
            var info = _targets[i];
            if (info.State == TargetState.Stuck && info.OverrideWorldTransform.HasValue)
            {
                var distance = (info.OverrideWorldTransform.Value.Position - worldPosition).Length;
                minDistance = MathF.Min(minDistance, distance);
            }
        }
        return minDistance;
    }

    [Serialized(
        "Target Names",
        Setter = nameof(SetTargetNames),
        Order = 0
    )]
    private StringVector _targetNames = new StringVector();
    private bool _isActive = true;
    private bool _isPositionSticky = true;
    private bool _isRotationSticky = true;
    private float _positionThreshold = DefaultPositionThreshold;
    private float _rotationThreshold = DefaultRotationThreshold;
    private float _timeThreshold = DefaultTimeThreshold;
    private float _recoverTime = DefaultRecoverTime;
    private float _minTargetDistance;
    private int _maxSimultaneousRecoveries;
    private Vector3f _baseWorldVelocity;

    private List<TargetInfo> _targets = [];
    private int _recoveryStartIndex;
}
