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

using RedBolt.Container;
using RedBolt.Core;
using RedBolt.IO;
using RedBolt.Network;
using RedBolt.Scene;

/// <summary>Aspect of network behavior injected into BehaviorNetworkObject.</summary>
[Reflected(ObjectCategory.Category_Network, Factory = false)]
public partial class NetworkBehavior : Component, INetworkCallback
{
    protected NetworkBehavior(NetworkCallbackFlags callbackMask)
    {
        _callbackMask = callbackMask;
    }

    public void SetNetworkObject(BehaviorNetworkObject? owner)
    {
        _owner = owner is not null ? new WeakReference<BehaviorNetworkObject>(owner) : null;
    }

    public NetworkObject? FindClosestNetworkObject()
    {
        return Node?.FindComponent<NetworkObject>(
            ComponentSearchFlag.SelfOrParentRecursive | ComponentSearchFlag.Derived);
    }

    public BehaviorNetworkObject? NetworkObject =>
        _owner?.TryGetTarget(out var value) == true ? value : null;
    public NetworkCallbackFlags CallbackMask => _callbackMask;

    protected override void OnNodeSet(Node? previousNode, Node? currentNode)
    {
        if (Node is null && NetworkObject is { } owner)
        {
            owner.InvalidateBehaviors();
            _owner = null;
        }
    }

    protected override void OnSceneSet(Scene? previousScene, Scene? scene)
    {
        base.OnSceneSet(previousScene, scene);
        if (scene is null)
        {
            UnsubscribeFromAllEvents();
            if (NetworkObject is { } owner)
                owner.InvalidateBehaviors();
            _owner = null;
        }
    }

    public virtual void InitializeOnServer() { }
    public virtual NetworkObjectRelevance? GetRelevanceForClient(ReplicatedPeer connection) => null;
    public virtual void UpdateTransformOnServer() { }
    public virtual void WriteSnapshot(NetworkFrame frame, Serializer destination) { }
    public virtual bool PrepareReliableDelta(NetworkFrame frame) => false;
    public virtual void WriteReliableDelta(NetworkFrame frame, Serializer destination) { }
    public virtual bool PrepareUnreliableDelta(NetworkFrame frame) => false;
    public virtual void WriteUnreliableDelta(NetworkFrame frame, Serializer destination) { }
    public virtual void ReadUnreliableFeedback(NetworkFrame feedbackFrame, Deserializer source) { }
    public virtual void InitializeFromSnapshot(NetworkFrame frame, Deserializer source, bool isOwned) { }
    public virtual void PrepareToRemove() { }
    public virtual void InterpolateState(float replicaTimeStep, float inputTimeStep,
        NetworkTime replicaTime, NetworkTime inputTime) { }
    public virtual void ReadReliableDelta(NetworkFrame frame, Deserializer source) { }
    public virtual void ReadUnreliableDelta(NetworkFrame frame, Deserializer source) { }
    public virtual bool PrepareUnreliableFeedback(NetworkFrame frame) => false;
    public virtual void WriteUnreliableFeedback(NetworkFrame frame, Serializer destination) { }
    public virtual void InitializeStandalone() { }
    public virtual void Update(float replicaTimeStep, float inputTimeStep) { }
    public virtual void PostUpdate(float replicaTimeStep, float inputTimeStep) { }

    private WeakReference<BehaviorNetworkObject>? _owner;
    private readonly NetworkCallbackFlags _callbackMask;
}

/// <summary>NetworkObject composed from a fixed amount of independent behaviors.</summary>
[Reflected(ObjectCategory.Category_Network)]
public partial class BehaviorNetworkObject : StaticNetworkObject
{
    public const uint MaxNumBehaviors = 29;

    public void InvalidateBehaviors()
    {
        _callbackMask = NetworkCallbackFlags.None;
        _behaviors.Clear();
    }

    public NetworkBehavior? GetNetworkBehavior(StringHash componentType, uint index = 0)
    {
        foreach (var connectedBehavior in _behaviors)
        {
            Debug.Assert(connectedBehavior.Component is not null);
            if (connectedBehavior.Component.GetTypeNameHash() == componentType)
            {
                if (index == 0)
                    return connectedBehavior.Component;
                --index;
            }
        }
        return null;
    }

