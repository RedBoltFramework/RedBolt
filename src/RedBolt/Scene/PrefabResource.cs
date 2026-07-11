using System.IO;

namespace RedBolt;

/// <summary>
/// Prefab resource. Contains representation of nodes and components with attributes, ready to be instantiated.
/// </summary>
public sealed class PrefabResource : SimpleResource
{
    private readonly NodePrefab _prefab = new();

    public PrefabResource()
        : this(string.Empty)
    {
    }

    public PrefabResource(string name)
        : base(name)
    {
    }

    public static void RegisterObject(Context context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.AddFactoryReflection<PrefabResource>();
    }

    /// <summary>
    /// Instantiate prefab reference into a scene or node.
    /// </summary>
    /// <param name="parentNode">Parent node for the instance.</param>
    /// <param name="inplace">Whether to instantiate directly into the parent node.</param>
    /// <returns>The instantiated node.</returns>
    public Node? InstantiateReference(Node? parentNode, bool inplace = false)
    {
        var instanceNode = inplace ? parentNode! : parentNode!.CreateChild();
        instanceNode.SetName(System.IO.Path.GetFileName(GetName()) ?? string.Empty);

        // TODO: port PrefabReference creation and assignment once the managed component exists.
        // auto prefabReference = instanceNode->GetOrCreateComponent<PrefabReference>();
        // prefabReference->SetPrefab(this);
        var nodePrefab = GetNodePrefab();
        if (!nodePrefab.IsEmpty())
            instanceNode.LoadPrefab(nodePrefab);

        return instanceNode;
    }

    public void NormalizeIds()
    {
        _prefab.NormalizeIds();

        var sceneAttributes = _prefab.Node.Attributes;
        sceneAttributes.RemoveAll(static attribute => attribute.Name is "Next Node ID" or "Next Component ID");
    }

    public override void SerializeInBlock(Archive archive)
    {
        // For prefabs, keep as much information as possible, because prefabs shouldn't be too heavy.
        // Can always turn on "compactSave" later.
        const bool compactSave = false;
        _ = compactSave;
        // TODO: port PrefabArchiveFlag and compactSave overloads when NodePrefab exposes them.
        _prefab.SerializeInBlock(archive);
    }

    public NodePrefab GetScenePrefab() => _prefab;

    public NodePrefab GetMutableScenePrefab() => _prefab;

    public NodePrefab GetNodePrefab()
    {
        return _prefab.Children.Count != 0 ? _prefab.Children[0] : NodePrefab.Empty;
    }

    public NodePrefab GetMutableNodePrefab()
    {
        if (_prefab.Children.Count == 0)
            _prefab.Children.Add(new NodePrefab());

        return _prefab.Children[0];
    }

    public NodePrefab GetNodePrefabSlice(string path)
    {
        return GetNodePrefab().FindChild(path);
    }

    public override bool BeginLoad(Stream source)
    {
        if (!base.BeginLoad(source))
            return false;

        if (GetAsyncLoadState() == AsyncLoadState.Loading)
            BackgroundLoadResources(_prefab);

        return true;
    }

    private void BackgroundLoadResources(NodePrefab prefab)
    {
        // TODO: port BackgroundLoadResources once ResourceCache exposes BackgroundLoadResource.
        // C++ traverses resource-reference attributes and calls:
        // GetSubsystem<ResourceCache>()->BackgroundLoadResource(resourceType, resourceName, true, this);
        foreach (var child in prefab.Children)
            BackgroundLoadResources(child);
    }

    protected override bool LoadLegacyXML(XMLElement source)
    {
        if (source.Name != "scene")
            return false;

        // This is awful, but we cannot do better because old prefab format has incomplete information.
        // TODO: port temporary Scene loading, GeneratePrefab, and the conversion warning once the managed XML scene loader exists.
        // auto tempScene = MakeShared<Scene>(context_);
        // if (!tempScene->LoadXML(source))
        //     return false;
        // tempScene->GeneratePrefab(prefab_);
        return false;
    }
}
