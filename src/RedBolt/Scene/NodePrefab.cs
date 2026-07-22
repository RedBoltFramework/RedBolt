using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace RedBolt;

/// <summary>
/// Attribute prefab.
/// Contains representation of attribute with value, type information and identifier.
/// At least one of the following identifiers should be present:
/// - Zero-based attribute ID optimized for variable-length encoding. Zero is invalid value.
/// - Full attribute name. May be empty if loaded from compacted binary archive.
/// - Attribute name hash. May be present even if attribute name is empty.
/// </summary>
public class AttributePrefab : ICloneable, IEquatable<AttributePrefab>, IDisposable
{
    public enum IdentifierType : byte
    {
        Id,
        Name,
        NameHash,
        Unused,
        // Don't add more types!
    }

    public AttributePrefab()
    {
    }

    public AttributePrefab(AttributeId id)
    {
        _id = id;
    }

    public AttributePrefab(string name)
    {
        _name = name;
        _nameHash = name;
    }

    public AttributePrefab(StringHash nameHash)
    {
        _nameHash = nameHash;
    }

    public void SetValue(Variant value)
    {
        var copy = value.Clone();
        _value.Dispose();
        _value = copy;
    }

    public void SetValue(ref Variant value)
    {
        _value.Dispose();
        _value = value;
        value = default;
    }

    public AttributeId Id => _id;
    public string Name => _name;
    public StringHash NameHash => _nameHash;
    public VariantType Type => _value.Type;
    public ref readonly Variant Value => ref _value;

    public IdentifierType GetIdentifierType()
    {
        if (_id != AttributeId.None)
            return IdentifierType.Id;
        return !string.IsNullOrEmpty(_name) ? IdentifierType.Name : IdentifierType.NameHash;
    }

    public bool Equals(AttributePrefab? other)
    {
        return other is not null
            && _id == other._id
            && _name == other._name
            && _nameHash == other._nameHash
            && _value == other._value;
    }

    public override bool Equals(object? obj) => Equals(obj as AttributePrefab);

    public override int GetHashCode() => HashCode.Combine(_id, _name, _nameHash, _value);

    public static bool operator ==(AttributePrefab? left, AttributePrefab? right) => Equals(left, right);

    public static bool operator !=(AttributePrefab? left, AttributePrefab? right) => !Equals(left, right);

    public object Clone()
    {
        return new AttributePrefab
        {
            _id = _id,
            _name = _name,
            _nameHash = _nameHash,
            _value = _value.Clone()
        };
    }

    public void SerializeInBlock(Archive archive, bool compactSave = false)
    {
        if (archive.IsHumanReadable)
        {
            var id = (uint)_id;
            ArchiveSerialization.SerializeOptionalValue(archive, "id", ref id, 0u,
                static (Archive ar, string name, ref uint value) =>
                    ArchiveSerialization.SerializeValue(ar, name, ref value));
            _id = (AttributeId)id;

            var nameValue = _name;
            ArchiveSerialization.SerializeOptionalValue(archive, "name", ref nameValue, string.Empty,
                static (Archive ar, string name, ref string value) =>
                    ArchiveSerialization.SerializeValue(ar, name, ref value));
            _name = nameValue;
            if (string.IsNullOrEmpty(_name))
            {
                var nameHash = _nameHash;
                ArchiveSerialization.SerializeOptionalValue(archive, "nameHash", ref nameHash, StringHash.Empty,
                    static (Archive ar, string name, ref StringHash value) =>
                        ArchiveSerialization.SerializeValue(ar, name, ref value));
                _nameHash = nameHash;
            }

            var type = _value.Type;
            ArchiveSerialization.SerializeOptionalValue(archive, "type", ref type, VariantType.None,
                static (Archive ar, string name, ref VariantType value) =>
                    ArchiveSerialization.SerializeEnum(ar, name, ref value, Variant.GetTypeNameList()));
            var value = archive.IsInput ? default : _value;
            ArchiveSerialization.SerializeVariantAsType(archive, "value", ref value, type);
            if (archive.IsInput)
            {
                _value.Dispose();
                _value = value;
            }
        }
        else
        {
            const int identifierTypeOffset = 6;
            const byte variantTypeMask = 0x3f;
            byte descriptor = 0;
            var identifierType = IdentifierType.Id;
            var type = _value.Type;

            if (!archive.IsInput)
            {
                identifierType = GetIdentifierType();
                if (compactSave && identifierType == IdentifierType.Name)
                    identifierType = IdentifierType.NameHash;
                descriptor = (byte)((byte)type & variantTypeMask
                    | (byte)((byte)identifierType << identifierTypeOffset));
            }

            ArchiveSerialization.SerializeValue(archive, "descriptor", ref descriptor);
            if (archive.IsInput)
            {
                type = (VariantType)(descriptor & variantTypeMask);
                identifierType = (IdentifierType)(descriptor >> identifierTypeOffset);
            }

            switch (identifierType)
            {
            case IdentifierType.Id:
            {
                var id = (uint)_id;
                archive.SerializeVle("id", ref id);
                _id = (AttributeId)id;
                break;
            }
            case IdentifierType.Name:
                archive.Serialize("name", ref _name);
                break;
            case IdentifierType.NameHash:
            {
                var nameHash = _nameHash.Value;
                archive.Serialize("nameHash", ref nameHash);
                _nameHash = new StringHash(nameHash);
                break;
            }
            default:
                Debug.Assert(false);
                break;
            }

            var value = archive.IsInput ? default : _value;
            ArchiveSerialization.SerializeVariantAsType(archive, "value", ref value, type);
            if (archive.IsInput)
            {
                _value.Dispose();
                _value = value;
            }
        }

        if (archive.IsInput && !string.IsNullOrEmpty(_name))
            _nameHash = _name;
    }

