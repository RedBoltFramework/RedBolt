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

// C++: namespace AttributeMetadata in Scene/Serializable.h
public static class AttributeMetadata
{
    /// Names of vector struct elements. StringVector.
    public static readonly StringHash VectorStructElements = "VectorStructElements";
    /// Flag that indicates that attribute is an action.
    public static readonly StringHash IsAction = "IsAction";
    /// Flag that indicates that the attribute should be resizable by user. Applies only to ResourceRefList.
    public static readonly StringHash AllowResize = "AllowResize";
    /// Flag that indicates that the elements metadata should be extracted from the value itself.
    /// Applies only to StringVariantMap.
    public static readonly StringHash DynamicMetadata = "DynamicMetadata";
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
    public abstract void Get(Serializable ptr, ref Variant dest);

    /// <summary>
    /// Set the attribute.
    /// </summary>
    public abstract void Set(Serializable ptr, Variant src);

    // C++: not in C++ AttributeAccessor; C# delegate-based adaptation
    public virtual object? GetObject(Serializable ptr)
    {
        var result = default(Variant);
        Get(ptr, ref result);
        return result.GetObject();
    }

    // C++: not in C++ AttributeAccessor; C# delegate-based adaptation
    public virtual void SetObject(Serializable ptr, object? src) => Set(ptr, Variant.FromObject(src));
}

public sealed class AttributeInfo : ICloneable
{
    private readonly Dictionary<StringHash, Variant> _metadata = new();

    public AttributeInfo(
        string name,
        Type valueType,
        Variant defaultValue = default,
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

    public Variant DefaultValue { get; internal set; }

    public IReadOnlyList<string> EnumNames { get; }

    public IReadOnlyDictionary<StringHash, Variant> Metadata => _metadata;

    public AttributeMode Mode { get; }

    public string Name { get; }

    public StringHash NameHash { get; }

    public AttributeScopeHint ScopeHint { get; set; } = AttributeScopeHint.Attribute;

    public bool ShouldLoad => (Mode & AttributeMode.File) != 0;

    public bool ShouldSave => (Mode & AttributeMode.File) != 0 && (Mode & AttributeMode.ReadOnly) == 0;

    public Type ValueType { get; }

    public Variant GetMetadata(StringHash key)
    {
        return _metadata.GetValueOrDefault(key);
    }

    public T GetMetadata<T>(StringHash key)
    {
        return GetMetadata(key).Get<T>();
    }

    /// <summary>
    /// Convert enum value to string.
    /// </summary>
    public string ConvertEnumToString(uint value)
    {
        return EnumNames[(int)value];
    }

    /// <summary>
    /// Convert enum value to integer.
    /// </summary>
    public uint ConvertEnumToUInt(string value)
    {
        for (var index = 0; index < EnumNames.Count; ++index)
        {
            if (string.Equals(EnumNames[index], value, StringComparison.OrdinalIgnoreCase))
                return (uint)index;
        }

        return uint.MaxValue;
    }

    public object Clone()
    {
        var result = new AttributeInfo(Name, ValueType, DefaultValue, Mode, EnumNames, Accessor)
        {
            ScopeHint = ScopeHint
        };

        foreach (var (key, value) in _metadata)
            result.SetMetadata(key, value);

        return result;
    }

    public void SetMetadata(StringHash key, Variant value)
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

        public override void Get(Serializable ptr, ref Variant dest)
        {
            dest = _getter is not null ? Variant.FromObject(_getter(ptr)) : default;
        }

        public override void Set(Serializable ptr, Variant src)
        {
            _setter?.Invoke(ptr, src.GetObject());
        }

        public override object? GetObject(Serializable ptr) => _getter?.Invoke(ptr);

        public override void SetObject(Serializable ptr, object? src) => _setter?.Invoke(ptr, src);
    }
}

public readonly struct AttributeHandle
{
    private readonly AttributeInfo _attribute;

    internal AttributeHandle(AttributeInfo attribute)
    {
        this._attribute = attribute;
    }

    public AttributeHandle SetMetadata(StringHash key, Variant value)
    {
        if (_attribute is not null)
            _attribute.SetMetadata(key, value);
        return this;
    }

    public AttributeHandle SetScopeHint(AttributeScopeHint scopeHint)
    {
        if (_attribute is not null)
            _attribute.ScopeHint = scopeHint;
        return this;
    }
}
