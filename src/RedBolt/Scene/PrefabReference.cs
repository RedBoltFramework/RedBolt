namespace RedBolt;

/// <summary>Component that instantiates prefab resource into the parent Node.</summary>
public sealed class PrefabReference : Component
{
    public PrefabReference()
    {
        RegisterAttribute(
            "Prefab",
            typeof(ResourceRef),
            self => ((PrefabReference)self).GetPrefabAttr(),
            (self, value) => ((PrefabReference)self).SetPrefabAttr(value is ResourceRef resourceRef ? resourceRef : ResourceRef.Empty),
            ResourceRef.Empty,
            AttributeMode.Default);
        RegisterAttribute(
            "Path",
            typeof(string),
            self => ((PrefabReference)self).GetPath(),
            (self, value) => ((PrefabReference)self).SetPath(value as string ?? string.Empty),
            string.Empty,
            AttributeMode.Default);
    }

    public static void RegisterObject(Context context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.AddFactoryReflection<PrefabReference>(ObjectCategory.Category_Scene);

        // URHO3D_ACTION_STATIC_LABEL("Inline", InlineConservative, "Convert prefab reference to nodes and components");
        // URHO3D_ACTION_STATIC_LABEL("Inline+", InlineAggressive, "Same as Inline. Also converts all temporary objects to persistent");
        // URHO3D_ACTION_STATIC_LABEL("Commit", CommitChanges, "Commit changes in this instance to the prefab resource");
    }

    /// <summary>Apply attribute changes that can not be applied immediately. Called after scene load or a network update.</summary>
    public override void ApplyAttributes()
    {
        if (_prefabDirty)
        {
            _prefabDirty = false;
            CreateInstance(true);
        }
    }

    /// <summary>Attributes.</summary>
    public void SetPrefab(PrefabResource? prefab, string path = "", bool createInstance = true,
        PrefabInstanceFlag instanceFlags = PrefabInstanceFlag.None)
    {
        if (ReferenceEquals(prefab, _prefab) && path == _path)
            return;

        if (_prefab is not null)
            _prefab.ReloadFinished -= OnPrefabReloadFinished;

        _prefab = prefab;
        _path = path;

        if (_prefab is not null)
            _prefabRef = new ResourceRef("PrefabResource", _prefab.Name);
        else
            _prefabRef = ResourceRef.Empty;

        if (_prefab is not null)
            _prefab.ReloadFinished += OnPrefabReloadFinished;
        if (createInstance)
            CreateInstance(false, instanceFlags);
    }

    public PrefabResource? GetPrefab() => _prefab;

    public void SetPath(string path)
    {
        if (_path != path)
            SetPrefab(_prefab, path, true);
    }

    public string GetPath() => _path;

    public void SetPrefabAttr(ResourceRef prefab)
    {
        if (string.IsNullOrEmpty(prefab.Name))
        {
            SetPrefab(null, _path, false);
        }
        else
        {
            var cache = GetSubsystem<ResourceCache>();
            SetPrefab(cache?.GetResourceOrNull<PrefabResource>(prefab.Name), _path, false);
        }

        _prefabRef = prefab;
        MarkPrefabDirty();
    }

    public ResourceRef GetPrefabAttr() => _prefabRef;

    /// <summary>Make all prefab nodes not temporary and remove component.</summary>
    public void Inline(PrefabInlineFlag flags)
    {
        if (Node is null)
            return;

        var node = Node;
        _instanceNode = null;
        Remove();

        // Some temporary components and children are spawned by the prefab itself.
        // Other temporary components and children may be spawned by the components in prefab.
        if ((flags & PrefabInlineFlag.KeepOtherTemporary) != 0)
        {
            var temporaryComponentIndex = 0u;
            foreach (var component in node.Components)
            {
                // Break when first N temporary components are converted
                if (temporaryComponentIndex >= _numInstanceComponents)
                    break;

                // Skip persistent components
                if (!component.IsTemporary)
                    continue;

                component.SetTemporary(false);
                ++temporaryComponentIndex;
            }

            var temporaryChildIndex = 0u;
            foreach (var child in node.Children)
            {
                // Break when first N temporary children are converted
                if (temporaryChildIndex >= _numInstanceChildren)
                    break;

                // Skip persistent children
                if (!child.IsTemporary)
                    continue;

                child.SetTemporary(false);
                ++temporaryChildIndex;
            }
        }
        else
        {
            foreach (var component in node.Components)
                component.SetTemporary(false);
            foreach (var child in node.Children)
                child.SetTemporary(false);
        }
    }

    public void InlineConservative() => Inline(PrefabInlineFlag.KeepOtherTemporary);

    public void InlineAggressive() => Inline(PrefabInlineFlag.None);