    public void Dispose()
    {
        _value.Dispose();
        _name = string.Empty;
        _nameHash = StringHash.Empty;
        _id = AttributeId.None;
        GC.SuppressFinalize(this);
    }

    private AttributeId _id;
    private string _name = string.Empty;
    private StringHash _nameHash = StringHash.Empty;
    private Variant _value;
}

/// <summary>
/// Serializable prefab.
/// Contains a list of attributes.
/// </summary>
public class SerializablePrefab : ICloneable, IEquatable<SerializablePrefab>, IDisposable
{
    public string TypeName => _typeName;
    public StringHash TypeNameHash => _typeNameHash;
    public SerializableId Id => _id;
    public List<AttributePrefab> Attributes => _attributes;

    public void SetType(string typeName)
    {
        _typeName = typeName;
        _typeNameHash = typeName;
    }

    public void SetType(StringHash typeNameHash)
    {
        _typeNameHash = typeNameHash;
        _typeName = string.Empty;
    }

    public void SetId(SerializableId id) => _id = id;

    public AttributeScopeHint GetEffectiveScopeHint()
    {
        if (_typeNameHash == StringHash.Empty)
            return AttributeScopeHint.Attribute;

        return Context.Instance.GetReflection(_typeNameHash)?.GetEffectiveScopeHint()
            ?? AttributeScopeHint.Attribute;
    }

    public bool Equals(SerializablePrefab? other)
    {
        return other is not null
            && _id == other._id
            && _typeNameHash == other._typeNameHash
            && _typeName == other._typeName
            && _attributes.SequenceEqual(other._attributes);
    }

    public override bool Equals(object? obj) => Equals(obj as SerializablePrefab);

    public override int GetHashCode() => HashCode.Combine(_id, _typeNameHash, _typeName, _attributes.Count);

    public static bool operator ==(SerializablePrefab? left, SerializablePrefab? right) => Equals(left, right);

    public static bool operator !=(SerializablePrefab? left, SerializablePrefab? right) => !Equals(left, right);

    public object Clone()
    {
        var clone = new SerializablePrefab
        {
            _typeName = _typeName,
            _typeNameHash = _typeNameHash,
            _id = _id,
            _temporary = _temporary
        };
        foreach (var attribute in _attributes)
            clone._attributes.Add((AttributePrefab)attribute.Clone());
        return clone;
    }

