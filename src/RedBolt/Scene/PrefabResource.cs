using System.IO;
using Microsoft.Extensions.Logging;

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

        var prefabReference = instanceNode.GetOrCreateComponent<PrefabReference>();
        prefabReference.SetPrefab(this);
        var nodePrefab = GetNodePrefab();
        if (!nodePrefab.IsEmpty())
            nodePrefab.Node.Export(instanceNode);

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
        var resourceCache = GetSubsystem<ResourceCache>()!;
        foreach (var component in prefab.Components)
        {
            foreach (var attribute in component.Attributes)
            {
                switch (attribute.Value.Type)
                {
                    case VariantType.ResourceRef:
                    {
                        var resourceRef = attribute.Value.GetResourceRef();
                        resourceCache.BackgroundLoadResource(resourceRef.Type, resourceRef.Name, true, this);
                        break;
                    }
                    case VariantType.ResourceRefList:
                    {
                        var resourceRefList = attribute.Value.GetResourceRefList();
                        foreach (var name in resourceRefList.Names)
                            resourceCache.BackgroundLoadResource(resourceRefList.Type, name, true, this);
                        break;
                    }
                }
            }
        }

        foreach (var child in prefab.Children)
            BackgroundLoadResources(child);
    }

    protected override bool LoadLegacyXML(XMLElement source)
    {
        if (source.Name != "scene")
            return false;

        // This is awful, but we cannot do better because old prefab format has incomplete information.
        var tempScene = new Scene();
        if (!tempScene.Root.LoadXML(source))
            return false;

        CopyPrefab(_prefab, tempScene.Root.GeneratePrefab(includeTemporary: true));
        Log.Logger.LogWarning(
            "Legacy prefab format is used in file '{Name}'! Rename it to *.prefab, open it in the Editor as Scene, and save it normally.",
            Name);
        return true;
    }

    private static void CopyPrefab(NodePrefab destination, NodePrefab source)
    {
        destination.Clear();
        CopySerializable(destination.Node, source.Node);

        foreach (var sourceComponent in source.Components)
        {
            var destinationComponent = new SerializablePrefab();
            CopySerializable(destinationComponent, sourceComponent);
            destination.Components.Add(destinationComponent);
        }

        foreach (var sourceChild in source.Children)
        {
            var destinationChild = new NodePrefab();
            CopyPrefab(destinationChild, sourceChild);
            destination.Children.Add(destinationChild);
        }
    }

    private static void CopySerializable(SerializablePrefab destination, SerializablePrefab source)
    {
        destination.TypeName = source.TypeName;
        destination.Id = source.Id;
        destination.Temporary = source.Temporary;
        foreach (var sourceAttribute in source.Attributes)
        {
            destination.Attributes.Add(new AttributePrefab
            {
                Name = sourceAttribute.Name,
                Value = sourceAttribute.Value
            });
        }
    }
}
