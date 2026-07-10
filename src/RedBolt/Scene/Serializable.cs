namespace RedBolt;

public abstract class Serializable : Object
{
    private readonly List<AttributeInfo> _attributes = [];
    private readonly Dictionary<StringHash, object?> _attributeValues = new();
    private readonly Dictionary<StringHash, object?> _instanceDefaultValues = new();
    private bool _setInstanceDefault;

    public IReadOnlyList<AttributeInfo> Attributes => _attributes;

    public bool IsTemporary => Temporary;

    public uint NumAttributes => (uint)_attributes.Count;

    public bool Temporary { get; private set; }

    public virtual void ApplyAttributes()
    {
    }

    public void CopyAttributes(Serializable source, bool resetToDefault = true)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (resetToDefault)
            ResetToDefault();

        foreach (var attribute in source.Attributes)
            SetAttribute(attribute.Name, source.GetAttribute(attribute.Name));
    }

    public object? GetAttribute(int index)
    {
        return index >= 0 && index < _attributes.Count ? OnGetAttribute(_attributes[index]) : null;
    }

    public object? GetAttribute(uint index)
    {
        return index <= int.MaxValue ? GetAttribute((int)index) : null;
    }

    public object? GetAttribute(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var attribute = FindAttribute(name);
        return attribute is not null ? OnGetAttribute(attribute) : null;
    }

    public object? GetAttributeDefault(int index)
    {
        if (index < 0 || index >= _attributes.Count)
            return null;

        return GetAttributeDefault(_attributes[index]);
    }

    public object? GetAttributeDefault(uint index)
    {
        return index <= int.MaxValue ? GetAttributeDefault((int)index) : null;
    }

    public object? GetAttributeDefault(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var attribute = FindAttribute(name);
        return attribute is not null ? GetAttributeDefault(attribute) : null;
    }

    public IReadOnlyList<AttributeInfo> GetAttributes()
    {
        return Attributes;
    }

    public object? GetInstanceDefault(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _instanceDefaultValues.GetValueOrDefault(new StringHash(name));
    }

    public uint GetNumAttributes()
    {
        return NumAttributes;
    }

    public virtual bool Load(Stream source)
    {
        return false;
    }

    public virtual object? OnGetAttribute(AttributeInfo attribute)
    {
        ArgumentNullException.ThrowIfNull(attribute);

        if (attribute.Accessor is not null)
            return attribute.Accessor.GetObject(this);

        return _attributeValues.TryGetValue(attribute.NameHash, out var value) ? value : GetAttributeDefault(attribute);
    }

    public virtual void OnSetAttribute(AttributeInfo attribute, object? value)
    {
        ArgumentNullException.ThrowIfNull(attribute);

        if (_setInstanceDefault)
            _instanceDefaultValues[attribute.NameHash] = value;

        if (attribute.Accessor is not null)
            attribute.Accessor.SetObject(this, value);
        else
            _attributeValues[attribute.NameHash] = value;
    }

    public void RemoveInstanceDefault()
    {
        _instanceDefaultValues.Clear();
    }

    public void ResetToDefault()
    {
        foreach (var attribute in _attributes)
        {
            if ((attribute.Mode & (AttributeMode.NoEdit | AttributeMode.NodeId | AttributeMode.ComponentId | AttributeMode.NodeIdVector)) != 0)
                continue;

            OnSetAttribute(attribute, GetAttributeDefault(attribute));
        }
    }

    public virtual bool Save(Stream destination)
    {
        return false;
    }

    public virtual bool SaveDefaultAttributes(AttributeInfo attribute)
    {
        return false;
    }

    public bool SetAttribute(int index, object? value)
    {
        if (index < 0 || index >= _attributes.Count)
            return false;

        var attribute = _attributes[index];
        if (value is not null && !attribute.ValueType.IsInstanceOfType(value))
            return false;

        OnSetAttribute(attribute, value);
        return true;
    }

    public bool SetAttribute(uint index, object? value)
    {
        return index <= int.MaxValue && SetAttribute((int)index, value);
    }

    public bool SetAttribute(string name, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var attribute = FindAttribute(name);
        if (attribute is null)
            return false;

        if (value is not null && !attribute.ValueType.IsInstanceOfType(value))
            return false;

        OnSetAttribute(attribute, value);
        return true;
    }

    public void SetInstanceDefault(bool enable)
    {
        _setInstanceDefault = enable;
    }

    public void SetInstanceDefault(string name, object? defaultValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _instanceDefaultValues[new StringHash(name)] = defaultValue;
    }

    public void SetTemporary(bool enable)
    {
        if (Temporary == enable)
            return;

        Temporary = enable;

        switch (this)
        {
            case Node node:
            {
                var eventData = new SceneEvents.TemporaryChangedEvent
                {
                    Scene = node.Scene,
                    Serializable = this
                };
                node.Scene.SendEvent(ref eventData);
                break;
            }
            case Component { Scene: { } scene }:
            {
                var eventData = new SceneEvents.TemporaryChangedEvent
                {
                    Scene = scene,
                    Serializable = this
                };
                scene.SendEvent(ref eventData);
                break;
            }
        }
    }

    protected AttributeHandle RegisterAttribute(
        string name,
        Type valueType,
        AttributeAccessor accessor,
        object? defaultValue = null,
        AttributeMode mode = AttributeMode.Default,
        IReadOnlyList<string>? enumNames = null)
    {
        ArgumentNullException.ThrowIfNull(accessor);

        var existing = FindAttribute(name);
        if (existing is not null)
            return new AttributeHandle(existing);

        var attribute = new AttributeInfo(name, valueType, Variant.FromObject(defaultValue), mode, enumNames, accessor);
        _attributes.Add(attribute);
        return new AttributeHandle(attribute);
    }

    protected AttributeHandle RegisterAttribute(
        string name,
        Type valueType,
        Func<Serializable, object?>? getter = null,
        Action<Serializable, object?>? setter = null,
        object? defaultValue = null,
        AttributeMode mode = AttributeMode.Default,
        IReadOnlyList<string>? enumNames = null)
    {
        var existing = FindAttribute(name);
        if (existing is not null)
            return new AttributeHandle(existing);

        var attribute = new AttributeInfo(name, valueType, Variant.FromObject(defaultValue), mode, enumNames, accessor: null, getter, setter);
        _attributes.Add(attribute);
        return new AttributeHandle(attribute);
    }

    private AttributeInfo? FindAttribute(string name)
    {
        var nameHash = new StringHash(name);
        foreach (var attribute in _attributes)
        {
            if (attribute.NameHash == nameHash && string.Equals(attribute.Name, name, StringComparison.Ordinal))
                return attribute;
        }

        return null;
    }

    private object? GetAttributeDefault(AttributeInfo attribute)
    {
        if (_instanceDefaultValues.TryGetValue(attribute.NameHash, out var value))
            return value;

        var defaultValue = attribute.DefaultValue.GetObject();
        if (attribute.ValueType.IsEnum)
            return Enum.ToObject(attribute.ValueType, attribute.DefaultValue.GetInt());
        if (attribute.ValueType == typeof(uint))
            return attribute.DefaultValue.GetUInt();
        if (attribute.ValueType == typeof(ulong))
            return attribute.DefaultValue.GetUInt64();
        if (attribute.ValueType.IsAssignableFrom(defaultValue.GetType()))
            return defaultValue;
        if (attribute.ValueType == typeof(IReadOnlyList<string>) && defaultValue is StringVector strings)
            return strings;
        return defaultValue;
    }
}
