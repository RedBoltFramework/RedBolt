namespace RedBolt.Scene;

using SystemMath = global::System.Math;

using RedBolt.Container;
using RedBolt.Core;
using RedBolt.IO;
using RedBolt.Input;
using RedBolt.Logging;
using RedBolt.Math;
using RedBolt.Resource;

/// <summary>Scene node that may contain components and child nodes.</summary>
[Reflected]
public partial class Node : Serializable
{
    private sealed class Vector3AttributeAccessor : AttributeAccessor
    {
        private readonly Func<Node, Vector3f> _getter;
        private readonly Action<Node, Vector3f> _setter;

        public Vector3AttributeAccessor(Func<Node, Vector3f> getter, Action<Node, Vector3f> setter)
        {
            _getter = getter;
            _setter = setter;
        }

        public override void Get(Serializable ptr, ref Variant dest)
        {
            dest = new Variant(_getter((Node)ptr));
        }

        public override void Set(Serializable ptr, Variant src)
        {
            _setter((Node)ptr, src.GetVector3());
        }
    }

    private Matrix3x4 _worldTransform = Matrix3x4.Identity;
    private bool _dirty;
    private bool _enabled = true;
    [Serialized("Is Enabled", Setter = nameof(SetEnabled))]
    private bool _enabledPrev = true;
    private Node? _parent;
    private Scene? _scene;
    private uint _id;
    private Vector3f _position = Vector3f.Zero;
    private Quaternion _rotation = Quaternion.IDENTITY;
    private Vector3f _scale = Vector3f.One;
    private Quaternion _worldRotation = Quaternion.IDENTITY;
    private readonly List<Component> _components = [];
    private readonly List<Node> _children = [];
    private readonly List<WeakReference<Component>> _listeners = [];
    // NodeImpl.
    private readonly List<Node> _dependencyNodes = [];
    [Serialized("Name", Setter = nameof(SetName))]
    private string _name = string.Empty;
    private readonly List<string> _tags = [];
    private StringHash _nameHash;
    private readonly VectorBuffer _attributeBuffer = new();
    private readonly Dictionary<StringHash, object?> _variables = new();
    private WorldOriginUpdateMode _worldOriginUpdateMode = WorldOriginUpdateMode.Move;

    /// <summary>Construct.</summary>
    public Node()
    {
        if (this is Scene)
            return;

    }

    /// <summary>Destruct. Any child nodes are detached.</summary>
    public override void Dispose()
    {
        RemoveAllChildren();
        RemoveAllComponents();

        if (_scene is not null)
            _scene.NodeRemoved(this);

        _variables.Clear();
        _attributeBuffer.Dispose();
        _tags.Clear();
        _name = string.Empty;
        _dependencyNodes.Clear();
        _listeners.Clear();
        base.Dispose();
    }

    /// <summary>For given set of components, return all nodes they belong to.</summary>
    public static IReadOnlyList<Node?> GetNodes(IEnumerable<Component> components)
    {
        var result = new List<Node?>();
        foreach (var component in components)
        {
            var node = component.Node;
            if (!result.Contains(node))
                result.Add(node);
        }
        return result;
    }

