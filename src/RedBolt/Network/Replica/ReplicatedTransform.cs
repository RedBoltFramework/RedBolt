//
// Copyright (c) 2017-2020 the rbfx project.
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

namespace RedBolt.Replica;

using RedBolt.Core;
using RedBolt.IO;
using RedBolt.Math;
using RedBolt.Network;

public enum ReplicatedRotationMode
{
    None,
    XYZ
}

/// <summary>Behavior that replicates the transform of the node.</summary>
[Reflected(ObjectCategory.Category_Network)]
public partial class ReplicatedTransform : NetworkBehavior
{
    public const uint DefaultNumUploadAttempts = 8;
    public const float DefaultSmoothingConstant = 15.0f;
    public const float DefaultMovementThreshold = 0.001f;
    public const float DefaultSnapThreshold = 5.0f;

    public const bool DefaultSynchronizePosition = true;
    public const ReplicatedRotationMode DefaultSynchronizeRotation = ReplicatedRotationMode.XYZ;
    public const bool DefaultExtrapolatePosition = true;
    public const bool DefaultExtrapolateRotation = false;
    public const bool DefaultSynchronizeScale = false;

    public const VectorBinaryEncoding DefaultPositionEncoding = VectorBinaryEncoding.Float;
    public const VectorBinaryEncoding DefaultRotationEncoding = VectorBinaryEncoding.Float;
    public const VectorBinaryEncoding DefaultVelocityEncoding = VectorBinaryEncoding.Float;
    public const VectorBinaryEncoding DefaultAngularVelocityEncoding = VectorBinaryEncoding.Float;
    public const float DefaultPositionEncodingParameter = 1024000.0f;
    public const float DefaultVelocityEncodingParameter = 100.0f;
    public const float DefaultAngularVelocityEncodingParameter = 100.0f;

    public new const NetworkCallbackFlags CallbackMask = NetworkCallbackFlags.UpdateTransformOnServer
        | NetworkCallbackFlags.UnreliableDelta | NetworkCallbackFlags.InterpolateState;

    public ReplicatedTransform()
        : base(CallbackMask)
    {
    }

    [Serialized("Num Upload Attempts")]
    public uint NumUploadAttempts { get; set; } = DefaultNumUploadAttempts;
    [Serialized("Replicate Owner")]
    public bool ReplicateOwner { get; set; }
    [Serialized("Position Track Only")]
    public bool PositionTrackOnly { get; set; }
    [Serialized("Rotation Track Only")]
    public bool RotationTrackOnly { get; set; }
    [Serialized("Scale Track Only")]
    public bool ScaleTrackOnly { get; set; }
    [Serialized("Smoothing Constant")]
    public float SmoothingConstant { get; set; } = DefaultSmoothingConstant;
    [Serialized("Movement Threshold")]
    public float MovementThreshold { get; set; } = DefaultMovementThreshold;
    [Serialized("Snap Threshold")]
    public float SnapThreshold { get; set; } = DefaultSnapThreshold;

    [Serialized("Synchronize Position")]
    public bool SynchronizePosition { get; set; } = DefaultSynchronizePosition;
    [Serialized("Synchronize Rotation")]
    public ReplicatedRotationMode SynchronizeRotation { get; set; } = DefaultSynchronizeRotation;
    [Serialized("Extrapolate Position")]
    public bool ExtrapolatePosition { get; set; } = DefaultExtrapolatePosition;
    [Serialized("Extrapolate Rotation")]
    public bool ExtrapolateRotation { get; set; } = DefaultExtrapolateRotation;
    [Serialized("Synchronize Scale")]
    public bool SynchronizeScale { get; set; } = DefaultSynchronizeScale;

    [Serialized("Position Encoding")]
    public VectorBinaryEncoding PositionEncoding { get; set; } = DefaultPositionEncoding;
    [Serialized("Position Encoding Parameter")]
    public float PositionEncodingParameter { get; set; } = DefaultPositionEncodingParameter;
    [Serialized("Rotation Encoding")]
    public VectorBinaryEncoding RotationEncoding { get; set; } = DefaultRotationEncoding;
    [Serialized("Velocity Encoding")]
    public VectorBinaryEncoding VelocityEncoding { get; set; } = DefaultVelocityEncoding;
    [Serialized("Velocity Encoding Parameter")]
    public float VelocityEncodingParameter { get; set; } = DefaultVelocityEncodingParameter;
    [Serialized("Angular Velocity Encoding")]
    public VectorBinaryEncoding AngularVelocityEncoding { get; set; } = DefaultAngularVelocityEncoding;
    [Serialized("Angular Velocity Encoding Parameter")]
    public float AngularVelocityEncodingParameter { get; set; } = DefaultAngularVelocityEncodingParameter;

