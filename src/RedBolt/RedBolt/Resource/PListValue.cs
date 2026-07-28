namespace RedBolt.Resource;

using System.Globalization;
using System.Runtime.InteropServices;

using RedBolt.Input;
using RedBolt.Math;

/// <summary>
/// PList value types.
/// </summary>
public enum PListValueType
{
    None = 0,
    Int,
    Bool,
    Float,
    String,
    ValueMap,
    ValueVector
}

/// <summary>
/// PList value.
/// </summary>
public class PListValue : IEquatable<PListValue>, ICloneable, IDisposable
{
    [StructLayout(LayoutKind.Explicit)]
    private struct ValueUnion
    {
        [FieldOffset(0)]
        public int Int;

        [FieldOffset(0)]
        public bool Bool;

        [FieldOffset(0)]
        public float Float;
    }

    private static readonly PListValue EmptyValue = new();
    private static readonly IReadOnlyDictionary<string, PListValue> EmptyValueMap =
        new Dictionary<string, PListValue>(StringComparer.Ordinal);
    private static readonly IReadOnlyList<PListValue> EmptyValueVector = Array.Empty<PListValue>();

    private PListValueType _type;
    private ValueUnion _value;
    private object? _referenceValue;

    /// <summary>
    /// Construct.
    /// </summary>
    public PListValue()
    {
    }

    /// <summary>
    /// Construct from int.
    /// </summary>
    public PListValue(int value)
    {
        SetInt(value);
    }

    /// <summary>
    /// Construct from boolean.
    /// </summary>
    public PListValue(bool value)
    {
        SetBool(value);
    }

    /// <summary>
    /// Construct from float.
    /// </summary>
    public PListValue(float value)
    {
        SetFloat(value);
    }

    /// <summary>
    /// Construct from string.
    /// </summary>
    public PListValue(string value)
    {
        SetString(value);
    }

    /// <summary>
    /// Construct from value map.
    /// </summary>
    public PListValue(IReadOnlyDictionary<string, PListValue> valueMap)
    {
        SetValueMap(valueMap);
    }

    /// <summary>
    /// Construct from value vector.
    /// </summary>
    public PListValue(IReadOnlyList<PListValue> valueVector)
    {
        SetValueVector(valueVector);
    }

    /// <summary>
    /// Construct from another value.
    /// </summary>
    public PListValue(PListValue value)
    {
        CopyFrom(value);
    }

    /// <summary>
    /// Destruct.
    /// </summary>
    public void Dispose()
    {
        Reset();
    }

    /// <summary>
    /// Assign from another value.
    /// </summary>
    public PListValue CopyFrom(PListValue rhs)
    {
        switch (rhs._type)
        {
            case PListValueType.None:
                Reset();
                break;
            case PListValueType.Int:
                SetInt(rhs._value.Int);
                break;
            case PListValueType.Bool:
                SetBool(rhs._value.Bool);
                break;
            case PListValueType.Float:
                SetFloat(rhs._value.Float);
                break;
            case PListValueType.String:
                SetString((string)rhs._referenceValue!);
                break;
            case PListValueType.ValueMap:
                SetValueMap((Dictionary<string, PListValue>)rhs._referenceValue!);
                break;
            case PListValueType.ValueVector:
                SetValueVector((List<PListValue>)rhs._referenceValue!);
                break;
        }

        return this;
    }

    public object Clone() => new PListValue(this);

    /// <summary>
    /// Return true if this value is valid.
    /// </summary>
    public static explicit operator bool(PListValue? value) =>
        value is not null && value._type != PListValueType.None;

    /// <summary>
    /// Equality operator.
    /// </summary>
    public static bool operator ==(PListValue? left, PListValue? right) =>
        ReferenceEquals(left, right) || left is not null && left.Equals(right);

    /// <summary>
    /// Inequality operator.
    /// </summary>
    public static bool operator !=(PListValue? left, PListValue? right) => !(left == right);

    /// <summary>
    /// Set int.
    /// </summary>
    public void SetInt(int value)
    {
        if (_type != PListValueType.Int)
        {
            Reset();
            _type = PListValueType.Int;
        }

        _value.Int = value;
    }

    /// <summary>
    /// Set boolean.
    /// </summary>
    public void SetBool(bool value)
    {
        if (_type != PListValueType.Bool)
        {
            Reset();
            _type = PListValueType.Bool;
        }

        _value.Bool = value;
    }

