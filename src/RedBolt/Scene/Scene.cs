using System.Buffers;

namespace RedBolt;

/// <summary>Asynchronous scene loading mode.</summary>
public enum LoadMode
{
    /// <summary>Preload resources without loading scene content.</summary>
    ResourcesOnly,
    /// <summary>Load scene content without preloading resources.</summary>
    Scene,
    /// <summary>Preload resources, then load scene content.</summary>
    SceneAndResources
}

public sealed class Scene : Object
{
    private readonly Dictionary<uint, Component> _components = [];
    private readonly Dictionary<uint, Node> _nodes = [];
    private readonly Dictionary<string, List<Node>> _taggedNodes = new(StringComparer.Ordinal);
    private readonly List<(StringHash Type, HashSet<Component> Components)> _componentIndexes = [];
    private readonly List<PackageFile> _requiredPackageFiles = [];
    private readonly List<ISceneUpdateListener> _updateListeners = [];
    private readonly List<Component> _delayedDirtyComponents = [];
    private readonly List<StaticModel> _staticModels = [];
    private readonly List<string> _lightmapNames = [];
    private readonly List<Texture2D?> _lightmapTextures = [];
    private readonly StringVector _updateEvents = [];
    private readonly List<(StringHash EventType, bool IsForced)> _cookedUpdateEvents = [];
    private int _asyncLoadingMs = 5;
    private LoadMode _asyncLoadMode = LoadMode.SceneAndResources;
    private bool _asyncLoading;
    private float _elapsedTime;
    private string _fileName = string.Empty;
    private IntVector3 _worldOrigin;
    private uint _nextComponentId = 1;
    private uint _nextNodeId = 1;
    private bool _manualUpdate;
    private bool _threadedUpdate;

    public static StringVector DefaultUpdateEvents { get; } =
    [
        "@SceneForcedUpdate",
        "SceneUpdate",
        "AttributeAnimationUpdate",
        "SceneSubsystemUpdate",
        "ScenePostUpdate",
        "@SceneForcedPostUpdate"
    ];

    public Scene()
    {
        Root = new Node(this, parent: null, name: string.Empty, AllocateNodeId(0));
        NodeAdded(Root);
        SetUpdateEvents(DefaultUpdateEvents);
    }

    public override void Dispose()
    {
        Root.RemoveAllComponents();
        Root.RemoveAllChildren();
        base.Dispose();
    }

    public Node Root { get; }

    internal IReadOnlyList<StaticModel> StaticModels => _staticModels;

    public bool UpdateEnabled { get; set; } = true;

    public float TimeScale { get; set; } = 1.0f;

    public float EffectiveTimeScale => UpdateEnabled ? TimeScale : 0.0f;

    public float ElapsedTime => _elapsedTime;

    public int AsyncLoadingMs => _asyncLoadingMs;

    public string FileName => _fileName;

    public uint Checksum { get; private set; }

    public void SetManualUpdate(bool enabled)
    {
        _manualUpdate = enabled;
    }

    public uint GetFreeNodeID() => AllocateNodeId(0);

    public uint GetFreeComponentID() => AllocateComponentId(0);

    public void AddRequiredPackageFile(PackageFile? package)
    {
        if (package is null || package.GetNumFiles() == 0)
            return;

        _requiredPackageFiles.Add(package);
    }

    public void ClearRequiredPackageFiles()
    {
        _requiredPackageFiles.Clear();
    }

    public IReadOnlyList<PackageFile> GetRequiredPackageFiles() => _requiredPackageFiles;

    public bool CreateComponentIndex(StringHash componentType)
    {
        if (!IsEmpty())
            return false;

        _componentIndexes.Add((componentType, []));
        return true;
    }

    public void CreateComponentIndex<T>()
        where T : Component
    {
        CreateComponentIndex(typeof(T).GetClassName());
    }

    public IReadOnlyCollection<Component> GetComponentIndex(StringHash componentType)
    {
        var index = GetMutableComponentIndex(componentType);
        return index is not null ? index : Array.Empty<Component>();
    }

