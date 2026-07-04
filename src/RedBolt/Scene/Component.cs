namespace RedBolt;

public abstract class Component
{
    public bool Enabled { get; set; } = true;

    public Node Node { get; private set; } = null!;

    public Scene Scene => Node.Scene;

    public bool IsEnabledEffective => Enabled && Node.IsEnabled;

    public T? GetComponent<T>()
        where T : Component
    {
        return Node.GetComponent<T>();
    }

    internal void Attach(Node node)
    {
        Node = node;
        OnAttached();
    }

    internal void Detach()
    {
        OnDetached();
        Node = null!;
    }

    protected virtual void OnAttached()
    {
    }

    protected virtual void OnDetached()
    {
    }
}
