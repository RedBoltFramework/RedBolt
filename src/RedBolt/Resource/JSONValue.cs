using System.Text.Json.Nodes;

namespace RedBolt;

public enum JSONValueType
{
    Null = 0,
    Bool,
    Number,
    String,
    Array,
    Object
}

public enum JSONNumberType
{
    NaN = 0,
    Int,
    UInt,
    FloatDouble
}

public sealed class JSONValue : IEquatable<JSONValue>
{
    private static readonly IReadOnlyList<JSONValue> EmptyArray = Array.Empty<JSONValue>();
    private static readonly IReadOnlyDictionary<string, JSONValue> EmptyObject = new Dictionary<string, JSONValue>(StringComparer.Ordinal);

    private Action? _onChanged;
    private JSONValueType _valueType;
    private JSONNumberType _numberType;
    private bool _boolValue;
    private double _numberValue;
    private string _stringValue = string.Empty;
    private List<JSONValue> _arrayValue = [];
    private Dictionary<string, JSONValue> _objectValue = new(StringComparer.Ordinal);

    public JSONValue()
    {
        ResetType(JSONValueType.Null);
    }

    public JSONValue(JSONValueType valueType, JSONNumberType numberType = JSONNumberType.NaN)
    {
        ResetType(valueType, numberType);
    }

    public JSONValue(bool value)
        : this()
    {
        Set(value);
    }

    public JSONValue(int value)
        : this()
    {
        Set(value);
    }

    public JSONValue(uint value)
        : this()
    {
        Set(value);
    }

    public JSONValue(float value)
        : this()
    {
        Set(value);
    }

    public JSONValue(double value)
        : this()
    {
        Set(value);
    }

    public JSONValue(string value)
        : this()
    {
        Set(value);
    }

    public JSONValue(IEnumerable<JSONValue> value)
        : this()
    {
        Set(value);
    }

    public JSONValue(IReadOnlyDictionary<string, JSONValue> value)
        : this()
    {
        Set(value);
    }

    public JSONValue(JSONValue value)
        : this()
    {
        ArgumentNullException.ThrowIfNull(value);
        CopyFrom(value);
    }

