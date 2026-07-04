namespace RedBolt;

public sealed class Node : Serializable
{
    private readonly List<Component> components = [];
    private readonly List<Node> children = [];
    private bool enabled = true;
    private string name = string.Empty;

    internal Node(Scene scene, Node? parent, string name, uint id, bool temporary = false)
    {
        Scene = scene;
        Parent = parent;
        Id = id;
        SetName(name);
        SetTemporary(temporary);

        RegisterAttribute(
            "Is Enabled",
            typeof(bool),
            self => ((Node)self).IsEnabledSelf,
            (self, value) => ((Node)self).SetEnabled(value is bool enabledValue && enabledValue),
            true);
        RegisterAttribute(
            "Name",
            typeof(string),
            self => ((Node)self).Name,
            (self, value) => ((Node)self).SetName(value as string ?? string.Empty),
            string.Empty);
        RegisterAttribute(
            "Position",
            typeof(Vector3),
            self => ((Node)self).Position,
            (self, value) => ((Node)self).Position = value is Vector3 position ? position : Vector3.Zero,
            Vector3.Zero);
        RegisterAttribute(
            "Rotation",
            typeof(Quaternion),
            self => ((Node)self).Rotation,
            (self, value) => ((Node)self).Rotation = value is Quaternion rotation ? rotation : Quaternion.Identity,
            Quaternion.Identity);
        RegisterAttribute(
            "Scale",
            typeof(Vector3),
            self => ((Node)self).Scale,
            (self, value) => ((Node)self).Scale = value is Vector3 scale ? scale : Vector3.One,
            Vector3.One);
    }

    public IReadOnlyList<Node> Children => children;

    public IReadOnlyList<Component> Components => components;

    public uint Id { get; private set; }

    public string Name => name;

    public StringHash NameHash => Name;

    public bool Enabled
    {
        get => enabled;
        set => SetEnabled(value);
    }

    public bool IsEnabled => enabled && (Parent?.IsEnabled ?? true);

    public bool IsEnabledSelf => enabled;

    public Node? Parent { get; }

    public Vector3 Position { get; set; } = Vector3.Zero;

    public Quaternion Rotation { get; set; } = Quaternion.Identity;

    public Vector3 Scale { get; set; } = Vector3.One;

    public Scene Scene { get; }

    public Node CreateChild(string name = "", uint id = 0, bool temporary = false)
    {
        var child = new Node(Scene, this, name, Scene.AllocateNodeId(id), temporary);
        children.Add(child);
        return child;
    }

    public Node CreateTemporaryChild(string name = "", uint id = 0)
    {
        return CreateChild(name, id, temporary: true);
    }

    public T CreateComponent<T>(uint id = 0)
        where T : Component, new()
    {
        var component = new T();
        component.SetId(Scene.AllocateComponentId(id));
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

    public uint GetComponentIndex(Component component)
    {
        ArgumentNullException.ThrowIfNull(component);

        var index = components.IndexOf(component);
        return index >= 0 ? (uint)index : uint.MaxValue;
    }

    public uint GetID()
    {
        return Id;
    }

    public string GetFullNameDebug()
    {
        var prefix = Parent is not null ? $"{Parent.GetFullNameDebug()}/[{Parent.children.IndexOf(this)}]" : string.Empty;
        return prefix + (string.IsNullOrEmpty(Name) ? nameof(Node) : Name);
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
        if (enabled == enable)
            return;

        enabled = enable;
        NotifyNodeSetEnabled();
    }

    public void SetEnabledRecursive(bool enable)
    {
        SetEnabled(enable);
        foreach (var child in children)
            child.SetEnabledRecursive(enable);
    }

    public void SetName(string name)
    {
        this.name = name ?? string.Empty;
    }

    internal void SetId(uint id)
    {
        Id = id;
    }

    private void NotifyNodeSetEnabled()
    {
        foreach (var component in components)
            component.OnNodeSetEnabled(this);

        foreach (var child in children)
            child.NotifyNodeSetEnabled();
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
