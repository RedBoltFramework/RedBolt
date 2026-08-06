// Copyright (c) 2008-2022 the Urho3D project.
// This work is licensed under the terms of the MIT license.

namespace RedBolt.Network;

using RedBolt.Core;
using RedBolt.IO;

public struct NetworkStatisticsSnapshot
{
    public ulong BytesInPerSec;
    public ulong BytesOutPerSec;
    public int PacketsInPerSec;
    public int PacketsOutPerSec;

    public static NetworkStatisticsSnapshot operator +(
        NetworkStatisticsSnapshot lhs, NetworkStatisticsSnapshot rhs)
    {
        lhs.BytesInPerSec += rhs.BytesInPerSec;
        lhs.BytesOutPerSec += rhs.BytesOutPerSec;
        lhs.PacketsInPerSec += rhs.PacketsInPerSec;
        lhs.PacketsOutPerSec += rhs.PacketsOutPerSec;
        return lhs;
    }
}

public class NetworkStatisticsCounter
{
    public NetworkStatisticsCounter(uint windowSize = 10, uint intervalMs = 1000)
    {
        var size = unchecked((int)windowSize);
        _packetCounterIncoming = new TimedCounter(size, intervalMs);
        _packetCounterOutgoing = new TimedCounter(size, intervalMs);
        _bytesCounterIncoming = new TimedCounter(size, intervalMs);
        _bytesCounterOutgoing = new TimedCounter(size, intervalMs);
    }

    public NetworkStatisticsCounter(NetworkConnection connection,
        uint windowSize = 10, uint intervalMs = 1000)
        : this(windowSize, intervalMs)
    {
        Attach(connection);
    }

    public void Attach(NetworkConnection? connection)
    {
        if (connection is null || ReferenceEquals(_connection, connection))
            return;

        _connection = connection;
        connection.SendDataRequested += OnSendData;
        connection.DataReceived += OnData;
    }

    public void AddIncomingPacket(uint bytes)
    {
        _packetCounterIncoming.AddSample(1);
        _bytesCounterIncoming.AddSample(bytes);
    }

    public void AddOutgoingPacket(uint bytes)
    {
        _packetCounterOutgoing.AddSample(1);
        _bytesCounterOutgoing.AddSample(bytes);
    }

    public ulong BytesInPerSec => (ulong)_bytesCounterIncoming.GetLast();
    public ulong BytesOutPerSec => (ulong)_bytesCounterOutgoing.GetLast();
    public int PacketsInPerSec => (int)_packetCounterIncoming.GetLast();
    public int PacketsOutPerSec => (int)_packetCounterOutgoing.GetLast();

    public NetworkStatisticsSnapshot Snapshot => new()
    {
        BytesInPerSec = BytesInPerSec,
        BytesOutPerSec = BytesOutPerSec,
        PacketsInPerSec = PacketsInPerSec,
        PacketsOutPerSec = PacketsOutPerSec
    };

    private void OnSendData(NetworkConnection connection, MemoryBuffer data,
        PacketType type, ref bool handled)
    {
        if (!handled)
            AddOutgoingPacket(data.Size);
    }

    private void OnData(NetworkConnection connection, MemoryBuffer data, ref bool handled)
    {
        AddIncomingPacket(data.Size);
    }

    private readonly TimedCounter _packetCounterIncoming;
    private readonly TimedCounter _packetCounterOutgoing;
    private readonly TimedCounter _bytesCounterIncoming;
    private readonly TimedCounter _bytesCounterOutgoing;
    private NetworkConnection? _connection;
}
