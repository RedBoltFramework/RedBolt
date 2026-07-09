using System.Buffers;

namespace RedBolt;

public sealed class Scene : Object
{
    private readonly Dictionary<uint, Component> _components = [];
    private readonly Dictionary<uint, Node> _nodes = [];
    private readonly Dictionary<string, List<Node>> _taggedNodes = new(StringComparer.Ordinal);
    private readonly List<ISceneUpdateListener> _updateListeners = [];
    private readonly List<StaticModel> _staticModels = [];
    private int _asyncLoadingMs = 5;
    private float _elapsedTime;
    private string _fileName = string.Empty;
    private uint _nextComponentId = 1;
    private uint _nextNodeId = 1;

    public Scene()
    {
        Root = new Node(this, parent: null, name: string.Empty, AllocateNodeId(0));
        NodeAdded(Root);
    }

    public Node Root { get; }

    internal IReadOnlyList<StaticModel> StaticModels => _staticModels;

    public bool UpdateEnabled { get; set; } = true;

    public float TimeScale { get; set; } = 1.0f;

    public float EffectiveTimeScale => UpdateEnabled ? TimeScale : 0.0f;

    public float ElapsedTime => _elapsedTime;

    public int AsyncLoadingMs => _asyncLoadingMs;

    public string FileName => _fileName;

    public uint Checksum { get; private set; }

    public Node CreateChild(string name = "", uint id = 0, bool temporary = false)
    {
        return Root.CreateChild(name, id, temporary);
    }

    public T CreateComponent<T>(uint id = 0)
        where T : Component, new()
    {
        return Root.CreateComponent<T>(id);
    }

    public void Clear()
    {
        Root.RemoveAllChildren();
        Root.RemoveAllComponents();
    }

    public void SetUpdateEnabled(bool enable)
    {
        if (UpdateEnabled == enable)
            return;

        UpdateEnabled = enable;

        var eventData = new SceneEvents.SceneUpdatesChangedEvent
        {
            Scene = this
        };
        SendEvent(ref eventData);
    }

    public bool IsUpdateEnabled() => UpdateEnabled;

    public void SetTimeScale(float scale)
    {
        TimeScale = Math.Max(scale, MathDefs.Epsilon);
    }

    public float GetTimeScale() => TimeScale;

    public float GetEffectiveTimeScale() => EffectiveTimeScale;

    public void SetElapsedTime(float time)
    {
        _elapsedTime = time;
    }

    public float GetElapsedTime() => _elapsedTime;

    public void SetAsyncLoadingMs(int ms)
    {
        _asyncLoadingMs = Math.Max(ms, 1);
    }

    public int GetAsyncLoadingMs() => _asyncLoadingMs;

    public bool IsAsyncLoading() => false;

    public float GetAsyncProgress() => 1.0f;

    public void SetFileName(string fileName)
    {
        this._fileName = fileName ?? string.Empty;
        Checksum = string.IsNullOrEmpty(this._fileName) ? 0u : StringHash.Calculate(this._fileName);
    }

    public string GetFileName() => _fileName;

    public uint GetChecksum() => Checksum;

    public bool IsEmpty(bool ignoreComponents = false)
    {
        return Root.GetNumChildren() == 0 && (ignoreComponents || Root.GetNumComponents() == 0);
    }

    public Node? GetNode(uint id)
    {
        return _nodes.GetValueOrDefault(id);
    }

    public Component? GetComponent(uint id)
    {
        return _components.GetValueOrDefault(id);
    }

    public IReadOnlyList<Node> GetNodesWithTag(string tag)
    {
        return _taggedNodes.TryGetValue(tag, out var tagged) ? tagged : Array.Empty<Node>();
    }

    public IReadOnlyDictionary<uint, Node> GetAllNodes()
    {
        return _nodes;
    }

    public IReadOnlyDictionary<uint, Component> GetAllComponents()
    {
        return _components;
    }

    public void GetComponents<T>(List<T> result)
        where T : Component
    {
        ArgumentNullException.ThrowIfNull(result);

        result.Clear();
        foreach (var component in _components.Values)
        {
            if (component is T typed)
                result.Add(typed);
        }
    }

    public void Update(float timeStep)
    {
        var effectiveTimeStep = timeStep * TimeScale;

        var forcedUpdateEvent = new SceneEvents.SceneForcedUpdateEvent
        {
            Scene = this,
            TimeStep = effectiveTimeStep
        };
        SendEvent(ref forcedUpdateEvent);

        if (UpdateEnabled)
        {
            var listenerSnapshot = SnapshotUpdateListeners();
            try
            {
                var sceneUpdateEvent = new SceneEvents.SceneUpdateEvent
                {
                    Scene = this,
                    TimeStep = effectiveTimeStep
                };
                SendEvent(ref sceneUpdateEvent);
                for (var i = 0; i < listenerSnapshot.Count; ++i)
                    listenerSnapshot.Array[i]!.OnSceneUpdate(effectiveTimeStep);

                var attributeAnimationUpdateEvent = new SceneEvents.AttributeAnimationUpdateEvent
                {
                    Scene = this,
                    TimeStep = effectiveTimeStep
                };
                SendEvent(ref attributeAnimationUpdateEvent);
                UpdateAttributeAnimations(effectiveTimeStep);

                var subsystemUpdateEvent = new SceneEvents.SceneSubsystemUpdateEvent
                {
                    Scene = this,
                    TimeStep = effectiveTimeStep
                };
                SendEvent(ref subsystemUpdateEvent);

                var postUpdateEvent = new SceneEvents.ScenePostUpdateEvent
                {
                    Scene = this,
                    TimeStep = effectiveTimeStep
                };
                SendEvent(ref postUpdateEvent);
                for (var i = 0; i < listenerSnapshot.Count; ++i)
                    listenerSnapshot.Array[i]!.OnScenePostUpdate(effectiveTimeStep);

                _elapsedTime += effectiveTimeStep;
            }
            finally
            {
                listenerSnapshot.Dispose();
            }
        }

        var forcedPostUpdateEvent = new SceneEvents.SceneForcedPostUpdateEvent
        {
            Scene = this,
            TimeStep = effectiveTimeStep
        };
        SendEvent(ref forcedPostUpdateEvent);
    }

