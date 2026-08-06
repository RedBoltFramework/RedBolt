// Copyright (c) 2020-2025 the rbfx project.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Replica;

using System.Diagnostics;

using RedBolt.Core;
using RedBolt.IO;
using RedBolt.Math;
using RedBolt.Network;
using RedBolt.Scene;

/// <summary>Replication state shared between all clients.</summary>
public sealed class SharedReplicationState : IDisposable
{
    public SharedReplicationState(NetworkObjectRegistry objectRegistry)
    {
        _objectRegistry = objectRegistry;
        objectRegistry.NetworkObjectAdded += OnNetworkObjectAdded;
        objectRegistry.NetworkObjectRemoved += OnNetworkObjectRemoved;

        foreach (var networkObject in objectRegistry.NetworkObjects)
            OnNetworkObjectAdded(networkObject);
    }

    public void PrepareForUpdate()
    {
        ResetFrameBuffers();
        InitializeNewObjects();
        _objectRegistry.UpdateNetworkObjects();
        _objectRegistry.GetSortedNetworkObjects(_sortedNetworkObjects);
    }

    public void QueueDeltaUpdate(NetworkObject networkObject)
    {
        var index = GetIndex(networkObject.NetworkId);
        _isDeltaUpdateQueued[index] = true;
    }

    public void CookDeltaUpdates(NetworkFrame currentFrame)
    {
        _recentlyRemovedObjects.Clear();

        for (var i = 0; i < _isDeltaUpdateQueued.Count; ++i)
        {
            if (!_isDeltaUpdateQueued[i])
                continue;

            var networkObject = _objectRegistry.GetNetworkObjectByIndex(i);
            Debug.Assert(networkObject is not null);
            if (networkObject is null)
                continue;

            if (networkObject.PrepareReliableDelta(currentFrame))
            {
                var beginOffset = _deltaUpdateBuffer.Position;
                networkObject.WriteReliableDelta(currentFrame, _deltaUpdateBuffer);
                var endOffset = _deltaUpdateBuffer.Position;
                _needReliableDeltaUpdate[i] = true;
                _reliableDeltaUpdateData[i] = new DeltaBufferSpan(beginOffset, endOffset);
            }

            if (networkObject.PrepareUnreliableDelta(currentFrame))
            {
                var beginOffset = _deltaUpdateBuffer.Position;
                networkObject.WriteUnreliableDelta(currentFrame, _deltaUpdateBuffer);
                var endOffset = _deltaUpdateBuffer.Position;
                _needUnreliableDeltaUpdate[i] = true;
                _unreliableDeltaUpdateData[i] = new DeltaBufferSpan(beginOffset, endOffset);
            }
        }
    }

    public IReadOnlySet<NetworkId> RecentlyRemovedObjects => _recentlyRemovedObjects;
    public IReadOnlyList<NetworkObject> SortedObjects => _sortedNetworkObjects;
    public int IndexUpperBound => _objectRegistry.NetworkIndexUpperBound;

    public IReadOnlySet<NetworkObject> GetOwnedObjectsByConnection(ReplicatedPeer connection) =>
        _ownedObjectsByConnection.TryGetValue(connection, out var objects) ? objects : EmptyObjects;

    public byte[]? GetReliableUpdateByIndex(int index) =>
        _needReliableDeltaUpdate[index] ? GetSpanData(_reliableDeltaUpdateData[index]) : null;

    public byte[]? GetUnreliableUpdateByIndex(int index) =>
        _needUnreliableDeltaUpdate[index] ? GetSpanData(_unreliableDeltaUpdateData[index]) : null;

    public void Dispose()
    {
        _objectRegistry.NetworkObjectAdded -= OnNetworkObjectAdded;
        _objectRegistry.NetworkObjectRemoved -= OnNetworkObjectRemoved;
        _deltaUpdateBuffer.Dispose();
    }

