namespace RedBolt;

public abstract class Component : Serializable
{
    private bool enabled = true;
    private Node? node;

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
        get => enabled;
        set => SetEnabled(value);
    }

    public uint Id { get; private set; }

    public bool IsAttached => node is not null;

    public Node Node => node ?? throw new InvalidOperationException("Component is not attached to a node.");

    public Scene Scene => Node.Scene;

    public bool IsEnabledEffective => Enabled && (node?.IsEnabled ?? false);

    public uint GetID()
    {
        return Id;
    }

    public T? GetComponent<T>()
        where T : Component
    {
        return node?.GetComponent<T>();
    }

    public uint GetIndexInParent()
    {
        return node?.GetComponentIndex(this) ?? uint.MaxValue;
    }

    public void Remove()
    {
        node?.RemoveComponent(this);
    }

    public void SetEnabled(bool enable)
    {
        if (enabled == enable)
            return;

        enabled = enable;
        OnSetEnabled();
    }

    internal void Attach(Node node)
    {
        var previousNode = this.node;
        var previousScene = previousNode?.Scene;

        this.node = node;
        OnNodeSet(previousNode, node);
        OnSceneSet(previousScene, node.Scene);
        OnAttached();
    }

    internal void Detach()
    {
        var previousNode = node;
        var previousScene = previousNode?.Scene;

        OnDetached();
        node = null;
        OnNodeSet(previousNode, null);
        OnSceneSet(previousScene, null);
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

    protected virtual void OnSetEnabled()
    {
    }
}
