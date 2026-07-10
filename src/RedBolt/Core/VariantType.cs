using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;

namespace RedBolt;

public enum VariantType
{
    None = 0,
    Int,
    Bool,
    Float,
    Vector2,
    Vector3,
    Vector4,
    DoubleVector2,
    DoubleVector3,
    Quaternion,
    Color,
    String,
    Buffer,
    VoidPtr,
    ResourceRef,
    ResourceRefList,
    VariantVector,
    VariantMap,
    IntRect,
    Vector2i,
    Ptr,
    Matrix3,
    Matrix3x4,
    Matrix4,
    Double,
    StringVector,
    Rect,
    Vector3i,
    Int64,
    Custom,
    VariantCurve,
    StringVariantMap,
    CString
}

public readonly struct Variant : IEquatable<Variant>
{
    private static readonly string[] TypeNames =
    [
        "None",
        "Int",
        "Bool",
        "Float",
        "Vector2",
        "Vector3",
        "Vector4f",
        "DoubleVector2",
        "DoubleVector3",
        "Quaternion",
        "Color",
        "String",
        "Buffer",
        "VoidPtr",
        "ResourceRef",
        "ResourceRefList",
        "VariantVector",
        "VariantMap",
        "IntRect",
        "Vector2i",
        "Ptr",
        "Matrix3",
        "Matrix3x4",
        "Matrix4",
        "Double",
        "StringVector",
        "Rect",
        "Vector3i",
        "Int64",
        "Custom",
        "VariantCurve",
        "StringVariantMap",
        "CString"
    ];

    private readonly VariantType _type;
    private readonly bool _boolValue;
    private readonly int _intValue;
    private readonly long _int64Value;
    private readonly float _floatValue;
    private readonly double _doubleValue;
    private readonly Vector2f _vector2Value;
    private readonly Vector2d _doubleVector2Value;
    private readonly Vector3f _vector3Value;
    private readonly Vector3d _doubleVector3Value;
    private readonly Vector4f _vector4Value;
    private readonly Quaternion _quaternionValue;
    private readonly Color _colorValue;
    private readonly Rect _rectValue;
    private readonly IntRect _intRectValue;
    private readonly Vector2i _intVector2Value;
    private readonly Vector3i _intVector3Value;
    private readonly string _stringValue = string.Empty;
    private readonly CString _cstringValue = CString.Empty;
    private readonly byte[] _bufferValue = [];
    private readonly ResourceRef _resourceRefValue;
    private readonly ResourceRefList _resourceRefListValue = ResourceRefList.Empty;
    private readonly Matrix3 _matrix3Value;
    private readonly Matrix3x4 _matrix3x4Value;
    private readonly Matrix4 _matrix4Value;
    private readonly VariantVector _variantVectorValue = new();
    private readonly StringVector _stringVectorValue = new();
    private readonly VariantMap _variantMapValue = new();
    private readonly StringVariantMap _stringVariantMapValue = new();

    public Variant(VariantType type)
    {
        this = type switch
        {
            VariantType.Int => new Variant(0),
            VariantType.Int64 => new Variant(0L),
            VariantType.Bool => new Variant(false),
            VariantType.Float => new Variant(0.0f),
            VariantType.Vector2 => new Variant(Vector2f.ZERO),
            VariantType.Vector3 => new Variant(Vector3f.ZERO),
            VariantType.DoubleVector2 => new Variant(Vector2d.ZERO),
            VariantType.DoubleVector3 => new Variant(Vector3d.ZERO),
            VariantType.Vector4 => new Variant(Vector4f.ZERO),
            VariantType.Quaternion => new Variant(Quaternion.IDENTITY),
            VariantType.Color => new Variant(Color.Black),
            VariantType.String => new Variant(string.Empty),
            VariantType.CString => new Variant(new CString()),
            VariantType.ResourceRef => new Variant(ResourceRef.Empty),
            VariantType.ResourceRefList => new Variant(ResourceRefList.Empty),
            VariantType.VariantVector => new Variant(new VariantVector()),
            VariantType.VariantMap => new Variant(new VariantMap()),
            VariantType.IntRect => new Variant(IntRect.Zero),
            VariantType.Vector2i => new Variant(Vector2i.Zero),
            VariantType.Matrix3 => new Variant(Matrix3.ZERO),
            VariantType.Matrix3x4 => new Variant(Matrix3x4.ZERO),
            VariantType.Matrix4 => new Variant(Matrix4.ZERO),
            VariantType.Double => new Variant(0.0),
            VariantType.StringVector => new Variant(new StringVector()),
            VariantType.Rect => new Variant(Rect.Zero),
            VariantType.Vector3i => new Variant(Vector3i.Zero),
            VariantType.StringVariantMap => new Variant(new StringVariantMap()),
            VariantType.Buffer =>
                new Variant(Array.Empty<byte>()),
            _ => default
        };
    }

    public Variant(bool value)
    {
        _type = VariantType.Bool;
        _boolValue = value;
    }

    public Variant(int value)
    {
        _type = VariantType.Int;
        _intValue = value;
    }

    public Variant(uint value)
        : this(unchecked((int)value))
    {
    }

    public Variant(long value)
    {
        _type = VariantType.Int64;
        _int64Value = value;
    }

    public Variant(ulong value)
        : this(unchecked((long)value))
    {
    }

    public Variant(float value)
    {
        _type = VariantType.Float;
        _floatValue = value;
    }

    public Variant(double value)
    {
        _type = VariantType.Double;
        _doubleValue = value;
    }

    public Variant(Vector2f value)
    {
        _type = VariantType.Vector2;
        _vector2Value = value;
    }

    public Variant(Vector2d value)
    {
        _type = VariantType.DoubleVector2;
        _doubleVector2Value = value;
    }

    public Variant(Vector3f value)
    {
        _type = VariantType.Vector3;
        _vector3Value = value;
    }

    public Variant(Vector3d value)
    {
        _type = VariantType.DoubleVector3;
        _doubleVector3Value = value;
    }

    public Variant(Vector4f value)
    {
        _type = VariantType.Vector4;
        _vector4Value = value;
    }

    public Variant(Quaternion value)
    {
        _type = VariantType.Quaternion;
        _quaternionValue = value;
    }

    public Variant(Color value)
    {
        _type = VariantType.Color;
        _colorValue = value;
    }

    public Variant(Rect value)
    {
        _type = VariantType.Rect;
        _rectValue = value;
    }

    public Variant(IntRect value)
    {
        _type = VariantType.IntRect;
        _intRectValue = value;
    }

    public Variant(Vector2i value)
    {
        _type = VariantType.Vector2i;
        _intVector2Value = value;
    }

    public Variant(Vector3i value)
    {
        _type = VariantType.Vector3i;
        _intVector3Value = value;
    }

    public Variant(StringHash value)
        : this(unchecked((int)value.Value))
    {
    }

    public Variant(byte[] value)
    {
        _type = VariantType.Buffer;
        _bufferValue = value ?? [];
    }

    public Variant(ResourceRef value)
    {
        _type = VariantType.ResourceRef;
        _resourceRefValue = value;
    }

    public Variant(ResourceRefList value)
    {
        _type = VariantType.ResourceRefList;
        _resourceRefListValue = value ?? ResourceRefList.Empty;
    }

    public Variant(Matrix3 value)
    {
        _type = VariantType.Matrix3;
        _matrix3Value = value;
    }

    public Variant(Matrix3x4 value)
    {
        _type = VariantType.Matrix3x4;
        _matrix3x4Value = value;
    }

    public Variant(Matrix4 value)
    {
        _type = VariantType.Matrix4;
        _matrix4Value = value;
    }

    public Variant(VariantVector value)
    {
        _type = VariantType.VariantVector;
        _variantVectorValue = value ?? new VariantVector();
    }

    public Variant(StringVector value)
    {
        _type = VariantType.StringVector;
        _stringVectorValue = value ?? new StringVector();
    }

    public Variant(VariantMap value)
    {
        _type = VariantType.VariantMap;
        _variantMapValue = value ?? new VariantMap();
    }

    public Variant(StringVariantMap value)
    {
        _type = VariantType.StringVariantMap;
        _stringVariantMapValue = value ?? new StringVariantMap();
    }

    public Variant(string value)
    {
        if (value is null)
            return;

        _type = VariantType.String;
        _stringValue = value;
    }

    public Variant(CString value)
    {
        _type = VariantType.CString;
        _cstringValue = value ?? new CString();
    }

    public VariantType Type => _type;

    public bool IsEmpty => _type == VariantType.None;

    public bool GetBool() => _type == VariantType.Bool ? _boolValue : throw CreateInvalidCastException(VariantType.Bool);

    public int GetInt() => _type == VariantType.Int ? _intValue : throw CreateInvalidCastException(VariantType.Int);

    public long GetInt64() => _type == VariantType.Int64 ? _int64Value : throw CreateInvalidCastException(VariantType.Int64);

    public float GetFloat() => _type == VariantType.Float ? _floatValue : throw CreateInvalidCastException(VariantType.Float);

    public double GetDouble() => _type == VariantType.Double ? _doubleValue : throw CreateInvalidCastException(VariantType.Double);

    public Vector2f GetVector2() => _type == VariantType.Vector2 ? _vector2Value : throw CreateInvalidCastException(VariantType.Vector2);

    public Vector2d GetDoubleVector2() => _type == VariantType.DoubleVector2 ? _doubleVector2Value : throw CreateInvalidCastException(VariantType.DoubleVector2);

    public Vector3f GetVector3() => _type == VariantType.Vector3 ? _vector3Value : throw CreateInvalidCastException(VariantType.Vector3);

    public Vector3d GetDoubleVector3() => _type == VariantType.DoubleVector3 ? _doubleVector3Value : throw CreateInvalidCastException(VariantType.DoubleVector3);

    public Vector4f GetVector4() => _type == VariantType.Vector4 ? _vector4Value : throw CreateInvalidCastException(VariantType.Vector4);

    public Quaternion GetQuaternion() => _type == VariantType.Quaternion ? _quaternionValue : throw CreateInvalidCastException(VariantType.Quaternion);

    public Color GetColor() => _type == VariantType.Color ? _colorValue : throw CreateInvalidCastException(VariantType.Color);

    public Rect GetRect() => _type == VariantType.Rect ? _rectValue : throw CreateInvalidCastException(VariantType.Rect);

    public IntRect GetIntRect() => _type == VariantType.IntRect ? _intRectValue : throw CreateInvalidCastException(VariantType.IntRect);

    public Vector2i GetIntVector2() => _type == VariantType.Vector2i ? _intVector2Value : throw CreateInvalidCastException(VariantType.Vector2i);

    public Vector3i GetIntVector3() => _type == VariantType.Vector3i ? _intVector3Value : throw CreateInvalidCastException(VariantType.Vector3i);

    public uint GetUInt() => _type == VariantType.Int ? unchecked((uint)_intValue) : throw CreateInvalidCastException(VariantType.Int);

    public ulong GetUInt64() => _type == VariantType.Int64 ? unchecked((ulong)_int64Value) : throw CreateInvalidCastException(VariantType.Int64);

    public StringHash GetStringHash() => _type == VariantType.Int ? new StringHash(GetUInt()) : StringHash.Empty;

    public string GetString() => _type == VariantType.String ? _stringValue : throw CreateInvalidCastException(VariantType.String);

    public CString GetCString() => _type == VariantType.CString ? _cstringValue : throw CreateInvalidCastException(VariantType.CString);

    public byte[] GetBuffer() => _type == VariantType.Buffer ? _bufferValue ?? [] : throw CreateInvalidCastException(VariantType.Buffer);

    public ResourceRef GetResourceRef() => _type == VariantType.ResourceRef ? _resourceRefValue : throw CreateInvalidCastException(VariantType.ResourceRef);

    public ResourceRefList GetResourceRefList() => _type == VariantType.ResourceRefList ? _resourceRefListValue ?? ResourceRefList.Empty : throw CreateInvalidCastException(VariantType.ResourceRefList);

    public Matrix3 GetMatrix3() => _type == VariantType.Matrix3 ? _matrix3Value : throw CreateInvalidCastException(VariantType.Matrix3);

    public Matrix3x4 GetMatrix3x4() => _type == VariantType.Matrix3x4 ? _matrix3x4Value : throw CreateInvalidCastException(VariantType.Matrix3x4);

    public Matrix4 GetMatrix4() => _type == VariantType.Matrix4 ? _matrix4Value : throw CreateInvalidCastException(VariantType.Matrix4);

    public VariantVector GetVariantVector() => _type == VariantType.VariantVector ? _variantVectorValue ?? new VariantVector() : throw CreateInvalidCastException(VariantType.VariantVector);

    public StringVector GetStringVector() => _type == VariantType.StringVector ? _stringVectorValue ?? new StringVector() : throw CreateInvalidCastException(VariantType.StringVector);

    public VariantMap GetVariantMap() => _type == VariantType.VariantMap ? _variantMapValue ?? new VariantMap() : throw CreateInvalidCastException(VariantType.VariantMap);

    public StringVariantMap GetStringVariantMap() => _type == VariantType.StringVariantMap ? _stringVariantMapValue ?? new StringVariantMap() : throw CreateInvalidCastException(VariantType.StringVariantMap);

    public string GetTypeName() => GetTypeName(_type);

    public static IReadOnlyList<string> GetTypeNameList() => TypeNames;

    public static string GetTypeName(VariantType type) => TypeNames[(int)type];

    public static VariantType GetTypeFromName(string typeName)
    {
        for (var i = 0; i < TypeNames.Length; ++i)
        {
            if (string.Equals(TypeNames[i], typeName, StringComparison.Ordinal))
                return (VariantType)i;
        }

        return VariantType.None;
    }

    public bool IsZero()
    {
        return _type switch
        {
            VariantType.None => true,
            VariantType.Bool => !_boolValue,
            VariantType.Int => _intValue == 0,
            VariantType.Int64 => _int64Value == 0,
            VariantType.Float => _floatValue == 0.0f,
            VariantType.Double => _doubleValue == 0.0,
            VariantType.Vector2 => _vector2Value == Vector2f.ZERO,
            VariantType.DoubleVector2 => _doubleVector2Value == Vector2d.ZERO,
            VariantType.Vector3 => _vector3Value == Vector3f.ZERO,
            VariantType.DoubleVector3 => _doubleVector3Value == Vector3d.ZERO,
            VariantType.Vector4 => _vector4Value == Vector4f.ZERO,
            VariantType.Quaternion => _quaternionValue == Quaternion.IDENTITY,
            VariantType.Color => _colorValue == Color.White,
            VariantType.String => string.IsNullOrEmpty(_stringValue),
            VariantType.CString => _cstringValue.ByteLength == 0,
            VariantType.Buffer => _bufferValue.Length == 0,
            VariantType.ResourceRef => string.IsNullOrEmpty(_resourceRefValue.Name),
            VariantType.ResourceRefList => _resourceRefListValue.Names.All(static name => string.IsNullOrEmpty(name)),
            VariantType.VariantVector => _variantVectorValue.Count == 0,
            VariantType.StringVector => _stringVectorValue.Count == 0,
            VariantType.VariantMap => _variantMapValue.Count == 0,
            VariantType.IntRect => _intRectValue == IntRect.Zero,
            VariantType.Vector2i => _intVector2Value == Vector2i.Zero,
            VariantType.Vector3i => _intVector3Value == Vector3i.Zero,
            VariantType.Matrix3 => _matrix3Value == Matrix3.IDENTITY,
            VariantType.Matrix3x4 => _matrix3x4Value == Matrix3x4.IDENTITY,
            VariantType.Matrix4 => _matrix4Value == Matrix4.IDENTITY,
            VariantType.Rect => _rectValue == Rect.Zero,
            VariantType.StringVariantMap => _stringVariantMapValue.Count == 0,
            _ => true
        };
    }

    public object GetObject()
    {
        return _type switch
        {
            VariantType.None => null!,
            VariantType.Bool => _boolValue,
            VariantType.Int => _intValue,
            VariantType.Int64 => _int64Value,
            VariantType.Float => _floatValue,
            VariantType.Double => _doubleValue,
            VariantType.Vector2 => _vector2Value,
            VariantType.DoubleVector2 => _doubleVector2Value,
            VariantType.Vector3 => _vector3Value,
            VariantType.DoubleVector3 => _doubleVector3Value,
            VariantType.Vector4 => _vector4Value,
            VariantType.Quaternion => _quaternionValue,
            VariantType.Color => _colorValue,
            VariantType.Rect => _rectValue,
            VariantType.IntRect => _intRectValue,
            VariantType.Vector2i => _intVector2Value,
            VariantType.Vector3i => _intVector3Value,
            VariantType.String => _stringValue,
            VariantType.CString => _cstringValue,
            VariantType.Buffer => _bufferValue,
            VariantType.ResourceRef => _resourceRefValue,
            VariantType.ResourceRefList => _resourceRefListValue,
            VariantType.Matrix3 => _matrix3Value,
            VariantType.Matrix3x4 => _matrix3x4Value,
            VariantType.Matrix4 => _matrix4Value,
            VariantType.VariantVector => _variantVectorValue,
            VariantType.StringVector => _stringVectorValue,
            VariantType.VariantMap => _variantMapValue,
            VariantType.StringVariantMap => _stringVariantMapValue,
            _ => null!
        };
    }

    public static Variant FromObject(object? value)
    {
        return value switch
        {
            null => default,
            Variant variant => variant,
            bool typedValue => typedValue,
            int typedValue => typedValue,
            uint typedValue => typedValue,
            long typedValue => typedValue,
            ulong typedValue => typedValue,
            float typedValue => typedValue,
            double typedValue => typedValue,
            StringHash typedValue => typedValue,
            Vector2f typedValue => typedValue,
            Vector2d typedValue => typedValue,
            Vector3f typedValue => typedValue,
            Vector3d typedValue => typedValue,
            Vector4f typedValue => typedValue,
            Quaternion typedValue => typedValue,
            Color typedValue => typedValue,
            Rect typedValue => typedValue,
            IntRect typedValue => typedValue,
            Vector2i typedValue => typedValue,
            Vector3i typedValue => typedValue,
            byte[] typedValue => typedValue,
            ResourceRef typedValue => typedValue,
            ResourceRefList typedValue => typedValue,
            Matrix3 typedValue => typedValue,
            Matrix3x4 typedValue => typedValue,
            Matrix4 typedValue => typedValue,
            VariantVector typedValue => typedValue,
            StringVector typedValue => typedValue,
            VariantMap typedValue => typedValue,
            StringVariantMap typedValue => typedValue,
            string typedValue => typedValue,
            CString typedValue => typedValue,
            _ => throw new ArgumentException($"Unsupported Variant value type '{value.GetType().Name}'.", nameof(value))
        };
    }

    public bool Equals(Variant other)
    {
        if (_type != other._type)
            return false;

        return _type switch
        {
            VariantType.None => true,
            VariantType.Bool => _boolValue == other._boolValue,
            VariantType.Int => _intValue == other._intValue,
            VariantType.Int64 => _int64Value == other._int64Value,
            VariantType.Float => _floatValue.Equals(other._floatValue),
            VariantType.Double => _doubleValue.Equals(other._doubleValue),
            VariantType.Vector2 => _vector2Value.Equals(other._vector2Value),
            VariantType.DoubleVector2 => _doubleVector2Value.Equals(other._doubleVector2Value),
            VariantType.Vector3 => _vector3Value.Equals(other._vector3Value),
            VariantType.DoubleVector3 => _doubleVector3Value.Equals(other._doubleVector3Value),
            VariantType.Vector4 => _vector4Value.Equals(other._vector4Value),
            VariantType.Quaternion => _quaternionValue.Equals(other._quaternionValue),
            VariantType.Color => _colorValue.Equals(other._colorValue),
            VariantType.Rect => _rectValue.Equals(other._rectValue),
            VariantType.IntRect => _intRectValue.Equals(other._intRectValue),
            VariantType.Vector2i => _intVector2Value.Equals(other._intVector2Value),
            VariantType.Vector3i => _intVector3Value.Equals(other._intVector3Value),
            VariantType.String => string.Equals(_stringValue, other._stringValue, StringComparison.Ordinal),
            VariantType.CString => _cstringValue.Equals(other._cstringValue),
            VariantType.Buffer => _bufferValue.AsSpan().SequenceEqual(other._bufferValue),
            VariantType.ResourceRef => _resourceRefValue.Equals(other._resourceRefValue),
            VariantType.ResourceRefList => _resourceRefListValue.Equals(other._resourceRefListValue),
            VariantType.Matrix3 => _matrix3Value.Equals(other._matrix3Value),
            VariantType.Matrix3x4 => _matrix3x4Value.Equals(other._matrix3x4Value),
            VariantType.Matrix4 => _matrix4Value.Equals(other._matrix4Value),
            VariantType.VariantVector => SequenceEqual(_variantVectorValue, other._variantVectorValue),
            VariantType.StringVector => SequenceEqual(_stringVectorValue, other._stringVectorValue),
            VariantType.VariantMap => DictionaryEqual(_variantMapValue, other._variantMapValue),
            VariantType.StringVariantMap => DictionaryEqual(_stringVariantMapValue, other._stringVariantMapValue),
            _ => false
        };
    }

    public override bool Equals(object? obj) => obj is Variant other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(_type);
        switch (_type)
        {
        case VariantType.Bool:
            hash.Add(_boolValue);
            break;
        case VariantType.Int:
            hash.Add(_intValue);
            break;
        case VariantType.Int64:
            hash.Add(_int64Value);
            break;
        case VariantType.Float:
            hash.Add(_floatValue);
            break;
        case VariantType.Double:
            hash.Add(_doubleValue);
            break;
        case VariantType.Vector2:
            hash.Add(_vector2Value);
            break;
        case VariantType.DoubleVector2:
            hash.Add(_doubleVector2Value);
            break;
        case VariantType.Vector3:
            hash.Add(_vector3Value);
            break;
        case VariantType.DoubleVector3:
            hash.Add(_doubleVector3Value);
            break;
        case VariantType.Vector4:
            hash.Add(_vector4Value);
            break;
        case VariantType.Quaternion:
            hash.Add(_quaternionValue);
            break;
        case VariantType.Color:
            hash.Add(_colorValue);
            break;
        case VariantType.Rect:
            hash.Add(_rectValue);
            break;
        case VariantType.IntRect:
            hash.Add(_intRectValue);
            break;
        case VariantType.Vector2i:
            hash.Add(_intVector2Value);
            break;
        case VariantType.Vector3i:
            hash.Add(_intVector3Value);
            break;
        case VariantType.String:
            hash.Add(_stringValue, StringComparer.Ordinal);
            break;
        case VariantType.CString:
            hash.Add(_cstringValue);
            break;
        case VariantType.Buffer:
            foreach (var item in _bufferValue)
                hash.Add(item);
            break;
        case VariantType.ResourceRef:
            hash.Add(_resourceRefValue);
            break;
        case VariantType.ResourceRefList:
            hash.Add(_resourceRefListValue);
            break;
        case VariantType.Matrix3:
            hash.Add(_matrix3Value);
            break;
        case VariantType.Matrix3x4:
            hash.Add(_matrix3x4Value);
            break;
        case VariantType.Matrix4:
            hash.Add(_matrix4Value);
            break;
        case VariantType.VariantVector:
            AddSequenceHash(ref hash, _variantVectorValue);
            break;
        case VariantType.StringVector:
            AddSequenceHash(ref hash, _stringVectorValue);
            break;
        case VariantType.VariantMap:
            AddDictionaryHash(ref hash, _variantMapValue);
            break;
        case VariantType.StringVariantMap:
            AddDictionaryHash(ref hash, _stringVariantMapValue);
            break;
        }

        return hash.ToHashCode();
    }

    public override string ToString()
    {
        return _type switch
        {
            VariantType.None => string.Empty,
            VariantType.Int => _intValue.ToString(CultureInfo.InvariantCulture),
            VariantType.Int64 => _int64Value.ToString(CultureInfo.InvariantCulture),
            VariantType.Bool => _boolValue ? "true" : "false",
            VariantType.Float => _floatValue.ToString(CultureInfo.InvariantCulture),
            VariantType.Vector2 => _vector2Value.ToString(),
            VariantType.Vector3 => _vector3Value.ToString(),
            VariantType.Vector4 => _vector4Value.ToString(),
            VariantType.DoubleVector2 => _doubleVector2Value.ToString(),
            VariantType.DoubleVector3 => _doubleVector3Value.ToString(),
            VariantType.Quaternion => _quaternionValue.ToString(),
            VariantType.Color => _colorValue.ToString(),
            VariantType.String => _stringValue,
            VariantType.CString => _cstringValue.ToString(),
            VariantType.Buffer => BufferToString(_bufferValue),
            VariantType.ResourceRef => _resourceRefValue.ToString(),
            VariantType.ResourceRefList => _resourceRefListValue.ToString(),
            VariantType.IntRect => _intRectValue.ToString(),
            VariantType.Vector2i => _intVector2Value.ToString(),
            VariantType.Vector3i => _intVector3Value.ToString(),
            VariantType.Matrix3 => _matrix3Value.ToString(),
            VariantType.Matrix3x4 => _matrix3x4Value.ToString(),
            VariantType.Matrix4 => _matrix4Value.ToString(),
            VariantType.Double => _doubleValue.ToString(CultureInfo.InvariantCulture),
            VariantType.Rect => _rectValue.ToString(),
            _ => string.Empty
        };
    }

    public static implicit operator Variant(bool value) => new(value);
    public static implicit operator Variant(int value) => new(value);
    public static implicit operator Variant(uint value) => new(value);
    public static implicit operator Variant(long value) => new(value);
    public static implicit operator Variant(ulong value) => new(value);
    public static implicit operator Variant(float value) => new(value);
    public static implicit operator Variant(double value) => new(value);
    public static implicit operator Variant(StringHash value) => new(value);
    public static implicit operator Variant(Vector2f value) => new(value);
    public static implicit operator Variant(Vector2d value) => new(value);
    public static implicit operator Variant(Vector3f value) => new(value);
    public static implicit operator Variant(Vector3d value) => new(value);
    public static implicit operator Variant(Vector4f value) => new(value);
    public static implicit operator Variant(Quaternion value) => new(value);
    public static implicit operator Variant(Color value) => new(value);
    public static implicit operator Variant(Rect value) => new(value);
    public static implicit operator Variant(IntRect value) => new(value);
    public static implicit operator Variant(Vector2i value) => new(value);
    public static implicit operator Variant(Vector3i value) => new(value);
    public static implicit operator Variant(byte[] value) => new(value);
    public static implicit operator Variant(ResourceRef value) => new(value);
    public static implicit operator Variant(ResourceRefList value) => new(value);
    public static implicit operator Variant(Matrix3 value) => new(value);
    public static implicit operator Variant(Matrix3x4 value) => new(value);
    public static implicit operator Variant(Matrix4 value) => new(value);
    public static implicit operator Variant(VariantVector value) => new(value);
    public static implicit operator Variant(StringVector value) => new(value);
    public static implicit operator Variant(VariantMap value) => new(value);
    public static implicit operator Variant(StringVariantMap value) => new(value);
    public static implicit operator Variant(string value) => new(value);
    public static implicit operator Variant(CString value) => new(value);
    public static bool operator ==(Variant lhs, Variant rhs) => lhs.Equals(rhs);
    public static bool operator !=(Variant lhs, Variant rhs) => !lhs.Equals(rhs);

    private InvalidCastException CreateInvalidCastException(VariantType expectedType)
    {
        return new InvalidCastException($"Variant contains {_type} instead of {expectedType}.");
    }

    private static string BufferToString(byte[] buffer)
    {
        if (buffer.Length == 0)
            return string.Empty;

        return string.Join(' ', buffer.Select(static value => value.ToString(CultureInfo.InvariantCulture)));
    }

    private static void AddDictionaryHash<TKey>(ref HashCode hash, IDictionary<TKey, Variant> dictionary)
        where TKey : notnull
    {
        foreach (var pair in dictionary.OrderBy(static pair => pair.Key))
        {
            hash.Add(pair.Key);
            hash.Add(pair.Value);
        }
    }

    private static void AddSequenceHash<T>(ref HashCode hash, IEnumerable<T> sequence)
    {
        if (sequence is null)
            return;

        foreach (var item in sequence)
            hash.Add(item);
    }

    private static bool DictionaryEqual<TKey>(IDictionary<TKey, Variant> lhs, IDictionary<TKey, Variant> rhs)
        where TKey : notnull
    {
        if (lhs.Count != rhs.Count)
            return false;

        foreach (var pair in lhs)
        {
            if (!rhs.TryGetValue(pair.Key, out var value) || !value.Equals(pair.Value))
                return false;
        }

        return true;
    }

    private static bool SequenceEqual<T>(IEnumerable<T> lhs, IEnumerable<T> rhs)
    {
        return lhs.SequenceEqual(rhs);
    }
}

