namespace RedBolt;

public abstract class Serializable
{
    private readonly List<AttributeInfo> attributes = [];
    private readonly Dictionary<StringHash, object?> attributeValues = new();
    private readonly Dictionary<StringHash, object?> instanceDefaultValues = new();
    private bool setInstanceDefault;

    public IReadOnlyList<AttributeInfo> Attributes => attributes;

    public bool IsTemporary => Temporary;

    public uint NumAttributes => (uint)attributes.Count;

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
        return index >= 0 && index < attributes.Count ? OnGetAttribute(attributes[index]) : null;
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
        if (index < 0 || index >= attributes.Count)
            return null;

        return GetAttributeDefault(attributes[index]);
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
        return instanceDefaultValues.GetValueOrDefault(new StringHash(name));
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

        if (attribute.Getter is not null)
            return attribute.Getter(this);

        return attributeValues.TryGetValue(attribute.NameHash, out var value) ? value : GetAttributeDefault(attribute);
    }

    public virtual void OnSetAttribute(AttributeInfo attribute, object? value)
    {
        ArgumentNullException.ThrowIfNull(attribute);

        if (setInstanceDefault)
            instanceDefaultValues[attribute.NameHash] = value;

        if (attribute.Setter is not null)
            attribute.Setter(this, value);
        else
            attributeValues[attribute.NameHash] = value;
    }

    public void RemoveInstanceDefault()
    {
        instanceDefaultValues.Clear();
    }

    public void ResetToDefault()
    {
        foreach (var attribute in attributes)
            OnSetAttribute(attribute, GetAttributeDefault(attribute));
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
        if (index < 0 || index >= attributes.Count)
            return false;

        OnSetAttribute(attributes[index], value);
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

        OnSetAttribute(attribute, value);
        return true;
    }

    public void SetInstanceDefault(bool enable)
    {
        setInstanceDefault = enable;
    }

    public void SetInstanceDefault(string name, object? defaultValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        instanceDefaultValues[new StringHash(name)] = defaultValue;
    }

    public void SetTemporary(bool enable)
    {
        Temporary = enable;
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

        var attribute = new AttributeInfo(name, valueType, defaultValue, mode, enumNames, getter, setter);
        attributes.Add(attribute);
        return new AttributeHandle(attribute);
    }

    private AttributeInfo? FindAttribute(string name)
    {
        var nameHash = new StringHash(name);
        foreach (var attribute in attributes)
        {
            if (attribute.NameHash == nameHash && string.Equals(attribute.Name, name, StringComparison.Ordinal))
                return attribute;
        }

        return null;
    }

    private object? GetAttributeDefault(AttributeInfo attribute)
    {
        return instanceDefaultValues.TryGetValue(attribute.NameHash, out var value) ? value : attribute.DefaultValue;
    }
}