    /// <summary>
    /// Set float.
    /// </summary>
    public void SetFloat(float value)
    {
        if (_type != PListValueType.Float)
        {
            Reset();
            _type = PListValueType.Float;
        }

        _value.Float = value;
    }

    /// <summary>
    /// Set string.
    /// </summary>
    public void SetString(string value)
    {
        if (_type != PListValueType.String)
        {
            Reset();
            _type = PListValueType.String;
        }

        _referenceValue = value;
    }

    /// <summary>
    /// Set value map.
    /// </summary>
    public void SetValueMap(IReadOnlyDictionary<string, PListValue> valueMap)
    {
        if (_type != PListValueType.ValueMap)
        {
            Reset();
            _type = PListValueType.ValueMap;
            _referenceValue = new Dictionary<string, PListValue>(StringComparer.Ordinal);
        }

        var destination = (Dictionary<string, PListValue>)_referenceValue!;
        if (ReferenceEquals(destination, valueMap))
            return;

        foreach (var value in destination.Values)
            value.Dispose();
        destination.Clear();

        foreach (var item in valueMap)
            destination[item.Key] = new PListValue(item.Value);
    }

    /// <summary>
    /// Set value vector.
    /// </summary>
    public void SetValueVector(IReadOnlyList<PListValue> valueVector)
    {
        if (_type != PListValueType.ValueVector)
        {
            Reset();
            _type = PListValueType.ValueVector;
            _referenceValue = new List<PListValue>();
        }

        var destination = (List<PListValue>)_referenceValue!;
        if (ReferenceEquals(destination, valueVector))
            return;

        foreach (var value in destination)
            value.Dispose();
        destination.Clear();

        foreach (var value in valueVector)
            destination.Add(new PListValue(value));
    }

    /// <summary>
    /// Return type.
    /// </summary>
    public PListValueType Type => _type;

    /// <summary>
    /// Return int.
    /// </summary>
    public int Int => _type == PListValueType.Int ? _value.Int : 0;

    /// <summary>
    /// Return boolean.
    /// </summary>
    public bool Bool => _type == PListValueType.Bool && _value.Bool;

    /// <summary>
    /// Return float.
    /// </summary>
    public float Float => _type == PListValueType.Float ? _value.Float : 0.0f;

    /// <summary>
    /// Return string.
    /// </summary>
    public string String => _type == PListValueType.String ? (string)_referenceValue! : string.Empty;

    /// <summary>
    /// Return IntRect, for string type.
    /// </summary>
    public IntRect IntRect
    {
        get
        {
            if (_type != PListValueType.String)
                return IntRect.Zero;

            Span<int> numbers = stackalloc int[4];
            return ParseNumbers(numbers) >= 4
                ? new IntRect(numbers[0], numbers[1], numbers[0] + numbers[2], numbers[1] + numbers[3])
                : IntRect.Zero;
        }
    }

    /// <summary>
    /// Return IntVector2, for string type.
    /// </summary>
    public Vector2i IntVector2
    {
        get
        {
            if (_type != PListValueType.String)
                return Vector2i.Zero;

            Span<int> numbers = stackalloc int[2];
            return ParseNumbers(numbers) >= 2 ? new Vector2i(numbers[0], numbers[1]) : Vector2i.Zero;
        }
    }

    /// <summary>
    /// Return IntVector3, for string type.
    /// </summary>
    public Vector3i IntVector3
    {
        get
        {
            if (_type != PListValueType.String)
                return Vector3i.Zero;

            Span<int> numbers = stackalloc int[3];
            return ParseNumbers(numbers) >= 3 ? new Vector3i(numbers[0], numbers[1], numbers[2]) : Vector3i.Zero;
        }
    }

    /// <summary>
    /// Return value map.
    /// </summary>
    public IReadOnlyDictionary<string, PListValue> ValueMap =>
        _type == PListValueType.ValueMap
            ? (Dictionary<string, PListValue>)_referenceValue!
            : EmptyValueMap;

    /// <summary>
    /// Return value vector.
    /// </summary>
    public IReadOnlyList<PListValue> ValueVector =>
        _type == PListValueType.ValueVector
            ? (List<PListValue>)_referenceValue!
            : EmptyValueVector;

    /// <summary>
    /// Convert to value map (internal use only).
    /// </summary>
    public Dictionary<string, PListValue> ConvertToValueMap()
    {
        if (_type != PListValueType.ValueMap)
        {
            Reset();
            _type = PListValueType.ValueMap;
            _referenceValue = new Dictionary<string, PListValue>(StringComparer.Ordinal);
        }

        return (Dictionary<string, PListValue>)_referenceValue!;
    }