    /// <summary>Commit prefab changes to the resource.</summary>
    public void CommitChanges()
    {
        if (!string.IsNullOrEmpty(_path))
        {
            // URHO3D_LOGERROR("Cannot commit changes to a prefab slice");
            return;
        }

        if (Node is null || _prefab is null)
            return;

        var originalNodePrefab = GetNodePrefab();
        var newNodePrefab = new NodePrefab();
        var writer = new PrefabWriterToMemory(
            newNodePrefab,
            PrefabSaveFlag.EnumsAsStrings | PrefabSaveFlag.Prefab | PrefabSaveFlag.SaveTemporary);
        if (!Node.Save(writer))
            return;
        newNodePrefab.Node.Attributes.Clear();
        foreach (var attribute in originalNodePrefab.Node.Attributes)
            newNodePrefab.Node.Attributes.Add(attribute);

        for (var index = newNodePrefab.Components.Count - 1; index >= 0; --index)
        {
            if (!Node.Components[index].IsTemporary)
                newNodePrefab.Components.RemoveAt(index);
        }

        for (var index = newNodePrefab.Children.Count - 1; index >= 0; --index)
        {
            if (!Node.Children[index].IsTemporary)
                newNodePrefab.Children.RemoveAt(index);
        }

        // Create and save resource.
        var newResource = new PrefabResource(_prefab.Name);
        var scenePrefab = newResource.GetMutableScenePrefab();
        CopyPrefab(scenePrefab, _prefab.GetScenePrefab());

        var nodeDestination = newResource.GetMutableNodePrefab();
        CopyPrefab(nodeDestination, newNodePrefab);
        newResource.NormalizeIds();
        if (!string.IsNullOrEmpty(_prefab.GetAbsoluteFileName()))
            newResource.SaveFile(_prefab.GetAbsoluteFileName());
    }

    private static void CopyPrefab(NodePrefab destination, NodePrefab source)
    {
        destination.Clear();
        destination.Node.TypeName = source.Node.TypeName;
        destination.Node.Id = source.Node.Id;
        destination.Node.Temporary = source.Node.Temporary;
        foreach (var attribute in source.Node.Attributes)
            destination.Node.Attributes.Add((AttributePrefab)attribute.Clone());
        foreach (var component in source.Components)
            destination.Components.Add((SerializablePrefab)component.Clone());
        foreach (var child in source.Children)
            destination.Children.Add((NodePrefab)child.Clone());
    }

    public override void OnSetEnabled()
    {
        if (!IsEnabledEffective)
            RemoveInstance();
        else if (!ReferenceEquals(_instanceNode, Node))
            CreateInstance(true);
    }

    private NodePrefab GetNodePrefab()
    {
        if (Node is not null && _prefab is not null)
            return _prefab.GetNodePrefabSlice(_path);

        return NodePrefab.Empty;
    }

    private bool IsInstanceMatching(Node node, NodePrefab nodePrefab, bool temporaryOnly)
    {
        if (!AreComponentsMatching(node, nodePrefab.Components, temporaryOnly))
            return false;
        if (!AreChildrenMatching(node, nodePrefab.Children, temporaryOnly))
            return false;
        return true;
    }

    private bool AreComponentsMatching(Node node, IReadOnlyList<SerializablePrefab> componentPrefabs, bool temporaryOnly)
    {
        var index = 0;
        foreach (var component in node.Components)
        {
            // Ignore extras, they may have been spawned by other components
            if (index >= componentPrefabs.Count)
                return true;
            if (temporaryOnly && !component.IsTemporary)
                continue;
            if (component.GetTypeName() != componentPrefabs[index].TypeName)
                return false;
            ++index;
        }
        return index >= componentPrefabs.Count;
    }

    private bool AreChildrenMatching(Node node, IReadOnlyList<NodePrefab> childPrefabs, bool temporaryOnly)
    {
        var index = 0;
        foreach (var child in node.Children)
        {
            // Ignore extras, they may have been spawned by other components
            if (index >= childPrefabs.Count)
                return true;
            if (temporaryOnly && !child.IsTemporary)
                continue;
            if (!IsInstanceMatching(child, childPrefabs[index], false /* ignore temporary flag */))
                return false;
            ++index;
        }
        return index >= childPrefabs.Count;
    }

    private void ExportInstance(Node node, NodePrefab nodePrefab, bool temporaryOnly)
    {
        ExportComponents(node, nodePrefab.Components, temporaryOnly);
        ExportChildren(node, nodePrefab.Children, temporaryOnly);
    }

