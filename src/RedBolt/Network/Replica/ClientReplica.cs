// Copyright (c) 2020-2025 the rbfx project.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Replica;

using RedBolt.Container;
using RedBolt.Core;
using RedBolt.IO;
using RedBolt.Network;
using RedBolt.Scene;

/// <summary>Maintains scene clocks of a replica on the client side.</summary>
public class ClientReplicaClock : RedObject
{
    public ClientReplicaClock(Scene scene, ReplicatedPeer connection,
        MsgSceneClock initialClock, VariantMap serverSettings)
    {
        _scene = scene;
        _peer = connection;
        _serverSettings = new VariantMap(serverSettings);
        _thisConnectionId = GetSetting(NetworkSettings.ConnectionId).GetUInt();
        _updateFrequency = GetSetting(NetworkSettings.UpdateFrequency).GetUInt();
        _inputDelay = initialClock.InputDelay;
        _replicaTime = InitializeSoftTime();
        _inputTime = InitializeSoftTime();
        _updateSync = new SceneUpdateSynchronizer(scene,
            new SceneUpdateSynchronizerParams(false, _updateFrequency));

        UpdateServerTime(initialClock, false);
        _replicaTime.Reset(ToReplicaTime(_serverTime));
        _inputTime.Reset(ToInputTime(_serverTime));
        _latestScaledInputTime = _replicaTime.Time;
    }

    public Scene Scene => _scene;
    public ReplicatedPeer Connection => _peer;
    public uint ConnectionId => _thisConnectionId;
    public uint UpdateFrequency => _updateFrequency;
    public double SecondsToFrames(double value) => value * _updateFrequency;

    public uint InputDelay => _inputDelay;
    public Variant GetSetting(NetworkSetting setting) =>
        NetworkSettings.GetNetworkSetting(_serverSettings, setting);

    public NetworkTime ServerTime => _serverTime;
    public NetworkTime ReplicaTime => _replicaTime.Time;
    public float ReplicaTimeStep => _replicaTimeStep;
    public NetworkTime InputTime => _inputTime.Time;
    public float InputTimeStep => _inputTimeStep;
    public bool IsNewInputFrame => _isNewInputFrame;
    public NetworkTime LatestScaledInputTime => _latestScaledInputTime;

    public override void Dispose()
    {
        _updateSync.Dispose();
        _serverSettings.Dispose();
        base.Dispose();
    }

    protected void UpdateClientClocks(float timeStep, IReadOnlyList<MsgSceneClock> pendingClockUpdates)
    {
        _serverTime += SecondsToFrames(timeStep);
        foreach (var message in pendingClockUpdates)
            UpdateServerTime(message, true);

        _replicaTimeStep = _replicaTime.Update(timeStep, ToReplicaTime(_serverTime));

        var previousInputTime = _inputTime.Time;
        _inputTimeStep = _inputTime.Update(timeStep, ToInputTime(_serverTime));

        if (timeStep != _inputTimeStep)
            _latestScaledInputTime = _inputTime.Time;

        _isNewInputFrame = previousInputTime.Frame != _inputTime.Time.Frame;
        if (_isNewInputFrame)
        {
            _updateSync.Synchronize(_inputTime.Time.Frame,
                _inputTime.Time.Fraction / _updateFrequency);
        }
        else
            _updateSync.Update(_inputTimeStep);
    }

    private SoftNetworkTime InitializeSoftTime()
    {
        var timeSnapThreshold = GetSetting(NetworkSettings.TimeSnapThreshold).GetFloat();
        var timeErrorTolerance = GetSetting(NetworkSettings.TimeErrorTolerance).GetFloat();
        var minTimeDilation = GetSetting(NetworkSettings.MinTimeDilation).GetFloat();
        var maxTimeDilation = GetSetting(NetworkSettings.MaxTimeDilation).GetFloat();
        return new SoftNetworkTime(checked((int)_updateFrequency), timeSnapThreshold,
            timeErrorTolerance, minTimeDilation, maxTimeDilation);
    }

    private void UpdateServerTime(MsgSceneClock message, bool skipOutdated)
    {
        if (skipOutdated && message.LatestFrame < _latestServerFrame)
            return;

        var serverFrameTime = _peer.RemoteToLocalTime(message.LatestFrameTime);
        var offsetMs = unchecked((int)(_peer.LocalTime - serverFrameTime));

        _inputDelay = message.InputDelay;
        _latestServerFrame = message.LatestFrame;
        _serverTime = new NetworkTime(message.LatestFrame);
        _serverTime += SecondsToFrames(offsetMs * 0.001);
    }