    /// <summary>
    /// Convert to value vector (internal use only).
    /// </summary>
    public List<PListValue> ConvertToValueVector()
    {
        if (_type != PListValueType.ValueVector)
        {
            Reset();
            _type = PListValueType.ValueVector;
            _referenceValue = new List<PListValue>();
        }

        return (List<PListValue>)_referenceValue!;
    }

    public bool Equals(PListValue? other)
    {
        if (other is null || _type != other._type)
            return false;

        switch (_type)
        {
            case PListValueType.None:
                return true;
            case PListValueType.Int:
                return _value.Int == other._value.Int;
            case PListValueType.Bool:
                return _value.Bool == other._value.Bool;
            case PListValueType.Float:
                return _value.Float == other._value.Float;
            case PListValueType.String:
                return string.Equals((string)_referenceValue!, (string)other._referenceValue!, StringComparison.Ordinal);
            case PListValueType.ValueMap:
            {
                var valueMap = (Dictionary<string, PListValue>)_referenceValue!;
                var otherValueMap = (Dictionary<string, PListValue>)other._referenceValue!;
                if (valueMap.Count != otherValueMap.Count)
                    return false;

                foreach (var item in valueMap)
                {
                    if (!otherValueMap.TryGetValue(item.Key, out var otherValue) || item.Value != otherValue)
                        return false;
                }

                return true;
            }
            case PListValueType.ValueVector:
            {
                var valueVector = (List<PListValue>)_referenceValue!;
                var otherValueVector = (List<PListValue>)other._referenceValue!;
                if (valueVector.Count != otherValueVector.Count)
                    return false;

                for (var i = 0; i < valueVector.Count; ++i)
                {
                    if (valueVector[i] != otherValueVector[i])
                        return false;
                }

                return true;
            }
            default:
                return false;
        }
    }

    public override bool Equals(object? obj) => obj is PListValue other && Equals(other);

    public override int GetHashCode()
    {
        switch (_type)
        {
            case PListValueType.None:
                return (int)PListValueType.None;
            case PListValueType.Int:
                return HashCode.Combine(_type, _value.Int);
            case PListValueType.Bool:
                return HashCode.Combine(_type, _value.Bool);
            case PListValueType.Float:
                return HashCode.Combine(_type, _value.Float);
            case PListValueType.String:
                return HashCode.Combine(_type, StringComparer.Ordinal.GetHashCode((string)_referenceValue!));
            case PListValueType.ValueMap:
            {
                var hash = (int)_type;
                foreach (var item in (Dictionary<string, PListValue>)_referenceValue!)
                    hash ^= HashCode.Combine(StringComparer.Ordinal.GetHashCode(item.Key), item.Value);
                return hash;
            }
            case PListValueType.ValueVector:
            {
                var hash = new HashCode();
                hash.Add(_type);
                foreach (var value in (List<PListValue>)_referenceValue!)
                    hash.Add(value);
                return hash.ToHashCode();
            }
            default:
                return 0;
        }
    }

    private int ParseNumbers(Span<int> numbers)
    {
        var stringValue = (string)_referenceValue!;
        var count = 0;
        var index = 0;
        while (index < stringValue.Length && count < numbers.Length)
        {
            while (index < stringValue.Length
                   && !char.IsDigit(stringValue[index])
                   && stringValue[index] != '-'
                   && stringValue[index] != '+')
            {
                ++index;
            }

            if (index >= stringValue.Length)
                break;

            var start = index++;
            while (index < stringValue.Length && char.IsDigit(stringValue[index]))
                ++index;

            if (int.TryParse(stringValue.AsSpan(start, index - start), NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out var value))
            {
                numbers[count++] = value;
            }
        }

        return count;
    }

    /// <summary>
    /// Reset.
    /// </summary>
    private void Reset()
    {
        if (_type == PListValueType.None)
            return;

        switch (_type)
        {
            case PListValueType.ValueMap:
                foreach (var value in ((Dictionary<string, PListValue>)_referenceValue!).Values)
                    value.Dispose();
                break;
            case PListValueType.ValueVector:
                foreach (var value in (List<PListValue>)_referenceValue!)
                    value.Dispose();
                break;
        }

        _referenceValue = null;
        _type = PListValueType.None;
    }
}
