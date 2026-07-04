namespace RedBolt;

public sealed class Node : Serializable
{
    private readonly List<Component> components = [];
    private readonly List<Node> children = [];
    private readonly List<string> tags = [];
    private readonly Dictionary<StringHash, object?> variables = new();
    private bool enabled = true;
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

    public bool IsEnabled => enabled && (Parent?.IsEnabled ?? true);

    public bool IsEnabledSelf => enabled;

    public Node? Parent { get; }

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

    public Vector3 WorldPosition => Parent is null ? position : Parent.LocalToWorld(position);

    public Quaternion WorldRotation => Parent is null ? rotation : (Parent.WorldRotation * rotation).Normalized;

    public Vector3 WorldScale => Parent is null ? scale : Parent.WorldScale * scale;

    public Vector3 WorldRight => WorldRotation * Vector3.Right;

    public Vector3 WorldUp => WorldRotation * Vector3.Up;

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
        return GetChild(new StringHash(StringHash.Calculate(name)), recursive);
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

    public Vector2 GetWorldScale2D() => new(WorldScale.X, WorldScale.Y);

    public Matrix3x4 GetWorldTransform() => new(WorldPosition, WorldRotation, WorldScale);

    public Vector3 GetWorldUp() => WorldUp;

    public bool HasTag(string tag) => tags.Contains(tag);

    public bool HasVar(string key) => variables.ContainsKey(new StringHash(StringHash.Calculate(key)));

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

    public void AddTag(string tag)
    {
        if (!string.IsNullOrEmpty(tag) && !tags.Contains(tag))
            tags.Add(tag);
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
        return GetVarByHash(new StringHash(StringHash.Calculate(key)));
    }

    public object? GetVarByHash(StringHash key)
    {
        return variables.GetValueOrDefault(key);
    }

    public IReadOnlyDictionary<StringHash, object?> GetVars() => Variables;

    public bool LookAt(Vector3 target, Vector3? up = null, TransformSpace space = TransformSpace.World)
    {
        var worldTarget = space switch
        {
            TransformSpace.Local => LocalToWorld(target),
            TransformSpace.Parent => Parent is null ? target : Parent.LocalToWorld(target),
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
        foreach (var component in components)
            component.OnMarkedDirty(this);

        foreach (var child in children)
            child.MarkDirty();
    }

    public void Pitch(float angle, TransformSpace space = TransformSpace.Local) => Rotate(Quaternion.FromAngleAxis(angle, Vector3.Right), space);

    public void RemoveAllTags() => tags.Clear();

    public bool RemoveTag(string tag) => tags.Remove(tag);

    public bool RemoveVar(string key) => variables.Remove(new StringHash(StringHash.Calculate(key)));

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
                SetWorldRotation((delta * WorldRotation).Normalized);
                break;
        }
    }

    public void Rotate2D(float delta, TransformSpace space = TransformSpace.Local) => Rotate(Quaternion.FromRotation2D(delta), space);

    public void SetDirection(Vector3 direction)
    {
        SetRotation(new Quaternion(Vector3.Forward, direction));
    }

    public void SetPosition(Vector3 position)
    {
        this.position = position;
        MarkDirty();
    }

    public void SetPosition2D(Vector2 position) => SetPosition(new Vector3(position.X, position.Y, this.position.Z));

    public void SetPosition2D(float x, float y) => SetPosition2D(new Vector2(x, y));

    public void SetRotation(Quaternion rotation)
    {
        this.rotation = rotation.Normalized;
        MarkDirty();
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

    public void SetScale2D(Vector2 scale) => SetScale(new Vector3(scale.X, scale.Y, this.scale.Z));

    public void SetScale2D(float x, float y) => SetScale2D(new Vector2(x, y));

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

    public void SetTransform(Transform transform) => SetTransform(transform.Position, transform.Rotation, transform.Scale);

    public void SetTransform2D(Vector2 position, float rotation) => SetTransform(new Vector3(position.X, position.Y, this.position.Z), Quaternion.FromRotation2D(rotation));

    public void SetTransform2D(Vector2 position, float rotation, float scale) => SetTransform(new Vector3(position.X, position.Y, this.position.Z), Quaternion.FromRotation2D(rotation), scale);

    public void SetTransform2D(Vector2 position, float rotation, Vector2 scale) => SetTransform(new Vector3(position.X, position.Y, this.position.Z), Quaternion.FromRotation2D(rotation), new Vector3(scale.X, scale.Y, this.scale.Z));

    public void SetVar(string key, object? value) => SetVarByHash(new StringHash(StringHash.Calculate(key)), value);

    public void SetVarByHash(StringHash key, object? value) => variables[key] = value;

    public void SetVariables(IEnumerable<KeyValuePair<StringHash, object?>> variables)
    {
        this.variables.Clear();
        foreach (var item in variables)
            this.variables[item.Key] = item.Value;
    }

    public void SetWorldDirection(Vector3 direction)
    {
        if (Quaternion.LookRotation(direction, Vector3.Up, out var worldRotation))
            SetWorldRotation(worldRotation);
    }

    public void SetWorldPosition(Vector3 position)
    {
        SetPosition(Parent is null ? position : Parent.WorldToLocal(position));
    }

    public void SetWorldPosition2D(Vector2 position) => SetWorldPosition(new Vector3(position.X, position.Y, WorldPosition.Z));

    public void SetWorldPosition2D(float x, float y) => SetWorldPosition2D(new Vector2(x, y));

    public void SetWorldRotation(Quaternion rotation)
    {
        SetRotation(Parent is null ? rotation : Parent.WorldRotation.Inverse * rotation);
    }

    public void SetWorldRotation2D(float rotation) => SetWorldRotation(Quaternion.FromRotation2D(rotation));

    public void SetWorldScale(float scale) => SetWorldScale(new Vector3(scale, scale, scale));

    public void SetWorldScale(Vector3 scale)
    {
        SetScale(Parent is null ? scale : scale / Parent.WorldScale);
    }

    public void SetWorldScale2D(Vector2 scale) => SetWorldScale(new Vector3(scale.X, scale.Y, WorldScale.Z));

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

    private void AddChildren(List<Node> result, bool recursive)
    {
        foreach (var child in children)
        {
            result.Add(child);
            if (recursive)
                child.AddChildren(result, recursive: true);
        }
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
