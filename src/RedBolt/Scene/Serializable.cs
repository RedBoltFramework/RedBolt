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
        ArgumentNullException.ThrowIfNull(source);
        using var copy = new MemoryStream();
        source.CopyTo(copy);
        var buffer = new MemoryBuffer(copy.ToArray());
        foreach (var attribute in _attributes)
        {
            if (!attribute.ShouldLoad) continue;
            if (buffer.IsEof()) return false;
            OnSetAttribute(attribute, buffer.ReadVariant(attribute.DefaultValue.Type).GetObject());
        }
        ApplyAttributes();
        return true;
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
        ArgumentNullException.ThrowIfNull(destination);
        var buffer = new VectorBuffer();
        foreach (var attribute in _attributes)
        {
            if (!attribute.ShouldSave) continue;
            if (!buffer.WriteVariantData(Variant.FromObject(OnGetAttribute(attribute)))) return false;
        }
        destination.Write(buffer.Buffer[..checked((int)buffer.Size)]);
        return true;
    }

    public virtual bool LoadXML(XMLElement source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.IsNull) return false;
        for (var element = source.GetChild("attribute"); element.NotNull; element = element.GetNext("attribute"))
        {
            var attribute = FindAttribute(element.GetAttribute("name"));
            if (attribute is null || !attribute.ShouldLoad) continue;
            var value = attribute.EnumNames.Count != 0 && attribute.DefaultValue.Type == VariantType.Int
                ? ParseEnum(attribute, element.GetAttribute("value"))
                : element.GetVariantValue(attribute.DefaultValue.Type);
            if (!value.IsEmpty) OnSetAttribute(attribute, ConvertVariant(attribute, value));
        }
        ApplyAttributes();
        return true;
    }

    public virtual bool SaveXML(XMLElement destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (destination.IsNull) return false;
        for (var i = 0; i < _attributes.Count; ++i)
        {
            var attribute = _attributes[i];
            if (!attribute.ShouldSave) continue;
            var value = Variant.FromObject(OnGetAttribute(attribute));
            if (value == attribute.DefaultValue && !SaveDefaultAttributes(attribute)) continue;
            var element = destination.CreateChild("attribute");
            element.SetString("name", attribute.Name);
            if (attribute.EnumNames.Count != 0 && value.Type == VariantType.Int && value.GetInt() >= 0 && value.GetInt() < attribute.EnumNames.Count)
                element.SetString("value", attribute.EnumNames[value.GetInt()]);
            else element.SetVariantValue(value);
        }
        return true;
    }

    public virtual bool LoadJSON(JSONValue source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var attributes = source.Get("attributes");
        if (attributes.IsNull) return true;
        if (!attributes.IsObject) return false;
        foreach (var attribute in _attributes)
        {
            if (!attribute.ShouldLoad) continue;
            var json = attributes.Get(attribute.Name);
            if (json.IsNull) continue;
            var value = attribute.EnumNames.Count != 0 && attribute.DefaultValue.Type == VariantType.Int
                ? ParseEnum(attribute, json.GetString())
                : json.GetVariantValue(attribute.DefaultValue.Type);
            if (!value.IsEmpty) OnSetAttribute(attribute, ConvertVariant(attribute, value));
        }
        ApplyAttributes();
        return true;
    }

    public virtual bool SaveJSON(JSONValue destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        var attributes = new JSONValue(JSONValueType.Object);
        for (var i = 0; i < _attributes.Count; ++i)
        {
            var attribute = _attributes[i];
            if (!attribute.ShouldSave) continue;
            var value = Variant.FromObject(OnGetAttribute(attribute));
            if (value == attribute.DefaultValue && !SaveDefaultAttributes(attribute)) continue;
            if (attribute.EnumNames.Count != 0 && value.Type == VariantType.Int && value.GetInt() >= 0 && value.GetInt() < attribute.EnumNames.Count)
                attributes.Set(attribute.Name, new JSONValue(attribute.EnumNames[value.GetInt()]));
            else
            {
                var json = new JSONValue(); json.SetVariantValue(value); attributes.Set(attribute.Name, json);
            }
        }
        destination.Set("attributes", attributes);
        return true;
    }

    public override void SerializeInBlock(Archive archive)
    {
        var count = 0u;
        if (!archive.IsInput)
        {
            foreach (var attribute in _attributes)
            {
                if (attribute.ShouldSave)
                    ++count;
            }
        }

        using var block = archive.OpenArrayBlock("attributes", count);
        if (archive.IsInput)
        {
            for (var i = 0u; i < block.SizeHint; ++i)
            {
                using var item = archive.OpenUnorderedBlock("attribute");
                var name = string.Empty; ArchiveSerialization.SerializeValue(archive, "name", ref name);
                var attribute = FindAttribute(name);
                var value = new Variant(attribute?.DefaultValue.Type ?? VariantType.None);
                using (archive.OpenUnorderedBlock("value")) ArchiveSerialization.SerializeVariantInBlock(archive, ref value);
                if (attribute is not null && attribute.ShouldLoad) OnSetAttribute(attribute, ConvertVariant(attribute, value));
            }
            ApplyAttributes();
        }
        else
        {
            foreach (var attribute in _attributes.Where(static attribute => attribute.ShouldSave))
            {
                using var item = archive.OpenUnorderedBlock("attribute");
                var name = attribute.Name; ArchiveSerialization.SerializeValue(archive, "name", ref name);
                var value = Variant.FromObject(OnGetAttribute(attribute));
                using (archive.OpenUnorderedBlock("value")) ArchiveSerialization.SerializeVariantInBlock(archive, ref value);
            }
        }
    }

    public Serializable Clone(bool resetToDefault = true)
    {
        if (Activator.CreateInstance(GetType()) is not Serializable clone)
            throw new InvalidOperationException($"Cannot clone {GetType().Name}: parameterless constructor is required.");
        clone.CopyAttributes(this, resetToDefault);
        return clone;
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
                    Serializable = this
                };
                node.Scene.SendEvent(ref eventData);
                break;
            }
            case Component { Scene: { } scene }:
            {
                var eventData = new SceneEvents.TemporaryChangedEvent
                {
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

    private static Variant ParseEnum(AttributeInfo attribute, string name)
    {
        for (var i = 0; i < attribute.EnumNames.Count; ++i)
            if (string.Equals(attribute.EnumNames[i], name, StringComparison.Ordinal)) return new Variant(i);
        return default;
    }

    private static object? ConvertVariant(AttributeInfo attribute, Variant value)
    {
        if (attribute.ValueType.IsEnum) return Enum.ToObject(attribute.ValueType, value.GetInt());
        if (attribute.ValueType == typeof(uint)) return value.GetUInt();
        if (attribute.ValueType == typeof(ulong)) return value.GetUInt64();
        return value.GetObject();
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
