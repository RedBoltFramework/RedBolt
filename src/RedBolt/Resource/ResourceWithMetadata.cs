namespace RedBolt;

public abstract class ResourceWithMetadata : Resource
{
    private readonly Dictionary<string, Variant> _metadata = new(StringComparer.Ordinal);
    private readonly List<string> _metadataKeys = [];

    protected ResourceWithMetadata(string name)
        : base(name)
    {
    }

    public IReadOnlyDictionary<string, Variant> Metadata => _metadata;

    public IReadOnlyList<string> MetadataKeys => _metadataKeys;

    public IReadOnlyList<string> GetMetadataKeys() => MetadataKeys;

    public void AddMetadata(string name, Variant value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!_metadata.ContainsKey(name))
            _metadataKeys.Add(name);
        _metadata[name] = value;
    }

    public void AddMetadata(string name, object? value) => AddMetadata(name, Variant.FromObject(value));

    public void CopyMetadata(ResourceWithMetadata source)
    {
        ArgumentNullException.ThrowIfNull(source);
        RemoveAllMetadata();
        foreach (var key in source._metadataKeys)
            AddMetadata(key, source._metadata[key]);
    }

    public Variant GetMetadata(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _metadata.GetValueOrDefault(name);
    }

    public bool HasMetadata() => _metadata.Count != 0;

    public bool HasMetadata(string name) => _metadata.ContainsKey(name);

    public void RemoveAllMetadata()
    {
        _metadata.Clear();
        _metadataKeys.Clear();
    }

    public bool RemoveMetadata(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _metadataKeys.Remove(name);
        return _metadata.Remove(name);
    }

    protected void LoadMetadataFromXML(XMLElement source)
    {
        ArgumentNullException.ThrowIfNull(source);
        RemoveAllMetadata();
        for (var element = source.GetChild("metadata"); element.NotNull; element = element.GetNext("metadata"))
            AddMetadata(element.GetAttribute("name"), element.GetVariant());
    }

    protected void LoadMetadataFromJSON(IReadOnlyList<JSONValue> array)
    {
        ArgumentNullException.ThrowIfNull(array);
        RemoveAllMetadata();
        foreach (var value in array)
            AddMetadata(value.Get("name").GetString(), value.GetVariant());
    }

    protected void SaveMetadataToXML(XMLElement destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        foreach (var key in _metadataKeys)
        {
            var element = destination.CreateChild("metadata");
            element.SetString("name", key);
            element.SetVariant(_metadata[key]);
        }
    }
}
