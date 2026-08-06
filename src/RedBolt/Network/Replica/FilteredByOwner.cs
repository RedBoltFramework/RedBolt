// Copyright (c) 2025-2025 the rbfx project.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Replica;

using RedBolt.Core;
using RedBolt.Network;

/// <summary>Filters a NetworkObject so it is relevant only to its owner.</summary>
[Reflected(ObjectCategory.Category_Network)]
public partial class FilteredByOwner : NetworkBehavior
{
    public new const NetworkCallbackFlags CallbackMask = NetworkCallbackFlags.GetRelevanceForClient;

    public FilteredByOwner()
        : base(CallbackMask)
    {
    }

    public override NetworkObjectRelevance? GetRelevanceForClient(ReplicatedPeer connection)
    {
        var isOwner = ReferenceEquals(NetworkObject!.OwnerConnection, connection);
        return !isOwner ? NetworkObjectRelevance.Irrelevant : null;
    }
}
