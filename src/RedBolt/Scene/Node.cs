namespace RedBolt;

public sealed class Node : Animatable
{
    private Matrix3x4 _worldTransform = Matrix3x4.Identity;
    private bool _dirty;
    private bool _enabled = true;
    private bool _enabledPrev = true;
    private Vector3f _position = Vector3f.Zero;
    private Quaternion _rotation = Quaternion.IDENTITY;
    private Vector3f _scale = Vector3f.One;
    private Quaternion _worldRotation = Quaternion.IDENTITY;
    private readonly List<Component> _components = [];
    private readonly List<Node> _children = [];
    private readonly List<WeakReference<Component>> _listeners = [];
    // NodeImpl.
    private readonly List<Node> _dependencyNodes = [];
    private string _name = string.Empty;
    private readonly List<string> _tags = [];
    private readonly Dictionary<StringHash, object?> _variables = new();
    private WorldOriginUpdateMode _worldOriginUpdateMode = WorldOriginUpdateMode.Move;

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
            typeof(Vector3f),
            self => ((Node)self).Position,
            (self, value) => ((Node)self).Position = value is Vector3f position ? position : Vector3f.Zero,
            Vector3f.Zero);
        RegisterAttribute(
            "Rotation",
            typeof(Quaternion),
            self => ((Node)self).Rotation,
            (self, value) => ((Node)self).Rotation = value is Quaternion rotation ? rotation : Quaternion.IDENTITY,
            Quaternion.IDENTITY);
        RegisterAttribute(
            "Scale",
            typeof(Vector3f),
            self => ((Node)self).Scale,
            (self, value) => ((Node)self).Scale = value is Vector3f scale ? scale : Vector3f.One,
            Vector3f.One);
        RegisterAttribute(
            "Variables",
            typeof(IReadOnlyDictionary<StringHash, object?>),
            self => ((Node)self).Variables,
            (self, value) => ((Node)self).SetVariables(value is VariantMap variantMap
                ? variantMap.Select(static pair => new KeyValuePair<StringHash, object?>(pair.Key, pair.Value.GetObject()))
                : value is IEnumerable<KeyValuePair<StringHash, object?>> variablesValue ? variablesValue : []),
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

    public override void SerializeInBlock(Archive archive)
    {
        var compactSave = !archive.IsHumanReadable;
        var saveFlags = compactSave ? PrefabSaveFlag.CompactAttributeNames : PrefabSaveFlag.EnumsAsStrings;
        SerializeInBlock(archive, false, saveFlags);
    }

    public void SerializeInBlock(
        Archive archive,
        bool serializeTemporary,
        PrefabSaveFlag saveFlags,
        PrefabLoadFlag loadFlags = PrefabLoadFlag.None)
    {
        var compactSave = !archive.IsHumanReadable;
        var archiveFlags = (compactSave ? PrefabArchiveFlag.CompactTypeNames : PrefabArchiveFlag.None)
            | (serializeTemporary ? PrefabArchiveFlag.SerializeTemporary : PrefabArchiveFlag.None);

        if (archive.IsInput)
        {
            using var reader = new PrefabReaderFromArchive(archive, blockName: null, flags: archiveFlags);
            if (!Load(reader, loadFlags))
                throw new InvalidOperationException("Failed to load node hierarchy from archive.");
        }
        else
        {
            if (serializeTemporary)
                saveFlags |= PrefabSaveFlag.SaveTemporary;

            using var writer = new PrefabWriterToArchive(archive, blockName: null, saveFlags: saveFlags, archiveFlags: archiveFlags);
            if (!Save(writer))
                throw new InvalidOperationException("Failed to save node hierarchy to archive.");
        }
    }

    public bool Load(PrefabReader reader, PrefabLoadFlag flags = PrefabLoadFlag.None)
    {
        ArgumentNullException.ThrowIfNull(reader);
        try
        {
            var nodePrefab = reader.ReadNode();
            if (nodePrefab is null)
                return false;

            var resolver = new SceneResolver();
            LoadInternal(nodePrefab, reader, resolver, flags);
            resolver.Resolve();
            if ((flags & PrefabLoadFlag.SkipApplyAttributes) == 0)
                ApplyAttributes();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public bool Save(PrefabWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        try
        {
            SaveInternal(writer);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public override bool Load(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);
        using var copy = new MemoryStream();
        source.CopyTo(copy);
        using var buffer = new MemoryBuffer(copy.ToArray());
        return Load(buffer);
    }

    public override bool Load(Deserializer source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var resolver = new SceneResolver();
        var nodeId = source.ReadUInt();
        resolver.AddNode(nodeId, this);

        var success = Load(source, resolver);
        if (success)
        {
            resolver.Resolve();
            ApplyAttributes();
        }

        return success;
    }

    public override bool Save(Stream destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        var buffer = new VectorBuffer();
        if (!Save(buffer)) return false;
        destination.Write(buffer.Buffer[..checked((int)buffer.Size)]);
        return true;
    }

    public override bool Save(Serializer destination)
    {
        ArgumentNullException.ThrowIfNull(destination);

        if (!destination.WriteUInt(Id))
            return false;

        if (!base.Save(destination))
            return false;

        destination.WriteVLE(GetNumPersistentComponents());
        foreach (var component in _components)
        {
            if (component.IsTemporary)
                continue;

            var componentBuffer = new VectorBuffer();
            if (!component.Save(componentBuffer))
                return false;

            destination.WriteVLE(componentBuffer.Size);
            destination.Write(componentBuffer.Buffer);
        }

        destination.WriteVLE(GetNumPersistentChildren());
        foreach (var child in _children)
        {
            if (child.IsTemporary)
                continue;

            if (!child.Save(destination))
                return false;
        }

        return true;
    }

    public NodePrefab GeneratePrefab(bool includeTemporary = false)
    {
        var prefab = new NodePrefab();
        FillSerializablePrefab(prefab.Node, this, Id);
        foreach (var component in _components.Where(component => includeTemporary || !component.IsTemporary))
        {
            var componentPrefab = new SerializablePrefab();
            FillSerializablePrefab(componentPrefab, component, component.Id);
            prefab.Components.Add(componentPrefab);
        }
        foreach (var child in _children.Where(child => includeTemporary || !child.IsTemporary))
            prefab.Children.Add(child.GeneratePrefab(includeTemporary));
        return prefab;
    }

    public void GeneratePrefab(NodePrefab prefab, bool includeTemporary = false)
    {
        ArgumentNullException.ThrowIfNull(prefab);

        var generated = GeneratePrefab(includeTemporary);
        prefab.Clear();
        prefab.Node.TypeName = generated.Node.TypeName;
        prefab.Node.Id = generated.Node.Id;
        prefab.Node.Temporary = generated.Node.Temporary;
        foreach (var attribute in generated.Node.Attributes)
            prefab.Node.Attributes.Add((AttributePrefab)attribute.Clone());
        foreach (var component in generated.Components)
            prefab.Components.Add((SerializablePrefab)component.Clone());
        foreach (var child in generated.Children)
            prefab.Children.Add((NodePrefab)child.Clone());
    }

    public void LoadPrefab(NodePrefab prefab, bool clearExisting = true)
    {
        ArgumentNullException.ThrowIfNull(prefab);
        if (clearExisting)
        {
            RemoveAllChildren();
            RemoveAllComponents();
        }
        ApplySerializablePrefab(this, prefab.Node);
        foreach (var componentPrefab in prefab.Components)
        {
            var type = ResolveComponentType(componentPrefab.TypeName);
            Component component;
            if (type is null || CreateComponent(type, componentPrefab.Id) is not { } created)
            {
                component = CreateComponent<UnknownComponent>(componentPrefab.Id);
                ((UnknownComponent)component).SetOriginalTypeName(componentPrefab.TypeName);
            }
            else component = created;
            ApplySerializablePrefab(component, componentPrefab);
        }
        foreach (var childPrefab in prefab.Children)
        {
            var child = CreateChild(id: childPrefab.Node.Id, temporary: childPrefab.Node.Temporary);
            child.LoadPrefab(childPrefab);
        }
        ApplyAttributes();
    }

    public override bool LoadXML(XMLElement source)
    {
        var resolver = new SceneResolver();
        resolver.AddNode(source.GetUInt("id"), this);

        var success = LoadXML(source, resolver);
        if (success)
        {
            resolver.Resolve();
            ApplyAttributes();
        }

        return success;
    }

    public bool LoadXML(
        XMLElement source,
        SceneResolver resolver,
        bool loadChildren = true,
        bool rewriteIds = false,
        bool removeComponents = true)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(resolver);

        RemoveAllChildren();
        if (removeComponents)
            RemoveAllComponents();

        if (!base.LoadXML(source))
            return false;

        for (var componentElement = source.GetChild("component"); componentElement.NotNull;
             componentElement = componentElement.GetNext("component"))
        {
            var typeName = componentElement.GetAttribute("type");
            var componentId = componentElement.GetUInt("id");
            var componentType = ResolveComponentType(typeName);
            if (componentType is null)
                continue;

            var component = CreateComponent(componentType, rewriteIds ? 0u : componentId);
            if (component is null)
                continue;

            resolver.AddComponent(componentId, component);
            if (!component.LoadXML(componentElement))
                return false;
        }

        if (!loadChildren)
            return true;

        for (var childElement = source.GetChild("node"); childElement.NotNull;
             childElement = childElement.GetNext("node"))
        {
            var nodeId = childElement.GetUInt("id");
            var child = CreateChild(rewriteIds ? 0u : nodeId);
            resolver.AddNode(nodeId, child);
            if (!child.LoadXML(childElement, resolver, loadChildren, rewriteIds, removeComponents))
                return false;
        }

        return true;
    }

    public override bool LoadJSON(JSONValue source)
    {
        var resolver = new SceneResolver();
        resolver.AddNode(source.Get("id").GetUInt(), this);

        var success = LoadJSON(source, resolver);
        if (success)
        {
            resolver.Resolve();
            ApplyAttributes();
        }

        return success;
    }

    public bool LoadJSON(
        JSONValue source,
        SceneResolver resolver,
        bool loadChildren = true,
        bool rewriteIds = false)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(resolver);

        RemoveAllChildren();
        RemoveAllComponents();

        if (!base.LoadJSON(source))
            return false;

        foreach (var componentValue in source.Get("components").GetArray())
        {
            var typeName = componentValue.Get("type").GetString();
            var componentId = componentValue.Get("id").GetUInt();
            var componentType = ResolveComponentType(typeName);
            if (componentType is null)
                continue;

            var component = CreateComponent(componentType, rewriteIds ? 0u : componentId);
            if (component is null)
                continue;

            resolver.AddComponent(componentId, component);
            if (!component.LoadJSON(componentValue))
                return false;
        }

        if (!loadChildren)
            return true;

        foreach (var childValue in source.Get("children").GetArray())
        {
            var nodeId = childValue.Get("id").GetUInt();
            var child = CreateChild(rewriteIds ? 0u : nodeId);
            resolver.AddNode(nodeId, child);
            if (!child.LoadJSON(childValue, resolver, loadChildren, rewriteIds))
                return false;
        }

        return true;
    }

    public override bool SaveJSON(JSONValue destination)
    {
        ArgumentNullException.ThrowIfNull(destination);

        destination.Set("id", new JSONValue(Id));
        if (!base.SaveJSON(destination))
            return false;

        var components = new JSONValue(JSONValueType.Array);
        foreach (var component in _components)
        {
            if (component.IsTemporary)
                continue;

            var componentValue = new JSONValue();
            if (!component.SaveJSON(componentValue))
                return false;
            components.Push(componentValue);
        }
        destination.Set("components", components);

        var children = new JSONValue(JSONValueType.Array);
        foreach (var child in _children)
        {
            if (child.IsTemporary)
                continue;

            var childValue = new JSONValue();
            if (!child.SaveJSON(childValue))
                return false;
            children.Push(childValue);
        }
        destination.Set("children", children);

        return true;
    }

    public override bool SaveXML(XMLElement destination)
    {
        ArgumentNullException.ThrowIfNull(destination);

        if (!destination.SetUInt("id", Id) || !base.SaveXML(destination))
            return false;

        foreach (var component in _components)
        {
            if (component.IsTemporary)
                continue;

            var componentElement = destination.CreateChild("component");
            if (!component.SaveXML(componentElement))
                return false;
        }

        foreach (var child in _children)
        {
            if (child.IsTemporary)
                continue;

            var childElement = destination.CreateChild("node");
            if (!child.SaveXML(childElement))
                return false;
        }

        return true;
    }

    public bool SaveXML(Stream destination, string indentation = "\t")
    {
        ArgumentNullException.ThrowIfNull(destination);

        var xml = new XMLFile(string.Empty);
        var root = xml.CreateRoot("node");
        if (!SaveXML(root))
            return false;

        return xml.Save(destination, indentation);
    }

    public bool SaveXML(Serializer destination, string indentation = "\t")
    {
        ArgumentNullException.ThrowIfNull(destination);
        using var stream = new MemoryStream();
        if (!SaveXML(stream, indentation))
            return false;

        var data = stream.ToArray();
        if (destination.Write(data) != data.Length)
            return false;

        return true;
    }

    public bool SaveJSON(Stream destination, string indentation = "\t")
    {
        ArgumentNullException.ThrowIfNull(destination);

        var root = new JSONValue();
        if (!SaveJSON(root))
            return false;

        var json = new JSONFile(string.Empty);
        if (!json.FromString(root.ToString()))
            return false;

        return json.Save(destination, indentation);
    }

    public bool SaveJSON(Serializer destination, string indentation = "\t")
    {
        ArgumentNullException.ThrowIfNull(destination);
        using var stream = new MemoryStream();
        if (!SaveJSON(stream, indentation))
            return false;

        var data = stream.ToArray();
        if (destination.Write(data) != data.Length)
            return false;

        return true;
    }

    private static void FillSerializablePrefab(SerializablePrefab prefab, Serializable value, uint id)
    {
        prefab.TypeName = value.GetTypeName(); prefab.Id = id; prefab.Temporary = value.IsTemporary;
        foreach (var attribute in value.Attributes.Where(static attribute => attribute.ShouldSave))
        {
            Variant variant;
            if (value is Node node && attribute.Name == "Variables")
            {
                var map = new VariantMap();
                foreach (var (key, item) in node._variables) map[key] = Variant.FromObject(item);
                variant = new Variant(map);
            }
            else variant = Variant.FromObject(value.GetAttribute(attribute.Name));
            prefab.Attributes.Add(new AttributePrefab { Name = attribute.Name, Value = variant });
        }
    }

    private static void ApplySerializablePrefab(
        Serializable value, SerializablePrefab prefab, PrefabLoadFlag flags = PrefabLoadFlag.None)
    {
        if ((flags & PrefabLoadFlag.KeepTemporaryState) == 0)
            value.SetTemporary(prefab.Temporary);
        foreach (var attributePrefab in prefab.Attributes)
        {
            if (value is Node node
                && (attributePrefab.Name == "Variables" || attributePrefab.NameHash == new StringHash("Variables")))
            {
                node.SetVariables(attributePrefab.Value.GetVariantMap().Select(static pair => new KeyValuePair<StringHash, object?>(pair.Key, pair.Value.GetObject())));
                continue;
            }
            AttributeInfo? attribute = null;
            foreach (var candidate in value.Attributes)
            {
                if (candidate.Name == attributePrefab.Name || candidate.NameHash == attributePrefab.NameHash)
                {
                    attribute = candidate;
                    break;
                }
            }

            if (attribute is null) continue;
            object? converted = attribute.ValueType.IsEnum ? Enum.ToObject(attribute.ValueType, attributePrefab.Value.GetInt()) : attributePrefab.Value.GetObject();
            if (attribute.ValueType == typeof(uint)) converted = attributePrefab.Value.GetUInt();
            value.SetAttribute(attribute.Name, converted);
        }
        if ((flags & PrefabLoadFlag.SkipApplyAttributes) == 0)
            value.ApplyAttributes();
    }

    private void LoadInternal(
        SerializablePrefab nodePrefab, PrefabReader reader, SceneResolver resolver, PrefabLoadFlag flags)
    {
        var discardIds = (flags & PrefabLoadFlag.DiscardIds) != 0;
        var loadAsTemporary = (flags & PrefabLoadFlag.LoadAsTemporary) != 0;

        if ((flags & PrefabLoadFlag.KeepExistingComponents) == 0)
            RemoveAllComponents();
        if ((flags & PrefabLoadFlag.KeepExistingChildren) == 0)
            RemoveAllChildren();

        if ((flags & PrefabLoadFlag.IgnoreRootAttributes) == 0)
            ApplySerializablePrefab(this, nodePrefab, flags);

        resolver.AddNode(nodePrefab.Id, this);

        var numComponents = reader.ReadNumComponents();
        for (var index = 0u; index < numComponents; ++index)
        {
            var componentPrefab = reader.ReadComponent();
            if (componentPrefab is null)
                throw new InvalidOperationException("Failed to read component prefab.");

            var componentId = (flags & PrefabLoadFlag.DiscardIds) != 0 ? 0u : componentPrefab.Id;
            var type = ResolveComponentType(componentPrefab.TypeName);
            Component component;
            if (type is null || CreateComponent(type, componentId) is not { } created)
            {
                component = CreateComponent<UnknownComponent>(componentId);
                ((UnknownComponent)component).SetOriginalTypeName(componentPrefab.TypeName);
            }
            else
                component = created;

            resolver.AddComponent(componentPrefab.Id, component);
            ApplySerializablePrefab(component, componentPrefab, flags);
            if (loadAsTemporary)
                component.SetTemporary(true);
        }

        var numChildren = reader.ReadNumChildren();
        for (var index = 0u; index < numChildren; ++index)
        {
            reader.BeginChild();
            var childPrefab = reader.ReadNode();
            if (childPrefab is null)
                throw new InvalidOperationException("Failed to read child prefab.");

            var childId = (flags & PrefabLoadFlag.DiscardIds) != 0 ? 0u : childPrefab.Id;
            var child = CreateChild(childId);
            var childFlags = flags & ~PrefabLoadFlag.LoadAsTemporary & ~PrefabLoadFlag.IgnoreRootAttributes;
            child.LoadInternal(childPrefab, reader, resolver, childFlags);
            if (loadAsTemporary)
                child.SetTemporary(true);
            reader.EndChild();
        }
    }

    private void SaveInternal(PrefabWriter writer)
    {
        var saveTemporary = (writer.GetFlags() & PrefabSaveFlag.SaveTemporary) != 0;

        writer.WriteNode(Id, this);
        writer.WriteNumComponents(saveTemporary ? GetNumComponents() : GetNumPersistentComponents());
        foreach (var component in _components)
        {
            if (saveTemporary || !component.IsTemporary)
                writer.WriteComponent(component.Id, component);
        }

        writer.WriteNumChildren(saveTemporary ? GetNumChildren() : GetNumPersistentChildren());
        foreach (var child in _children)
        {
            if (saveTemporary || !child.IsTemporary)
            {
                writer.BeginChild();
                child.SaveInternal(writer);
                writer.EndChild();
            }
        }
    }

    private static Type? ResolveComponentType(string typeName)
    {
        foreach (var type in typeof(Component).Assembly.GetTypes())
        {
            if (!type.IsAbstract && typeof(Component).IsAssignableFrom(type)
                && (type.Name == typeName || type.FullName == typeName || type.GetClassName() == typeName))
                return type;
        }

        return null;
    }

    private static Type? ResolveComponentType(StringHash typeHash)
    {
        foreach (var type in typeof(Component).Assembly.GetTypes())
        {
            if (!type.IsAbstract && typeof(Component).IsAssignableFrom(type)
                && new StringHash(type.GetClassName()) == typeHash)
                return type;
        }

        return null;
    }

    internal bool Load(Deserializer source, SceneResolver resolver, bool loadChildren = true, bool rewriteIds = false)
    {
        RemoveAllChildren();
        RemoveAllComponents();

        if (!base.Load(source))
            return false;

        var numComponents = source.ReadVLE();
        for (var index = 0u; index < numComponents; ++index)
        {
            var componentBuffer = new VectorBuffer(source, source.ReadVLE());
            var componentType = componentBuffer.ReadStringHash();
            var componentId = componentBuffer.ReadUInt();
            var component = ResolveComponentType(componentType) is { } type
                ? CreateComponent(type, rewriteIds ? 0u : componentId)
                : CreateComponent<UnknownComponent>(rewriteIds ? 0u : componentId);

            if (component is not null)
            {
                resolver.AddComponent(componentId, component);
                component.Load(componentBuffer);
            }
        }

        if (!loadChildren)
            return true;

        var numChildren = source.ReadVLE();
        for (var index = 0u; index < numChildren; ++index)
        {
            var nodeId = source.ReadUInt();
            var child = CreateChild(rewriteIds ? 0u : nodeId);
            resolver.AddNode(nodeId, child);
            if (!child.Load(source, resolver, loadChildren, rewriteIds))
                return false;
        }

        return true;
    }

    public AttributeScopeHint GetEffectiveScopeHint()
    {
        var result = AttributeScopeHint.Serializable;
        foreach (var component in _components)
            result = (AttributeScopeHint)Math.Max((int)result, (int)component.GetEffectiveScopeHint());
        foreach (var child in _children)
            result = (AttributeScopeHint)Math.Max((int)result, (int)child.GetEffectiveScopeHint());
        return result;
    }

    // Node* Node::InstantiatePrefab(const PrefabResource* prefabResource, const Vector3& position, const Quaternion& rotation);
    // Node* Node::InstantiatePrefab(const NodePrefab& prefab, const Vector3& position, const Quaternion& rotation);
    //
    //
    // bool Node::GetChildLazy(WeakPtr<Node>& childNode, StringHash nameHash, SceneLookupFlags flags = SceneLookupFlag::None) const;
    // template <class T> bool Node::GetNthComponentLazy(WeakPtr<T>& childComponent, unsigned index = 0) const;
    //
    public void TraverseDepthFirst(Func<Node, bool> nodeCallback, Action<Component> componentCallback)
    {
        ArgumentNullException.ThrowIfNull(nodeCallback);
        ArgumentNullException.ThrowIfNull(componentCallback);

        foreach (var component in _components)
            componentCallback(component);

        foreach (var child in _children)
        {
            if (nodeCallback(child))
                child.TraverseDepthFirst(nodeCallback, componentCallback);
        }
    }

    public bool GetChildLazy(ref Node? childNode, StringHash nameHash, SceneLookupFlag flags = SceneLookupFlag.None)
    {
        if (childNode is not null)
        {
            var isNameGood = !flags.HasFlag(SceneLookupFlag.ValidateName) || childNode.NameHash == nameHash;
            var isRelationGood = !flags.HasFlag(SceneLookupFlag.ValidateRelation) || childNode.IsChildOf(this);
            if (isNameGood && isRelationGood)
                return true;

            childNode = null;
        }

        childNode = GetChild(nameHash, flags.HasFlag(SceneLookupFlag.Recursive));
        return childNode is not null;
    }

    public bool GetNthComponentLazy<T>(ref T? childComponent, uint index = 0)
        where T : Component
    {
        if (childComponent is not null)
            return true;

        childComponent = GetNthComponent<T>(index);
        return childComponent is not null;
    }
    //
    // C++ Node constructor takes Context* only; C# takes scene/parent/name/id/temporary.
    // C++ uses PIMPL via NodeImpl (name_, tags_, nameHash_, dependencyNodes_, attrBuffer_);
    // C# inlines those fields directly.
    //
    // Scene lifetime helpers - the managed Scene is currently constructor-owned and non-nullable,
    // so these mutators do not yet map onto the C# API surface:
    //
    // void Node::SetScene(Scene* scene) { scene_ = scene; }
    // void Node::ResetScene() { SetID(0); SetScene(nullptr); }

    public IReadOnlyList<Node> Children => _children;

    public IReadOnlyList<Component> Components => _components;

    public IReadOnlyList<string> Tags => _tags;

    public IReadOnlyDictionary<StringHash, object?> Variables => _variables;

    public IReadOnlyDictionary<StringHash, object?> Vars => Variables;

    public uint Id { get; private set; }

    public string Name => _name;

    public StringHash NameHash => Name;

    public bool Enabled
    {
        get => _enabled;
        set => SetEnabled(value);
    }

    public bool IsEnabled => _enabled;

    public bool IsEnabledSelf => _enabledPrev;

    public bool IsDirty => _dirty;

    public Node? Parent { get; private set; }

    public Vector3f Position
    {
        get => _position;
        set => SetPosition(value);
    }

    public Quaternion Rotation
    {
        get => _rotation;
        set => SetRotation(value);
    }

    public Vector3f Scale
    {
        get => _scale;
        set => SetScale(value);
    }

    public Scene Scene { get; }

    public WorldOriginUpdateMode WorldOriginUpdateMode
    {
        get => _worldOriginUpdateMode;
        set => _worldOriginUpdateMode = value;
    }

    public void SetWorldOriginUpdateMode(WorldOriginUpdateMode mode) => _worldOriginUpdateMode = mode;

    public WorldOriginUpdateMode GetWorldOriginUpdateMode() => _worldOriginUpdateMode;

    public Vector3f Direction => _rotation * Vector3f.Forward;

    public Vector3f Right => _rotation * Vector3f.Right;

    public Vector3f Up => _rotation * Vector3f.Up;

    public Vector3f WorldPosition
    {
        get
        {
            if (_dirty)
                UpdateWorldTransform();

            return _worldTransform.Translation;
        }
    }

    public Quaternion WorldRotation
    {
        get
        {
            if (_dirty)
                UpdateWorldTransform();

            return _worldRotation;
        }
    }

    public Vector3f WorldDirection
    {
        get
        {
            if (_dirty)
                UpdateWorldTransform();

            return _worldRotation * Vector3f.Forward;
        }
    }

    public Vector3f WorldUp
    {
        get
        {
            if (_dirty)
                UpdateWorldTransform();

            return _worldRotation * Vector3f.Up;
        }
    }

    public Vector3f WorldRight
    {
        get
        {
            if (_dirty)
                UpdateWorldTransform();

            return _worldRotation * Vector3f.Right;
        }
    }

    public Vector3f WorldScale
    {
        get
        {
            if (_dirty)
                UpdateWorldTransform();

            return _worldTransform.Scale;
        }
    }

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

        _components.Add(component);
        component.SetId(Scene.AllocateComponentId(id));
        component.Attach(this);
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
        _components.Add(clone);
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
        foreach (var component in _components)
        {
            if (component.GetType() == typeof(T) && component is T typed)
                return typed;
        }

        return null;
    }

    public Component? GetComponent(Type componentType)
    {
        ValidateComponentType(componentType, nameof(componentType));

        foreach (var component in _components)
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
        foreach (var component in _components)
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
        foreach (var component in _components)
        {
            if (component.GetType() == componentType)
                result.Add(component);
        }

        return result;
    }

    public Component? GetComponent(uint index)
    {
        return index < _components.Count ? _components[(int)index] : null;
    }

    public T? GetNthComponent<T>(uint index)
        where T : Component
    {
        var current = 0u;
        foreach (var component in _components)
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

        foreach (var component in _components)
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
        foreach (var component in _components)
        {
            if (component is T typed)
                return typed;
        }

        return null;
    }

    public Component? GetDerivedComponent(Type componentType)
    {
        ValidateComponentType(componentType, nameof(componentType));

        foreach (var component in _components)
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

    public uint GetNumComponents() => (uint)_components.Count;

    public uint GetNumPersistentComponents()
    {
        uint count = 0;
        foreach (var component in _components)
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

        var index = _components.IndexOf(component);
        return index >= 0 ? (uint)index : uint.MaxValue;
    }

    public uint GetID()
    {
        return Id;
    }

    public string GetFullNameDebug()
    {
        var prefix = Parent is not null ? $"{Parent.GetFullNameDebug()}/[{Parent._children.IndexOf(this)}]" : string.Empty;
        return prefix + (string.IsNullOrEmpty(Name) ? nameof(Node) : Name);
    }

    public uint GetChildIndex(Node child)
    {
        ArgumentNullException.ThrowIfNull(child);
        var index = _children.IndexOf(child);
        return index >= 0 ? (uint)index : uint.MaxValue;
    }

    public uint GetIndexInParent()
    {
        return Parent?.GetChildIndex(this) ?? uint.MaxValue;
    }

    public uint GetNumChildren(bool recursive = false)
    {
        if (!recursive)
            return (uint)_children.Count;

        uint count = (uint)_children.Count;
        foreach (var child in _children)
            count += child.GetNumChildren(recursive: true);
        return count;
    }

    public uint GetNumPersistentChildren()
    {
        uint count = 0;
        foreach (var child in _children)
        {
            if (!child.IsTemporary)
                ++count;
        }

        return count;
    }

    public Node? GetChild(uint index)
    {
        return index < _children.Count ? _children[(int)index] : null;
    }

    public Node? GetChild(StringHash nameHash, bool recursive = false)
    {
        foreach (var child in _children)
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
        var dest = new List<Node>();
        GetChildren(dest, recursive);
        return dest;
    }

    public void GetChildren(List<Node> dest, bool recursive = false)
    {
        dest.Clear();

        if (!recursive)
        {
            foreach (var child in _children)
                dest.Add(child);
        }
        else
            GetChildrenRecursive(dest);
    }

    public IReadOnlyList<Node> GetChildrenWithTag(string tag, bool recursive = false)
    {
        var dest = new List<Node>();
        GetChildrenWithTag(dest, tag, recursive);
        return dest;
    }

    public void GetChildrenWithTag(List<Node> dest, string tag, bool recursive = false)
    {
        dest.Clear();

        if (!recursive)
        {
            foreach (var child in _children)
            {
                if (child.HasTag(tag))
                    dest.Add(child);
            }
        }
        else
            GetChildrenWithTagRecursive(dest, tag);
    }

    public IReadOnlyList<Node> GetChildrenWithComponent<T>(bool recursive = false)
        where T : Component
    {
        return GetChildrenWithComponent(typeof(T), recursive);
    }

    public void GetChildrenWithComponent<T>(List<Node> dest, bool recursive = false)
        where T : Component
    {
        GetChildrenWithComponent(dest, typeof(T), recursive);
    }

    public IReadOnlyList<Node> GetChildrenWithComponent(Type componentType, bool recursive = false)
    {
        var dest = new List<Node>();
        GetChildrenWithComponent(dest, componentType, recursive);
        return dest;
    }

    public void GetChildrenWithComponent(List<Node> dest, Type componentType, bool recursive = false)
    {
        ValidateComponentType(componentType, nameof(componentType));

        dest.Clear();

        if (!recursive)
        {
            foreach (var child in _children)
            {
                if (child.HasComponent(componentType))
                    dest.Add(child);
            }
        }
        else
            GetChildrenWithComponentRecursive(dest, componentType);
    }

    public Node? GetChildByNameOrIndex(string name, bool recursive = false)
    {
        if (string.IsNullOrEmpty(name))
            return null;

        if (name[0] == '#')
        {
            if (uint.TryParse(name.AsSpan(1), out var index) || (name.Length == 2 && name[1] == '0'))
                return GetChild(index);
        }

        return GetChild(new StringHash(name), recursive);
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

    public Vector3f GetPosition() => Position;

    public Vector2f GetPosition2D() => new(_position.X, _position.Y);

    public Quaternion GetRotation() => Rotation;

    public float GetRotation2D() => _rotation.RollAngle;

    public Vector3f GetDirection() => Direction;

    public Vector3f GetRight() => Right;

    public Vector3f GetScale() => Scale;

    public Vector2f GetScale2D() => new(_scale.X, _scale.Y);

    public Transform GetTransform() => new(_position, _rotation, _scale);

    public Matrix3x4 GetTransformMatrix() => new(_position, _rotation, _scale);

    public Vector3f GetUp() => Up;

    public Vector3f GetWorldDirection() => WorldDirection;

    public Vector3f GetWorldPosition() => WorldPosition;

    public Vector2f GetWorldPosition2D() => new(WorldPosition.X, WorldPosition.Y);

    public Quaternion GetWorldRotation() => WorldRotation;

    public float GetWorldRotation2D() => WorldRotation.RollAngle;

    public Vector3f GetWorldRight() => WorldRight;

    public Vector3f GetWorldScale() => WorldScale;

    public Vector3f GetSignedWorldScale()
    {
        if (_dirty)
            UpdateWorldTransform();

        return _worldTransform.SignedScale(_worldRotation.RotationMatrix);
    }

    public Vector2f GetWorldScale2D() => new(WorldScale.X, WorldScale.Y);

    public Matrix3x4 GetWorldTransform()
    {
        if (_dirty)
            UpdateWorldTransform();

        return _worldTransform;
    }

    public Vector3f GetWorldUp() => WorldUp;

    public bool HasTag(string tag) => _tags.Contains(tag);

    public bool HasVar(string key) => _variables.ContainsKey(new StringHash(key));

    public bool HasVar(StringHash key) => _variables.ContainsKey(key);

    public Vector3f LocalToWorld(Vector3f localPosition) => GetWorldTransform() * localPosition;

    public Vector3f LocalToWorld(Vector4f localVector) => GetWorldTransform() * localVector;

    public Vector2f LocalToWorld2D(Vector2f localVector) => LocalToWorld(new Vector3f(localVector.X, localVector.Y, 0.0f)).ToVector2();

    public Vector3f WorldToLocal(Vector3f worldPosition) => WorldRotation.Inverse * (worldPosition - WorldPosition) / WorldScale;

    public Vector3f WorldToLocal(Vector4f worldVector) => WorldRotation.Inverse * (new Vector3f(worldVector.X, worldVector.Y, worldVector.Z) - WorldPosition * worldVector.W) / WorldScale;

    public Vector2f WorldToLocal2D(Vector2f worldVector) => WorldToLocal(new Vector3f(worldVector.X, worldVector.Y, 0.0f)).ToVector2();

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
        for (var index = _components.Count - 1; index >= 0; --index)
        {
            if (_components[index].GetType() == typeof(T) && _components[index] is T component)
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

        for (var index = _components.Count - 1; index >= 0; --index)
        {
            if (_components[index].GetType() == componentType)
                RemoveComponent(_components[index]);
        }
    }

    public void RemoveComponent(Component component)
    {
        ArgumentNullException.ThrowIfNull(component);

        if (_components.Remove(component))
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
        var oldIndex = _components.IndexOf(component);
        if (oldIndex < 0 || index >= _components.Count)
            return;

        _components.RemoveAt(oldIndex);
        _components.Insert((int)Math.Min(index, (uint)_components.Count), component);
    }

    public void RemoveComponents()
    {
        for (var index = _components.Count - 1; index >= 0; --index)
            RemoveComponent(_components[index]);
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

        if (!ReferenceEquals(node.Parent, this) || !_children.Remove(node))
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
        var insertIndex = index >= _children.Count ? _children.Count : (int)index;
        _children.Insert(insertIndex, node);
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
        if (!ReferenceEquals(child.Parent, this) || index >= _children.Count)
            return;

        _children.Remove(child);
        _children.Insert((int)Math.Min(index, (uint)_children.Count), child);
    }

    public void RemoveAllChildren() => RemoveChildren(recursive: true);

    public void RemoveChildren(bool recursive)
    {
        for (var index = _children.Count - 1; index >= 0; --index)
        {
            var child = _children[index];
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
        SetEnabled(_enabledPrev, recursive: false, storeSelf: false);

        foreach (var child in _children)
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
        parent._children.Add(this);
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
        if (!string.IsNullOrEmpty(tag) && !_tags.Contains(tag))
        {
            _tags.Add(tag);
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
        return _variables.GetValueOrDefault(key);
    }

    public object? GetMutableVar(string key)
    {
        return GetMutableVarByHash(new StringHash(key));
    }

    public object? GetMutableVarByHash(StringHash key)
    {
        return _variables.GetValueOrDefault(key);
    }

    public IReadOnlyDictionary<StringHash, object?> GetVars() => Variables;

    public IReadOnlyList<Node> GetDependencyNodes() => _dependencyNodes;

    public IReadOnlyList<Component> GetListeners()
    {
        var listeners = new List<Component>(_listeners.Count);
        for (var i = 0; i < _listeners.Count; ++i)
        {
            if (_listeners[i].TryGetTarget(out var listener))
                listeners.Add(listener);
        }
        return listeners;
    }

    public void AddListener(Component? component)
    {
        if (component is null)
            return;

        for (var i = 0; i < _listeners.Count; ++i)
        {
            if (_listeners[i].TryGetTarget(out var listener) && ReferenceEquals(listener, component))
                return;
        }

        _listeners.Add(new WeakReference<Component>(component));
        if (_dirty)
            component.OnMarkedDirty(this);
    }

    public void RemoveListener(Component? component)
    {
        if (component is null)
            return;

        for (var i = 0; i < _listeners.Count; ++i)
        {
            if (_listeners[i].TryGetTarget(out var listener) && ReferenceEquals(listener, component))
            {
                _listeners.RemoveAt(i);
                return;
            }
        }
    }

    public bool LookAt(Vector3f target, Vector3f? up = null, TransformSpace space = TransformSpace.World)
    {
        var worldTarget = space switch
        {
            TransformSpace.Local => LocalToWorld(target),
            TransformSpace.Parent => IsTransformHierarchyRoot() ? target : Parent!.LocalToWorld(target),
            _ => target
        };

        var direction = worldTarget - WorldPosition;
        // Check if target is very close, in that case can not reliably calculate lookat direction
        if (direction.ApproximatelyEquivalent(Vector3f.Zero))
            return false;

        var newRotation = new Quaternion();
        // Do nothing if setting look rotation failed
        if (!newRotation.FromLookRotation(direction, up ?? Vector3f.Up))
            return false;

        SetWorldRotation(newRotation);
        return true;
    }

    public void MarkDirty()
    {
        if (_dirty)
            return;

        _dirty = true;

        for (var i = 0; i < _listeners.Count;)
        {
            if (_listeners[i].TryGetTarget(out var listener))
            {
                listener.OnMarkedDirty(this);
                ++i;
            }
            else
            {
                _listeners[i] = _listeners[^1];
                _listeners.RemoveAt(_listeners.Count - 1);
            }
        }

        foreach (var child in _children)
            child.MarkDirty();
    }

    public void Pitch(float angle, TransformSpace space = TransformSpace.Local) => Rotate(new Quaternion(angle, Vector3f.Right), space);

    public void RemoveAllTags()
    {
        for (var index = _tags.Count - 1; index >= 0; --index)
            RemoveTag(_tags[index]);
    }

    public bool RemoveTag(string tag)
    {
        if (!_tags.Remove(tag))
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

    public bool RemoveVar(string key) => _variables.Remove(new StringHash(key));

    public bool RemoveVarByHash(StringHash key) => _variables.Remove(key);

    public void Roll(float angle, TransformSpace space = TransformSpace.Local) => Rotate(new Quaternion(angle, Vector3f.Forward), space);

    public void Rotate(Quaternion delta, TransformSpace space = TransformSpace.Local)
    {
        switch (space)
        {
            case TransformSpace.Local:
                SetRotation((_rotation * delta).Normalized);
                break;
            case TransformSpace.Parent:
                SetRotation((delta * _rotation).Normalized);
                break;
            case TransformSpace.World:
                if (IsTransformHierarchyRoot())
                    SetRotation((delta * _rotation).Normalized);
                else
                {
                    var worldRotation = WorldRotation;
                    SetRotation((_rotation * worldRotation.Inverse * delta * worldRotation).Normalized);
                }
                break;
        }
    }

    public void Rotate2D(float delta, TransformSpace space = TransformSpace.Local) => Rotate(new Quaternion(delta), space);

    public void RotateAround(Vector3f point, Quaternion delta, TransformSpace space = TransformSpace.Local)
    {
        var parentSpacePoint = space switch
        {
            TransformSpace.Local => GetTransformMatrix() * point,
            TransformSpace.Parent => point,
            TransformSpace.World => IsTransformHierarchyRoot() ? point : Parent!.WorldToLocal(point),
            _ => point
        };
        var oldRotation = _rotation;

        _rotation = space switch
        {
            TransformSpace.Local => (_rotation * delta).Normalized,
            TransformSpace.Parent => (delta * _rotation).Normalized,
            TransformSpace.World when IsTransformHierarchyRoot() => (delta * _rotation).Normalized,
            TransformSpace.World => (_rotation * WorldRotation.Inverse * delta * WorldRotation).Normalized,
            _ => _rotation
        };

        var oldRelativePosition = oldRotation.Inverse * (_position - parentSpacePoint);
        _position = _rotation * oldRelativePosition + parentSpacePoint;
        MarkDirty();
    }

    public void RotateAround2D(Vector2f point, float delta, TransformSpace space = TransformSpace.Local) => RotateAround(new Vector3f(point.X, point.Y, 0.0f), new Quaternion(delta), space);

    public void SetDirection(Vector3f direction)
    {
        SetRotation(new Quaternion(Vector3f.Forward, direction));
    }

    public void SetPosition(Vector3f position)
    {
        this._position = position;
        MarkDirty();
    }

    public void SetPositionSilent(Vector3f position)
    {
        this._position = position;
    }

    public void SetPosition2D(Vector2f position) => SetPosition(new Vector3f(position.X, position.Y, 0.0f));

    public void SetPosition2D(float x, float y) => SetPosition2D(new Vector2f(x, y));

    public void SetRotation(Quaternion rotation)
    {
        this._rotation = rotation.Normalized;
        MarkDirty();
    }

    public void SetRotationSilent(Quaternion rotation)
    {
        this._rotation = rotation;
    }

    public void SetRotation2D(float rotation) => SetRotation(new Quaternion(rotation));

    public void SetScale(float scale) => SetScale(new Vector3f(scale, scale, scale));

    public void SetScale(Vector3f scale)
    {
        this._scale = new Vector3f(
            Math.Abs(scale.X) <= MathDefs.Epsilon ? MathDefs.Epsilon : scale.X,
            Math.Abs(scale.Y) <= MathDefs.Epsilon ? MathDefs.Epsilon : scale.Y,
            Math.Abs(scale.Z) <= MathDefs.Epsilon ? MathDefs.Epsilon : scale.Z);
        MarkDirty();
    }

    public void SetScaleSilent(Vector3f scale)
    {
        this._scale = scale;
    }

    public void SetScale2D(Vector2f scale) => SetScale(new Vector3f(scale.X, scale.Y, 1.0f));

    public void SetScale2D(float x, float y) => SetScale2D(new Vector2f(x, y));

    public void ScaleBy(float scale) => ScaleBy(new Vector3f(scale, scale, scale));

    public void ScaleBy(Vector3f scale)
    {
        this._scale *= scale;
        MarkDirty();
    }

    public void ScaleBy2D(Vector2f scale) => ScaleBy(new Vector3f(scale.X, scale.Y, 1.0f));

    public void ScaleAround(Vector3f point, Vector3f scale, TransformSpace space = TransformSpace.Local)
    {
        var parentSpacePoint = space switch
        {
            TransformSpace.Local => GetTransformMatrix() * point,
            TransformSpace.Parent => point,
            TransformSpace.World => IsTransformHierarchyRoot() ? point : Parent!.WorldToLocal(point),
            _ => point
        };
        var oldScale = this._scale;

        this._scale *= scale;
        var oldRelativePosition = Vector3f.One / oldScale * (_position - parentSpacePoint);
        _position = this._scale * oldRelativePosition + parentSpacePoint;
        MarkDirty();
    }

    public void ScaleAround2D(Vector2f point, Vector2f scale, TransformSpace space = TransformSpace.Local) => ScaleAround(new Vector3f(point.X, point.Y, 0.0f), new Vector3f(scale.X, scale.Y, 1.0f), space);

    public void SetTags(IEnumerable<string> tags)
    {
        RemoveAllTags();
        AddTags(tags);
    }

    public void SetTransform(Vector3f position, Quaternion rotation)
    {
        this._position = position;
        this._rotation = rotation.Normalized;
        MarkDirty();
    }

    public void SetTransform(Vector3f position, Quaternion rotation, float scale) => SetTransform(position, rotation, new Vector3f(scale, scale, scale));

    public void SetTransform(Vector3f position, Quaternion rotation, Vector3f scale)
    {
        this._position = position;
        this._rotation = rotation.Normalized;
        this._scale = scale;
        MarkDirty();
    }

    public void SetTransformSilent(Vector3f position, Quaternion rotation, Vector3f scale)
    {
        this._position = position;
        this._rotation = rotation;
        this._scale = scale;
    }

    public void SetTransformMatrix(Matrix3x4 matrix) => SetTransform(matrix.Translation, matrix.Rotation, matrix.Scale);

    public void SetTransformMatrixSilent(Matrix3x4 matrix) => SetTransformSilent(matrix.Translation, matrix.Rotation, matrix.Scale);

    public void SetTransform(in Transform transform) => SetTransform(transform.Position, transform.Rotation, transform.Scale);

    public void SetTransform2D(Vector2f position, float rotation) => SetTransform(new Vector3f(position.X, position.Y, 0.0f), new Quaternion(rotation));

    public void SetTransform2D(Vector2f position, float rotation, float scale) => SetTransform(new Vector3f(position.X, position.Y, 0.0f), new Quaternion(rotation), new Vector3f(scale, scale, 1.0f));

    public void SetTransform2D(Vector2f position, float rotation, Vector2f scale) => SetTransform(new Vector3f(position.X, position.Y, 0.0f), new Quaternion(rotation), new Vector3f(scale.X, scale.Y, 1.0f));

    public void SetVar(string key, object? value) => _variables[new StringHash(key)] = value;

    public void SetVarByHash(StringHash key, object? value)
    {
        _variables[key] = value;
    }

    public void SetVariables(IEnumerable<KeyValuePair<StringHash, object?>> variables)
    {
        this._variables.Clear();
        foreach (var item in variables)
            this._variables[item.Key] = item.Value;
    }

    public void SetWorldDirection(Vector3f direction)
    {
        if (direction.LengthSquared > 0.0f)
            SetWorldRotation(new Quaternion(Vector3f.Forward, direction.NormalizedOrDefault()));
    }

    public void SetWorldPosition(Vector3f position)
    {
        SetPosition(IsTransformHierarchyRoot() ? position : Parent!.WorldToLocal(position));
    }

    public void SetWorldPosition2D(Vector2f position) => SetWorldPosition(new Vector3f(position.X, position.Y, 0.0f));

    public void SetWorldPosition2D(float x, float y) => SetWorldPosition2D(new Vector2f(x, y));

    public void SetWorldRotation(Quaternion rotation)
    {
        SetRotation(IsTransformHierarchyRoot() ? rotation : Parent!.WorldRotation.Inverse * rotation);
    }

    public void SetWorldRotation2D(float rotation) => SetWorldRotation(new Quaternion(rotation));

    public void SetWorldScale(float scale) => SetWorldScale(new Vector3f(scale, scale, scale));

    public void SetWorldScale(Vector3f scale)
    {
        SetScale(IsTransformHierarchyRoot() ? scale : scale / Parent!.WorldScale);
    }

    public void SetWorldScale2D(Vector2f scale) => SetWorldScale(new Vector3f(scale.X, scale.Y, 1.0f));

    public void SetWorldScale2D(float x, float y) => SetWorldScale2D(new Vector2f(x, y));

    public void SetWorldTransform(Vector3f position, Quaternion rotation)
    {
        SetWorldPosition(position);
        SetWorldRotation(rotation);
    }

    public void SetWorldTransform(Vector3f position, Quaternion rotation, float scale)
    {
        SetWorldPosition(position);
        SetWorldRotation(rotation);
        SetWorldScale(scale);
    }

    public void SetWorldTransform(Vector3f position, Quaternion rotation, Vector3f scale)
    {
        SetWorldPosition(position);
        SetWorldRotation(rotation);
        SetWorldScale(scale);
    }

    public void SetWorldTransform(Matrix3x4 worldTransform) => SetWorldTransform(worldTransform.Translation, worldTransform.Rotation, worldTransform.Scale);

    public void SetWorldTransform2D(Vector2f position, float rotation) => SetWorldTransform(new Vector3f(position.X, position.Y, 0.0f), new Quaternion(rotation));

    public void SetWorldTransform2D(Vector2f position, float rotation, float scale) => SetWorldTransform(new Vector3f(position.X, position.Y, 0.0f), new Quaternion(rotation), new Vector3f(scale, scale, 1.0f));

    public void SetWorldTransform2D(Vector2f position, float rotation, Vector2f scale) => SetWorldTransform(new Vector3f(position.X, position.Y, 0.0f), new Quaternion(rotation), new Vector3f(scale.X, scale.Y, 1.0f));

    public void Translate(Vector3f delta, TransformSpace space = TransformSpace.Local)
    {
        switch (space)
        {
            case TransformSpace.Local:
                SetPosition(_position + _rotation * delta);
                break;
            case TransformSpace.Parent:
                SetPosition(_position + delta);
                break;
            case TransformSpace.World:
                SetWorldPosition(WorldPosition + delta);
                break;
        }
    }

    public void Translate2D(Vector2f delta, TransformSpace space = TransformSpace.Local) => Translate(new Vector3f(delta.X, delta.Y, 0.0f), space);

    public void Yaw(float angle, TransformSpace space = TransformSpace.Local) => Rotate(new Quaternion(angle, Vector3f.Up), space);

    public void SetName(string name)
    {
        name ??= string.Empty;
        if (this._name == name)
            return;

        this._name = name;
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
            _enabledPrev = enable;

        if (enable == _enabled)
        {
            if (recursive)
            {
                foreach (var child in _children)
                    child.SetEnabled(enable, recursive: true, storeSelf: storeSelf);
            }

            return;
        }

        _enabled = enable;

        for (var i = 0; i < _listeners.Count;)
        {
            if (_listeners[i].TryGetTarget(out var listener))
            {
                listener.OnNodeSetEnabled(this);
                ++i;
            }
            else
                _listeners.RemoveAt(i);
        }

        foreach (var component in _components)
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
            foreach (var child in _children)
                child.SetEnabled(enable, recursive: true, storeSelf: storeSelf);
        }
    }

    private void GetChildrenRecursive(List<Node> dest)
    {
        foreach (var node in _children)
        {
            dest.Add(node);
            if (node._children.Count != 0)
                node.GetChildrenRecursive(dest);
        }
    }

    private void GetChildrenWithComponentRecursive(List<Node> dest, Type componentType)
    {
        foreach (var node in _children)
        {
            if (node.HasComponent(componentType))
                dest.Add(node);
            if (node._children.Count != 0)
                node.GetChildrenWithComponentRecursive(dest, componentType);
        }
    }

    private void GetChildrenWithTagRecursive(List<Node> dest, string tag)
    {
        foreach (var node in _children)
        {
            if (node.HasTag(tag))
                dest.Add(node);
            if (node._children.Count != 0)
                node.GetChildrenWithTagRecursive(dest, tag);
        }
    }

    private Node CloneRecursive(Node parent)
    {
        var clone = parent.CreateChild(_name, temporary: Temporary);
        clone.CopyAttributes(this);
        clone.SetTemporary(Temporary);

        foreach (var component in _components)
        {
            if (!component.IsTemporary)
                clone.CloneComponent(component);
        }

        foreach (var child in _children)
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
            foreach (var child in _children)
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
            foreach (var child in _children)
                child.FindComponents(childFlags | ComponentSearchFlag.Self, componentType, result);
        }
    }

    private static void AddMatchingComponents<T>(Node node, ComponentSearchFlag flags, List<T> result)
        where T : Component
    {
        var enabledOnly = (flags & ComponentSearchFlag.EnabledOnly) != 0;
        if (enabledOnly && !node.IsEnabled)
            return;

        foreach (var component in node._components)
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

        foreach (var component in node._components)
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

        foreach (var component in _components)
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

    private void UpdateWorldTransform()
    {
        var transform = GetTransformMatrix();

        // Assume the root node (scene) has identity transform.
        if (IsTransformHierarchyRoot())
        {
            _worldTransform = transform;
            _worldRotation = _rotation;
        }
        else
        {
            _worldTransform = Parent!.GetWorldTransform() * transform;
            _worldRotation = Parent!.WorldRotation * _rotation;
        }

        _dirty = false;
    }

    private void DetachChildForReparent(Node child, Node newParent)
    {
        if (!_children.Remove(child))
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
