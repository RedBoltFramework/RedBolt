namespace RedBolt;

public sealed class Node : Animatable
{
    private readonly List<Component> components = [];
    private readonly List<Node> children = [];
    private readonly List<Component> listeners = [];
    private readonly List<string> tags = [];
    private readonly Dictionary<StringHash, object?> variables = new();
    private bool dirty;
    private bool enabled = true;
    private bool enabledPrev = true;
    private string name = string.Empty;
    private Vector3 position = Vector3.Zero;
    private Quaternion rotation = Quaternion.Identity;
    private Vector3 scale = Vector3.One;

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
            "Tags",
            typeof(IReadOnlyList<string>),
            self => ((Node)self).Tags,
            (self, value) => ((Node)self).SetTags(value is IEnumerable<string> tagsValue ? tagsValue : []),
            Array.Empty<string>());
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
        RegisterAttribute(
            "Variables",
            typeof(IReadOnlyDictionary<StringHash, object?>),
            self => ((Node)self).Variables,
            (self, value) => ((Node)self).SetVariables(value is IEnumerable<KeyValuePair<StringHash, object?>> variablesValue ? variablesValue : []),
            new Dictionary<StringHash, object?>());
    }

    public static IReadOnlyList<Node> GetNodes(IEnumerable<Component> components)
    {
        ArgumentNullException.ThrowIfNull(components);
        return components
            .Where(static component => component.IsAttached)
            .Select(static component => component.Node)
            .Distinct()
            .ToArray();
    }

    public static IReadOnlyList<Node> GetParentNodes(IEnumerable<Node> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        var candidates = nodes.Distinct().ToArray();
        return candidates
            .Where(node => !candidates.Any(other => !ReferenceEquals(node, other) && node.IsChildOf(other)))
            .ToArray();
    }

    public IReadOnlyList<Node> Children => children;

    public IReadOnlyList<Component> Components => components;

    public IReadOnlyList<string> Tags => tags;

    public IReadOnlyDictionary<StringHash, object?> Variables => variables;

    public IReadOnlyDictionary<StringHash, object?> Vars => Variables;

    public uint Id { get; private set; }

    public string Name => name;

    public StringHash NameHash => Name;

    public bool Enabled
    {
        get => enabled;
        set => SetEnabled(value);
    }

    public bool IsEnabled => enabled;

    public bool IsEnabledSelf => enabledPrev;

    public bool IsDirty => dirty;

    public Node? Parent { get; private set; }

    public Vector3 Position
    {
        get => position;
        set => SetPosition(value);
    }

    public Quaternion Rotation
    {
        get => rotation;
        set => SetRotation(value);
    }

    public Vector3 Scale
    {
        get => scale;
        set => SetScale(value);
    }

    public Scene Scene { get; }

    public Vector3 Direction => rotation * Vector3.Forward;

    public Vector3 Right => rotation * Vector3.Right;

    public Vector3 Up => rotation * Vector3.Up;

    public Vector3 WorldDirection => WorldRotation * Vector3.Forward;

    public Vector3 WorldPosition
    {
        get
        {
            ClearDirty();
            return IsTransformHierarchyRoot() ? position : Parent!.LocalToWorld(position);
        }
    }

    public Quaternion WorldRotation
    {
        get
        {
            ClearDirty();
            return IsTransformHierarchyRoot() ? rotation : (Parent!.WorldRotation * rotation).Normalized;
        }
    }

    public Vector3 WorldScale
    {
        get
        {
            ClearDirty();
            return IsTransformHierarchyRoot() ? scale : (Parent!.GetWorldTransform() * GetTransformMatrix()).Scale;
        }
    }

    public Vector3 WorldRight => WorldRotation * Vector3.Right;

    public Vector3 WorldUp => WorldRotation * Vector3.Up;

    public Node CreateChild(string name = "", uint id = 0, bool temporary = false)
    {
        var child = new Node(Scene, parent: null, name: name, id: Scene.AllocateNodeId(id), temporary: temporary);
        AddChild(child);
        return child;
    }

    public Node CreateChild(uint id, bool temporary = false)
    {
        return CreateChild(name: string.Empty, id: id, temporary: temporary);
    }

    public Node CreateTemporaryChild(string name = "", uint id = 0)
    {
        return CreateChild(name, id, temporary: true);
    }

    public T CreateComponent<T>(uint id = 0)
        where T : Component, new()
    {
        var component = new T();
        AddComponent(component, id);
        return component;
    }

    public Component? CreateComponent(Type type, uint id = 0)
    {
        ArgumentNullException.ThrowIfNull(type);
        if (type.IsAbstract || !typeof(Component).IsAssignableFrom(type))
            return null;

        if (Activator.CreateInstance(type) is not Component component)
            return null;

        AddComponent(component, id);
        return component;
    }

    public void AddComponent(Component component, uint id = 0)
    {
        ArgumentNullException.ThrowIfNull(component);
        if (component.IsAttached)
            throw new InvalidOperationException("Component already belongs to a node.");

        component.SetId(Scene.AllocateComponentId(id));
        component.Attach(this);
        components.Add(component);
        Scene.ComponentAdded(component);
        component.OnMarkedDirty(this);

        var eventData = new SceneEvents.ComponentAddedEvent
        {
            Scene = Scene,
            Node = this,
            Component = component
        };
        Scene.SendEvent(ref eventData);
    }

    public T GetOrCreateComponent<T>(uint id = 0)
        where T : Component, new()
    {
        return GetComponent<T>() ?? CreateComponent<T>(id);
    }

    public Component? GetOrCreateComponent(Type type, uint id = 0)
    {
        ValidateComponentType(type, nameof(type));
        return GetComponent(type) ?? CreateComponent(type, id);
    }

    public Component? CloneComponent(Component component, uint id = 0)
    {
        ArgumentNullException.ThrowIfNull(component);

        if (Activator.CreateInstance(component.GetType()) is not Component clone)
            return null;

        clone.SetId(Scene.AllocateComponentId(id));
        clone.Attach(this);
        components.Add(clone);
        Scene.ComponentAdded(clone);
        clone.CopyAttributes(component);
        clone.SetTemporary(component.IsTemporary);
        clone.ApplyAttributes();

        var componentAddedEvent = new SceneEvents.ComponentAddedEvent
        {
            Scene = Scene,
            Node = this,
            Component = clone
        };
        Scene.SendEvent(ref componentAddedEvent);

        var componentClonedEvent = new SceneEvents.ComponentClonedEvent
        {
            Scene = Scene,
            Node = this,
            Component = component,
            CloneComponent = clone
        };
        Scene.SendEvent(ref componentClonedEvent);
        return clone;
    }

    public T? CloneComponent<T>(T component, uint id = 0)
        where T : Component
    {
        return CloneComponent((Component)component, id) as T;
    }

    public T? GetComponent<T>()
        where T : Component
    {
        foreach (var component in components)
        {
            if (component.GetType() == typeof(T) && component is T typed)
                return typed;
        }

        return null;
    }

    public Component? GetComponent(Type componentType)
    {
        ValidateComponentType(componentType, nameof(componentType));

        foreach (var component in components)
        {
            if (component.GetType() == componentType)
                return component;
        }

        return null;
    }

    public IReadOnlyList<Component> GetComponents() => Components;

    public IReadOnlyList<T> GetComponents<T>()
        where T : Component
    {
        var result = new List<T>();
        foreach (var component in components)
        {
            if (component.GetType() == typeof(T) && component is T typed)
                result.Add(typed);
        }

        return result;
    }

    public IReadOnlyList<Component> GetComponents(Type componentType)
    {
        ValidateComponentType(componentType, nameof(componentType));

        var result = new List<Component>();
        foreach (var component in components)
        {
            if (component.GetType() == componentType)
                result.Add(component);
        }

        return result;
    }

    public Component? GetComponent(uint index)
    {
        return index < components.Count ? components[(int)index] : null;
    }

    public T? GetNthComponent<T>(uint index)
        where T : Component
    {
        var current = 0u;
        foreach (var component in components)
        {
            if (component.GetType() != typeof(T) || component is not T typed)
                continue;

            if (current == index)
                return typed;

            ++current;
        }

        return null;
    }

    public Component? GetNthComponent(Type componentType, uint index)
    {
        ValidateComponentType(componentType, nameof(componentType));

        foreach (var component in components)
        {
            if (component.GetType() != componentType)
                continue;

            if (index == 0)
                return component;

            --index;
        }

        return null;
    }

    public T? GetDerivedComponent<T>()
        where T : Component
    {
        foreach (var component in components)
        {
            if (component is T typed)
                return typed;
        }

        return null;
    }

    public Component? GetDerivedComponent(Type componentType)
    {
        ValidateComponentType(componentType, nameof(componentType));

        foreach (var component in components)
        {
            if (componentType.IsInstanceOfType(component))
                return component;
        }

        return null;
    }

    public bool HasComponent<T>()
        where T : Component
    {
        return GetComponent<T>() is not null;
    }

    public bool HasComponent(Type componentType)
    {
        return GetComponent(componentType) is not null;
    }

    public uint GetNumComponents() => (uint)components.Count;

    public uint GetNumPersistentComponents()
    {
        uint count = 0;
        foreach (var component in components)
        {
            if (!component.IsTemporary)
                ++count;
        }

        return count;
    }

    public T? GetParentComponent<T>()
        where T : Component
    {
        return Parent?.GetComponent<T>();
    }

    public Component? GetParentComponent(Type componentType)
    {
        ValidateComponentType(componentType, nameof(componentType));
        return Parent?.GetComponent(componentType);
    }

    public Node? GetParent() => Parent;

    public Scene GetScene() => Scene;

    public string GetName() => Name;

    public StringHash GetNameHash() => NameHash;

    public IReadOnlyList<string> GetTags() => Tags;

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

    public uint GetChildIndex(Node child)
    {
        ArgumentNullException.ThrowIfNull(child);
        var index = children.IndexOf(child);
        return index >= 0 ? (uint)index : uint.MaxValue;
    }

    public uint GetIndexInParent()
    {
        return Parent?.GetChildIndex(this) ?? uint.MaxValue;
    }

    public uint GetNumChildren(bool recursive = false)
    {
        if (!recursive)
            return (uint)children.Count;

        uint count = (uint)children.Count;
        foreach (var child in children)
            count += child.GetNumChildren(recursive: true);
        return count;
    }

    public uint GetNumPersistentChildren()
    {
        uint count = 0;
        foreach (var child in children)
        {
            if (!child.IsTemporary)
                ++count;
        }

        return count;
    }

    public Node? GetChild(uint index)
    {
        return index < children.Count ? children[(int)index] : null;
    }

    public Node? GetChild(StringHash nameHash, bool recursive = false)
    {
        foreach (var child in children)
        {
            if (child.NameHash == nameHash)
                return child;

            if (recursive)
            {
                var descendant = child.GetChild(nameHash, recursive: true);
                if (descendant is not null)
                    return descendant;
            }
        }

        return null;
    }

    public Node? GetChild(string name, bool recursive = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return GetChild(new StringHash(name), recursive);
    }

    public IReadOnlyList<Node> GetChildren(bool recursive = false)
    {
        if (!recursive)
            return children.ToArray();

        var result = new List<Node>();
        AddChildren(result, recursive: true);
        return result;
    }

    public IReadOnlyList<Node> GetChildrenWithTag(string tag, bool recursive = false)
    {
        var result = new List<Node>();
        foreach (var child in children)
        {
            if (child.HasTag(tag))
                result.Add(child);
            if (recursive)
                result.AddRange(child.GetChildrenWithTag(tag, recursive: true));
        }
        return result;
    }

    public IReadOnlyList<Node> GetChildrenWithComponent<T>(bool recursive = false)
        where T : Component
    {
        return GetChildrenWithComponent(typeof(T), recursive);
    }

    public IReadOnlyList<Node> GetChildrenWithComponent(Type componentType, bool recursive = false)
    {
        ArgumentNullException.ThrowIfNull(componentType);
        if (!typeof(Component).IsAssignableFrom(componentType))
            throw new ArgumentException("Type must derive from Component.", nameof(componentType));

        var result = new List<Node>();
        foreach (var child in children)
        {
            if (child.components.Any(component => component.GetType() == componentType))
                result.Add(child);
            if (recursive)
                result.AddRange(child.GetChildrenWithComponent(componentType, recursive: true));
        }
        return result;
    }

    public Node? FindChild(string path, bool firstRecursive = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var separator = path.IndexOf('/');
        var isLast = separator < 0;
        var childName = isLast ? path : path[..separator];
        if (string.IsNullOrEmpty(childName))
            return null;

        var subPath = isLast ? string.Empty : path[(separator + 1)..];
        if (childName == "**")
            return FindChild(subPath, firstRecursive: true);

        var child = childName.StartsWith("#", StringComparison.Ordinal) && uint.TryParse(childName[1..], out var index)
            ? GetChild(index)
            : GetChild(childName, firstRecursive);
        return child is not null && !isLast ? child.FindChild(subPath) : child;
    }

    public (Serializable? Serializable, uint AttributeIndex) FindComponentAttribute(string path)
    {
        if (string.IsNullOrEmpty(path) || path[0] != '@')
            return default;

        var separator = path.IndexOf('/');
        if (separator < 0)
            return default;

        var componentName = path[1..separator];
        var attributeName = path[(separator + 1)..];
        var serializable = GetSerializableByName(componentName);
        if (serializable is null)
            return default;

        var attributes = serializable.GetAttributes();
        for (var i = 0; i < attributes.Count; ++i)
        {
            if (string.Equals(attributes[i].Name, attributeName, StringComparison.OrdinalIgnoreCase))
                return (serializable, (uint)i);
        }

        return default;
    }

    public bool IsChildOf(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);
        var parent = Parent;
        while (parent is not null)
        {
            if (ReferenceEquals(parent, node))
                return true;
            parent = parent.Parent;
        }

        return false;
    }

    public Node? GetDirectChildFor(Node indirectChild)
    {
        ArgumentNullException.ThrowIfNull(indirectChild);
        if (ReferenceEquals(indirectChild, this))
            return null;

        var child = indirectChild;
        while (child.Parent is not null && !ReferenceEquals(child.Parent, this))
            child = child.Parent;

        return ReferenceEquals(child.Parent, this) ? child : null;
    }

    public bool IsTemporaryEffective()
    {
        for (var node = this; node is not null; node = node.Parent)
        {
            if (node.IsTemporary)
                return true;
        }

        return false;
    }

    public bool IsTransformHierarchyRoot() => Parent is null || ReferenceEquals(Parent, Scene.Root);

    public Vector3 GetPosition() => Position;

    public Vector2 GetPosition2D() => new(position.X, position.Y);

    public Quaternion GetRotation() => Rotation;

    public float GetRotation2D() => rotation.RollAngle;

    public Vector3 GetDirection() => Direction;

    public Vector3 GetRight() => Right;

    public Vector3 GetScale() => Scale;

    public Vector2 GetScale2D() => new(scale.X, scale.Y);

    public Transform GetTransform() => new(position, rotation, scale);

    public Matrix3x4 GetTransformMatrix() => new(position, rotation, scale);

    public Vector3 GetUp() => Up;

    public Vector3 GetWorldDirection() => WorldDirection;

    public Vector3 GetWorldPosition() => WorldPosition;

    public Vector2 GetWorldPosition2D() => new(WorldPosition.X, WorldPosition.Y);

    public Quaternion GetWorldRotation() => WorldRotation;

    public float GetWorldRotation2D() => WorldRotation.RollAngle;

    public Vector3 GetWorldRight() => WorldRight;

    public Vector3 GetWorldScale() => WorldScale;

    public Vector3 GetSignedWorldScale() => GetWorldTransform().SignedScale(WorldRotation.RotationMatrix);

    public Vector2 GetWorldScale2D() => new(WorldScale.X, WorldScale.Y);

    public Matrix3x4 GetWorldTransform() => new(WorldPosition, WorldRotation, WorldScale);

    public Vector3 GetWorldUp() => WorldUp;

    public bool HasTag(string tag) => tags.Contains(tag);

    public bool HasVar(string key) => variables.ContainsKey(new StringHash(key));

    public bool HasVar(StringHash key) => variables.ContainsKey(key);

    public Vector3 LocalToWorld(Vector3 localPosition) => WorldPosition + WorldRotation * (localPosition * WorldScale);

    public Vector3 LocalToWorld(Vector4 localVector) => WorldRotation * (new Vector3(localVector.X, localVector.Y, localVector.Z) * WorldScale) + WorldPosition * localVector.W;

    public Vector2 LocalToWorld2D(Vector2 localVector) => LocalToWorld(new Vector3(localVector.X, localVector.Y, 0.0f)).ToVector2();

    public Vector3 WorldToLocal(Vector3 worldPosition) => WorldRotation.Inverse * (worldPosition - WorldPosition) / WorldScale;

    public Vector3 WorldToLocal(Vector4 worldVector) => WorldRotation.Inverse * (new Vector3(worldVector.X, worldVector.Y, worldVector.Z) - WorldPosition * worldVector.W) / WorldScale;

    public Vector2 WorldToLocal2D(Vector2 worldVector) => WorldToLocal(new Vector3(worldVector.X, worldVector.Y, 0.0f)).ToVector2();

    public T? FindComponent<T>(ComponentSearchFlag flags = ComponentSearchFlag.Default)
        where T : Component
    {
        foreach (var component in FindComponents<T>(flags))
            return component;

        return null;
    }

    public Component? FindComponent(Type componentType, ComponentSearchFlag flags = ComponentSearchFlag.Default)
    {
        foreach (var component in FindComponents(componentType, flags))
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

    public IReadOnlyList<Component> FindComponents(Type componentType, ComponentSearchFlag flags = ComponentSearchFlag.Default)
    {
        ValidateComponentType(componentType, nameof(componentType));

        var result = new List<Component>();
        FindComponents(flags, componentType, result);
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
            if (components[index].GetType() == typeof(T) && components[index] is T component)
                RemoveComponent(component);
        }
    }

    public void RemoveComponent(Type componentType)
    {
        var component = GetComponent(componentType);
        if (component is not null)
            RemoveComponent(component);
    }

    public void RemoveComponents(Type componentType)
    {
        ValidateComponentType(componentType, nameof(componentType));

        for (var index = components.Count - 1; index >= 0; --index)
        {
            if (components[index].GetType() == componentType)
                RemoveComponent(components[index]);
        }
    }

    public void RemoveComponent(Component component)
    {
        ArgumentNullException.ThrowIfNull(component);

        if (components.Remove(component))
        {
            var eventData = new SceneEvents.ComponentRemovedEvent
            {
                Scene = Scene,
                Node = this,
                Component = component
            };
            Scene.SendEvent(ref eventData);

            RemoveListener(component);
            Scene.ComponentRemoved(component);
            component.Detach();
        }
    }

    public void ReorderComponent(Component component, uint index)
    {
        ArgumentNullException.ThrowIfNull(component);
        var oldIndex = components.IndexOf(component);
        if (oldIndex < 0 || index >= components.Count)
            return;

        components.RemoveAt(oldIndex);
        components.Insert((int)Math.Min(index, (uint)components.Count), component);
    }

    public void RemoveComponents()
    {
        for (var index = components.Count - 1; index >= 0; --index)
            RemoveComponent(components[index]);
    }

    public void RemoveAllComponents() => RemoveComponents();

    public Node? Clone(Node? parent = null)
    {
        var destination = parent ?? Parent;
        if (destination is null)
            return null;
        if (!ReferenceEquals(destination.Scene, Scene))
            throw new InvalidOperationException("Cross-scene cloning is not supported by the managed scene slice.");

        return CloneRecursive(destination);
    }

    public void Remove()
    {
        Parent?.RemoveChild(this);
    }

    public void RemoveChild(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);

        if (!ReferenceEquals(node.Parent, this) || !children.Remove(node))
            return;

        var eventData = new SceneEvents.NodeRemovedEvent
        {
            Scene = Scene,
            Parent = this,
            Node = node
        };
        Scene.SendEvent(ref eventData);

        node.Parent = null;
        node.MarkDirty();
        Scene.NodeRemoved(node);
    }

    public void AddChild(Node node, uint index = uint.MaxValue)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (ReferenceEquals(node, this) || IsChildOf(node))
            throw new InvalidOperationException("A node cannot be added to itself or its descendant.");
        if (!ReferenceEquals(node.Scene, Scene))
            throw new InvalidOperationException("Cross-scene parenting is not supported by the managed scene slice.");

        var wasAttached = node.Parent is not null;

        node.Parent?.DetachChildForReparent(node, this);
        var insertIndex = index >= children.Count ? children.Count : (int)index;
        children.Insert(insertIndex, node);
        node.Parent = this;
        if (!wasAttached)
            Scene.NodeAdded(node);
        node.MarkDirty();

        var eventData = new SceneEvents.NodeAddedEvent
        {
            Scene = Scene,
            Parent = this,
            Node = node
        };
        Scene.SendEvent(ref eventData);
    }

    public void ReorderChild(Node child, uint index)
    {
        ArgumentNullException.ThrowIfNull(child);
        if (!ReferenceEquals(child.Parent, this) || index >= children.Count)
            return;

        children.Remove(child);
        children.Insert((int)Math.Min(index, (uint)children.Count), child);
    }

    public void RemoveAllChildren() => RemoveChildren(recursive: true);

    public void RemoveChildren(bool recursive)
    {
        for (var index = children.Count - 1; index >= 0; --index)
        {
            var child = children[index];
            if (recursive)
                child.RemoveChildren(recursive: true);
            RemoveChild(child);
        }
    }

    public void SetEnabled(bool enable)
    {
        SetEnabled(enable, recursive: false, storeSelf: true);
    }

    public void SetDeepEnabled(bool enable)
    {
        SetEnabled(enable, recursive: true, storeSelf: false);
    }

    public void ResetDeepEnabled()
    {
        SetEnabled(enabledPrev, recursive: false, storeSelf: false);

        foreach (var child in children)
            child.ResetDeepEnabled();
    }

    public void SetEnabledRecursive(bool enable)
    {
        SetEnabled(enable, recursive: true, storeSelf: true);
    }

    public void SetParent(Node? parent)
    {
        if (parent is null)
        {
            Remove();
            return;
        }

        if (ReferenceEquals(parent, this) || parent.IsChildOf(this))
            throw new InvalidOperationException("A node cannot be reparented to itself or its descendant.");
        if (!ReferenceEquals(parent.Scene, Scene))
            throw new InvalidOperationException("Cross-scene reparenting is not supported by the managed scene slice.");
        if (ReferenceEquals(Parent, parent))
            return;

        var worldPosition = WorldPosition;
        var worldRotation = WorldRotation;
        var worldScale = WorldScale;
        var wasAttached = Parent is not null;

        Parent?.DetachChildForReparent(this, parent);
        parent.children.Add(this);
        Parent = parent;
        if (!wasAttached)
            Scene.NodeAdded(this);
        SetWorldTransform(worldPosition, worldRotation, worldScale);
        MarkDirty();

        var eventData = new SceneEvents.NodeAddedEvent
        {
            Scene = Scene,
            Parent = parent,
            Node = this
        };
        Scene.SendEvent(ref eventData);
    }

    public void AddTag(string tag)
    {
        if (!string.IsNullOrEmpty(tag) && !tags.Contains(tag))
        {
            tags.Add(tag);
            Scene.NodeTagAdded(this, tag);

            var eventData = new SceneEvents.NodeTagAddedEvent
            {
                Scene = Scene,
                Node = this,
                Tag = tag
            };
            Scene.SendEvent(ref eventData);
        }
    }

    public void AddTags(string tags, char separator = ';')
    {
        ArgumentNullException.ThrowIfNull(tags);
        AddTags(tags.Split([separator], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    public void AddTags(IEnumerable<string> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);
        foreach (var tag in tags)
            AddTag(tag);
    }

    public object? GetVar(string key)
    {
        return GetVarByHash(new StringHash(key));
    }

    public object? GetVarByHash(StringHash key)
    {
        return variables.GetValueOrDefault(key);
    }

    public IReadOnlyDictionary<StringHash, object?> GetVars() => Variables;

    public IReadOnlyList<Component> GetListeners() => listeners.ToArray();

    public void AddListener(Component? component)
    {
        if (component is null || listeners.Contains(component))
            return;

        listeners.Add(component);
        if (dirty)
            component.OnMarkedDirty(this);
    }

    public void RemoveListener(Component? component)
    {
        if (component is null)
            return;

        listeners.Remove(component);
    }

    public bool LookAt(Vector3 target, Vector3? up = null, TransformSpace space = TransformSpace.World)
    {
        var worldTarget = space switch
        {
            TransformSpace.Local => LocalToWorld(target),
            TransformSpace.Parent => IsTransformHierarchyRoot() ? target : Parent!.LocalToWorld(target),
            _ => target
        };

        var direction = worldTarget - WorldPosition;
        if (direction.LengthSquared <= MathDefs.Epsilon)
            return false;

        if (!Quaternion.LookRotation(direction, up ?? Vector3.Up, out var newRotation))
            return false;

        SetWorldRotation(newRotation);
        return true;
    }

    public void MarkDirty()
    {
        if (dirty)
            return;

        dirty = true;

        foreach (var listener in listeners)
            listener.OnMarkedDirty(this);

        foreach (var component in components)
            component.OnMarkedDirty(this);

        foreach (var child in children)
            child.MarkDirty();
    }

    public void Pitch(float angle, TransformSpace space = TransformSpace.Local) => Rotate(Quaternion.FromAngleAxis(angle, Vector3.Right), space);

    public void RemoveAllTags()
    {
        for (var index = tags.Count - 1; index >= 0; --index)
            RemoveTag(tags[index]);
    }

    public bool RemoveTag(string tag)
    {
        if (!tags.Remove(tag))
            return false;

        Scene.NodeTagRemoved(this, tag);
        var eventData = new SceneEvents.NodeTagRemovedEvent
        {
            Scene = Scene,
            Node = this,
            Tag = tag
        };
        Scene.SendEvent(ref eventData);
        return true;
    }

    public bool RemoveVar(string key) => variables.Remove(new StringHash(key));

    public bool RemoveVarByHash(StringHash key) => variables.Remove(key);

    public void Roll(float angle, TransformSpace space = TransformSpace.Local) => Rotate(Quaternion.FromAngleAxis(angle, Vector3.Forward), space);

    public void Rotate(Quaternion delta, TransformSpace space = TransformSpace.Local)
    {
        switch (space)
        {
            case TransformSpace.Local:
                SetRotation((rotation * delta).Normalized);
                break;
            case TransformSpace.Parent:
                SetRotation((delta * rotation).Normalized);
                break;
            case TransformSpace.World:
                if (IsTransformHierarchyRoot())
                    SetRotation((delta * rotation).Normalized);
                else
                {
                    var worldRotation = WorldRotation;
                    SetRotation((rotation * worldRotation.Inverse * delta * worldRotation).Normalized);
                }
                break;
        }
    }

    public void Rotate2D(float delta, TransformSpace space = TransformSpace.Local) => Rotate(Quaternion.FromRotation2D(delta), space);

    public void RotateAround(Vector3 point, Quaternion delta, TransformSpace space = TransformSpace.Local)
    {
        var parentSpacePoint = space switch
        {
            TransformSpace.Local => GetTransformMatrix() * point,
            TransformSpace.Parent => point,
            TransformSpace.World => IsTransformHierarchyRoot() ? point : Parent!.WorldToLocal(point),
            _ => point
        };
        var oldRotation = rotation;

        rotation = space switch
        {
            TransformSpace.Local => (rotation * delta).Normalized,
            TransformSpace.Parent => (delta * rotation).Normalized,
            TransformSpace.World when IsTransformHierarchyRoot() => (delta * rotation).Normalized,
            TransformSpace.World => (rotation * WorldRotation.Inverse * delta * WorldRotation).Normalized,
            _ => rotation
        };

        var oldRelativePosition = oldRotation.Inverse * (position - parentSpacePoint);
        position = rotation * oldRelativePosition + parentSpacePoint;
        MarkDirty();
    }

    public void RotateAround2D(Vector2 point, float delta, TransformSpace space = TransformSpace.Local) => RotateAround(new Vector3(point.X, point.Y, 0.0f), Quaternion.FromRotation2D(delta), space);

    public void SetDirection(Vector3 direction)
    {
        SetRotation(new Quaternion(Vector3.Forward, direction));
    }

    public void SetPosition(Vector3 position)
    {
        this.position = position;
        MarkDirty();
    }

    public void SetPositionSilent(Vector3 position)
    {
        this.position = position;
    }

    public void SetPosition2D(Vector2 position) => SetPosition(new Vector3(position.X, position.Y, 0.0f));

    public void SetPosition2D(float x, float y) => SetPosition2D(new Vector2(x, y));

    public void SetRotation(Quaternion rotation)
    {
        this.rotation = rotation.Normalized;
        MarkDirty();
    }

    public void SetRotationSilent(Quaternion rotation)
    {
        this.rotation = rotation;
    }

    public void SetRotation2D(float rotation) => SetRotation(Quaternion.FromRotation2D(rotation));

    public void SetScale(float scale) => SetScale(new Vector3(scale, scale, scale));

    public void SetScale(Vector3 scale)
    {
        this.scale = new Vector3(
            Math.Abs(scale.X) <= MathDefs.Epsilon ? MathDefs.Epsilon : scale.X,
            Math.Abs(scale.Y) <= MathDefs.Epsilon ? MathDefs.Epsilon : scale.Y,
            Math.Abs(scale.Z) <= MathDefs.Epsilon ? MathDefs.Epsilon : scale.Z);
        MarkDirty();
    }

    public void SetScaleSilent(Vector3 scale)
    {
        this.scale = scale;
    }

    public void SetScale2D(Vector2 scale) => SetScale(new Vector3(scale.X, scale.Y, 1.0f));

    public void SetScale2D(float x, float y) => SetScale2D(new Vector2(x, y));

    public void ScaleBy(float scale) => ScaleBy(new Vector3(scale, scale, scale));

    public void ScaleBy(Vector3 scale)
    {
        this.scale *= scale;
        MarkDirty();
    }

    public void ScaleBy2D(Vector2 scale) => ScaleBy(new Vector3(scale.X, scale.Y, 1.0f));

    public void ScaleAround(Vector3 point, Vector3 scale, TransformSpace space = TransformSpace.Local)
    {
        var parentSpacePoint = space switch
        {
            TransformSpace.Local => GetTransformMatrix() * point,
            TransformSpace.Parent => point,
            TransformSpace.World => IsTransformHierarchyRoot() ? point : Parent!.WorldToLocal(point),
            _ => point
        };
        var oldScale = this.scale;

        this.scale *= scale;
        var oldRelativePosition = Vector3.One / oldScale * (position - parentSpacePoint);
        position = this.scale * oldRelativePosition + parentSpacePoint;
        MarkDirty();
    }

    public void ScaleAround2D(Vector2 point, Vector2 scale, TransformSpace space = TransformSpace.Local) => ScaleAround(new Vector3(point.X, point.Y, 0.0f), new Vector3(scale.X, scale.Y, 1.0f), space);

    public void SetTags(IEnumerable<string> tags)
    {
        RemoveAllTags();
        AddTags(tags);
    }

    public void SetTransform(Vector3 position, Quaternion rotation)
    {
        this.position = position;
        this.rotation = rotation.Normalized;
        MarkDirty();
    }

    public void SetTransform(Vector3 position, Quaternion rotation, float scale) => SetTransform(position, rotation, new Vector3(scale, scale, scale));

    public void SetTransform(Vector3 position, Quaternion rotation, Vector3 scale)
    {
        this.position = position;
        this.rotation = rotation.Normalized;
        this.scale = scale;
        MarkDirty();
    }

    public void SetTransformSilent(Vector3 position, Quaternion rotation, Vector3 scale)
    {
        this.position = position;
        this.rotation = rotation;
        this.scale = scale;
    }

    public void SetTransformMatrix(Matrix3x4 matrix) => SetTransform(matrix.Translation, matrix.Rotation, matrix.Scale);

    public void SetTransformMatrixSilent(Matrix3x4 matrix) => SetTransformSilent(matrix.Translation, matrix.Rotation, matrix.Scale);

    public void SetTransform(Transform transform) => SetTransform(transform.Position, transform.Rotation, transform.Scale);

    public void SetTransform2D(Vector2 position, float rotation) => SetTransform(new Vector3(position.X, position.Y, 0.0f), Quaternion.FromRotation2D(rotation));

    public void SetTransform2D(Vector2 position, float rotation, float scale) => SetTransform(new Vector3(position.X, position.Y, 0.0f), Quaternion.FromRotation2D(rotation), new Vector3(scale, scale, 1.0f));

    public void SetTransform2D(Vector2 position, float rotation, Vector2 scale) => SetTransform(new Vector3(position.X, position.Y, 0.0f), Quaternion.FromRotation2D(rotation), new Vector3(scale.X, scale.Y, 1.0f));

    public void SetVar(string key, object? value) => variables[new StringHash(key)] = value;

    public void SetVarByHash(StringHash key, object? value)
    {
        if (variables.ContainsKey(key))
            variables[key] = value;
    }

    public void SetVariables(IEnumerable<KeyValuePair<StringHash, object?>> variables)
    {
        this.variables.Clear();
        foreach (var item in variables)
            this.variables[item.Key] = item.Value;
    }

    public void SetWorldDirection(Vector3 direction)
    {
        if (direction.LengthSquared > 0.0f)
            SetWorldRotation(Quaternion.FromRotationTo(Vector3.Forward, direction.NormalizedOrDefault));
    }

    public void SetWorldPosition(Vector3 position)
    {
        SetPosition(IsTransformHierarchyRoot() ? position : Parent!.WorldToLocal(position));
    }

    public void SetWorldPosition2D(Vector2 position) => SetWorldPosition(new Vector3(position.X, position.Y, 0.0f));

    public void SetWorldPosition2D(float x, float y) => SetWorldPosition2D(new Vector2(x, y));

    public void SetWorldRotation(Quaternion rotation)
    {
        SetRotation(IsTransformHierarchyRoot() ? rotation : Parent!.WorldRotation.Inverse * rotation);
    }

    public void SetWorldRotation2D(float rotation) => SetWorldRotation(Quaternion.FromRotation2D(rotation));

    public void SetWorldScale(float scale) => SetWorldScale(new Vector3(scale, scale, scale));

    public void SetWorldScale(Vector3 scale)
    {
        SetScale(IsTransformHierarchyRoot() ? scale : scale / Parent!.WorldScale);
    }

    public void SetWorldScale2D(Vector2 scale) => SetWorldScale(new Vector3(scale.X, scale.Y, 1.0f));

    public void SetWorldScale2D(float x, float y) => SetWorldScale2D(new Vector2(x, y));

    public void SetWorldTransform(Vector3 position, Quaternion rotation)
    {
        SetWorldPosition(position);
        SetWorldRotation(rotation);
    }

    public void SetWorldTransform(Vector3 position, Quaternion rotation, float scale)
    {
        SetWorldPosition(position);
        SetWorldRotation(rotation);
        SetWorldScale(scale);
    }

    public void SetWorldTransform(Vector3 position, Quaternion rotation, Vector3 scale)
    {
        SetWorldPosition(position);
        SetWorldRotation(rotation);
        SetWorldScale(scale);
    }

    public void SetWorldTransform(Matrix3x4 worldTransform) => SetWorldTransform(worldTransform.Translation, worldTransform.Rotation, worldTransform.Scale);

    public void SetWorldTransform2D(Vector2 position, float rotation) => SetWorldTransform(new Vector3(position.X, position.Y, 0.0f), Quaternion.FromRotation2D(rotation));

    public void SetWorldTransform2D(Vector2 position, float rotation, float scale) => SetWorldTransform(new Vector3(position.X, position.Y, 0.0f), Quaternion.FromRotation2D(rotation), new Vector3(scale, scale, 1.0f));

    public void SetWorldTransform2D(Vector2 position, float rotation, Vector2 scale) => SetWorldTransform(new Vector3(position.X, position.Y, 0.0f), Quaternion.FromRotation2D(rotation), new Vector3(scale.X, scale.Y, 1.0f));

    public void Translate(Vector3 delta, TransformSpace space = TransformSpace.Local)
    {
        switch (space)
        {
            case TransformSpace.Local:
                SetPosition(position + rotation * delta);
                break;
            case TransformSpace.Parent:
                SetPosition(position + delta);
                break;
            case TransformSpace.World:
                SetWorldPosition(WorldPosition + delta);
                break;
        }
    }

    public void Translate2D(Vector2 delta, TransformSpace space = TransformSpace.Local) => Translate(new Vector3(delta.X, delta.Y, 0.0f), space);

    public void Yaw(float angle, TransformSpace space = TransformSpace.Local) => Rotate(Quaternion.FromAngleAxis(angle, Vector3.Up), space);

    public void SetName(string name)
    {
        name ??= string.Empty;
        if (this.name == name)
            return;

        this.name = name;
        var eventData = new SceneEvents.NodeNameChangedEvent
        {
            Scene = Scene,
            Node = this
        };
        Scene.SendEvent(ref eventData);
    }

    internal void SetId(uint id)
    {
        Id = id;
    }

    private void SetEnabled(bool enable, bool recursive, bool storeSelf)
    {
        if (storeSelf)
            enabledPrev = enable;

        if (enable == enabled)
        {
            if (recursive)
            {
                foreach (var child in children)
                    child.SetEnabled(enable, recursive: true, storeSelf: storeSelf);
            }

            return;
        }

        enabled = enable;

        foreach (var listener in listeners)
            listener.OnNodeSetEnabled(this);

        foreach (var component in components)
        {
            component.OnSetEnabled();

            var componentEventData = new SceneEvents.ComponentEnabledChangedEvent
            {
                Scene = Scene,
                Node = this,
                Component = component
            };
            Scene.SendEvent(ref componentEventData);
        }

        var eventData = new SceneEvents.NodeEnabledChangedEvent
        {
            Scene = Scene,
            Node = this
        };
        Scene.SendEvent(ref eventData);

        if (recursive)
        {
            foreach (var child in children)
                child.SetEnabled(enable, recursive: true, storeSelf: storeSelf);
        }
    }

    private void AddChildren(List<Node> result, bool recursive)
    {
        foreach (var child in children)
        {
            result.Add(child);
            if (recursive)
                child.AddChildren(result, recursive: true);
        }
    }

    private Node CloneRecursive(Node parent)
    {
        var clone = parent.CreateChild(name, temporary: Temporary);
        clone.CopyAttributes(this);
        clone.SetTemporary(Temporary);

        foreach (var component in components)
        {
            if (!component.IsTemporary)
                clone.CloneComponent(component);
        }

        foreach (var child in children)
        {
            if (!child.IsTemporary)
                child.CloneRecursive(clone);
        }

        clone.ApplyAttributes();

        var eventData = new SceneEvents.NodeClonedEvent
        {
            Scene = Scene,
            Node = this,
            CloneNode = clone
        };
        Scene.SendEvent(ref eventData);
        return clone;
    }

    internal void FindComponents<T>(ComponentSearchFlag flags, List<T> result)
        where T : Component
    {
        if ((flags & ComponentSearchFlag.Self) != 0)
            AddMatchingComponents(this, flags, result);

        if ((flags & ComponentSearchFlag.Parent) != 0)
        {
            var parentFlags = (flags & ComponentSearchFlag.ParentRecursive) != 0
                ? flags & ~(ComponentSearchFlag.Children | ComponentSearchFlag.ChildrenRecursive)
                : flags & ~(ComponentSearchFlag.Parent | ComponentSearchFlag.Children | ComponentSearchFlag.ChildrenRecursive);
            Parent?.FindComponents(parentFlags | ComponentSearchFlag.Self, result);
        }

        if ((flags & ComponentSearchFlag.Children) != 0)
        {
            var childFlags = (flags & ComponentSearchFlag.ChildrenRecursive) != 0
                ? flags & ~(ComponentSearchFlag.Parent | ComponentSearchFlag.ParentRecursive)
                : flags & ~(ComponentSearchFlag.Children | ComponentSearchFlag.ChildrenRecursive | ComponentSearchFlag.Parent | ComponentSearchFlag.ParentRecursive);
            foreach (var child in children)
                child.FindComponents(childFlags | ComponentSearchFlag.Self, result);
        }
    }

    internal void FindComponents(ComponentSearchFlag flags, Type componentType, List<Component> result)
    {
        if ((flags & ComponentSearchFlag.Self) != 0)
            AddMatchingComponents(this, flags, componentType, result);

        if ((flags & ComponentSearchFlag.Parent) != 0)
        {
            var parentFlags = (flags & ComponentSearchFlag.ParentRecursive) != 0
                ? flags & ~(ComponentSearchFlag.Children | ComponentSearchFlag.ChildrenRecursive)
                : flags & ~(ComponentSearchFlag.Parent | ComponentSearchFlag.Children | ComponentSearchFlag.ChildrenRecursive);
            Parent?.FindComponents(parentFlags | ComponentSearchFlag.Self, componentType, result);
        }

        if ((flags & ComponentSearchFlag.Children) != 0)
        {
            var childFlags = (flags & ComponentSearchFlag.ChildrenRecursive) != 0
                ? flags & ~(ComponentSearchFlag.Parent | ComponentSearchFlag.ParentRecursive)
                : flags & ~(ComponentSearchFlag.Children | ComponentSearchFlag.ChildrenRecursive | ComponentSearchFlag.Parent | ComponentSearchFlag.ParentRecursive);
            foreach (var child in children)
                child.FindComponents(childFlags | ComponentSearchFlag.Self, componentType, result);
        }
    }

    private static void AddMatchingComponents<T>(Node node, ComponentSearchFlag flags, List<T> result)
        where T : Component
    {
        var enabledOnly = (flags & ComponentSearchFlag.EnabledOnly) != 0;
        if (enabledOnly && !node.IsEnabled)
            return;

        foreach (var component in node.components)
        {
            var matches = (flags & ComponentSearchFlag.Derived) != 0 ? component is T : component.GetType() == typeof(T);
            if (matches && component is T typed && (!enabledOnly || component.IsEnabledEffective))
                result.Add(typed);
        }
    }

    private static void AddMatchingComponents(Node node, ComponentSearchFlag flags, Type componentType, List<Component> result)
    {
        var enabledOnly = (flags & ComponentSearchFlag.EnabledOnly) != 0;
        if (enabledOnly && !node.IsEnabled)
            return;

        foreach (var component in node.components)
        {
            var matches = (flags & ComponentSearchFlag.Derived) != 0
                ? componentType.IsInstanceOfType(component)
                : component.GetType() == componentType;
            if (matches && (!enabledOnly || component.IsEnabledEffective))
                result.Add(component);
        }
    }

    private Serializable? GetSerializableByName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return this;

        var index = 0u;
        var separator = name.IndexOf('#');
        if (separator >= 0)
        {
            if (!uint.TryParse(name[(separator + 1)..], out index))
                return null;
            name = name[..separator];
        }

        foreach (var component in components)
        {
            if (!string.Equals(component.GetType().Name, name, StringComparison.Ordinal))
                continue;

            if (index == 0)
                return component;

            --index;
        }

        return null;
    }

    private static void ValidateComponentType(Type componentType, string paramName)
    {
        ArgumentNullException.ThrowIfNull(componentType);
        if (!typeof(Component).IsAssignableFrom(componentType))
            throw new ArgumentException("Type must derive from Component.", paramName);
    }

    private void ClearDirty()
    {
        if (!dirty)
            return;

        if (!IsTransformHierarchyRoot())
            Parent!.ClearDirty();

        dirty = false;
    }

    private void DetachChildForReparent(Node child, Node newParent)
    {
        if (!children.Remove(child))
            return;

        if (!ReferenceEquals(newParent.Scene, Scene))
        {
            RemoveChild(child);
            return;
        }

        var eventData = new SceneEvents.NodeRemovedEvent
        {
            Scene = Scene,
            Parent = this,
            Node = child
        };
        Scene.SendEvent(ref eventData);
        child.Parent = null;
    }
}