    public JSONValue this[int index]
    {
        get => IsArray && index >= 0 && index < _arrayValue.Count ? _arrayValue[index] : new JSONValue();
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            EnsureArray();
            while (_arrayValue.Count <= index)
                _arrayValue.Add(CreateChild(new JSONValue()));

            _arrayValue[index] = CreateChild(value);
            NotifyChanged();
        }
    }

    public JSONValue this[string key]
    {
        get
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            return IsObject && _objectValue.TryGetValue(key, out var value) ? value : new JSONValue();
        }
        set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            Set(key, value);
        }
    }

    public bool IsNull => _valueType == JSONValueType.Null;

    public bool IsBool => _valueType == JSONValueType.Bool;

    public bool IsNumber => _valueType == JSONValueType.Number;

    public bool IsString => _valueType == JSONValueType.String;

    public bool IsArray => _valueType == JSONValueType.Array;

    public bool IsObject => _valueType == JSONValueType.Object;

    public JSONValueType GetValueType() => _valueType;

    public JSONNumberType GetNumberType() => IsNumber ? _numberType : JSONNumberType.NaN;

    public string GetValueTypeName() => _valueType.ToString();

    public string GetNumberTypeName() => GetNumberType().ToString();

    public bool GetBool(bool defaultValue = false) => IsBool ? _boolValue : defaultValue;

    public int GetInt(int defaultValue = 0) => IsNumber ? (int)_numberValue : defaultValue;

    public uint GetUInt(uint defaultValue = 0) => IsNumber ? (uint)Math.Max(_numberValue, 0.0) : defaultValue;

    public float GetFloat(float defaultValue = 0.0f) => IsNumber ? (float)_numberValue : defaultValue;

    public double GetDouble(double defaultValue = 0.0) => IsNumber ? _numberValue : defaultValue;

    public string GetString(string? defaultValue = null) => IsString ? _stringValue : defaultValue ?? string.Empty;

    public IReadOnlyList<JSONValue> GetArray() => IsArray ? _arrayValue : EmptyArray;

    public IReadOnlyDictionary<string, JSONValue> GetObject() => IsObject ? _objectValue : EmptyObject;

    public JSONValue Get(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return this[key];
    }

    public JSONValue Get(int index)
    {
        if (IsObject && index >= 0 && index < _objectValue.Count)
            return _objectValue.ElementAt(index).Value;

        return this[index];
    }

    public uint Size()
    {
        return _valueType switch
        {
            JSONValueType.Array => (uint)_arrayValue.Count,
            JSONValueType.Object => (uint)_objectValue.Count,
            _ => 0u
        };
    }

    public void Push(JSONValue value)
    {
        EnsureArray();
        _arrayValue.Add(CreateChild(value));
        NotifyChanged();
    }

    public void Pop()
    {
        if (!IsArray || _arrayValue.Count == 0)
            return;

        _arrayValue.RemoveAt(_arrayValue.Count - 1);
        NotifyChanged();
    }

    public void Insert(uint position, JSONValue value)
    {
        EnsureArray();
        var index = (int)Math.Min(position, (uint)_arrayValue.Count);
        _arrayValue.Insert(index, CreateChild(value));
        NotifyChanged();
    }

    public void Erase(uint position, uint length = 1)
    {
        if (!IsArray || position >= _arrayValue.Count || length == 0)
            return;

        var count = (int)Math.Min(length, (uint)_arrayValue.Count - position);
        _arrayValue.RemoveRange((int)position, count);
        NotifyChanged();
    }

    public void Resize(uint newSize)
    {
        EnsureArray();
        while (_arrayValue.Count > newSize)
            _arrayValue.RemoveAt(_arrayValue.Count - 1);
        while (_arrayValue.Count < newSize)
            _arrayValue.Add(CreateChild(new JSONValue()));
        NotifyChanged();
    }

    public void Set(string key, JSONValue value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        EnsureObject();
        _objectValue[key] = CreateChild(value);
        NotifyChanged();
    }

    public bool Erase(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (!IsObject || !_objectValue.Remove(key))
            return false;

        NotifyChanged();
        return true;
    }

    public bool Contains(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return IsObject && _objectValue.ContainsKey(key);
    }

    public void Clear()
    {
        if (IsArray)
            _arrayValue.Clear();
        else if (IsObject)
            _objectValue.Clear();
        else
            return;

        NotifyChanged();
    }

    public void SetType(JSONValueType valueType, JSONNumberType numberType = JSONNumberType.NaN)
    {
        ResetType(valueType, numberType);
        NotifyChanged();
    }

    public void Set(bool value)
    {
        ResetType(JSONValueType.Bool);
        _boolValue = value;
        NotifyChanged();
    }

    public void Set(int value)
    {
        ResetType(JSONValueType.Number, JSONNumberType.Int);
        _numberValue = value;
        NotifyChanged();
    }

    public void Set(uint value)
    {
        ResetType(JSONValueType.Number, JSONNumberType.UInt);
        _numberValue = value;
        NotifyChanged();
    }

    public void Set(float value)
    {
        ResetType(JSONValueType.Number, JSONNumberType.FloatDouble);
        _numberValue = value;
        NotifyChanged();
    }

    public void Set(double value)
    {
        ResetType(JSONValueType.Number, JSONNumberType.FloatDouble);
        _numberValue = value;
        NotifyChanged();
    }

    public void Set(string? value)
    {
        ResetType(JSONValueType.String);
        _stringValue = value ?? string.Empty;
        NotifyChanged();
    }

    public void Set(IEnumerable<JSONValue> value)
    {
        ArgumentNullException.ThrowIfNull(value);
        ResetType(JSONValueType.Array);
        foreach (var item in value)
            _arrayValue.Add(CreateChild(item));
        NotifyChanged();
    }

    public void Set(IReadOnlyDictionary<string, JSONValue> value)
    {
        ArgumentNullException.ThrowIfNull(value);
        ResetType(JSONValueType.Object);
        foreach (var item in value)
            _objectValue[item.Key] = CreateChild(item.Value);
        NotifyChanged();
    }

    public void Set(JSONValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        CopyFrom(value);
        NotifyChanged();
    }

    public void SetVariant(Variant value)
    {
        SetType(JSONValueType.Object);
        Set("type", new JSONValue(value.GetTypeName()));
        Set("value", FromVariantValue(value));
    }

    public Variant GetVariant()
    {
        var type = Variant.GetTypeFromName(Get("type").GetString());
        var value = Get("value");
        return type switch
        {
            VariantType.Bool => new Variant(value.GetBool()),
            VariantType.Int => new Variant(value.GetInt()),
            VariantType.Int64 => new Variant((long)value.GetDouble()),
            VariantType.Float => new Variant(value.GetFloat()),
            VariantType.Double => new Variant(value.GetDouble()),
            VariantType.String => new Variant(value.GetString()),
            _ => Variant.FromString(type, value.GetString())
        };
    }

    private static JSONValue FromVariantValue(Variant value)
    {
        return value.Type switch
        {
            VariantType.Bool => new JSONValue(value.GetBool()),
            VariantType.Int => new JSONValue(value.GetInt()),
            VariantType.Int64 => new JSONValue((double)value.GetInt64()),
            VariantType.Float => new JSONValue(value.GetFloat()),
            VariantType.Double => new JSONValue(value.GetDouble()),
            VariantType.String => new JSONValue(value.GetString()),
            _ => new JSONValue(value.ToString())
        };
    }

    public bool Equals(JSONValue? other)
    {
        if (other is null || _valueType != other._valueType || GetNumberType() != other.GetNumberType())
            return false;

        return _valueType switch
        {
            JSONValueType.Null => true,
            JSONValueType.Bool => _boolValue == other._boolValue,
            JSONValueType.Number => _numberValue.Equals(other._numberValue),
            JSONValueType.String => string.Equals(_stringValue, other._stringValue, StringComparison.Ordinal),
            JSONValueType.Array => _arrayValue.SequenceEqual(other._arrayValue),
            JSONValueType.Object => _objectValue.Count == other._objectValue.Count && _objectValue.All(pair => other._objectValue.TryGetValue(pair.Key, out var otherValue) && pair.Value.Equals(otherValue)),
            _ => false
        };
    }

    public override bool Equals(object? obj) => obj is JSONValue value && Equals(value);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(_valueType);
        hash.Add(_numberType);
        hash.Add(_boolValue);
        hash.Add(_numberValue);
        hash.Add(_stringValue, StringComparer.Ordinal);
        foreach (var item in _arrayValue)
            hash.Add(item);
        foreach (var item in _objectValue)
        {
            hash.Add(item.Key, StringComparer.Ordinal);
            hash.Add(item.Value);
        }
        return hash.ToHashCode();
    }

    public override string ToString() => ToJsonNode()?.ToJsonString() ?? "null";

    internal static JSONValue FromJsonNode(JsonNode? node, Action? onChanged = null)
    {
        var value = new JSONValue();
        value.SetChangeCallbackRecursive(onChanged);
        switch (node)
        {
            case null:
                value.ResetType(JSONValueType.Null);
                break;

            case JsonArray jsonArray:
                value.ResetType(JSONValueType.Array);
                foreach (var item in jsonArray)
                    value._arrayValue.Add(FromJsonNode(item, onChanged));
                break;

            case JsonObject jsonObject:
                value.ResetType(JSONValueType.Object);
                foreach (var item in jsonObject)
                    value._objectValue[item.Key] = FromJsonNode(item.Value, onChanged);
                break;

            case JsonValue jsonValue when jsonValue.TryGetValue<bool>(out var boolResult):
                value.ResetType(JSONValueType.Bool);
                value._boolValue = boolResult;
                break;

            case JsonValue jsonValue when jsonValue.TryGetValue<int>(out var intResult):
                value.ResetType(JSONValueType.Number, JSONNumberType.Int);
                value._numberValue = intResult;
                break;

            case JsonValue jsonValue when jsonValue.TryGetValue<uint>(out var uintResult):
                value.ResetType(JSONValueType.Number, JSONNumberType.UInt);
                value._numberValue = uintResult;
                break;

            case JsonValue jsonValue when jsonValue.TryGetValue<double>(out var doubleResult):
                value.ResetType(JSONValueType.Number, JSONNumberType.FloatDouble);
                value._numberValue = doubleResult;
                break;

            case JsonValue jsonValue when jsonValue.TryGetValue<string>(out var stringResult):
                value.ResetType(JSONValueType.String);
                value._stringValue = stringResult ?? string.Empty;
                break;

            default:
                value.ResetType(JSONValueType.Null);
                break;
        }

        return value;
    }

    internal JsonNode? ToJsonNode()
    {
        return _valueType switch
        {
            JSONValueType.Null => null,
            JSONValueType.Bool => JsonValue.Create(_boolValue),
            JSONValueType.Number => _numberType switch
            {
                JSONNumberType.Int => JsonValue.Create(GetInt()),
                JSONNumberType.UInt => JsonValue.Create(GetUInt()),
                _ => JsonValue.Create(_numberValue)
            },
            JSONValueType.String => JsonValue.Create(_stringValue),
            JSONValueType.Array => ToJsonArray(),
            JSONValueType.Object => ToJsonObject(),
            _ => null
        };
    }

    internal void SetChangeCallbackRecursive(Action? callback)
    {
        _onChanged = callback;
        foreach (var item in _arrayValue)
            item.SetChangeCallbackRecursive(callback);
        foreach (var item in _objectValue.Values)
            item.SetChangeCallbackRecursive(callback);
    }

    private void CopyFrom(JSONValue value)
    {
        ResetType(value._valueType, value._numberType);
        _boolValue = value._boolValue;
        _numberValue = value._numberValue;
        _stringValue = value._stringValue;

        if (value.IsArray)
        {
            foreach (var item in value._arrayValue)
                _arrayValue.Add(CreateChild(item));
        }

        if (value.IsObject)
        {
            foreach (var item in value._objectValue)
                _objectValue[item.Key] = CreateChild(item.Value);
        }
    }

    private JSONValue CreateChild(JSONValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var child = new JSONValue(value);
        child.SetChangeCallbackRecursive(_onChanged);
        return child;
    }

    private void EnsureArray()
    {
        if (!IsArray)
            ResetType(JSONValueType.Array);
    }

    private void EnsureObject()
    {
        if (!IsObject)
            ResetType(JSONValueType.Object);
    }

    private void NotifyChanged() => _onChanged?.Invoke();

    private void ResetType(JSONValueType valueType, JSONNumberType numberType = JSONNumberType.NaN)
    {
        this._valueType = valueType;
        this._numberType = valueType == JSONValueType.Number
            ? numberType == JSONNumberType.NaN ? JSONNumberType.FloatDouble : numberType
            : JSONNumberType.NaN;
        _boolValue = false;
        _numberValue = 0.0;
        _stringValue = string.Empty;
        _arrayValue = [];
        _objectValue = new Dictionary<string, JSONValue>(StringComparer.Ordinal);
    }

    private JsonArray ToJsonArray()
    {
        var result = new JsonArray();
        foreach (var item in _arrayValue)
            result.Add(item.ToJsonNode());
        return result;
    }

    private JsonObject ToJsonObject()
    {
        var result = new JsonObject();
        foreach (var item in _objectValue)
            result[item.Key] = item.Value.ToJsonNode();
        return result;
    }
}
