//
// Copyright (c) 2008-2022 the Urho3D project.
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

namespace RedBolt.Network;

/// <summary>Known message IDs used by networking system. User-defined message IDs should start from User. Expect values of these message IDs to change between versions.</summary>
public enum NetworkMessageId : uint
{
    /// <summary>Message used to synchronize clock between client and server.</summary>
    ClockSync,

    /// <summary>Server-&gt;Client. ReplicationManager message. Deliver networking settings.</summary>
    Configure,
    /// <summary>Server-&gt;Client. ReplicationManager message. Send server time and dynamic properties of the client connection.</summary>
    SceneClock,
    /// <summary>Client-&gt;Server. ReplicationManager message. Notify server that the client is ready for replication.</summary>
    Synchronized,
    /// <summary>Server-&gt;Client. ReplicationManager message. Remove replicated NetworkObjects.</summary>
    RemoveObjects,
    /// <summary>Server-&gt;Client. ReplicationManager message. Create replicated NetworkObjects from snapshots.</summary>
    AddObjects,
    AddObjectsIncomplete,
    /// <summary>Server-&gt;Client. ReplicationManager message. Perform ordered and reliable update of NetworkObjects.</summary>
    UpdateObjectsReliable,
    UpdateObjectsReliableIncomplete,
    /// <summary>Server-&gt;Client. ReplicationManager message. Perform unordered and unreliable update of NetworkObjects.</summary>
    UpdateObjectsUnreliable,
    /// <summary>Client-&gt;Server. ReplicationManager message. Perform unordered and unreliable update of owned NetworkObjects from client to server.</summary>
    ObjectsFeedbackUnreliable,

    /// <summary>Server-&gt;Client. Request client to load scene file and report result.</summary>
    LoadScene,
    /// <summary>Client-&gt;Server. Report scene loading result.</summary>
    SceneLoadResult,

    /// <summary>Message IDs starting from User are reserved for the end user.</summary>
    User,

    /// <summary>Max message ID value.</summary>
    Max = uint.MaxValue,
}

public static class NetworkProtocol
{
    /// <summary>Conservative limit for the size of the packet transmitted over underlying transport. Real limit may be higher.</summary>
    /// <seealso cref="ReplicatedPeer.MaxPacketSize" />
    public const int MaxNetworkPacketSize = 1024;
    /// <summary>Maximum possible size of network message payload. Transport limitations are not considered. Real limit may be lower.</summary>
    /// <seealso cref="ReplicatedPeer.MaxMessageSize" />
    public const int MaxNetworkMessageSize = 0xFFFF;

    /// <summary>Max size of the message header (message id only).</summary>
    public const int NetworkMessageHeaderSize = 4;
}
