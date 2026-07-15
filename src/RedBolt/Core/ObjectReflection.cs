using Microsoft.Extensions.Logging;

namespace RedBolt;

public static class ObjectReflection<T>
{
    public static Type Type { get; } = typeof(T);

    public static string ClassName { get; } = Type.GetClassName();

    public static StringHash TypeId { get; } = ClassName;
}

public sealed class ObjectReflection
{
    private readonly List<AttributeInfo> _attributes = [];
    private readonly List<StringHash> _attributeNames = [];
    private readonly Dictionary<StringHash, Variant> _metadata = [];
    private Func<Object>? _factory;

    public ObjectReflection(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        ManagedType = type;
        ClassName = GetClassName(type);
        TypeId = GetTypeId(type, ClassName);
    }

    public IReadOnlyList<AttributeInfo> Attributes => _attributes;
    public string Category { get; private set; } = string.Empty;
    public string ClassName { get; }
    public Type ManagedType { get; }
    public uint NumAttributes => (uint)_attributes.Count;
    public AttributeScopeHint ScopeHint { get; private set; } = AttributeScopeHint.Attribute;
    public StringHash TypeId { get; }

    // C++: AttributeHandle ObjectReflection::AddAttribute(const AttributeInfo& attr)
    // C++: {
    // C++:     // None or pointer types can not be supported
    // C++:     if (attr.type_ == VAR_NONE || attr.type_ == VAR_VOIDPTR || attr.type_ == VAR_PTR)
    // C++:     {
    // C++:         URHO3D_LOGWARNING("Attempt to register unsupported attribute type {} to class {}",
    // C++:             Variant::GetTypeName(attr.type_), GetTypeName());
    // C++:         return AttributeHandle();
    // C++:     }
    // C++:     // Only SharedPtr<> of Serializable or it's subclasses are supported in attributes
    // C++:     if (attr.type_ == VAR_CUSTOM && !attr.defaultValue_.IsCustomType<SharedPtr<Serializable>>())
    // C++:     {
    // C++:         URHO3D_LOGWARNING("Attempt to register unsupported attribute of custom type to class {}", GetTypeName());
    // C++:         return AttributeHandle();
    // C++:     }
    // C++:     AttributeHandle handle;
    // C++:     attributes_.push_back(attr);
    // C++:     attributeNames_.push_back(attr.nameHash_);
    // C++:     handle.attributeInfo_ = &attributes_.back();
    // C++:     return handle;
    // C++: }
    public AttributeHandle AddAttribute(AttributeInfo attribute)
    {
        ArgumentNullException.ThrowIfNull(attribute);
        var copy = (AttributeInfo)attribute.Clone();
        _attributes.Add(copy);
        _attributeNames.Add(copy.NameHash);
        return new AttributeHandle(copy);
    }

    // C++: void ObjectReflection::RemoveAttribute(StringHash nameHash)
    // C++: {
    // C++:     const unsigned index = attributeNames_.index_of(nameHash);
    // C++:     if (index >= attributeNames_.size())
    // C++:     {
    // C++:         URHO3D_LOGWARNING("Cannot find attribute {}", nameHash.ToDebugString());
    // C++:         return;
    // C++:     }
    // C++:     attributes_.erase_at(index);
    // C++:     attributeNames_.erase_at(index);
    // C++: }
    public void RemoveAttribute(StringHash nameHash)
    {
        var index = _attributeNames.IndexOf(nameHash);
        if (index < 0)
            return;
        _attributes.RemoveAt(index);
        _attributeNames.RemoveAt(index);
    }

    public void RemoveAllAttributes()
    {
        _attributes.Clear();
        _attributeNames.Clear();
    }

