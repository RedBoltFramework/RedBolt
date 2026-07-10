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

/// <summary>
/// Abstract base class for invoking attribute accessors.
/// </summary>
public abstract class AttributeAccessor
{
    /// <summary>
    /// Construct.
    /// </summary>
    protected AttributeAccessor()
    {
    }

    /// <summary>
    /// Get the attribute.
    /// </summary>
    public abstract object? Get(Serializable ptr);

    /// <summary>
    /// Set the attribute.
    /// </summary>
    public abstract void Set(Serializable ptr, object? src);
}

public sealed class AttributeInfo
{
    private readonly Dictionary<StringHash, object?> _metadata = new();

    public AttributeInfo(
        string name,
        Type valueType,
        object? defaultValue = null,
        AttributeMode mode = AttributeMode.Default,
        IReadOnlyList<string>? enumNames = null,
        AttributeAccessor? accessor = null,
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
        Accessor = accessor ?? (getter is not null || setter is not null ? new DelegateAttributeAccessor(getter, setter) : null);
    }

    public AttributeAccessor? Accessor { get; }

    public object? DefaultValue { get; }

    public IReadOnlyList<string> EnumNames { get; }

    public IReadOnlyDictionary<StringHash, object?> Metadata => _metadata;

    public AttributeMode Mode { get; }

    public string Name { get; }

    public StringHash NameHash { get; }

    public AttributeScopeHint ScopeHint { get; internal set; } = AttributeScopeHint.Attribute;

    public bool ShouldLoad => (Mode & AttributeMode.File) != 0;

    public bool ShouldSave => (Mode & AttributeMode.File) != 0 && (Mode & AttributeMode.ReadOnly) == 0;

    public Type ValueType { get; }

    public object? GetMetadata(StringHash key)
    {
        return _metadata.GetValueOrDefault(key);
    }

    internal void SetMetadata(StringHash key, object? value)
    {
        _metadata[key] = value;
    }

    private sealed class DelegateAttributeAccessor : AttributeAccessor
    {
        private readonly Func<Serializable, object?>? _getter;
        private readonly Action<Serializable, object?>? _setter;

        public DelegateAttributeAccessor(Func<Serializable, object?>? getter, Action<Serializable, object?>? setter)
        {
            _getter = getter;
            _setter = setter;
        }

        public override object? Get(Serializable ptr)
        {
            return _getter is not null ? _getter(ptr) : null;
        }

        public override void Set(Serializable ptr, object? src)
        {
            _setter?.Invoke(ptr, src);
        }
    }
}

public readonly struct AttributeHandle
{
    private readonly AttributeInfo _attribute;

    internal AttributeHandle(AttributeInfo attribute)
    {
        this._attribute = attribute;
    }

    public AttributeHandle SetMetadata(StringHash key, object? value)
    {
        _attribute.SetMetadata(key, value);
        return this;
    }

    public AttributeHandle SetScopeHint(AttributeScopeHint scopeHint)
    {
        _attribute.ScopeHint = scopeHint;
        return this;
    }
}
