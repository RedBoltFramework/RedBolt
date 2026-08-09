namespace RedBolt.Resource;

using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text.Json.Nodes;

using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Input;
using RedBolt.Logging;
using RedBolt.Scene;

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

public class JSONValue : IEquatable<JSONValue>, ICloneable, IDisposable
{
    private static readonly string[] ValueTypeNames = ["Null", "Bool", "Number", "String", "Array", "Object"];
    private static readonly string[] NumberTypeNames = ["NaN", "Int", "Unsigned", "Real"];

    public static readonly JSONValue Empty = new();
    public static readonly IReadOnlyList<JSONValue> EmptyArray = Array.Empty<JSONValue>();
    public static readonly IReadOnlyDictionary<string, JSONValue> EmptyObject = new Dictionary<string, JSONValue>(StringComparer.Ordinal);

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

    public object Clone() => new JSONValue(this);

    public void Dispose() => ResetType(JSONValueType.Null);

    public void Set(bool value)
    {
        ResetType(JSONValueType.Bool);
        _boolValue = value;
    }

    public void Set(int value)
    {
        ResetType(JSONValueType.Number, JSONNumberType.Int);
        _numberValue = value;
    }

    public void Set(uint value)
    {
        ResetType(JSONValueType.Number, JSONNumberType.UInt);
        _numberValue = value;
    }

    public void Set(float value)
    {
        ResetType(JSONValueType.Number, JSONNumberType.FloatDouble);
        _numberValue = value;
    }

    public void Set(double value)
    {
        ResetType(JSONValueType.Number, JSONNumberType.FloatDouble);
        _numberValue = value;
    }

    public void Set(string? value)
    {
        ResetType(JSONValueType.String);
        _referenceValue = value ?? string.Empty;
    }

    public void Set(IEnumerable<JSONValue> value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (IsArray && ReferenceEquals(value, ArrayValue))
            return;
        SetType(JSONValueType.Array);
        Clear();
        foreach (var item in value)
            ArrayValue.Add(CreateChild(item));
    }

    public void Set(IReadOnlyDictionary<string, JSONValue> value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (IsObject && ReferenceEquals(value, ObjectValue))
            return;
        SetType(JSONValueType.Object);
        Clear();
        foreach (var item in value)
            ObjectValue[item.Key] = CreateChild(item.Value);
    }

    public void Set(JSONValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (ReferenceEquals(this, value))
            return;
        CopyFrom(value);
    }

    public bool Equals(JSONValue? other)
    {
        if (other is null || GetValueType() != other.GetValueType())
            return false;

        return GetValueType() switch
        {
            JSONValueType.Null => false,
            JSONValueType.Bool => _boolValue == other._boolValue,
            JSONValueType.Number => _numberValue.Equals(other._numberValue),
            JSONValueType.String => string.Equals(StringValue, other.StringValue, StringComparison.Ordinal),
            JSONValueType.Array => ManagedSequence.Equal(ArrayValue, other.ArrayValue),
            JSONValueType.Object => ObjectValue.Count == other.ObjectValue.Count && ObjectValue.All(pair => other.ObjectValue.TryGetValue(pair.Key, out var otherValue) && pair.Value.Equals(otherValue)),
            _ => false
        };
    }

    public override bool Equals(object? obj) => obj is JSONValue value && Equals(value);

    public static bool operator ==(JSONValue? left, JSONValue? right) => Equals(left, right);

    public static bool operator !=(JSONValue? left, JSONValue? right) => !Equals(left, right);

    public JSONValueType GetValueType() => (JSONValueType)(_type >> 16);

    public JSONNumberType GetNumberType() => (JSONNumberType)(_type & 0xffff);

    public string GetValueTypeName() => GetValueTypeName(GetValueType());

    public string GetNumberTypeName() => GetNumberTypeName(GetNumberType());

    public bool IsNull => GetValueType() == JSONValueType.Null;

    public bool IsBool => GetValueType() == JSONValueType.Bool;

    public bool IsNumber => GetValueType() == JSONValueType.Number;

    public bool IsString => GetValueType() == JSONValueType.String;

    public bool IsArray => GetValueType() == JSONValueType.Array;

    public bool IsObject => GetValueType() == JSONValueType.Object;

    public bool GetBool(bool defaultValue = false) => IsBool ? _boolValue : defaultValue;

    public int GetInt(int defaultValue = 0) => IsNumber ? (int)_numberValue : defaultValue;