    private void OnNetworkObjectAdded(NetworkObject networkObject)
    {
        _recentlyAddedObjects.Add(networkObject.NetworkId);
    }

    private void OnNetworkObjectRemoved(NetworkObject networkObject)
    {
        if (!_recentlyAddedObjects.Remove(networkObject.NetworkId))
            _recentlyRemovedObjects.Add(networkObject.NetworkId);

        if (networkObject.OwnerConnection is { } ownerConnection
            && _ownedObjectsByConnection.TryGetValue(ownerConnection, out var objects))
        {
            objects.Remove(networkObject);
            if (objects.Count == 0)
                _ownedObjectsByConnection.Remove(ownerConnection);
        }
    }

    private void ResetFrameBuffers()
    {
        var indexUpperBound = IndexUpperBound;
        Reset(_isDeltaUpdateQueued, indexUpperBound, false);
        Reset(_needReliableDeltaUpdate, indexUpperBound, false);
        Reset(_reliableDeltaUpdateData, indexUpperBound, default);
        Reset(_needUnreliableDeltaUpdate, indexUpperBound, false);
        Reset(_unreliableDeltaUpdateData, indexUpperBound, default);
        _deltaUpdateBuffer.Clear();
    }

    private void InitializeNewObjects()
    {
        foreach (var networkId in _recentlyAddedObjects)
        {
            var networkObject = _objectRegistry.GetNetworkObject(networkId);
            if (networkObject is null)
            {
                Debug.Fail("Cannot find recently added NetworkObject");
                continue;
            }

            networkObject.InitializeOnServer();
            networkObject.SetNetworkMode(NetworkObjectMode.Server);
            if (networkObject.OwnerConnection is { } ownerConnection)
            {
                if (!_ownedObjectsByConnection.TryGetValue(ownerConnection, out var objects))
                {
                    objects = [];
                    _ownedObjectsByConnection.Add(ownerConnection, objects);
                }
                objects.Add(networkObject);
            }
        }
        _recentlyAddedObjects.Clear();
    }

    private byte[] GetSpanData(DeltaBufferSpan span) => _deltaUpdateBuffer.Buffer[
        checked((int)span.BeginOffset)..checked((int)span.EndOffset)].ToArray();

    private static int GetIndex(NetworkId networkId) =>
        TrackedComponent.DeconstructComponentReference(networkId).Index;

    private static void Reset<T>(List<T> values, int size, T value)
    {
        values.Clear();
        for (var i = 0; i < size; ++i)
            values.Add(value);
    }

    private readonly record struct DeltaBufferSpan(uint BeginOffset, uint EndOffset);

    private static readonly IReadOnlySet<NetworkObject> EmptyObjects = new HashSet<NetworkObject>();
    private readonly NetworkObjectRegistry _objectRegistry;
    private readonly HashSet<NetworkId> _recentlyRemovedObjects = [];
    private readonly HashSet<NetworkId> _recentlyAddedObjects = [];
    private readonly List<NetworkObject> _sortedNetworkObjects = [];
    private readonly List<bool> _isDeltaUpdateQueued = [];
    private readonly List<bool> _needReliableDeltaUpdate = [];
    private readonly List<bool> _needUnreliableDeltaUpdate = [];
    private readonly VectorBuffer _deltaUpdateBuffer = new();
    private readonly List<DeltaBufferSpan> _reliableDeltaUpdateData = [];
    private readonly List<DeltaBufferSpan> _unreliableDeltaUpdateData = [];
    private readonly Dictionary<ReplicatedPeer, HashSet<NetworkObject>> _ownedObjectsByConnection = [];
}

/// <summary>Clock synchronization state specific to one client connection.</summary>
public class ClientSynchronizationState : IDisposable
{
    private const uint InputStatsSafetyLimit = 64;

