namespace RedBolt;

public class Serializable : Object
{
    private Dictionary<StringHash, Variant>? _instanceDefaultValues;
    private bool _setInstanceDefault;
    private bool _temporary;
    private readonly List<AttributeInfo> _attributes = [];
    private readonly Dictionary<StringHash, Variant> _attributeValues = new();

    public IReadOnlyList<AttributeInfo> Attributes => _attributes;

    public bool IsTemporary => _temporary;

    public int NumAttributes => _attributes.Count;

    public override void Dispose()
    {
        RemoveInstanceDefault();
        for (var i = _attributes.Count - 1; i >= 0; --i)
            _attributes[i].Dispose();
        _attributes.Clear();
        foreach (var value in _attributeValues.Values)
        {
            var ownedValue = value;
            ownedValue.Dispose();
        }
        _attributeValues.Clear();
        base.Dispose();
    }

    /// <summary>Return reflection used for serialization.</summary>
    public virtual ObjectReflection? GetReflection()
    {
        return Context.Instance.GetReflection(new StringHash(GetType().GetClassName()));
    }

    public virtual void ApplyAttributes()
    {
    }

    public void CopyAttributes(Serializable source, bool resetToDefault = true)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (resetToDefault)
            ResetToDefault();

        var sourceReflection = source.GetReflection();
        if (sourceReflection is not null
            && ReferenceEquals(sourceReflection, GetReflection())
            && source.Attributes.Count == _attributes.Count)
        {
            for (var i = 0; i < source.Attributes.Count; ++i)
            {
                var sourceAttribute = source.Attributes[i];
                var attribute = _attributes[i];
                var value = default(Variant);
                try
                {
                    source.OnGetAttribute(sourceAttribute, ref value);
                    OnSetAttribute(attribute, value);
                }
                finally
                {
                    value.Dispose();
                }
            }
        }
        else
        {
            foreach (var attribute in _attributes)
            {
                AttributeInfo? sourceAttribute = null;
                foreach (var candidate in source.Attributes)
                {
                    if (candidate.NameHash == attribute.NameHash)
                    {
                        sourceAttribute = candidate;
                        break;
                    }
                }

                if (sourceAttribute is null)
                    continue;

                var value = default(Variant);
                try
                {
                    source.OnGetAttribute(sourceAttribute, ref value);
                    OnSetAttribute(attribute, value);
                }
                finally
                {
                    value.Dispose();
                }
            }
        }
    }

    public Variant GetAttribute(int index)
    {
        var value = default(Variant);
        if (index >= 0 && index < _attributes.Count)
            OnGetAttribute(_attributes[index], ref value);
        return value;
    }

    public Variant GetAttribute(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var attribute = FindAttribute(name);
        var value = default(Variant);
        if (attribute is not null)
            OnGetAttribute(attribute, ref value);
        return value;
    }

    public Variant GetAttributeDefault(int index)
    {
        if (index < 0 || index >= _attributes.Count)
            return default;

        return GetAttributeDefault(_attributes[index]);
    }

    public Variant GetAttributeDefault(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var attribute = FindAttribute(name);
        return attribute is not null ? GetAttributeDefault(attribute) : default;
    }

    public IReadOnlyList<AttributeInfo> GetAttributes()
    {
        return Attributes;
    }

    public Variant GetInstanceDefault(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _instanceDefaultValues is not null
            && _instanceDefaultValues.TryGetValue(new StringHash(name), out var value)
                ? value.Clone()
                : default;
    }

    public int GetNumAttributes()
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
        return resource is not null && LoadJSON(resource.Root);
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
            OnSetAttribute(attribute, value);
        }
        return true;
    }

    public virtual void OnGetAttribute(AttributeInfo attribute, ref Variant destination)
    {
        ArgumentNullException.ThrowIfNull(attribute);

        destination.Dispose();
        if (attribute.Accessor is not null)
        {
            attribute.Accessor.Get(this, ref destination);
            return;
        }

        destination = _attributeValues.TryGetValue(attribute.NameHash, out var value)
            ? value.Clone()
            : GetAttributeDefault(attribute);
    }

    public virtual void OnSetAttribute(AttributeInfo attribute, Variant value)
    {
        ArgumentNullException.ThrowIfNull(attribute);

        if (_setInstanceDefault)
            SetInstanceDefault(attribute.Name, value);

        if (attribute.Accessor is not null)
            attribute.Accessor.Set(this, value);
        else
        {
            var copy = value.Clone();
            if (_attributeValues.TryGetValue(attribute.NameHash, out var previousValue))
                previousValue.Dispose();
            _attributeValues[attribute.NameHash] = copy;
        }
    }

    public void RemoveInstanceDefault()
    {
        if (_instanceDefaultValues is null)
            return;

        foreach (var value in _instanceDefaultValues.Values)
        {
            var ownedValue = value;
            ownedValue.Dispose();
        }
        _instanceDefaultValues = null;
    }

    public void ResetToDefault()
    {
        foreach (var attribute in _attributes)
        {
            if ((attribute.Mode & (AttributeMode.NoEdit | AttributeMode.NodeId | AttributeMode.ComponentId | AttributeMode.NodeIdVector)) != 0)
                continue;

            using var value = GetAttributeDefault(attribute);
            OnSetAttribute(attribute, value);
        }
    }

    public virtual bool Save(Serializer destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        foreach (var attribute in _attributes)
        {
            if (!attribute.ShouldSave) continue;
            var value = default(Variant);
            OnGetAttribute(attribute, ref value);
            using (value)
            if (!destination.WriteVariantData(value)) return false;
        }
        return true;
    }

    public virtual bool LoadXML(XMLElement source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.IsNull)
        {
            Log.GetLogger().Log(Microsoft.Extensions.Logging.LogLevel.Error, default,
                $"Could not load {GetTypeName()}, null source element", null, static (state, _) => state);
            return false;
        }

        var startIndex = 0;
        for (var element = source.GetChild("attribute"); element.NotNull; element = element.GetNext("attribute"))
        {
            var name = element.GetAttribute("name");
            var i = startIndex;
            var attempts = _attributes.Count;

            while (attempts != 0)
            {
                var attribute = _attributes[i];
                if (attribute.ShouldLoad && string.Equals(attribute.Name, name, StringComparison.Ordinal))
                {
                    // If enums specified, do enum lookup and int assignment. Otherwise assign the variant directly
                    using var value = attribute.EnumNames.Count != 0 && attribute.DefaultValue.Type == VariantType.Int
                        ? ParseEnum(attribute, element.GetAttribute("value"))
                        : element.GetVariantValue(attribute.DefaultValue.Type);
                    if (value.IsEmpty && attribute.EnumNames.Count != 0
                        && attribute.DefaultValue.Type == VariantType.Int)
                        Log.GetLogger().Log(Microsoft.Extensions.Logging.LogLevel.Warning, default,
                            $"Unknown enum value {element.GetAttribute("value")} in attribute {attribute.Name}", null,
                            static (state, _) => state);
                    if (!value.IsEmpty)
                        OnSetAttribute(attribute, value);

                    startIndex = (i + 1) % _attributes.Count;
                    break;
                }

                i = (i + 1) % _attributes.Count;
                --attempts;
            }

            if (attempts == 0)
                Log.GetLogger().Log(Microsoft.Extensions.Logging.LogLevel.Warning, default,
                    $"Unknown attribute {name} in XML data", null, static (state, _) => state);
        }
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
            var value = default(Variant);
            OnGetAttribute(attribute, ref value);
            using (value)
            {
                if (value == attribute.DefaultValue && !SaveDefaultAttributes(attribute)) continue;
                var element = destination.CreateChild("attribute");
                element.SetString("name", attribute.Name);
                if (attribute.EnumNames.Count != 0 && value.Type == VariantType.Int)
                    element.SetString("value", attribute.EnumNames[value.GetInt()]);
                else element.SetVariantValue(value);
            }
        }
        return true;
    }

    public virtual bool LoadJSON(JSONValue source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.IsNull)
        {
            Log.GetLogger().Log(Microsoft.Extensions.Logging.LogLevel.Error, default,
                $"Could not load {GetTypeName()}, null JSON source element", null, static (state, _) => state);
            return false;
        }

        // Get attributes value
        var attributes = source.Get("attributes");
        if (attributes.IsNull) return true;
        // Warn if the attributes value isn't an object
        if (!attributes.IsObject)
        {
            Log.GetLogger().Log(Microsoft.Extensions.Logging.LogLevel.Warning, default,
                $"'attributes' object is present in {GetTypeName()} but is not a JSON object; skipping load", null,
                static (state, _) => state);
            return true;
        }

        foreach (var attribute in _attributes)
        {
            if (!attribute.ShouldLoad) continue;
            var json = attributes.Get(attribute.Name);
            if (json.IsNull) continue;
            using var value = attribute.EnumNames.Count != 0 && attribute.DefaultValue.Type == VariantType.Int
                ? ParseEnum(attribute, json.GetString())
                : json.GetVariantValue(attribute.DefaultValue.Type);
            if (value.IsEmpty && attribute.EnumNames.Count != 0 && attribute.DefaultValue.Type == VariantType.Int)
                Log.GetLogger().Log(Microsoft.Extensions.Logging.LogLevel.Warning, default,
                    $"Unknown enum value {json.GetString()} in attribute {attribute.Name}", null,
                    static (state, _) => state);
            if (!value.IsEmpty) OnSetAttribute(attribute, value);
        }

        // Report missing attributes.
        foreach (var pair in attributes.GetObject())
        {
            var found = false;
            for (var i = 0; i < _attributes.Count && !found; ++i)
                found = string.Equals(_attributes[i].Name, pair.Key, StringComparison.Ordinal);
            if (!found)
                Log.GetLogger().Log(Microsoft.Extensions.Logging.LogLevel.Warning, default,
                    $"Unknown attribute {pair.Key} in JSON data", null, static (state, _) => state);
        }
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
            var value = default(Variant);
            OnGetAttribute(attribute, ref value);
            using (value)
            {
                if (value == attribute.DefaultValue && !SaveDefaultAttributes(attribute)) continue;
                if (attribute.EnumNames.Count != 0 && value.Type == VariantType.Int)
                    attributes.Set(attribute.Name, new JSONValue(attribute.EnumNames[value.GetInt()]));
                else
                {
                    var json = new JSONValue();
                    json.SetVariantValue(value);
                    attributes.Set(attribute.Name, json);
                }
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
        var count = 0;
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
            for (var i = 0; i < block.SizeHint; ++i)
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
                        OnSetAttribute(attribute, value);
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
            foreach (var attribute in _attributes)
            {
                if (!attribute.ShouldSave
                    && (!serializeTemporary || (attribute.Mode & AttributeMode.Temporary) == 0))
                    continue;

                using var item = archive.OpenUnorderedBlock("attribute");
                var name = attribute.Name;
                ArchiveSerialization.SerializeOptionalValue(
                    archive, "name", ref name, string.Empty,
                    static (Archive valueArchive, string valueName, ref string value) =>
                        ArchiveSerialization.SerializeValue(valueArchive, valueName, ref value));
                var value = default(Variant);
                OnGetAttribute(attribute, ref value);
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

    public Serializable? Clone(bool resetToDefault = true)
    {
        var cloneObject = Context.Instance.CreateObject(new StringHash(GetType().GetClassName()));
        if (cloneObject is Serializable clone)
        {
            clone.CopyAttributes(this, resetToDefault);
            return clone;
        }
        return null;
    }

    public virtual bool SaveDefaultAttributes(AttributeInfo attribute)
    {
        return false;
    }

    public bool SetAttribute(int index, Variant value)
    {
        if (index < 0 || index >= _attributes.Count)
            return false;

        var attribute = _attributes[index];
        if (value.Type != attribute.DefaultValue.Type)
            return false;

        OnSetAttribute(attribute, value);
        return true;
    }

    public bool SetAttribute(string name, Variant value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var attribute = FindAttribute(name);
        if (attribute is null)
            return false;

        if (value.Type != attribute.DefaultValue.Type)
            return false;

        OnSetAttribute(attribute, value);
        return true;
    }

    public void SetInstanceDefault(bool enable)
    {
        _setInstanceDefault = enable;
    }

    public void SetInstanceDefault(string name, Variant defaultValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _instanceDefaultValues ??= new Dictionary<StringHash, Variant>();
        var nameHash = new StringHash(name);
        var value = defaultValue.Clone();
        if (_instanceDefaultValues.TryGetValue(nameHash, out var previousValue))
            previousValue.Dispose();
        _instanceDefaultValues[nameHash] = value;
    }

    public void SetTemporary(bool enable)
    {
        if (_temporary == enable)
            return;

        _temporary = enable;

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
            if (string.Equals(attribute.EnumNames[i], name, StringComparison.OrdinalIgnoreCase)) return new Variant(i);
        return default;
    }

    private Variant GetAttributeDefault(AttributeInfo attribute)
    {
        if (_instanceDefaultValues is not null
            && _instanceDefaultValues.TryGetValue(attribute.NameHash, out var value))
            return value.Clone();

        return attribute.DefaultValue.Clone();
    }
}