    public void Import(Serializable serializable, PrefabSaveFlag flags = PrefabSaveFlag.None)
    {
        var reflection = serializable.GetReflection();
        if (reflection is null)
        {
            Log.GetLogger().LogError(
                "Serializable '{Serializable}' is not reflected and cannot be serialized", serializable.GetTypeName());
            return;
        }

        var objectAttributes = serializable.GetAttributes();
        var numObjectAttributes = objectAttributes.Count;

        _typeName = reflection.ClassName;
        _typeNameHash = reflection.TypeId;
        _temporary = serializable.IsTemporary;

        DisposeAttributes();
        _attributes.Capacity = Math.Max(_attributes.Capacity, numObjectAttributes);
        for (var attributeIndex = 0; attributeIndex < numObjectAttributes; ++attributeIndex)
        {
            var attribute = objectAttributes[attributeIndex];

            // If temporary attribute saving is enabled, it overrides other flags.
            var shouldSave = attribute.ShouldSave
                || ((flags & PrefabSaveFlag.SaveTemporary) != 0 && (attribute.Mode & AttributeMode.Temporary) != 0);
            if (!shouldSave)
                continue;

            if ((attribute.Mode & AttributeMode.Prefab) == 0 && (flags & PrefabSaveFlag.Prefab) != 0)
                continue;

            var value = Variant.FromObject(serializable.OnGetAttribute(attribute));

            // Skip defaults if allowed
            if ((flags & PrefabSaveFlag.SaveDefaultValues) == 0)
            {
                var defaultValue = Variant.FromObject(serializable.GetAttributeDefault(attributeIndex));
                var isDefault = value == defaultValue;
                defaultValue.Dispose();
                if (isDefault)
                {
                    value.Dispose();
                    continue;
                }
            }

            var attributePrefab = (flags & PrefabSaveFlag.CompactAttributeNames) != 0
                ? new AttributePrefab(attribute.NameHash)
                : new AttributePrefab(attribute.Name);
            if ((flags & PrefabSaveFlag.EnumsAsStrings) != 0 && attribute.EnumNames.Count != 0)
            {
                var enumValue = checked((int)value.GetUInt());
                value.Dispose();
                value = new Variant(attribute.ConvertEnumToString(enumValue));
            }

            attributePrefab.SetValue(ref value);
            _attributes.Add(attributePrefab);
        }
    }

    public void Export(Serializable serializable, PrefabLoadFlag flags = PrefabLoadFlag.None)
    {
        var reflection = serializable.GetReflection();
        if (reflection is null)
        {
            Log.GetLogger().LogError(
                "Serializable '{Serializable}' is not reflected and cannot be serialized", serializable.GetTypeName());
            return;
        }

        if ((flags & PrefabLoadFlag.CheckSerializableType) != 0
            && reflection.TypeId != _typeNameHash)
        {
            Log.GetLogger().LogError(
                "Serializable '{Serializable}' is not of type '{ExpectedType}'.",
                reflection.ClassName, !string.IsNullOrEmpty(_typeName) ? _typeName : _typeNameHash.ToString());
            return;
        }

        if ((flags & PrefabLoadFlag.KeepTemporaryState) == 0)
            serializable.SetTemporary(_temporary);

        var objectAttributes = serializable.GetAttributes();
        foreach (var attributePrefab in _attributes)
        {
            // Not supported
            if (attributePrefab.Id != AttributeId.None)
                continue;

            var attributeIndex = -1;
            for (var index = 0; index < objectAttributes.Count; ++index)
            {
                if (objectAttributes[index].NameHash == attributePrefab.NameHash)
                {
                    attributeIndex = index;
                    break;
                }
            }
            if (attributeIndex == -1)
                continue;
            var attribute = objectAttributes[attributeIndex];

            // If temporary attribute was saved, we load it unconditionally.
            if (!attribute.ShouldLoad && (attribute.Mode & AttributeMode.Temporary) == 0)
                continue;

            object? value;
            if (attributePrefab.Value.Type == VariantType.String && attribute.EnumNames.Count != 0)
            {
                var enumName = attributePrefab.Value.GetString();
                var enumValue = attribute.ConvertEnumToUInt(enumName);
                if (enumValue < 0)
                {
                    Log.GetLogger().LogWarning(
                        "Attribute '{Attribute}' of Serializable '{Serializable}' has unknown enum value '{Value}'",
                        attribute.Name, reflection.ClassName, enumName);
                    continue;
                }
                value = attribute.ValueType.IsEnum
                    ? Enum.ToObject(attribute.ValueType, enumValue)
                    : enumValue;
            }
            else
            {
                value = attributePrefab.Value.GetObject();
                if (value is not null && attribute.ValueType.IsEnum)
                    value = Enum.ToObject(attribute.ValueType, value);
                else if (attribute.ValueType == typeof(uint))
                    value = attributePrefab.Value.GetUInt();
            }

            if (value is not null && attribute.ValueType.IsEnum && !value.GetType().IsEnum)
                value = Enum.ToObject(attribute.ValueType, value);
            serializable.OnSetAttribute(attribute, value);
        }
    }