    internal uint AllocateComponentId(uint requestedId)
    {
        return AllocateId(_components, ref _nextComponentId, requestedId);
    }

    internal uint AllocateNodeId(uint requestedId)
    {
        return AllocateId(_nodes, ref _nextNodeId, requestedId);
    }

    internal void NodeAdded(Node node)
    {
        if (_nodes.TryGetValue(node.Id, out var existingNode) && !ReferenceEquals(existingNode, node))
            existingNode.Parent?.RemoveChild(existingNode);

        if (!_nodes.TryAdd(node.Id, node))
            return;

        foreach (var tag in node.Tags)
            NodeTagAdded(node, tag);

        foreach (var component in node.Components)
            ComponentAdded(component);

        foreach (var child in node.Children)
            NodeAdded(child);
    }

    internal void NodeRemoved(Node node)
    {
        if (!_nodes.Remove(node.Id))
            return;

        foreach (var child in node.Children)
            NodeRemoved(child);

        foreach (var component in node.Components)
            ComponentRemoved(component);

        foreach (var tag in node.Tags)
            NodeTagRemoved(node, tag);
    }

    internal void NodeTagAdded(Node node, string tag)
    {
        if (!_taggedNodes.TryGetValue(tag, out var tagged))
            _taggedNodes[tag] = tagged = [];
        tagged.Add(node);
    }

    internal void NodeTagRemoved(Node node, string tag)
    {
        if (!_taggedNodes.TryGetValue(tag, out var tagged))
            return;

        tagged.Remove(node);
        if (tagged.Count == 0)
            _taggedNodes.Remove(tag);
    }

    internal void ComponentAdded(Component component)
    {
        if (_components.TryGetValue(component.Id, out var existingComponent) && !ReferenceEquals(existingComponent, component))
            existingComponent.Node.RemoveComponent(existingComponent);

        _components[component.Id] = component;
        component.OnSceneSet(null, this);

        if (component is ISceneUpdateListener listener)
            _updateListeners.Add(listener);
        if (component is StaticModel staticModel)
            _staticModels.Add(staticModel);
    }

    internal void ComponentRemoved(Component component)
    {
        if (!_components.Remove(component.Id))
            return;

        if (component is ISceneUpdateListener listener)
            _updateListeners.Remove(listener);
        if (component is StaticModel staticModel)
            _staticModels.Remove(staticModel);

        component.SetId(0);
        component.OnSceneSet(this, null);
    }

    private void UpdateAttributeAnimations(float timeStep)
    {
        var nodeSnapshot = SnapshotValues(_nodes);
        try
        {
            for (var i = 0; i < nodeSnapshot.Count; ++i)
                nodeSnapshot.Array[i]!.ApplyAttributeAnimations(timeStep);
        }
        finally
        {
            nodeSnapshot.Dispose();
        }

        var componentSnapshot = SnapshotValues(_components);
        try
        {
            for (var i = 0; i < componentSnapshot.Count; ++i)
                componentSnapshot.Array[i]!.ApplyAttributeAnimations(timeStep);
        }
        finally
        {
            componentSnapshot.Dispose();
        }
    }

    private static uint AllocateId<T>(Dictionary<uint, T> objects, ref uint nextId, uint requestedId)
        where T : class
    {
        if (requestedId != 0 && !objects.ContainsKey(requestedId))
        {
            if (requestedId >= nextId)
                nextId = requestedId + 1;
            return requestedId;
        }

        return NextFreeId(objects, ref nextId);
    }

    private static uint NextFreeId<T>(Dictionary<uint, T> objects, ref uint nextId)
        where T : class
    {
        while (nextId == 0 || objects.ContainsKey(nextId))
            ++nextId;

        return nextId++;
    }

    private Snapshot<ISceneUpdateListener> SnapshotUpdateListeners()
    {
        var array = ArrayPool<ISceneUpdateListener>.Shared.Rent(_updateListeners.Count);
        _updateListeners.CopyTo(array, 0);
        return new Snapshot<ISceneUpdateListener>(array, _updateListeners.Count);
    }

    private static Snapshot<T> SnapshotValues<T>(Dictionary<uint, T> objects)
        where T : class
    {
        var array = ArrayPool<T>.Shared.Rent(objects.Count);
        var index = 0;
        foreach (var value in objects.Values)
            array[index++] = value;
        return new Snapshot<T>(array, index);
    }

    private readonly ref struct Snapshot<T>
        where T : class
    {
        public Snapshot(T[] array, int count)
        {
            Array = array;
            Count = count;
        }

        public T[] Array { get; }
        public int Count { get; }

        public void Dispose()
        {
            System.Array.Clear(Array, 0, Count);
            ArrayPool<T>.Shared.Return(Array);
        }
    }
}
