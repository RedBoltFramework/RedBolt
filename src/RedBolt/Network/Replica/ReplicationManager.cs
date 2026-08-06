// Copyright (c) 2020-2025 the rbfx project.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Replica;

using System.Diagnostics;

using RedBolt.Container;
using RedBolt.Core;
using RedBolt.IO;
using RedBolt.Network;
using RedBolt.Scene;

/// <summary>Part of ReplicationManager used by both client and server.</summary>
[Reflected(ObjectCategory.Category_Network, Factory = false)]
public partial class NetworkObjectRegistry : ReferencedComponentRegistryBase
{
    public NetworkObjectRegistry()
        : base(new StringHash(typeof(NetworkObject).GetClassName()))
    {
    }

    public event Action<NetworkObject>? NetworkObjectAdded;
    public event Action<NetworkObject>? NetworkObjectRemoved;

    public void RemoveAllNetworkObjects()
    {
        var nodesToRemove = NetworkObjects.Select(networkObject => networkObject.Node).ToArray();
        foreach (var node in nodesToRemove)
            node?.Remove();

        _networkObjectsDirty.Clear();
    }

    public void QueueNetworkObjectUpdate(NetworkObject networkObject)
    {
        var networkId = networkObject.NetworkId;
        if (!ReferenceEquals(GetNetworkObject(networkId), networkObject))
        {
            Logging.Log.GetLogger().Warning("Cannot queue update for unknown NetworkObject {}",
                TrackedComponent.ToString(networkId));
            return;
        }

        var (index, _) = TrackedComponent.DeconstructComponentReference(networkId);
        _networkObjectsDirty[index] = true;
    }

    public void UpdateNetworkObjects()
    {
        for (var index = 0; index < _networkObjectsDirty.Count; ++index)
        {
            if (!_networkObjectsDirty[index])
                continue;

            var networkObject = GetNetworkObjectByIndex(index);
            if (networkObject is not null)
            {
                networkObject.UpdateObjectHierarchy();
                _ = networkObject.Node!.GetWorldTransform();
            }
            _networkObjectsDirty[index] = false;
        }
    }

    public void GetSortedNetworkObjects(List<NetworkObject> networkObjects)
    {
        networkObjects.Clear();

        foreach (var networkObject in NetworkObjects)
        {
            if (networkObject.ParentNetworkObject is null)
                networkObjects.Add(networkObject);
        }

        for (var i = 0; i < networkObjects.Count; ++i)
            networkObjects.AddRange(networkObjects[i].ChildrenNetworkObjects);
    }

    public IReadOnlyList<NetworkObject> NetworkObjects => TrackedComponents.Cast<NetworkObject>().ToArray();
    public int NetworkIndexUpperBound => ReferenceIndexUpperBound;
    public NetworkObject? GetNetworkObject(NetworkId networkId, bool checkVersion = true) =>
        GetTrackedComponentByReference(networkId, checkVersion) as NetworkObject;
    public NetworkObject? GetNetworkObjectByIndex(int networkIndex) =>
        GetTrackedComponentByReferenceIndex(networkIndex) as NetworkObject;

    protected override void OnComponentAdded(TrackedComponentBase baseComponent)
    {
        base.OnComponentAdded(baseComponent);

        var networkObject = (NetworkObject)baseComponent;
        var (index, _) = TrackedComponent.DeconstructComponentReference(networkObject.NetworkId);
        while (_networkObjectsDirty.Count <= index)
            _networkObjectsDirty.Add(false);
        _networkObjectsDirty[index] = true;

        NetworkObjectAdded?.Invoke(networkObject);
    }

    protected override void OnComponentRemoved(TrackedComponentBase baseComponent)
    {
        var networkObject = (NetworkObject)baseComponent;
        if (networkObject.ParentNetworkObject is { } parentObject
            && parentObject.NetworkId != NetworkId.None)
        {
            QueueNetworkObjectUpdate(parentObject);
        }

        NetworkObjectRemoved?.Invoke(networkObject);
        base.OnComponentRemoved(baseComponent);
    }

    private readonly List<bool> _networkObjectsDirty = [];
}

public enum ReplicationManagerMode
{
    Standalone,
    Server,
    Client
}

/// <summary>Root-level scene component that manages scene replication.</summary>
[Reflected(ObjectCategory.Category_Subsystem)]
public partial class ReplicationManager : NetworkObjectRegistry
{
    [Serialized("Is Fixed Update Server")]
    public bool IsFixedUpdateServer { get; set; } = true;

    [Serialized("Allow Zero Updates On Server")]
    public bool IsAllowZeroUpdatesOnServer { get; set; }

