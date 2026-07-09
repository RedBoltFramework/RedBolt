using System.Buffers;

namespace RedBolt;

public sealed class Scene : Object
{
    private readonly Dictionary<uint, Component> components = [];
    private readonly Dictionary<uint, Node> nodes = [];
    private readonly Dictionary<string, List<Node>> taggedNodes = new(StringComparer.Ordinal);
    private readonly List<ISceneUpdateListener> updateListeners = [];
    private readonly List<StaticModel> staticModels = [];
    private int asyncLoadingMs = 5;
    private float elapsedTime;
    private string fileName = string.Empty;
    private uint nextComponentId = 1;
    private uint nextNodeId = 1;

    public Scene()
    {
        Root = new Node(this, parent: null, name: string.Empty, AllocateNodeId(0));
        NodeAdded(Root);
    }

    public Node Root { get; }

    internal IReadOnlyList<StaticModel> StaticModels => staticModels;

    public bool UpdateEnabled { get; set; } = true;

    public float TimeScale { get; set; } = 1.0f;

    public float EffectiveTimeScale => UpdateEnabled ? TimeScale : 0.0f;

    public float ElapsedTime => elapsedTime;

    public int AsyncLoadingMs => asyncLoadingMs;

    public string FileName => fileName;

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
        elapsedTime = time;
    }

    public float GetElapsedTime() => elapsedTime;

    public void SetAsyncLoadingMs(int ms)
    {
        asyncLoadingMs = Math.Max(ms, 1);
    }

    public int GetAsyncLoadingMs() => asyncLoadingMs;

    public bool IsAsyncLoading() => false;

    public float GetAsyncProgress() => 1.0f;

    public void SetFileName(string fileName)
    {
        this.fileName = fileName ?? string.Empty;
        Checksum = string.IsNullOrEmpty(this.fileName) ? 0u : StringHash.Calculate(this.fileName);
    }

    public string GetFileName() => fileName;

    public uint GetChecksum() => Checksum;

    public bool IsEmpty(bool ignoreComponents = false)
    {
        return Root.GetNumChildren() == 0 && (ignoreComponents || Root.GetNumComponents() == 0);
    }

    public Node? GetNode(uint id)
    {
        return nodes.GetValueOrDefault(id);
    }

    public Component? GetComponent(uint id)
    {
        return components.GetValueOrDefault(id);
    }

    public IReadOnlyList<Node> GetNodesWithTag(string tag)
    {
        return taggedNodes.TryGetValue(tag, out var tagged) ? tagged : Array.Empty<Node>();
    }

    public IReadOnlyDictionary<uint, Node> GetAllNodes()
    {
        return nodes;
    }

    public IReadOnlyDictionary<uint, Component> GetAllComponents()
    {
        return components;
    }

    public void GetComponents<T>(List<T> result)
        where T : Component
    {
        ArgumentNullException.ThrowIfNull(result);

        result.Clear();
        foreach (var component in components.Values)
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

                elapsedTime += effectiveTimeStep;
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
        return AllocateId(components, ref nextComponentId, requestedId);
    }

    internal uint AllocateNodeId(uint requestedId)
    {
        return AllocateId(nodes, ref nextNodeId, requestedId);
    }

    internal void NodeAdded(Node node)
    {
        if (nodes.TryGetValue(node.Id, out var existingNode) && !ReferenceEquals(existingNode, node))
            existingNode.Parent?.RemoveChild(existingNode);

        if (!nodes.TryAdd(node.Id, node))
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
        if (!nodes.Remove(node.Id))
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
        if (!taggedNodes.TryGetValue(tag, out var tagged))
            taggedNodes[tag] = tagged = [];
        tagged.Add(node);
    }

    internal void NodeTagRemoved(Node node, string tag)
    {
        if (!taggedNodes.TryGetValue(tag, out var tagged))
            return;

        tagged.Remove(node);
        if (tagged.Count == 0)
            taggedNodes.Remove(tag);
    }

    internal void ComponentAdded(Component component)
    {
        if (components.TryGetValue(component.Id, out var existingComponent) && !ReferenceEquals(existingComponent, component))
            existingComponent.Node.RemoveComponent(existingComponent);

        components[component.Id] = component;
        component.OnSceneSet(null, this);

        if (component is ISceneUpdateListener listener)
            updateListeners.Add(listener);
        if (component is StaticModel staticModel)
            staticModels.Add(staticModel);
    }

    internal void ComponentRemoved(Component component)
    {
        if (!components.Remove(component.Id))
            return;

        if (component is ISceneUpdateListener listener)
            updateListeners.Remove(listener);
        if (component is StaticModel staticModel)
            staticModels.Remove(staticModel);

        component.SetId(0);
        component.OnSceneSet(this, null);
    }

    private void UpdateAttributeAnimations(float timeStep)
    {
        var nodeSnapshot = SnapshotValues(nodes);
        try
        {
            for (var i = 0; i < nodeSnapshot.Count; ++i)
                nodeSnapshot.Array[i]!.ApplyAttributeAnimations(timeStep);
        }
        finally
        {
            nodeSnapshot.Dispose();
        }

        var componentSnapshot = SnapshotValues(components);
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
        var array = ArrayPool<ISceneUpdateListener>.Shared.Rent(updateListeners.Count);
        updateListeners.CopyTo(array, 0);
        return new Snapshot<ISceneUpdateListener>(array, updateListeners.Count);
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
