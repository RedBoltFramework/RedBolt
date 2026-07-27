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
public partial class IKLegSolver : IKSolverComponent
{

    public IKLegSolver()
    {
    }

    public override void Dispose()
    {
        base.Dispose();
    }


    public void UpdateProperties()
    {
        UpdateHeelGroundOffset();
    }

    public override void DrawDebugGeometry(DebugRenderer? debug, bool depthTest)
    {
        var thighBone = _legChain.GetBeginNode();
        var calfBone = _legChain.GetMiddleNode();
        var heelBone = _legChain.GetEndNode();
        var toeBone = _footSegment.EndNode;

        if (thighBone is not null && calfBone is not null && heelBone is not null)
        {
            DrawIKNode(debug!, thighBone, false);
            DrawIKNode(debug!, calfBone, false);
            DrawIKNode(debug!, heelBone, false);
            DrawIKSegment(debug!, thighBone, calfBone);
            DrawIKSegment(debug!, calfBone, heelBone);

            var currentBendDirection = _legChain.GetCurrentChainRotation() * Node.WorldRotation * _bendDirection;
            DrawDirection(debug!, calfBone.Position, currentBendDirection);
        }
        if (heelBone is not null && toeBone is not null)
        {
            DrawIKNode(debug!, toeBone, false);
            DrawIKSegment(debug!, heelBone, toeBone);
        }
        if (_target is not null && _target.TryGetTarget(out _))
            DrawIKTarget(debug!, _latestTargetPosition, Quaternion.IDENTITY, false);
        if (_heelTarget is not null && _heelTarget.TryGetTarget(out var heelTarget))
            DrawIKTarget(debug!, heelTarget, false);
        if (_bendTarget is not null && _bendTarget.TryGetTarget(out var bendTarget))
            DrawIKTarget(debug!, bendTarget, false);

        Node? groundNode = null;
        _groundTarget?.TryGetTarget(out groundNode);
        groundNode ??= Node;
        var groundBox = new BoundingBox(new Vector3f(-0.5f, -0.2f, -0.5f), new Vector3f(0.5f, 0.0f, 0.5f));
        var groundTransform = groundNode.GetWorldTransform();
        debug!.AddBoundingBox(groundBox, groundTransform, Color.Green, false);
    }

    /// <summary>Attributes.</summary>
    [Serialized("Thigh Bone Name", DefaultSource = nameof(_thighBoneName))]
    public string ThighBoneName { get => _thighBoneName; set { _thighBoneName = value; OnTreeDirty(); } }
    [Serialized("Calf Bone Name", DefaultSource = nameof(_calfBoneName))]
    public string CalfBoneName { get => _calfBoneName; set { _calfBoneName = value; OnTreeDirty(); } }
    [Serialized("Heel Bone Name", DefaultSource = nameof(_heelBoneName))]
    public string HeelBoneName { get => _heelBoneName; set { _heelBoneName = value; OnTreeDirty(); } }
    [Serialized("Toe Bone Name", DefaultSource = nameof(_toeBoneName))]
    public string ToeBoneName { get => _toeBoneName; set { _toeBoneName = value; OnTreeDirty(); } }

    [Serialized("Target Name", DefaultSource = nameof(_targetName))]
    public string TargetName { get => _targetName; set { _targetName = value; OnTreeDirty(); } }
    [Serialized("Heel Target Name", DefaultSource = nameof(_heelTargetName))]
    public string HeelTargetName { get => _heelTargetName; set { _heelTargetName = value; OnTreeDirty(); } }
    [Serialized("Bend Target Name", DefaultSource = nameof(_bendTargetName))]
    public string BendTargetName { get => _bendTargetName; set { _bendTargetName = value; OnTreeDirty(); } }
    [Serialized("Ground Target Name", DefaultSource = nameof(_groundTargetName))]
    public string GroundTargetName { get => _groundTargetName; set { _groundTargetName = value; OnTreeDirty(); } }