    public void StartStandalone()
    {
        Stop();
        _mode = ReplicationManagerMode.Standalone;

        foreach (var networkObject in NetworkObjects)
        {
            networkObject.SetNetworkMode(NetworkObjectMode.Standalone);
            networkObject.InitializeStandalone();
        }
    }

    public void StartServer()
    {
        Stop();
        _mode = ReplicationManagerMode.Server;
        _server = new ServerReplicator(Scene!);
    }

    public void StartClient(ReplicatedPeer connectionToServer)
    {
        Stop();
        _mode = ReplicationManagerMode.Client;
        _client = new ClientData { Peer = connectionToServer };
        RemoveAllNetworkObjects();
    }

    public bool ProcessMessage(ReplicatedPeer connection, NetworkMessageId messageId,
        MemoryBuffer messageData)
    {
        if (_client is not null)
        {
            return _client.Replica is null
                ? ProcessMessageOnUninitializedClient(connection, messageId, messageData)
                : _client.Replica.ProcessMessage(messageId, messageData);
        }

        if (_server is not null)
            return _server.ProcessMessage(connection, messageId, messageData);
        return false;
    }

    public void DropConnection(ReplicatedPeer connection)
    {
        if (_server is not null)
            _server.RemoveConnection(connection);
        else if (_client is not null && ReferenceEquals(_client.Peer, connection))
            StartStandalone();
    }

    public uint UpdateFrequency => _server?.UpdateFrequency
        ?? _client?.Replica?.UpdateFrequency
        ?? NetworkSettings.UpdateFrequency.DefaultValue.GetUInt();

    public float TraceDurationInSeconds => _server is not null
        ? _server.GetSetting(NetworkSettings.ServerTracingDuration).GetFloat()
        : _client?.Replica is not null
            ? _client.Replica.GetSetting(NetworkSettings.ClientTracingDuration).GetFloat()
            : 0.0f;

    public uint TraceDurationInFrames
    {
        get
        {
            var updateFrequency = GetSetting(NetworkSettings.UpdateFrequency).GetUInt();
            return System.Math.Max(1u,
                checked((uint)System.Math.Ceiling(TraceDurationInSeconds * updateFrequency)));
        }
    }

    public Variant GetSetting(NetworkSetting setting)
    {
        if (_server is not null)
            return _server.GetSetting(setting);
        if (_client?.Replica is not null)
            return _client.Replica.GetSetting(setting);
        return Variant.Empty;
    }

    public string DebugInfo => _client?.Replica?.DebugInfo
        ?? (_client is not null ? GetUninitializedClientDebugInfo() : _server?.DebugInfo ?? string.Empty);
    public bool IsStandalone => _mode == ReplicationManagerMode.Standalone;
    public bool IsServer => _mode == ReplicationManagerMode.Server;
    public bool IsClient => _mode == ReplicationManagerMode.Client;
    public ClientReplica? ClientReplica => _client?.Replica;
    public ServerReplicator? ServerReplicator => _server;

    public override void Dispose()
    {
        Stop();
        base.Dispose();
    }

    protected override void OnSceneSet(Scene? previousScene, Scene? scene)
    {
        base.OnSceneSet(previousScene, scene);

        if (previousScene is not null)
        {
            UnsubscribeFromEvent<SceneEvents.SceneUpdateEvent>(previousScene);
            UnsubscribeFromEvent<SceneEvents.ScenePostUpdateEvent>(previousScene);
        }
        if (scene is not null)
        {
            SubscribeToEvent<SceneEvents.SceneUpdateEvent>(scene, HandleSceneUpdate);
            SubscribeToEvent<SceneEvents.ScenePostUpdateEvent>(scene, HandleScenePostUpdate);
        }
    }

    protected override void OnComponentAdded(TrackedComponentBase baseComponent)
    {
        base.OnComponentAdded(baseComponent);
        if (IsStandalone)
            _recentlyAddedObjects.Add(((NetworkObject)baseComponent).NetworkId);
    }

    protected override void OnComponentRemoved(TrackedComponentBase baseComponent)
    {
        if (IsStandalone)
            _recentlyAddedObjects.Remove(((NetworkObject)baseComponent).NetworkId);
        base.OnComponentRemoved(baseComponent);
    }

    private void HandleSceneUpdate(ref SceneEvents.SceneUpdateEvent eventData) =>
        HandleSceneUpdate(false, eventData.TimeStep);

    private void HandleScenePostUpdate(ref SceneEvents.ScenePostUpdateEvent eventData) =>
        HandleSceneUpdate(true, eventData.TimeStep);