    // C++: void ObjectReflection::CopyAttributesFrom(const ObjectReflection* other)
    // C++: {
    // C++:     if (!other)
    // C++:     {
    // C++:         URHO3D_LOGWARNING("Attempt to copy base attributes from unknown base class for class " + GetTypeName());
    // C++:         return;
    // C++:     }
    // C++:     // Prevent endless loop if mistakenly copying attributes from same class as derived
    // C++:     if (other == this)
    // C++:     {
    // C++:         URHO3D_LOGWARNING("Attempt to copy base attributes to itself for class " + GetTypeName());
    // C++:         return;
    // C++:     }
    // C++:     attributes_.append(other->attributes_);
    // C++:     attributeNames_.append(other->attributeNames_);
    // C++: }
    public void CopyAttributesFrom(ObjectReflection other)
    {
        if (other is null || ReferenceEquals(other, this))
            return;
        foreach (var attribute in other._attributes)
            AddAttribute(attribute);
    }

    // C++: void ObjectReflection::UpdateAttributeDefaultValue(StringHash nameHash, const Variant& defaultValue)
    // C++: {
    // C++:     AttributeInfo* info = GetAttribute(nameHash);
    // C++:     if (info)
    // C++:         info->defaultValue_ = defaultValue;
    // C++:     else
    // C++:         URHO3D_LOGWARNING("Cannot find attribute {}", nameHash.ToDebugString());
    // C++: }
    public void UpdateAttributeDefaultValue(StringHash nameHash, Variant value)
    {
        var attribute = GetAttribute(nameHash);
        if (attribute is not null)
            attribute.DefaultValue = value;
    }

    public uint GetAttributeIndex(StringHash nameHash)
    {
        var index = _attributeNames.IndexOf(nameHash);
        return index < 0 ? uint.MaxValue : (uint)index;
    }

    public uint GetAttributeIndex(StringHash nameHash, uint hintIndex)
    {
        hintIndex = Math.Min(hintIndex, (uint)_attributeNames.Count);
        for (var index = hintIndex; index < _attributeNames.Count; ++index)
        {
            if (_attributeNames[(int)index] == nameHash)
                return index;
        }
        for (uint index = 0; index < hintIndex; ++index)
        {
            if (_attributeNames[(int)index] == nameHash)
                return index;
        }
        return uint.MaxValue;
    }

    public AttributeInfo GetAttribute(StringHash nameHash)
    {
        var index = GetAttributeIndex(nameHash);
        return index == uint.MaxValue ? null! : _attributes[(int)index];
    }

    public AttributeInfo GetAttributeByIndex(uint index) => _attributes[(int)index];

    public Variant GetMetadata(StringHash key) => _metadata.GetValueOrDefault(key);

    public void SetMetadata(StringHash key, Variant value) => _metadata[key] = value;

    public void SetScopeHint(AttributeScopeHint hint) => ScopeHint = hint;

    public AttributeScopeHint GetEffectiveScopeHint()
    {
        var result = ScopeHint;
        foreach (var attribute in _attributes)
            result = (AttributeScopeHint)Math.Max((int)result, (int)attribute.ScopeHint);
        return result;
    }

    public void SetCategory(string category) => Category = category ?? string.Empty;

    public void SetObjectFactory(Func<Object> callback) => _factory = callback;

    public Object CreateObject() => _factory is null ? null! : _factory();

    public bool HasObjectFactory() => _factory is not null;

    private static string GetClassName(Type type)
    {
        return type.GetClassName();
    }

    private static StringHash GetTypeId(Type type, string className)
    {
        return className;
    }
}

public class ObjectReflectionRegistry
{
    private readonly Dictionary<StringHash, ObjectReflection> _reflections = [];
    private readonly Dictionary<string, List<StringHash>> _categories = new(StringComparer.Ordinal);

    public event Action<ObjectReflection>? ReflectionRemoved;

    public ObjectReflection Reflect(Type type)
    {
        var reflection = new ObjectReflection(type);
        if (_reflections.TryGetValue(reflection.TypeId, out var existing))
            return existing;
        _reflections.Add(reflection.TypeId, reflection);
        AddReflectionToCategory(reflection);
        return reflection;
    }