    [Serialized("Position Weight", DefaultSource = nameof(_positionWeight))]
    public float PositionWeight { get => _positionWeight; set => _positionWeight = value; }
    [Serialized("Rotation Weight", DefaultSource = nameof(_rotationWeight))]
    public float RotationWeight { get => _rotationWeight; set => _rotationWeight = value; }
    [Serialized("Heel Weight", DefaultSource = nameof(_heelWeight))]
    public float HeelWeight { get => _heelWeight; set => _heelWeight = value; }
    [Serialized("Bend Weight", DefaultSource = nameof(_bendWeight))]
    public float BendWeight { get => _bendWeight; set => _bendWeight = value; }
    [Serialized("Min Angle", DefaultSource = nameof(_minKneeAngle))]
    public float MinAngle { get => _minKneeAngle; set => _minKneeAngle = value; }
    [Serialized("Max Angle", DefaultSource = nameof(_maxKneeAngle))]
    public float MaxAngle { get => _maxKneeAngle; set => _maxKneeAngle = value; }
    [Serialized("Tiptoe Factor", DefaultSource = nameof(_tiptoeFactor))]
    public float TiptoeFactor { get => _tiptoeFactor; set => _tiptoeFactor = value; }
    [Serialized("Bend Direction", DefaultSource = nameof(_bendDirection))]
    public Vector3f BendDirection { get => _bendDirection; set => _bendDirection = value; }

    // TODO: port
    // URHO3D_ACTION_STATIC_LABEL(
    //     "Update Properties", UpdateProperties, "Set properties below from current bone positions");
    [Serialized("Heel Ground Offset", DefaultSource = nameof(_heelGroundOffset))]
    public float HeelGroundOffset { get => _heelGroundOffset; set => _heelGroundOffset = value; }

    /// <summary>Implement IKSolverComponent.</summary>
    protected override bool InitializeNodes(IKNodeCache nodeCache)
    {
        var target = AddCheckedNode(nodeCache, _targetName);
        _target = target is not null ? new WeakReference<Node>(target) : null;
        if (target is null)
            return false;

        var heelTarget = AddCheckedNode(nodeCache, _heelTargetName);
        _heelTarget = heelTarget is not null ? new WeakReference<Node>(heelTarget) : null;
        var bendTarget = AddCheckedNode(nodeCache, _bendTargetName);
        _bendTarget = bendTarget is not null ? new WeakReference<Node>(bendTarget) : null;
        var groundTarget = AddCheckedNode(nodeCache, _groundTargetName);
        _groundTarget = groundTarget is not null ? new WeakReference<Node>(groundTarget) : null;

        var thighBone = AddSolverNode(nodeCache, _thighBoneName);
        if (thighBone is null)
            return false;

        var calfBone = AddSolverNode(nodeCache, _calfBoneName);
        if (calfBone is null)
            return false;

        var heelBone = AddSolverNode(nodeCache, _heelBoneName);
        if (heelBone is null)
            return false;

        var toeBone = AddSolverNode(nodeCache, _toeBoneName);
        if (toeBone is null)
            return false;

        SetParentAsFrameOfReference(thighBone);
        _legChain.Initialize(thighBone, calfBone, heelBone);
        _footSegment = new IKNodeSegment { BeginNode = heelBone, EndNode = toeBone };
        return true;
    }

    protected override void UpdateChainLengths(Transform inverseLocalFrameOfReference)
    {
        _legChain.UpdateLengths();
        _footSegment.UpdateLength();

        var thighBone = _legChain.GetBeginNode();
        var calfBone = _legChain.GetMiddleNode();
        var heelBone = _legChain.GetEndNode();
        var toeBone = _footSegment.EndNode!;

        _local.ToeToHeel = heelBone.LocalOriginalPosition - toeBone.LocalOriginalPosition;
        _local.DefaultThighToToeDistance = (toeBone.LocalOriginalPosition - thighBone.LocalOriginalPosition).Length;

        _local.BendDirection = inverseLocalFrameOfReference.Rotation * _bendDirection;
        _local.TargetDirection = inverseLocalFrameOfReference.Rotation
            * (_legChain.GetEndNode().LocalOriginalPosition - _legChain.GetBeginNode().LocalOriginalPosition).Normalized;
        _local.DefaultFootRotation = calfBone.LocalOriginalRotation.Inverse * heelBone.LocalOriginalRotation;
        _local.DefaultToeOffset = heelBone.LocalOriginalRotation.Inverse
            * (toeBone.LocalOriginalPosition - heelBone.LocalOriginalPosition);
        _local.DefaultToeRotation = heelBone.LocalOriginalRotation.Inverse * toeBone.LocalOriginalRotation;
        _local.ToeRotation = inverseLocalFrameOfReference.Rotation * toeBone.LocalOriginalRotation;
    }

