//
// Copyright (c) 2017-2020 the rbfx project.
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

namespace RedBolt.Replica;

using RedBolt.IO;
using RedBolt.Network;

/// <summary>Mask used to enable and disable network callbacks.</summary>
[Flags]
public enum NetworkCallbackFlags
{
    None = 0,

    GetRelevanceForClient = 1 << 0,
    UpdateTransformOnServer = 1 << 1,

    PrepareToRemove = 1 << 2,
    InterpolateState = 1 << 3,

    ReliableDelta = 1 << 4,
    UnreliableDelta = 1 << 5,
    UnreliableFeedback = 1 << 6,

    Update = 1 << 7,
    PostUpdate = 1 << 8
}

/// <summary>Server-side callbacks for NetworkObject and NetworkBehavior.</summary>
public interface IServerNetworkCallback
{
    void InitializeOnServer() { }

    NetworkObjectRelevance? GetRelevanceForClient(ReplicatedPeer connection) => null;
    void UpdateTransformOnServer() { }

    void WriteSnapshot(NetworkFrame frame, Serializer destination) { }

    bool PrepareReliableDelta(NetworkFrame frame) => false;
    void WriteReliableDelta(NetworkFrame frame, Serializer destination) { }
    bool PrepareUnreliableDelta(NetworkFrame frame) => false;
    void WriteUnreliableDelta(NetworkFrame frame, Serializer destination) { }

    void ReadUnreliableFeedback(NetworkFrame feedbackFrame, Deserializer source) { }
}

/// <summary>Client-side callbacks for NetworkObject and NetworkBehavior.</summary>
public interface IClientNetworkCallback
{
    void InitializeFromSnapshot(NetworkFrame frame, Deserializer source, bool isOwned) { }
    void PrepareToRemove() { }

    void InterpolateState(float replicaTimeStep, float inputTimeStep,
        NetworkTime replicaTime, NetworkTime inputTime) { }

    void ReadReliableDelta(NetworkFrame frame, Deserializer source) { }
    void ReadUnreliableDelta(NetworkFrame frame, Deserializer source) { }

    bool PrepareUnreliableFeedback(NetworkFrame frame) => false;
    void WriteUnreliableFeedback(NetworkFrame frame, Serializer destination) { }
}

/// <summary>Aggregate network-related callbacks used by NetworkObject and NetworkBehavior.</summary>
public interface INetworkCallback : IServerNetworkCallback, IClientNetworkCallback
{
    void InitializeStandalone() { }
    void Update(float replicaTimeStep, float inputTimeStep) { }
    void PostUpdate(float replicaTimeStep, float inputTimeStep) { }
}