    public ClientSynchronizationState(NetworkObjectRegistry objectRegistry,
        ReplicatedPeer connection, VariantMap settings)
    {
        _objectRegistry = objectRegistry;
        _peer = connection;
        _settings = new VariantMap(settings);
        _updateFrequency = GetSetting(NetworkSettings.UpdateFrequency).GetUInt();
        _inputDelayFilter = new FilteredUint(
            GetSetting(NetworkSettings.InputDelayFilterBufferSize).GetUInt());
        _inputStats = new ClientInputStatistics(
            GetSetting(NetworkSettings.InputBufferingWindowSize).GetUInt(), InputStatsSafetyLimit);
        _inputBufferFilter = new FilteredUint(
            GetSetting(NetworkSettings.InputBufferingFilterBufferSize).GetUInt());
        NetworkSettings.SetNetworkSetting(_settings, NetworkSettings.ConnectionId, _peer.ObjectId);
    }

    public void BeginNetworkFrame(NetworkFrame currentFrame, float overtime)
    {
        var timeStep = 1.0f / _updateFrequency;
        _frame = currentFrame;
        _frameLocalTime = unchecked(_peer.LocalTime
            - (uint)(int)System.MathF.Round(overtime * 1000, MidpointRounding.AwayFromZero));
        _clockTimeAccumulator += timeStep;
    }

    public Variant GetSetting(NetworkSetting setting) =>
        NetworkSettings.GetNetworkSetting(_settings, setting);
    public bool IsSynchronized => _synchronized;
    public NetworkFrame CurrentFrame => _frame;
    public uint InputDelay => _inputDelay;
    public uint InputBufferSize => _inputBufferSize;

    public virtual void Dispose()
    {
        _settings.Dispose();
    }

    protected void SendMessages()
    {
        if (!_synchronizationMagic.HasValue)
        {
            var magic = MakeMagic();
            NetworkMessageUtils.WriteSerializedMessage(_peer.Connection,
                NetworkMessageId.Configure, new MsgConfigure { Magic = magic, Settings = _settings },
                PacketType.ReliableUnordered);
            _synchronizationMagic = magic;
        }

        var clockInterval = GetSetting(NetworkSettings.PeriodicClockInterval).GetFloat();
        if (_clockTimeAccumulator >= clockInterval)
        {
            _clockTimeAccumulator %= clockInterval;
            UpdateInputDelay();
            UpdateInputBuffer();

            var message = new MsgSceneClock
            {
                LatestFrame = _frame,
                LatestFrameTime = _frameLocalTime,
                InputDelay = _inputDelay + _inputBufferSize
            };
            NetworkMessageUtils.WriteSerializedMessage(_peer.Connection,
                NetworkMessageId.SceneClock, message, PacketType.UnreliableUnordered);
        }
    }

    protected bool ProcessMessage(NetworkMessageId messageId, MemoryBuffer messageData)
    {
        if (messageId != NetworkMessageId.Synchronized)
            return false;

        ProcessSynchronized(NetworkMessageUtils.ReadSerializedMessage<MsgSynchronized>(messageData));
        return true;
    }

    protected void OnInputReceived(NetworkFrame inputFrame)
    {
        _inputStats.OnInputReceived(inputFrame);
    }

    protected NetworkObjectRegistry ObjectRegistry => _objectRegistry;
    protected ReplicatedPeer Peer => _peer;

    private void UpdateInputDelay()
    {
        var latestPingTimestamp = _peer.LocalTimeOfLatestRoundtrip;
        if (_latestProcessedPingTimestamp == latestPingTimestamp)
            return;
        _latestProcessedPingTimestamp = latestPingTimestamp;

        var inputDelayInFrames = 0.001 * _peer.Ping * _updateFrequency;
        _inputDelayFilter.AddValue(checked((uint)System.Math.Ceiling(inputDelayInFrames)));
        _inputDelay = _inputDelayFilter.StabilizedAverageMaxValue;
    }

