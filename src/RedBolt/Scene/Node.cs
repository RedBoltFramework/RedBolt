namespace RedBolt;

public sealed class Node
{
    private readonly List<Component> components = [];
    private readonly List<Node> children = [];

    internal Node(Scene scene, Node? parent, string name)
    {
        Scene = scene;
        Parent = parent;
        Name = name;
    }

    public IReadOnlyList<Node> Children => children;

    public IReadOnlyList<Component> Components => components;

    public string Name { get; }

    public Node? Parent { get; }

    public Vector3 Position { get; set; } = Vector3.Zero;

    public Quaternion Rotation { get; set; } = Quaternion.Identity;

    public Scene Scene { get; }

    public Node CreateChild(string name)
    {
        var child = new Node(Scene, this, name);
        children.Add(child);
        return child;
    }

    public T CreateComponent<T>()
        where T : Component, new()
    {
        var component = new T();
        component.Attach(this);
        components.Add(component);
        Scene.RegisterComponent(component);
        return component;
    }

    public T GetComponent<T>()
        where T : Component
    {
        foreach (var component in components)
        {
            if (component is T typed)
                return typed;
        }

        throw new InvalidOperationException($"Node '{Name}' does not have a {typeof(T).Name} component.");
    }
}