    protected override void SolveInternal(Transform frameOfReference, IKSettings settings, float timeStep)
    {
        EnsureInitialized();

        // Store original rotation
        var thighBone = _legChain.GetBeginNode();
        var calfBone = _legChain.GetMiddleNode();
        var heelBone = _legChain.GetEndNode();
        var toeBone = _footSegment.EndNode!;

        var thighBoneRotation = thighBone.Rotation;
        var calfBoneRotation = calfBone.Rotation;
        var heelBoneRotation = heelBone.Rotation;
        var toeBoneRotation = toeBone.Rotation;

        // Solve rotations for full solver weight
        _latestTargetPosition = GetTargetPosition();

        var (originalDirection, currentDirection) = CalculateBendDirections(frameOfReference, _latestTargetPosition);
        var footRotation = Quaternion.IDENTITY;
        var toeToHeel = CalculateToeToHeel(frameOfReference, _tiptoeFactor, _latestTargetPosition,
            originalDirection, currentDirection, footRotation);
        var heelTargetPosition = _latestTargetPosition + toeToHeel;

        _legChain.Solve(heelTargetPosition, originalDirection, currentDirection, _minKneeAngle, _maxKneeAngle);
        RotateFoot(toeToHeel, footRotation);

        // Interpolate rotation to apply solver weight
        thighBone.Rotation = thighBoneRotation.Slerp(thighBone.Rotation, _positionWeight);
        calfBone.Rotation = calfBoneRotation.Slerp(calfBone.Rotation, _positionWeight);
        heelBone.Rotation = heelBoneRotation.Slerp(heelBone.Rotation, _positionWeight);
        toeBone.Rotation = toeBoneRotation.Slerp(toeBone.Rotation, _positionWeight);

        // Apply target rotation if needed
        if (_rotationWeight > 0.0f)
        {
            _target!.TryGetTarget(out var target);
            var targetRotation = target!.WorldRotation * toeBone.LocalOriginalRotation;
            toeBone.Rotation = toeBone.Rotation.Slerp(targetRotation, _rotationWeight);
        }
    }

    private void EnsureInitialized()
    {
        if (_heelGroundOffset < 0.0f)
            UpdateHeelGroundOffset();

        _positionWeight = MathDefs.Clamp(_positionWeight, 0.0f, 1.0f);
        _rotationWeight = MathDefs.Clamp(_rotationWeight, 0.0f, 1.0f);
        _heelWeight = MathDefs.Clamp(_heelWeight, 0.0f, 1.0f);
        _bendWeight = MathDefs.Clamp(_bendWeight, 0.0f, 1.0f);
        _minKneeAngle = MathDefs.Clamp(_minKneeAngle, 0.0f, 180.0f);
        _maxKneeAngle = MathDefs.Clamp(_maxKneeAngle, 0.0f, 180.0f);
        _tiptoeFactor = MathDefs.Clamp(_tiptoeFactor, 0.0f, 1.0f);
    }

    private void UpdateHeelGroundOffset()
    {
        var heelNode = Node.GetChild(_heelBoneName, true);
        if (heelNode is not null)
        {
            var heelOffset = heelNode.WorldPosition - Node.WorldPosition;
            _heelGroundOffset = MathF.Max(0.0f, heelOffset.Y);
        }
    }

    private void RotateFoot(Vector3f toeToHeel, Quaternion footRotation)
    {
        var calfBone = _legChain.GetMiddleNode();
        var heelBone = _legChain.GetEndNode();
        var toeBone = _footSegment.EndNode!;

        // heelBone.position should already set by legChain_.Solve()
        heelBone.PreviousPosition = heelBone.Position;
        heelBone.PreviousRotation = footRotation * calfBone.Rotation * _local.DefaultFootRotation;
        toeBone.PreviousPosition = heelBone.PreviousPosition + heelBone.PreviousRotation * _local.DefaultToeOffset;
        toeBone.PreviousRotation = heelBone.PreviousRotation * _local.DefaultToeRotation;
        toeBone.Position = heelBone.Position - toeToHeel;

        _footSegment.UpdateRotationInNodes(true, true);
    }

