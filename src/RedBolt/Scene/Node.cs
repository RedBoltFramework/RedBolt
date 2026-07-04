namespace RedBolt;

public sealed class Node
{
    private readonly List<Component> components = [];
    private readonly List<Node> children = [];
    private bool enabled = true;

    internal Node(Scene scene, Node? parent, string name)
    {
        Scene = scene;
        Parent = parent;
        Name = name;
    }

    public IReadOnlyList<Node> Children => children;

    public IReadOnlyList<Component> Components => components;

    public string Name { get; }

    public bool Enabled
    {
        get => enabled;
        set => enabled = value;
    }

    public bool IsEnabled => enabled && (Parent?.IsEnabled ?? true);

    public bool IsEnabledSelf => enabled;

    public Node? Parent { get; }

    public Vector3 Position { get; set; } = Vector3.Zero;

    public Quaternion Rotation { get; set; } = Quaternion.Identity;

    public Scene Scene { get; }

    public Node CreateChild(string name = "")
    {
        var child = new Node(Scene, this, name);
        children.Add(child);
        return child;
    }

    public T CreateComponent<T>(uint id = 0)
        where T : Component, new()
    {
        var component = new T();
        component.Attach(this);
        components.Add(component);
        Scene.RegisterComponent(component);
        return component;
    }

    public T GetOrCreateComponent<T>(uint id = 0)
        where T : Component, new()
    {
        return GetComponent<T>() ?? CreateComponent<T>(id);
    }

    public T? GetComponent<T>()
        where T : Component
    {
        foreach (var component in components)
        {
            if (component is T typed)
                return typed;
        }

        return null;
    }

    public IReadOnlyList<T> GetComponents<T>()
        where T : Component
    {
        var result = new List<T>();
        foreach (var component in components)
        {
            if (component is T typed)
                result.Add(typed);
        }

        return result;
    }

    public T? GetParentComponent<T>()
        where T : Component
    {
        var node = Parent;
        while (node is not null)
        {
            var component = node.GetComponent<T>();
            if (component is not null)
                return component;
            node = node.Parent;
        }

        return null;
    }

    public T? FindComponent<T>(ComponentSearchFlag flags = ComponentSearchFlag.Default)
        where T : Component
    {
        foreach (var component in FindComponents<T>(flags))
            return component;

        return null;
    }

    public IReadOnlyList<T> FindComponents<T>(ComponentSearchFlag flags = ComponentSearchFlag.Default)
        where T : Component
    {
        var result = new List<T>();
        FindComponents(flags, result);
        return result;
    }

    public void RemoveComponent<T>()
        where T : Component
    {
        var component = GetComponent<T>();
        if (component is not null)
            RemoveComponent(component);
    }

    public void RemoveComponents<T>()
        where T : Component
    {
        for (var index = components.Count - 1; index >= 0; --index)
        {
            if (components[index] is T component)
                RemoveComponent(component);
        }
    }

    public void RemoveComponent(Component component)
    {
        ArgumentNullException.ThrowIfNull(component);

        if (components.Remove(component))
        {
            Scene.UnregisterComponent(component);
            component.Detach();
        }
    }

    public void RemoveComponents()
    {
        for (var index = components.Count - 1; index >= 0; --index)
            RemoveComponent(components[index]);
    }

    public void SetEnabled(bool enable)
    {
        Enabled = enable;
    }

    public void SetEnabledRecursive(bool enable)
    {
        Enabled = enable;
        foreach (var child in children)
            child.SetEnabledRecursive(enable);
    }

    private void FindComponents<T>(ComponentSearchFlag flags, List<T> result)
        where T : Component
    {
        if (flags.HasFlag(ComponentSearchFlag.Self))
            AddMatchingComponents(this, flags, result);

        if (flags.HasFlag(ComponentSearchFlag.Parent))
        {
            var parentFlags = flags.HasFlag(ComponentSearchFlag.ParentRecursive)
                ? flags
                : flags & ~ComponentSearchFlag.Parent;
            Parent?.FindComponents(parentFlags | ComponentSearchFlag.Self, result);
        }

        if (flags.HasFlag(ComponentSearchFlag.Children))
        {
            var childFlags = flags.HasFlag(ComponentSearchFlag.ChildrenRecursive)
                ? flags
                : flags & ~ComponentSearchFlag.Children;
            foreach (var child in children)
                child.FindComponents(childFlags | ComponentSearchFlag.Self, result);
        }
    }

    private static void AddMatchingComponents<T>(Node node, ComponentSearchFlag flags, List<T> result)
        where T : Component
    {
        var enabledOnly = flags.HasFlag(ComponentSearchFlag.EnabledOnly);
        if (enabledOnly && !node.IsEnabled)
            return;

        foreach (var component in node.components)
        {
            if (component is T typed && (!enabledOnly || component.IsEnabledEffective))
                result.Add(typed);
        }
    }
}