    public T? GetNetworkBehavior<T>(uint index = 0)
        where T : NetworkBehavior => GetNetworkBehavior(new StringHash(typeof(T).GetClassName()), index) as T;

    public override void InitializeStandalone()
    {
        base.InitializeStandalone();
        InitializeBehaviors();
        foreach (var behavior in _behaviors)
            behavior.Component.InitializeStandalone();
    }

    public override void InitializeOnServer()
    {
        base.InitializeOnServer();
        InitializeBehaviors();
        foreach (var behavior in _behaviors)
            behavior.Component.InitializeOnServer();
    }

    public override void WriteSnapshot(NetworkFrame frame, Serializer destination)
    {
        base.WriteSnapshot(frame, destination);
        destination.WriteVLE(checked((uint)_behaviors.Count));
        foreach (var behavior in _behaviors)
            destination.WriteStringHash(behavior.Component.GetTypeNameHash());
        foreach (var behavior in _behaviors)
            behavior.Component.WriteSnapshot(frame, destination);
    }

    public override void InitializeFromSnapshot(NetworkFrame frame, Deserializer source, bool isOwned)
    {
        base.InitializeFromSnapshot(frame, source, isOwned);
        InitializeBehaviors();

        var expectedNumBehaviors = source.ReadVLE();
        if (expectedNumBehaviors != _behaviors.Count)
        {
            Logging.Log.GetLogger().Error(
                "Number of behaviors in NetworkObject {} doesn't match on the client ({}) and the server ({})",
                TrackedComponent.ToString(NetworkId), _behaviors.Count, expectedNumBehaviors);
            InvalidateBehaviors();
            return;
        }

        for (var i = 0; i < expectedNumBehaviors; ++i)
        {
            var expectedType = source.ReadStringHash();
            var behavior = _behaviors[i].Component;
            if (expectedType != behavior.GetTypeNameHash())
            {
                Logging.Log.GetLogger().Error(
                    "Type of behavior #{} in NetworkObject {} doesn't match on the client ({}) and the server ({})",
                    i, TrackedComponent.ToString(NetworkId), behavior.GetTypeNameHash().ToDebugString(),
                    expectedType.ToDebugString());
                InvalidateBehaviors();
                return;
            }
        }

        foreach (var behavior in _behaviors)
            behavior.Component.InitializeFromSnapshot(frame, source, isOwned);
    }

    public override NetworkObjectRelevance? GetRelevanceForClient(ReplicatedPeer connection)
    {
        if (_callbackMask.HasFlag(NetworkCallbackFlags.GetRelevanceForClient))
        {
            foreach (var behavior in _behaviors)
            {
                if (!behavior.CallbackMask.HasFlag(NetworkCallbackFlags.GetRelevanceForClient))
                    continue;
                if (behavior.Component.GetRelevanceForClient(connection) is { } relevance)
                    return relevance;
            }
        }
        return null;
    }

    public override void UpdateTransformOnServer()
    {
        base.UpdateTransformOnServer();
        Invoke(NetworkCallbackFlags.UpdateTransformOnServer,
            behavior => behavior.UpdateTransformOnServer());
    }

    public override void InterpolateState(float replicaTimeStep, float inputTimeStep,
        NetworkTime replicaTime, NetworkTime inputTime)
    {
        base.InterpolateState(replicaTimeStep, inputTimeStep, replicaTime, inputTime);
        Invoke(NetworkCallbackFlags.InterpolateState,
            behavior => behavior.InterpolateState(replicaTimeStep, inputTimeStep, replicaTime, inputTime));
    }

    public override bool PrepareReliableDelta(NetworkFrame frame)
    {
        var needUpdate = base.PrepareReliableDelta(frame);
        _reliableUpdateMask = PrepareDelta(NetworkCallbackFlags.ReliableDelta,
            behavior => behavior.PrepareReliableDelta(frame));
        return needUpdate || _reliableUpdateMask != 0;
    }