    private NetworkTime ToReplicaTime(NetworkTime serverTime)
    {
        var interpolationDelay = GetSetting(NetworkSettings.InterpolationDelay).GetDouble();
        var interpolationLimit = GetSetting(NetworkSettings.InterpolationLimit).GetDouble();
        var clientDelay = System.Math.Min(interpolationLimit,
            interpolationDelay + _peer.Ping * 0.001);
        return serverTime - SecondsToFrames(clientDelay);
    }

    private NetworkTime ToInputTime(NetworkTime serverTime) => serverTime + _inputDelay;

    private readonly Scene _scene;
    private readonly ReplicatedPeer _peer;
    private readonly VariantMap _serverSettings;
    private readonly uint _thisConnectionId;
    private readonly uint _updateFrequency;

    private uint _inputDelay;
    private NetworkTime _serverTime;
    private NetworkFrame _latestServerFrame;
    private NetworkTime _latestScaledInputTime;
    private bool _isNewInputFrame;

    private readonly SoftNetworkTime _replicaTime;
    private float _replicaTimeStep;
    private readonly SoftNetworkTime _inputTime;
    private float _inputTimeStep;
    private readonly SceneUpdateSynchronizer _updateSync;
}

/// <summary>Client part of the ReplicationManager subsystem.</summary>
public class ClientReplica : ClientReplicaClock
{
    public ClientReplica(Scene scene, ReplicatedPeer connection,
        MsgSceneClock initialClock, VariantMap serverSettings)
        : base(scene, connection, initialClock, serverSettings)
    {
        _network = Context.GetSubsystem<global::RedBolt.Network.Network>()!;
        _objectRegistry = scene.GetComponent<ReplicationManager>()!;

        SubscribeToEvent<CoreEvents.InputReadyEvent>(HandleInputReady);
        SubscribeToEvent<NetworkEvents.NetworkUpdateEvent>(_network, HandleNetworkUpdate);
    }

    public bool ProcessMessage(NetworkMessageId messageId, MemoryBuffer messageData)
    {
        switch (messageId)
        {
            case NetworkMessageId.SceneClock:
                ProcessSceneClock(NetworkMessageUtils.ReadSerializedMessage<MsgSceneClock>(messageData));
                return true;

            case NetworkMessageId.RemoveObjects:
                ProcessRemoveObjects(messageData);
                return true;

            case NetworkMessageId.AddObjects:
            case NetworkMessageId.AddObjectsIncomplete:
            {
                var reader = new LargeMessageReader(_buffer,
                    NetworkMessageId.AddObjectsIncomplete, NetworkMessageId.AddObjects);
                reader.OnMessage(messageId, messageData, fullMessageData =>
                {
                    ProcessAddObjects(fullMessageData);
                    _buffer.Clear();
                });
                return true;
            }

            case NetworkMessageId.UpdateObjectsReliable:
            case NetworkMessageId.UpdateObjectsReliableIncomplete:
            {
                var reader = new LargeMessageReader(_buffer,
                    NetworkMessageId.UpdateObjectsReliableIncomplete,
                    NetworkMessageId.UpdateObjectsReliable);
                reader.OnMessage(messageId, messageData, fullMessageData =>
                {
                    ProcessUpdateObjectsReliable(fullMessageData);
                    _buffer.Clear();
                });
                return true;
            }

            case NetworkMessageId.UpdateObjectsUnreliable:
                ProcessUpdateObjectsUnreliable(messageData);
                return true;

            default:
                return false;
        }
    }

    public void ProcessSceneUpdate(bool postUpdate)
    {
        if (postUpdate)
        {
            var eventData = new SceneEvents.SceneNetworkPostUpdateEvent
            {
                Scene = Scene,
                TimeStepReplica = ReplicaTimeStep,
                TimeStepInput = InputTimeStep
            };
            Scene.SendEvent(ref eventData);
        }
        else
        {
            var eventData = new SceneEvents.SceneNetworkUpdateEvent
            {
                Scene = Scene,
                TimeStepReplica = ReplicaTimeStep,
                TimeStepInput = InputTimeStep
            };
            Scene.SendEvent(ref eventData);
        }
    }