    private Vector3f GetTargetPosition()
    {
        var firstBone = _legChain.GetBeginNode();

        const float minDistance = 0.001f;
        var maxDistance = GetToeReachDistance();
        var origin = firstBone.Position;
        _target!.TryGetTarget(out var targetNode);
        var target = targetNode!.WorldPosition;
        return origin + (target - origin).ReNormalized(minDistance, maxDistance);
    }

    private Plane GetGroundPlane()
    {
        Node? groundNode = null;
        _groundTarget?.TryGetTarget(out groundNode);
        groundNode ??= Node;
        return new Plane(groundNode.WorldUp, groundNode.WorldPosition);
    }

    private Vector2f ProjectOnGround(Vector3f position)
    {
        Node? groundNode = null;
        _groundTarget?.TryGetTarget(out groundNode);
        groundNode ??= Node;
        var right = groundNode.WorldRotation * Vector3f.Right;
        var forward = groundNode.WorldRotation * Vector3f.Forward;
        var localPos = position - groundNode.WorldPosition;
        return new Vector2f(right.DotProduct(localPos), forward.DotProduct(localPos));
    }

    private float GetHeelReachDistance()
    {
        return GetMaxDistance(_legChain, _maxKneeAngle);
    }

    private float GetToeReachDistance()
    {
        return GetHeelReachDistance() + _footSegment.Length;
    }

    private Vector3f RecoverFromGroundPenetration(Vector3f toeToHeel, Vector3f toePosition)
    {
        var groundPlane = GetGroundPlane();
        var yAxis = groundPlane.Normal;
        var xAxis = toeToHeel.Orthogonalize(yAxis);

        // Decompose the vector into vertical and horizontal components relative to ground normal
        //
        //      o-heel
        //     / } (x,y)
        //    o-toe
        //    | } y0
        // ___|_____
        var x = xAxis.DotProduct(toeToHeel);
        var y = yAxis.DotProduct(toeToHeel);
        var y0 = groundPlane.Distance(toePosition);

        // Clamp heel y to the minimum distance from the ground
        var len = _footSegment.Length;
        var y2 = MathF.Min(MathF.Max(y, _heelGroundOffset - y0), len);
        var x2 = MathF.Sqrt(MathF.Max(len * len - y2 * y2, 0.0f)) * (x < 0.0f ? -1.0f : 1.0f);

        return xAxis * x2 + yAxis * y2;
    }

    private Vector3f SnapToReachablePosition(Vector3f toeToHeel, Vector3f toePosition)
    {
        var thighBone = _legChain.GetBeginNode();
        var reachableSphere = new Sphere(thighBone.Position, GetHeelReachDistance());
        if (reachableSphere.IsInside(toePosition + toeToHeel) != Intersection.Outside)
            return toeToHeel;

        var availableSphere = new Sphere(toePosition, toeToHeel.Length);
        var availableHeelPositions = reachableSphere.Intersect(availableSphere);

        var heelPosition = availableHeelPositions.GetPoint(toeToHeel);
        return heelPosition - toePosition;
    }

    private (Vector3f OriginalDirection, Vector3f CurrentDirection) CalculateBendDirections(
        Transform frameOfReference, Vector3f toeTargetPosition)
    {
        var parameters = new BendCalculationParams
        {
            ParentNodeRotation = Node.WorldRotation,
            StartPosition = _legChain.GetBeginNode().Position,
            TargetPosition = toeTargetPosition,
            TargetDirectionInLocalSpace = _local.TargetDirection,
            BendDirectionInNodeSpace = _bendDirection,
            BendDirectionInLocalSpace = _local.BendDirection,
            BendTarget = _bendTarget is not null && _bendTarget.TryGetTarget(out var bendTarget) ? bendTarget : null,
            BendTargetWeight = _bendWeight
        };

        return CalculateBendDirectionsInternal(frameOfReference, parameters);
    }

