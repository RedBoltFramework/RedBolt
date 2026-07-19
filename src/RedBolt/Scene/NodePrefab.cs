using Microsoft.Extensions.Logging;

namespace RedBolt;

public sealed class AttributePrefab : ICloneable, IEquatable<AttributePrefab>
{
    public enum IdentifierType : byte
    {
        Id,
        Name,
        NameHash,
        Unused
    }

    public AttributePrefab()
    {
    }

    public AttributePrefab(uint id)
    {
        Id = id;
    }

    public AttributePrefab(string name)
    {
        Name = name;
    }

    public AttributePrefab(StringHash nameHash)
    {
        NameHash = nameHash;
    }

    public string Name
    {
        get => _name;
        set
        {
            _name = value ?? string.Empty;
            NameHash = _name;
        }
    }

    public StringHash NameHash { get; set; }

    public uint Id { get; set; }

    public Variant Value { get; set; }

    public StringHash GetNameHash() => NameHash;

    public IdentifierType GetIdentifierType()
    {
        if (Id != 0)
            return IdentifierType.Id;
        return !string.IsNullOrEmpty(Name) ? IdentifierType.Name : IdentifierType.NameHash;
    }

    public bool Equals(AttributePrefab? other)
    {
        return other is not null
            && Name == other.Name
            && NameHash == other.NameHash
            && Id == other.Id
            && Value == other.Value;
    }

    public override bool Equals(object? obj) => Equals(obj as AttributePrefab);

    public override int GetHashCode() => HashCode.Combine(Name, NameHash, Id, Value);

    public static bool operator ==(AttributePrefab? left, AttributePrefab? right) => Equals(left, right);

    public static bool operator !=(AttributePrefab? left, AttributePrefab? right) => !Equals(left, right);

    public object Clone()
    {
        return new AttributePrefab
        {
            Name = Name,
            NameHash = NameHash,
            Id = Id,
            Value = Value
        };
    }

    public void SerializeInBlock(Archive archive, bool compactSave = false)
    {
        if (archive.IsHumanReadable)
        {
            var name = Name;
            if (!archive.IsInput && string.IsNullOrEmpty(name) && NameHash != StringHash.Empty)
            {
                var nameHash = NameHash;
                ArchiveSerialization.SerializeValue(archive, "nameHash", ref nameHash);
            }
            else if (archive.IsInput && archive.HasElementOrBlock("nameHash") && !archive.HasElementOrBlock("name"))
            {
                var nameHash = NameHash;
                ArchiveSerialization.SerializeValue(archive, "nameHash", ref nameHash);
                NameHash = nameHash;
            }
            else
            {
                ArchiveSerialization.SerializeValue(archive, "name", ref name);
                Name = name;
            }

            var type = Value.Type;
            ArchiveSerialization.SerializeEnum(archive, "type", ref type, Variant.GetTypeNameList());
            var value = Value;
            ArchiveSerialization.SerializeVariantAsType(archive, "value", ref value, type);
            Value = value;
            return;
        }

        var identifier = compactSave && !string.IsNullOrEmpty(Name)
            ? (byte)2
            : (byte)(string.IsNullOrEmpty(Name) ? 2 : 1);
        const int identifierTypeOffset = 6;
        const byte variantTypeMask = 0x3f;
        var descriptor = archive.IsInput ? (byte)0 : (byte)((byte)Value.Type | (identifier << identifierTypeOffset));
        ArchiveSerialization.SerializeValue(archive, "descriptor", ref descriptor);
        var typeValue = (VariantType)(descriptor & variantTypeMask);
        var identifierType = descriptor >> identifierTypeOffset;
        if (archive.IsInput)
        {
            if (identifierType == 1)
            {
                var name = string.Empty;
                ArchiveSerialization.SerializeValue(archive, "name", ref name);
                Name = name;
            }
            else
            {
                var nameHash = NameHash;
                ArchiveSerialization.SerializeValue(archive, "nameHash", ref nameHash);
                NameHash = nameHash;
            }
        }
        else if (identifier == 1)
        {
            var name = Name;
            ArchiveSerialization.SerializeValue(archive, "name", ref name);
        }
        else
        {
            var nameHash = NameHash;
            ArchiveSerialization.SerializeValue(archive, "nameHash", ref nameHash);
            NameHash = nameHash;
        }

        var serializedValue = Value;
        ArchiveSerialization.SerializeVariantAsType(archive, "value", ref serializedValue, typeValue);
        Value = serializedValue;
        if (archive.IsInput && !string.IsNullOrEmpty(Name))
            NameHash = Name;
    }