    public string DebugInfo
    {
        get
        {
            var sceneName = Scene.Name.Length != 0 ? Scene.Name : "Unnamed";
            var inputDelayMs = (InputTime - ServerTime) / UpdateFrequency * 1000.0;
            var replicaDelayMs = (ServerTime - ReplicaTime) / UpdateFrequency * 1000.0;
            return $"Scene '{sceneName}': Ping {Connection.Ping}ms, Time "
                + $"{System.Math.Max(0, (int)System.Math.Ceiling(inputDelayMs))}ms+#{ServerTime.Frame}"
                + $"-{System.Math.Max(0, (int)System.Math.Ceiling(replicaDelayMs))}ms, "
                + $"Sync since #{LatestScaledInputTime.Frame}\n";
        }
    }

    public IReadOnlyCollection<NetworkObject> OwnedNetworkObjects => _ownedObjects;
    public bool HasOwnedNetworkObjects => _ownedObjects.Count != 0;
    public NetworkObject? OwnedNetworkObject => _ownedObjects.Count == 1 ? _ownedObjects.First() : null;

    private void ProcessSceneClock(MsgSceneClock message)
    {
        _pendingClockUpdates.Add(message);
    }

    private void ProcessRemoveObjects(MemoryBuffer messageData)
    {
        _ = (NetworkFrame)messageData.ReadInt64();
        while (!messageData.IsEof())
        {
            var networkId = (NetworkId)messageData.ReadUInt();
            var networkObject = _objectRegistry.GetNetworkObject(networkId);
            if (networkObject is null)
            {
                Logging.Log.GetLogger().Warning("Cannot find NetworkObject {} to remove",
                    TrackedComponent.ToString(networkId));
                continue;
            }

            RemoveNetworkObject(networkObject);
        }
    }

    private void ProcessAddObjects(MemoryBuffer messageData)
    {
        var messageFrame = (NetworkFrame)messageData.ReadInt64();
        while (!messageData.IsEof())
        {
            var networkId = (NetworkId)messageData.ReadUInt();
            var componentType = messageData.ReadStringHash();
            var ownerConnectionId = messageData.ReadVLE();
            _componentBuffer.SetData(messageData.ReadBuffer());

            var isOwned = ownerConnectionId == ConnectionId;
            var networkObject = CreateNetworkObject(networkId, componentType);
            if (networkObject is null)
                continue;

            networkObject.InitializeFromSnapshot(messageFrame, _componentBuffer, isOwned);
            if (isOwned)
            {
                networkObject.SetNetworkMode(NetworkObjectMode.ClientOwned);
                _ownedObjects.Add(networkObject);
            }
            else
                networkObject.SetNetworkMode(NetworkObjectMode.ClientReplicated);
        }
    }

    private void ProcessUpdateObjectsReliable(MemoryBuffer messageData)
    {
        var messageFrame = (NetworkFrame)messageData.ReadInt64();
        while (!messageData.IsEof())
        {
            var networkId = (NetworkId)messageData.ReadUInt();
            var componentType = messageData.ReadStringHash();
            _componentBuffer.SetData(messageData.ReadBuffer());
            GetCheckedNetworkObject(networkId, componentType)?.ReadReliableDelta(messageFrame, _componentBuffer);
        }
    }

    private void ProcessUpdateObjectsUnreliable(MemoryBuffer messageData)
    {
        var messageFrame = (NetworkFrame)messageData.ReadInt64();
        while (!messageData.IsEof())
        {
            var networkId = (NetworkId)messageData.ReadUInt();
            var componentType = messageData.ReadStringHash();
            _componentBuffer.SetData(messageData.ReadBuffer());
            GetCheckedNetworkObject(networkId, componentType)?.ReadUnreliableDelta(messageFrame, _componentBuffer);
        }
    }

    private NetworkObject? CreateNetworkObject(NetworkId networkId, StringHash componentType)
    {
        if (Context.CreateObject(componentType) is not NetworkObject networkObject)
        {
            Logging.Log.GetLogger().Warning("Cannot create NetworkObject {} of type #{} '{}'",
                TrackedComponent.ToString(networkId), componentType.Value, componentType.ToDebugString());
            return null;
        }
        networkObject.SetNetworkId(networkId);

        if (_objectRegistry.GetNetworkObject(networkId, false) is { } oldNetworkObject)
        {
            Logging.Log.GetLogger().Warning("NetworkObject {} overwrites existing NetworkObject {}",
                TrackedComponent.ToString(networkId), TrackedComponent.ToString(oldNetworkObject.NetworkId));
            RemoveNetworkObject(oldNetworkObject);
        }

        var newNode = Scene.CreateChild(string.Empty);
        newNode.AddComponent(networkObject);
        return networkObject;
    }

