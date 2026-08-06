// Copyright (c) 2025-2025 the rbfx project.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Replica;

using RedBolt.Core;
using RedBolt.IO;

/// <summary>Behavior that replicates the current parent network object.</summary>
[Reflected(ObjectCategory.Category_Network)]
public partial class ReplicatedParent : NetworkBehavior
{
    public new const NetworkCallbackFlags CallbackMask = NetworkCallbackFlags.ReliableDelta;

    public ReplicatedParent()
        : base(CallbackMask)
    {
    }

    public override void InitializeOnServer()
    {
        _latestSentParentObject = NetworkObject!.ParentNetworkId;
    }

    public override bool PrepareReliableDelta(NetworkFrame frame)
    {
        var parentObject = NetworkObject!.ParentNetworkId;
        var needUpdate = _latestSentParentObject != parentObject;
        _latestSentParentObject = parentObject;
        return needUpdate;
    }

    public override void WriteReliableDelta(NetworkFrame frame, Serializer destination)
    {
        destination.WriteUInt((uint)_latestSentParentObject);
    }

    public override void ReadReliableDelta(NetworkFrame frame, Deserializer source)
    {
        NetworkObject!.SetParentNetworkObject((NetworkId)source.ReadUInt());
    }

    private NetworkId _latestSentParentObject = NetworkId.None;
}