    private string _name = string.Empty;
}

public sealed class SerializablePrefab : ICloneable, IEquatable<SerializablePrefab>
{
    public string TypeName
    {
        get => _typeName;
        set
        {
            _typeName = value ?? string.Empty;
            _typeNameHash = _typeName;
        }
    }

    public StringHash TypeNameHash => _typeNameHash;
    public uint Id { get; set; }
    public bool Temporary { get; set; }
    public List<AttributePrefab> Attributes { get; } = [];

    public void SetType(string typeName) => TypeName = typeName;

    public void SetType(StringHash typeNameHash)
    {
        _typeNameHash = typeNameHash;
        _typeName = typeNameHash.Reverse();
    }

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
            && TypeName == other.TypeName
            && Id == other.Id
            && Temporary == other.Temporary
            && Attributes.SequenceEqual(other.Attributes);
    }

    public override bool Equals(object? obj) => Equals(obj as SerializablePrefab);

    public override int GetHashCode() => HashCode.Combine(TypeName, Id, Temporary, Attributes.Count);

    public static bool operator ==(SerializablePrefab? left, SerializablePrefab? right) => Equals(left, right);

    public static bool operator !=(SerializablePrefab? left, SerializablePrefab? right) => !Equals(left, right);

    public object Clone()
    {
        var clone = new SerializablePrefab
        {
            TypeName = TypeName,
            Id = Id,
            Temporary = Temporary
        };
        foreach (var attribute in Attributes)
            clone.Attributes.Add((AttributePrefab)attribute.Clone());
        return clone;
    }

    public void Import(Serializable? serializable, PrefabSaveFlag flags = PrefabSaveFlag.None)
    {
        TypeName = serializable!.GetTypeName();
        Temporary = serializable.IsTemporary;

        Attributes.Clear();
        for (var attributeIndex = 0u; attributeIndex < serializable.Attributes.Count; ++attributeIndex)
        {
            var attribute = serializable.Attributes[(int)attributeIndex];

            // If temporary attribute saving is enabled, it overrides other flags.
            var shouldSave = attribute.ShouldSave
                || ((flags & PrefabSaveFlag.SaveTemporary) != 0 && (attribute.Mode & AttributeMode.Temporary) != 0);
            if (!shouldSave)
                continue;

            if ((attribute.Mode & AttributeMode.Prefab) == 0 && (flags & PrefabSaveFlag.Prefab) != 0)
                continue;

            Variant value;
            if (serializable is Node node && attribute.Name == "Variables")
            {
                var variables = new VariantMap();
                foreach (var (key, item) in node.GetVars())
                    variables[key] = Variant.FromObject(item);
                value = new Variant(variables);
            }
            else
                value = Variant.FromObject(serializable.OnGetAttribute(attribute));

            // Skip defaults if allowed
            if ((flags & PrefabSaveFlag.SaveDefaultValues) == 0)
            {
                var defaultValue = Variant.FromObject(serializable.GetAttributeDefault(attributeIndex));
                if (value == defaultValue)
                    continue;
            }

            var attributePrefab = (flags & PrefabSaveFlag.CompactAttributeNames) != 0
                ? new AttributePrefab { NameHash = attribute.NameHash }
                : new AttributePrefab { Name = attribute.Name };
            if ((flags & PrefabSaveFlag.EnumsAsStrings) != 0 && attribute.EnumNames.Count != 0)
                value = new Variant(attribute.ConvertEnumToString(checked((int)value.GetUInt())));

            attributePrefab.Value = value;
            Attributes.Add(attributePrefab);
        }
    }

    public void Export(Serializable? serializable, PrefabLoadFlag flags = PrefabLoadFlag.None)
    {
        ArgumentNullException.ThrowIfNull(serializable);

        if ((flags & PrefabLoadFlag.CheckSerializableType) != 0
            && serializable.GetTypeName() != TypeName)
        {
            Log.GetLogger().LogError(
                "Serializable '{Serializable}' is not of type '{ExpectedType}'.",
                serializable.GetTypeName(), TypeName);
            return;
        }

        if ((flags & PrefabLoadFlag.KeepTemporaryState) == 0)
            serializable.SetTemporary(Temporary);

        foreach (var attributePrefab in Attributes)
        {
            AttributeInfo? attribute = null;
            foreach (var candidate in serializable.Attributes)
            {
                if ((!string.IsNullOrEmpty(attributePrefab.Name) && candidate.Name == attributePrefab.Name)
                    || (string.IsNullOrEmpty(attributePrefab.Name) && candidate.NameHash == attributePrefab.NameHash))
                {
                    attribute = candidate;
                    break;
                }
            }

            if (attribute is null)
                continue;

            // If temporary attribute was saved, we load it unconditionally.
            if (!attribute.ShouldLoad && (attribute.Mode & AttributeMode.Temporary) == 0)
                continue;

            var value = attributePrefab.Value.GetObject();
            if (value is string enumName && attribute.EnumNames.Count != 0)
            {
                for (var enumIndex = 0; enumIndex < attribute.EnumNames.Count; ++enumIndex)
                {
                    if (attribute.EnumNames[enumIndex] == enumName)
                    {
                        value = enumIndex;
                        break;
                    }
                }
            }

            if (value is not null && attribute.ValueType.IsEnum)
                value = Enum.ToObject(attribute.ValueType, value);
            else if (attribute.ValueType == typeof(uint))
                value = attributePrefab.Value.GetUInt();

            serializable.OnSetAttribute(attribute, value);
        }

        if ((flags & PrefabLoadFlag.SkipApplyAttributes) == 0)
            serializable.ApplyAttributes();
    }

    public void SerializeInBlock(Archive archive, PrefabArchiveFlag flags = PrefabArchiveFlag.None, bool compactSave = false)
    {
        if ((flags & PrefabArchiveFlag.IgnoreSerializableId) != 0)
        {
            if (archive.IsInput)
                Id = 0;
        }
        else
        {
            if (!archive.IsInput || !archive.IsUnorderedAccessSupportedInCurrentBlock || archive.HasElementOrBlock("id"))
            {
                var id = Id;
                ArchiveSerialization.SerializeValue(archive, "id", ref id);
                Id = id;
            }
        }

        if ((flags & PrefabArchiveFlag.IgnoreSerializableType) != 0)
        {
            if (archive.IsInput)
                TypeName = string.Empty;
        }
        else
        {
            if (!archive.IsInput || !archive.IsUnorderedAccessSupportedInCurrentBlock || archive.HasElementOrBlock("type"))
            {
                var type = TypeName;
                ArchiveSerialization.SerializeValue(archive, "type", ref type);
                TypeName = type;
            }
        }

        if ((flags & PrefabArchiveFlag.SerializeTemporary) != 0)
        {
            if (!archive.IsInput || !archive.IsUnorderedAccessSupportedInCurrentBlock || archive.HasElementOrBlock("temporary"))
            {
                var temporary = Temporary;
                ArchiveSerialization.SerializeValue(archive, "temporary", ref temporary);
                Temporary = temporary;
            }
        }
        else if (archive.IsInput)
            Temporary = false;

        var count = archive.IsInput ? 0u : (uint)Attributes.Count;
        if (archive.IsInput && archive.IsUnorderedAccessSupportedInCurrentBlock && !archive.HasElementOrBlock("attributes"))
        {
            Attributes.Clear();
            return;
        }
        if (!archive.IsInput && count == 0 && archive.IsUnorderedAccessSupportedInCurrentBlock)
            return;
        using var block = archive.OpenArrayBlock("attributes", count);
        if (archive.IsInput)
        {
            Attributes.Clear();
            for (var i = 0u; i < block.SizeHint; ++i)
            {
                using var item = archive.OpenUnorderedBlock("attribute");
                var attribute = new AttributePrefab();
                attribute.SerializeInBlock(archive, compactSave);
                Attributes.Add(attribute);
            }
        }
        else foreach (var attribute in Attributes)
        {
            using var item = archive.OpenUnorderedBlock("attribute");
            attribute.SerializeInBlock(archive, compactSave);
        }
    }

    private string _typeName = string.Empty;
    private StringHash _typeNameHash;
}

