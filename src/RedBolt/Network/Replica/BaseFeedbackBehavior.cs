// Copyright (c) 2017-2025 the rbfx project.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Replica;

using RedBolt.IO;
using RedBolt.Network;

/// <summary>Base class for behavior that sends data (e.g. user input) back to the server. Unreliable.</summary>
public abstract class BaseFeedbackBehavior<T> : NetworkBehavior
    where T : struct
{
    public new const NetworkCallbackFlags CallbackMask =
        NetworkCallbackFlags.UnreliableFeedback | NetworkCallbackFlags.InterpolateState;

    public BaseFeedbackBehavior(NetworkCallbackFlags callbackMask = CallbackMask)
        : base(callbackMask)
    {
    }

    public void CreateFrameOnClient(NetworkFrame frame, T payload)
    {
        if (_clientInput.Count != 0)
        {
            var previousInputFrame = _clientInput[^1].Frame;
            var numSkippedFrames = checked((int)(frame - previousInputFrame) - 1);
            if (numSkippedFrames > 0)
            {
                if (numSkippedFrames >= _clientCapacity)
                    _clientInput.Clear();
                else
                {
                    var lastFrame = _clientInput[^1];
                    lastFrame.IsLost = true;
                    for (var i = 1; i <= numSkippedFrames; ++i)
                    {
                        lastFrame.Frame = previousInputFrame;
                        PushClientFrame(lastFrame);
                    }
                }
            }
        }

        PushClientFrame(new InputFrameData { Frame = frame, Payload = payload });
    }

    public T? FindFrameOnClient(NetworkFrame frame, bool ignoreLost = true)
    {
        foreach (var frameData in _clientInput)
        {
            if ((!frameData.IsLost || !ignoreLost) && frameData.Frame == frame)
                return frameData.Payload;
        }
        return null;
    }

    public bool HasFramesOnClient => _clientInput.Count != 0;

    public override void InitializeStandalone()
    {
        InitializeCommon();
    }

    public override void InitializeOnServer()
    {
        InitializeCommon();
        _serverInput.Resize(_maxInputFrames);
        SubscribeToEvent<NetworkEvents.BeginServerNetworkFrameEvent>(HandleServerFrameBegin);
    }

    public override void InitializeFromSnapshot(NetworkFrame frame, Deserializer source, bool isOwned)
    {
        InitializeCommon();
        if (isOwned)
            _clientCapacity = checked((int)_maxInputFrames);
    }

    public override void InterpolateState(float replicaTimeStep, float inputTimeStep,
        NetworkTime replicaTime, NetworkTime inputTime)
    {
        if (NetworkObject!.IsOwnedByThisClient)
            _desiredRedundancy = checked((uint)System.Math.Max(1, (int)System.Math.Floor(inputTime - replicaTime)));
    }

    public override bool PrepareUnreliableFeedback(NetworkFrame frame) => true;

    public override void WriteUnreliableFeedback(NetworkFrame frame, Serializer destination)
    {
        if (_clientInput.Count == 0 || _clientInput[^1].Frame != frame)
            return;

        var inputBufferSize = System.Math.Min(
            System.Math.Min(_desiredRedundancy, _maxRedundancy), checked((uint)_clientInput.Count));
        destination.WriteVLE(inputBufferSize);
        for (var i = 0; i < inputBufferSize; ++i)
            WritePayload(_clientInput[^(checked((int)i) + 1)].Payload, destination);
    }

    public override void ReadUnreliableFeedback(NetworkFrame feedbackFrame, Deserializer source)
    {
        var numInputFrames = System.Math.Min(source.ReadVLE(), _maxRedundancy);
        for (var i = 0u; i < numInputFrames; ++i)
        {
            var frameData = new InputFrameData { Frame = feedbackFrame - i };
            ReadPayload(ref frameData.Payload, source);
            if (!_serverInput.Has(frameData.Frame))
                _serverInput.Set(frameData.Frame, frameData);
        }
    }

    protected virtual void OnServerFrameBegin(NetworkFrame serverFrame)
    {
        if (_serverInput.GetRawOrPrior(serverFrame) is { } frameDataAndIndex)
        {
            ApplyPayload(frameDataAndIndex.Value.Payload);
            if (frameDataAndIndex.Frame != serverFrame)
                ++_lostFrames;
        }
        else
            ++_lostFrames;
        ++_totalFrames;

        const uint batchSize = 100;
        if (_totalFrames >= batchSize)
        {
            var loss = (float)_lostFrames / _totalFrames;
            _lostFrames = 0;
            _totalFrames = 0;

            var serverReplicator = NetworkObject!.ReplicationManager!.ServerReplicator!;
            if (NetworkObject.OwnerConnection is { } ownerConnection)
                serverReplicator.ReportInputLoss(ownerConnection, loss);
        }
    }

    protected abstract void ApplyPayload(T payload);
    protected abstract void WritePayload(T payload, Serializer destination);
    protected abstract void ReadPayload(ref T payload, Deserializer source);

    private void InitializeCommon()
    {
        UnsubscribeFromEvent<NetworkEvents.BeginServerNetworkFrameEvent>();
        var replicationManager = NetworkObject!.ReplicationManager!;
        _maxInputFrames = replicationManager.GetSetting(NetworkSettings.MaxInputFrames).GetUInt();
        _maxRedundancy = replicationManager.GetSetting(NetworkSettings.MaxInputRedundancy).GetUInt();
    }

    private void HandleServerFrameBegin(ref NetworkEvents.BeginServerNetworkFrameEvent eventData) =>
        OnServerFrameBegin(eventData.Frame);

    private void PushClientFrame(InputFrameData frameData)
    {
        if (_clientCapacity == 0)
            return;
        if (_clientInput.Count == _clientCapacity)
            _clientInput.RemoveAt(0);
        _clientInput.Add(frameData);
    }

    private struct InputFrameData
    {
        public bool IsLost;
        public NetworkFrame Frame;
        public T Payload;
    }

    private uint _maxRedundancy;
    private uint _maxInputFrames;
    private readonly NetworkValue<InputFrameData> _serverInput = new();
    private uint _totalFrames;
    private uint _lostFrames;
    private readonly List<InputFrameData> _clientInput = [];
    private int _clientCapacity;
    private uint _desiredRedundancy = 1;
}
