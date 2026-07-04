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

    public Node CreateChild(string name)
    {
        return Root.CreateChild(name);
    }

    public T CreateComponent<T>()
        where T : Component, new()
    {
        return Root.CreateComponent<T>();
    }

    internal void RegisterComponent(Component component)
    {
        if (component is StaticModel staticModel)
            staticModels.Add(staticModel);
    }
}
