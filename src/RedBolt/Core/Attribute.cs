namespace RedBolt;

[Flags]
public enum AttributeMode
{
    Edit = 0,
    File = 1 << 0,
    NoEdit = 1 << 1,
    NodeId = 1 << 2,
    ComponentId = 1 << 3,
    NodeIdVector = 1 << 4,
    ReadOnly = 1 << 5,
    Prefab = 1 << 6,
    Temporary = 1 << 7,
    Default = File | Prefab
}

public enum AttributeScopeHint
{
    Attribute,
    Serializable,
    Node,
    Scene
}

public sealed class AttributeInfo
{
    private readonly Dictionary<StringHash, object?> metadata = new();

    public AttributeInfo(
        string name,
        Type valueType,
        object? defaultValue = null,
        AttributeMode mode = AttributeMode.Default,
        IReadOnlyList<string>? enumNames = null,
        Func<Serializable, object?>? getter = null,
        Action<Serializable, object?>? setter = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(valueType);

        Name = name;
        NameHash = name;
        ValueType = valueType;
        DefaultValue = defaultValue;
        Mode = mode;
        EnumNames = enumNames?.ToArray() ?? [];
        Getter = getter;
        Setter = setter;
    }

    public object? DefaultValue { get; }

    public IReadOnlyList<string> EnumNames { get; }

    public IReadOnlyDictionary<StringHash, object?> Metadata => metadata;

    public AttributeMode Mode { get; }

    public string Name { get; }

    public StringHash NameHash { get; }

    public AttributeScopeHint ScopeHint { get; internal set; } = AttributeScopeHint.Attribute;

    public bool ShouldLoad => (Mode & AttributeMode.File) != 0;

    public bool ShouldSave => (Mode & AttributeMode.File) != 0 && (Mode & AttributeMode.ReadOnly) == 0;

    public Type ValueType { get; }

    internal Func<Serializable, object?>? Getter { get; }

    internal Action<Serializable, object?>? Setter { get; }

    public object? GetMetadata(StringHash key)
    {
        return metadata.GetValueOrDefault(key);
    }

    internal void SetMetadata(StringHash key, object? value)
    {
        metadata[key] = value;
    }
}

public readonly struct AttributeHandle
{
    private readonly AttributeInfo? attribute;

    internal AttributeHandle(AttributeInfo? attribute)
    {
        this.attribute = attribute;
    }

    public AttributeHandle SetMetadata(StringHash key, object? value)
    {
        attribute?.SetMetadata(key, value);
        return this;
    }

    public AttributeHandle SetScopeHint(AttributeScopeHint scopeHint)
    {
        if (attribute is not null)
            attribute.ScopeHint = scopeHint;
        return this;
    }
}