public sealed class NodePrefab : ICloneable, IEquatable<NodePrefab>
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

    public SerializablePrefab Node { get; } = new();
    public List<SerializablePrefab> Components { get; } = [];
    public List<NodePrefab> Children { get; } = [];

    public object Clone()
    {
        var clone = new NodePrefab
        {
            Node = { TypeName = Node.TypeName, Id = Node.Id, Temporary = Node.Temporary }
        };
        foreach (var attribute in Node.Attributes)
            clone.Node.Attributes.Add((AttributePrefab)attribute.Clone());
        foreach (var component in Components)
            clone.Components.Add((SerializablePrefab)component.Clone());
        foreach (var child in Children)
            clone.Children.Add((NodePrefab)child.Clone());
        return clone;
    }

    public string GetNodeName()
    {
        foreach (var attribute in Node.Attributes)
        {
            if (attribute.Name == "Name")
                return attribute.Value.GetObject() as string ?? string.Empty;
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
            return Empty;

        if (childName[0] == '#')
        {
            if (!uint.TryParse(childName.AsSpan(1), out var childIndex) || childIndex >= Children.Count)
                return Empty;

            return Children[(int)childIndex].FindChild(pathTail);
        }

        foreach (var child in Children)
        {
            if (child.GetNodeName() == childName)
                return child.FindChild(pathTail);
        }

        return Empty;
    }

    public void NormalizeIds()
    {
        var nodeReferences = new List<AttributePrefab>();
        var componentReferences = new List<AttributePrefab>();
        var referencedNodeIds = new HashSet<uint>();
        var referencedComponentIds = new HashSet<uint>();
        ScanNode(this, nodeReferences, componentReferences, referencedNodeIds, referencedComponentIds);

        var nodeIdRemap = CreateRemap(referencedNodeIds);
        var componentIdRemap = CreateRemap(referencedComponentIds);
        PatchAttributes(nodeReferences, nodeIdRemap, vector: true);
        PatchAttributes(componentReferences, componentIdRemap, vector: false);
        PruneIds(this, nodeIdRemap, componentIdRemap);
    }

    private static void ScanNode(
        NodePrefab node,
        List<AttributePrefab> nodeReferences,
        List<AttributePrefab> componentReferences,
        HashSet<uint> referencedNodeIds,
        HashSet<uint> referencedComponentIds)
    {
        ScanSerializable(node.Node, isNode: true, nodeReferences, componentReferences, referencedNodeIds, referencedComponentIds);
        foreach (var component in node.Components)
            ScanSerializable(component, isNode: false, nodeReferences, componentReferences, referencedNodeIds, referencedComponentIds);
        foreach (var child in node.Children)
            ScanNode(child, nodeReferences, componentReferences, referencedNodeIds, referencedComponentIds);
    }

    private static void ScanSerializable(
        SerializablePrefab prefab,
        bool isNode,
        List<AttributePrefab> nodeReferences,
        List<AttributePrefab> componentReferences,
        HashSet<uint> referencedNodeIds,
        HashSet<uint> referencedComponentIds)
    {
        var reflection = FindReflection(prefab.TypeName, isNode);
        foreach (var attribute in prefab.Attributes)
        {
            var info = reflection?.GetAttribute(attribute.NameHash);
            if (info is null)
                continue;

            if ((info.Mode & AttributeMode.NodeId) != 0 && attribute.Value.Type == VariantType.Int)
            {
                nodeReferences.Add(attribute);
                referencedNodeIds.Add(attribute.Value.GetUInt());
            }
            else if ((info.Mode & AttributeMode.NodeIdVector) != 0 && attribute.Value.Type == VariantType.VariantVector)
            {
                nodeReferences.Add(attribute);
                foreach (var value in attribute.Value.GetVariantVector())
                    referencedNodeIds.Add(value.GetUInt());
            }
            else if ((info.Mode & AttributeMode.ComponentId) != 0 && attribute.Value.Type == VariantType.Int)
            {
                componentReferences.Add(attribute);
                referencedComponentIds.Add(attribute.Value.GetUInt());
            }
        }
    }

    private static ObjectReflection? FindReflection(string typeName, bool isNode)
    {
        var context = Context.Instance;
        if (!string.IsNullOrEmpty(typeName))
        {
            var typeHash = new StringHash(typeName);
            return context.IsReflected(typeHash) ? context.GetReflection(typeHash) : null;
        }

        if (isNode && context.IsReflected<Node>())
            return context.GetReflection<Node>();
        if (!isNode && context.IsReflected<Scene>())
            return context.GetReflection<Scene>();
        return null;
    }

    private static Dictionary<uint, uint> CreateRemap(IEnumerable<uint> referencedIds)
    {
        var result = new Dictionary<uint, uint>();
        var nextId = 1u;
        foreach (var id in referencedIds)
        {
            if (id != 0)
                result[id] = nextId++;
        }
        return result;
    }

    private static void PatchAttributes(List<AttributePrefab> attributes, Dictionary<uint, uint> remap, bool vector)
    {
        foreach (var attribute in attributes)
        {
            if (!vector)
            {
                attribute.Value = new Variant(remap.GetValueOrDefault(attribute.Value.GetUInt()));
                continue;
            }

            if (attribute.Value.Type == VariantType.Int)
            {
                attribute.Value = new Variant(remap.GetValueOrDefault(attribute.Value.GetUInt()));
            }
            else
            {
                var values = new VariantVector();
                foreach (var value in attribute.Value.GetVariantVector())
                    values.Add(new Variant(remap.GetValueOrDefault(value.GetUInt())));
                attribute.Value = new Variant(values);
            }
        }
    }

    private static void PruneIds(NodePrefab node, Dictionary<uint, uint> nodeIdRemap, Dictionary<uint, uint> componentIdRemap)
    {
        node.Node.Id = nodeIdRemap.GetValueOrDefault(node.Node.Id);
        foreach (var component in node.Components)
            component.Id = componentIdRemap.GetValueOrDefault(component.Id);
        foreach (var child in node.Children)
            PruneIds(child, nodeIdRemap, componentIdRemap);
    }

    public bool IsEmpty()
    {
        return Node.Attributes.Count == 0 && Components.Count == 0 && Children.Count == 0;
    }

    public void Clear()
    {
        Node.TypeName = string.Empty;
        Node.Id = 0;
        Node.Temporary = false;
        Node.Attributes.Clear();
        Components.Clear();
        Children.Clear();
    }

    public void SerializeInBlock(Archive archive, PrefabArchiveFlag flags = PrefabArchiveFlag.None, bool compactSave = false)
    {
        using (archive.OpenUnorderedBlock("node"))
            Node.SerializeInBlock(archive, PrefabTypes.ToNodeFlags(flags), compactSave);
        SerializeList(archive, "components", "component", Components,
            (value, ar) => value.SerializeInBlock(ar, PrefabTypes.ToComponentFlags(flags), compactSave));
        SerializeList(archive, "children", "child", Children,
            (value, ar) => value.SerializeInBlock(ar, flags, compactSave));
    }

    private static void SerializeList<T>(Archive archive, string name, string itemName, List<T> values, Action<T, Archive> serialize)
        where T : new()
    {
        if (archive.IsInput && archive.IsUnorderedAccessSupportedInCurrentBlock && !archive.HasElementOrBlock(name))
        {
            values.Clear();
            return;
        }
        if (!archive.IsInput && values.Count == 0 && archive.IsUnorderedAccessSupportedInCurrentBlock)
            return;

        var count = archive.IsInput ? 0u : (uint)values.Count;
        using var block = archive.OpenArrayBlock(name, count);
        if (archive.IsInput)
        {
            values.Clear();
            for (var i = 0u; i < block.SizeHint; ++i)
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
}
