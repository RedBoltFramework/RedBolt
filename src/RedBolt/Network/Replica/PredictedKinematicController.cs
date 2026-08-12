// Copyright (c) 2017-2025 the rbfx project.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Replica;

using RedBolt.Core;
using RedBolt.IO;
using RedBolt.Math;
using RedBolt.PhysicsJolt;

public struct PredictedKinematicControllerFrame
{
    public Vector3f WalkVelocity;
    public Quaternion Rotation;
    public bool NeedJump;
    public Vector3d StartPosition;
}

/// <summary>Jolt kinematic controller with client-side input prediction and server correction.</summary>
[Reflected(ObjectCategory.Category_Network)]
public partial class PredictedKinematicController : BaseFeedbackBehavior<PredictedKinematicControllerFrame>
{
    private ReplicatedTransform? _replicatedTransform;
    private KinematicCharacterController? _kinematicController;
    private PhysicsWorld? _physicsWorld;
    private Vector3d _previousPosition;
    private Vector3f _effectiveVelocity;
    private float _networkStepTime;
    private float _physicsStepTime;
    private PredictedKinematicControllerFrame _currentFrameData;
    private NetworkFrame? _latestConfirmedFrame;
    private NetworkFrame? _latestAffectedFrame;

    public void SetWalkVelocity(Vector3f velocity) => _currentFrameData.WalkVelocity = velocity;
    public void SetJump() => _currentFrameData.NeedJump = true;

    public bool IsConnectedToComponents =>
        _physicsWorld is not null && _replicatedTransform is not null && _kinematicController is not null;
    public bool IsConnectedToStandaloneComponents => _physicsWorld is not null && _kinematicController is not null;
    public Vector3f Velocity => _effectiveVelocity;

    public override void InitializeStandalone()
    {
        InitializeCommon();
        if (!IsConnectedToStandaloneComponents)
            return;
        base.InitializeStandalone();
        SubscribeToEvent<PhysicsEvents.PhysicsPreStepEvent>(_physicsWorld!, HandleStandalonePhysicsPreStep);
    }

    public override void InitializeOnServer()
    {
        InitializeCommon();
        if (!IsConnectedToComponents)
            return;
        base.InitializeOnServer();
    }

    public override void InitializeFromSnapshot(NetworkFrame frame, Deserializer source, bool isOwned)
    {
        InitializeCommon();
        if (!IsConnectedToComponents)
            return;
        base.InitializeFromSnapshot(frame, source, isOwned);

        if (isOwned)
        {
            _replicatedTransform!.PositionTrackOnly = true;
            SubscribeToEvent<PhysicsEvents.PhysicsPreStepEvent>(_physicsWorld!, HandleOwnedPhysicsPreStep);
        }
        else
            _kinematicController!.Gravity = Vector3f.Zero;
    }

    public override void InterpolateState(float replicaTimeStep, float inputTimeStep,
        NetworkTime replicaTime, NetworkTime inputTime)
    {
        if (!IsConnectedToComponents)
            return;
        base.InterpolateState(replicaTimeStep, inputTimeStep, replicaTime, inputTime);

        if (!NetworkObject!.IsOwnedByThisClient)
        {
            var derivativeTimeStep = 1.0f / NetworkObject.ReplicationManager!.UpdateFrequency;
            var positionAndVelocity = _replicatedTransform!.SampleTemporalPosition(new NetworkTime(replicaTime.Frame));
            _effectiveVelocity = ToVector3f(positionAndVelocity.Derivative) * derivativeTimeStep;
        }
    }

    protected override void OnServerFrameBegin(NetworkFrame serverFrame)
    {
        if (!IsConnectedToComponents)
            return;
        UpdateEffectiveVelocity(_networkStepTime);
        base.OnServerFrameBegin(serverFrame);
    }

    protected override void ApplyPayload(PredictedKinematicControllerFrame payload)
    {
        if (!IsConnectedToComponents)
            return;
        Node!.SetWorldRotation(payload.Rotation);
        _kinematicController!.SetWalkIncrement(payload.WalkVelocity * _physicsStepTime);
        if (payload.NeedJump && _kinematicController.OnGround)
            _kinematicController.Jump();
    }

    protected override void WritePayload(PredictedKinematicControllerFrame payload, Serializer destination)
    {
        destination.WriteVector3(payload.WalkVelocity);
        destination.WriteBool(payload.NeedJump);
        destination.WriteQuaternion(payload.Rotation);
    }