    public void SerializeInBlock(Archive archive, PrefabArchiveFlag flags = PrefabArchiveFlag.None, bool compactSave = false)
    {
        if ((flags & PrefabArchiveFlag.IgnoreSerializableId) != 0)
        {
            if (archive.IsInput)
                _id = SerializableId.None;
        }
        else
        {
            var id = (uint)_id;
            ArchiveSerialization.SerializeOptionalValue(archive, "_id", ref id, 0u,
                static (Archive ar, string name, ref uint value) => ar.SerializeVle(name, ref value));
            _id = (SerializableId)id;
        }

        if ((flags & PrefabArchiveFlag.IgnoreSerializableType) != 0)
        {
            if (archive.IsInput)
            {
                _typeName = string.Empty;
                _typeNameHash = StringHash.Empty;
            }
        }
        else
        {
            if (archive.IsUnorderedAccessSupportedInCurrentBlock)
            {
                ArchiveSerialization.SerializeOptionalValue(archive, "_typeName", ref _typeName, string.Empty,
                    static (Archive ar, string name, ref string value) =>
                        ArchiveSerialization.SerializeValue(ar, name, ref value));
                if (string.IsNullOrEmpty(_typeName))
                {
                    ArchiveSerialization.SerializeOptionalValue(
                        archive, "_typeHash", ref _typeNameHash, StringHash.Empty,
                        static (Archive ar, string name, ref StringHash value) =>
                            ArchiveSerialization.SerializeValue(ar, name, ref value));
                }
            }
            else if ((flags & PrefabArchiveFlag.CompactTypeNames) != 0)
                ArchiveSerialization.SerializeValue(archive, "_typeHash", ref _typeNameHash);
            else
                ArchiveSerialization.SerializeValue(archive, "_typeName", ref _typeName);
        }

        if (archive.IsInput && !string.IsNullOrEmpty(_typeName))
            _typeNameHash = _typeName;

        if ((flags & PrefabArchiveFlag.SerializeTemporary) != 0)
        {
            ArchiveSerialization.SerializeOptionalValue(archive, "_temporary", ref _temporary, false,
                static (Archive ar, string name, ref bool value) =>
                    ArchiveSerialization.SerializeValue(ar, name, ref value));
        }
        else if (archive.IsInput)
            _temporary = false;

        var count = archive.IsInput ? 0 : _attributes.Count;
        if (archive.IsInput && archive.IsUnorderedAccessSupportedInCurrentBlock && !archive.HasElementOrBlock("attributes"))
        {
            DisposeAttributes();
            return;
        }
        if (!archive.IsInput && count == 0 && archive.IsUnorderedAccessSupportedInCurrentBlock)
            return;
        using var block = archive.OpenArrayBlock("attributes", count);
        if (archive.IsInput)
        {
            DisposeAttributes();
            var size = block.SizeHint;
            for (var index = 0; index < size; ++index)
            {
                using var item = archive.OpenUnorderedBlock("attribute");
                var attribute = new AttributePrefab();
                attribute.SerializeInBlock(archive, compactSave);
                _attributes.Add(attribute);
            }
        }
        else foreach (var attribute in _attributes)
        {
            using var item = archive.OpenUnorderedBlock("attribute");
            attribute.SerializeInBlock(archive, compactSave);
        }
    }

    public void Dispose()
    {
        DisposeAttributes();
        _typeName = string.Empty;
        _typeNameHash = StringHash.Empty;
        _id = SerializableId.None;
        _temporary = false;
        GC.SuppressFinalize(this);
    }

