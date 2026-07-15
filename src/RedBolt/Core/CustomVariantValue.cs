namespace RedBolt;

public interface IArchiveSerializable
{
    void SerializeInBlock(Archive archive);
}

// C++: class CustomVariantValue {
// C++:   friend class CustomVariantValueImpl<T>;
// C++:   CustomVariantValue() = default;
// C++:   virtual ~CustomVariantValue() = default;
// C++:   virtual bool CopyTo(CustomVariantValue& dest) const { return false; }
// C++:   virtual bool MoveTo(CustomVariantValue& dest) { return false; }
// C++:   virtual void RelocateTo(void* dest) { }
// C++:   virtual unsigned GetSize() const { return sizeof(CustomVariantValue); }
// C++:   virtual bool Compare(const CustomVariantValue& rhs) const { return false; }
// C++:   virtual void Serialize(Archive& archive, const char* name) { URHO3D_ASSERT(0); }

public class CustomVariantValue : IEquatable<CustomVariantValue>, ICloneable
{
    public static readonly CustomVariantValue Empty = new();

    private readonly object _value = null!;

    public CustomVariantValue()
    {
        Type = typeof(void);
    }

    public CustomVariantValue(object value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _value = value;
        Type = value.GetType();
    }

    protected CustomVariantValue(Type type)
    {
        Type = type;
    }

    public Type Type { get; }

    // C++: virtual T* GetValuePtr() / const T* GetValuePtr() const — missing
    // C++: virtual bool CopyTo(CustomVariantValue& dest) const — missing
    // C++: virtual bool MoveTo(CustomVariantValue& dest) — missing
    // C++: virtual void RelocateTo(void* dest) — missing
    // C++: virtual unsigned GetSize() const — missing

    public virtual object Value => _value;

    public virtual object Clone()
    {
        return Type == typeof(void) ? new CustomVariantValue() : new CustomVariantValue(Value);
    }

    public bool IsType<T>() => Type == typeof(T);

    public bool TryGetValue<T>(out T value)
    {
        if (Value is T typedValue)
        {
            value = typedValue;
            return true;
        }

        value = default!;
        return false;
    }

    public virtual bool IsZero()
    {
        return Value is not null && Value.Equals(GetDefaultValue(Type));
    }

    public virtual bool Equals(CustomVariantValue? other)
    {
        return other is not null && Type == other.Type && Equals(Value, other.Value);
    }

    public override bool Equals(object? obj) => Equals(obj as CustomVariantValue);

    public override int GetHashCode() => HashCode.Combine(Type, Value);

    public override string ToString() => Value?.ToString() ?? string.Empty;

    public virtual void Serialize(Archive archive, string name)
    {
        ArgumentNullException.ThrowIfNull(archive);
        if (Value is not IArchiveSerializable value)
            throw new NotSupportedException("Serialization is not implemented for this Custom type.");

        using var block = archive.OpenUnorderedBlock(name);
        value.SerializeInBlock(archive);
    }

    private static object GetDefaultValue(Type type)
    {
        return type.IsValueType ? Activator.CreateInstance(type)! : null!;
    }
}

// TODO: port - C++ code:
// template <class T> struct CustomVariantValueTraits {
//     static void Copy(T& dest, const T& src) { dest = src; }
//     static void Move(T& dest, T& src) { dest = std::move(src); }
//     static bool Compare(const T& lhs, const T& rhs) { ... }
//     static bool IsZero(const T& value) { ... }
//     static ea::string ToString(const T& value) { ... }
//     static void Serialize(Archive& archive, const char* name, T& value) { ... }
// };
// template <class T> struct CustomVariantValueTraits<ea::unique_ptr<T>> { ... };
// TODO: port - C++ code:
// constexpr bool IsCustomTypeOnStack() { return sizeof(CustomVariantValueImpl<T>) <= VARIANT_VALUE_SIZE; }

// C++: class CustomVariantValueImpl<T> : CustomVariantValue {
// C++:   using ClassName = CustomVariantValueImpl<T>;
// C++:   using Traits = CustomVariantValueTraits<T>;
// C++:   CustomVariantValueImpl(T&& value);       // move constructor
// C++:   bool CopyTo(...) override; bool MoveTo(...) override;
// C++:   unsigned GetSize() const override;
// C++:   bool Compare(...) override; bool IsZero() override;
// C++:   ea::string ToString() override; void Serialize(...) override;
public sealed class CustomVariantValueImpl<T> : CustomVariantValue
{
    public CustomVariantValueImpl(T value)
        : base(typeof(T))
    {
        TypedValue = value;
    }

    public T TypedValue { get; }

    public override object Value => TypedValue!;

    public override object Clone() => new CustomVariantValueImpl<T>(TypedValue);

    public override bool IsZero() => EqualityComparer<T>.Default.Equals(TypedValue, default!);

    public override bool Equals(CustomVariantValue? other)
    {
        return other is CustomVariantValueImpl<T> typedOther
            && EqualityComparer<T>.Default.Equals(TypedValue, typedOther.TypedValue);
    }

    public override int GetHashCode() => EqualityComparer<T>.Default.GetHashCode(TypedValue!);
}