    private void UpdateInputBuffer()
    {
        _inputBufferFilter.AddValue(_inputStats.RecommendedBufferSize);

        var tweakA = GetSetting(NetworkSettings.InputBufferingTweakA).GetFloat();
        var tweakB = GetSetting(NetworkSettings.InputBufferingTweakB).GetFloat();
        var newInputBufferSize = (int)System.MathF.Round(
            tweakA * _inputBufferFilter.StabilizedAverageMaxValue + tweakB,
            MidpointRounding.AwayFromZero);

        var minInputBuffer = checked((int)GetSetting(NetworkSettings.InputBufferingMin).GetUInt());
        var maxInputBuffer = checked((int)GetSetting(NetworkSettings.InputBufferingMax).GetUInt());
        _inputBufferSize = checked((uint)System.Math.Clamp(newInputBufferSize,
            minInputBuffer, maxInputBuffer));
    }

    private void ProcessSynchronized(MsgSynchronized message)
    {
        if (_synchronizationMagic != message.Magic)
        {
            Logging.Log.GetLogger().Warning(
                "Connection {}: Unexpected synchronization acknowledgement received", _peer.ToString());
            return;
        }
        _synchronized = true;
    }

    private static uint MakeMagic() => RandomEngine.GetDefaultEngine().GetUInt();

    private readonly NetworkObjectRegistry _objectRegistry;
    private readonly ReplicatedPeer _peer;
    private readonly VariantMap _settings;
    private readonly uint _updateFrequency;
    private uint? _synchronizationMagic;
    private bool _synchronized;
    private NetworkFrame _frame;
    private uint _frameLocalTime;
    private uint? _latestProcessedPingTimestamp;
    private readonly FilteredUint _inputDelayFilter;
    private uint _inputDelay;
    private readonly ClientInputStatistics _inputStats;
    private readonly FilteredUint _inputBufferFilter;
    private uint _inputBufferSize;
    private float _clockTimeAccumulator;
}

/// <summary>Scene replication state specific to one client connection.</summary>
public sealed class ClientReplicationState : ClientSynchronizationState
{
    public ClientReplicationState(NetworkObjectRegistry objectRegistry,
        ReplicatedPeer connection, VariantMap settings)
        : base(objectRegistry, connection, settings)
    {
    }

    public void UpdateNetworkObjects(SharedReplicationState sharedState)
    {
        if (!IsSynchronized)
            return;

        var timeStep = 1.0f / GetSetting(NetworkSettings.UpdateFrequency).GetUInt();
        var relevanceTimeout = GetSetting(NetworkSettings.RelevanceTimeout).GetFloat();

        while (_objectsRelevance.Count < sharedState.IndexUpperBound)
        {
            _objectsRelevance.Add(NetworkObjectRelevance.Irrelevant);
            _objectsRelevanceTimeouts.Add(0.0f);
        }

        _pendingRemovedObjects.Clear();
        _pendingUpdatedObjects.Clear();

        foreach (var networkId in sharedState.RecentlyRemovedObjects)
        {
            var index = GetIndex(networkId);
            if (_objectsRelevance[index] != NetworkObjectRelevance.Irrelevant)
            {
                _objectsRelevance[index] = NetworkObjectRelevance.Irrelevant;
                _pendingRemovedObjects.Add(networkId);
            }
        }

        foreach (var networkObject in sharedState.SortedObjects)
        {
            var networkId = networkObject.NetworkId;
            var parentNetworkId = networkObject.ParentNetworkId;
            var index = GetIndex(networkId);
            var wasRelevant = _objectsRelevance[index] != NetworkObjectRelevance.Irrelevant;
            var isParentRelevant = parentNetworkId == NetworkId.None
                || _objectsRelevance[GetIndex(parentNetworkId)] != NetworkObjectRelevance.Irrelevant;

            if (!wasRelevant && isParentRelevant)
            {
                _objectsRelevance[index] = networkObject.GetRelevanceForClient(Peer)
                    ?? NetworkObjectRelevance.NormalUpdates;
                if (_objectsRelevance[index] != NetworkObjectRelevance.Irrelevant)
                {
                    _objectsRelevanceTimeouts[index] = relevanceTimeout;
                    _pendingUpdatedObjects.Add(new PendingObjectUpdate(networkObject, true));
                }
            }
            else if (wasRelevant)
            {
                _objectsRelevanceTimeouts[index] -= timeStep;
                if (_objectsRelevanceTimeouts[index] < 0.0f || !isParentRelevant)
                {
                    _objectsRelevance[index] = isParentRelevant
                        ? networkObject.GetRelevanceForClient(Peer) ?? NetworkObjectRelevance.NormalUpdates
                        : NetworkObjectRelevance.Irrelevant;

                    if (_objectsRelevance[index] == NetworkObjectRelevance.Irrelevant)
                    {
                        _pendingRemovedObjects.Add(networkId);
                        continue;
                    }

                    _objectsRelevanceTimeouts[index] = relevanceTimeout;
                }

                sharedState.QueueDeltaUpdate(networkObject);
                _pendingUpdatedObjects.Add(new PendingObjectUpdate(networkObject, false));
            }
        }
    }