    private void DisposeAttributes()
    {
        for (var index = _attributes.Count - 1; index >= 0; --index)
            _attributes[index].Dispose();
        _attributes.Clear();
    }

    private string _typeName = string.Empty;
    private StringHash _typeNameHash = StringHash.Empty;
    private SerializableId _id;
    private bool _temporary;
    private readonly List<AttributePrefab> _attributes = [];
}

/// <summary>
/// Scene prefab.
/// Contains node attributes, components and child nodes.
/// </summary>
public class NodePrefab : ICloneable, IEquatable<NodePrefab>, IDisposable
{
    public AttributeScopeHint GetEffectiveScopeHint()
    {
        var result = AttributeScopeHint.Attribute;
        foreach (var component in Components)
            result = (AttributeScopeHint)Math.Max((int)result, (int)component.GetEffectiveScopeHint());
        foreach (var child in Children)
            result = (AttributeScopeHint)Math.Max((int)result, (int)child.GetEffectiveScopeHint());
        return result;
    }

    public bool Equals(NodePrefab? other)
    {
        return other is not null
            && Node == other.Node
            && Components.SequenceEqual(other.Components)
            && Children.SequenceEqual(other.Children);
    }

    public override bool Equals(object? obj) => Equals(obj as NodePrefab);

    public override int GetHashCode() => HashCode.Combine(Node, Components.Count, Children.Count);

    public static bool operator ==(NodePrefab? left, NodePrefab? right) => Equals(left, right);

    public static bool operator !=(NodePrefab? left, NodePrefab? right) => !Equals(left, right);

    public static void SerializeValue(Archive archive, string name, NodePrefab value,
        PrefabArchiveFlag flags = PrefabArchiveFlag.None, bool compactSave = false)
    {
        using var block = archive.OpenUnorderedBlock(name);
        value.SerializeInBlock(archive, flags, compactSave);
    }

    public static NodePrefab Empty { get; } = new();

    public SerializablePrefab Node => _node;
    public List<SerializablePrefab> Components => _components;
    public List<NodePrefab> Children => _children;

    public object Clone()
    {
        var clone = new NodePrefab
        {
            _node = (SerializablePrefab)_node.Clone()
        };
        foreach (var component in Components)
            clone.Components.Add((SerializablePrefab)component.Clone());
        foreach (var child in Children)
            clone.Children.Add((NodePrefab)child.Clone());
        return clone;
    }

    public void CopyFrom(NodePrefab source)
    {
        if (ReferenceEquals(this, source))
            return;

        Clear();
        _node = (SerializablePrefab)source._node.Clone();
        foreach (var component in source._components)
            _components.Add((SerializablePrefab)component.Clone());
        foreach (var child in source._children)
            _children.Add((NodePrefab)child.Clone());
    }

    public string GetNodeName()
    {
        foreach (var attribute in Node.Attributes)
        {
            if (attribute.Name == "Name" || attribute.NameHash == new StringHash("Name"))
                return attribute.Value.GetString();
        }

        return string.Empty;
    }

    public NodePrefab FindChild(string path)
    {
        if (string.IsNullOrEmpty(path))
            return this;

        var separator = path.IndexOf('/');
        var childName = separator < 0 ? path : path[..separator];
        var pathTail = separator < 0 ? string.Empty : path[(separator + 1)..];
        if (string.IsNullOrEmpty(childName))
        {
            Log.GetLogger().LogError("Empty child name in prefab path '{Path}'", path);
            return Empty;
        }

        if (childName[0] == '#')
        {
            if (!int.TryParse(childName.AsSpan(1), out var childIndex))
                childIndex = 0;
            if (childIndex < 0 || childIndex >= Children.Count)
            {
                Log.GetLogger().LogError(
                    "Invalid child index #{ChildIndex} in prefab path '{Path}'", childIndex, path);
                return Empty;
            }

            return Children[childIndex].FindChild(pathTail);
        }

        foreach (var child in Children)
        {
            if (child.GetNodeName() == childName)
                return child.FindChild(pathTail);
        }

        Log.GetLogger().LogError("Child '{Child}' is not found in prefab path '{Path}'", childName, path);
        return Empty;
    }

