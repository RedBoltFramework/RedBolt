// Copyright (c) 2017-2022 the Urho3D project.
// This work is licensed under the terms of the MIT license.

namespace RedBolt.Network;

using System.Net;
using System.Net.Sockets;

using RedBolt.Core;
using RedBolt.Container;
using RedBolt.IO;
using Timer = RedBolt.Core.Timer;

[Flags]
public enum LANDiscoveryMode
{
    Local = 1,
    LAN = 2,
    All = Local | LAN
}

public sealed class LANDiscoveryManager : RedObject
{
    public void SetBroadcastData(VariantMap data)
    {
        _broadcastData.Dispose();
        _broadcastData = new VariantMap(data);
    }

    public bool Start(ushort port, LANDiscoveryMode mode = LANDiscoveryMode.All)
    {
        try
        {
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _socket.EnableBroadcast = true;

            if (_broadcastData.Count == 0)
            {
                var address = mode.HasFlag(LANDiscoveryMode.LAN)
                    ? IPAddress.Any
                    : IPAddress.Loopback;
                _socket.Blocking = false;
                _socket.Bind(new IPEndPoint(address, port));
                SubscribeToEvent<CoreEvents.UpdateEvent>(HandleReceiveUpdate);
            }
            else
            {
                _port = port;
                _mode = mode;
                SubscribeToEvent<CoreEvents.UpdateEvent>(HandleBroadcastUpdate);
            }
            return true;
        }
        catch (SocketException)
        {
            _socket?.Dispose();
            _socket = null;
            return false;
        }
    }

    public void Stop()
    {
        _socket?.Dispose();
        _socket = null;
        UnsubscribeFromAllEvents();
    }

    public uint BroadcastTimeMs { get; set; } = 5000;

    public override void Dispose()
    {
        Stop();
        _broadcastData.Dispose();
        _buffer.Dispose();
        base.Dispose();
    }

    private void HandleReceiveUpdate(ref CoreEvents.UpdateEvent eventData)
    {
        if (_timer.GetMSec(false) < BroadcastTimeMs || _socket is null)
            return;

        _timer.Reset();
        EndPoint remote = new IPEndPoint(IPAddress.Any, 0);
        try
        {
            var bytesRead = _socket.ReceiveFrom(_receiveBuffer, ref remote);
            if (bytesRead <= 0 || remote is not IPEndPoint endpoint)
                return;

            using var message = new MemoryBuffer(_receiveBuffer.AsMemory(0, bytesRead));
            if (message.ReadStringHash() != Magic)
                return;

            var args = new NetworkEvents.NetworkHostDiscoveredEvent
            {
                Address = endpoint.Address.ToString(),
                Port = endpoint.Port,
                Beacon = message.ReadVariantMap()
            };
            SendEvent(ref args);
        }
        catch (SocketException exception) when (exception.SocketErrorCode is
            SocketError.WouldBlock or SocketError.TryAgain)
        {
        }
    }

    private void HandleBroadcastUpdate(ref CoreEvents.UpdateEvent eventData)
    {
        if (_timer.GetMSec(false) < BroadcastTimeMs || _socket is null)
            return;

        _timer.Reset();
        _buffer.Clear();
        _buffer.WriteStringHash(Magic);
        _buffer.WriteVariantMap(_broadcastData);

        try
        {
            if (_mode.HasFlag(LANDiscoveryMode.LAN))
                _socket.SendTo(_buffer.Buffer, SocketFlags.None,
                    new IPEndPoint(IPAddress.Broadcast, _port));
            if (_mode.HasFlag(LANDiscoveryMode.Local))
                _socket.SendTo(_buffer.Buffer, SocketFlags.None,
                    new IPEndPoint(IPAddress.Loopback, _port));
        }
        catch (SocketException)
        {
            Stop();
        }
    }

    private static readonly StringHash Magic = new("rbfx-LANDiscovery-v1");

    private VariantMap _broadcastData = new();
    private Socket? _socket;
    private readonly Timer _timer = new();
    private readonly VectorBuffer _buffer = new();
    private readonly byte[] _receiveBuffer = new byte[ushort.MaxValue];
    private ushort _port;
    private LANDiscoveryMode _mode;
}