    public new bool ProcessMessage(NetworkMessageId messageId, MemoryBuffer messageData)
    {
        if (base.ProcessMessage(messageId, messageData))
            return true;

        if (messageId != NetworkMessageId.ObjectsFeedbackUnreliable)
            return false;
        ProcessObjectsFeedbackUnreliable(messageData);
        return true;
    }

    public void SendMessages(NetworkFrame currentFrame, SharedReplicationState sharedState)
    {
        base.SendMessages();
        if (!IsSynchronized)
            return;

        SendRemoveObjects();
        SendAddObjects();
        SendUpdateObjectsReliable(sharedState);
        SendUpdateObjectsUnreliable(currentFrame, sharedState);
    }

    public float ReportedInputLoss { get; set; }

    public override void Dispose()
    {
        _componentBuffer.Dispose();
        _buffer.Dispose();
        base.Dispose();
    }

    private void ProcessObjectsFeedbackUnreliable(MemoryBuffer messageData)
    {
        if (!IsSynchronized)
        {
            Logging.Log.GetLogger().Warning("Connection {}: Received unexpected feedback", Peer.ToString());
            return;
        }

        var feedbackFrame = (NetworkFrame)messageData.ReadInt64();
        OnInputReceived(feedbackFrame);

        while (!messageData.IsEof())
        {
            var networkId = (NetworkId)messageData.ReadUInt();
            _componentBuffer.SetData(messageData.ReadBuffer());
            var networkObject = ObjectRegistry.GetNetworkObject(networkId);
            if (networkObject is null)
            {
                Logging.Log.GetLogger().Warning(
                    "Connection {}: Received feedback for unknown NetworkObject {}",
                    Peer.ToString(), TrackedComponent.ToString(networkId));
                continue;
            }

            if (!ReferenceEquals(networkObject.OwnerConnection, Peer))
            {
                Logging.Log.GetLogger().Warning(
                    "Connection {}: Received feedback for NetworkObject {} owned by connection #{}",
                    Peer.ToString(), TrackedComponent.ToString(networkId), networkObject.OwnerConnectionId);
                continue;
            }

            networkObject.ReadUnreliableFeedback(feedbackFrame, _componentBuffer);
        }
    }

    private void SendRemoveObjects()
    {
        using var writer = new MultiMessageWriter(Peer.Connection, _buffer,
            NetworkMessageId.RemoveObjects, PacketType.ReliableOrdered);
        var message = writer.Buffer;
        message.WriteInt64(CurrentFrame.Value);
        writer.CompleteHeader();

        foreach (var networkId in _pendingRemovedObjects)
        {
            message.WriteUInt((uint)networkId);
            AppendDebugInfo(writer, networkId);
            writer.CompletePayload();
        }
    }