    public override void WriteReliableDelta(NetworkFrame frame, Serializer destination)
    {
        base.WriteReliableDelta(frame, destination);
        WriteDelta(NetworkCallbackFlags.ReliableDelta, _reliableUpdateMask, destination,
            behavior => behavior.WriteReliableDelta(frame, destination));
    }

    public override void ReadReliableDelta(NetworkFrame frame, Deserializer source)
    {
        base.ReadReliableDelta(frame, source);
        ReadDelta(NetworkCallbackFlags.ReliableDelta, source,
            behavior => behavior.ReadReliableDelta(frame, source));
    }

    public override bool PrepareUnreliableDelta(NetworkFrame frame)
    {
        var needUpdate = base.PrepareUnreliableDelta(frame);
        _unreliableUpdateMask = PrepareDelta(NetworkCallbackFlags.UnreliableDelta,
            behavior => behavior.PrepareUnreliableDelta(frame));
        return needUpdate || _unreliableUpdateMask != 0;
    }

    public override void WriteUnreliableDelta(NetworkFrame frame, Serializer destination)
    {
        base.WriteUnreliableDelta(frame, destination);
        WriteDelta(NetworkCallbackFlags.UnreliableDelta, _unreliableUpdateMask, destination,
            behavior => behavior.WriteUnreliableDelta(frame, destination));
    }

    public override void ReadUnreliableDelta(NetworkFrame frame, Deserializer source)
    {
        base.ReadUnreliableDelta(frame, source);
        ReadDelta(NetworkCallbackFlags.UnreliableDelta, source,
            behavior => behavior.ReadUnreliableDelta(frame, source));
    }

    public override bool PrepareUnreliableFeedback(NetworkFrame frame)
    {
        var needUpdate = base.PrepareUnreliableFeedback(frame);
        _unreliableFeedbackMask = PrepareDelta(NetworkCallbackFlags.UnreliableFeedback,
            behavior => behavior.PrepareUnreliableFeedback(frame));
        return needUpdate || _unreliableFeedbackMask != 0;
    }

    public override void WriteUnreliableFeedback(NetworkFrame frame, Serializer destination)
    {
        base.WriteUnreliableFeedback(frame, destination);
        WriteDelta(NetworkCallbackFlags.UnreliableFeedback, _unreliableFeedbackMask, destination,
            behavior => behavior.WriteUnreliableFeedback(frame, destination));
    }

    public override void ReadUnreliableFeedback(NetworkFrame feedbackFrame, Deserializer source)
    {
        base.ReadUnreliableFeedback(feedbackFrame, source);
        ReadDelta(NetworkCallbackFlags.UnreliableFeedback, source,
            behavior => behavior.ReadUnreliableFeedback(feedbackFrame, source));
    }

    public override void Update(float replicaTimeStep, float inputTimeStep)
    {
        base.Update(replicaTimeStep, inputTimeStep);
        Invoke(NetworkCallbackFlags.Update,
            behavior => behavior.Update(replicaTimeStep, inputTimeStep));
    }

    public override void PostUpdate(float replicaTimeStep, float inputTimeStep)
    {
        base.PostUpdate(replicaTimeStep, inputTimeStep);
        Invoke(NetworkCallbackFlags.PostUpdate,
            behavior => behavior.PostUpdate(replicaTimeStep, inputTimeStep));
    }

    public override void PrepareToRemove()
    {
        Invoke(NetworkCallbackFlags.PrepareToRemove, behavior => behavior.PrepareToRemove());
        base.PrepareToRemove();
    }

    protected virtual void CreateBehaviors()
    {
    }

    protected override void OnSceneSet(Scene? previousScene, Scene? scene)
    {
        base.OnSceneSet(previousScene, scene);
        if (scene is null)
        {
            UnsubscribeFromEvent<SceneEvents.SceneNetworkUpdateEvent>();
            UnsubscribeFromEvent<SceneEvents.SceneNetworkPostUpdateEvent>();
            InvalidateBehaviors();
        }
    }

