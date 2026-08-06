// Copyright (c) 2017-2025 the rbfx project.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Network;

using System.Text;

using RedBolt.Core;
using RedBolt.IO;

public delegate void NetworkDataHandler(
    NetworkConnection connection, MemoryBuffer data, ref bool handled);
public delegate void NetworkSendDataHandler(
    NetworkConnection connection, MemoryBuffer data, PacketType type, ref bool handled);
public delegate void NetworkMessageHandler(
    NetworkConnection connection, NetworkMessageId messageId, MemoryBuffer data, ref bool handled);
public delegate void NetworkSendMessageHandler(
    NetworkConnection connection, NetworkMessageId messageId, MemoryBuffer data, PacketType type, ref bool handled);

/// <summary>RAII-style wrapper for building network messages.</summary>
public sealed class NetworkMessageBuilder : IDisposable
{
    internal NetworkMessageBuilder(
        NetworkConnection connection, PacketType type, VectorBuffer buffer, uint offset)
    {
        _connection = connection;
        Type = type;
        Buffer = buffer;
        Offset = offset;
    }

    public bool IsValid => _connection is not null;

    public void EndMessage()
    {
        if (Finished || !IsValid)
            return;

        _connection!.EndMessage(this);
        Finished = true;
    }

    public void Dispose()
    {
        EndMessage();
        GC.SuppressFinalize(this);
    }

    public VectorBuffer Buffer { get; }
    public uint Offset { get; }
    public bool Finished { get; private set; }
    internal PacketType Type { get; }

    private NetworkConnection? _connection;
}

public enum NetworkConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Disconnecting
}

public abstract class NetworkConnection : RedObject
{
    protected NetworkConnection()
    {
        _workQueue = Context.GetSubsystem<WorkQueue>()!;
    }

    public abstract bool Connect(URL url);

    public virtual void Disconnect()
    {
        NotifyDisconnecting();
    }

    public virtual bool SendData(MemoryBuffer data, PacketType type = PacketType.ReliableOrdered)
    {
        var eventData = new NetworkEvents.ClientSendDataEvent
        {
            Connection = this,
            Type = type,
            Data = data
        };
        SendEvent(ref eventData);

        var handled = eventData.Handled;
        if (!handled)
            SendDataRequested?.Invoke(this, data, type, ref handled);
        return !handled;
    }

    public virtual bool SendMessage(NetworkMessageId messageId, MemoryBuffer data,
        PacketType type = PacketType.ReliableOrdered, string debugInfo = "")
    {
        _debugInfo.Clear();
        _debugInfo.Append(debugInfo);
        var eventData = new NetworkEvents.ClientSendMessageEvent
        {
            Connection = this,
            MessageId = messageId,
            Type = type,
            Data = data
        };
        SendEvent(ref eventData);

        var handled = eventData.Handled;
        if (!handled)
            SendMessageRequested?.Invoke(this, messageId, data, type, ref handled);
        if (handled)
            return false;

        _outgoing.WriteVLE((uint)messageId);
        CopyAll(data, _outgoing);
        var sent = SendData(new MemoryBuffer(_outgoing), type);
        _outgoing.Clear();
        return sent;
    }

    public virtual uint MaxMessageSize => System.Math.Min(
        (uint)NetworkProtocol.MaxNetworkMessageSize,
        unchecked(_maxPacketSize - (uint)NetworkProtocol.NetworkMessageHeaderSize));

    public virtual NetworkMessageBuilder BeginMessage(NetworkMessageId messageId,
        PacketType type = PacketType.ReliableOrdered, string debugInfo = "")
    {
        _debugInfo.Clear();
        _debugInfo.Append(debugInfo);
        _outgoing.WriteVLE((uint)messageId);
        return new NetworkMessageBuilder(this, type, _outgoing, _outgoing.Position);
    }

    public virtual void EndMessage(NetworkMessageBuilder builder)
    {
        SendData(new MemoryBuffer(builder.Buffer), builder.Type);
        builder.Buffer.Clear();
    }

    public string Address => _address;
    public ushort Port => _port;
    public NetworkConnectionState State => _state;
    public bool IsConnected => _state == NetworkConnectionState.Connected;
    public bool IsDisconnected => _state == NetworkConnectionState.Disconnected;
    public uint MaxPacketSize { get => _maxPacketSize; set => _maxPacketSize = value; }
    public StringBuilder DebugInfoBuffer => _debugInfo;
    public bool ProcessDataOnMainThread { get; set; } = true;
    public NetworkServer? Server => _server;

    public event Action<NetworkConnection>? Connected;
    public event Action<NetworkConnection>? Disconnected;
    public event NetworkDataHandler? DataReceived;
    public event NetworkMessageHandler? MessageReceived;
    public event NetworkSendDataHandler? SendDataRequested;
    public event NetworkSendMessageHandler? SendMessageRequested;