    private void HandleSceneUpdate(bool postUpdate, float timeStep)
    {
        switch (_mode)
        {
            case ReplicationManagerMode.Standalone:
                Debug.Assert(_server is null && _client is null);
                InitializeObjectsStandalone();
                SendSceneNetworkEvent(postUpdate, timeStep, timeStep);
                break;

            case ReplicationManagerMode.Server:
                Debug.Assert(_server is not null);
                _server.ProcessSceneUpdate(postUpdate);
                break;

            case ReplicationManagerMode.Client:
                Debug.Assert(_client is not null);
                _client.Replica?.ProcessSceneUpdate(postUpdate);
                break;
        }

        if (postUpdate)
            UpdateNetworkObjects();
    }

    private void SendSceneNetworkEvent(bool postUpdate, float replicaTimeStep, float inputTimeStep)
    {
        if (postUpdate)
        {
            var eventData = new SceneEvents.SceneNetworkPostUpdateEvent
            {
                Scene = Scene!,
                TimeStepReplica = replicaTimeStep,
                TimeStepInput = inputTimeStep
            };
            Scene!.SendEvent(ref eventData);
        }
        else
        {
            var eventData = new SceneEvents.SceneNetworkUpdateEvent
            {
                Scene = Scene!,
                TimeStepReplica = replicaTimeStep,
                TimeStepInput = inputTimeStep
            };
            Scene!.SendEvent(ref eventData);
        }
    }

    private void InitializeObjectsStandalone()
    {
        foreach (var networkId in _recentlyAddedObjects)
        {
            var networkObject = GetNetworkObject(networkId);
            if (networkObject is null)
            {
                Debug.Fail("Cannot find recently added NetworkObject");
                continue;
            }

            networkObject.SetNetworkMode(NetworkObjectMode.Standalone);
            networkObject.InitializeStandalone();
        }
        _recentlyAddedObjects.Clear();
    }

    private void Stop()
    {
        if (_client is not null)
        {
            _client.Replica?.Dispose();
            _client.ServerSettings?.Dispose();
            _client = null;
        }

        if (_server is not null)
        {
            _server.Dispose();
            _server = null;
        }

        _recentlyAddedObjects.Clear();
        _mode = ReplicationManagerMode.Standalone;
    }

    private bool ProcessMessageOnUninitializedClient(ReplicatedPeer connection,
        NetworkMessageId messageId, MemoryBuffer messageData)
    {
        Debug.Assert(_client is not null && _client.Replica is null);

        if (messageId == NetworkMessageId.Configure)
        {
            var message = NetworkMessageUtils.ReadSerializedMessage<MsgConfigure>(messageData);
            _client.AckMagic = message.Magic;
            _client.ServerSettings?.Dispose();
            _client.ServerSettings = message.Settings;
        }
        else if (messageId == NetworkMessageId.SceneClock)
            _client.InitialClock = NetworkMessageUtils.ReadSerializedMessage<MsgSceneClock>(messageData);
        else
            return false;

        if (connection.IsClockSynchronized && _client.IsReadyToInitialize)
        {
            if (_client.Peer is null)
                return false;

            _client.Replica = new ClientReplica(Scene!, _client.Peer,
                _client.InitialClock!.Value, _client.ServerSettings!);
            NetworkMessageUtils.WriteSerializedMessage(connection.Connection,
                NetworkMessageId.Synchronized, new MsgSynchronized { Magic = _client.AckMagic!.Value },
                PacketType.ReliableUnordered);
        }

        return true;
    }

    private string GetUninitializedClientDebugInfo()
    {
        var waitList = new List<string>();
        if (_client!.Peer is not null && !_client.Peer.IsClockSynchronized)
            waitList.Add("system clock");
        if (_client.ServerSettings is null)
            waitList.Add("settings");
        if (!_client.InitialClock.HasValue || waitList.Count == 0)
            waitList.Add("server scene time");
        return $"Connecting... Waiting for {string.Join(", ", waitList)}...";
    }

    private sealed class ClientData
    {
        public ReplicatedPeer? Peer;
        public MsgSceneClock? InitialClock;
        public VariantMap? ServerSettings;
        public uint? AckMagic;
        public ClientReplica? Replica;

        public bool IsReadyToInitialize => Peer is not null && InitialClock.HasValue
            && ServerSettings is not null && AckMagic.HasValue;
    }

    private ReplicationManagerMode _mode;
    private ServerReplicator? _server;
    private ClientData? _client;
    private readonly HashSet<NetworkId> _recentlyAddedObjects = [];
}
