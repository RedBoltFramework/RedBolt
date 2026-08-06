// Copyright (c) 2017-2025 the rbfx project.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Replica;

using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.IO;
using RedBolt.Network;
using RedBolt.Resource;

/// <summary>Behavior that replicates animation over network.</summary>
[Reflected(ObjectCategory.Category_Network)]
public partial class ReplicatedAnimation : NetworkBehavior
{
    public const uint SmallSnapshotSize = 256;
    public const uint DefaultNumUploadAttempts = 4;
    public const float DefaultSmoothingTime = 0.2f;

    public new const NetworkCallbackFlags CallbackMask = NetworkCallbackFlags.ReliableDelta
        | NetworkCallbackFlags.UnreliableDelta | NetworkCallbackFlags.InterpolateState
        | NetworkCallbackFlags.PostUpdate;

    public ReplicatedAnimation()
        : base(CallbackMask)
    {
    }

    [Serialized("Num Upload Attempts")]
    public uint NumUploadAttempts { get; set; } = DefaultNumUploadAttempts;

    [Serialized("Replicate Owner")]
    public bool ReplicateOwner { get; set; }

    [Serialized("Smoothing Time")]
    public float SmoothingTime { get; set; } = DefaultSmoothingTime;

    public IReadOnlyList<int> Layers => _layers;

    public IReadOnlyDictionary<StringHash, string> AnimationLookup => _animationLookup;

    public void SetLayers(IEnumerable<int> layers)
    {
        _layers.Clear();
        _layers.AddRange(layers);
    }

    public void SetLayersAttr(VariantVector layers)
    {
        _layers.Clear();
        foreach (var value in layers)
            _layers.Add(checked((int)value.GetUInt()));
    }

    [Serialized("Layers", Setter = nameof(SetLayersAttr), DefaultSource = nameof(GetLayersDefault))]
    public VariantVector GetLayersAttr()
    {
        var result = new VariantVector();
        foreach (var layer in _layers)
            result.Add(new Variant(checked((uint)layer)));
        return result;
    }

    public override void InitializeStandalone()
    {
        InitializeCommon();
    }

    public override void InitializeOnServer()
    {
        InitializeCommon();
        if (_animationController is null)
            return;

        _latestRevision = _animationController.GetRevision();
        UpdateLookupsOnServer();
        _newAnimationLookups.Clear();
        SubscribeToEvent<NetworkEvents.EndServerNetworkFrameEvent>(HandleServerFrameEnd);
    }

    public override void WriteSnapshot(NetworkFrame frame, Serializer destination)
    {
        if (_animationController is null)
            return;

        destination.WriteVLE(checked((uint)_animationLookup.Count));
        foreach (var name in _animationLookup.Values)
            destination.WriteString(name);
        WriteAnimationSnapshot(destination);
    }

    public override void InitializeFromSnapshot(NetworkFrame frame, Deserializer source, bool isOwned)
    {
        InitializeCommon();
        if (_animationController is null)
            return;

        var replicationManager = NetworkObject!.ReplicationManager!;
        _networkStepTime = 1.0f / replicationManager.UpdateFrequency;
        _animationTrace.Resize(replicationManager.TraceDurationInFrames);
        _latestAppliedFrame = null;

        ReadLookupsOnClient(source);
        _animationTrace.Set(frame, ReadAnimationSnapshot(source));
    }

    public override bool PrepareReliableDelta(NetworkFrame frame) =>
        _newAnimationLookups.Count != 0;

    public override void WriteReliableDelta(NetworkFrame frame, Serializer destination)
    {
        destination.WriteVLE(checked((uint)_newAnimationLookups.Count));
        foreach (var name in _newAnimationLookups)
            destination.WriteString(name);
    }

    public override void ReadReliableDelta(NetworkFrame frame, Deserializer source)
    {
        ReadLookupsOnClient(source);
        _latestAppliedFrame = null;
    }

    public override bool PrepareUnreliableDelta(NetworkFrame frame) =>
        _animationController is not null && (_pendingUploadAttempts > 0 || NumUploadAttempts == 0);

    public override void WriteUnreliableDelta(NetworkFrame frame, Serializer destination)
    {
        WriteAnimationSnapshot(destination);
    }

    public override void ReadUnreliableDelta(NetworkFrame frame, Deserializer source)
    {
        _animationTrace.Set(frame, ReadAnimationSnapshot(source));
    }

