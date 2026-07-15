using System.Globalization;

namespace RedBolt;

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

public sealed class PListValue : IEquatable<PListValue>, ICloneable, IDisposable
{
    private static readonly IReadOnlyDictionary<string, PListValue> EmptyValueMap = new Dictionary<string, PListValue>(StringComparer.Ordinal);
    private static readonly IReadOnlyList<PListValue> EmptyValueVector = Array.Empty<PListValue>();

    private PListValueType _type;
    private int _intValue;
    private bool _boolValue;
    private float _floatValue;
    private string _stringValue = string.Empty;
    private Dictionary<string, PListValue> _valueMap = new(StringComparer.Ordinal);
    private List<PListValue> _valueVector = [];

    public PListValue()
    {
    }

    public PListValue(int value)
    {
        SetInt(value);
    }

    public PListValue(bool value)
    {
        SetBool(value);
    }

    public PListValue(float value)
    {
        SetFloat(value);
    }

    public PListValue(string value)
    {
        SetString(value);
    }

    public PListValue(IReadOnlyDictionary<string, PListValue> valueMap)
    {
        SetValueMap(valueMap);
    }

    public PListValue(IReadOnlyList<PListValue> valueVector)
    {
        SetValueVector(valueVector);
    }

    public PListValue(PListValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        CopyFrom(value);
    }

    public object Clone() => new PListValue(this);

    public static explicit operator bool(PListValue? value) => value is not null && value._type != PListValueType.None;

    public static bool operator ==(PListValue left, PListValue right) => Equals(left, right);

    public static bool operator !=(PListValue left, PListValue right) => !Equals(left, right);

    public new PListValueType GetType() => _type;

    public void SetInt(int value)
    {
        if (_type != PListValueType.Int)
        {
            Reset();
            _type = PListValueType.Int;
        }
        _intValue = value;
    }

    public void SetBool(bool value)
    {
        if (_type != PListValueType.Bool)
        {
            Reset();
            _type = PListValueType.Bool;
        }
        _boolValue = value;
    }

    public void SetFloat(float value)
    {
        if (_type != PListValueType.Float)
        {
            Reset();
            _type = PListValueType.Float;
        }
        _floatValue = value;
    }

    public void SetString(string value)
    {
        if (_type != PListValueType.String)
        {
            Reset();
            _type = PListValueType.String;
        }
        _stringValue = value ?? string.Empty;
    }

    public void SetValueMap(IReadOnlyDictionary<string, PListValue> valueMap)
    {
        ArgumentNullException.ThrowIfNull(valueMap);
        if (_type != PListValueType.ValueMap)
        {
            Reset();
            _type = PListValueType.ValueMap;
        }
        _valueMap.Clear();
        foreach (var item in valueMap)
            this._valueMap[item.Key] = new PListValue(item.Value);
    }

    public void SetValueVector(IReadOnlyList<PListValue> valueVector)
    {
        ArgumentNullException.ThrowIfNull(valueVector);
        if (_type != PListValueType.ValueVector)
        {
            Reset();
            _type = PListValueType.ValueVector;
        }
        _valueVector.Clear();
        foreach (var value in valueVector)
            _valueVector.Add(new PListValue(value));
    }

    public int GetInt() => _type == PListValueType.Int ? _intValue : 0;

    public bool GetBool() => _type == PListValueType.Bool && _boolValue;

    public float GetFloat() => _type == PListValueType.Float ? _floatValue : 0.0f;

    public string GetString() => _type == PListValueType.String ? _stringValue : string.Empty;

    // C++: uses sscanf(string_->c_str(), "{{%d,%d},{%d,%d}}", &x, &y, &w, &h)
    public IntRect GetIntRect()
    {
        if (_type != PListValueType.String)
            return IntRect.Zero;

        // C++: specific format "{{%d,%d},{%d,%d}}" parsing via sscanf
        var numbers = ParseNumbers();
        return numbers.Length >= 4
            ? new IntRect(numbers[0], numbers[1], numbers[0] + numbers[2], numbers[1] + numbers[3])
            : IntRect.Zero;
    }

