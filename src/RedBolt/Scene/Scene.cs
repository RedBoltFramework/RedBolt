namespace RedBolt;

public sealed class Scene
{
    private readonly HashSet<uint> componentIds = [];
    private readonly HashSet<uint> nodeIds = [];
    private readonly List<StaticModel> staticModels = [];
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

    public Node CreateChild(string name = "", uint id = 0, bool temporary = false)
    {
        return Root.CreateChild(name, id, temporary);
    }

    public T CreateComponent<T>(uint id = 0)
        where T : Component, new()
    {
        return Root.CreateComponent<T>(id);
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