    public override void InterpolateState(float replicaTimeStep, float inputTimeStep,
        NetworkTime replicaTime, NetworkTime inputTime)
    {
        if (_animationController is null || !IsAnimationReplicated())
            return;

        var adjustedReplicaTime = replicaTime - replicaTimeStep / _networkStepTime;
        var closestPriorFrame = _animationTrace.FindClosestAllocatedFrame(
            adjustedReplicaTime.Frame, true, false);
        if (!closestPriorFrame.HasValue || closestPriorFrame == _latestAppliedFrame)
            return;

        var firstUpdate = !_latestAppliedFrame.HasValue;
        _latestAppliedFrame = closestPriorFrame;
        DecodeSnapshot(_animationTrace.GetRawUnchecked(closestPriorFrame.Value), _snapshotAnimations);

        var delay = (float)((adjustedReplicaTime - new NetworkTime(closestPriorFrame.Value)) * _networkStepTime);
        _animationController.ReplaceAnimations(_snapshotAnimations.ToArray(), delay,
            firstUpdate ? 0.0f : SmoothingTime, _layers.ToArray());
    }

    public override void PostUpdate(float replicaTimeStep, float inputTimeStep)
    {
        if (_animationController is null)
            return;

        var timeStep = IsAnimationReplicated() ? replicaTimeStep : inputTimeStep;
        _animationController.Update(timeStep);

        if (NetworkObject!.IsServer)
            _animatedModel?.ApplyAnimation();
    }

    private void InitializeCommon()
    {
        _animationController = Node!.GetDerivedComponent<AnimationController>();
        if (_animationController is null)
            return;

        _animationController.SetEnabled(false);
        _animatedModel = Node.GetDerivedComponent<AnimatedModel>();
    }

    private void HandleServerFrameEnd(ref NetworkEvents.EndServerNetworkFrameEvent eventData) =>
        OnServerFrameEnd(eventData.Frame);

    private void OnServerFrameEnd(NetworkFrame frame)
    {
        if (_animationController is null)
            return;

        if (_pendingUploadAttempts > 0)
            --_pendingUploadAttempts;

        var revision = _animationController.GetRevision();
        if (_latestRevision != revision)
        {
            _latestRevision = revision;
            _pendingUploadAttempts = NumUploadAttempts;
            UpdateLookupsOnServer();
        }
        else
            _newAnimationLookups.Clear();
    }

    private void UpdateLookupsOnServer()
    {
        _newAnimationLookups.Clear();
        for (var i = 0; i < _animationController!.GetNumAnimations(); ++i)
        {
            var parameters = _animationController.GetAnimationParameters(i);
            if (_animationLookup.ContainsKey(parameters.GetAnimationName()))
                continue;

            var name = parameters.GetAnimation()!.Name;
            _animationLookup[new StringHash(name)] = name;
            _newAnimationLookups.Add(name);
        }
    }

    private void ReadLookupsOnClient(Deserializer source)
    {
        var numLookups = source.ReadVLE();
        for (var i = 0u; i < numLookups; ++i)
        {
            var name = source.ReadString();
            _animationLookup[new StringHash(name)] = name;
        }
    }

    private bool IsAnimationReplicated() =>
        NetworkObject!.IsReplicatedClient || (ReplicateOwner && NetworkObject.IsOwnedByThisClient);

    private Animation? GetAnimationByHash(StringHash nameHash)
    {
        return _animationLookup.TryGetValue(nameHash, out var name)
            ? GetSubsystem<ResourceCache>()!.GetResource<Animation>(name)
            : null;
    }

    private void WriteAnimationSnapshot(Serializer destination)
    {
        _snapshotBuffer.Clear();
        for (var i = 0; i < _animationController!.GetNumAnimations(); ++i)
        {
            var parameters = _animationController.GetAnimationParameters(i);
            if (_layers.Count != 0 && !_layers.Contains(parameters.LayerIndex))
                continue;

            _snapshotBuffer.WriteStringHash(parameters.GetAnimationName());
            parameters.Serialize(_snapshotBuffer);
        }
        destination.WriteBuffer(_snapshotBuffer.Buffer);
    }

    private static AnimationSnapshot ReadAnimationSnapshot(Deserializer source) =>
        new(source.ReadBuffer());

    private void DecodeSnapshot(AnimationSnapshot snapshot, List<AnimationParameters> result)
    {
        result.Clear();
        using var source = new MemoryBuffer(snapshot.Data);
        while (!source.IsEof())
        {
            var animation = GetAnimationByHash(source.ReadStringHash());
            var parameters = AnimationParameters.Deserialize(animation, source);
            if (animation is not null)
                result.Add(parameters);
        }
    }

    private static object GetLayersDefault() => new VariantVector();

    private readonly record struct AnimationSnapshot(byte[] Data);

    private AnimationController? _animationController;
    private AnimatedModel? _animatedModel;
    private readonly List<int> _layers = [];
    private readonly Dictionary<StringHash, string> _animationLookup = [];
    private uint _pendingUploadAttempts;
    private uint _latestRevision;
    private readonly List<string> _newAnimationLookups = [];
    private readonly VectorBuffer _snapshotBuffer = new();
    private float _networkStepTime;
    private NetworkFrame? _latestAppliedFrame;
    private readonly List<AnimationParameters> _snapshotAnimations = [];
    private readonly NetworkValue<AnimationSnapshot> _animationTrace = new();
}