    public uint GetUInt(uint defaultValue = 0) => IsNumber ? (uint)_numberValue : defaultValue;

    public float GetFloat(float defaultValue = 0.0f) => IsNumber ? (float)_numberValue : defaultValue;

    public double GetDouble(double defaultValue = 0.0) => IsNumber ? _numberValue : defaultValue;

    public string GetString(string? defaultValue = null) => IsString ? StringValue : defaultValue ?? string.Empty;

    public CString GetCString(CString? defaultValue = null) => IsString ? new CString(StringValue) : defaultValue ?? CString.Empty;

    public IReadOnlyList<JSONValue> GetArray() => IsArray ? ArrayValue : EmptyArray;

    public IReadOnlyDictionary<string, JSONValue> GetObject() => IsObject ? ObjectValue : EmptyObject;

    public JSONValue this[int index]
    {
        get
        {
            EnsureArray();
            return ArrayValue[index];
        }
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            EnsureArray();
            var child = CreateChild(value);
            ArrayValue[index].Dispose();
            ArrayValue[index] = child;
        }
    }

    public void Push(JSONValue value)
    {
        EnsureArray();
        ArrayValue.Add(CreateChild(value));
    }

    public void Pop()
    {
        if (!IsArray)
            return;

        ArrayValue[^1].Dispose();
        ArrayValue.RemoveAt(ArrayValue.Count - 1);
    }

    public void Insert(int position, JSONValue value)
    {
        if (!IsArray)
            return;
        ArrayValue.Insert(position, CreateChild(value));
    }

    public void Erase(int position, int length = 1)
    {
        if (!IsArray)
            return;

        for (var i = position; i < position + length; ++i)
            ArrayValue[i].Dispose();
        ArrayValue.RemoveRange(position, length);
    }

    public void Resize(int newSize)
    {
        EnsureArray();
        while (ArrayValue.Count > newSize)
        {
            ArrayValue[^1].Dispose();
            ArrayValue.RemoveAt(ArrayValue.Count - 1);
        }
        while (ArrayValue.Count < newSize)
            ArrayValue.Add(new JSONValue());
    }

    public int Size()
    {
        return GetValueType() switch
        {
            JSONValueType.Array => ArrayValue.Count,
            JSONValueType.Object => ObjectValue.Count,
            _ => 0
        };
    }

    public JSONValue this[string key]
    {
        get
        {
            EnsureObject();
            if (!ObjectValue.TryGetValue(key, out var value))
            {
                value = new JSONValue();
                ObjectValue[key] = value;
            }
            return value;
        }
        set
        {
            Set(key, value);
        }
    }

    public void Set(string key, JSONValue value)
    {
        EnsureObject();
        var child = CreateChild(value);
        if (ObjectValue.TryGetValue(key, out var oldValue))
            oldValue.Dispose();
        ObjectValue[key] = child;
    }

    public JSONValue Get(string key)
    {
        return IsObject && ObjectValue.TryGetValue(key, out var value) ? value : Empty;
    }

    public JSONValue Get(int index)
    {
        return IsArray && index >= 0 && index < ArrayValue.Count ? ArrayValue[index] : Empty;
    }

    public bool Erase(string key)
    {
        if (!IsObject || !ObjectValue.Remove(key, out var value))
            return false;

        value.Dispose();
        return true;
    }

    public bool Contains(string key)
    {
        return IsObject && ObjectValue.ContainsKey(key);
    }

    public void Clear()
    {
        if (IsArray)
        {
            foreach (var value in ArrayValue)
                value.Dispose();
            ArrayValue.Clear();
        }
        else if (IsObject)
        {
            foreach (var value in ObjectValue.Values)
                value.Dispose();
            ObjectValue.Clear();
        }
        else
            return;
    }

    public void ReplaceRecursively(JSONValue overrideValue)
    {
        ArgumentNullException.ThrowIfNull(overrideValue);
        if (!IsObject || !overrideValue.IsObject)
        {
            Set(overrideValue);
            return;
        }

        foreach (var (key, value) in overrideValue.ObjectValue)
        {
            if (ObjectValue.TryGetValue(key, out var current) && current.IsObject && value.IsObject)
                current.ReplaceRecursively(value);
            else
                Set(key, value);
        }
    }

    public void SetType(JSONValueType valueType, JSONNumberType numberType = JSONNumberType.NaN)
    {
        ResetType(valueType, numberType);
    }

    public void SetVariant(Variant value, Context? context = null)
    {
        if (GetValueType() != JSONValueType.Null)
            Log.GetLogger().LogWarning("JsonValue is not null");
        using var type = new JSONValue(value.GetTypeName());
        Set("type", type);
        using var variantValue = new JSONValue();
        variantValue.SetVariantValue(value, context);
        Set("value", variantValue);
    }

    public Variant GetVariant()
    {
        var type = Variant.GetTypeFromName(Get("type").GetString());
        return Get("value").GetVariantValue(type);
    }

    public void SetVariantValue(Variant value, Context? context = null)
    {
        if (!IsNull)
            Log.GetLogger().LogWarning("JsonValue is not null");
        if (value.Type == VariantType.Custom)
        {
            if (value.GetCustomVariantValue().TryGetValue<Serializable>(out var serializable))
            {
                using var serialized = new JSONValue();
                if (serializable.SaveJSON(serialized))
                {
                    using var typeName = new JSONValue(serializable.GetTypeName());
                    Set("type", typeName);
                    Set("value", serialized);
                }
                else
                    SetType(JSONValueType.Null);
            }
            else
            {
                SetType(JSONValueType.Null);
                Log.GetLogger().LogError("Serialization of objects other than Serializable is not supported.");
            }
            return;
        }

        if (value.Type == VariantType.ResourceRef && context is null)
        {
            Log.GetLogger().LogError("Context must not be null for ResourceRef");
            return;
        }
        if (value.Type == VariantType.ResourceRefList && context is null)
        {
            Log.GetLogger().LogError("Context must not be null for ResourceRefList");
            return;
        }

        using var variantValue = FromVariantValue(value, context);
        Set(variantValue);
    }

    public Variant GetVariantValue(VariantType type, Context? context = null)
    {
        if (type == VariantType.Custom)
        {
            if (context is null)
            {
                Log.GetLogger().LogError("Context must not be null for Serializable");
                return default;
            }

            if (IsNull)
                return default;
            if (!IsObject)
            {
                Log.GetLogger().LogError("Serializable custom variant expects a JSON object");
                return default;
            }

            var typeName = Get("type").GetString();
            if (string.IsNullOrEmpty(typeName))
            {
                Log.GetLogger().LogError("Malformed JSON input: 'type' is required when deserializing an object");
                return default;
            }

            var serializable = context.CreateObject(new StringHash(typeName)) as Serializable;
            if (serializable is null)
            {
                Log.GetLogger().LogError("Creation of type {TypeName} failed because it has no factory registered", typeName);
                return default;
            }

            if (!serializable.LoadJSON(Get("value")))
            {
                Log.GetLogger().LogError("Deserialization of {TypeName} failed", typeName);
                return default;
            }

            return Variant.MakeCustomValue(serializable);
        }

        var result = type switch
        {
            VariantType.Bool => new Variant(GetBool()),
            VariantType.Int => new Variant(GetInt()),
            VariantType.Int64 => new Variant((long)GetDouble()),
            VariantType.Float => new Variant(GetFloat()),
            VariantType.Double => new Variant(GetDouble()),
            VariantType.String => new Variant(GetString()),
            VariantType.VariantVector => new Variant(GetVariantVector()),
            VariantType.VariantMap => new Variant(GetVariantMap()),
            VariantType.StringVariantMap => new Variant(GetStringVariantMap()),
            VariantType.StringVector => new Variant(new StringVector(GetArray().Select(static item => item.GetString()))),
            VariantType.ResourceRef => new Variant(ResourceRef.Parse(GetString())),
            VariantType.ResourceRefList => new Variant(ResourceRefList.Parse(GetString())),
            _ => default
        };

        if (type is not (VariantType.Bool or VariantType.Int or VariantType.Float or VariantType.Double or VariantType.String or VariantType.VariantVector or VariantType.VariantMap or VariantType.StringVariantMap or VariantType.ResourceRef or VariantType.ResourceRefList or VariantType.StringVector or VariantType.Custom))
            result.FromString(type, GetString());

        return result;
    }

    public void SetVariantMap(VariantMap values, Context? context = null)
    {
        SetType(JSONValueType.Object);
        foreach (var pair in values)
        {
            using var item = FromVariantObject(pair.Value, null);
            Set(pair.Key.ToString(), item);
        }
    }

    public VariantMap GetVariantMap()
    {
        var result = new VariantMap();
        if (!IsObject)
        {
            Log.GetLogger().LogError("JSONValue is not a object");
            return result;
        }
        foreach (var item in GetObject())
        {
            uint.TryParse(item.Key, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hash);
            result[new StringHash(hash)] = item.Value.GetVariant();
        }
        return result;
    }

    public void SetVariantVector(VariantVector values, Context? context = null)
    {
        SetType(JSONValueType.Array);
        foreach (var value in values)
        {
            using var item = FromVariantObject(value, context);
            Push(item);
        }
    }

    public VariantVector GetVariantVector()
    {
        var result = new VariantVector();
        if (!IsArray)
        {
            Log.GetLogger().LogError("JSONValue is not a array");
            return result;
        }
        foreach (var item in ArrayValue)
            result.Add(item.GetVariant());
        return result;
    }

    public void SetStringVariantMap(StringVariantMap values, Context? context = null)
    {
        SetType(JSONValueType.Object);
        foreach (var pair in values)
        {
            using var item = FromVariantObject(pair.Value, null);
            Set(pair.Key, item);
        }
    }

    public StringVariantMap GetStringVariantMap()
    {
        var result = new StringVariantMap();
        if (!IsObject)
        {
            Log.GetLogger().LogError("JSONValue is not a object");
            return result;
        }
        foreach (var item in GetObject())
            result[item.Key] = item.Value.GetVariant();
        return result;
    }

    public static string GetValueTypeName(JSONValueType type) => ValueTypeNames[(int)type];

    public static string GetNumberTypeName(JSONNumberType type) => NumberTypeNames[(int)type];

    public static JSONValueType GetValueTypeFromName(string? typeName)
    {
        for (var i = 0; i < ValueTypeNames.Length; ++i)
        {
            if (string.Equals(typeName, ValueTypeNames[i], StringComparison.OrdinalIgnoreCase))
                return (JSONValueType)i;
        }
        return JSONValueType.Null;
    }

    public static JSONNumberType GetNumberTypeFromName(string? typeName)
    {
        for (var i = 0; i < NumberTypeNames.Length; ++i)
        {
            if (string.Equals(typeName, NumberTypeNames[i], StringComparison.OrdinalIgnoreCase))
                return (JSONNumberType)i;
        }
        return JSONNumberType.NaN;
    }

    private static JSONValue FromVariantObject(Variant value, Context? context)
    {
        var result = new JSONValue();
        result.SetVariant(value, context);
        return result;
    }

    private static JSONValue FromVariantValue(Variant value, Context? context)
    {
        return value.Type switch
        {
            VariantType.Bool => new JSONValue(value.GetBool()),
            VariantType.Int => new JSONValue(value.GetInt()),
            VariantType.Float => new JSONValue(value.GetFloat()),
            VariantType.Double => new JSONValue(value.GetDouble()),
            VariantType.String => new JSONValue(value.GetString()),
            VariantType.VariantVector => FromVariantVector(value.GetVariantVector(), context),
            VariantType.VariantMap => FromVariantMap(value.GetVariantMap(), context),
            VariantType.StringVariantMap => FromStringVariantMap(value.GetStringVariantMap(), context),
            VariantType.StringVector => FromStringVector(value.GetStringVector()),
            VariantType.ResourceRef => new JSONValue(value.GetResourceRef().ToString(context!)),
            VariantType.ResourceRefList => FromResourceRefList(value.GetResourceRefList(), context!),
            _ => new JSONValue(value.ToString())
        };
    }

    private static JSONValue FromVariantVector(VariantVector values, Context? context)
    {
        var result = new JSONValue();
        result.SetVariantVector(values, context);
        return result;
    }

    private static JSONValue FromStringVector(StringVector values)
    {
        var result = new JSONValue(JSONValueType.Array);
        result.Resize(values.Count);
        for (var i = 0; i < values.Count; ++i)
            result[i].Set(values[i]);
        return result;
    }

    private static JSONValue FromResourceRefList(ResourceRefList value, Context context)
    {
        var result = context.GetTypeName(value.Type);
        foreach (var name in value.Names)
            result += $";{name}";
        return new JSONValue(result);
    }

    private static JSONValue FromVariantMap(VariantMap values, Context? context)
    {
        var result = new JSONValue();
        result.SetVariantMap(values, context);
        return result;
    }

    private static JSONValue FromStringVariantMap(StringVariantMap values, Context? context)
    {
        var result = new JSONValue();
        result.SetStringVariantMap(values, context);
        return result;
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(GetValueType());
        hash.Add(_boolValue);
        hash.Add(_numberValue);
        if (IsString)
            hash.Add(StringValue, StringComparer.Ordinal);
        foreach (var item in GetArray())
            hash.Add(item);
        foreach (var item in GetObject())
        {
            hash.Add(item.Key, StringComparer.Ordinal);
            hash.Add(item.Value);
        }
        return hash.ToHashCode();
    }

    public override string ToString() => ToJsonNode()?.ToJsonString() ?? "null";

    internal static JSONValue FromJsonNode(JsonNode? node)
    {
        var value = new JSONValue();
        switch (node)
        {
            case null:
                value.ResetType(JSONValueType.Null);
                break;

            case JsonArray jsonArray:
                value.ResetType(JSONValueType.Array);
                foreach (var item in jsonArray)
                    value.ArrayValue.Add(FromJsonNode(item));
                break;

            case JsonObject jsonObject:
                value.ResetType(JSONValueType.Object);
                foreach (var item in jsonObject)
                    value.ObjectValue[item.Key] = FromJsonNode(item.Value);
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
                value._referenceValue = stringResult ?? string.Empty;
                break;

            default:
                value.ResetType(JSONValueType.Null);
                break;
        }

        return value;
    }

    internal JsonNode? ToJsonNode()
    {
        return GetValueType() switch
        {
            JSONValueType.Null => null,
            JSONValueType.Bool => JsonValue.Create(_boolValue),
            JSONValueType.Number => GetNumberType() switch
            {
                JSONNumberType.Int => JsonValue.Create(GetInt()),
                JSONNumberType.UInt => JsonValue.Create(GetUInt()),
                _ => JsonValue.Create(_numberValue)
            },
            JSONValueType.String => JsonValue.Create(StringValue),
            JSONValueType.Array => ToJsonArray(),
            JSONValueType.Object => ToJsonObject(),
            _ => null
        };
    }

    protected uint _type;
    protected bool _boolValue;
    protected double _numberValue;
    protected object? _referenceValue;

    protected string StringValue => (string)_referenceValue!;
    protected List<JSONValue> ArrayValue => (List<JSONValue>)_referenceValue!;
    protected Dictionary<string, JSONValue> ObjectValue => (Dictionary<string, JSONValue>)_referenceValue!;

    private void CopyFrom(JSONValue value)
    {
        ResetType(value.GetValueType(), value.GetNumberType());
        _boolValue = value._boolValue;
        _numberValue = value._numberValue;
        if (value.IsString)
            _referenceValue = value.StringValue;

        if (value.IsArray)
        {
            foreach (var item in ArrayValue)
                item.Dispose();
            ArrayValue.Clear();
            foreach (var item in value.ArrayValue)
                ArrayValue.Add(CreateChild(item));
        }

        if (value.IsObject)
        {
            foreach (var item in ObjectValue.Values)
                item.Dispose();
            ObjectValue.Clear();
            foreach (var item in value.ObjectValue)
                ObjectValue[item.Key] = CreateChild(item.Value);
        }
    }

    private JSONValue CreateChild(JSONValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new JSONValue(value);
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

    private void ResetType(JSONValueType valueType, JSONNumberType numberType = JSONNumberType.NaN)
    {
        var type = (uint)valueType << 16 | (uint)numberType;
        if (_type == type)
            return;

        if (IsArray)
        {
            foreach (var value in ArrayValue)
                value.Dispose();
        }
        else if (IsObject)
        {
            foreach (var value in ObjectValue.Values)
                value.Dispose();
        }

        _type = type;
        _boolValue = false;
        _numberValue = 0.0;
        _referenceValue = valueType switch
        {
            JSONValueType.String => string.Empty,
            JSONValueType.Array => new List<JSONValue>(),
            JSONValueType.Object => new Dictionary<string, JSONValue>(StringComparer.Ordinal),
            _ => null
        };
    }

    private JsonArray ToJsonArray()
    {
        var result = new JsonArray();
        foreach (var item in ArrayValue)
            result.Add(item.ToJsonNode());
        return result;
    }

    private JsonObject ToJsonObject()
    {
        var result = new JsonObject();
        foreach (var item in ObjectValue)
            result[item.Key] = item.Value.ToJsonNode();
        return result;
    }
}