    private void SendAddObjects()
    {
        using var writer = new LargeMessageWriter(Peer.Connection, _buffer,
            NetworkMessageId.AddObjectsIncomplete, NetworkMessageId.AddObjects);
        var message = writer.Buffer;
        message.WriteInt64(CurrentFrame.Value);

        var sendMessage = false;
        foreach (var update in _pendingUpdatedObjects)
        {
            if (!update.IsSnapshot)
                continue;

            sendMessage = true;
            message.WriteUInt((uint)update.NetworkObject.NetworkId);
            message.WriteStringHash(update.NetworkObject.GetTypeNameHash());
            message.WriteVLE(update.NetworkObject.OwnerConnectionId);
            _componentBuffer.Clear();
            update.NetworkObject.WriteSnapshot(CurrentFrame, _componentBuffer);
            message.WriteBuffer(_componentBuffer.Buffer);
            AppendDebugInfo(writer, update.NetworkObject.NetworkId);
        }

        if (!sendMessage)
            writer.Discard();
    }

    private void SendUpdateObjectsReliable(SharedReplicationState sharedState)
    {
        using var writer = new LargeMessageWriter(Peer.Connection, _buffer,
            NetworkMessageId.UpdateObjectsReliableIncomplete,
            NetworkMessageId.UpdateObjectsReliable);
        var message = writer.Buffer;
        message.WriteInt64(CurrentFrame.Value);

        var sendMessage = false;
        foreach (var update in _pendingUpdatedObjects)
        {
            if (update.IsSnapshot)
                continue;
            var updateData = sharedState.GetReliableUpdateByIndex(GetIndex(update.NetworkObject.NetworkId));
            if (updateData is null)
                continue;

            sendMessage = true;
            message.WriteUInt((uint)update.NetworkObject.NetworkId);
            message.WriteStringHash(update.NetworkObject.GetTypeNameHash());
            message.WriteBuffer(updateData);
            AppendDebugInfo(writer, update.NetworkObject.NetworkId);
        }

        if (!sendMessage)
            writer.Discard();
    }

    private void SendUpdateObjectsUnreliable(NetworkFrame currentFrame,
        SharedReplicationState sharedState)
    {
        using var writer = new MultiMessageWriter(Peer.Connection, _buffer,
            NetworkMessageId.UpdateObjectsUnreliable, PacketType.UnreliableUnordered);
        var message = writer.Buffer;
        message.WriteInt64(CurrentFrame.Value);
        writer.CompleteHeader();

        foreach (var update in _pendingUpdatedObjects)
        {
            if (update.IsSnapshot)
                continue;
            var index = GetIndex(update.NetworkObject.NetworkId);
            var updateData = sharedState.GetUnreliableUpdateByIndex(index);
            if (updateData is null)
                continue;

            var relevance = _objectsRelevance[index];
            Debug.Assert(relevance != NetworkObjectRelevance.Irrelevant);
            if (relevance == NetworkObjectRelevance.NoUpdates
                || currentFrame.Value % (uint)relevance != 0)
            {
                continue;
            }

            message.WriteUInt((uint)update.NetworkObject.NetworkId);
            message.WriteStringHash(update.NetworkObject.GetTypeNameHash());
            message.WriteBuffer(updateData);
            AppendDebugInfo(writer, update.NetworkObject.NetworkId);
            writer.CompletePayload();
        }
    }

    private static void AppendDebugInfo(MultiMessageWriter writer, NetworkId networkId)
    {
        if (writer.DebugInfo.Length != 0)
            writer.DebugInfo.Append(", ");
        writer.DebugInfo.Append(TrackedComponent.ToString(networkId));
    }