    public override void InitializeOnServer()
    {
        InitializeCommon();

        _server.PreviousPosition = Scene!.ToAbsoluteWorldPosition(Node!.GetWorldPosition());
        _server.PreviousRotation = Node.GetWorldRotation();
        _server.LatestSentPosition = _server.PreviousPosition;
        _server.LatestSentRotation = _server.PreviousRotation;
        _server.LatestSentScale = Node.GetScale().X;

        _includePreviousFrame = NumUploadAttempts != 0;
        SubscribeToEvent<NetworkEvents.EndServerNetworkFrameEvent>(HandleServerFrameEnd);
    }

    public override void WriteSnapshot(NetworkFrame frame, Serializer destination)
    {
        var flags = 0u;
        if (SynchronizePosition) flags |= 1u << 0;
        if (SynchronizeRotation != ReplicatedRotationMode.None) flags |= 1u << 1;
        if (ExtrapolatePosition) flags |= 1u << 2;
        if (ExtrapolateRotation) flags |= 1u << 3;
        if (_includePreviousFrame) flags |= 1u << 4;
        if (SynchronizeScale) flags |= 1u << 5;
        destination.WriteVLE(flags);
    }

    public override void InitializeFromSnapshot(NetworkFrame frame, Deserializer source, bool isOwned)
    {
        InitializeCommon();

        var flags = source.ReadVLE();
        SynchronizePosition = (flags & (1u << 0)) != 0;
        SynchronizeRotation = (flags & (1u << 1)) != 0
            ? ReplicatedRotationMode.XYZ : ReplicatedRotationMode.None;
        ExtrapolatePosition = (flags & (1u << 2)) != 0;
        ExtrapolateRotation = (flags & (1u << 3)) != 0;
        _includePreviousFrame = (flags & (1u << 4)) != 0;
        SynchronizeScale = (flags & (1u << 5)) != 0;

        var replicationManager = NetworkObject!.ReplicationManager!;
        var updateFrequency = replicationManager.UpdateFrequency;
        var extrapolationInSeconds = replicationManager.GetSetting(NetworkSettings.ExtrapolationLimit).GetFloat();
        var extrapolationInFrames = checked((uint)System.Math.Ceiling(
            extrapolationInSeconds * updateFrequency));
        _positionSampler.Setup(ExtrapolatePosition ? extrapolationInFrames : 0,
            SmoothingConstant, SnapThreshold);
        _rotationSampler.Setup(ExtrapolateRotation ? extrapolationInFrames : 0,
            SmoothingConstant, MathDefs.LargeValue);
        _scaleSampler.Setup(0, SmoothingConstant, SnapThreshold);

        _positionTrace.Set(frame, new PositionAndVelocity(
            Scene!.ToAbsoluteWorldPosition(Node!.GetWorldPosition()), Vector3d.Zero));
        _rotationTrace.Set(frame, new QuaternionValueWithDerivative(
            Node.GetWorldRotation(), Vector3f.Zero));
        _scaleTrace.Set(frame, Node.GetScale().X);
    }

    public override void UpdateTransformOnServer()
    {
        _server.MovedDuringFrame = true;
    }