    private void ExportComponents(Node node, IReadOnlyList<SerializablePrefab> componentPrefabs, bool temporaryOnly)
    {
        var index = 0;
        foreach (var component in node.Components)
        {
            // Ignore extras, they may have been spawned by other components
            if (index >= componentPrefabs.Count)
                return;
            if (temporaryOnly && !component.IsTemporary)
                continue;
            componentPrefabs[index].Export(component, PrefabLoadFlag.KeepTemporaryState);
            ++index;
        }
    }

    private void ExportChildren(Node node, IReadOnlyList<NodePrefab> childPrefabs, bool temporaryOnly)
    {
        var index = 0;
        foreach (var child in node.Children)
        {
            // Ignore extras, they may have been spawned by other components
            if (index >= childPrefabs.Count)
                return;
            if (temporaryOnly && !child.IsTemporary)
                continue;
            ExportInstance(child, childPrefabs[index], false /* ignore temporary flag */);
            ++index;
        }
    }

    private void RemoveTemporaryComponents(Node node)
    {
        for (var index = node.Components.Count - 1; index >= 0; --index)
        {
            var component = node.Components[index];
            if (!component.IsTemporary)
                continue;

            if (!ReferenceEquals(component, this))
                node.RemoveComponent(component);
            else
            {
                // URHO3D_LOGWARNING("PrefabReference component should not be temporary");
                component.SetTemporary(false);
            }
        }
    }

    private void RemoveTemporaryChildren(Node node)
    {
        for (var index = node.Children.Count - 1; index >= 0; --index)
        {
            var child = node.Children[index];
            if (child.IsTemporary)
                node.RemoveChild(child);
        }
    }

    private void RemoveInstance()
    {
        if (_instanceNode is not null)
        {
            RemoveTemporaryComponents(_instanceNode);
            RemoveTemporaryChildren(_instanceNode);
        }
        _instanceNode = null;
    }

    private bool TryCreateInplace()
    {
        var nodePrefab = GetNodePrefab();
        if (nodePrefab.IsEmpty() || Node is null)
            return false;
        if (!IsInstanceMatching(Node, nodePrefab, true))
            return false;
        ExportInstance(Node, nodePrefab, true);
        return true;
    }

    private void InstantiatePrefab(NodePrefab nodePrefab, PrefabInstanceFlag instanceFlags)
    {
        if (Node is null)
            return;

        var node = Node;
        var flags = PrefabLoadFlag.KeepExistingComponents | PrefabLoadFlag.KeepExistingChildren
            | PrefabLoadFlag.LoadAsTemporary | PrefabLoadFlag.IgnoreRootAttributes;
        var reader = new PrefabReaderFromMemory(nodePrefab);
        node.Load(reader, flags);

        if (instanceFlags != PrefabInstanceFlag.None)
        {
            foreach (var attribute in nodePrefab.Node.Attributes)
            {
                var flag = attribute.Name switch
                {
                    "Scale" => PrefabInstanceFlag.UpdateScale,
                    "Position" => PrefabInstanceFlag.UpdatePosition,
                    "Rotation" => PrefabInstanceFlag.UpdateRotation,
                    "Tags" => PrefabInstanceFlag.UpdateTags,
                    "Name" => PrefabInstanceFlag.UpdateName,
                    "Variables" => PrefabInstanceFlag.UpdateVariables,
                    _ => PrefabInstanceFlag.None,
                };
                if (flag != PrefabInstanceFlag.None && (instanceFlags & flag) != 0)
                    node.SetAttribute(attribute.Name, attribute.Value.GetObject());
            }
        }
    }

    private void CreateInstance(bool tryInplace = false, PrefabInstanceFlag instanceFlags = PrefabInstanceFlag.None)
    {
        // Remove existing instance if moved to another node
        if (_instanceNode is not null && !ReferenceEquals(_instanceNode, Node))
            RemoveInstance();

        // Cannot spawn instance without node
        if (Node is null)
            return;

        var nodePrefab = GetNodePrefab();
        _instanceNode = Node;
        _numInstanceComponents = (uint)nodePrefab.Components.Count;
        _numInstanceChildren = (uint)nodePrefab.Children.Count;

        // Try to create inplace first
        if (tryInplace && TryCreateInplace())
            return;

        RemoveTemporaryComponents(Node);
        RemoveTemporaryChildren(Node);
        InstantiatePrefab(nodePrefab, instanceFlags);
    }

    private void MarkPrefabDirty()
    {
        _prefabDirty = true;
    }

    private void OnPrefabReloadFinished(Resource resource)
    {
        CreateInstance();
    }

    private PrefabResource? _prefab;
    private ResourceRef _prefabRef = ResourceRef.Empty;
    private string _path = string.Empty;
    private bool _prefabDirty;
    private Node? _instanceNode;
    private uint _numInstanceComponents;
    private uint _numInstanceChildren;
}