    public IReadOnlyCollection<Component> GetComponentIndex<T>()
        where T : Component
    {
        return GetComponentIndex(typeof(T).GetClassName());
    }

    /// <summary>Serialize object. May throw archive exception.</summary>
    public override void SerializeInBlock(Archive archive)
    {
        var compactSave = !archive.IsHumanReadable;
        var saveFlags = compactSave ? PrefabSaveFlag.CompactAttributeNames : PrefabSaveFlag.EnumsAsStrings;
        SerializeInBlock(archive, false, saveFlags, PrefabLoadFlag.None);
    }

    public void SerializeInBlock(
        Archive archive,
        bool serializeTemporary,
        PrefabSaveFlag saveFlags,
        PrefabLoadFlag loadFlags)
    {
        Root.SerializeInBlock(archive, serializeTemporary, saveFlags, loadFlags | PrefabLoadFlag.SkipApplyAttributes);
        SerializeAuxiliaryData(archive);

        if (archive.IsInput)
            Root.ApplyAttributes();

        if (!string.IsNullOrEmpty(archive.Name))
            _fileName = archive.Name;
        if (archive.Checksum != 0)
            Checksum = archive.Checksum;
    }

    public bool LoadXML(XMLElement source)
    {
        StopAsyncLoading();

        if (source.Name == SceneResource.GetXmlRootName())
        {
            var archive = new XMLInputArchive(Context, source.Element!);
            using (archive.OpenUnorderedBlock(SceneResource.GetXmlRootName()))
                SerializeInBlock(archive, false, PrefabSaveFlag.None, PrefabLoadFlag.None);
            return true;
        }

        if (Root.LoadXML(source))
            return true;

        throw new InvalidOperationException("Cannot load Scene from legacy XML format");
    }

    public bool LoadXML(Stream source)
    {
        StopAsyncLoading();

        var xml = new XMLFile(string.Empty);
        if (!xml.Load(source))
            return false;

        Clear();
        return LoadXML(xml.GetRoot());
    }

    public bool LoadJSON(JSONValue source)
    {
        StopAsyncLoading();
        return Root.LoadJSON(source);
    }

    public bool LoadJSON(Stream source)
    {
        StopAsyncLoading();

        var json = new JSONFile(string.Empty);
        if (!json.Load(source))
            return false;

        Clear();
        return LoadJSON(json.GetRoot());
    }

    public bool LoadXML(Deserializer source)
    {
        ArgumentNullException.ThrowIfNull(source);
        StopAsyncLoading();
        var xml = new XMLFile(string.Empty);
        if (!xml.Load(new MemoryStream(ReadRemaining(source))))
            return false;

        Clear();
        return LoadXML(xml.GetRoot());
    }

    public bool LoadJSON(Deserializer source)
    {
        ArgumentNullException.ThrowIfNull(source);
        StopAsyncLoading();
        var json = new JSONFile(string.Empty);
        if (!json.Load(new MemoryStream(ReadRemaining(source))))
            return false;

        Clear();
        if (Root.LoadJSON(json.GetRoot()))
        {
            FinishLoading(source);
            return true;
        }

        return false;
    }

    public bool Load(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);
        using var copy = new MemoryStream();
        source.CopyTo(copy);
        var data = copy.ToArray();
        var firstIndex = 0;
        while (firstIndex < data.Length
            && (data[firstIndex] == (byte)' ' || data[firstIndex] == (byte)'\t'
                || data[firstIndex] == (byte)'\r' || data[firstIndex] == (byte)'\n'))
            ++firstIndex;

        if (firstIndex < data.Length && data[firstIndex] == (byte)'<')
            return LoadXML(new MemoryStream(data));
        if (firstIndex < data.Length && (data[firstIndex] == (byte)'{' || data[firstIndex] == (byte)'['))
            return LoadJSON(new MemoryStream(data));

