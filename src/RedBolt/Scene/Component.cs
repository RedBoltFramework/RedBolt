namespace RedBolt;

/// <summary>Autoremove is used by some components for automatic removal from the scene hierarchy upon completion of an action, for example sound or particle effect.</summary>
public enum AutoRemoveMode
{
    Disabled = 0,
    Component,
    Node
}

/// <summary>Base class for components. Components can be created to scene nodes.</summary>
public class Component : Serializable
{
    /// <summary>Construct.</summary>
    public Component()
    {
    }

    /// <summary>Destruct.</summary>
    public override void Dispose()
    {
        base.Dispose();
    }

    /// <summary>Handle enabled/disabled state change.</summary>
    public virtual void OnSetEnabled()
    {
    }

    /// <summary>
    /// Evaluate effective attribute scope.
    /// It is a hint for the Editor to know what is affected by the component addition/removal
    /// so it can generate optimal undo/redo actions.
    /// </summary>
    public AttributeScopeHint GetEffectiveScopeHint()
    {
        // Don't use Serializable.GetReflection() to avoid effect from overrides.
        // GetEffectiveScopeHint() should not depend on the state of the Component
        var reflection = Context.GetReflection(GetTypeNameHash());
        return reflection?.GetEffectiveScopeHint() ?? AttributeScopeHint.Attribute;
    }

    /// <summary>Save as binary data. Return true if successful.</summary>
    public override bool Save(Serializer destination)
    {
        ArgumentNullException.ThrowIfNull(destination);

        // Write type and ID
        if (!destination.WriteStringHash(new StringHash(GetTypeName())))
            return false;
        if (!destination.WriteUInt(_id))
            return false;

        // Write attributes
        return base.Save(destination);
    }

    /// <summary>Save as XML data. Return true if successful.</summary>
    public override bool SaveXML(XMLElement destination)
    {
        // Write type and ID
        if (!destination.SetString("type", GetTypeName()))
            return false;
        if (!destination.SetUInt("id", _id))
            return false;

        // Write attributes
        return base.SaveXML(destination);
    }

    /// <summary>Save as JSON data. Return true if successful.</summary>
    public override bool SaveJSON(JSONValue destination)
    {
        // Write type and ID
        destination.Set("type", new JSONValue(GetTypeName()));
        destination.Set("id", new JSONValue(_id));

        // Write attributes
        return base.SaveJSON(destination);
    }

    /// <summary>Return the depended on nodes to order network updates.</summary>
    public virtual void GetDependencyNodes(ICollection<Node> destination)
    {
    }

    /// <summary>Visualize the component as debug geometry.</summary>
    public virtual void DrawDebugGeometry(DebugRenderer? debug, bool depthTest)
    {
    }

    /// <summary>
    /// Return whether the component provides auxiliary data.
    /// Only components directly attached to the Scene can have auxiliary data.
    /// Auxiliary data is not supported in prefabs.
    /// </summary>
    public virtual bool HasAuxiliaryData() => false;

    /// <summary>Serialize auxiliary data from/to the current block of the archive. May throw ArchiveException.</summary>
    public virtual void SerializeAuxiliaryData(Archive archive)
    {
    }

    /// <summary>Set enabled/disabled state.</summary>
    public void SetEnabled(bool enable)
    {
        if (enable != _enabled)
        {
            _enabled = enable;
            OnSetEnabled();

            // Send change event for the component
            var scene = Scene;
            if (scene is not null)
            {
                var eventData = new SceneEvents.ComponentEnabledChangedEvent
                {
                    Scene = scene,
                    Node = _node,
                    Component = this
                };
                scene.SendEvent(ref eventData);
            }
        }
    }

    /// <summary>Remove from the scene node. If no other shared pointer references exist, causes immediate deletion.</summary>
    public void Remove()
    {
        _node?.RemoveComponent(this);
    }

    /// <summary>Return ID.</summary>
    public uint Id => _id;

    /// <summary>Return scene node.</summary>
    public Node? Node => _node;

    /// <summary>Return the scene the node belongs to.</summary>
    public Scene? Scene => _node?.Scene;

    /// <summary>Return whether is enabled.</summary>
    public bool Enabled
    {
        get => _enabled;
        set => SetEnabled(value);
    }

    /// <summary>Return whether is effectively enabled (node is also enabled).</summary>
    public bool IsEnabledEffective => _enabled && _node is not null && _node.IsEnabled;

    /// <summary>Return full component name for debugging. Unique for each component in the scene. Slow!</summary>
    public string GetFullNameDebug()
    {
        var fullName = _node is not null ? $"{_node.GetFullNameDebug()}/({_node.GetComponentIndex(this)})" : string.Empty;
        fullName += GetTypeName();
        return fullName;
    }

    /// <summary>Return component in the same scene node by type. If there are several, returns the first.</summary>
    public Component? GetComponent(Type type)
    {
        return _node?.GetComponent(type);
    }

    /// <summary>Template version of returning a component in the same scene node by type.</summary>
    public T? GetComponent<T>()
        where T : Component
    {
        return _node?.GetComponent<T>();
    }

    /// <summary>Return index of this component in the node.</summary>
    public int GetIndexInParent()
    {
        return _node is not null ? checked((int)_node.GetComponentIndex(this)) : int.MaxValue;
    }

    /// <summary>Handle scene node being assigned at creation.</summary>
    protected virtual void OnNodeSet(Node? previousNode, Node? currentNode)
    {
    }

    /// <summary>Handle scene being assigned. This may happen several times during the component's lifetime. Scene-wide subsystems and events are subscribed to here.</summary>
    protected internal virtual void OnSceneSet(Scene? previousScene, Scene? scene)
    {
    }

    /// <summary>Handle scene node transform dirtied.</summary>
    protected internal virtual void OnMarkedDirty(Node node)
    {
    }

    /// <summary>Handle scene node enabled status changing.</summary>
    protected internal virtual void OnNodeSetEnabled(Node node)
    {
    }

    /// <summary>Set ID. Called by Scene.</summary>
    internal void SetID(uint id)
    {
        _id = id;
    }

    /// <summary>Set scene node. Called by Node when creating the component.</summary>
    internal void SetNode(Node? node)
    {
        var previousNode = _node;
        _node = node;
        OnNodeSet(previousNode, _node);
    }

    /// <summary>Return a component from the scene root that sends out fixed update events (either PhysicsWorld or PhysicsWorld2D). Return null if neither exists.</summary>
    protected Component? GetFixedUpdateSource()
    {
        Component? result = null;
        return result;
    }

    /// <summary>Perform autoremove. Called by subclasses. Caller should keep a weak pointer to itself to check whether was actually removed, and return immediately without further member operations in that case.</summary>
    protected void DoAutoRemove(AutoRemoveMode mode)
    {
        switch (mode)
        {
            case AutoRemoveMode.Component:
                Remove();
                return;

            case AutoRemoveMode.Node:
                if (_node is not null)
                    _node.Remove();
                return;

            default:
                return;
        }
    }

    /// <summary>Scene node.</summary>
    protected Node? _node;
    /// <summary>Unique ID within the scene.</summary>
    protected uint _id;
    /// <summary>Network update queued flag.</summary>
    protected bool _networkUpdate;
    /// <summary>Enabled flag.</summary>
    protected bool _enabled = true;
}