    private Quaternion CalculateLegRotation(
        Vector3f toeTargetPosition, Vector3f originalDirection, Vector3f currentDirection)
    {
        var thighBone = _legChain.GetBeginNode();
        var toeBone = _footSegment.EndNode!;

        return IKTrigonometricChain.CalculateRotation(thighBone.OriginalPosition,
            toeBone.OriginalPosition, originalDirection, thighBone.Position, toeTargetPosition, currentDirection);
    }

    // TODO: port
    // Vector2 CalculateLegOffset(
    //     const Transform& frameOfReference, const Vector3& toeTargetPosition, const Vector3& bendDirection);

    private Vector3f CalculateToeToHeel(Transform frameOfReference, float tiptoeFactor,
        Vector3f toeTargetPosition, Vector3f originalDirection, Vector3f currentDirection, Quaternion footRotation)
    {
        var legRotation = CalculateLegRotation(toeTargetPosition, originalDirection, currentDirection);
        var approximateBendDirection = legRotation * originalDirection;

        var toeToHeelMin = footRotation * legRotation * Node.WorldRotation * _local.ToeToHeel;
        var toeToHeelMax = CalculateToeToHeelBent(toeTargetPosition, approximateBendDirection);
        var toeToHeelAuto = toeToHeelMin.Lerp(toeToHeelMax, tiptoeFactor).Normalized * _footSegment.Length;

        // If heel target is provided, use it
        var toeToHeelTarget = toeToHeelAuto;
        var heelTarget = _heelTarget is not null && _heelTarget.TryGetTarget(out var target) ? target : null;
        if (heelTarget is not null)
        {
            var heelTargetPosition = heelTarget.WorldPosition;
            toeToHeelTarget = (heelTargetPosition - toeTargetPosition).Normalized * _footSegment.Length;
        }

        var toeToHeel = toeToHeelAuto.Lerp(toeToHeelTarget, _heelWeight).Normalized * _footSegment.Length;
        return SnapToReachablePosition(RecoverFromGroundPenetration(toeToHeel, toeTargetPosition), toeTargetPosition);
    }

    private Vector3f CalculateToeToHeelBent(Vector3f toeTargetPosition, Vector3f approximateBendDirection)
    {
        var thighBone = _legChain.GetBeginNode();
        var (newPos1, newPos2) = IKTrigonometricChain.Solve(thighBone.Position, _legChain.GetFirstLength(),
            _legChain.GetSecondLength() + _footSegment.Length, toeTargetPosition, approximateBendDirection,
            _minKneeAngle, _maxKneeAngle);
        return (newPos1 - newPos2).Normalized * _footSegment.Length;
    }


    /// <summary>Attributes.</summary>
    private string _thighBoneName = string.Empty;
    private string _calfBoneName = string.Empty;
    private string _heelBoneName = string.Empty;
    private string _toeBoneName = string.Empty;

    private string _targetName = string.Empty;
    private string _heelTargetName = string.Empty;
    private string _bendTargetName = string.Empty;
    private string _groundTargetName = string.Empty;

    private float _positionWeight = 1.0f;
    private float _rotationWeight;
    private float _heelWeight = 1.0f;
    private float _bendWeight = 1.0f;
    private float _minKneeAngle;
    private float _maxKneeAngle = 180.0f;
    private float _tiptoeFactor;
    private Vector3f _bendDirection = Vector3f.Forward;

    private float _heelGroundOffset = -1.0f;

    /// <summary>IK nodes and effectors.</summary>
    private readonly IKTrigonometricChain _legChain = new();
    private IKNodeSegment _footSegment;
    private WeakReference<Node>? _target;
    private WeakReference<Node>? _heelTarget;
    private WeakReference<Node>? _bendTarget;
    private WeakReference<Node>? _groundTarget;

    private struct LocalCache
    {
        public Vector3f ToeToHeel;
        public float DefaultThighToToeDistance;

        public Vector3f BendDirection;
        public Vector3f TargetDirection;
        public Quaternion DefaultFootRotation;
        public Vector3f DefaultToeOffset;
        public Quaternion DefaultToeRotation;

        public Quaternion ToeRotation;
    }

    private LocalCache _local;

    private Vector3f _latestTargetPosition;
}