    public void NormalizeIds()
    {
        var normalizer = new PrefabNormalizer();
        normalizer.ScanNode(this);
        normalizer.RemapAndPrune(this);
    }

    public bool IsEmpty()
    {
        return Node.Attributes.Count == 0 && Components.Count == 0 && Children.Count == 0;
    }

    public void Clear()
    {
        _node.Dispose();
        for (var index = _components.Count - 1; index >= 0; --index)
            _components[index].Dispose();
        _components.Clear();
        for (var index = _children.Count - 1; index >= 0; --index)
            _children[index].Dispose();
        _children.Clear();
    }

    public void SerializeInBlock(Archive archive, PrefabArchiveFlag flags = PrefabArchiveFlag.None, bool compactSave = false)
    {
        _node.SerializeInBlock(archive, PrefabTypes.ToNodeFlags(flags));
        SerializeList(archive, "components", "component", Components,
            (value, ar) => value.SerializeInBlock(ar, PrefabTypes.ToComponentFlags(flags), compactSave));
        SerializeList(archive, "nodes", "node", Children,
            (value, ar) => value.SerializeInBlock(ar, flags, compactSave));
    }

    public void Dispose()
    {
        Clear();
        GC.SuppressFinalize(this);
    }

    private static void SerializeList<T>(Archive archive, string name, string itemName, List<T> values, Action<T, Archive> serialize)
        where T : IDisposable, new()
    {
        if (archive.IsInput && archive.IsUnorderedAccessSupportedInCurrentBlock && !archive.HasElementOrBlock(name))
        {
            for (var index = values.Count - 1; index >= 0; --index)
                values[index].Dispose();
            values.Clear();
            return;
        }
        if (!archive.IsInput && values.Count == 0 && archive.IsUnorderedAccessSupportedInCurrentBlock)
            return;

        var count = archive.IsInput ? 0 : values.Count;
        using var block = archive.OpenArrayBlock(name, count);
        if (archive.IsInput)
        {
            for (var index = values.Count - 1; index >= 0; --index)
                values[index].Dispose();
            values.Clear();
            var size = block.SizeHint;
            for (var index = 0; index < size; ++index)
            {
                using var item = archive.OpenUnorderedBlock(itemName);
                var value = new T(); serialize(value, archive); values.Add(value);
            }
        }
        else foreach (var value in values)
        {
            using var item = archive.OpenUnorderedBlock(itemName); serialize(value, archive);
        }
    }

    private SerializablePrefab _node = new();
    private readonly List<SerializablePrefab> _components = [];
    private readonly List<NodePrefab> _children = [];
}

/// <summary>Utility class to remap and resolve prefab IDs. Similar to SceneResolver.</summary>
public class PrefabNormalizer
{
    public PrefabNormalizer()
    {
        _context = Context.Instance;
    }

    public void ScanNode(NodePrefab node)
    {
        ScanSerializable(node.Node);

        foreach (var component in node.Components)
            ScanSerializable(component);

        foreach (var child in node.Children)
            ScanNode(child);
    }

    public void RemapAndPrune(NodePrefab node)
    {
        RemapReferencedIds();
        PatchAttributes();
        PruneUnreferencedIds(node);
    }

    private void ScanSerializable(SerializablePrefab prefab)
    {
        var typeHash = prefab.TypeNameHash;
        if (typeHash == StringHash.Empty)
        {
            // Node or Scene, try both
            var nodeReflection = _context.GetReflection<Node>();
            var sceneReflection = _context.GetReflection<Scene>();

            foreach (var attributePrefab in prefab.Attributes)
            {
                var attribute = nodeReflection.GetAttribute(attributePrefab.NameHash);
                if (attribute is not null)
                    ScanAttribute(attributePrefab, attribute);
                else
                {
                    attribute = sceneReflection.GetAttribute(attributePrefab.NameHash);
                    if (attribute is not null)
                        ScanAttribute(attributePrefab, attribute);
                }
            }
        }
        else
        {
            var reflection = _context.GetReflection(typeHash);
            if (reflection is null)
                return;

            foreach (var attributePrefab in prefab.Attributes)
            {
                var attribute = reflection.GetAttribute(attributePrefab.NameHash);
                if (attribute is not null)
                    ScanAttribute(attributePrefab, attribute);
            }
        }
    }

