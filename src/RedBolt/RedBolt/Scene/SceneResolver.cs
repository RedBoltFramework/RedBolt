using Microsoft.Extensions.Logging;

using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Logging;
namespace RedBolt.Scene;

/// <summary>
/// Utility class that resolves node &amp; component IDs after a scene or partial scene load.
/// </summary>
public class SceneResolver : IDisposable
{
    /// <summary>
    /// Construct.
    /// </summary>
    public SceneResolver()
    {
    }

    /// <summary>
    /// Destruct.
    /// </summary>
    public void Dispose()
    {
        _components.Clear();
        _componentLookup.Clear();
        _nodeLookup.Clear();
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
            _nodeLookup[oldId] = new WeakReference<Node>(node);
    }

    /// <summary>
    /// Remember a created component.
    /// </summary>
    public void AddComponent(uint oldId, Component? component)
    {
        if (component is not null)
        {
            _componentLookup[oldId] = new WeakReference<Component>(component);
            _components.Add(new WeakReference<Component>(component));
        }
    }

    /// <summary>
    /// Resolve component and node ID attributes and reset.
    /// </summary>
    public void Resolve()
    {
        // Nodes do not have component or node ID attributes, so only have to go through components
        var noIdAttributes = new HashSet<StringHash>();
        foreach (var componentWeakReference in _components)
        {
            if (!componentWeakReference.TryGetTarget(out var component)
                || noIdAttributes.Contains(component.GetTypeNameHash()))
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
                    using var attrValue = component.GetAttribute(j);
                    var oldNodeId = attrValue.GetUInt();
                    if (oldNodeId != 0)
                    {
                        if (_nodeLookup.TryGetValue(oldNodeId, out var nodeReference)
                            && nodeReference.TryGetTarget(out var node))
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
                    using var attrValue = component.GetAttribute(j);
                    var oldComponentId = attrValue.GetUInt();
                    if (oldComponentId != 0)
                    {
                        if (_componentLookup.TryGetValue(oldComponentId, out var componentReference)
                            && componentReference.TryGetTarget(out var resolvedComponent))
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
                    using var attrValue = component.GetAttribute(j);
                    var ids = attrValue.GetVariantVector();
                    if (ids.Count != 0)
                    {
                        // first index stores number redundantly. This is for editing
                        var numIds = ids[0].GetUInt();
                        using var newIds = new VariantVector { new Variant(numIds) };
                        for (var k = 1; k < ids.Count; ++k)
                        {
                            var oldNodeId = ids[k].GetUInt();
                            if (_nodeLookup.TryGetValue(oldNodeId, out var nodeReference)
                                && nodeReference.TryGetTarget(out var node))
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
    private readonly Dictionary<uint, WeakReference<Node>> _nodeLookup = [];

    /// <summary>
    /// Component by old Id.
    /// </summary>
    private readonly Dictionary<uint, WeakReference<Component>> _componentLookup = [];

    /// <summary>
    /// Components to resolve.
    /// </summary>
    private readonly List<WeakReference<Component>> _components = [];
}
