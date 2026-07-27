namespace RedBolt;

public interface IArchiveSerializable
{
    void SerializeInBlock(Archive archive);
}

public class CustomVariantValue : IEquatable<CustomVariantValue>, ICloneable, IDisposable
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
        _value = value is ICloneable cloneable ? cloneable.Clone() : value;
        Type = value.GetType();
    }

    protected CustomVariantValue(Type type)
    {
        Type = type;
    }

    public Type Type { get; }

    public virtual object Value => _value;

    public virtual object Clone()
    {
        return Type == typeof(void) ? new CustomVariantValue() : new CustomVariantValue(Value);
    }

    public virtual void Dispose()
    {
        if (_value is ICloneable && _value is IDisposable disposable)
            disposable.Dispose();
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

public sealed class CustomVariantValueImpl<T> : CustomVariantValue
{
    private static readonly bool IsCloneable = typeof(ICloneable).IsAssignableFrom(typeof(T));
    private static readonly bool IsDisposableValue = IsCloneable && typeof(IDisposable).IsAssignableFrom(typeof(T));
    private T _typedValue;

    public CustomVariantValueImpl(T value)
        : base(typeof(T))
    {
        _typedValue = CopyValue(value);
    }

    public ref T TypedValue => ref _typedValue;

    public override object Value => _typedValue!;

    public override object Clone() => new CustomVariantValueImpl<T>(_typedValue);

    public override void Dispose()
    {
        if (IsDisposableValue)
            ((IDisposable)(object)_typedValue!).Dispose();
    }

    public override bool IsZero() => EqualityComparer<T>.Default.Equals(_typedValue, default!);

    public override bool Equals(CustomVariantValue? other)
    {
        return other is CustomVariantValueImpl<T> typedOther
            && EqualityComparer<T>.Default.Equals(_typedValue, typedOther._typedValue);
    }

    public override int GetHashCode() => EqualityComparer<T>.Default.GetHashCode(_typedValue!);

    private static T CopyValue(T value)
    {
        return IsCloneable ? (T)((ICloneable)(object)value!).Clone() : value;
    }
}