    private void ScanAttribute(AttributePrefab attributePrefab, AttributeInfo attribute)
    {
        var value = attributePrefab.Value;
        if ((attribute.Mode & AttributeMode.NodeId) != 0 && value.Type == VariantType.Int)
        {
            _nodeIdAttributes.Add(attributePrefab);
            _referencedNodeIds.Add((SerializableId)value.GetUInt());
        }
        else if ((attribute.Mode & AttributeMode.NodeIdVector) != 0 && value.Type == VariantType.VariantVector)
        {
            _nodeIdAttributes.Add(attributePrefab);
            foreach (var element in value.GetVariantVector())
                _referencedNodeIds.Add((SerializableId)element.GetUInt());
        }
        else if ((attribute.Mode & AttributeMode.ComponentId) != 0 && value.Type == VariantType.Int)
        {
            _componentIdAttributes.Add(attributePrefab);
            _referencedComponentIds.Add((SerializableId)value.GetUInt());
        }
    }

    private void RemapReferencedIds()
    {
        uint nextNodeId = 1;
        uint nextComponentId = 1;

        foreach (var nodeId in _referencedNodeIds)
        {
            if (!_nodeIdRemap.ContainsKey(nodeId))
                _nodeIdRemap[nodeId] = (SerializableId)nextNodeId++;
        }

        foreach (var componentId in _referencedComponentIds)
        {
            if (!_componentIdRemap.ContainsKey(componentId))
                _componentIdRemap[componentId] = (SerializableId)nextComponentId++;
        }
    }

    private void PatchAttributes()
    {
        foreach (var attributePrefab in _nodeIdAttributes)
        {
            var oldValue = attributePrefab.Value;
            if (oldValue.Type == VariantType.Int)
            {
                var oldNodeId = (SerializableId)oldValue.GetUInt();
                Debug.Assert(_nodeIdRemap.ContainsKey(oldNodeId));
                var newValue = new Variant((uint)_nodeIdRemap[oldNodeId]);
                attributePrefab.SetValue(ref newValue);
            }
            else if (oldValue.Type == VariantType.VariantVector)
            {
                var vector = new VariantVector();
                foreach (var element in oldValue.GetVariantVector())
                {
                    var oldNodeId = (SerializableId)element.GetUInt();
                    Debug.Assert(_nodeIdRemap.ContainsKey(oldNodeId));
                    vector.Add(new Variant((uint)_nodeIdRemap[oldNodeId]));
                }
                var newValue = new Variant(vector);
                vector.Dispose();
                attributePrefab.SetValue(ref newValue);
            }
        }

        foreach (var attributePrefab in _componentIdAttributes)
        {
            var oldValue = attributePrefab.Value;
            if (oldValue.Type == VariantType.Int)
            {
                var oldComponentId = (SerializableId)oldValue.GetUInt();
                Debug.Assert(_componentIdRemap.ContainsKey(oldComponentId));
                var newValue = new Variant((uint)_componentIdRemap[oldComponentId]);
                attributePrefab.SetValue(ref newValue);
            }
        }
    }

    private void PruneUnreferencedIds(NodePrefab node)
    {
        PruneUnreferencedId(node.Node, true);
        foreach (var component in node.Components)
            PruneUnreferencedId(component, false);
        foreach (var child in node.Children)
            PruneUnreferencedIds(child);
    }

    private void PruneUnreferencedId(SerializablePrefab prefab, bool isNode)
    {
        var remap = isNode ? _nodeIdRemap : _componentIdRemap;
        prefab.SetId(remap.TryGetValue(prefab.Id, out var newId) ? newId : SerializableId.None);
    }

    private readonly Context _context;

    private readonly List<AttributePrefab> _nodeIdAttributes = [];
    private readonly List<AttributePrefab> _componentIdAttributes = [];
    private readonly List<SerializableId> _referencedNodeIds = [];
    private readonly List<SerializableId> _referencedComponentIds = [];
    private readonly Dictionary<SerializableId, SerializableId> _nodeIdRemap = [];
    private readonly Dictionary<SerializableId, SerializableId> _componentIdRemap = [];
}