public sealed class VariantMap : Dictionary<StringHash, Variant>
{
    public VariantMap()
    {
    }

    public VariantMap(IDictionary<StringHash, Variant> dictionary)
        : base(dictionary)
    {
    }
}

public sealed class VariantVector : List<Variant>
{
    public VariantVector()
    {
    }

    public VariantVector(IEnumerable<Variant> collection)
        : base(collection)
    {
    }
}

public sealed class StringVector : List<string>
{
    public StringVector()
    {
    }

    public StringVector(IEnumerable<string> collection)
        : base(collection)
    {
    }
}

public sealed class StringVariantMap : Dictionary<string, Variant>
{
    public StringVariantMap()
        : base(StringComparer.Ordinal)
    {
    }

    public StringVariantMap(IDictionary<string, Variant> dictionary)
        : base(dictionary, StringComparer.Ordinal)
    {
    }
}

public static class VariantTypeUtility
{
    public static readonly JsonSerializerOptions SerializerOptions = new()
    {
        IncludeFields = true,
        IgnoreReadOnlyProperties = true,
        WriteIndented = false
    };

    static VariantTypeUtility()
    {
        SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    }

    public static VariantType GetVariantType(object? value)
    {
        return value switch
        {
            null => VariantType.None,
            Variant variant => variant.Type,
            bool => VariantType.Bool,
            int => VariantType.Int,
            uint => VariantType.Int,
            long => VariantType.Int64,
            ulong => VariantType.Int64,
            float => VariantType.Float,
            double => VariantType.Double,
            StringHash => VariantType.Int,
            Vector2f => VariantType.Vector2,
            Vector2d => VariantType.DoubleVector2,
            Vector3f => VariantType.Vector3,
            Vector3d => VariantType.DoubleVector3,
            Vector4f => VariantType.Vector4,
            Quaternion => VariantType.Quaternion,
            Color => VariantType.Color,
            Rect => VariantType.Rect,
            IntRect => VariantType.IntRect,
            Vector2i => VariantType.Vector2i,
            Vector3i => VariantType.Vector3i,
            byte[] => VariantType.Buffer,
            ResourceRef => VariantType.ResourceRef,
            ResourceRefList => VariantType.ResourceRefList,
            Matrix3 => VariantType.Matrix3,
            Matrix3x4 => VariantType.Matrix3x4,
            Matrix4 => VariantType.Matrix4,
            VariantVector => VariantType.VariantVector,
            StringVector => VariantType.StringVector,
            VariantMap => VariantType.VariantMap,
            StringVariantMap => VariantType.StringVariantMap,
            string => VariantType.String,
            CString => VariantType.CString,
            _ => VariantType.None
        };
    }

