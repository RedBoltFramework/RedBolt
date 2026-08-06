// Copyright (c) 2008-2022 the Urho3D project.
// This work is licensed under the terms of the MIT license.

namespace RedBolt.Network;

using RedBolt.Core;
using RedBolt.IO;
using RedBolt.Replica;

public static class NetworkEvents
{
    /// <summary>Incoming network messages are processed on the client or server.</summary>
    public struct NetworkInputProcessedEvent
    {
        public float TimeStep;
    }

    /// <summary>Begin the frame of network update on the server.</summary>
    public struct BeginServerNetworkFrameEvent
    {
        public NetworkFrame Frame;
    }

    /// <summary>End the frame of network update on the server.</summary>
    public struct EndServerNetworkFrameEvent
    {
        public NetworkFrame Frame;
    }

    /// <summary>Begin the input frame on the client.</summary>
    public struct BeginClientNetworkFrameEvent
    {
        public NetworkFrame Frame;
    }

    /// <summary>End the input frame on the client.</summary>
    public struct EndClientNetworkFrameEvent
    {
        public NetworkFrame Frame;
    }

    /// <summary>About to send network update on the client or server.</summary>
    public struct NetworkUpdateEvent
    {
        public bool IsServer;
    }

    /// <summary>Network update has been sent on the client or server.</summary>
    public struct NetworkUpdateSentEvent
    {
        public bool IsServer;
    }

    /// <summary>LAN discovery found a hosted server.</summary>
    public struct NetworkHostDiscoveredEvent
    {
        public string Address;
        public int Port;
        public VariantMap Beacon;
    }

    public struct ClientConnectedEvent
    {
        public string Address;
        public int Port;
        public NetworkConnection Connection;
    }

    public struct ClientDisconnectedEvent
    {
        public string Address;
        public int Port;
        public NetworkConnection Connection;
    }

    public struct ServerClientConnectedEvent
    {
        public NetworkServer Server;
        public NetworkConnection Connection;
        public string Address;
        public int Port;
    }

    public struct ServerClientDisconnectedEvent
    {
        public NetworkServer Server;
        public NetworkConnection Connection;
        public string Address;
        public int Port;
    }

    public struct ServerListenStartEvent
    {
        public NetworkServer Server;
    }

    public struct ServerListenStopEvent
    {
        public NetworkServer Server;
    }

    public struct ClientDataEvent
    {
        public NetworkConnection Connection;
        public MemoryBuffer Data;
        public bool Handled;
    }

    public struct ClientMessageEvent
    {
        public NetworkConnection Connection;
        public NetworkMessageId MessageId;
        public MemoryBuffer Data;
        public bool Handled;
    }

    public struct ClientSendDataEvent
    {
        public NetworkConnection Connection;
        public PacketType Type;
        public MemoryBuffer Data;
        public bool Handled;
    }

    public struct ClientSendMessageEvent
    {
        public NetworkConnection Connection;
        public NetworkMessageId MessageId;
        public PacketType Type;
        public MemoryBuffer Data;
        public bool Handled;
    }
}
