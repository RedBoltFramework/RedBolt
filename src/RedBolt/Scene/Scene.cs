namespace RedBolt;

public sealed class Scene
{
    private readonly HashSet<uint> componentIds = [];
    private readonly HashSet<uint> nodeIds = [];
    private readonly List<StaticModel> staticModels = [];
    private int asyncLoadingMs = 5;
    private float elapsedTime;
    private string fileName = string.Empty;
    private uint nextComponentId = 1;
    private uint nextNodeId = 1;

    public Scene()
    {
        Root = new Node(this, parent: null, name: string.Empty, AllocateNodeId(0));
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
        UpdateEnabled = enable;
    }

    public bool IsUpdateEnabled() => UpdateEnabled;

    public void SetTimeScale(float scale)
    {
        TimeScale = Math.Max(scale, 0.0f);
    }

    public float GetTimeScale() => TimeScale;

    public float GetEffectiveTimeScale() => EffectiveTimeScale;

    public void SetElapsedTime(float time)
    {
        elapsedTime = Math.Max(time, 0.0f);
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
        if (Root.Id == id)
            return Root;

        return Root.GetChildren(recursive: true).FirstOrDefault(node => node.Id == id);
    }

    public Component? GetComponent(uint id)
    {
        foreach (var component in GetAllComponents().Values)
        {
            if (component.Id == id)
                return component;
        }

        return null;
    }

    public IReadOnlyList<Node> GetNodesWithTag(string tag)
    {
        return Root.GetChildrenWithTag(tag, recursive: true);
    }

    public IReadOnlyDictionary<uint, Node> GetAllNodes()
    {
        var result = new Dictionary<uint, Node> { [Root.Id] = Root };
        foreach (var node in Root.GetChildren(recursive: true))
            result[node.Id] = node;
        return result;
    }

    public IReadOnlyDictionary<uint, Component> GetAllComponents()
    {
        var result = new Dictionary<uint, Component>();
        AddNodeComponents(Root, result);
        foreach (var node in Root.GetChildren(recursive: true))
            AddNodeComponents(node, result);
        return result;
    }

    public void GetComponents<T>(List<T> result)
        where T : Component
    {
        ArgumentNullException.ThrowIfNull(result);

        result.Clear();
        Root.FindComponents(ComponentSearchFlag.Self | ComponentSearchFlag.Children | ComponentSearchFlag.ChildrenRecursive, result);
    }

    public void Update(float timeStep)
    {
        if (!UpdateEnabled)
            return;

        var effectiveTimeStep = Math.Max(timeStep, 0.0f) * TimeScale;
        elapsedTime += effectiveTimeStep;

        var updateListeners = GetAllComponents().Values.OfType<ISceneUpdateListener>().ToArray();
        foreach (var listener in updateListeners)
            listener.OnSceneUpdate(effectiveTimeStep);
        foreach (var listener in updateListeners)
            listener.OnScenePostUpdate(effectiveTimeStep);
    }

    internal uint AllocateComponentId(uint requestedId)
    {
        return AllocateId(componentIds, ref nextComponentId, requestedId, "component");
    }

    internal uint AllocateNodeId(uint requestedId)
    {
        return AllocateId(nodeIds, ref nextNodeId, requestedId, "node");
    }

    internal void RegisterComponent(Component component)
    {
        if (component is StaticModel staticModel)
            staticModels.Add(staticModel);
    }

    internal void UnregisterComponent(Component component)
    {
        if (component is StaticModel staticModel)
            staticModels.Remove(staticModel);
    }

    internal void ReleaseComponentId(uint id)
    {
        componentIds.Remove(id);
    }

    internal void ReleaseNodeId(uint id)
    {
        if (id != Root.Id)
            nodeIds.Remove(id);
    }

    private static void AddNodeComponents(Node node, Dictionary<uint, Component> result)
    {
        foreach (var component in node.Components)
            result[component.Id] = component;
    }

    private static uint AllocateId(HashSet<uint> ids, ref uint nextId, uint requestedId, string kind)
    {
        var id = requestedId == 0 ? NextFreeId(ids, ref nextId) : requestedId;
        if (!ids.Add(id))
            throw new InvalidOperationException($"Scene {kind} ID {id} is already in use.");

        if (id >= nextId)
            nextId = id + 1;

        return id;
    }

    private static uint NextFreeId(HashSet<uint> ids, ref uint nextId)
    {
        while (nextId == 0 || ids.Contains(nextId))
            ++nextId;

        return nextId++;
    }
}
