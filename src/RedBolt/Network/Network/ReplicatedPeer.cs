// Copyright (c) 2026-2026 the rbfx project.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Network;

using RedBolt.Container;
using RedBolt.Core;
using RedBolt.IO;
using RedBolt.Replica;

/// <summary>Replication-oriented interface attached to a transport connection.</summary>
public class ReplicatedPeer : IDisposable
{
    public ReplicatedPeer(NetworkConnection connection, uint pingIntervalMs = 250,
        uint maxPingMs = 10000, uint clockBufferSize = 40, uint pingBufferSize = 10,
        Func<uint>? getTimestamp = null)
    {
        ArgumentNullException.ThrowIfNull(connection);

        _connection = connection;
        _clock = new ClockSynchronizer(pingIntervalMs, maxPingMs, clockBufferSize,
            pingBufferSize, getTimestamp);
        _listener = new Listener(this);
    }

    public uint ObjectId => _idFamily.GetObjectID();
    public NetworkConnection Connection => _connection;

    public void SetReplicationManager(ReplicationManager? replicationManager)
    {
        if (ReferenceEquals(_replicationManager, replicationManager))
            return;

        if (_replicationManager is not null && _connection.IsConnected)
            OnDisconnected();

        _replicationManager = replicationManager;

        if (_replicationManager is not null && _connection.IsConnected)
            OnConnected();
    }

    public ReplicationManager? ReplicationManager => _replicationManager;

    public override string ToString()
    {
        if (_connection.IsDisconnected)
            return $"#{ObjectId} <disconnected>";
        return $"#{ObjectId} {_connection.Address}:{_connection.Port}";
    }

    public virtual bool IsClockSynchronized => _clock.IsReady;
    public virtual uint RemoteToLocalTime(uint time) => _clock.RemoteToLocal(time);
    public virtual uint LocalToRemoteTime(uint time) => _clock.LocalToRemote(time);
    public virtual uint LocalTime => Time.GetSystemTime();
    public virtual uint LocalTimeOfLatestRoundtrip => _clock.LocalTimeOfLatestRoundtrip;
    public virtual uint Ping => _clock.Ping;
    public ClockSynchronizer Clock => _clock;

    public virtual void Dispose()
    {
        SetReplicationManager(null);
        _listener.Dispose();
        _idFamily.Dispose();
        GC.SuppressFinalize(this);
    }

    private void ProcessReplicationMessage(NetworkMessageId messageId, MemoryBuffer message, ref bool handled)
    {
        if (handled || _replicationManager is null)
            return;

        if (messageId == NetworkMessageId.ClockSync)
        {
            var clockMessage = NetworkMessageUtils.ReadSerializedMessage<ClockSynchronizerMessage>(message);
            Clock.ProcessMessage(clockMessage);
            handled = true;
        }

        if (_replicationManager is not null)
            handled |= _replicationManager.ProcessMessage(this, messageId, message);
    }

    private void SendReplicationMessages()
    {
        if (!_connection.IsConnected || _replicationManager is null)
            return;

        while (Clock.PollMessage() is { } clockMessage)
        {
            NetworkMessageUtils.WriteSerializedMessage(_connection, NetworkMessageId.ClockSync,
                clockMessage, PacketType.UnreliableUnordered);
        }
    }

    private void OnConnected()
    {
        if (_replicationManager is null)
            return;

        if (_replicationManager.IsServer)
            _replicationManager.ServerReplicator!.AddConnection(this);
        else
            _replicationManager.StartClient(this);
    }

    private void OnDisconnected()
    {
        _replicationManager?.DropConnection(this);
    }

    private sealed class Listener : RedObject
    {
        public Listener(ReplicatedPeer owner)
        {
            _owner = owner;
            owner._connection.MessageReceived += HandleMessageReceived;
            owner._connection.Connected += HandleConnected;
            owner._connection.Disconnected += HandleDisconnected;
            SubscribeToEvent<CoreEvents.PostUpdateEvent>(HandlePostUpdate);
        }

        public override void Dispose()
        {
            _owner._connection.MessageReceived -= HandleMessageReceived;
            _owner._connection.Connected -= HandleConnected;
            _owner._connection.Disconnected -= HandleDisconnected;
            base.Dispose();
        }

        private void HandlePostUpdate(ref CoreEvents.PostUpdateEvent eventData) =>
            _owner.SendReplicationMessages();

        private void HandleMessageReceived(NetworkConnection connection, NetworkMessageId messageId,
            MemoryBuffer message, ref bool handled) =>
            _owner.ProcessReplicationMessage(messageId, message, ref handled);

        private void HandleConnected(NetworkConnection connection) => _owner.OnConnected();
        private void HandleDisconnected(NetworkConnection connection) => _owner.OnDisconnected();

        private readonly ReplicatedPeer _owner;
    }

    private readonly NetworkConnection _connection;
    private readonly ClockSynchronizer _clock;
    private readonly Listener _listener;
    private readonly IDFamily<ReplicatedPeer> _idFamily = new();
    private ReplicationManager? _replicationManager;
}
