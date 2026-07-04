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

    private Action? onChanged;
    private JSONValueType valueType;
    private JSONNumberType numberType;
    private bool boolValue;
    private double numberValue;
    private string stringValue = string.Empty;
    private List<JSONValue> arrayValue = [];
    private Dictionary<string, JSONValue> objectValue = new(StringComparer.Ordinal);

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
        get => IsArray && index >= 0 && index < arrayValue.Count ? arrayValue[index] : new JSONValue();
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            EnsureArray();
            while (arrayValue.Count <= index)
                arrayValue.Add(CreateChild(new JSONValue()));

            arrayValue[index] = CreateChild(value);
            NotifyChanged();
        }
    }

    public JSONValue this[string key]
    {
        get
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            return IsObject && objectValue.TryGetValue(key, out var value) ? value : new JSONValue();
        }
        set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            Set(key, value);
        }
    }

    public bool IsNull => valueType == JSONValueType.Null;

    public bool IsBool => valueType == JSONValueType.Bool;

    public bool IsNumber => valueType == JSONValueType.Number;

    public bool IsString => valueType == JSONValueType.String;

    public bool IsArray => valueType == JSONValueType.Array;

    public bool IsObject => valueType == JSONValueType.Object;

    public JSONValueType GetValueType() => valueType;

    public JSONNumberType GetNumberType() => IsNumber ? numberType : JSONNumberType.NaN;

    public string GetValueTypeName() => valueType.ToString();

    public string GetNumberTypeName() => GetNumberType().ToString();

    public bool GetBool(bool defaultValue = false) => IsBool ? boolValue : defaultValue;

    public int GetInt(int defaultValue = 0) => IsNumber ? (int)numberValue : defaultValue;

    public uint GetUInt(uint defaultValue = 0) => IsNumber ? (uint)Math.Max(numberValue, 0.0) : defaultValue;

    public float GetFloat(float defaultValue = 0.0f) => IsNumber ? (float)numberValue : defaultValue;

    public double GetDouble(double defaultValue = 0.0) => IsNumber ? numberValue : defaultValue;

    public string GetString(string? defaultValue = null) => IsString ? stringValue : defaultValue ?? string.Empty;

    public IReadOnlyList<JSONValue> GetArray() => IsArray ? arrayValue : EmptyArray;

    public IReadOnlyDictionary<string, JSONValue> GetObject() => IsObject ? objectValue : EmptyObject;

    public JSONValue Get(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return this[key];
    }

    public JSONValue Get(int index)
    {
        if (IsObject && index >= 0 && index < objectValue.Count)
            return objectValue.ElementAt(index).Value;

        return this[index];
    }

    public uint Size()
    {
        return valueType switch
        {
            JSONValueType.Array => (uint)arrayValue.Count,
            JSONValueType.Object => (uint)objectValue.Count,
            _ => 0u
        };
    }

    public void Push(JSONValue value)
    {
        EnsureArray();
        arrayValue.Add(CreateChild(value));
        NotifyChanged();
    }

    public void Pop()
    {
        if (!IsArray || arrayValue.Count == 0)
            return;

        arrayValue.RemoveAt(arrayValue.Count - 1);
        NotifyChanged();
    }

    public void Insert(uint position, JSONValue value)
    {
        EnsureArray();
        var index = (int)Math.Min(position, (uint)arrayValue.Count);
        arrayValue.Insert(index, CreateChild(value));
        NotifyChanged();
    }

    public void Erase(uint position, uint length = 1)
    {
        if (!IsArray || position >= arrayValue.Count || length == 0)
            return;

        var count = (int)Math.Min(length, (uint)arrayValue.Count - position);
        arrayValue.RemoveRange((int)position, count);
        NotifyChanged();
    }

    public void Resize(uint newSize)
    {
        EnsureArray();
        while (arrayValue.Count > newSize)
            arrayValue.RemoveAt(arrayValue.Count - 1);
        while (arrayValue.Count < newSize)
            arrayValue.Add(CreateChild(new JSONValue()));
        NotifyChanged();
    }

    public void Set(string key, JSONValue value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        EnsureObject();
        objectValue[key] = CreateChild(value);
        NotifyChanged();
    }

    public bool Erase(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (!IsObject || !objectValue.Remove(key))
            return false;

        NotifyChanged();
        return true;
    }

    public bool Contains(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return IsObject && objectValue.ContainsKey(key);
    }

    public void Clear()
    {
        if (IsArray)
            arrayValue.Clear();
        else if (IsObject)
            objectValue.Clear();
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
        boolValue = value;
        NotifyChanged();
    }

    public void Set(int value)
    {
        ResetType(JSONValueType.Number, JSONNumberType.Int);
        numberValue = value;
        NotifyChanged();
    }

    public void Set(uint value)
    {
        ResetType(JSONValueType.Number, JSONNumberType.UInt);
        numberValue = value;
        NotifyChanged();
    }

    public void Set(float value)
    {
        ResetType(JSONValueType.Number, JSONNumberType.FloatDouble);
        numberValue = value;
        NotifyChanged();
    }

    public void Set(double value)
    {
        ResetType(JSONValueType.Number, JSONNumberType.FloatDouble);
        numberValue = value;
        NotifyChanged();
    }

    public void Set(string? value)
    {
        ResetType(JSONValueType.String);
        stringValue = value ?? string.Empty;
        NotifyChanged();
    }

    public void Set(IEnumerable<JSONValue> value)
    {
        ArgumentNullException.ThrowIfNull(value);
        ResetType(JSONValueType.Array);
        foreach (var item in value)
            arrayValue.Add(CreateChild(item));
        NotifyChanged();
    }

    public void Set(IReadOnlyDictionary<string, JSONValue> value)
    {
        ArgumentNullException.ThrowIfNull(value);
        ResetType(JSONValueType.Object);
        foreach (var item in value)
            objectValue[item.Key] = CreateChild(item.Value);
        NotifyChanged();
    }

    public void Set(JSONValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        CopyFrom(value);
        NotifyChanged();
    }

    public bool Equals(JSONValue? other)
    {
        if (other is null || valueType != other.valueType || GetNumberType() != other.GetNumberType())
            return false;

        return valueType switch
        {
            JSONValueType.Null => true,
            JSONValueType.Bool => boolValue == other.boolValue,
            JSONValueType.Number => numberValue.Equals(other.numberValue),
            JSONValueType.String => string.Equals(stringValue, other.stringValue, StringComparison.Ordinal),
            JSONValueType.Array => arrayValue.SequenceEqual(other.arrayValue),
            JSONValueType.Object => objectValue.Count == other.objectValue.Count && objectValue.All(pair => other.objectValue.TryGetValue(pair.Key, out var otherValue) && pair.Value.Equals(otherValue)),
            _ => false
        };
    }

    public override bool Equals(object? obj) => obj is JSONValue value && Equals(value);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(valueType);
        hash.Add(numberType);
        hash.Add(boolValue);
        hash.Add(numberValue);
        hash.Add(stringValue, StringComparer.Ordinal);
        foreach (var item in arrayValue)
            hash.Add(item);
        foreach (var item in objectValue)
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
                    value.arrayValue.Add(FromJsonNode(item, onChanged));
                break;

            case JsonObject jsonObject:
                value.ResetType(JSONValueType.Object);
                foreach (var item in jsonObject)
                    value.objectValue[item.Key] = FromJsonNode(item.Value, onChanged);
                break;

            case JsonValue jsonValue when jsonValue.TryGetValue<bool>(out var boolResult):
                value.ResetType(JSONValueType.Bool);
                value.boolValue = boolResult;
                break;

            case JsonValue jsonValue when jsonValue.TryGetValue<int>(out var intResult):
                value.ResetType(JSONValueType.Number, JSONNumberType.Int);
                value.numberValue = intResult;
                break;

            case JsonValue jsonValue when jsonValue.TryGetValue<uint>(out var uintResult):
                value.ResetType(JSONValueType.Number, JSONNumberType.UInt);
                value.numberValue = uintResult;
                break;

            case JsonValue jsonValue when jsonValue.TryGetValue<double>(out var doubleResult):
                value.ResetType(JSONValueType.Number, JSONNumberType.FloatDouble);
                value.numberValue = doubleResult;
                break;

            case JsonValue jsonValue when jsonValue.TryGetValue<string>(out var stringResult):
                value.ResetType(JSONValueType.String);
                value.stringValue = stringResult ?? string.Empty;
                break;

            default:
                value.ResetType(JSONValueType.Null);
                break;
        }

        return value;
    }

    internal JsonNode? ToJsonNode()
    {
        return valueType switch
        {
            JSONValueType.Null => null,
            JSONValueType.Bool => JsonValue.Create(boolValue),
            JSONValueType.Number => numberType switch
            {
                JSONNumberType.Int => JsonValue.Create(GetInt()),
                JSONNumberType.UInt => JsonValue.Create(GetUInt()),
                _ => JsonValue.Create(numberValue)
            },
            JSONValueType.String => JsonValue.Create(stringValue),
            JSONValueType.Array => ToJsonArray(),
            JSONValueType.Object => ToJsonObject(),
            _ => null
        };
    }

    internal void SetChangeCallbackRecursive(Action? callback)
    {
        onChanged = callback;
        foreach (var item in arrayValue)
            item.SetChangeCallbackRecursive(callback);
        foreach (var item in objectValue.Values)
            item.SetChangeCallbackRecursive(callback);
    }

    private void CopyFrom(JSONValue value)
    {
        ResetType(value.valueType, value.numberType);
        boolValue = value.boolValue;
        numberValue = value.numberValue;
        stringValue = value.stringValue;

        if (value.IsArray)
        {
            foreach (var item in value.arrayValue)
                arrayValue.Add(CreateChild(item));
        }

        if (value.IsObject)
        {
            foreach (var item in value.objectValue)
                objectValue[item.Key] = CreateChild(item.Value);
        }
    }

    private JSONValue CreateChild(JSONValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var child = new JSONValue(value);
        child.SetChangeCallbackRecursive(onChanged);
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

    private void NotifyChanged() => onChanged?.Invoke();

    private void ResetType(JSONValueType valueType, JSONNumberType numberType = JSONNumberType.NaN)
    {
        this.valueType = valueType;
        this.numberType = valueType == JSONValueType.Number
            ? numberType == JSONNumberType.NaN ? JSONNumberType.FloatDouble : numberType
            : JSONNumberType.NaN;
        boolValue = false;
        numberValue = 0.0;
        stringValue = string.Empty;
        arrayValue = [];
        objectValue = new Dictionary<string, JSONValue>(StringComparer.Ordinal);
    }

    private JsonArray ToJsonArray()
    {
        var result = new JsonArray();
        foreach (var item in arrayValue)
            result.Add(item.ToJsonNode());
        return result;
    }

    private JsonObject ToJsonObject()
    {
        var result = new JsonObject();
        foreach (var item in objectValue)
            result[item.Key] = item.Value.ToJsonNode();
        return result;
    }
}
