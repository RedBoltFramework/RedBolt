namespace RedBolt;

public sealed class Scene
{
    private readonly List<StaticModel> staticModels = [];

    public Scene()
    {
        Root = new Node(this, parent: null, name: string.Empty);
    }

    public Node Root { get; }

    internal IReadOnlyList<StaticModel> StaticModels => staticModels;

    public bool UpdateEnabled { get; set; } = true;

    public float TimeScale { get; set; } = 1.0f;

    public float EffectiveTimeScale => UpdateEnabled ? TimeScale : 0.0f;

    public Node CreateChild(string name = "")
    {
        return Root.CreateChild(name);
    }

    public T CreateComponent<T>(uint id = 0)
        where T : Component, new()
    {
        return Root.CreateComponent<T>(id);
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
}
