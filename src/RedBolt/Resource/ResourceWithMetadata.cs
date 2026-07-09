namespace RedBolt;

public abstract class ResourceWithMetadata : Resource
{
    private readonly Dictionary<string, object?> _metadata = new(StringComparer.Ordinal);

    protected ResourceWithMetadata(string name)
        : base(name)
    {
    }

    public IReadOnlyDictionary<string, object?> Metadata => _metadata;

    public IReadOnlyList<string> MetadataKeys => _metadata.Keys.ToArray();

    public IReadOnlyList<string> GetMetadataKeys() => MetadataKeys;

    public void AddMetadata(string name, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _metadata[name] = value;
    }

    public void CopyMetadata(ResourceWithMetadata source)
    {
        ArgumentNullException.ThrowIfNull(source);
        RemoveAllMetadata();
        foreach (var item in source._metadata)
            _metadata.Add(item.Key, item.Value);
    }

    public object? GetMetadata(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _metadata.GetValueOrDefault(name);
    }

    public bool HasMetadata() => _metadata.Count != 0;

    public bool HasMetadata(string name) => _metadata.ContainsKey(name);

    public void RemoveAllMetadata() => _metadata.Clear();

    public bool RemoveMetadata(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _metadata.Remove(name);
    }
}