    private static void AppendDebugInfo(LargeMessageWriter writer, NetworkId networkId)
    {
        if (writer.DebugInfo.Length != 0)
            writer.DebugInfo.Append(", ");
        writer.DebugInfo.Append(TrackedComponent.ToString(networkId));
    }

    private static int GetIndex(NetworkId networkId) =>
        TrackedComponent.DeconstructComponentReference(networkId).Index;

    private readonly record struct PendingObjectUpdate(NetworkObject NetworkObject, bool IsSnapshot);

    private readonly List<NetworkObjectRelevance> _objectsRelevance = [];
    private readonly List<float> _objectsRelevanceTimeouts = [];
    private readonly List<NetworkId> _pendingRemovedObjects = [];
    private readonly List<PendingObjectUpdate> _pendingUpdatedObjects = [];
    private readonly VectorBuffer _componentBuffer = new();
    private readonly VectorBuffer _buffer = new();
}

/// <summary>Server part of the ReplicationManager subsystem.</summary>
public sealed class ServerReplicator : RedObject
{
    public ServerReplicator(Scene scene)
    {
        _network = Context.GetSubsystem<global::RedBolt.Network.Network>()!;
        _scene = scene;
        _replicationManager = scene.GetComponent<ReplicationManager>()!;
        _updateFrequency = _network.UpdateFps;
        _sharedState = new SharedReplicationState(_replicationManager);

        if (_replicationManager.IsFixedUpdateServer)
        {
            _updateSync = new SceneUpdateSynchronizer(scene,
                new SceneUpdateSynchronizerParams(true, _updateFrequency,
                    _replicationManager.IsAllowZeroUpdatesOnServer));
        }

        NetworkSettings.SetDefaultNetworkSetting(_settings, NetworkSettings.InternalProtocolVersion);
        NetworkSettings.SetNetworkSetting(_settings, NetworkSettings.UpdateFrequency, _updateFrequency);

        SubscribeToEvent<CoreEvents.InputReadyEvent>(HandleInputReady);
        SubscribeToEvent<NetworkEvents.NetworkUpdateEvent>(_network, HandleNetworkUpdate);
    }

    public void AddConnection(ReplicatedPeer connection)
    {
        if (_connections.ContainsKey(connection))
        {
            Debug.Fail($"Connection {connection} is already added");
            return;
        }

        _connections.Remove(connection);
        _connections.Add(connection,
            new ClientReplicationState(_replicationManager, connection, _settings));
    }

    public void RemoveConnection(ReplicatedPeer connection)
    {
        if (!_connections.Remove(connection, out var state))
        {
            Debug.Fail($"Connection {connection} is not added");
            return;
        }
        state.Dispose();
    }

    public bool ProcessMessage(ReplicatedPeer connection, NetworkMessageId messageId,
        MemoryBuffer messageData) =>
        GetClientState(connection)?.ProcessMessage(messageId, messageData) ?? false;

    public void ProcessSceneUpdate(bool postUpdate)
    {
        if (!_network.IsUpdateNow)
            return;

        var fixedTimeStep = 1.0f / UpdateFrequency;
        if (postUpdate)
        {
            var eventData = new SceneEvents.SceneNetworkPostUpdateEvent
            {
                Scene = _scene,
                TimeStepReplica = fixedTimeStep,
                TimeStepInput = fixedTimeStep
            };
            _scene.SendEvent(ref eventData);
        }
        else
        {
            var eventData = new SceneEvents.SceneNetworkUpdateEvent
            {
                Scene = _scene,
                TimeStepReplica = fixedTimeStep,
                TimeStepInput = fixedTimeStep
            };
            _scene.SendEvent(ref eventData);
        }
    }

    public void ReportInputLoss(ReplicatedPeer connection, float percentLoss)
    {
        if (GetClientState(connection) is { } clientState)
            clientState.ReportedInputLoss = percentLoss;
    }

    public void SetCurrentFrame(NetworkFrame frame) => _currentFrame = frame;

