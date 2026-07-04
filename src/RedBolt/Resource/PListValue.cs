using System.Globalization;
using System.Text.RegularExpressions;

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

public sealed class PListValue : IEquatable<PListValue>
{
    private static readonly Regex NumberPattern = new(@"-?\d+(?:\.\d+)?", RegexOptions.Compiled);
    private static readonly IReadOnlyDictionary<string, PListValue> EmptyValueMap = new Dictionary<string, PListValue>(StringComparer.Ordinal);
    private static readonly IReadOnlyList<PListValue> EmptyValueVector = Array.Empty<PListValue>();

    private PListValueType type;
    private int intValue;
    private bool boolValue;
    private float floatValue;
    private string stringValue = string.Empty;
    private Dictionary<string, PListValue> valueMap = new(StringComparer.Ordinal);
    private List<PListValue> valueVector = [];

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

    public new PListValueType GetType() => type;

    public void SetInt(int value)
    {
        Reset();
        type = PListValueType.Int;
        intValue = value;
    }

    public void SetBool(bool value)
    {
        Reset();
        type = PListValueType.Bool;
        boolValue = value;
    }

    public void SetFloat(float value)
    {
        Reset();
        type = PListValueType.Float;
        floatValue = value;
    }

    public void SetString(string value)
    {
        Reset();
        type = PListValueType.String;
        stringValue = value ?? string.Empty;
    }

    public void SetValueMap(IReadOnlyDictionary<string, PListValue> valueMap)
    {
        ArgumentNullException.ThrowIfNull(valueMap);
        Reset();
        type = PListValueType.ValueMap;
        this.valueMap = new Dictionary<string, PListValue>(StringComparer.Ordinal);
        foreach (var item in valueMap)
            this.valueMap[item.Key] = new PListValue(item.Value);
    }

    public void SetValueVector(IReadOnlyList<PListValue> valueVector)
    {
        ArgumentNullException.ThrowIfNull(valueVector);
        Reset();
        type = PListValueType.ValueVector;
        this.valueVector = valueVector.Select(static value => new PListValue(value)).ToList();
    }

    public int GetInt() => type == PListValueType.Int ? intValue : 0;

    public bool GetBool() => type == PListValueType.Bool && boolValue;

    public float GetFloat() => type == PListValueType.Float ? floatValue : 0.0f;

    public string GetString() => type == PListValueType.String ? stringValue : string.Empty;

    public IntRect GetIntRect()
    {
        if (type != PListValueType.String)
            return IntRect.Zero;

        var numbers = ParseNumbers();
        return numbers.Length >= 4
            ? new IntRect(numbers[0], numbers[1], numbers[0] + numbers[2], numbers[1] + numbers[3])
            : IntRect.Zero;
    }

    public IntVector2 GetIntVector2()
    {
        if (type != PListValueType.String)
            return IntVector2.Zero;

        var numbers = ParseNumbers();
        return numbers.Length >= 2 ? new IntVector2(numbers[0], numbers[1]) : IntVector2.Zero;
    }

    public IntVector3 GetIntVector3()
    {
        if (type != PListValueType.String)
            return IntVector3.Zero;

        var numbers = ParseNumbers();
        return numbers.Length >= 3 ? new IntVector3(numbers[0], numbers[1], numbers[2]) : IntVector3.Zero;
    }

    public IReadOnlyDictionary<string, PListValue> GetValueMap() => type == PListValueType.ValueMap ? valueMap : EmptyValueMap;

    public IReadOnlyList<PListValue> GetValueVector() => type == PListValueType.ValueVector ? valueVector : EmptyValueVector;

    public Dictionary<string, PListValue> ConvertToValueMap()
    {
        if (type != PListValueType.ValueMap)
        {
            Reset();
            type = PListValueType.ValueMap;
            valueMap = new Dictionary<string, PListValue>(StringComparer.Ordinal);
        }

        return valueMap;
    }

    public List<PListValue> ConvertToValueVector()
    {
        if (type != PListValueType.ValueVector)
        {
            Reset();
            type = PListValueType.ValueVector;
            valueVector = [];
        }

        return valueVector;
    }

    public bool Equals(PListValue? other)
    {
        if (other is null || type != other.type)
            return false;

        return type switch
        {
            PListValueType.None => true,
            PListValueType.Int => intValue == other.intValue,
            PListValueType.Bool => boolValue == other.boolValue,
            PListValueType.Float => MathDefs.ApproximatelyEquivalent(floatValue, other.floatValue),
            PListValueType.String => string.Equals(stringValue, other.stringValue, StringComparison.Ordinal),
            PListValueType.ValueMap => valueMap.Count == other.valueMap.Count && valueMap.All(item => other.valueMap.TryGetValue(item.Key, out var otherValue) && item.Value.Equals(otherValue)),
            PListValueType.ValueVector => valueVector.SequenceEqual(other.valueVector),
            _ => false
        };
    }

    public override bool Equals(object? obj) => obj is PListValue other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(type);
        hash.Add(intValue);
        hash.Add(boolValue);
        hash.Add(floatValue);
        hash.Add(stringValue, StringComparer.Ordinal);
        foreach (var item in valueMap)
        {
            hash.Add(item.Key, StringComparer.Ordinal);
            hash.Add(item.Value);
        }
        foreach (var item in valueVector)
            hash.Add(item);
        return hash.ToHashCode();
    }

    private void CopyFrom(PListValue value)
    {
        switch (value.type)
        {
            case PListValueType.None:
                Reset();
                break;
            case PListValueType.Int:
                SetInt(value.intValue);
                break;
            case PListValueType.Bool:
                SetBool(value.boolValue);
                break;
            case PListValueType.Float:
                SetFloat(value.floatValue);
                break;
            case PListValueType.String:
                SetString(value.stringValue);
                break;
            case PListValueType.ValueMap:
                SetValueMap(value.valueMap);
                break;
            case PListValueType.ValueVector:
                SetValueVector(value.valueVector);
                break;
        }
    }

    private int[] ParseNumbers()
    {
        return NumberPattern.Matches(stringValue)
            .Select(static match => int.TryParse(match.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0)
            .ToArray();
    }

    private void Reset()
    {
        type = PListValueType.None;
        intValue = 0;
        boolValue = false;
        floatValue = 0.0f;
        stringValue = string.Empty;
        valueMap = new Dictionary<string, PListValue>(StringComparer.Ordinal);
        valueVector = [];
    }
}
