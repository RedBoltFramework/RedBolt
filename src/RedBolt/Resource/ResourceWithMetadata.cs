namespace RedBolt;

public abstract class ResourceWithMetadata : Resource
{
    private readonly Dictionary<string, object?> metadata = new(StringComparer.Ordinal);

    protected ResourceWithMetadata(string name)
        : base(name)
    {
    }

    public IReadOnlyDictionary<string, object?> Metadata => metadata;

    public IReadOnlyList<string> MetadataKeys => metadata.Keys.ToArray();

    public void AddMetadata(string name, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        metadata[name] = value;
    }

    public void CopyMetadata(ResourceWithMetadata source)
    {
        ArgumentNullException.ThrowIfNull(source);
        RemoveAllMetadata();
        foreach (var item in source.metadata)
            metadata.Add(item.Key, item.Value);
    }

    public object? GetMetadata(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return metadata.GetValueOrDefault(name);
    }

    public bool HasMetadata() => metadata.Count != 0;

    public bool HasMetadata(string name) => metadata.ContainsKey(name);

    public void RemoveAllMetadata() => metadata.Clear();

    public bool RemoveMetadata(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return metadata.Remove(name);
    }
}