    public virtual void OnConnected()
    {
        Connected?.Invoke(this);
        var eventData = new NetworkEvents.ClientConnectedEvent
        {
            Connection = this,
            Address = _address,
            Port = _port
        };
        SendEvent(ref eventData);
    }

    public virtual void OnDisconnected()
    {
        Disconnected?.Invoke(this);
        var eventData = new NetworkEvents.ClientDisconnectedEvent
        {
            Connection = this,
            Address = _address,
            Port = _port
        };
        SendEvent(ref eventData);
    }

    public virtual bool OnData(MemoryBuffer message)
    {
        var messageId = (NetworkMessageId)message.ReadVLE();
        var eventData = new NetworkEvents.ClientMessageEvent
        {
            Connection = this,
            MessageId = messageId,
            Data = message
        };
        SendEvent(ref eventData);

        var handled = eventData.Handled;
        if (!handled)
            MessageReceived?.Invoke(this, messageId, message, ref handled);
        return handled || OnMessage(messageId, message);
    }

    public virtual bool OnMessage(NetworkMessageId messageId, MemoryBuffer message) => false;

    protected void NotifyDisconnecting()
    {
        Context.GetSubsystem<Network>()?.OnConnectionDisconnecting(this);
    }

    protected void SetServer(NetworkServer? server)
    {
        _server = server;
    }

    protected void DoOnConnected()
    {
        _workQueue.PostTaskForMainThread(() =>
        {
            _state = NetworkConnectionState.Connected;
            OnConnected();
        });
    }

    protected void DoOnDisconnected()
    {
        _workQueue.PostTaskForMainThread(() =>
        {
            _state = NetworkConnectionState.Disconnected;
            OnDisconnected();
        });
    }

    protected void DoOnData(MemoryBuffer message)
    {
        if (!ProcessDataOnMainThread || WorkQueue.IsMainThread())
        {
            var eventData = new NetworkEvents.ClientDataEvent { Connection = this, Data = message };
            SendEvent(ref eventData);

            var handled = eventData.Handled;
            if (!handled)
                DataReceived?.Invoke(this, message, ref handled);
            if (!handled)
                OnData(message);
        }
        else
            QueueIncomingPacket(message);
    }

    protected void DoOnMessage(NetworkMessageId messageId, MemoryBuffer message)
    {
        OnMessage(messageId, message);
    }

    protected NetworkConnectionState _state = NetworkConnectionState.Disconnected;
    protected string _address = string.Empty;
    protected ushort _port;

    private void QueueIncomingPacket(MemoryBuffer message)
    {
        var packet = CopyBuffer(message);
        lock (_mutex)
        {
            _incoming.Add(packet);
            if (_incoming.Count > 1)
                return;
        }
        _workQueue.PostDelayedTaskForMainThread(ProcessQueuedPackets);
    }

    private void ProcessQueuedPackets()
    {
        lock (_mutex)
        {
            (_incoming, _incomingBack) = (_incomingBack, _incoming);
        }

        foreach (var packet in _incomingBack)
        {
            using var message = new MemoryBuffer(packet);
            DoOnData(message);
        }

        lock (_mutex)
        {
            foreach (var packet in _incomingBack)
            {
                packet.Clear();
                _buffers.Add(packet);
            }
            _incomingBack.Clear();
        }
    }

    private VectorBuffer CopyBuffer(MemoryBuffer message)
    {
        VectorBuffer packet;
        lock (_mutex)
        {
            if (_buffers.Count != 0)
            {
                packet = _buffers[^1];
                _buffers.RemoveAt(_buffers.Count - 1);
            }
            else
                packet = new VectorBuffer();
        }

        var oldPosition = message.Position;
        message.Seek(0);
        var data = new byte[checked((int)message.Size)];
        message.Read(data);
        message.Seek(oldPosition);
        packet.SetData(data);
        return packet;
    }

    private static void CopyAll(MemoryBuffer source, VectorBuffer destination)
    {
        if (source.Size == 0)
            return;

        var oldPosition = source.Position;
        source.Seek(0);
        var data = new byte[checked((int)source.Size)];
        source.Read(data);
        source.Seek(oldPosition);
        destination.Write(data);
    }

    private uint _maxPacketSize = NetworkProtocol.MaxNetworkPacketSize;
    private readonly VectorBuffer _outgoing = new();
    private readonly StringBuilder _debugInfo = new();
    private NetworkServer? _server;

    private readonly object _mutex = new();
    private readonly WorkQueue _workQueue;
    private List<VectorBuffer> _incoming = [];
    private List<VectorBuffer> _incomingBack = [];
    private readonly List<VectorBuffer> _buffers = [];
}
