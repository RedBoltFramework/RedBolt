// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Scene;

using Microsoft.Extensions.Logging;
using System.Buffers;
using System.Diagnostics;
using SystemMath = global::System.Math;

using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.IO;
using RedBolt.Input;
using RedBolt.Logging;
using RedBolt.Math;
using RedBolt.Resource;
using ResourceType = global::RedBolt.Resource.Resource;

/// <summary>Asynchronous scene loading mode.</summary>
public static class SceneConstants
{
    public const uint FirstReplicatedId = 0x1;
    public const uint LastReplicatedId = 0xffffff;
}

public enum LoadMode
{
    /// <summary>Preload resources without loading scene content.</summary>
    ResourcesOnly,
    /// <summary>Load scene content without preloading resources.</summary>
    Scene,
    /// <summary>Preload resources, then load scene content.</summary>
    SceneAndResources
}

/// <summary>Asynchronous loading progress of a scene.</summary>
public struct AsyncProgress
{
    /// <summary>File for binary mode.</summary>
    public AbstractFile? File;
    /// <summary>XML file for XML mode.</summary>
    public XMLFile? XmlFile;
    /// <summary>JSON file for JSON mode.</summary>
    public JSONFile? JsonFile;
    /// <summary>Current XML element for XML mode.</summary>
    public XMLElement? XmlElement;
    /// <summary>Current JSON child index for JSON mode.</summary>
    public int JsonIndex;
    /// <summary>Loading mode.</summary>
    public LoadMode Mode;
    /// <summary>Resources being loaded.</summary>
    public HashSet<StringHash> Resources;
    /// <summary>Number of loaded resources.</summary>
    public int LoadedResources;
    /// <summary>Total number of resources.</summary>
    public int TotalResources;
    /// <summary>Number of loaded root-level nodes.</summary>
    public int LoadedNodes;
    /// <summary>Total number of root-level nodes.</summary>
    public int TotalNodes;

    public AsyncProgress()
    {
        Mode = LoadMode.SceneAndResources;
        Resources = [];
    }
}

[Reflected(InheritAttributes = false)]
public partial class Scene : Node
{
    /// <summary>Register Scene library objects.</summary>
    public static void RegisterSceneLibrary(Context context)
    {
        context.RegisterAssembly(typeof(Scene).Assembly);
    }

    private readonly Dictionary<uint, Component> _components = [];
    private readonly Dictionary<uint, Node> _nodes = [];
    private readonly Dictionary<string, List<Node>> _taggedNodes = new(StringComparer.Ordinal);
    private readonly List<(StringHash Type, HashSet<Component> Components)> _componentIndexes = [];
    private readonly List<PackageFile> _requiredPackageFiles = [];
    private readonly List<Component> _delayedDirtyComponents = [];
    private readonly List<StaticModel> _staticModels = [];
    private readonly List<string> _lightmapNames = [];
    private readonly List<Texture2D?> _lightmapTextures = [];
    private readonly StringVector _updateEvents = [];
    private readonly List<(StringHash EventType, bool IsForced)> _cookedUpdateEvents = [];
    private AsyncProgress _asyncProgress = new();
    private readonly SceneResolver _resolver = new();
    private int _asyncLoadingMs = 5;
    private bool _asyncLoading;
    private Vector3d _elapsedTimes;
    [Serialized("Elapsed Time Wrap")]
    private Vector3d _elapsedTimeWrap = new(1_000_000_000_000.0, 1024.0, 1.0);
    [Serialized("Elapsed Time", Setter = nameof(SetElapsedTime))]
    private float _elapsedTime;
    private string _fileName = string.Empty;
    [Serialized("World Origin")]
    private IntVector3 _worldOrigin;
    [Serialized("Next Component ID", Mode = AttributeMode.Default | AttributeMode.NoEdit)]
    private uint _nextComponentId = 1;
    [Serialized("Next Node ID", Mode = AttributeMode.Default | AttributeMode.NoEdit)]
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
        SetUpdateEvents(DefaultUpdateEvents);
        SetWorldOriginUpdateMode(WorldOriginUpdateMode.Recurse);

        // Assign an ID to self so that nodes can refer to this node as a parent
        SetID(GetFreeNodeID());
        NodeAdded(this);