        using var buffer = new MemoryBuffer(data);
        return Load(buffer);
    }

    public bool Load(Deserializer source)
    {
        ArgumentNullException.ThrowIfNull(source);
        StopAsyncLoading();
        if (source.ReadFileID() != "USCN")
            return false;

        Clear();
        if (Root.Load(source))
        {
            FinishLoading(source);
            return true;
        }

        return false;
    }

    public bool Save(Stream destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        var buffer = new VectorBuffer();
        if (!Save(buffer)) return false;
        destination.Write(buffer.Buffer[..checked((int)buffer.Size)]);
        return true;
    }

    public bool Save(Serializer destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (!destination.WriteFileID("USCN"))
            return false;

        if (Root.Save(destination))
        {
            FinishSaving(destination);
            return true;
        }

        return false;
    }

    public bool SaveXML(Stream destination, string indentation = "\t")
    {
        ArgumentNullException.ThrowIfNull(destination);
        var xml = new XMLFile(string.Empty);
        if (!Root.SaveXML(xml.CreateRoot("scene")))
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

        FinishSaving(destination);
        return true;
    }

    public bool SaveJSON(Stream destination, string indentation = "\t")
    {
        ArgumentNullException.ThrowIfNull(destination);
        var root = new JSONValue();
        if (!Root.SaveJSON(root))
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

        FinishSaving(destination);
        return true;
    }

    public void StopAsyncLoading()
    {
        _asyncLoading = false;
        // TODO: port asyncProgress_.file_.Reset(), xmlFile_.Reset(), jsonFile_.Reset(),
        // xmlElement_ = XMLElement::EMPTY, jsonIndex_ = 0, resources_.clear(), and resolver_.Reset().
    }

    public void SetUpdateEvents(StringVector events)
    {
        _updateEvents.Clear();
        _cookedUpdateEvents.Clear();

        foreach (var @event in events)
        {
            _updateEvents.Add(@event);
            if (string.IsNullOrEmpty(@event))
                continue;

            var isForced = @event[0] == '@';
            var eventName = isForced ? @event[1..] : @event;
            _cookedUpdateEvents.Add((new StringHash(eventName), isForced));
        }
    }

    public StringVector GetUpdateEvents() => new(_updateEvents);

    public uint GetNumLightmaps() => (uint)_lightmapNames.Count;

    public void ResetLightmaps()
    {
        _lightmapNames.Clear();
        _lightmapTextures.Clear();
    }

    public void AddLightmap(string lightmapTextureName)
    {
        _lightmapNames.Add(lightmapTextureName);
        var cache = GetSubsystem<ResourceCache>()!;
        _lightmapTextures.Add(cache.GetResourceOrNull<Texture2D>(lightmapTextureName));
    }

    public void ReloadLightmaps()
    {
        var cache = GetSubsystem<ResourceCache>()!;
        _lightmapTextures.Clear();
        foreach (var lightmapTextureName in _lightmapNames)
            _lightmapTextures.Add(cache.GetResourceOrNull<Texture2D>(lightmapTextureName));
    }

    public Texture2D? GetLightmapTexture(uint index)
    {
        return index < (uint)_lightmapTextures.Count ? _lightmapTextures[(int)index] : null;
    }

    public Node CreateChild(string name = "", uint id = 0, bool temporary = false)
    {
        return Root.CreateChild(name, id, temporary);
    }

    public T CreateComponent<T>(uint id = 0)
        where T : Component, new()
    {
        return Root.CreateComponent<T>(id);
    }

    public void Clear()
    {
        Root.RemoveAllChildren();
        Root.RemoveAllComponents();
    }

    public void SetUpdateEnabled(bool enable)
    {
        if (UpdateEnabled == enable)
            return;

        UpdateEnabled = enable;

        var eventData = new SceneEvents.SceneUpdatesChangedEvent
        {
            Scene = this,
            UpdateEnabled = enable
        };
        SendEvent(ref eventData);
    }

    public bool IsUpdateEnabled() => UpdateEnabled;

    public void SetTimeScale(float scale)
    {
        TimeScale = Math.Max(scale, MathDefs.Epsilon);
    }

    public float GetTimeScale() => TimeScale;

    public float GetEffectiveTimeScale() => EffectiveTimeScale;

    public void SetElapsedTime(float time)
    {
        _elapsedTime = time;
    }

    public float GetElapsedTime() => _elapsedTime;

    public void SetAsyncLoadingMs(int ms)
    {
        _asyncLoadingMs = Math.Max(ms, 1);
    }

    public int GetAsyncLoadingMs() => _asyncLoadingMs;

    public bool IsAsyncLoading() => _asyncLoading;

    public float GetAsyncProgress() => 1.0f;

    public LoadMode GetAsyncLoadMode() => _asyncLoadMode;

    public void SetFileName(string fileName)
    {
        this._fileName = fileName ?? string.Empty;
        Checksum = string.IsNullOrEmpty(this._fileName) ? 0u : StringHash.Calculate(this._fileName);
    }

    public string GetFileName() => _fileName;

    public uint GetChecksum() => Checksum;

    /// <summary>
    /// Rebase world origin and update scene contents accordingly.
    /// </summary>
    public void UpdateWorldOrigin(IntVector3 worldOrigin)
    {
        if (_worldOrigin == worldOrigin)
            return;

        var oldWorldOrigin = _worldOrigin;
        var offset = new IntVector3(
            worldOrigin.X - oldWorldOrigin.X,
            worldOrigin.Y - oldWorldOrigin.Y,
            worldOrigin.Z - oldWorldOrigin.Z);

        var updateEvent = new SceneEvents.WorldOriginUpdateEvent
        {
            Scene = this,
            OldOrigin = oldWorldOrigin,
            NewOrigin = worldOrigin,
            Delta = offset
        };
        SendEvent(ref updateEvent);

        _worldOrigin = worldOrigin;
        var relativeOffset = new Vector3f(offset.X, offset.Y, offset.Z);
        if (Root.GetWorldOriginUpdateMode() == WorldOriginUpdateMode.Move)
        {
            foreach (var child in Root.GetChildren())
                child.SetWorldPosition(child.GetWorldPosition() - relativeOffset);
        }
        else
            UpdateNodeWorldOrigin(Root, relativeOffset);

        var postUpdateEvent = new SceneEvents.WorldOriginPostUpdateEvent
        {
            OldOrigin = oldWorldOrigin,
            NewOrigin = worldOrigin
        };
        SendEvent(ref postUpdateEvent);
    }

    /// <summary>
    /// Convert world position from relative to world origin to absolute.
    /// </summary>
    public Vector3d ToAbsoluteWorldPosition(Vector3f position) =>
        new(position.X + _worldOrigin.X, position.Y + _worldOrigin.Y, position.Z + _worldOrigin.Z);

    /// <summary>
    /// Convert world position from absolute to relative to world origin.
    /// </summary>
    public Vector3f ToRelativeWorldPosition(Vector3d position) =>
        new((float)(position.X - _worldOrigin.X), (float)(position.Y - _worldOrigin.Y), (float)(position.Z - _worldOrigin.Z));

    /// <summary>
    /// Convert world transform from relative to world origin to absolute.
    /// </summary>
    public DoubleTransform ToAbsoluteWorldTransform(Transform transform) => new()
    {
        Position = ToAbsoluteWorldPosition(transform.Position),
        Rotation = transform.Rotation,
        Scale = transform.Scale
    };

    /// <summary>
    /// Convert world transform from absolute to relative to world origin.
    /// </summary>
    public Transform ToRelativeWorldTransform(DoubleTransform transform) => new(
        ToRelativeWorldPosition(transform.Position),
        transform.Rotation,
        transform.Scale);

    /// <summary>
    /// Set world origin without any scene updates.
    /// </summary>
    public void SetWorldOrigin(IntVector3 worldOrigin) => _worldOrigin = worldOrigin;

    /// <summary>
    /// Return world origin.
    /// </summary>
    public IntVector3 GetWorldOrigin() => _worldOrigin;

    public bool IsEmpty(bool ignoreComponents = false)
    {
        return Root.GetNumChildren() == 0 && (ignoreComponents || Root.GetNumComponents() == 0);
    }

    public Node? GetNode(uint id)
    {
        return _nodes.GetValueOrDefault(id);
    }

    public Component? GetComponent(uint id)
    {
        return _components.GetValueOrDefault(id);
    }

    public IReadOnlyList<Node> GetNodesWithTag(string tag)
    {
        return _taggedNodes.TryGetValue(tag, out var tagged) ? tagged : Array.Empty<Node>();
    }

    public IReadOnlyDictionary<uint, Node> GetAllNodes()
    {
        return _nodes;
    }

    public IReadOnlyDictionary<uint, Component> GetAllComponents()
    {
        return _components;
    }

    public void GetComponents<T>(List<T> result)
        where T : Component
    {
        ArgumentNullException.ThrowIfNull(result);

        result.Clear();
        foreach (var component in _components.Values)
        {
            if (component is T typed)
                result.Add(typed);
        }
    }

    public void Update(float timeStep)
    {
        var effectiveTimeStep = timeStep * TimeScale;

        var forcedUpdateEvent = new SceneEvents.SceneForcedUpdateEvent
        {
            Scene = this,
            TimeStep = effectiveTimeStep
        };
        SendEvent(ref forcedUpdateEvent);

        if (UpdateEnabled)
        {
            var listenerSnapshot = SnapshotUpdateListeners();
            try
            {
                var sceneUpdateEvent = new SceneEvents.SceneUpdateEvent
                {
                    Scene = this,
                    TimeStep = effectiveTimeStep
                };
                SendEvent(ref sceneUpdateEvent);
                for (var i = 0; i < listenerSnapshot.Count; ++i)
                    listenerSnapshot.Array[i]!.OnSceneUpdate(effectiveTimeStep);

                var attributeAnimationUpdateEvent = new SceneEvents.AttributeAnimationUpdateEvent
                {
                    Scene = this,
                    TimeStep = effectiveTimeStep
                };
                SendEvent(ref attributeAnimationUpdateEvent);
                UpdateAttributeAnimations(effectiveTimeStep);

                var subsystemUpdateEvent = new SceneEvents.SceneSubsystemUpdateEvent
                {
                    Scene = this,
                    TimeStep = effectiveTimeStep
                };
                SendEvent(ref subsystemUpdateEvent);

                var postUpdateEvent = new SceneEvents.ScenePostUpdateEvent
                {
                    Scene = this,
                    TimeStep = effectiveTimeStep
                };
                SendEvent(ref postUpdateEvent);
                for (var i = 0; i < listenerSnapshot.Count; ++i)
                    listenerSnapshot.Array[i]!.OnScenePostUpdate(effectiveTimeStep);

                _elapsedTime += effectiveTimeStep;
            }
            finally
            {
                listenerSnapshot.Dispose();
            }
        }

        var forcedPostUpdateEvent = new SceneEvents.SceneForcedPostUpdateEvent
        {
            Scene = this,
            TimeStep = effectiveTimeStep
        };
        SendEvent(ref forcedPostUpdateEvent);
    }

    public void BeginThreadedUpdate()
    {
        if (GetSubsystem<WorkQueue>()!.IsMultithreaded())
            _threadedUpdate = true;
    }

    public void EndThreadedUpdate()
    {
        if (!_threadedUpdate)
            return;

        _threadedUpdate = false;

        lock (_delayedDirtyComponents)
        {
            foreach (var component in _delayedDirtyComponents)
                component.OnMarkedDirty(component.Node);
            _delayedDirtyComponents.Clear();
        }
    }

    public void DelayedMarkedDirty(Component component)
    {
        lock (_delayedDirtyComponents)
            _delayedDirtyComponents.Add(component);
    }

    public bool IsThreadedUpdate() => _threadedUpdate;

    internal uint AllocateComponentId(uint requestedId)
    {
        return AllocateId(_components, ref _nextComponentId, requestedId);
    }

    internal uint AllocateNodeId(uint requestedId)
    {
        return AllocateId(_nodes, ref _nextNodeId, requestedId);
    }

    public void NodeAdded(Node node)
    {
        if (_nodes.TryGetValue(node.Id, out var existingNode) && !ReferenceEquals(existingNode, node))
            existingNode.Parent?.RemoveChild(existingNode);

        if (!_nodes.TryAdd(node.Id, node))
            return;

        foreach (var tag in node.Tags)
            NodeTagAdded(node, tag);

        foreach (var component in node.Components)
            ComponentAdded(component);

        foreach (var child in node.Children)
            NodeAdded(child);
    }

    public void NodeRemoved(Node node)
    {
        if (!_nodes.Remove(node.Id))
            return;

        foreach (var child in node.Children)
            NodeRemoved(child);

        foreach (var component in node.Components)
            ComponentRemoved(component);

        foreach (var tag in node.Tags)
            NodeTagRemoved(node, tag);
    }

    public void NodeTagAdded(Node node, string tag)
    {
        if (!_taggedNodes.TryGetValue(tag, out var tagged))
            _taggedNodes[tag] = tagged = [];
        tagged.Add(node);
    }

    public void NodeTagRemoved(Node node, string tag)
    {
        if (!_taggedNodes.TryGetValue(tag, out var tagged))
            return;

        tagged.Remove(node);
        if (tagged.Count == 0)
            _taggedNodes.Remove(tag);
    }

    public void ComponentAdded(Component component)
    {
        if (_components.TryGetValue(component.Id, out var existingComponent) && !ReferenceEquals(existingComponent, component))
            existingComponent.Node.RemoveComponent(existingComponent);

        _components[component.Id] = component;
        component.OnSceneSet(null, this);

        GetMutableComponentIndex(component.GetTypeNameHash())?.Add(component);

        if (component is ISceneUpdateListener listener)
            _updateListeners.Add(listener);
        if (component is StaticModel staticModel)
            _staticModels.Add(staticModel);
    }

    public void ComponentRemoved(Component component)
    {
        if (!_components.Remove(component.Id))
            return;

        GetMutableComponentIndex(component.GetTypeNameHash())?.Remove(component);

        if (component is ISceneUpdateListener listener)
            _updateListeners.Remove(listener);
        if (component is StaticModel staticModel)
            _staticModels.Remove(staticModel);

        component.SetId(0);
        component.OnSceneSet(this, null);
    }

    private static void UpdateNodeWorldOrigin(Node node, Vector3f offset)
    {
        switch (node.GetWorldOriginUpdateMode())
        {
            case WorldOriginUpdateMode.Ignore:
                break;
            case WorldOriginUpdateMode.Move:
                node.SetPosition(node.GetPosition() - offset);
                break;
            case WorldOriginUpdateMode.Recurse:
                foreach (var child in node.GetChildren())
                    UpdateNodeWorldOrigin(child, offset);
                break;
        }
    }

    private void UpdateAttributeAnimations(float timeStep)
    {
        var nodeSnapshot = SnapshotValues(_nodes);
        try
        {
            for (var i = 0; i < nodeSnapshot.Count; ++i)
                nodeSnapshot.Array[i]!.ApplyAttributeAnimations(timeStep);
        }
        finally
        {
            nodeSnapshot.Dispose();
        }

        var componentSnapshot = SnapshotValues(_components);
        try
        {
            for (var i = 0; i < componentSnapshot.Count; ++i)
                componentSnapshot.Array[i]!.ApplyAttributeAnimations(timeStep);
        }
        finally
        {
            componentSnapshot.Dispose();
        }
    }

    private static uint AllocateId<T>(Dictionary<uint, T> objects, ref uint nextId, uint requestedId)
        where T : class
    {
        if (requestedId != 0 && !objects.ContainsKey(requestedId))
        {
            if (requestedId >= nextId)
                nextId = requestedId + 1;
            return requestedId;
        }

        return NextFreeId(objects, ref nextId);
    }

    private static uint NextFreeId<T>(Dictionary<uint, T> objects, ref uint nextId)
        where T : class
    {
        while (nextId == 0 || objects.ContainsKey(nextId))
            ++nextId;

        return nextId++;
    }

    private static byte[] ReadRemaining(Deserializer source)
    {
        var remaining = source.GetSize() - source.GetPosition();
        var data = new byte[checked((int)remaining)];
        var read = source.Read(data);
        if (read != data.Length)
            Array.Resize(ref data, checked((int)read));
        return data;
    }

    private void FinishLoading(Deserializer? source)
    {
        if (source is not null)
        {
            _fileName = source.GetName();
            Checksum = source.GetChecksum();
        }
    }

    private void FinishSaving(Serializer destination)
    {
        if (destination is Deserializer source)
        {
            _fileName = source.GetName();
            Checksum = source.GetChecksum();
        }
    }

    private HashSet<Component>? GetMutableComponentIndex(StringHash componentType)
    {
        for (var i = 0; i < _componentIndexes.Count; ++i)
        {
            if (_componentIndexes[i].Type == componentType)
                return _componentIndexes[i].Components;
        }

        return null;
    }

    private void SerializeAuxiliaryData(Archive archive)
    {
        if (archive.IsInput && archive.IsUnorderedAccessSupportedInCurrentBlock && !archive.HasElementOrBlock("auxiliary"))
            return;

        var components = new Dictionary<string, Component>(StringComparer.Ordinal);
        foreach (var component in _components.Values)
        {
            if (component.HasAuxiliaryData())
                components[component.GetTypeName()] = component;
        }

        using var block = archive.OpenArrayBlock("auxiliary", archive.IsInput ? 0u : (uint)components.Count);
        if (archive.IsInput)
        {
            for (var i = 0u; i < block.SizeHint; ++i)
            {
                using var elementBlock = archive.OpenUnorderedBlock("component");
                var typeName = string.Empty;
                ArchiveSerialization.SerializeValue(archive, "key", ref typeName);
                if (components.TryGetValue(typeName, out var component))
                {
                    try
                    {
                        component.SerializeAuxiliaryData(archive);
                    }
                    catch (Exception)
                    {
                    }
                }
            }
        }
        else
        {
            foreach (var (typeName, component) in components)
            {
                using var elementBlock = archive.OpenUnorderedBlock("component");
                var key = typeName;
                ArchiveSerialization.SerializeValue(archive, "key", ref key);
                try
                {
                    component.SerializeAuxiliaryData(archive);
                }
                catch (Exception)
                {
                }
            }
        }
    }

    private Snapshot<ISceneUpdateListener> SnapshotUpdateListeners()
    {
        var array = ArrayPool<ISceneUpdateListener>.Shared.Rent(_updateListeners.Count);
        _updateListeners.CopyTo(array, 0);
        return new Snapshot<ISceneUpdateListener>(array, _updateListeners.Count);
    }

    private static Snapshot<T> SnapshotValues<T>(Dictionary<uint, T> objects)
        where T : class
    {
        var array = ArrayPool<T>.Shared.Rent(objects.Count);
        var index = 0;
        foreach (var value in objects.Values)
            array[index++] = value;
        return new Snapshot<T>(array, index);
    }

    private readonly ref struct Snapshot<T>
        where T : class
    {
        public Snapshot(T[] array, int count)
        {
            Array = array;
            Count = count;
        }

        public T[] Array { get; }
        public int Count { get; }

        public void Dispose()
        {
            System.Array.Clear(Array, 0, Count);
            ArrayPool<T>.Shared.Return(Array);
        }
    }
}
