namespace RedBolt;

public abstract class Component : Animatable
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

    protected internal virtual void OnNodeSet(Node? previousNode, Node? currentNode)
    {
    }

    protected internal virtual void OnNodeSetEnabled(Node node)
    {
    }

    protected internal virtual void OnSceneSet(Scene? previousScene, Scene? scene)
    {
    }

    protected internal virtual void OnSetEnabled()
    {
    }
}
