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

    public override void Dispose()
    {
        for (var i = _attributes.Count - 1; i >= 0; --i)
            _attributes[i].Dispose();
        _attributes.Clear();
        base.Dispose();
    }

    /// <summary>Return reflection used for serialization.</summary>
    public virtual ObjectReflection? GetReflection()
    {
        return Context.Instance.GetReflection(new StringHash(GetType().GetClassName()));
    }

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

    /// <summary>Load from a binary resource.</summary>
    public virtual bool Load(string resourceName)
    {
        var resourceCache = GetSubsystem<ResourceCache>();
        using var source = resourceCache.GetFile(resourceName, false);
        return source is not null && Load(source);
    }

    /// <summary>Load from an XML resource.</summary>
    public virtual bool LoadXML(string resourceName)
    {
        var resource = GetSubsystem<ResourceCache>().GetResourceOrNull<XMLFile>(resourceName);
        return resource is not null && LoadXML(resource.GetRoot());
    }

    /// <summary>Load from a JSON resource.</summary>
    public virtual bool LoadJSON(string resourceName)
    {
        var resource = GetSubsystem<ResourceCache>().GetResourceOrNull<JSONFile>(resourceName);
        return resource is not null && LoadJSON(resource.GetRoot());
    }

    /// <summary>Load from a resource of automatically detected type.</summary>
    public virtual bool LoadFile(string resourceName)
    {
        var resourceCache = GetSubsystem<ResourceCache>();
        var identifier = new FileIdentifier(resourceName);
        resourceCache.RouteResourceName(ref identifier);
        var extension = Path.GetExtension(identifier.ToUri());
        return extension.Equals(".xml", StringComparison.OrdinalIgnoreCase)
            ? LoadXML(identifier.ToUri())
            : extension.Equals(".json", StringComparison.OrdinalIgnoreCase)
                ? LoadJSON(identifier.ToUri())
                : Load(identifier.ToUri());
    }

    public virtual bool Load(Deserializer source)
    {
        ArgumentNullException.ThrowIfNull(source);
        foreach (var attribute in _attributes)
        {
            if (!attribute.ShouldLoad) continue;
            if (source.IsEof()) return false;
            using var value = source.ReadVariant(attribute.DefaultValue.Type);
            OnSetAttribute(attribute, ConvertVariant(attribute, value));
        }
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

    public virtual bool Save(Serializer destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        foreach (var attribute in _attributes)
        {
            if (!attribute.ShouldSave) continue;
            using var value = Variant.FromObject(OnGetAttribute(attribute));
            if (!destination.WriteVariantData(value)) return false;
        }
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
            using var value = attribute.EnumNames.Count != 0 && attribute.DefaultValue.Type == VariantType.Int
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
            using var value = Variant.FromObject(OnGetAttribute(attribute));
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
            using var value = attribute.EnumNames.Count != 0 && attribute.DefaultValue.Type == VariantType.Int
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
            using var value = Variant.FromObject(OnGetAttribute(attribute));
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
        SerializeInBlock(archive, false);
    }

    public void SerializeInBlock(Archive archive, bool serializeTemporary)
    {
        var count = 0u;
        if (!archive.IsInput)
        {
            foreach (var attribute in _attributes)
            {
                if (attribute.ShouldSave || serializeTemporary && (attribute.Mode & AttributeMode.Temporary) != 0)
                    ++count;
            }
        }

        using var block = archive.OpenArrayBlock("attributes", count);
        if (archive.IsInput)
        {
            for (var i = 0u; i < block.SizeHint; ++i)
            {
                using var item = archive.OpenUnorderedBlock("attribute");
                var name = string.Empty;
                ArchiveSerialization.SerializeOptionalValue(
                    archive, "name", ref name, string.Empty,
                    static (Archive valueArchive, string valueName, ref string value) =>
                        ArchiveSerialization.SerializeValue(valueArchive, valueName, ref value));
                var attribute = FindAttribute(name);
                var type = attribute?.DefaultValue.Type ?? VariantType.None;
                ArchiveSerialization.SerializeOptionalValue(
                    archive, "type", ref type, VariantType.None,
                    static (Archive valueArchive, string valueName, ref VariantType value) =>
                        ArchiveSerialization.SerializeValue(valueArchive, valueName, ref value));
                var value = new Variant(type);
                try
                {
                    ArchiveSerialization.SerializeVariantAsType(archive, "value", ref value, type);
                    if (attribute is not null && (attribute.ShouldLoad || serializeTemporary && (attribute.Mode & AttributeMode.Temporary) != 0))
                        OnSetAttribute(attribute, ConvertVariant(attribute, value));
                }
                finally
                {
                    value.Dispose();
                }
            }
            ApplyAttributes();
        }
        else
        {
            foreach (var attribute in _attributes.Where(attribute =>
                attribute.ShouldSave || serializeTemporary && (attribute.Mode & AttributeMode.Temporary) != 0))
            {
                using var item = archive.OpenUnorderedBlock("attribute");
                var name = attribute.Name;
                ArchiveSerialization.SerializeOptionalValue(
                    archive, "name", ref name, string.Empty,
                    static (Archive valueArchive, string valueName, ref string value) =>
                        ArchiveSerialization.SerializeValue(valueArchive, valueName, ref value));
                var value = Variant.FromObject(OnGetAttribute(attribute));
                try
                {
                    var type = value.Type;
                    ArchiveSerialization.SerializeOptionalValue(
                        archive, "type", ref type, VariantType.None,
                        static (Archive valueArchive, string valueName, ref VariantType valueType) =>
                            ArchiveSerialization.SerializeValue(valueArchive, valueName, ref valueType));
                    ArchiveSerialization.SerializeVariantAsType(archive, "value", ref value, type);
                }
                finally
                {
                    value.Dispose();
                }
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

        var eventData = new SceneEvents.TemporaryChangedEvent
        {
            Serializable = this
        };
        SendEvent(ref eventData);
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

        using var defaultVariant = Variant.FromObject(defaultValue);
        var attribute = new AttributeInfo(name, valueType, defaultVariant, mode, enumNames, accessor);
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

        using var defaultVariant = Variant.FromObject(defaultValue);
        var attribute = new AttributeInfo(name, valueType, defaultVariant, mode, enumNames, accessor: null, getter, setter);
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
        if (defaultValue is null)
            return null;
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