        SubscribeToEvent<CoreEvents.UpdateEvent>(HandleUpdate);
        SubscribeToEvent<ResourceEvents.ResourceBackgroundLoaded>(HandleResourceBackgroundLoaded);

    }


    public override void Dispose()
    {
        RemoveAllComponents();
        RemoveAllChildren();
        foreach (var node in _nodes.Values)
            node.ResetScene();
        _resolver.Dispose();
        base.Dispose();
    }

    internal IReadOnlyList<StaticModel> StaticModels => _staticModels;

    public bool UpdateEnabled { get; set; } = true;

    [Serialized("Time Scale", Setter = nameof(SetTimeScale))]
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

    public new void SerializeInBlock(
        Archive archive,
        bool serializeTemporary,
        PrefabSaveFlag saveFlags,
        PrefabLoadFlag loadFlags)
    {
        base.SerializeInBlock(archive, serializeTemporary, saveFlags, loadFlags | PrefabLoadFlag.SkipApplyAttributes);
        SerializeAuxiliaryData(archive);

        if (archive.IsInput)
            ApplyAttributes();

        if (!string.IsNullOrEmpty(archive.Name))
            _fileName = archive.Name;
        if (archive.Checksum != 0)
            Checksum = archive.Checksum;
    }

    public override bool LoadXML(XMLElement source)
    {
        StopAsyncLoading();

        if (source.Name == SceneResource.GetXmlRootName())
        {
            using var archive = new XMLInputArchive(Context, source, source.File);
            using (archive.OpenUnorderedBlock(SceneResource.GetXmlRootName()))
                SerializeInBlock(archive, false, PrefabSaveFlag.None, PrefabLoadFlag.None);
            return true;
        }

        if (base.LoadXML(source))
        {
            FinishLoading(null);
            return true;
        }

        throw new ArchiveException("Cannot load Scene from legacy XML format");
    }

    public override bool LoadJSON(JSONValue source)
    {
        StopAsyncLoading();
        if (base.LoadJSON(source))
        {
            FinishLoading(null);
            return true;
        }

        return false;
    }

    public bool LoadXML(Deserializer source)
    {
        ArgumentNullException.ThrowIfNull(source);
        StopAsyncLoading();
        var xml = new XMLFile();
        if (!xml.Load(source))
            return false;

        Clear();
        return LoadXML(xml.GetRoot());
    }

    public bool LoadJSON(Deserializer source)
    {
        ArgumentNullException.ThrowIfNull(source);
        StopAsyncLoading();
        var json = new JSONFile();
        if (!json.Load(source))
            return false;

        Clear();
        if (base.LoadJSON(json.Root))
        {
            FinishLoading(source);
            return true;
        }

        return false;
    }

    public override bool Load(Deserializer source)
    {
        ArgumentNullException.ThrowIfNull(source);
        StopAsyncLoading();

        ReadOnlySpan<byte> sceneBinaryMagic = "USCN"u8;
        var format = ResourceFormat.PeekResourceFormat(source, sceneBinaryMagic);
        switch (format)
        {
            case InternalResourceFormat.Json:
                return LoadJSON(source);
            case InternalResourceFormat.Xml:
                return LoadXML(source);
            case InternalResourceFormat.Binary:
                break;
            default:
                Log.GetLogger().LogError("{Name} is not a valid scene file", source.GetName());
                return false;
        }

        Log.GetLogger().LogInformation("Loading scene from {Name}", source.GetName());

        Clear();
        source.Seek(source.Position + checked((uint)sceneBinaryMagic.Length));
        if (base.Load(source))
        {
            FinishLoading(source);
            return true;
        }

        return false;
    }

    public override bool Save(Serializer destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (!destination.WriteFileID("USCN"))
            return false;

        if (base.Save(destination))
        {
            FinishSaving(destination);
            return true;
        }

        return false;
    }

    public new bool SaveXML(Serializer destination, string indentation = "\t")
    {
        ArgumentNullException.ThrowIfNull(destination);
        var xml = new XMLFile();
        if (!base.SaveXML(xml.CreateRoot("scene")))
            return false;
        if (!xml.Save(destination, indentation))
            return false;

        FinishSaving(destination);
        return true;
    }

    public new bool SaveJSON(Serializer destination, string indentation = "\t")
    {
        ArgumentNullException.ThrowIfNull(destination);
        using var json = new JSONFile();
        using var root = new JSONValue();
        if (!base.SaveJSON(root))
            return false;
        json.Root.Set(root);
        if (!json.Save(destination, indentation))
            return false;

        FinishSaving(destination);
        return true;
    }

    public bool LoadAsync(AbstractFile? file, LoadMode mode = LoadMode.SceneAndResources)
    {
        if (file is null)
        {
            Log.GetLogger().LogError("Null file for async loading");
            return false;
        }

        StopAsyncLoading();

        var isSceneFile = file.ReadFileID() == "USCN";
        if (!isSceneFile)
        {
            if (mode > LoadMode.ResourcesOnly)
            {
                Log.GetLogger().LogError("{FileName} is not a valid scene file", file.Name);
                return false;
            }

            file.Seek(0);
        }

        if (mode > LoadMode.ResourcesOnly)
        {
            Log.GetLogger().LogInformation("Loading scene from {FileName}", file.Name);
            Clear();
        }

        _asyncLoading = true;
        _asyncProgress.File = file;
        _asyncProgress.Mode = mode;
        _asyncProgress.LoadedNodes = 0;
        _asyncProgress.TotalNodes = 0;
        _asyncProgress.LoadedResources = 0;
        _asyncProgress.TotalResources = 0;
        _asyncProgress.Resources.Clear();

        if (mode > LoadMode.ResourcesOnly)
        {
            if (mode != LoadMode.Scene)
            {
                var currentPosition = file.GetPosition();
                PreloadResources(file, isSceneFile);
                file.Seek(currentPosition);
            }

            var nodeId = file.ReadUInt();
            _resolver.AddNode(nodeId, this);
            if (!base.Load(file, _resolver, loadChildren: false))
            {
                StopAsyncLoading();
                return false;
            }

            _asyncProgress.TotalNodes = checked((int)file.ReadVLE());
        }
        else
            PreloadResources(file, isSceneFile);

        return true;
    }

    public bool LoadAsyncXML(AbstractFile? file, LoadMode mode = LoadMode.SceneAndResources)
    {
        if (file is null)
        {
            Log.GetLogger().LogError("Null file for async loading");
            return false;
        }

        StopAsyncLoading();

        var xml = new XMLFile();
        if (!xml.Load(file))
            return false;

        if (mode > LoadMode.ResourcesOnly)
        {
            Log.GetLogger().LogInformation("Loading scene from {FileName}", file.Name);
            Clear();
        }

        _asyncLoading = true;
        _asyncProgress.XmlFile = xml;
        _asyncProgress.File = file;
        _asyncProgress.Mode = mode;
        _asyncProgress.LoadedNodes = 0;
        _asyncProgress.TotalNodes = 0;
        _asyncProgress.LoadedResources = 0;
        _asyncProgress.TotalResources = 0;
        _asyncProgress.Resources.Clear();

        var rootElement = xml.GetRoot();
        if (mode > LoadMode.ResourcesOnly)
        {
            if (mode != LoadMode.Scene)
                PreloadResourcesXML(rootElement);

            var nodeId = rootElement.GetUInt("id");
            _resolver.AddNode(nodeId, this);
            if (!base.LoadXML(rootElement, _resolver, loadChildren: false))
                return false;

            _asyncProgress.XmlElement = rootElement.GetChild("node");
            var childNodeElement = _asyncProgress.XmlElement;
            while (childNodeElement is { NotNull: true })
            {
                ++_asyncProgress.TotalNodes;
                childNodeElement = childNodeElement.GetNext("node");
            }
        }
        else
            PreloadResourcesXML(rootElement);

        return true;
    }

    public bool LoadAsyncJSON(AbstractFile? file, LoadMode mode = LoadMode.SceneAndResources)
    {
        if (file is null)
        {
            Log.GetLogger().LogError("Null file for async loading");
            return false;
        }

        StopAsyncLoading();

        var json = new JSONFile();
        if (!json.Load(file))
            return false;

        if (mode > LoadMode.ResourcesOnly)
        {
            Log.GetLogger().LogInformation("Loading scene from {FileName}", file.Name);
            Clear();
        }

        _asyncLoading = true;
        _asyncProgress.JsonFile = json;
        _asyncProgress.File = file;
        _asyncProgress.Mode = mode;
        _asyncProgress.JsonIndex = 0;
        _asyncProgress.LoadedNodes = 0;
        _asyncProgress.TotalNodes = 0;
        _asyncProgress.LoadedResources = 0;
        _asyncProgress.TotalResources = 0;
        _asyncProgress.Resources.Clear();

        var rootValue = json.Root;
        if (mode > LoadMode.ResourcesOnly)
        {
            if (mode != LoadMode.Scene)
                PreloadResourcesJSON(rootValue);

            var nodeId = rootValue.Get("id").GetUInt();
            _resolver.AddNode(nodeId, this);
            if (!base.LoadJSON(rootValue, _resolver, loadChildren: false))
                return false;

            _asyncProgress.TotalNodes = rootValue.Get("children").GetArray().Count;
        }
        else
            PreloadResourcesJSON(rootValue);

        return true;
    }

    public Node? Instantiate(Deserializer source, Vector3f position, Quaternion rotation)
    {
        ArgumentNullException.ThrowIfNull(source);

        using var resolver = new SceneResolver();
        var nodeId = source.ReadUInt();
        var node = CreateChild(0);
        resolver.AddNode(nodeId, node);
        if (node.Load(source, resolver, true, true))
        {
            resolver.Resolve();
            node.SetTransform(position, rotation);
            node.ApplyAttributes();
            return node;
        }

        node.Remove();
        return null;
    }

    public Node? InstantiateXML(XMLElement source, Vector3f position, Quaternion rotation)
    {
        ArgumentNullException.ThrowIfNull(source);

        using var resolver = new SceneResolver();
        var nodeId = source.GetUInt("id");
        var node = CreateChild(0);
        resolver.AddNode(nodeId, node);
        if (node.LoadXML(source, resolver, true, true))
        {
            resolver.Resolve();
            node.SetTransform(position, rotation);
            node.ApplyAttributes();
            return node;
        }

        node.Remove();
        return null;
    }

    public Node? InstantiateJSON(JSONValue source, Vector3f position, Quaternion rotation)
    {
        ArgumentNullException.ThrowIfNull(source);

        using var resolver = new SceneResolver();
        var nodeId = source.Get("id").GetUInt();
        var node = CreateChild(0);
        resolver.AddNode(nodeId, node);
        if (node.LoadJSON(source, resolver, true, true))
        {
            resolver.Resolve();
            node.SetTransform(position, rotation);
            node.ApplyAttributes();
            return node;
        }

        node.Remove();
        return null;
    }

    public Node? InstantiateXML(Deserializer source, Vector3f position, Quaternion rotation)
    {
        ArgumentNullException.ThrowIfNull(source);
        var xml = new XMLFile();
        return xml.Load(source) ? InstantiateXML(xml.GetRoot(), position, rotation) : null;
    }

    public Node? InstantiateJSON(Deserializer source, Vector3f position, Quaternion rotation)
    {
        ArgumentNullException.ThrowIfNull(source);
        var json = new JSONFile();
        return json.Load(source) ? InstantiateJSON(json.Root, position, rotation) : null;
    }

    public void StopAsyncLoading()
    {
        _asyncLoading = false;
        _asyncProgress.File = null;
        _asyncProgress.XmlFile = null;
        _asyncProgress.JsonFile = null;
        _asyncProgress.XmlElement = null;
        _asyncProgress.JsonIndex = 0;
        _asyncProgress.Resources.Clear();
        _resolver.Reset();
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

    [Serialized("Update Events", DefaultSource = nameof(GetUpdateEventsAttributeDefault), Setter = nameof(SetUpdateEvents))]
    public StringVector GetUpdateEvents() => new(_updateEvents);

    public int GetNumLightmaps() => _lightmapNames.Count;

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

    public Texture2D? GetLightmapTexture(int index)
    {
        return index >= 0 && index < _lightmapTextures.Count ? _lightmapTextures[index] : null;
    }

    public void Clear()
    {
        StopAsyncLoading();

        RemoveChildren(recursive: true);
        RemoveComponents();

        SetName(string.Empty);
        SetUpdateEvents(DefaultUpdateEvents);
        _fileName = string.Empty;
        Checksum = 0;

        _nextNodeId = 1;
        _nextComponentId = 1;
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
        TimeScale = SystemMath.Max(scale, MathDefs.Epsilon);
    }

    public float GetTimeScale() => TimeScale;

    public float GetEffectiveTimeScale() => EffectiveTimeScale;

    public void SetElapsedTime(float time)
    {
        SetElapsedTime((double)time);
    }

    public float GetElapsedTime() => _elapsedTime;

    public void SetElapsedTime(double time)
    {
        _elapsedTimes = new Vector3d(time, time, time);
        WrapElapsedTime();
        _elapsedTime = (float)_elapsedTimes.X;
    }

    public Vector3d GetElapsedTimes() => _elapsedTimes;

    public void SetAsyncLoadingMs(int ms)
    {
        _asyncLoadingMs = SystemMath.Max(ms, 1);
    }

    public int GetAsyncLoadingMs() => _asyncLoadingMs;

    public bool IsAsyncLoading() => _asyncLoading;

    public float GetAsyncProgress()
    {
        var total = _asyncProgress.TotalNodes + _asyncProgress.TotalResources;
        return !_asyncLoading || total == 0
            ? 1.0f
            : (_asyncProgress.LoadedNodes + _asyncProgress.LoadedResources) / (float)total;
    }

    public LoadMode GetAsyncLoadMode() => _asyncProgress.Mode;

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
        UpdateNodeWorldOrigin(this, relativeOffset);

        var postUpdateEvent = new SceneEvents.WorldOriginPostUpdateEvent
        {
            Scene = this,
            OldOrigin = oldWorldOrigin,
            NewOrigin = worldOrigin,
            Delta = offset
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
    public DoubleTransform ToAbsoluteWorldTransform(in Transform transform) => new()
    {
        Position = ToAbsoluteWorldPosition(transform.Position),
        Rotation = transform.Rotation,
        Scale = transform.Scale
    };

    /// <summary>
    /// Convert world transform from absolute to relative to world origin.
    /// </summary>
    public Transform ToRelativeWorldTransform(in DoubleTransform transform) => new(
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
        return GetNumChildren() == 0 && (ignoreComponents || GetNumComponents() == 0);
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
        if (UpdateEnabled && _asyncLoading)
        {
            UpdateAsyncLoading();
            if (_asyncProgress.Mode > LoadMode.ResourcesOnly)
                return;
        }

        var effectiveTimeStep = timeStep * TimeScale;

        var forcedUpdateEvent = new SceneEvents.SceneForcedUpdateEvent
        {
            Scene = this,
            TimeStep = effectiveTimeStep
        };
        SendEvent(ref forcedUpdateEvent);

        if (UpdateEnabled)
        {
            var sceneUpdateEvent = new SceneEvents.SceneUpdateEvent
            {
                Scene = this,
                TimeStep = effectiveTimeStep
            };
            SendEvent(ref sceneUpdateEvent);

            var attributeAnimationUpdateEvent = new SceneEvents.AttributeAnimationUpdateEvent
            {
                Scene = this,
                TimeStep = effectiveTimeStep
            };
            SendEvent(ref attributeAnimationUpdateEvent);
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

            _elapsedTimes += new Vector3d(effectiveTimeStep, effectiveTimeStep, effectiveTimeStep);
            WrapElapsedTime();
            _elapsedTime = (float)_elapsedTimes.X;
        }

        var forcedPostUpdateEvent = new SceneEvents.SceneForcedPostUpdateEvent
        {
            Scene = this,
            TimeStep = effectiveTimeStep
        };
        SendEvent(ref forcedPostUpdateEvent);
    }

    private void WrapElapsedTime()
    {
        _elapsedTimes = new Vector3d(
            _elapsedTimes.X % _elapsedTimeWrap.X,
            _elapsedTimes.Y % _elapsedTimeWrap.Y,
            _elapsedTimes.Z % _elapsedTimeWrap.Z);
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
        if (node is null || ReferenceEquals(node.GetScene(), this))
            return;

        var oldScene = node.GetScene();
        if (oldScene is not null)
            oldScene.NodeRemoved(node);

        node.SetScene(this);

        var id = node.Id;
        if (id == 0)
        {
            id = GetFreeNodeID();
            node.SetID(id);
        }

        if (_nodes.TryGetValue(id, out var existingNode) && !ReferenceEquals(existingNode, node))
        {
            Log.GetLogger().Warning("Overwriting node with ID {0}", id);
            NodeRemoved(existingNode);
        }

        _nodes[id] = node;

        foreach (var tag in node.Tags)
            NodeTagAdded(node, tag);

        foreach (var component in node.Components)
            ComponentAdded(component);

        foreach (var child in node.Children)
            NodeAdded(child);
    }

    public void NodeRemoved(Node node)
    {
        if (node is null || !ReferenceEquals(node.GetScene(), this))
            return;

        _nodes.Remove(node.Id);
        node.ResetScene();

        foreach (var tag in node.Tags)
            NodeTagRemoved(node, tag);

        foreach (var component in node.Components)
            ComponentRemoved(component);

        foreach (var child in node.Children)
            NodeRemoved(child);
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
        var id = component.Id;
        if (id == 0)
        {
            id = GetFreeComponentID();
            component.SetID(id);
        }

        if (_components.TryGetValue(component.Id, out var existingComponent) && !ReferenceEquals(existingComponent, component))
            ComponentRemoved(existingComponent);

        _components[component.Id] = component;
        component.OnSceneSet(null, this);

        GetMutableComponentIndex(component.GetTypeNameHash())?.Add(component);

        if (component is StaticModel staticModel)
            _staticModels.Add(staticModel);
    }

    public void ComponentRemoved(Component component)
    {
        _components.Remove(component.Id);

        GetMutableComponentIndex(component.GetTypeNameHash())?.Remove(component);

        if (component is StaticModel staticModel)
        {
            var lastIndex = _staticModels.Count - 1;
            if (lastIndex >= 0 && ReferenceEquals(_staticModels[lastIndex], staticModel))
                _staticModels.RemoveAt(lastIndex);
            else
                _staticModels.Remove(staticModel);
        }

        component.SetID(0);
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

    private void HandleUpdate(ref CoreEvents.UpdateEvent eventData)
    {
        if (_manualUpdate)
            return;

        Update(eventData.TimeStep);
    }

    private void HandleResourceBackgroundLoaded(ref ResourceEvents.ResourceBackgroundLoaded eventData)
    {
        if (!_asyncLoading || eventData.Resource is null)
            return;

        var resourceNameHash = new StringHash(eventData.Resource.Name);
        if (_asyncProgress.Resources.Remove(resourceNameHash))
            ++_asyncProgress.LoadedResources;
    }

    private void UpdateAsyncLoading()
    {
        if (_asyncProgress.LoadedResources < _asyncProgress.TotalResources)
            return;

        var startTimestamp = Stopwatch.GetTimestamp();
        for (;;)
        {
            if (_asyncProgress.LoadedNodes >= _asyncProgress.TotalNodes)
            {
                FinishAsyncLoading();
                return;
            }

            if (_asyncProgress.XmlFile is not null && _asyncProgress.XmlElement is { NotNull: true } xmlElement)
            {
                var nodeId = xmlElement.GetUInt("id");
                var newNode = CreateChild(nodeId);
                _resolver.AddNode(nodeId, newNode);
                newNode.LoadXML(xmlElement, _resolver);
                _asyncProgress.XmlElement = xmlElement.GetNext("node");
            }
            else if (_asyncProgress.JsonFile is not null)
            {
                var childValue = _asyncProgress.JsonFile.Root.Get("children").Get(_asyncProgress.JsonIndex);
                var nodeId = childValue.Get("id").GetUInt();
                var newNode = CreateChild(nodeId);
                _resolver.AddNode(nodeId, newNode);
                newNode.LoadJSON(childValue, _resolver);
                ++_asyncProgress.JsonIndex;
            }
            else if (_asyncProgress.File is not null)
            {
                var nodeId = _asyncProgress.File.ReadUInt();
                var newNode = CreateChild(nodeId);
                _resolver.AddNode(nodeId, newNode);
                newNode.Load(_asyncProgress.File, _resolver);
            }

            ++_asyncProgress.LoadedNodes;

            var elapsedMicroseconds = (Stopwatch.GetTimestamp() - startTimestamp) * 1_000_000L
                / Stopwatch.Frequency;
            if (elapsedMicroseconds >= _asyncLoadingMs * 1000L)
                break;
        }

        var progressEvent = new SceneEvents.AsyncLoadProgressEvent
        {
            Scene = this,
            Progress = GetAsyncProgress(),
            LoadedNodes = _asyncProgress.LoadedNodes,
            TotalNodes = _asyncProgress.TotalNodes,
            LoadedResources = _asyncProgress.LoadedResources,
            TotalResources = _asyncProgress.TotalResources
        };
        SendEvent(ref progressEvent);
    }

    private void FinishAsyncLoading()
    {
        if (_asyncProgress.Mode > LoadMode.ResourcesOnly)
        {
            _resolver.Resolve();
            ApplyAttributes();
            FinishLoading(_asyncProgress.File);
        }

        StopAsyncLoading();

        var finishedEvent = new SceneEvents.AsyncLoadFinishedEvent { Scene = this };
        SendEvent(ref finishedEvent);
    }

    private void PreloadResources(AbstractFile file, bool isSceneFile)
    {
        var cache = GetSubsystem<ResourceCache>()!;

        file.ReadUInt();

        var attributes = GetAttributes();
        for (var index = 0; index < attributes.Count; ++index)
        {
            var attribute = attributes[index];
            if (!attribute.ShouldLoad)
                continue;

            file.ReadVariant(attribute.DefaultValue.Type);
        }

        var numComponents = file.ReadVLE();
        for (var index = 0u; index < numComponents; ++index)
        {
            var componentBuffer = new VectorBuffer(file, file.ReadVLE());
            var componentType = componentBuffer.ReadStringHash();
            componentBuffer.ReadUInt();

            var type = ResolveComponentType(componentType);
            if (type is null)
                continue;

            var component = Activator.CreateInstance(type, nonPublic: true) as Component;
            if (component is null)
                continue;

            var componentAttributes = component.GetAttributes();
            for (var attributeIndex = 0; attributeIndex < componentAttributes.Count; ++attributeIndex)
            {
                var attribute = componentAttributes[attributeIndex];
                if (!attribute.ShouldLoad)
                    continue;

                var value = componentBuffer.ReadVariant(attribute.DefaultValue.Type);
                if (value.Type == VariantType.ResourceRef)
                {
                    var resourceRef = value.GetResourceRef();
                    var name = cache.SanitateResourceName(resourceRef.Name);
                    if (cache.BackgroundLoadResource(resourceRef.Type, name))
                    {
                        ++_asyncProgress.TotalResources;
                        _asyncProgress.Resources.Add(new StringHash(name));
                    }
                }
                else if (value.Type == VariantType.ResourceRefList)
                {
                    var resourceRefList = value.GetResourceRefList();
                    foreach (var resourceName in resourceRefList.Names)
                    {
                        var name = cache.SanitateResourceName(resourceName);
                        if (cache.BackgroundLoadResource(resourceRefList.Type, name))
                        {
                            ++_asyncProgress.TotalResources;
                            _asyncProgress.Resources.Add(new StringHash(name));
                        }
                    }
                }
            }
        }

        var numChildren = file.ReadVLE();
        for (var index = 0u; index < numChildren; ++index)
            PreloadResources(file, false);
    }

    private void PreloadResourcesXML(XMLElement element)
    {
        var cache = GetSubsystem<ResourceCache>()!;

        var componentElement = element.GetChild("component");
        while (componentElement is { NotNull: true })
        {
            var componentType = ResolveComponentType(new StringHash(componentElement.GetAttribute("type")));
            if (componentType is not null
                && Activator.CreateInstance(componentType, nonPublic: true) is Component component)
            {
                var attributes = component.GetAttributes();
                var attributeElement = componentElement.GetChild("attribute");
                var startIndex = 0;

                while (attributeElement is { NotNull: true })
                {
                    var attributeName = attributeElement.GetAttribute("name");
                    var index = startIndex;
                    var attempts = attributes.Count;

                    while (attempts != 0)
                    {
                        var attribute = attributes[index];
                        if (attribute.ShouldLoad && string.Equals(attribute.Name, attributeName, StringComparison.Ordinal))
                        {
                            var value = attributeElement.GetVariantValue(attribute.DefaultValue.Type);
                            if (value.Type == VariantType.ResourceRef)
                            {
                                var resourceRef = value.GetResourceRef();
                                var name = cache.SanitateResourceName(resourceRef.Name);
                                if (cache.BackgroundLoadResource(resourceRef.Type, name))
                                {
                                    ++_asyncProgress.TotalResources;
                                    _asyncProgress.Resources.Add(new StringHash(name));
                                }
                            }
                            else if (value.Type == VariantType.ResourceRefList)
                            {
                                var resourceRefList = value.GetResourceRefList();
                                foreach (var resourceName in resourceRefList.Names)
                                {
                                    var name = cache.SanitateResourceName(resourceName);
                                    if (cache.BackgroundLoadResource(resourceRefList.Type, name))
                                    {
                                        ++_asyncProgress.TotalResources;
                                        _asyncProgress.Resources.Add(new StringHash(name));
                                    }
                                }
                            }

                            startIndex = (index + 1) % attributes.Count;
                            break;
                        }

                        index = (index + 1) % attributes.Count;
                        --attempts;
                    }

                    attributeElement = attributeElement.GetNext("attribute");
                }
            }

            componentElement = componentElement.GetNext("component");
        }

        var childElement = element.GetChild("node");
        while (childElement is { NotNull: true })
        {
            PreloadResourcesXML(childElement);
            childElement = childElement.GetNext("node");
        }
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

    private void PreloadResourcesJSON(JSONValue value)
    {
        var cache = GetSubsystem<ResourceCache>()!;

        var componentArray = value.Get("components").GetArray();
        for (var componentIndex = 0; componentIndex < componentArray.Count; ++componentIndex)
        {
            var componentValue = componentArray[componentIndex];
            var componentType = ResolveComponentType(new StringHash(componentValue.Get("type").GetString()));
            if (componentType is not null
                && Activator.CreateInstance(componentType, nonPublic: true) is Component component)
            {
                var attributes = component.GetAttributes();
                var attributesArray = componentValue.Get("attributes").GetArray();
                var startIndex = 0;

                for (var attributeIndex = 0; attributeIndex < attributesArray.Count; ++attributeIndex)
                {
                    var attributeValue = attributesArray[attributeIndex];
                    var attributeName = attributeValue.Get("name").GetString();
                    var index = startIndex;
                    var attempts = attributes.Count;

                    while (attempts != 0)
                    {
                        var attribute = attributes[index];
                        if (attribute.ShouldLoad && string.Equals(attribute.Name, attributeName, StringComparison.Ordinal))
                        {
                            var variant = attributeValue.Get("value").GetVariantValue(attribute.DefaultValue.Type);
                            if (variant.Type == VariantType.ResourceRef)
                            {
                                var resourceRef = variant.GetResourceRef();
                                var name = cache.SanitateResourceName(resourceRef.Name);
                                if (cache.BackgroundLoadResource(resourceRef.Type, name))
                                {
                                    ++_asyncProgress.TotalResources;
                                    _asyncProgress.Resources.Add(new StringHash(name));
                                }
                            }
                            else if (variant.Type == VariantType.ResourceRefList)
                            {
                                var resourceRefList = variant.GetResourceRefList();
                                foreach (var resourceName in resourceRefList.Names)
                                {
                                    var name = cache.SanitateResourceName(resourceName);
                                    if (cache.BackgroundLoadResource(resourceRefList.Type, name))
                                    {
                                        ++_asyncProgress.TotalResources;
                                        _asyncProgress.Resources.Add(new StringHash(name));
                                    }
                                }
                            }

                            startIndex = (index + 1) % attributes.Count;
                            break;
                        }

                        index = (index + 1) % attributes.Count;
                        --attempts;
                    }
                }
            }
        }

        var childrenArray = value.Get("children").GetArray();
        for (var childIndex = 0; childIndex < childrenArray.Count; ++childIndex)
            PreloadResourcesJSON(childrenArray[childIndex]);
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

        using var block = archive.OpenArrayBlock("auxiliary", archive.IsInput ? 0 : components.Count);
        if (archive.IsInput)
        {
            for (var i = 0; i < block.SizeHint; ++i)
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

    private static object? GetNameAttributeDefault() => string.Empty;

    private static object? GetVariablesAttributeDefault() => new StringVariantMap();

    [Serialized("Variables", typeof(StringVariantMap), DefaultSource = nameof(GetVariablesAttributeDefault), Setter = nameof(SetSerializedVariables))]
    private object? GetSerializedVariables()
    {
        Serializable self = this;
        var result = new StringVariantMap();
        foreach (var (key, value) in ((Scene)self).Variables)
            result[key.Reverse()] = Variant.FromObject(value);
        return result;
    }

    private void SetSerializedVariables(object? value)
    {
        Serializable self = this;
        if (value is not StringVariantMap variables)
            return;

        ((Scene)self).SetVariables(variables.Select(static pair =>
            new KeyValuePair<StringHash, object?>(new StringHash(pair.Key), pair.Value.GetObject())));
    }

    private static object? GetUpdateEventsAttributeDefault() => new StringVector(DefaultUpdateEvents);

    private static object? GetLightmapsAttributeDefault() => new ResourceRefList(ObjectReflection<Texture2D>.TypeId);

    [Serialized("Lightmaps", typeof(ResourceRefList), DefaultSource = nameof(GetLightmapsAttributeDefault), Setter = nameof(SetSerializedLightmaps))]
    private object? GetSerializedLightmaps()
    {
        Serializable self = this;
        return new ResourceRefList(ObjectReflection<Texture2D>.TypeId, ((Scene)self)._lightmapNames);
    }

    private void SetSerializedLightmaps(object? value)
    {
        Serializable self = this;
        if (value is not ResourceRefList lightmaps)
            return;

        ((Scene)self)._lightmapNames.Clear();
        ((Scene)self)._lightmapNames.AddRange(lightmaps.Names);
        ((Scene)self).ReloadLightmaps();
    }


    [Serialized("Name", DefaultSource = nameof(GetNameAttributeDefault))]
    private string SerializedName
    {
        get => Name;
        set => SetName(value);
    }
}