    protected override void ReadPayload(ref PredictedKinematicControllerFrame payload, Deserializer source)
    {
        payload.WalkVelocity = source.ReadVector3();
        payload.NeedJump = source.ReadBool();
        payload.Rotation = source.ReadQuaternion();
    }

    private void InitializeCommon()
    {
        UnsubscribeFromEvent<PhysicsEvents.PhysicsPreStepEvent>();
        _replicatedTransform = Node!.GetDerivedComponent<ReplicatedTransform>();
        _kinematicController = Node.GetDerivedComponent<KinematicCharacterController>();
        _physicsWorld = Scene?.GetComponent<PhysicsWorld>();
        _previousPosition = Scene!.ToAbsoluteWorldPosition(Node.GetWorldPosition());
        _physicsStepTime = _physicsWorld is not null ? 1.0f / _physicsWorld.Fps : 0.0f;
        _networkStepTime = 1.0f / NetworkObject!.ReplicationManager!.UpdateFrequency;
    }

    private void HandleStandalonePhysicsPreStep(ref PhysicsEvents.PhysicsPreStepEvent eventData)
    {
        UpdateEffectiveVelocity(_physicsStepTime);
        ApplyActionsOnClient();
    }

    private void HandleOwnedPhysicsPreStep(ref PhysicsEvents.PhysicsPreStepEvent eventData)
    {
        if (eventData.NetworkFrame is not { } networkFrame)
            return;
        OnPhysicsSynchronizedOnClient(new NetworkFrame(networkFrame));
    }

    private void OnPhysicsSynchronizedOnClient(NetworkFrame frame)
    {
        if (!IsConnectedToComponents)
            return;
        CheckAndCorrectController(frame);
        _currentFrameData.StartPosition = Scene!.ToAbsoluteWorldPosition(_kinematicController!.RawPosition);
        _currentFrameData.Rotation = Node!.GetWorldRotation();
        CreateFrameOnClient(frame, _currentFrameData);
        ApplyActionsOnClient();
        UpdateEffectiveVelocity(_networkStepTime);
    }

    private void ApplyActionsOnClient()
    {
        _kinematicController!.SetWalkIncrement(_currentFrameData.WalkVelocity * _physicsStepTime);
        if (_currentFrameData.NeedJump && _kinematicController.OnGround)
            _kinematicController.Jump();
        _currentFrameData.NeedJump = false;
    }

    private void UpdateEffectiveVelocity(float timeStep)
    {
        if (timeStep <= 0.0f)
            return;
        var currentPosition = Scene!.ToAbsoluteWorldPosition(Node!.GetWorldPosition());
        _effectiveVelocity = ToVector3f((currentPosition - _previousPosition) / timeStep);
        _previousPosition = currentPosition;
    }

    private void CheckAndCorrectController(NetworkFrame frame)
    {
        var latestConfirmedFrame = _replicatedTransform!.LatestFrame;
        if (!HasFramesOnClient || latestConfirmedFrame is null)
            return;
        if (_latestConfirmedFrame == latestConfirmedFrame)
            return;
        if (_latestAffectedFrame is { } latestAffected && latestConfirmedFrame <= latestAffected)
            return;

        var nextInput = FindFrameOnClient(latestConfirmedFrame.Value + 1);
        if (nextInput is null)
            return;
        if (AdjustConfirmedFrame(latestConfirmedFrame.Value, nextInput.Value))
            _latestAffectedFrame = frame;
        _latestConfirmedFrame = latestConfirmedFrame;
    }

    private bool AdjustConfirmedFrame(NetworkFrame confirmedFrame, PredictedKinematicControllerFrame nextInput)
    {
        var confirmedPosition = _replicatedTransform!.GetTemporalPosition(confirmedFrame);
        if (confirmedPosition is null)
            return false;
        var offset = ToVector3f(confirmedPosition.Value.Value - nextInput.StartPosition);
        if (offset.ApproximatelyEquivalent(Vector3f.Zero, _replicatedTransform.MovementThreshold))
            return false;
        _kinematicController!.AdjustRawPosition(offset, _replicatedTransform.SmoothingConstant);
        return true;
    }

    private static Vector3f ToVector3f(Vector3d value) =>
        new((float)value.X, (float)value.Y, (float)value.Z);
}