    public static bool IsInterpolatable(VariantType type)
    {
        return type is VariantType.Float or VariantType.Double or VariantType.Vector2 or VariantType.DoubleVector2 or VariantType.Vector3 or VariantType.DoubleVector3 or VariantType.Vector4 or VariantType.Quaternion or VariantType.Color or VariantType.Rect or VariantType.IntRect or VariantType.Vector2i or VariantType.Vector3i;
    }

    public static string Serialize(object? value, VariantType type)
    {
        if (value is Variant variant)
            value = variant.GetObject();

        if (type == VariantType.CString)
            return JsonSerializer.Serialize(value is CString cstring ? cstring.ConvertToString() : string.Empty, SerializerOptions);

        return JsonSerializer.Serialize(value, GetClrType(type), SerializerOptions);
    }

    public static string Serialize(Variant value)
    {
        return Serialize(value.GetObject(), value.Type);
    }

    public static object? Deserialize(string json, VariantType type)
    {
        if (type == VariantType.CString)
            return new CString(JsonSerializer.Deserialize<string>(json, SerializerOptions) ?? string.Empty);

        return JsonSerializer.Deserialize(json, GetClrType(type), SerializerOptions);
    }

    public static Variant DeserializeVariant(string json, VariantType type)
    {
        return Variant.FromObject(Deserialize(json, type));
    }