    public ObjectReflection Reflect<T>() => Reflect(typeof(T));

    public ObjectReflection AddReflection<T>(string category = "") where T : Object
    {
        if (IsReflected<T>())
            throw new InvalidOperationException($"Object {ObjectReflection<T>.ClassName} is reflected multiple times.");

        var reflection = Reflect<T>();
        SetReflectionCategory(typeof(T), category);
        return reflection;
    }

    public ObjectReflection AddFactoryReflection<T>(string category = "") where T : Object, new()
    {
        var reflection = AddReflection<T>(category);
        reflection.SetObjectFactory(static () => new T());
        return reflection;
    }

    public ObjectReflection AddAbstractReflection<T>(string category = "") where T : Object => AddReflection<T>(category);

    public ObjectReflection GetReflection(StringHash typeNameHash) => _reflections.GetValueOrDefault(typeNameHash)!;

    public ObjectReflection GetReflection<T>() => GetReflection(ObjectReflection<T>.TypeId);

    public bool IsReflected(StringHash typeNameHash) => _reflections.ContainsKey(typeNameHash);

    public bool IsReflected<T>() => IsReflected(ObjectReflection<T>.TypeId);

    public void RemoveAttribute<T>(StringHash nameHash) where T : Object
    {
        GetReflection<T>()?.RemoveAttribute(nameHash);
    }

    public void UpdateAttributeDefaultValue<T>(StringHash nameHash, Variant value) where T : Object
    {
        GetReflection<T>()?.UpdateAttributeDefaultValue(nameHash, value);
    }

    public void SetReflectionCategory(Type type, string category)
    {
        var reflection = Reflect(type);
        RemoveReflectionFromCategory(reflection);
        reflection.SetCategory(category);
        AddReflectionToCategory(reflection);
    }

    public void SetReflectionCategory<T>(string category) => SetReflectionCategory(typeof(T), category);

    // C++: void ObjectReflectionRegistry::RemoveReflection(StringHash typeNameHash)
    // C++: {
    // C++:     const auto iter = reflections_.find(typeNameHash);
    // C++:     if (iter == reflections_.end())
    // C++:     {
    // C++:         ErrorReflectionNotFound(typeNameHash);
    // C++:         return;
    // C++:     }
    // C++:     const auto reflection = iter->second;
    // C++:     RemoveReflectionFromCurrentCategory(reflection);
    // C++:     reflections_.erase(iter);
    // C++:     OnReflectionRemoved(this, reflection.Get());
    // C++: }
    public void RemoveReflection(StringHash typeNameHash)
    {
        if (_reflections.Remove(typeNameHash, out var reflection))
        {
            RemoveReflectionFromCategory(reflection);
            ReflectionRemoved?.Invoke(reflection);
        }
        else
            Log.Logger.LogWarning("Reflection of object {TypeName} is not found", typeNameHash.ToDebugString());
    }

    public void RemoveReflection<T>()
    {
        var reflection = GetReflection<T>();
        if (reflection is not null)
            RemoveReflection(reflection.TypeId);
    }

    public Object CreateObject(StringHash typeNameHash) => GetReflection(typeNameHash)?.CreateObject()!;

    public IReadOnlyDictionary<StringHash, ObjectReflection> GetObjectReflections() => _reflections;

    public IReadOnlyDictionary<string, List<StringHash>> GetObjectCategories() => _categories;

    private void AddReflectionToCategory(ObjectReflection reflection)
    {
        if (!_categories.TryGetValue(reflection.Category, out var category))
        {
            category = [];
            _categories.Add(reflection.Category, category);
        }
        category.Add(reflection.TypeId);
    }

    private void RemoveReflectionFromCategory(ObjectReflection reflection)
    {
        if (_categories.TryGetValue(reflection.Category, out var category))
            category.Remove(reflection.TypeId);
    }

}
