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

using RedBolt.Core;
using RedBolt.Network;

/// <summary>Filters a NetworkObject by minimum distance to the client.</summary>
[Reflected(ObjectCategory.Category_Network)]
public partial class FilteredByDistance : NetworkBehavior
{
    public new const NetworkCallbackFlags CallbackMask = NetworkCallbackFlags.GetRelevanceForClient;
    public const float DefaultDistance = 100.0f;

    public FilteredByDistance()
        : base(CallbackMask)
    {
    }

    [Serialized("Is Relevant")]
    public bool IsRelevant { get; set; } = true;

    [Serialized("Update Period")]
    public uint UpdatePeriod { get; set; }

    [Serialized("Distance")]
    public float Distance { get; set; } = DefaultDistance;

    public override NetworkObjectRelevance? GetRelevanceForClient(ReplicatedPeer connection)
    {
        if (ReferenceEquals(NetworkObject!.OwnerConnection, connection))
            return null;

        var replicationManager = NetworkObject.ReplicationManager!;
        var serverReplicator = replicationManager.ServerReplicator!;
        var ownedObjects = serverReplicator.GetNetworkObjectsOwnedByConnection(connection);

        var distanceToConnectionObjects = float.MaxValue;
        foreach (var networkObject in ownedObjects)
        {
            if (networkObject.CalculateDistanceForFiltering(NetworkObject) is { } distance)
                distanceToConnectionObjects = System.Math.Min(distanceToConnectionObjects, distance);
        }

        if (distanceToConnectionObjects < Distance)
            return null;
        if (!IsRelevant)
            return NetworkObjectRelevance.Irrelevant;

        return (NetworkObjectRelevance)System.Math.Min(UpdatePeriod,
            (uint)NetworkObjectRelevance.MaxPeriod);
    }
}