    public static Type GetClrType(VariantType type)
    {
        return type switch
        {
            VariantType.Bool => typeof(bool),
            VariantType.Int => typeof(int),
            VariantType.Int64 => typeof(long),
            VariantType.Float => typeof(float),
            VariantType.Double => typeof(double),
            VariantType.Vector2 => typeof(Vector2f),
            VariantType.DoubleVector2 => typeof(Vector2d),
            VariantType.Vector3 => typeof(Vector3f),
            VariantType.DoubleVector3 => typeof(Vector3d),
            VariantType.Vector4 => typeof(Vector4f),
            VariantType.Quaternion => typeof(Quaternion),
            VariantType.Color => typeof(Color),
            VariantType.Rect => typeof(Rect),
            VariantType.IntRect => typeof(IntRect),
            VariantType.Vector2i => typeof(Vector2i),
            VariantType.Vector3i => typeof(Vector3i),
            VariantType.Buffer => typeof(byte[]),
            VariantType.ResourceRef => typeof(ResourceRef),
            VariantType.ResourceRefList => typeof(ResourceRefList),
            VariantType.Matrix3 => typeof(Matrix3),
            VariantType.Matrix3x4 => typeof(Matrix3x4),
            VariantType.Matrix4 => typeof(Matrix4),
            VariantType.VariantVector => typeof(VariantVector),
            VariantType.StringVector => typeof(StringVector),
            VariantType.VariantMap => typeof(VariantMap),
            VariantType.StringVariantMap => typeof(StringVariantMap),
            VariantType.String => typeof(string),
            VariantType.CString => typeof(string),
            _ => typeof(object)
        };
    }
}
