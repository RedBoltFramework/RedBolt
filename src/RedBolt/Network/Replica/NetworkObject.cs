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

using System.Diagnostics;

using RedBolt.Core;
using RedBolt.Network;
using RedBolt.Scene;

public enum NetworkObjectMode
{
    Standalone,
    Server,
    ClientReplicated,
    ClientOwned
}

/// <summary>Base component of a network-replicated object.</summary>
[Reflected(ObjectCategory.Category_Network, Factory = false)]
public partial class NetworkObject : ReferencedComponent<NetworkObjectRegistry>, INetworkCallback
{
    public void SetOwner(ReplicatedPeer? owner)
    {
        if (_networkMode != NetworkObjectMode.Standalone)
        {
            Debug.Fail("NetworkObject.SetOwner may be called only for NetworkObject in Standalone mode");
            return;
        }

        _ownerConnection = owner is not null ? new WeakReference<ReplicatedPeer>(owner) : null;
    }

    public void UpdateObjectHierarchy()
    {
        var newParentNetworkObject = Node!.FindComponent<NetworkObject>(
            ComponentSearchFlag.ParentRecursive | ComponentSearchFlag.Derived);
        if (!ReferenceEquals(newParentNetworkObject, ParentNetworkObject))
        {
            ParentNetworkObject?.RemoveChildNetworkObject(this);
            _parentNetworkObject = newParentNetworkObject is not null
                ? new WeakReference<NetworkObject>(newParentNetworkObject)
                : null;
            ParentNetworkObject?.AddChildNetworkObject(this);
        }

        _childrenNetworkObjects.RemoveAll(child =>
            !child.TryGetTarget(out var value) || !value.IsTrackedInRegistry);

        if (IsServer)
            UpdateTransformOnServer();
    }

    public void SetNetworkId(NetworkId networkId) => Reference = networkId;

    public void SetNetworkMode(NetworkObjectMode mode) => _networkMode = mode;

    public virtual float? CalculateDistanceForFiltering(NetworkObject otherNetworkObject)
    {
        var thisPosition = Node!.GetWorldPosition();
        var otherPosition = otherNetworkObject.Node!.GetWorldPosition();
        return (thisPosition - otherPosition).Length;
    }

    public void SetParentNetworkObject(NetworkId parentNetworkId)
    {
        if (parentNetworkId != NetworkId.None)
        {
            var parentNetworkObject = GetOtherNetworkObject(parentNetworkId);
            if (parentNetworkObject is not null)
            {
                var parentNode = parentNetworkObject.Node;
                if (!ReferenceEquals(Node!.Parent, parentNode))
                    Node.SetParent(parentNode);
            }
            else
            {
                Logging.Log.GetLogger().Error("Cannot assign NetworkObject {} to unknown parent NetworkObject {}",
                    TrackedComponent.ToString(NetworkId), TrackedComponent.ToString(parentNetworkId));
            }
        }
        else
        {
            var parentNode = Scene;
            if (!ReferenceEquals(Node!.Parent, parentNode))
                Node.SetParent(parentNode);
        }
    }

    public NetworkId NetworkId => Reference;
    public ReplicationManager? ReplicationManager => Registry as ReplicationManager;
    public NetworkId ParentNetworkId => ParentNetworkObject?.NetworkId ?? NetworkId.None;
    public NetworkObject? ParentNetworkObject =>
        _parentNetworkObject?.TryGetTarget(out var value) == true ? value : null;
    public IReadOnlyList<NetworkObject> ChildrenNetworkObjects =>
        _childrenNetworkObjects.Select(child => child.TryGetTarget(out var value) ? value : null)
            .Where(value => value?.IsTrackedInRegistry == true).Cast<NetworkObject>().ToArray();
    public ReplicatedPeer? OwnerConnection =>
        _ownerConnection?.TryGetTarget(out var value) == true ? value : null;
    public uint OwnerConnectionId => OwnerConnection?.ObjectId ?? 0;

    public NetworkObjectMode NetworkMode => _networkMode;
    public bool IsStandalone => _networkMode == NetworkObjectMode.Standalone;
    public bool IsServer => _networkMode == NetworkObjectMode.Server;
    public bool IsOwnedByThisClient => _networkMode == NetworkObjectMode.ClientOwned;
    public bool IsReplicatedClient => _networkMode == NetworkObjectMode.ClientReplicated;

    public virtual void PrepareToRemove()
    {
        Node?.Remove();
    }

    protected override void OnNodeSet(Node? previousNode, Node? currentNode)
    {
        if (Node is not null)
        {
            Node.AddListener(this);
            Node.MarkDirty();
        }
        else
        {
            foreach (var childReference in _childrenNetworkObjects)
            {
                if (childReference.TryGetTarget(out var childNetworkObject))
                    childNetworkObject.Node?.MarkDirty();
            }
        }
    }

    protected override void OnMarkedDirty(Node node)
    {
        ReplicationManager?.QueueNetworkObjectUpdate(this);
    }

    protected NetworkObject? GetOtherNetworkObject(NetworkId networkId) =>
        ReplicationManager?.GetNetworkObject(networkId);

    public virtual void InitializeOnServer() { }
    public virtual NetworkObjectRelevance? GetRelevanceForClient(ReplicatedPeer connection) => null;
    public virtual void UpdateTransformOnServer() { }
    public virtual void WriteSnapshot(NetworkFrame frame, IO.Serializer destination) { }
    public virtual bool PrepareReliableDelta(NetworkFrame frame) => false;
    public virtual void WriteReliableDelta(NetworkFrame frame, IO.Serializer destination) { }
    public virtual bool PrepareUnreliableDelta(NetworkFrame frame) => false;
    public virtual void WriteUnreliableDelta(NetworkFrame frame, IO.Serializer destination) { }
    public virtual void ReadUnreliableFeedback(NetworkFrame feedbackFrame, IO.Deserializer source) { }
    public virtual void InitializeFromSnapshot(NetworkFrame frame, IO.Deserializer source, bool isOwned) { }
    public virtual void InterpolateState(float replicaTimeStep, float inputTimeStep,
        NetworkTime replicaTime, NetworkTime inputTime) { }
    public virtual void ReadReliableDelta(NetworkFrame frame, IO.Deserializer source) { }
    public virtual void ReadUnreliableDelta(NetworkFrame frame, IO.Deserializer source) { }
    public virtual bool PrepareUnreliableFeedback(NetworkFrame frame) => false;
    public virtual void WriteUnreliableFeedback(NetworkFrame frame, IO.Serializer destination) { }
    public virtual void InitializeStandalone() { }
    public virtual void Update(float replicaTimeStep, float inputTimeStep) { }
    public virtual void PostUpdate(float replicaTimeStep, float inputTimeStep) { }

    private void AddChildNetworkObject(NetworkObject networkObject)
    {
        _childrenNetworkObjects.Add(new WeakReference<NetworkObject>(networkObject));
    }

    private void RemoveChildNetworkObject(NetworkObject networkObject)
    {
        _childrenNetworkObjects.RemoveAll(child =>
            child.TryGetTarget(out var value) && ReferenceEquals(value, networkObject));
    }

    private NetworkObjectMode _networkMode;
    private WeakReference<ReplicatedPeer>? _ownerConnection;
    private WeakReference<NetworkObject>? _parentNetworkObject;
    private readonly List<WeakReference<NetworkObject>> _childrenNetworkObjects = [];
}