    public override void InterpolateState(float replicaTimeStep, float inputTimeStep,
        NetworkTime replicaTime, NetworkTime inputTime)
    {
        if (!ReplicateOwner && NetworkObject!.IsOwnedByThisClient)
            return;

        var maintainPosition = !PositionTrackOnly && SynchronizePosition;
        var maintainRotation = !RotationTrackOnly && SynchronizeRotation != ReplicatedRotationMode.None;
        var maintainScale = !ScaleTrackOnly && SynchronizeScale;

        if (maintainPosition)
        {
            if (_clientPreviousPositionInvalid)
            {
                var previousPosition = Node!.GetWorldPosition();
                _positionSampler.UpdatePreviousValue(replicaTime,
                    Scene!.ToAbsoluteWorldPosition(previousPosition));
            }
            if (_positionSampler.UpdateAndSample(_positionTrace, replicaTime, replicaTimeStep) is { } newPosition)
                Node!.SetWorldPosition(Scene!.ToRelativeWorldPosition(newPosition));
        }

        if (maintainRotation)
        {
            if (_clientPreviousRotationInvalid)
                _rotationSampler.UpdatePreviousValue(replicaTime, Node!.GetWorldRotation());
            if (_rotationSampler.UpdateAndSample(_rotationTrace, replicaTime, replicaTimeStep) is { } newRotation)
                Node!.SetWorldRotation(newRotation);
        }

        if (maintainScale)
        {
            if (_clientPreviousScaleInvalid)
                _scaleSampler.UpdatePreviousValue(replicaTime, Node!.GetScale().X);
            if (_scaleSampler.UpdateAndSample(_scaleTrace, replicaTime, replicaTimeStep) is { } newScale)
                Node!.SetScale(newScale);
        }

        _clientPreviousPositionInvalid = !maintainPosition;
        _clientPreviousRotationInvalid = !maintainRotation;
        _clientPreviousScaleInvalid = !maintainScale;
    }

    public override bool PrepareUnreliableDelta(NetworkFrame frame) =>
        _server.PendingUploadAttempts > 0 || NumUploadAttempts == 0;

    public override void WriteUnreliableDelta(NetworkFrame frame, Serializer destination)
    {
        WriteUnreliableDeltaForFrame(frame, destination);
        if (_includePreviousFrame)
            WriteUnreliableDeltaForFrame(frame - 1, destination);
    }

    public override void ReadUnreliableDelta(NetworkFrame frame, Deserializer source)
    {
        ReadUnreliableDeltaForFrame(frame, source);
        if (_includePreviousFrame)
            ReadUnreliableDeltaForFrame(frame - 1, source);
    }

    public PositionAndVelocity SampleTemporalPosition(NetworkTime time) => _positionTrace.SampleValid(time);
    public QuaternionValueWithDerivative SampleTemporalRotation(NetworkTime time) => _rotationTrace.SampleValid(time);
    public float SampleTemporalScale(NetworkTime time) => _scaleTrace.SampleValid(time);
    public PositionAndVelocity? GetTemporalPosition(NetworkFrame frame) => _positionTrace.GetRaw(frame);
    public QuaternionValueWithDerivative? GetTemporalRotation(NetworkFrame frame) => _rotationTrace.GetRaw(frame);
    public float? GetTemporalScale(NetworkFrame frame) => _scaleTrace.GetRaw(frame);
    public NetworkFrame? LatestFrame => _positionTrace.IsInitialized ? _positionTrace.LastFrame : null;

    private void InitializeCommon()
    {
        var traceDuration = NetworkObject!.ReplicationManager!.TraceDurationInFrames;
        _positionTrace.Resize(traceDuration);
        _rotationTrace.Resize(traceDuration);
        _scaleTrace.Resize(traceDuration);
    }

    private void HandleServerFrameEnd(ref NetworkEvents.EndServerNetworkFrameEvent eventData) =>
        OnServerFrameEnd(eventData.Frame);

    private void OnServerFrameEnd(NetworkFrame frame)
    {
        _server.PreviousPosition = _server.Position;
        _server.PreviousRotation = _server.Rotation;
        _server.Position = Scene!.ToAbsoluteWorldPosition(Node!.GetWorldPosition());
        _server.Rotation = Node.GetWorldRotation();
        _server.Scale = Node.GetScale().X;

        if (_server.MovedDuringFrame)
        {
            _server.Velocity = _server.Position - _server.PreviousPosition;
            _server.AngularVelocity = (_server.Rotation * _server.PreviousRotation.Inverse).AngularVelocity;
        }
        else
        {
            _server.Velocity = Vector3d.Zero;
            _server.AngularVelocity = Vector3f.Zero;
        }

        _positionTrace.Set(frame, new PositionAndVelocity(_server.Position, _server.Velocity));
        _rotationTrace.Set(frame, new QuaternionValueWithDerivative(_server.Rotation, _server.AngularVelocity));
        _scaleTrace.Set(frame, _server.Scale);

        if (_server.PendingUploadAttempts > 0)
            --_server.PendingUploadAttempts;

        if (_server.MovedDuringFrame)
        {
            _server.MovedDuringFrame = false;

            var positionErrorSquare = (_server.LatestSentPosition - _server.Position).LengthSquared;
            var isPositionDirty = positionErrorSquare > MovementThreshold * MovementThreshold;
            var isRotationDirty = !_server.LatestSentRotation.Equivalent(
                _server.Rotation, MathDefs.LargeEpsilon);
            var isScaleDirty = System.Math.Abs(_server.LatestSentScale - _server.Scale) > MathDefs.LargeEpsilon;
            if (isPositionDirty || isRotationDirty || isScaleDirty)
            {
                _server.PendingUploadAttempts = NumUploadAttempts;
                _server.LatestSentPosition = _server.Position;
                _server.LatestSentRotation = _server.Rotation;
                _server.LatestSentScale = _server.Scale;
            }
        }
    }

