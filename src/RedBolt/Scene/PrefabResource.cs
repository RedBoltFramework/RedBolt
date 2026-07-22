using System.IO;
using Microsoft.Extensions.Logging;

namespace RedBolt;

/// <summary>
/// Prefab resource. Contains representation of nodes and components with attributes, ready to be instantiated.
/// </summary>
public class PrefabResource : SimpleResource
{
    public PrefabResource()
    {
    }

    public override void Dispose()
    {
        _prefab.Dispose();
        base.Dispose();
    }

    public static void RegisterObject(Context context)
    {
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
        instanceNode.SetName(System.IO.Path.GetFileName(GetName())!);

        var prefabReference = instanceNode.GetOrCreateComponent<PrefabReference>();
        prefabReference.SetPrefab(this);

        var nodePrefab = GetNodePrefab();
        if (!nodePrefab.IsEmpty())
            nodePrefab.Node.Export(instanceNode, PrefabLoadFlag.KeepTemporaryState);

        return instanceNode;
    }

    public void NormalizeIds()
    {
        _prefab.NormalizeIds();

        var sceneAttributes = _prefab.Node.Attributes;
        for (var index = sceneAttributes.Count - 1; index >= 0; --index)
        {
            if (!IdAttributes.Contains(sceneAttributes[index].NameHash))
                continue;

            sceneAttributes[index].Dispose();
            sceneAttributes.RemoveAt(index);
        }
    }

    public override void SerializeInBlock(Archive archive)
    {
        // For prefabs, keep as much information as possible, because prefabs shouldn't be too heavy.
        // Can always turn on "compactSave" later.
        const bool compactSave = false;
        const PrefabArchiveFlag flags = PrefabArchiveFlag.None;

        _prefab.SerializeInBlock(archive, flags, compactSave);
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

    public override bool BeginLoad(Deserializer source)
    {
        if (!base.BeginLoad(source))
            return false;

        if (GetAsyncLoadState() == AsyncLoadState.Loading)
            BackgroundLoadResources(_prefab);

        return true;
    }

    private void BackgroundLoadResources(NodePrefab prefab)
    {
        foreach (var component in prefab.Components)
        {
            foreach (var attribute in component.Attributes)
            {
                switch (attribute.Value.Type)
                {
                    case VariantType.ResourceRef:
                    {
                        var resourceRef = attribute.Value.GetResourceRef();
                        GetSubsystem<ResourceCache>()!.BackgroundLoadResource(
                            resourceRef.Type, resourceRef.Name, true, this);
                        break;
                    }
                    case VariantType.ResourceRefList:
                    {
                        var resourceRefList = attribute.Value.GetResourceRefList();
                        foreach (var name in resourceRefList.Names)
                            GetSubsystem<ResourceCache>()!.BackgroundLoadResource(
                                resourceRefList.Type, name, true, this);
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
        using var tempScene = new Scene();
        if (!tempScene.LoadXML(source))
            return false;

        tempScene.Root.GeneratePrefab(_prefab);

        const string helpMessage =
            "To convert prefab into new format:\n"
            + "1. Rename file to *.prefab;\n"
            + "2. Open it in the Editor as Scene (LMB double click on resource);\n"
            + "3. Save it normally ('Save' icon or menu item).\n";

        Log.GetLogger().LogWarning(
            "Legacy prefab format is used in file '{Name}'! {HelpMessage}", GetName(), helpMessage);
        return true;
    }

    private static readonly HashSet<StringHash> IdAttributes = ["Next Node ID", "Next Component ID"];
    private readonly NodePrefab _prefab = new();
}