    private NetworkObject? GetCheckedNetworkObject(NetworkId networkId, StringHash componentType)
    {
        var networkObject = _objectRegistry.GetNetworkObject(networkId);
        if (networkObject is null)
        {
            Logging.Log.GetLogger().Warning("Cannot find existing NetworkObject {}",
                TrackedComponent.ToString(networkId));
            return null;
        }

        if (networkObject.GetTypeNameHash() != componentType)
        {
            Logging.Log.GetLogger().Warning(
                "NetworkObject {} has unexpected type '{}', message was prepared for {}",
                TrackedComponent.ToString(networkId), networkObject.GetTypeName(), componentType.ToDebugString());
            return null;
        }

        return networkObject;
    }

    private void RemoveNetworkObject(NetworkObject networkObject)
    {
        if (networkObject.NetworkMode == NetworkObjectMode.ClientOwned)
            _ownedObjects.Remove(networkObject);

        var parentNode = networkObject.Node!.Parent;
        foreach (var childNetworkObject in networkObject.ChildrenNetworkObjects)
            childNetworkObject.Node?.SetParent(parentNode);

        networkObject.PrepareToRemove();
        if (networkObject.Node is not null)
            networkObject.Remove();
    }

    private void HandleInputReady(ref CoreEvents.InputReadyEvent eventData) => OnInputReady(eventData.TimeStep);

    private void HandleNetworkUpdate(ref NetworkEvents.NetworkUpdateEvent eventData)
    {
        if (!eventData.IsServer)
            OnNetworkUpdate();
    }

    private void OnInputReady(float timeStep)
    {
        UpdateClientClocks(timeStep, _pendingClockUpdates);
        _pendingClockUpdates.Clear();

        foreach (var networkObject in _objectRegistry.NetworkObjects)
        {
            networkObject.InterpolateState(ReplicaTimeStep, InputTimeStep, ReplicaTime, InputTime);
        }

        if (IsNewInputFrame)
        {
            var eventData = new NetworkEvents.BeginClientNetworkFrameEvent { Frame = InputTime.Frame };
            _network.SendEvent(ref eventData);
        }
    }

    private void OnNetworkUpdate()
    {
        if (!IsNewInputFrame)
            return;

        var eventData = new NetworkEvents.EndClientNetworkFrameEvent { Frame = InputTime.Frame };
        _network.SendEvent(ref eventData);
        SendObjectsFeedbackUnreliable(InputTime.Frame);
    }

    private void SendObjectsFeedbackUnreliable(NetworkFrame feedbackFrame)
    {
        using var writer = new MultiMessageWriter(Connection.Connection, _buffer2,
            NetworkMessageId.ObjectsFeedbackUnreliable, PacketType.UnreliableUnordered);

        var message = writer.Buffer;
        var debugInfo = writer.DebugInfo;
        message.WriteInt64(feedbackFrame.Value);
        writer.CompleteHeader();

        foreach (var networkObject in _ownedObjects)
        {
            if (!networkObject.PrepareUnreliableFeedback(feedbackFrame))
                continue;

            _componentBuffer.Clear();
            networkObject.WriteUnreliableFeedback(feedbackFrame, _componentBuffer);
            message.WriteUInt((uint)networkObject.NetworkId);
            message.WriteBuffer(_componentBuffer.Buffer);

            if (debugInfo.Length != 0)
                debugInfo.Append(", ");
            debugInfo.Append(TrackedComponent.ToString(networkObject.NetworkId));
            writer.CompletePayload();
        }
    }

    private readonly global::RedBolt.Network.Network _network;
    private readonly NetworkObjectRegistry _objectRegistry;
    private readonly List<MsgSceneClock> _pendingClockUpdates = [];
    private readonly HashSet<NetworkObject> _ownedObjects = [];
    private readonly VectorBuffer _componentBuffer = new();
    private readonly VectorBuffer _buffer = new();
    private readonly VectorBuffer _buffer2 = new();
}