    private void InitializeBehaviors()
    {
        InvalidateBehaviors();
        CreateBehaviors();

        var networkBehaviors = Node!.FindComponents<NetworkBehavior>(
                ComponentSearchFlag.SelfOrChildrenRecursive | ComponentSearchFlag.Derived)
            .Where(behavior => ReferenceEquals(behavior.FindClosestNetworkObject(), this)).ToArray();

        if (networkBehaviors.Length > MaxNumBehaviors)
        {
            Logging.Log.GetLogger().Error(
                "Cannot connect more than {} NetworkBehavior-s to existing NetworkObject {}",
                MaxNumBehaviors, TrackedComponent.ToString(NetworkId));
            return;
        }

        foreach (var networkBehavior in networkBehaviors)
        {
            var bit = 1u << _behaviors.Count;
            var callbackMask = networkBehavior.CallbackMask;
            networkBehavior.SetNetworkObject(this);
            _behaviors.Add(new ConnectedNetworkBehavior(bit, networkBehavior, callbackMask));
            _callbackMask |= callbackMask;
        }

        UnsubscribeFromEvent<SceneEvents.SceneNetworkUpdateEvent>();
        if (_callbackMask.HasFlag(NetworkCallbackFlags.Update))
            SubscribeToEvent<SceneEvents.SceneNetworkUpdateEvent>(Scene!, HandleSceneNetworkUpdate);

        UnsubscribeFromEvent<SceneEvents.SceneNetworkPostUpdateEvent>();
        if (_callbackMask.HasFlag(NetworkCallbackFlags.PostUpdate))
            SubscribeToEvent<SceneEvents.SceneNetworkPostUpdateEvent>(Scene!, HandleSceneNetworkPostUpdate);
    }

    private void HandleSceneNetworkUpdate(ref SceneEvents.SceneNetworkUpdateEvent eventData) =>
        Update(eventData.TimeStepReplica, eventData.TimeStepInput);

    private void HandleSceneNetworkPostUpdate(ref SceneEvents.SceneNetworkPostUpdateEvent eventData) =>
        PostUpdate(eventData.TimeStepReplica, eventData.TimeStepInput);

    private uint PrepareDelta(NetworkCallbackFlags flag, Func<NetworkBehavior, bool> prepare)
    {
        var mask = 0u;
        if (!_callbackMask.HasFlag(flag))
            return mask;
        foreach (var behavior in _behaviors)
        {
            if (behavior.CallbackMask.HasFlag(flag) && prepare(behavior.Component))
                mask |= behavior.Bit;
        }
        return mask;
    }

    private void WriteDelta(NetworkCallbackFlags flag, uint mask, Serializer destination,
        Action<NetworkBehavior> write)
    {
        if (!_callbackMask.HasFlag(flag))
            return;
        destination.WriteVLE(mask);
        foreach (var behavior in _behaviors)
        {
            if ((mask & behavior.Bit) != 0)
                write(behavior.Component);
        }
    }

    private void ReadDelta(NetworkCallbackFlags flag, Deserializer source,
        Action<NetworkBehavior> read)
    {
        if (!_callbackMask.HasFlag(flag))
            return;
        var mask = source.ReadVLE();
        foreach (var behavior in _behaviors)
        {
            if ((mask & behavior.Bit) != 0)
                read(behavior.Component);
        }
    }

    private void Invoke(NetworkCallbackFlags flag, Action<NetworkBehavior> callback)
    {
        if (!_callbackMask.HasFlag(flag))
            return;
        foreach (var behavior in _behaviors)
        {
            if (behavior.CallbackMask.HasFlag(flag))
                callback(behavior.Component);
        }
    }

    private readonly record struct ConnectedNetworkBehavior(
        uint Bit, NetworkBehavior Component, NetworkCallbackFlags CallbackMask);

    private readonly List<ConnectedNetworkBehavior> _behaviors = [];
    private NetworkCallbackFlags _callbackMask;
    private uint _reliableUpdateMask;
    private uint _unreliableUpdateMask;
    private uint _unreliableFeedbackMask;
}
