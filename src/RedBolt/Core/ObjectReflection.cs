using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace RedBolt;

public static class ObjectReflection<T>
{
    public static Type Type { get; } = typeof(T);

    public static string ClassName { get; } = Type.GetClassName();

    public static StringHash TypeId { get; } = ClassName;
}

public sealed class ObjectReflection : IDisposable
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

    public AttributeHandle AddAttribute(AttributeInfo attribute)
    {
        ArgumentNullException.ThrowIfNull(attribute);
        var copy = (AttributeInfo)attribute.Clone();
        _attributes.Add(copy);
        _attributeNames.Add(copy.NameHash);
        return new AttributeHandle(copy);
    }

    public void RemoveAttribute(StringHash nameHash)
    {
        var index = _attributeNames.IndexOf(nameHash);
        if (index < 0)
        {
            Log.GetLogger().LogWarning("Cannot find attribute {NameHash}", nameHash.ToDebugString());
            return;
        }
        var attribute = _attributes[index];
        _attributes.RemoveAt(index);
        attribute.Dispose();
        _attributeNames.RemoveAt(index);
    }

    public void RemoveAllAttributes()
    {
        for (var i = _attributes.Count - 1; i >= 0; --i)
            _attributes[i].Dispose();
        _attributes.Clear();
        _attributeNames.Clear();
    }

    public void CopyAttributesFrom(ObjectReflection? other)
    {
        if (other is null)
        {
            Log.GetLogger().LogWarning("Attempt to copy base attributes from unknown base class for class {TypeName}", ClassName);
            return;
        }
        if (ReferenceEquals(other, this))
        {
            Log.GetLogger().LogWarning("Attempt to copy base attributes to itself for class {TypeName}", ClassName);
            return;
        }
        foreach (var attribute in other._attributes)
            AddAttribute(attribute);
    }

    public void UpdateAttributeDefaultValue(StringHash nameHash, Variant value)
    {
        var attribute = GetAttribute(nameHash);
        if (attribute is not null)
            attribute.SetDefaultValue(value);
        else
            Log.GetLogger().LogWarning("Cannot find attribute {NameHash}", nameHash.ToDebugString());
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

    public ref readonly Variant GetMetadata(StringHash key)
    {
        ref var value = ref CollectionsMarshal.GetValueRefOrNullRef(_metadata, key);
        if (!Unsafe.IsNullRef(ref value))
            return ref value;
        return ref Variant.Empty;
    }

    public T GetMetadata<T>(StringHash key) => GetMetadata(key).Get<T>();

    public void SetMetadata(StringHash key, Variant value)
    {
        var copy = value.Clone();
        ref var storedValue = ref CollectionsMarshal.GetValueRefOrAddDefault(_metadata, key, out var exists);
        if (exists)
            storedValue.Dispose();
        storedValue = copy;
    }

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

    public void Dispose()
    {
        _attributeNames.Clear();
        for (var i = _attributes.Count - 1; i >= 0; --i)
            _attributes[i].Dispose();
        _attributes.Clear();
        foreach (var value in _metadata.Values)
        {
            var ownedValue = value;
            ownedValue.Dispose();
        }
        _metadata.Clear();
        _factory = null;
        GC.SuppressFinalize(this);
    }

    private static string GetClassName(Type type)
    {
        return type.GetClassName();
    }

    private static StringHash GetTypeId(Type type, string className)
    {
        return className;
    }
}

public class ObjectReflectionRegistry : IDisposable
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
            throw new InvalidOperationException($"Object {typeof(T).GetClassName()} is reflected multiple times.");

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

    public ObjectReflection AddGeneratedReflection(Type type, string category, Func<Object>? factory)
    {
        ArgumentNullException.ThrowIfNull(type);
        if (!typeof(Object).IsAssignableFrom(type))
            throw new ArgumentException($"Type {type.FullName} must derive from {typeof(Object).FullName}.", nameof(type));

        var typeId = new StringHash(type.GetClassName());
        if (IsReflected(typeId))
            throw new InvalidOperationException($"Object {type.GetClassName()} is reflected multiple times.");

        var reflection = Reflect(type);
        SetReflectionCategory(type, category);
        if (factory is not null)
            reflection.SetObjectFactory(factory);
        return reflection;
    }

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

    public void RemoveReflection(StringHash typeNameHash)
    {
        if (_reflections.Remove(typeNameHash, out var reflection))
        {
            RemoveReflectionFromCategory(reflection);
            ReflectionRemoved?.Invoke(reflection);
            reflection.Dispose();
        }
        else
            Log.GetLogger().LogWarning("Reflection of object {TypeName} is not found", typeNameHash.ToDebugString());
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

    public virtual void Dispose()
    {
        _categories.Clear();
        foreach (var reflection in _reflections.Values)
            reflection.Dispose();
        _reflections.Clear();
        GC.SuppressFinalize(this);
    }

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
