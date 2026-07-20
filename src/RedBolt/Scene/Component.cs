namespace RedBolt;

public enum AutoRemoveMode { Disabled, Component, Node }

public abstract class Component : Serializable
{
    private bool _enabled = true;
    private Node? _node;

    protected Component()
    {
        RegisterAttribute(
            "Is Enabled",
            typeof(bool),
            self => ((Component)self).Enabled,
            (self, value) => ((Component)self).SetEnabled(value is bool enabledValue && enabledValue),
            true);
    }

    public bool Enabled
    {
        get => _enabled;
        set => SetEnabled(value);
    }

    public uint Id { get; private set; }

    public bool IsAttached => _node is not null;

    public Node Node => _node ?? throw new InvalidOperationException("Component is not attached to a node.");

    /// <summary>Return scene node.</summary>
    public Node? GetNode() => _node;

    public Scene? Scene => _node?.Scene;

    public bool IsEnabledEffective => Enabled && (_node?.IsEnabled ?? false);

    public uint GetID()
    {
        return Id;
    }

    public T? GetComponent<T>()
        where T : Component
    {
        return _node?.GetComponent<T>();
    }

    public uint GetIndexInParent()
    {
        return _node?.GetComponentIndex(this) ?? uint.MaxValue;
    }

    public AttributeScopeHint GetEffectiveScopeHint()
    {
        var result = AttributeScopeHint.Attribute;
        for (var i = 0; i < Attributes.Count; ++i)
        {
            var hint = Attributes[i].ScopeHint;
            if (i == 0 || hint > result)
                result = hint;
        }

        return result;
    }

    public override bool Save(Stream destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        var header = new VectorBuffer();
        if (!header.WriteStringHash(new StringHash(GetTypeName())) || !header.WriteUInt(Id)) return false;
        var attributes = new VectorBuffer();
        if (!base.Save(attributes)) return false;
        destination.Write(header.Buffer[..checked((int)header.Size)]);
        destination.Write(attributes.Buffer[..checked((int)attributes.Size)]);
        return true;
    }

    public override bool Save(Serializer destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (!destination.WriteStringHash(new StringHash(GetTypeName())) || !destination.WriteUInt(Id)) return false;
        return base.Save(destination);
    }

    public override bool SaveXML(XMLElement destination)
    {
        return destination.SetString("type", GetTypeName()) && destination.SetUInt("id", Id) && base.SaveXML(destination);
    }

    public override bool SaveJSON(JSONValue destination)
    {
        destination.Set("type", new JSONValue(GetTypeName()));
        destination.Set("id", new JSONValue(Id));
        return base.SaveJSON(destination);
    }

    public virtual void GetDependencyNodes(ICollection<Node> destination) { }

    public virtual void DrawDebugGeometry(DebugRenderer? debug, bool depthTest) { }

    public virtual bool HasAuxiliaryData() => false;

    public virtual void SerializeAuxiliaryData(Archive archive) { }

    public string GetFullNameDebug() => (_node is null ? string.Empty : $"{_node.GetFullNameDebug()}/({GetIndexInParent()})") + GetTypeName();

    public void Remove()
    {
        _node?.RemoveComponent(this);
    }

    public void SetEnabled(bool enable)
    {
        if (_enabled == enable)
            return;

        _enabled = enable;
        OnSetEnabled();

        if (_node is { Scene: { } scene })
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

    internal void Attach(Node node)
    {
        var previousNode = this._node;

        this._node = node;
        OnNodeSet(previousNode, node);
        OnAttached();
    }

    internal void Detach()
    {
        var previousNode = _node;

        OnDetached();
        _node = null;
        OnNodeSet(previousNode, null);
    }

    internal void SetId(uint id)
    {
        Id = id;
    }

    protected virtual void OnAttached()
    {
    }

    protected virtual void OnDetached()
    {
    }

    protected internal virtual void OnMarkedDirty(Node node)
    {
    }

    protected virtual void OnNodeSet(Node? previousNode, Node? currentNode)
    {
    }

    protected internal virtual void OnNodeSetEnabled(Node node)
    {
    }

    protected internal virtual void OnSceneSet(Scene? previousScene, Scene? scene)
    {
    }

    public virtual void OnSetEnabled()
    {
    }

    protected void DoAutoRemove(AutoRemoveMode mode)
    {
        if (mode == AutoRemoveMode.Component) Remove();
        else if (mode == AutoRemoveMode.Node) _node?.Remove();
    }

}