    public string DebugInfo
    {
        get
        {
            var result = $"Scene '{(_scene.Name.Length != 0 ? _scene.Name : "Unnamed")}': Time #{_currentFrame}\n";
            foreach (var (connection, clientState) in _connections)
            {
                result += $"Connection {connection}: Ping {connection.Ping}ms, InDelay "
                    + $"{clientState.InputDelay}+{clientState.InputBufferSize} frames, InLoss "
                    + $"{System.Math.Ceiling(clientState.ReportedInputLoss * 100.0f)}%\n";
            }
            return result;
        }
    }

    public Variant GetSetting(NetworkSetting setting) =>
        NetworkSettings.GetNetworkSetting(_settings, setting);
    public uint GetFeedbackDelay(ReplicatedPeer connection) =>
        GetClientState(connection) is { } state ? state.InputDelay + state.InputBufferSize : 0;
    public IReadOnlySet<NetworkObject> GetNetworkObjectsOwnedByConnection(ReplicatedPeer connection) =>
        _sharedState.GetOwnedObjectsByConnection(connection);
    public NetworkObject? GetNetworkObjectOwnedByConnection(ReplicatedPeer connection)
    {
        var ownedObjects = GetNetworkObjectsOwnedByConnection(connection);
        return ownedObjects.Count == 1 ? ownedObjects.First() : null;
    }
    public NetworkTime ServerTime => new(_currentFrame);
    public uint UpdateFrequency => _updateFrequency;
    public NetworkFrame CurrentFrame => _currentFrame;

    public override void Dispose()
    {
        foreach (var state in _connections.Values)
            state.Dispose();
        _connections.Clear();
        _sharedState.Dispose();
        _updateSync?.Dispose();
        _settings.Dispose();
        base.Dispose();
    }

    private void HandleInputReady(ref CoreEvents.InputReadyEvent eventData) =>
        OnInputReady(eventData.TimeStep, _network.IsUpdateNow, _network.UpdateOvertime);

    private void HandleNetworkUpdate(ref NetworkEvents.NetworkUpdateEvent eventData)
    {
        if (eventData.IsServer)
            OnNetworkUpdate();
    }

    private void OnInputReady(float timeStep, bool isUpdateNow, float overtime)
    {
        if (isUpdateNow)
        {
            ++_currentFrame;
            _updateSync?.Synchronize(_currentFrame, overtime);
            foreach (var clientState in _connections.Values)
                clientState.BeginNetworkFrame(_currentFrame, overtime);

            var eventData = new NetworkEvents.BeginServerNetworkFrameEvent { Frame = _currentFrame };
            _network.SendEvent(ref eventData);
        }
        else
            _updateSync?.Update(timeStep);
    }

    private void OnNetworkUpdate()
    {
        var eventData = new NetworkEvents.EndServerNetworkFrameEvent { Frame = _currentFrame };
        _network.SendEvent(ref eventData);

        _sharedState.PrepareForUpdate();
        foreach (var clientState in _connections.Values)
            clientState.UpdateNetworkObjects(_sharedState);
        _sharedState.CookDeltaUpdates(_currentFrame);
        foreach (var clientState in _connections.Values)
            clientState.SendMessages(_currentFrame, _sharedState);
    }

    private ClientReplicationState? GetClientState(ReplicatedPeer connection) =>
        _connections.GetValueOrDefault(connection);

    private readonly global::RedBolt.Network.Network _network;
    private readonly Scene _scene;
    private readonly ReplicationManager _replicationManager;
    private readonly VariantMap _settings = new();
    private readonly uint _updateFrequency;
    private NetworkFrame _currentFrame;
    private readonly SceneUpdateSynchronizer? _updateSync;
    private readonly SharedReplicationState _sharedState;
    private readonly Dictionary<ReplicatedPeer, ClientReplicationState> _connections = [];
}