    private void WriteUnreliableDeltaForFrame(NetworkFrame frame, Serializer destination)
    {
        if (SynchronizePosition)
        {
            var positionData = _positionTrace.GetRaw(frame)
                ?? new PositionAndVelocity(_server.Position, _server.Velocity);
            destination.WritePackedVector3(positionData.Value, PositionEncoding, PositionEncodingParameter);
            destination.WritePackedVector3(positionData.Derivative, VelocityEncoding, VelocityEncodingParameter);
        }

        if (SynchronizeRotation == ReplicatedRotationMode.XYZ)
        {
            var rotationData = _rotationTrace.GetRaw(frame)
                ?? new QuaternionValueWithDerivative(_server.Rotation, _server.AngularVelocity);
            destination.WritePackedQuaternion(rotationData.Value, RotationEncoding);
            destination.WritePackedVector3(ToVector3d(rotationData.Derivative),
                AngularVelocityEncoding, AngularVelocityEncodingParameter);
        }

        if (SynchronizeScale)
            destination.WriteFloat(_scaleTrace.GetRaw(frame) ?? _server.Scale);
    }

    private void ReadUnreliableDeltaForFrame(NetworkFrame frame, Deserializer source)
    {
        if (SynchronizePosition)
        {
            var position = source.ReadPackedVector3(PositionEncoding, PositionEncodingParameter);
            var velocity = source.ReadPackedVector3(VelocityEncoding, VelocityEncodingParameter);
            _positionTrace.Set(frame, new PositionAndVelocity(position, velocity));
        }

        if (SynchronizeRotation == ReplicatedRotationMode.XYZ)
        {
            var rotation = source.ReadPackedQuaternion(RotationEncoding);
            var angularVelocity = ToVector3f(source.ReadPackedVector3(
                AngularVelocityEncoding, AngularVelocityEncodingParameter));
            _rotationTrace.Set(frame, new QuaternionValueWithDerivative(rotation, angularVelocity));
        }

        if (SynchronizeScale)
            _scaleTrace.Set(frame, source.ReadFloat());
    }

    private static Vector3d ToVector3d(Vector3f value) => new(value.X, value.Y, value.Z);
    private static Vector3f ToVector3f(Vector3d value) =>
        new((float)value.X, (float)value.Y, (float)value.Z);

    private sealed class ServerData
    {
        public uint PendingUploadAttempts;
        public Vector3d PreviousPosition;
        public Quaternion PreviousRotation;
        public Vector3d Position;
        public Quaternion Rotation;
        public Vector3d Velocity;
        public Vector3f AngularVelocity;
        public float Scale;
        public bool MovedDuringFrame;
        public Vector3d LatestSentPosition;
        public Quaternion LatestSentRotation;
        public float LatestSentScale;
    }

    private readonly NetworkValue<PositionAndVelocity> _positionTrace = new(
        new ValueWithDerivativeNetworkValueTraits<Vector3d>());
    private readonly NetworkValue<QuaternionValueWithDerivative> _rotationTrace = new(
        new QuaternionValueWithDerivativeNetworkValueTraits());
    private readonly NetworkValue<float> _scaleTrace = new();
    private readonly NetworkValueSampler<PositionAndVelocity, Vector3d> _positionSampler = new(
        new ValueWithDerivativeNetworkValueTraits<Vector3d>());
    private readonly NetworkValueSampler<QuaternionValueWithDerivative, Quaternion> _rotationSampler = new(
        new QuaternionValueWithDerivativeNetworkValueTraits());
    private readonly NetworkValueSampler<float> _scaleSampler = new();
    private bool _includePreviousFrame;
    private readonly ServerData _server = new();
    private bool _clientPreviousPositionInvalid;
    private bool _clientPreviousRotationInvalid;
    private bool _clientPreviousScaleInvalid;
}
