namespace RedBolt;

public abstract class Resource
{
    protected Resource(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }

    public string Name { get; }
}