    // C++: uses sscanf(string_->c_str(), "{%d,%d}", &x, &y) for specific format parsing
    public Vector2i GetIntVector2()
    {
        if (_type != PListValueType.String)
            return Vector2i.Zero;

        var numbers = ParseNumbers();
        return numbers.Length >= 2 ? new Vector2i(numbers[0], numbers[1]) : Vector2i.Zero;
    }

    // C++: uses sscanf(string_->c_str(), "{%d,%d,%d}", &x, &y, &z) for specific format parsing
    public Vector3i GetIntVector3()
    {
        if (_type != PListValueType.String)
            return Vector3i.Zero;

        var numbers = ParseNumbers();
        return numbers.Length >= 3 ? new Vector3i(numbers[0], numbers[1], numbers[2]) : Vector3i.Zero;
    }

    public IReadOnlyDictionary<string, PListValue> GetValueMap() => _type == PListValueType.ValueMap ? _valueMap : EmptyValueMap;

    public IReadOnlyList<PListValue> GetValueVector() => _type == PListValueType.ValueVector ? _valueVector : EmptyValueVector;

    public Dictionary<string, PListValue> ConvertToValueMap()
    {
        if (_type != PListValueType.ValueMap)
        {
            Reset();
            _type = PListValueType.ValueMap;
            _valueMap = new Dictionary<string, PListValue>(StringComparer.Ordinal);
        }

        return _valueMap;
    }

    public List<PListValue> ConvertToValueVector()
    {
        if (_type != PListValueType.ValueVector)
        {
            Reset();
            _type = PListValueType.ValueVector;
            _valueVector = [];
        }

        return _valueVector;
    }

    public bool Equals(PListValue? other)
    {
        if (other is null || _type != other._type)
            return false;

        return _type switch
        {
            PListValueType.None => true,
            PListValueType.Int => _intValue == other._intValue,
            PListValueType.Bool => _boolValue == other._boolValue,
            PListValueType.Float => _floatValue == other._floatValue,
            PListValueType.String => string.Equals(_stringValue, other._stringValue, StringComparison.Ordinal),
            PListValueType.ValueMap => _valueMap.Count == other._valueMap.Count && _valueMap.All(item => other._valueMap.TryGetValue(item.Key, out var otherValue) && item.Value.Equals(otherValue)),
            PListValueType.ValueVector => ManagedSequence.Equal(_valueVector, other._valueVector),
            _ => false
        };
    }

    public override bool Equals(object? obj) => obj is PListValue other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(_type);
        hash.Add(_intValue);
        hash.Add(_boolValue);
        hash.Add(_floatValue);
        hash.Add(_stringValue, StringComparer.Ordinal);
        foreach (var item in _valueMap)
        {
            hash.Add(item.Key, StringComparer.Ordinal);
            hash.Add(item.Value);
        }
        foreach (var item in _valueVector)
            hash.Add(item);
        return hash.ToHashCode();
    }

    private void CopyFrom(PListValue value)
    {
        switch (value._type)
        {
            case PListValueType.None:
                Reset();
                break;
            case PListValueType.Int:
                SetInt(value._intValue);
                break;
            case PListValueType.Bool:
                SetBool(value._boolValue);
                break;
            case PListValueType.Float:
                SetFloat(value._floatValue);
                break;
            case PListValueType.String:
                SetString(value._stringValue);
                break;
            case PListValueType.ValueMap:
                SetValueMap(value._valueMap);
                break;
            case PListValueType.ValueVector:
                SetValueVector(value._valueVector);
                break;
        }
    }

    private int[] ParseNumbers()
    {
        var numbers = new List<int>();
        var index = 0;
        while (index < _stringValue.Length)
        {
            while (index < _stringValue.Length && !char.IsDigit(_stringValue[index]) && _stringValue[index] != '-')
                ++index;
            if (index >= _stringValue.Length)
                break;

            var start = index++;
            while (index < _stringValue.Length && char.IsDigit(_stringValue[index]))
                ++index;
            if (int.TryParse(_stringValue.AsSpan(start, index - start), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                numbers.Add(value);
        }

        return [.. numbers];
    }

    public void Dispose() => Reset();

    private void Reset()
    {
        _type = PListValueType.None;
        _intValue = 0;
        _boolValue = false;
        _floatValue = 0.0f;
        _stringValue = string.Empty;
        _valueMap = new Dictionary<string, PListValue>(StringComparer.Ordinal);
        _valueVector = [];
    }
}
