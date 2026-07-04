namespace RedBolt;

public abstract class Component
{
    public Node Node { get; private set; } = null!;

    internal void Attach(Node node)
    {
        Node = node;
        OnAttached();
    }

    protected virtual void OnAttached()
    {
    }
}
