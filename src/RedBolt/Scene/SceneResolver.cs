using Microsoft.Extensions.Logging;

namespace RedBolt;

/// <summary>
/// Utility class that resolves node &amp; component IDs after a scene or partial scene load.
/// </summary>
public sealed class SceneResolver
{
    /// <summary>
    /// Construct.
    /// </summary>
    public SceneResolver()
    {
    }

    /// <summary>
    /// Reset. Clear all remembered nodes and components.
    /// </summary>
    public void Reset()
    {
        _nodeLookup.Clear();
        _componentLookup.Clear();
    }

    /// <summary>
    /// Remember a created node.
    /// </summary>
    public void AddNode(uint oldId, Node? node)
    {
        if (node is not null)
            _nodeLookup[oldId] = node;
    }

    /// <summary>
    /// Remember a created component.
    /// </summary>
    public void AddComponent(uint oldId, Component? component)
    {
        if (component is not null)
        {
            _componentLookup[oldId] = component;
            _components.Add(component);
        }
    }

    /// <summary>
    /// Resolve component and node ID attributes and reset.
    /// </summary>
    public void Resolve()
    {
        // Nodes do not have component or node ID attributes, so only have to go through components
        var noIdAttributes = new HashSet<StringHash>();
        foreach (var component in _components)
        {
            if (component is null || noIdAttributes.Contains(component.GetTypeNameHash()))
                continue;

            var hasIdAttributes = false;
            IReadOnlyList<AttributeInfo>? attributes = component.GetAttributes();
            if (attributes is null)
            {
                noIdAttributes.Add(component.GetTypeNameHash());
                continue;
            }

            for (var j = 0; j < attributes.Count; ++j)
            {
                var info = attributes[j];
                if ((info.Mode & AttributeMode.NodeId) != 0)
                {
                    hasIdAttributes = true;
                    var oldNodeId = component.GetAttribute(j) is uint value ? value : 0u;
                    if (oldNodeId != 0)
                    {
                        if (_nodeLookup.TryGetValue(oldNodeId, out var node) && node is not null)
                        {
                            var newNodeId = node.GetID();
                            component.SetAttribute(j, newNodeId);
                        }
                        else
                            Log.GetLogger().LogWarning("Could not resolve node ID {OldNodeId}", oldNodeId);
                    }
                }
                else if ((info.Mode & AttributeMode.ComponentId) != 0)
                {
                    hasIdAttributes = true;
                    var oldComponentId = component.GetAttribute(j) is uint value ? value : 0u;
                    if (oldComponentId != 0)
                    {
                        if (_componentLookup.TryGetValue(oldComponentId, out var resolvedComponent) && resolvedComponent is not null)
                        {
                            var newComponentId = resolvedComponent.Id;
                            component.SetAttribute(j, newComponentId);
                        }
                        else
                            Log.GetLogger().LogWarning("Could not resolve component ID {OldComponentId}", oldComponentId);
                    }
                }
                else if ((info.Mode & AttributeMode.NodeIdVector) != 0)
                {
                    hasIdAttributes = true;
                    var attrValue = component.GetAttribute(j) is VariantVector value ? value : new VariantVector();
                    if (attrValue.Count != 0)
                    {
                        // first index stores number redundantly. This is for editing
                        var numIds = attrValue[0].GetUInt();
                        var newIds = new VariantVector { new Variant(numIds) };
                        for (var k = 1; k < attrValue.Count; ++k)
                        {
                            var oldNodeId = attrValue[k].GetUInt();
                            if (_nodeLookup.TryGetValue(oldNodeId, out var node) && node is not null)
                                newIds.Add(new Variant(node.GetID()));
                            else
                            {
                                newIds.Add(new Variant(0u));
                                Log.GetLogger().LogWarning("Could not resolve node ID {OldNodeId}", oldNodeId);
                            }
                        }

                        component.SetAttribute(j, newIds);
                    }
                }
            }

            if (!hasIdAttributes)
                noIdAttributes.Add(component.GetTypeNameHash());
        }

        // Attributes have been resolved, so no need to remember nodes after this
        Reset();
    }

    /// <summary>
    /// Node by old Id.
    /// </summary>
    private readonly Dictionary<uint, Node?> _nodeLookup = [];

    /// <summary>
    /// Component by old Id.
    /// </summary>
    private readonly Dictionary<uint, Component?> _componentLookup = [];

    /// <summary>
    /// Components to resolve.
    /// </summary>
    private readonly List<Component?> _components = [];
}