    /// <summary>For given set of nodes, exclude all children nodes and return all root nodes only.</summary>
    public static IReadOnlyList<Node> GetParentNodes(IEnumerable<Node> nodes)
    {
        var candidates = nodes.ToArray();
        var result = new List<Node>();
        foreach (var candidate in candidates)
        {
            var isChildOfAny = false;
            foreach (var node in candidates)
            {
                if (candidate.IsChildOf(node))
                {
                    isChildOfAny = true;
                    break;
                }
            }

            if (!isChildOfAny)
                result.Add(candidate);
        }
        return result;
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
                throw new ArchiveException("Failed to load node hierarchy from archive");
        }
        else
        {
            if (serializeTemporary)
                saveFlags |= PrefabSaveFlag.SaveTemporary;

            using var writer = new PrefabWriterToArchive(archive, blockName: null, saveFlags: saveFlags, archiveFlags: archiveFlags);
            if (!Save(writer))
                throw new ArchiveException("Failed to save node hierarchy to archive");
        }
    }

    public bool Load(PrefabReader reader, PrefabLoadFlag flags = PrefabLoadFlag.None)
    {
        ArgumentNullException.ThrowIfNull(reader);
        try
        {
            var nodePrefab = reader.ReadNode();
            if (nodePrefab is null)
                throw new ArchiveException("Failed to read node prefab");

            using var resolver = new SceneResolver();
            LoadInternal(nodePrefab, reader, resolver, flags);
            resolver.Resolve();
            if ((flags & PrefabLoadFlag.SkipApplyAttributes) == 0)
                ApplyAttributes();
            return true;
        }
        catch (ArchiveException exception)
        {
            Log.GetLogger().Error("{0}", exception.Message);
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
        catch (ArchiveException exception)
        {
            Log.GetLogger().Error("{0}", exception.Message);
            return false;
        }
    }

    /// <summary>Instantiate scene content from prefab. Return root node if successful.</summary>
    public Node? InstantiatePrefab(
        PrefabResource? prefabResource, Vector3f? position = null, Quaternion? rotation = null)
    {
        return prefabResource is null
            ? null
            : InstantiatePrefab(prefabResource.GetNodePrefab(), position, rotation);
    }

    /// <summary>Instantiate scene content from prefab. Return root node if successful.</summary>
    public Node? InstantiatePrefab(NodePrefab prefab, Vector3f? position = null, Quaternion? rotation = null)
    {
        var childNode = CreateChild();
        using var reader = new PrefabReaderFromMemory(prefab);
        if (!childNode.Load(reader, PrefabLoadFlag.None))
        {
            childNode.Remove();
            return null;
        }

        childNode.SetPosition(position ?? Vector3f.Zero);
        childNode.SetRotation(rotation ?? Quaternion.IDENTITY);
        return childNode;
    }

    public override bool Load(Deserializer source)
    {
        ArgumentNullException.ThrowIfNull(source);

        using var resolver = new SceneResolver();
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

    public override bool Save(Serializer destination)
    {
        ArgumentNullException.ThrowIfNull(destination);

        if (!destination.WriteUInt(Id))
            return false;

        if (!base.Save(destination))
            return false;

        destination.WriteVLE(checked((uint)GetNumPersistentComponents()));
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

        destination.WriteVLE(checked((uint)GetNumPersistentChildren()));
        foreach (var child in _children)
        {
            if (child.IsTemporary)
                continue;

            if (!child.Save(destination))
                return false;
        }

        return true;
    }

    public void GeneratePrefab(NodePrefab prefab)
    {
        var flags = PrefabSaveFlag.EnumsAsStrings | PrefabSaveFlag.Prefab;
        using var writer = new PrefabWriterToMemory(prefab, flags);
        Save(writer);
    }

    public NodePrefab GeneratePrefab()
    {
        var prefab = new NodePrefab();
        GeneratePrefab(prefab);
        return prefab;
    }

    public override bool LoadXML(XMLElement source)
    {
        using var resolver = new SceneResolver();
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
            var component = SafeCreateComponent(typeName, new StringHash(typeName), rewriteIds ? 0u : componentId);

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
        using var resolver = new SceneResolver();
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
            var component = SafeCreateComponent(typeName, new StringHash(typeName), rewriteIds ? 0u : componentId);

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

    /// <summary>Apply attribute changes that can not be applied immediately recursively to child nodes and components.</summary>
    public override void ApplyAttributes()
    {
        foreach (var component in _components)
            component.ApplyAttributes();

        foreach (var child in _children)
            child.ApplyAttributes();
    }

    /// <summary>Return whether should save default-valued attributes into XML.</summary>
    public override bool SaveDefaultAttributes(AttributeInfo attribute)
    {
        return true;
    }

    public bool SaveXML(Serializer destination, string indentation = "\t")
    {
        ArgumentNullException.ThrowIfNull(destination);
        var xml = new XMLFile();
        var root = xml.CreateRoot("node");
        if (!SaveXML(root))
            return false;
        return xml.Save(destination, indentation);
    }

    public bool SaveJSON(Serializer destination, string indentation = "\t")
    {
        ArgumentNullException.ThrowIfNull(destination);
        using var json = new JSONFile();
        if (!SaveJSON(json.Root))
            return false;
        return json.Save(destination, indentation);
    }

    public void LoadInternal(
        SerializablePrefab nodePrefab, PrefabReader reader, SceneResolver resolver, PrefabLoadFlag flags)
    {
        var discardIds = (flags & PrefabLoadFlag.DiscardIds) != 0;
        var loadAsTemporary = (flags & PrefabLoadFlag.LoadAsTemporary) != 0;

        if ((flags & PrefabLoadFlag.KeepExistingComponents) == 0)
            RemoveAllComponents();
        if ((flags & PrefabLoadFlag.KeepExistingChildren) == 0)
            RemoveAllChildren();

        if ((flags & PrefabLoadFlag.IgnoreRootAttributes) == 0)
            nodePrefab.Export(this, flags);

        var oldId = (uint)nodePrefab.Id;
        resolver.AddNode(oldId, this);

        var numComponents = reader.ReadNumComponents();
        for (var index = 0; index < numComponents; ++index)
        {
            var componentPrefab = reader.ReadComponent();
            if (componentPrefab is null)
                throw new ArchiveException("Failed to read component prefab");

            var oldComponentId = (uint)componentPrefab.Id;
            var component = SafeCreateComponent(
                componentPrefab.TypeName, componentPrefab.TypeNameHash, discardIds ? 0u : oldComponentId);

            resolver.AddComponent(oldComponentId, component);
            componentPrefab.Export(component, flags);
            if (loadAsTemporary)
                component.SetTemporary(true);
        }

        var numChildren = reader.ReadNumChildren();
        for (var index = 0; index < numChildren; ++index)
        {
            reader.BeginChild();
            var childPrefab = reader.ReadNode();
            if (childPrefab is null)
                throw new ArchiveException("Failed to read child prefab");

            var oldChildId = (uint)childPrefab.Id;
            var child = CreateChild(discardIds ? 0u : oldChildId);
            var childFlags = flags & ~PrefabLoadFlag.LoadAsTemporary & ~PrefabLoadFlag.IgnoreRootAttributes;
            child.LoadInternal(childPrefab, reader, resolver, childFlags);
            if (loadAsTemporary)
                child.SetTemporary(true);
            reader.EndChild();
        }
    }

    public void SaveInternal(PrefabWriter writer)
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

    /// <summary>Create component, allowing UnknownComponent if actual type is not supported.</summary>
    private Component SafeCreateComponent(string typeName, StringHash type, uint id)
    {
        if (!string.IsNullOrEmpty(Context.Instance.GetTypeName(type)))
        {
            if (CreateComponent(type, id) is { } component)
                return component;
        }

        Log.GetLogger().Warning("Component type {0} not known, creating UnknownComponent as placeholder", type);
        return CreateComponent<UnknownComponent>(id);
    }

    public bool Load(Deserializer source, SceneResolver resolver, bool loadChildren = true, bool rewriteIds = false)
    {
        RemoveAllChildren();
        RemoveAllComponents();

        if (!base.Load(source))
            return false;

        var numComponents = source.ReadVLE();
        for (var index = 0; index < checked((int)numComponents); ++index)
        {
            var componentBuffer = new VectorBuffer(source, source.ReadVLE());
            var componentType = componentBuffer.ReadStringHash();
            var componentId = componentBuffer.ReadUInt();
            var component = SafeCreateComponent(string.Empty, componentType, rewriteIds ? 0u : componentId);

            if (component is not null)
            {
                resolver.AddComponent(componentId, component);
                component.Load(componentBuffer);
            }
        }

        if (!loadChildren)
            return true;

        var numChildren = source.ReadVLE();
        for (var index = 0; index < checked((int)numChildren); ++index)
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
            result = (AttributeScopeHint)SystemMath.Max((int)result, (int)component.GetEffectiveScopeHint());
        foreach (var child in _children)
            result = (AttributeScopeHint)SystemMath.Max((int)result, (int)child.GetEffectiveScopeHint());
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
            var isNameGood = (flags & SceneLookupFlag.ValidateName) == 0 || childNode.NameHash == nameHash;
            var isRelationGood = (flags & SceneLookupFlag.ValidateRelation) == 0 || childNode.IsChildOf(this);
            if (isNameGood && isRelationGood)
            {
#if DEBUG
                if (childNode.NameHash != nameHash)
                    Log.GetLogger().Warning("Change of node name is ignored during lazy lookup");
                if (!childNode.IsChildOf(this))
                    Log.GetLogger().Warning("Change of node hierarchy is ignored during lazy lookup");
#endif
                return true;
            }

            childNode = null;
        }

        childNode = GetChild(nameHash, (flags & SceneLookupFlag.Recursive) != 0);
        return childNode is not null;
    }

    public bool GetNthComponentLazy<T>(ref T? childComponent, int index = 0)
        where T : Component
    {
        if (childComponent is not null)
            return true;

        childComponent = GetNthComponent<T>(index);
        return childComponent is not null;
    }
    public IReadOnlyList<Node> Children => _children;

    public IReadOnlyList<Component> Components => _components;

    [Serialized("Tags", DefaultSource = nameof(GetTagsAttributeDefault), Setter = nameof(SetTags))]
    public IReadOnlyList<string> Tags => _tags;

    [Serialized("Variables", DefaultSource = nameof(GetVariablesAttributeDefault), Setter = nameof(SetSerializedVariables))]
    public IReadOnlyDictionary<StringHash, object?> Variables => _variables;

    public uint Id => _id;

    public string Name => _name;

    public StringHash NameHash => _nameHash;

    public bool Enabled
    {
        get => _enabled;
        set => SetEnabled(value);
    }

    public bool IsEnabled => _enabled;

    public bool IsEnabledSelf => _enabledPrev;

    public bool IsDirty => _dirty;

    public Node? Parent => _parent;

    [Serialized("Position", AccessorFactory = nameof(CreatePositionAttributeAccessor))]
    public Vector3f Position
    {
        get => _position;
        set => SetPosition(value);
    }

    [Serialized("Rotation")]
    public Quaternion Rotation
    {
        get => _rotation;
        set => SetRotation(value);
    }

    [Serialized("Scale", AccessorFactory = nameof(CreateScaleAttributeAccessor))]
    public Vector3f Scale
    {
        get => _scale;
        set => SetScale(value);
    }

    public Scene? Scene => _scene;

    [Serialized("World Origin Update Mode")]
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
        var newNode = CreateChild(id, temporary);
        newNode.SetName(name);
        return newNode;
    }

    public Node CreateChild(uint id, bool temporary = false)
    {
        var newNode = new Node();
        newNode.SetTemporary(temporary);

        if (_scene is not null)
        {
            if (id == 0 || _scene.GetNode(id) is not null)
                id = _scene.GetFreeNodeID();
            newNode.SetID(id);
        }
        else
            newNode.SetID(id);

        AddChild(newNode);
        return newNode;
    }

    public Node CreateTemporaryChild(string name = "", uint id = 0)
    {
        return CreateChild(name, id, temporary: true);
    }

    public T CreateComponent<T>(uint id = 0)
        where T : Component, new()
    {
        var component = (T)Context.Instance.CreateObject(ObjectReflection<T>.TypeId);
        AddComponent(component, id);
        return component;
    }

    public Component? CreateComponent(StringHash type, uint id = 0)
    {
        if (Context.Instance.CreateObject(type) is not Component component)
        {
            Log.GetLogger().Error("Could not create unknown component type {0}", type);
            return null;
        }

        AddComponent(component, id);
        return component;
    }

    public Component? CreateComponent(Type type, uint id = 0)
    {
        ArgumentNullException.ThrowIfNull(type);
        if (type.IsAbstract || !typeof(Component).IsAssignableFrom(type))
            return null;

        return CreateComponent(new StringHash(type.GetClassName()), id);
    }

    public void AddComponent(Component component, uint id = 0)
    {
        ArgumentNullException.ThrowIfNull(component);

        _components.Add(component);

        if (component.TryGetNode() is not null)
            Log.GetLogger().Warning("Component {0} already belongs to a node!", component.GetTypeName());

        if (_scene is not null)
        {
            if (id == 0 || _scene.GetComponent(id) is not null)
                id = _scene.GetFreeComponentID();
            component.SetID(id);
            component.SetNode(this);
            _scene.ComponentAdded(component);
        }
        else
        {
            component.SetID(id);
            component.SetNode(this);
        }

        component.OnMarkedDirty(this);

        if (_scene is not null)
        {
            var eventData = new SceneEvents.ComponentAddedEvent
            {
                Scene = _scene,
                Node = this,
                Component = component
            };
            _scene.SendEvent(ref eventData);
        }
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
        if (component is null)
        {
            Log.GetLogger().Error("Null source component given for CloneComponent");
            return null;
        }

        var clone = SafeCreateComponent(component.GetTypeName(), component.GetTypeNameHash(), 0);
        if (clone is null)
        {
            Log.GetLogger().Error("Could not clone component {0}", component.GetTypeName());
            return null;
        }

        var componentAttributes = component.GetAttributes();
        var cloneAttributes = clone.GetAttributes();
        for (var index = 0; index < componentAttributes.Count && index < cloneAttributes.Count; ++index)
        {
            var attribute = componentAttributes[index];
            var cloneAttribute = cloneAttributes[index];
            if ((attribute.Mode & AttributeMode.File) != 0)
            {
                var value = default(Variant);
                try
                {
                    component.OnGetAttribute(attribute, ref value);
                    clone.OnSetAttribute(cloneAttribute, value);
                }
                finally
                {
                    value.Dispose();
                }
            }
        }
        clone.ApplyAttributes();

        if (_scene is not null)
        {
            var componentClonedEvent = new SceneEvents.ComponentClonedEvent
            {
                Scene = _scene,
                Component = component,
                CloneComponent = clone
            };
            _scene.SendEvent(ref componentClonedEvent);
        }
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

    public Component? GetComponent(int index)
    {
        return index >= 0 && index < _components.Count ? _components[index] : null;
    }

    public T? GetNthComponent<T>(int index)
        where T : Component
    {
        foreach (var component in _components)
        {
            if (component.GetType() != typeof(T) || component is not T typed)
                continue;

            if (index == 0)
                return typed;

            --index;
        }

        return null;
    }

    public Component? GetNthComponent(Type componentType, int index)
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

    public int GetNumComponents() => _components.Count;

    public int GetNumPersistentComponents()
    {
        var count = 0;
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

    public Scene? GetScene() => Scene;

    public string GetName() => Name;

    public StringHash GetNameHash() => NameHash;

    public IReadOnlyList<string> GetTags() => Tags;

    public int GetComponentIndex(Component component)
    {
        ArgumentNullException.ThrowIfNull(component);

        var index = _components.IndexOf(component);
        return index >= 0 ? index : int.MaxValue;
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

    public int GetChildIndex(Node child)
    {
        ArgumentNullException.ThrowIfNull(child);
        var index = _children.IndexOf(child);
        return index >= 0 ? index : int.MaxValue;
    }

    public int GetIndexInParent()
    {
        return Parent?.GetChildIndex(this) ?? int.MaxValue;
    }

    public int GetNumChildren(bool recursive = false)
    {
        if (!recursive)
            return _children.Count;

        var count = _children.Count;
        foreach (var child in _children)
            count += child.GetNumChildren(recursive: true);
        return count;
    }

    public int GetNumPersistentChildren()
    {
        var count = 0;
        foreach (var child in _children)
        {
            if (!child.IsTemporary)
                ++count;
        }

        return count;
    }

    public Node? GetChild(int index)
    {
        return index >= 0 && index < _children.Count ? _children[index] : null;
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

    private Node? GetChildByNameOrIndex(string name, bool recursive = false)
    {
        if (string.IsNullOrEmpty(name))
            return null;

        if (name[0] == '#')
        {
            if (int.TryParse(name.AsSpan(1), out var index) || (name.Length == 2 && name[1] == '0'))
                return GetChild(index);
        }

        return GetChild(new StringHash(name), recursive);
    }

    public Node? FindChild(string path, bool firstRecursive = false)
    {
        var separator = path.IndexOf('/');
        var isLast = separator < 0;
        var childName = isLast ? path : path[..separator];
        if (string.IsNullOrEmpty(childName))
            return null;

        var subPath = isLast ? string.Empty : path[(separator + 1)..];
        if (childName == "**")
            return FindChild(subPath, firstRecursive: true);

        var child = childName.StartsWith("#", StringComparison.Ordinal) && int.TryParse(childName[1..], out var index)
            ? GetChild(index)
            : GetChild(childName, firstRecursive);
        return child is not null && !isLast ? child.FindChild(subPath) : child;
    }

    public (Serializable? Serializable, int AttributeIndex) FindComponentAttribute(string path)
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
                return (serializable, i);
        }

        return default;
    }

    public bool IsChildOf(Node? node)
    {
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

    public bool IsTransformHierarchyRoot() => Parent is null || ReferenceEquals(Parent, Scene);

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

    public ref readonly Matrix3x4 GetWorldTransform()
    {
        if (_dirty)
            UpdateWorldTransform();

        return ref _worldTransform;
    }

    public Vector3f GetWorldUp() => WorldUp;

    public bool HasTag(string tag) => _tags.Contains(tag);

    public Vector3f LocalToWorld(Vector3f localPosition) => GetWorldTransform() * localPosition;

    public Vector3f LocalToWorld(Vector4f localVector) => GetWorldTransform() * localVector;

    public Vector2f LocalToWorld2D(Vector2f localVector) => LocalToWorld(new Vector3f(localVector.X, localVector.Y, 0.0f)).ToVector2();

    public Vector3f WorldToLocal(Vector3f worldPosition) => GetWorldTransform().Inverse * worldPosition;

    public Vector3f WorldToLocal(Vector4f worldVector) => GetWorldTransform().Inverse * worldVector;

    public Vector2f WorldToLocal2D(Vector2f worldVector) => WorldToLocal(new Vector3f(worldVector.X, worldVector.Y, 0.0f)).ToVector2();

    public T? FindComponent<T>(ComponentSearchFlag flags = ComponentSearchFlag.Default)
        where T : Component
    {
        return Find(this, flags);

        static T? Find(Node node, ComponentSearchFlag flags)
        {
            var includeDisabled = (flags & ComponentSearchFlag.EnabledOnly) == 0;
            var includeDerived = (flags & ComponentSearchFlag.Derived) != 0;

            if ((flags & ComponentSearchFlag.Self) != 0 && (includeDisabled || node.IsEnabled))
            {
                foreach (var component in node._components)
                {
                    if ((includeDisabled || component.Enabled)
                        && (includeDerived ? component is T : component.GetType() == typeof(T)))
                        return (T)component;
                }
            }

            if ((flags & ComponentSearchFlag.Parent) != 0 && node.Parent is { } parent)
            {
                var recursionFlag = flags & ~ComponentSearchFlag.ChildrenRecursive;
                recursionFlag |= ComponentSearchFlag.Self;
                if ((recursionFlag & ComponentSearchFlag.ParentRecursive) != ComponentSearchFlag.ParentRecursive)
                    recursionFlag &= ~ComponentSearchFlag.ParentRecursive;

                if (Find(parent, recursionFlag) is { } result)
                    return result;
            }

            if ((flags & ComponentSearchFlag.Children) != 0)
            {
                var recursionFlag = flags & ~ComponentSearchFlag.ParentRecursive;
                recursionFlag |= ComponentSearchFlag.Self;
                if ((recursionFlag & ComponentSearchFlag.ChildrenRecursive) != ComponentSearchFlag.ChildrenRecursive)
                    recursionFlag &= ~ComponentSearchFlag.ChildrenRecursive;

                foreach (var child in node._children)
                {
                    if (Find(child, recursionFlag) is { } result)
                        return result;
                }
            }

            return null;
        }
    }

    public Component? FindComponent(Type componentType, ComponentSearchFlag flags = ComponentSearchFlag.Default)
    {
        ValidateComponentType(componentType, nameof(componentType));
        return Find(this, componentType, flags);

        static Component? Find(Node node, Type componentType, ComponentSearchFlag flags)
        {
            var includeDisabled = (flags & ComponentSearchFlag.EnabledOnly) == 0;
            var includeDerived = (flags & ComponentSearchFlag.Derived) != 0;

            if ((flags & ComponentSearchFlag.Self) != 0 && (includeDisabled || node.IsEnabled))
            {
                foreach (var component in node._components)
                {
                    if ((includeDisabled || component.Enabled)
                        && (includeDerived ? componentType.IsInstanceOfType(component) : component.GetType() == componentType))
                        return component;
                }
            }

            if ((flags & ComponentSearchFlag.Parent) != 0 && node.Parent is { } parent)
            {
                var recursionFlag = flags & ~ComponentSearchFlag.ChildrenRecursive;
                recursionFlag |= ComponentSearchFlag.Self;
                if ((recursionFlag & ComponentSearchFlag.ParentRecursive) != ComponentSearchFlag.ParentRecursive)
                    recursionFlag &= ~ComponentSearchFlag.ParentRecursive;

                if (Find(parent, componentType, recursionFlag) is { } result)
                    return result;
            }

            if ((flags & ComponentSearchFlag.Children) != 0)
            {
                var recursionFlag = flags & ~ComponentSearchFlag.ParentRecursive;
                recursionFlag |= ComponentSearchFlag.Self;
                if ((recursionFlag & ComponentSearchFlag.ChildrenRecursive) != ComponentSearchFlag.ChildrenRecursive)
                    recursionFlag &= ~ComponentSearchFlag.ChildrenRecursive;

                foreach (var child in node._children)
                {
                    if (Find(child, componentType, recursionFlag) is { } result)
                        return result;
                }
            }

            return null;
        }
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
        var index = _components.IndexOf(component);
        if (index < 0)
            return;

        if (_scene is not null)
        {
            var eventData = new SceneEvents.ComponentRemovedEvent
            {
                Scene = _scene,
                Node = this,
                Component = component
            };
            _scene.SendEvent(ref eventData);
        }

        RemoveListener(component);
        if (_scene is not null)
            _scene.ComponentRemoved(component);
        component.SetNode(null);
        _components.RemoveAt(index);
    }

    public void ReorderComponent(Component component, int index)
    {
        ArgumentNullException.ThrowIfNull(component);
        var oldIndex = _components.IndexOf(component);
        if (oldIndex < 0 || index < 0 || index >= _components.Count)
            return;

        _components.RemoveAt(oldIndex);
        _components.Insert(SystemMath.Min(index, _components.Count), component);
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
        if (ReferenceEquals(this, _scene) || destination is null)
        {
            Log.GetLogger().Error("Can not clone node without a parent");
            return null;
        }

        using var resolver = new SceneResolver();
        var clone = CloneRecursive(destination, resolver);
        resolver.Resolve();
        clone.ApplyAttributes();
        return clone;
    }

    public void Remove()
    {
        Parent?.RemoveChild(this);
    }

    public void RemoveChild(Node node)
    {
        if (node is null)
            return;

        var index = _children.IndexOf(node);
        if (index < 0)
            return;

        RemoveChild(index);
    }

    private void RemoveChild(int index)
    {
        var node = _children[index];

        if (_scene is not null)
        {
            var eventData = new SceneEvents.NodeRemovedEvent
            {
                Scene = _scene,
                Parent = this,
                Node = node
            };
            _scene.SendEvent(ref eventData);
        }

        node._parent = null;
        node.MarkDirty();
        if (_scene is not null)
            _scene.NodeRemoved(node);

        _children.RemoveAt(index);
    }

    public void AddChild(Node node, int index = int.MaxValue)
    {
        if (node is null || ReferenceEquals(node, this) || ReferenceEquals(node._parent, this))
            return;
        if (IsChildOf(node))
            return;

        var oldParent = node._parent;
        if (oldParent is not null)
        {
            if (!ReferenceEquals(oldParent.GetScene(), _scene))
                oldParent.RemoveChild(node);
            else
            {
                if (_scene is not null)
                {
                    var removedEventData = new SceneEvents.NodeRemovedEvent
                    {
                        Scene = _scene,
                        Parent = oldParent,
                        Node = node
                    };
                    _scene.SendEvent(ref removedEventData);
                }

                oldParent._children.Remove(node);
            }
        }

        _children.Insert(SystemMath.Min(index, _children.Count), node);
        node._parent = this;

        if (_scene is not null && !ReferenceEquals(node.GetScene(), _scene))
            _scene.NodeAdded(node);
        node.MarkDirty();

        if (_scene is not null)
        {
            var eventData = new SceneEvents.NodeAddedEvent
            {
                Scene = _scene,
                Parent = this,
                Node = node
            };
            _scene.SendEvent(ref eventData);
        }
    }

    public void ReorderChild(Node child, int index)
    {
        ArgumentNullException.ThrowIfNull(child);
        if (!ReferenceEquals(child.Parent, this) || index < 0 || index >= _children.Count)
            return;

        _children.Remove(child);
        _children.Insert(SystemMath.Min(index, _children.Count), child);
    }

    public void RemoveAllChildren() => RemoveChildren(recursive: true);

    public void RemoveChildren(bool recursive)
    {
        for (var index = _children.Count - 1; index >= 0; --index)
        {
            var child = _children[index];
            if (recursive)
                child.RemoveChildren(recursive: true);
            RemoveChild(index);
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
        if (parent is not null)
        {
            var oldWorldTransform = GetWorldTransform();

            parent.AddChild(this);

            if (!ReferenceEquals(parent, _scene))
            {
                var newTransform = parent.GetWorldTransform().Inverse * oldWorldTransform;
                SetTransform(newTransform.Translation, newTransform.Rotation, newTransform.Scale);
            }
            else
                SetTransform(oldWorldTransform.Translation, oldWorldTransform.Rotation, oldWorldTransform.Scale);
        }
    }

    public void AddTag(string tag)
    {
        if (!string.IsNullOrEmpty(tag) && !_tags.Contains(tag))
        {
            _tags.Add(tag);
            if (_scene is not null)
            {
                _scene.NodeTagAdded(this, tag);

                var eventData = new SceneEvents.NodeTagAddedEvent
                {
                    Scene = _scene,
                    Node = this,
                    Tag = tag
                };
                _scene.SendEvent(ref eventData);
            }
        }
    }

    public void AddTags(string tags, char separator = ';')
    {
        AddTags(tags.Split(separator));
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
        var current = this;
        for (;;)
        {
            if (current._dirty)
                return;
            current._dirty = true;

            for (var index = 0; index < current._listeners.Count;)
            {
                if (current._listeners[index].TryGetTarget(out var listener))
                {
                    listener.OnMarkedDirty(current);
                    ++index;
                }
                else
                {
                    current._listeners[index] = current._listeners[^1];
                    current._listeners.RemoveAt(current._listeners.Count - 1);
                }
            }

            if (current._children.Count != 0)
            {
                for (var index = 1; index < current._children.Count; ++index)
                    current._children[index].MarkDirty();
                current = current._children[0];
            }
            else
                return;
        }
    }

    public void Pitch(float angle, TransformSpace space = TransformSpace.Local) => Rotate(new Quaternion(angle, Vector3f.Right), space);

    public void RemoveAllTags()
    {
        if (_scene is not null)
        {
            foreach (var tag in _tags)
            {
                _scene.NodeTagRemoved(this, tag);

                var eventData = new SceneEvents.NodeTagRemovedEvent
                {
                    Scene = _scene,
                    Node = this,
                    Tag = tag
                };
                _scene.SendEvent(ref eventData);
            }
        }

        _tags.Clear();
    }

    public bool RemoveTag(string tag)
    {
        if (!_tags.Remove(tag))
            return false;

        if (_scene is not null)
        {
            _scene.NodeTagRemoved(this, tag);
            var eventData = new SceneEvents.NodeTagRemovedEvent
            {
                Scene = _scene,
                Node = this,
                Tag = tag
            };
            _scene.SendEvent(ref eventData);
        }
        return true;
    }

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
                    SetRotation(_rotation * worldRotation.Inverse * delta * worldRotation);
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
            TransformSpace.World => _rotation * WorldRotation.Inverse * delta * WorldRotation,
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
        this._rotation = rotation;
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
        this._scale = scale;
        if (this._scale.X == 0.0f)
            this._scale.X = MathDefs.Epsilon;
        if (this._scale.Y == 0.0f)
            this._scale.Y = MathDefs.Epsilon;
        if (this._scale.Z == 0.0f)
            this._scale.Z = MathDefs.Epsilon;
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
        this._rotation = rotation;
        MarkDirty();
    }

    public void SetTransform(Vector3f position, Quaternion rotation, float scale) => SetTransform(position, rotation, new Vector3f(scale, scale, scale));

    public void SetTransform(Vector3f position, Quaternion rotation, Vector3f scale)
    {
        this._position = position;
        this._rotation = rotation;
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

    public void SetTransform2D(Vector2f position, float rotation, float scale) => SetTransform(new Vector3f(position.X, position.Y, 0.0f), new Quaternion(rotation), scale);

    public void SetTransform2D(Vector2f position, float rotation, Vector2f scale) => SetTransform(new Vector3f(position.X, position.Y, 0.0f), new Quaternion(rotation), new Vector3f(scale.X, scale.Y, 1.0f));

    public void SetVar(string key, object? value) => _variables[new StringHash(key)] = value;

    public void SetVarByHash(StringHash key, object? value)
    {
        if (_variables.ContainsKey(key))
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
        var localDirection = IsTransformHierarchyRoot() ? direction : Parent!.WorldRotation.Inverse * direction;
        SetRotation(new Quaternion(Vector3f.Forward, localDirection));
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

    public void SetWorldTransform2D(Vector2f position, float rotation, float scale) => SetWorldTransform(new Vector3f(position.X, position.Y, 0.0f), new Quaternion(rotation), scale);

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
                _position += IsTransformHierarchyRoot()
                    ? delta
                    : Parent!.GetWorldTransform().Inverse * new Vector4f(delta.X, delta.Y, delta.Z, 0.0f);
                MarkDirty();
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
        _nameHash = name;

        if (_scene is not null)
        {
            var eventData = new SceneEvents.NodeNameChangedEvent
            {
                Scene = _scene,
                Node = this
            };
            _scene.SendEvent(ref eventData);
        }
    }

    /// <summary>Set ID. Called by Scene.</summary>
    public void SetID(uint id)
    {
        _id = id;
    }

    /// <summary>Set scene. Called by Scene.</summary>
    public void SetScene(Scene? scene)
    {
        _scene = scene;
    }

    /// <summary>Reset scene and ID. Called by Scene.</summary>
    public void ResetScene()
    {
        SetID(0);
        SetScene(null);
    }

    private void SetEnabled(bool enable, bool recursive, bool storeSelf)
    {
        if (ReferenceEquals(this, _scene))
        {
            Log.GetLogger().Error("Can not change enabled state of the Scene");
            return;
        }

        if (storeSelf)
            _enabledPrev = enable;

        if (enable != _enabled)
        {
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

            if (_scene is not null)
            {
                var eventData = new SceneEvents.NodeEnabledChangedEvent
                {
                    Scene = _scene,
                    Node = this
                };
                _scene.SendEvent(ref eventData);
            }

            foreach (var component in _components)
            {
                component.OnSetEnabled();

                if (_scene is not null)
                {
                    var componentEventData = new SceneEvents.ComponentEnabledChangedEvent
                    {
                        Scene = _scene,
                        Node = this,
                        Component = component
                    };
                    _scene.SendEvent(ref componentEventData);
                }
            }
        }

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

    private Node CloneRecursive(Node parent, SceneResolver resolver)
    {
        var clone = parent.CreateChild(0);
        resolver.AddNode(_id, clone);

        var attributes = GetAttributes();
        for (var index = 0; index < attributes.Count; ++index)
        {
            var attribute = attributes[index];
            if ((attribute.Mode & AttributeMode.File) != 0)
            {
                var value = default(Variant);
                try
                {
                    OnGetAttribute(attribute, ref value);
                    clone.OnSetAttribute(attribute, value);
                }
                finally
                {
                    value.Dispose();
                }
            }
        }

        foreach (var component in _components)
        {
            if (component.IsTemporary)
                continue;

            var cloneComponent = clone.CloneComponent(component);
            if (cloneComponent is not null)
                resolver.AddComponent(component.Id, cloneComponent);
        }

        foreach (var child in _children)
        {
            if (child.IsTemporary)
                continue;

            child.CloneRecursive(clone, resolver);
        }

        if (_scene is not null)
        {
            var eventData = new SceneEvents.NodeClonedEvent
            {
                Scene = _scene,
                Node = this,
                CloneNode = clone
            };
            _scene.SendEvent(ref eventData);
        }
        return clone;
    }

    internal void FindComponents<T>(ComponentSearchFlag flags, List<T> result)
        where T : Component
    {
        if ((flags & ComponentSearchFlag.Self) != 0)
            AddMatchingComponents(this, flags, result);

        if ((flags & ComponentSearchFlag.Parent) != 0)
        {
            var recursionFlag = flags & ~ComponentSearchFlag.ChildrenRecursive;
            recursionFlag |= ComponentSearchFlag.Self;
            if ((recursionFlag & ComponentSearchFlag.ParentRecursive) != ComponentSearchFlag.ParentRecursive)
                recursionFlag &= ~ComponentSearchFlag.ParentRecursive;

            Parent?.FindComponents(recursionFlag, result);
        }

        if ((flags & ComponentSearchFlag.Children) != 0)
        {
            var recursionFlag = flags & ~ComponentSearchFlag.ParentRecursive;
            recursionFlag |= ComponentSearchFlag.Self;
            if ((recursionFlag & ComponentSearchFlag.ChildrenRecursive) != ComponentSearchFlag.ChildrenRecursive)
                recursionFlag &= ~ComponentSearchFlag.ChildrenRecursive;

            foreach (var child in _children)
                child.FindComponents(recursionFlag, result);
        }
    }

    internal void FindComponents(ComponentSearchFlag flags, Type componentType, List<Component> result)
    {
        if ((flags & ComponentSearchFlag.Self) != 0)
            AddMatchingComponents(this, flags, componentType, result);

        if ((flags & ComponentSearchFlag.Parent) != 0)
        {
            var recursionFlag = flags & ~ComponentSearchFlag.ChildrenRecursive;
            recursionFlag |= ComponentSearchFlag.Self;
            if ((recursionFlag & ComponentSearchFlag.ParentRecursive) != ComponentSearchFlag.ParentRecursive)
                recursionFlag &= ~ComponentSearchFlag.ParentRecursive;

            Parent?.FindComponents(recursionFlag, componentType, result);
        }

        if ((flags & ComponentSearchFlag.Children) != 0)
        {
            var recursionFlag = flags & ~ComponentSearchFlag.ParentRecursive;
            recursionFlag |= ComponentSearchFlag.Self;
            if ((recursionFlag & ComponentSearchFlag.ChildrenRecursive) != ComponentSearchFlag.ChildrenRecursive)
                recursionFlag &= ~ComponentSearchFlag.ChildrenRecursive;

            foreach (var child in _children)
                child.FindComponents(recursionFlag, componentType, result);
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
            if (matches && component is T typed && (!enabledOnly || component.Enabled))
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
            if (matches && (!enabledOnly || component.Enabled))
                result.Add(component);
        }
    }

    private Serializable? GetSerializableByName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return this;

        var index = 0;
        var separator = name.IndexOf('#');
        if (separator >= 0)
        {
            int.TryParse(name[(separator + 1)..], out index);
            name = name[..separator];
        }

        var type = new StringHash(name);
        foreach (var component in _components)
        {
            if (component.GetTypeNameHash() != type)
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

    private static object? GetTagsAttributeDefault() => Array.Empty<string>();

    private static AttributeAccessor CreatePositionAttributeAccessor() => new Vector3AttributeAccessor(self => self.Position, (self, value) => self.Position = value);

    private static AttributeAccessor CreateScaleAttributeAccessor() => new Vector3AttributeAccessor(self => self.Scale, (self, value) => self.Scale = value);


    private static object? GetVariablesAttributeDefault() => new Dictionary<StringHash, object?>();

    private void SetSerializedVariables(object? value)
    {
        Serializable self = this;
        ((Node)self).SetVariables(value is VariantMap variantMap
            ? variantMap.Select(static pair => new KeyValuePair<StringHash, object?>(pair.Key, pair.Value.GetObject()))
            : value is IEnumerable<KeyValuePair<StringHash, object?>> variablesValue ? variablesValue : []);
    }

}
