using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RedBolt;

public enum VariantType : byte
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

[StructLayout(LayoutKind.Sequential)]
public struct Variant : IEquatable<Variant>, ICloneable, IDisposable
{
#if REDBOLT_PLATFORM_WEB
    [StructLayout(LayoutKind.Explicit, Size = 4 * sizeof(int))]
#else
    [StructLayout(LayoutKind.Explicit, Size = 4 * sizeof(long))]
#endif
    private struct VariantValue
    {
        [FieldOffset(0)] public bool Bool;
        [FieldOffset(0)] public int Int;
        [FieldOffset(0)] public long Int64;
        [FieldOffset(0)] public float Float;
        [FieldOffset(0)] public double Double;
        [FieldOffset(0)] public Vector2f Vector2;
        [FieldOffset(0)] public Vector2d DoubleVector2;
        [FieldOffset(0)] public Vector3f Vector3;
        [FieldOffset(0)] public Vector4f Vector4;
        [FieldOffset(0)] public Quaternion Quaternion;
        [FieldOffset(0)] public Color Color;
        [FieldOffset(0)] public Rect Rect;
        [FieldOffset(0)] public IntRect IntRect;
        [FieldOffset(0)] public Vector2i IntVector2;
        [FieldOffset(0)] public Vector3i IntVector3;
        [FieldOffset(0)] public IntPtr VoidPtr;
    }

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

    private static readonly VariantVector EmptyVariantVector = new();
    private static readonly StringVector EmptyStringVector = new();
    private static readonly VariantMap EmptyVariantMap = new();
    private static readonly StringVariantMap EmptyStringVariantMap = new();
    private static readonly VariantCurve EmptyVariantCurve = new();

    private readonly VariantType _type;
    private VariantValue _value;
    private object? _referenceValue;

    private string StringValue
    {
        readonly get => (string?)_referenceValue ?? string.Empty;
        set => _referenceValue = value;
    }

    private CString CStringValue
    {
        readonly get => (CString?)_referenceValue ?? CString.Empty;
        set => _referenceValue = value;
    }

    private byte[] BufferValue
    {
        readonly get => (byte[]?)_referenceValue ?? Array.Empty<byte>();
        set => _referenceValue = value;
    }

    private Vector3d DoubleVector3Value
    {
        readonly get => (Vector3d)_referenceValue!;
        set => _referenceValue = value;
    }

    private Matrix3 Matrix3Value
    {
        readonly get => (Matrix3)_referenceValue!;
        set => _referenceValue = value;
    }

    private Matrix3x4 Matrix3x4Value
    {
        readonly get => (Matrix3x4)_referenceValue!;
        set => _referenceValue = value;
    }

    private Matrix4 Matrix4Value
    {
        readonly get => (Matrix4)_referenceValue!;
        set => _referenceValue = value;
    }

    private ResourceRef ResourceRefValue
    {
        readonly get => new(new StringHash(unchecked((uint)_value.Int)), (string?)_referenceValue ?? string.Empty);
        set
        {
            _value.Int = unchecked((int)value.Type.Value);
            _referenceValue = value.Name;
        }
    }

    private ResourceRefList ResourceRefListValue
    {
        readonly get => new()
        {
            Type = new StringHash(unchecked((uint)_value.Int)),
            Names = (List<string>)_referenceValue!
        };
        set
        {
            _value.Int = unchecked((int)value.Type.Value);
            _referenceValue = value.Names;
        }
    }

    private VariantVector VariantVectorValue
    {
        readonly get => (VariantVector?)_referenceValue ?? EmptyVariantVector;
        set => _referenceValue = value;
    }

    private StringVector StringVectorValue
    {
        readonly get => (StringVector?)_referenceValue ?? EmptyStringVector;
        set => _referenceValue = value;
    }

    private VariantMap VariantMapValue
    {
        readonly get => (VariantMap?)_referenceValue ?? EmptyVariantMap;
        set => _referenceValue = value;
    }

    private StringVariantMap StringVariantMapValue
    {
        readonly get => (StringVariantMap?)_referenceValue ?? EmptyStringVariantMap;
        set => _referenceValue = value;
    }

    private VariantCurve VariantCurveValue
    {
        readonly get => (VariantCurve?)_referenceValue ?? EmptyVariantCurve;
        set => _referenceValue = value;
    }

    private CustomVariantValue CustomValue
    {
        readonly get => (CustomVariantValue?)_referenceValue ?? CustomVariantValue.Empty;
        set => _referenceValue = value;
    }

    private Variant(CustomVariantValue value)
    {
        _type = VariantType.Custom;
        CustomValue = value;
    }

    public Variant(VariantType type)
    {
        this = type switch
        {
            VariantType.Int => new Variant(0),
            VariantType.Int64 => new Variant(0L),
            VariantType.Bool => new Variant(false),
            VariantType.Float => new Variant(0.0f),
            VariantType.Vector2 => new Variant(Vector2f.Zero),
            VariantType.Vector3 => new Variant(Vector3f.Zero),
            VariantType.DoubleVector2 => new Variant(Vector2d.Zero),
            VariantType.DoubleVector3 => new Variant(Vector3d.Zero),
            VariantType.Vector4 => new Variant(Vector4f.Zero),
            VariantType.Quaternion => new Variant(Quaternion.IDENTITY),
            VariantType.Color => new Variant(Color.Black),
            VariantType.String => new Variant(string.Empty),
            VariantType.CString => new Variant(new CString()),
            VariantType.VoidPtr => new Variant(IntPtr.Zero),
            VariantType.ResourceRef => new Variant(ResourceRef.Empty),
            VariantType.ResourceRefList => new Variant(ResourceRefList.Empty),
            VariantType.VariantVector => new Variant(new VariantVector()),
            VariantType.VariantMap => new Variant(new VariantMap()),
            VariantType.IntRect => new Variant(IntRect.Zero),
            VariantType.Vector2i => new Variant(Vector2i.Zero),
            VariantType.Matrix3 => new Variant(Matrix3.Zero),
            VariantType.Matrix3x4 => new Variant(Matrix3x4.ZERO),
            VariantType.Matrix4 => new Variant(Matrix4.ZERO),
            VariantType.Double => new Variant(0.0),
            VariantType.StringVector => new Variant(new StringVector()),
            VariantType.Rect => new Variant(Rect.Zero),
            VariantType.Vector3i => new Variant(Vector3i.Zero),
            VariantType.StringVariantMap => new Variant(new StringVariantMap()),
            VariantType.VariantCurve => new Variant(new VariantCurve()),
            VariantType.Buffer =>
                new Variant(Array.Empty<byte>()),
            _ => default
        };
    }

    public Variant(bool value)
    {
        _type = VariantType.Bool;
        _value.Bool = value;
    }

    public Variant(int value)
    {
        _type = VariantType.Int;
        _value.Int = value;
    }

    public Variant(uint value)
        : this(unchecked((int)value))
    {
    }

    public Variant(long value)
    {
        _type = VariantType.Int64;
        _value.Int64 = value;
    }

    public Variant(ulong value)
        : this(unchecked((long)value))
    {
    }

    public Variant(float value)
    {
        _type = VariantType.Float;
        _value.Float = value;
    }

    public Variant(double value)
    {
        _type = VariantType.Double;
        _value.Double = value;
    }

    public Variant(Vector2f value)
    {
        _type = VariantType.Vector2;
        _value.Vector2 = value;
    }

    public Variant(Vector2d value)
    {
        _type = VariantType.DoubleVector2;
        _value.DoubleVector2 = value;
    }

    public Variant(Vector3f value)
    {
        _type = VariantType.Vector3;
        _value.Vector3 = value;
    }

    public Variant(Vector3d value)
    {
        _type = VariantType.DoubleVector3;
        DoubleVector3Value = value;
    }

    public Variant(Vector4f value)
    {
        _type = VariantType.Vector4;
        _value.Vector4 = value;
    }

    public Variant(Quaternion value)
    {
        _type = VariantType.Quaternion;
        _value.Quaternion = value;
    }

    public Variant(Color value)
    {
        _type = VariantType.Color;
        _value.Color = value;
    }

    public Variant(Rect value)
    {
        _type = VariantType.Rect;
        _value.Rect = value;
    }

    public Variant(IntRect value)
    {
        _type = VariantType.IntRect;
        _value.IntRect = value;
    }

    public Variant(Vector2i value)
    {
        _type = VariantType.Vector2i;
        _value.IntVector2 = value;
    }

    public Variant(Vector3i value)
    {
        _type = VariantType.Vector3i;
        _value.IntVector3 = value;
    }

    public Variant(StringHash value)
        : this(unchecked((int)value.Value))
    {
    }

    public Variant(byte[] value)
    {
        _type = VariantType.Buffer;
        BufferValue = value is null ? [] : (byte[])value.Clone();
    }

    /// <summary>
    /// Construct from a VectorBuffer and store as a buffer.
    /// </summary>
    public Variant(VectorBuffer value)
        : this(value.GetBuffer())
    {
    }

    /// <summary>
    /// Construct from a pointer.
    /// </summary>
    public Variant(IntPtr value)
    {
        _type = VariantType.VoidPtr;
        _value.VoidPtr = value;
    }

    public Variant(ResourceRef value)
    {
        _type = VariantType.ResourceRef;
        ResourceRefValue = value;
    }

    public Variant(ResourceRefList value)
    {
        _type = VariantType.ResourceRefList;
        ResourceRefListValue = new ResourceRefList(value.Type, value.Names);
    }

    public Variant(Matrix3 value)
    {
        _type = VariantType.Matrix3;
        Matrix3Value = value;
    }

    public Variant(Matrix3x4 value)
    {
        _type = VariantType.Matrix3x4;
        Matrix3x4Value = value;
    }

    public Variant(Matrix4 value)
    {
        _type = VariantType.Matrix4;
        Matrix4Value = value;
    }

    public Variant(VariantVector value)
    {
        _type = VariantType.VariantVector;
        VariantVectorValue = value is null ? new VariantVector() : new VariantVector(value);
    }

    public Variant(StringVector value)
    {
        _type = VariantType.StringVector;
        StringVectorValue = value is null ? new StringVector() : new StringVector(value);
    }

    public Variant(VariantMap value)
    {
        _type = VariantType.VariantMap;
        VariantMapValue = value is null ? new VariantMap() : new VariantMap(value);
    }

    public Variant(StringVariantMap value)
    {
        _type = VariantType.StringVariantMap;
        StringVariantMapValue = value is null ? new StringVariantMap() : new StringVariantMap(value);
    }

    public Variant(VariantCurve value)
    {
        _type = VariantType.VariantCurve;
        VariantCurveValue = value?.Clone() ?? new VariantCurve();
    }

    public Variant(string value)
    {
        if (value is null)
            return;

        _type = VariantType.String;
        StringValue = value;
    }

    public Variant(CString value)
    {
        _type = VariantType.CString;
        CStringValue = value is null ? new CString() : (CString)value.Clone();
    }

    public Variant Clone()
    {
        return _type switch
        {
            VariantType.Buffer => new Variant(BufferValue),
            VariantType.CString => new Variant(CStringValue),
            VariantType.ResourceRefList => new Variant(ResourceRefListValue),
            VariantType.VariantVector => new Variant(VariantVectorValue!),
            VariantType.StringVector => new Variant(StringVectorValue!),
            VariantType.VariantMap => new Variant(VariantMapValue!),
            VariantType.StringVariantMap => new Variant(StringVariantMapValue!),
            VariantType.VariantCurve => new Variant(VariantCurveValue),
            VariantType.Custom => new Variant((CustomVariantValue)CustomValue.Clone()),
            _ => this
        };
    }

    object ICloneable.Clone() => Clone();

    public void Dispose()
    {
        switch (_type)
        {
        case VariantType.CString:
            CStringValue?.Dispose();
            break;
        case VariantType.VariantVector:
            VariantVectorValue!.Dispose();
            break;
        case VariantType.VariantMap:
            VariantMapValue!.Dispose();
            break;
        case VariantType.StringVariantMap:
            StringVariantMapValue!.Dispose();
            break;
        case VariantType.Custom:
            if (!ReferenceEquals(CustomValue, CustomVariantValue.Empty))
                CustomValue.Dispose();
            break;
        case VariantType.VariantCurve:
            if (!ReferenceEquals(VariantCurveValue, EmptyVariantCurve))
                VariantCurveValue.Dispose();
            break;
        }
        this = default;
    }

    /// <summary>Reset to empty.</summary>
    public void Clear() => Dispose();

    /// <summary>Set buffer type from a memory area.</summary>
    public void SetBuffer(ReadOnlySpan<byte> data, uint size)
    {
        var replacement = new Variant(data[..(int)size].ToArray());
        Dispose();
        this = replacement;
    }

    /// <summary>Set custom value.</summary>
    public void SetCustom<T>(T value)
    {
        var replacement = MakeCustomValue(value);
        Dispose();
        this = replacement;
    }

    public VariantType Type => _type;

    public bool IsEmpty => _type == VariantType.None;

    public bool GetBool()
    {
        return _type switch
        {
            VariantType.Bool => _value.Bool,
            VariantType.Int => _value.Int != 0,
            VariantType.Int64 => _value.Int64 != 0,
            VariantType.Float => _value.Float != 0.0f,
            VariantType.Double => _value.Double != 0.0,
            VariantType.VoidPtr => _value.VoidPtr != IntPtr.Zero,
            _ => false
        };
    }

    public int GetInt()
    {
        return _type switch
        {
            VariantType.Int => _value.Int,
            VariantType.Int64 => unchecked((int)_value.Int64),
            VariantType.Float => unchecked((int)_value.Float),
            VariantType.Double => unchecked((int)_value.Double),
            VariantType.Bool => _value.Bool ? 1 : 0,
            _ => 0
        };
    }

    public long GetInt64()
    {
        return _type switch
        {
            VariantType.Int64 => _value.Int64,
            VariantType.Int => _value.Int,
            VariantType.Float => unchecked((long)_value.Float),
            VariantType.Double => unchecked((long)_value.Double),
            VariantType.Bool => _value.Bool ? 1L : 0L,
            _ => 0L
        };
    }

    public float GetFloat()
    {
        return _type switch
        {
            VariantType.Float => _value.Float,
            VariantType.Double => unchecked((float)_value.Double),
            VariantType.Int => _value.Int,
            VariantType.Int64 => _value.Int64,
            VariantType.Bool => _value.Bool ? 1.0f : 0.0f,
            _ => 0.0f
        };
    }

    public double GetDouble()
    {
        return _type switch
        {
            VariantType.Double => _value.Double,
            VariantType.Float => _value.Float,
            VariantType.Int => _value.Int,
            VariantType.Int64 => _value.Int64,
            VariantType.Bool => _value.Bool ? 1.0 : 0.0,
            _ => 0.0
        };
    }

    public Vector2f GetVector2() => _type == VariantType.Vector2 ? _value.Vector2 : Vector2f.Zero;

    public Vector2d GetDoubleVector2() => _type == VariantType.DoubleVector2 ? _value.DoubleVector2 : Vector2d.Zero;

    public Vector3f GetVector3() => _type == VariantType.Vector3 ? _value.Vector3 : Vector3f.Zero;

    public Vector3d GetDoubleVector3() => _type == VariantType.DoubleVector3 ? DoubleVector3Value : Vector3d.Zero;

    public Vector4f GetVector4() => _type == VariantType.Vector4 ? _value.Vector4 : Vector4f.Zero;

    public Quaternion GetQuaternion() => _type == VariantType.Quaternion ? _value.Quaternion : Quaternion.IDENTITY;

    public Color GetColor() => _type switch
    {
        VariantType.Color => _value.Color,
        VariantType.Vector4 => new Color(_value.Vector4.X, _value.Vector4.Y, _value.Vector4.Z, _value.Vector4.W),
        _ => Color.White
    };

    public Rect GetRect() => _type == VariantType.Rect ? _value.Rect : Rect.Zero;

    public IntRect GetIntRect() => _type == VariantType.IntRect ? _value.IntRect : IntRect.Zero;

    public Vector2i GetIntVector2() => _type == VariantType.Vector2i ? _value.IntVector2 : Vector2i.Zero;

    public Vector3i GetIntVector3() => _type == VariantType.Vector3i ? _value.IntVector3 : Vector3i.Zero;

    public uint GetUInt()
    {
        return _type switch
        {
            VariantType.Int => unchecked((uint)_value.Int),
            VariantType.Int64 => unchecked((uint)_value.Int64),
            VariantType.Float => unchecked((uint)_value.Float),
            VariantType.Double => unchecked((uint)_value.Double),
            VariantType.Bool => _value.Bool ? 1u : 0u,
            _ => 0u
        };
    }

    public ulong GetUInt64()
    {
        return _type switch
        {
            VariantType.Int64 => unchecked((ulong)_value.Int64),
            VariantType.Int => unchecked((ulong)_value.Int),
            VariantType.Float => unchecked((ulong)_value.Float),
            VariantType.Double => unchecked((ulong)_value.Double),
            VariantType.Bool => _value.Bool ? 1u : 0u,
            _ => 0u
        };
    }

    public StringHash GetStringHash() => _type == VariantType.Int ? new StringHash(GetUInt()) : StringHash.Empty;

    public string GetString() => _type == VariantType.String ? StringValue : string.Empty;

    public CString GetCString() => _type == VariantType.CString ? CStringValue : CString.Empty;

    public byte[] GetBuffer() => _type == VariantType.Buffer ? BufferValue : Array.Empty<byte>();

    /// <summary>
    /// Return VectorBuffer containing the buffer or empty on type mismatch.
    /// </summary>
    public VectorBuffer GetVectorBuffer() => new(GetBuffer());

    /// <summary>
    /// Return void pointer or null on type mismatch.
    /// </summary>
    public IntPtr GetVoidPtr() => _type == VariantType.VoidPtr ? _value.VoidPtr : IntPtr.Zero;

    public ResourceRef GetResourceRef() => _type == VariantType.ResourceRef ? ResourceRefValue : ResourceRef.Empty;

    public ResourceRefList GetResourceRefList() => _type == VariantType.ResourceRefList ? ResourceRefListValue : ResourceRefList.Empty;

    public Matrix3 GetMatrix3() => _type == VariantType.Matrix3 ? Matrix3Value : Matrix3.Identity;

    public Matrix3x4 GetMatrix3x4() => _type == VariantType.Matrix3x4 ? Matrix3x4Value : Matrix3x4.IDENTITY;

    public Matrix4 GetMatrix4() => _type == VariantType.Matrix4 ? Matrix4Value : Matrix4.IDENTITY;

    public VariantVector GetVariantVector() => _type == VariantType.VariantVector ? VariantVectorValue! : EmptyVariantVector;

    public StringVector GetStringVector() => _type == VariantType.StringVector ? StringVectorValue! : EmptyStringVector;

    public VariantMap GetVariantMap() => _type == VariantType.VariantMap ? VariantMapValue! : EmptyVariantMap;

    public StringVariantMap GetStringVariantMap() => _type == VariantType.StringVariantMap ? StringVariantMapValue! : EmptyStringVariantMap;

    public VariantCurve GetVariantCurve() => _type == VariantType.VariantCurve ? VariantCurveValue : EmptyVariantCurve;

    public T GetCustom<T>()
    {
        return _type == VariantType.Custom && CustomValue.TryGetValue(out T value) ? value : default!;
    }

    public bool IsCustomType<T>()
    {
        return _type == VariantType.Custom && CustomValue.IsType<T>();
    }

    public bool TryGetCustom<T>(out T value)
    {
        if (_type == VariantType.Custom)
            return CustomValue.TryGetValue(out value);

        value = default!;
        return false;
    }

    public CustomVariantValue GetCustomVariantValue() => _type == VariantType.Custom ? CustomValue : CustomVariantValue.Empty;

    public static Variant MakeCustomValue<T>(T value)
    {
        return new Variant(new CustomVariantValueImpl<T>(value));
    }

    /// <summary>
    /// Return the value, template version.
    /// </summary>
    public T Get<T>()
    {
        var type = typeof(T);
        if (type.IsEnum)
            return (T)Enum.ToObject(type, GetInt());

        if (type == typeof(int)) return Reinterpret<int, T>(GetInt());
        if (type == typeof(uint)) return Reinterpret<uint, T>(GetUInt());
        if (type == typeof(long)) return Reinterpret<long, T>(GetInt64());
        if (type == typeof(ulong)) return Reinterpret<ulong, T>(GetUInt64());
        if (type == typeof(StringHash)) return Reinterpret<StringHash, T>(GetStringHash());
        if (type == typeof(bool)) return Reinterpret<bool, T>(GetBool());
        if (type == typeof(float)) return Reinterpret<float, T>(GetFloat());
        if (type == typeof(double)) return Reinterpret<double, T>(GetDouble());
        if (type == typeof(Vector2f)) return Reinterpret<Vector2f, T>(GetVector2());
        if (type == typeof(Vector2d)) return Reinterpret<Vector2d, T>(GetDoubleVector2());
        if (type == typeof(Vector3f)) return Reinterpret<Vector3f, T>(GetVector3());
        if (type == typeof(Vector3d)) return Reinterpret<Vector3d, T>(GetDoubleVector3());
        if (type == typeof(Vector4f)) return Reinterpret<Vector4f, T>(GetVector4());
        if (type == typeof(Quaternion)) return Reinterpret<Quaternion, T>(GetQuaternion());
        if (type == typeof(Color)) return Reinterpret<Color, T>(GetColor());
        if (type == typeof(string)) return Reinterpret<string, T>(GetString());
        if (type == typeof(CString)) return Reinterpret<CString, T>((CString)GetCString().Clone());
        if (type == typeof(byte[])) return Reinterpret<byte[], T>((byte[])GetBuffer().Clone());
        if (type == typeof(IntPtr)) return Reinterpret<IntPtr, T>(GetVoidPtr());
        if (type == typeof(ResourceRef)) return Reinterpret<ResourceRef, T>(GetResourceRef());
        if (type == typeof(ResourceRefList)) return Reinterpret<ResourceRefList, T>(new ResourceRefList(GetResourceRefList().Type, GetResourceRefList().Names));
        if (type == typeof(VariantVector)) return Reinterpret<VariantVector, T>(new VariantVector(GetVariantVector()));
        if (type == typeof(StringVector)) return Reinterpret<StringVector, T>(new StringVector(GetStringVector()));
        if (type == typeof(VariantMap)) return Reinterpret<VariantMap, T>(new VariantMap(GetVariantMap()));
        if (type == typeof(Rect)) return Reinterpret<Rect, T>(GetRect());
        if (type == typeof(IntRect)) return Reinterpret<IntRect, T>(GetIntRect());
        if (type == typeof(Vector2i)) return Reinterpret<Vector2i, T>(GetIntVector2());
        if (type == typeof(Vector3i)) return Reinterpret<Vector3i, T>(GetIntVector3());
        if (type == typeof(Matrix3)) return Reinterpret<Matrix3, T>(GetMatrix3());
        if (type == typeof(Matrix3x4)) return Reinterpret<Matrix3x4, T>(GetMatrix3x4());
        if (type == typeof(Matrix4)) return Reinterpret<Matrix4, T>(GetMatrix4());
        if (type == typeof(StringVariantMap)) return Reinterpret<StringVariantMap, T>(new StringVariantMap(GetStringVariantMap()));
        if (type == typeof(VariantCurve)) return Reinterpret<VariantCurve, T>(GetVariantCurve().Clone());
        if (_type == VariantType.Custom) return GetCustom<T>();

        throw new ArgumentException($"Unsupported Variant value type '{type.Name}'.", nameof(T));
    }

    public T? GetOptional<T>() where T : struct
    {
        return IsEmpty ? null : Get<T>();
    }

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

    /// <summary>Empty variant.</summary>
    public static readonly Variant Empty = new();

    public Variant Convert(VariantType targetType)
    {
        if (targetType == _type)
            return Clone();
        if (targetType == VariantType.None)
            return default;
        if (targetType == VariantType.String)
            return ToString();

        if (_type == VariantType.String)
        {
            var result = default(Variant);
            result.FromString(targetType, StringValue);
            return result;
        }

        return targetType switch
        {
            VariantType.Int => new Variant(GetInt()),
            VariantType.Bool => new Variant(GetBool()),
            VariantType.Float => new Variant(GetFloat()),
            VariantType.Double => new Variant(GetDouble()),
            VariantType.Int64 => new Variant(GetInt64()),
            _ => default
        };
    }

    /// <summary>Set from type and value string.</summary>
    public void FromString(string type, string value) => FromString(GetTypeFromName(type), value);

    /// <summary>Set from type and value string.</summary>
    public void FromString(VariantType type, string value)
    {
        var replacement = CreateFromString(type, value);
        Dispose();
        this = replacement;
    }

    private static Variant CreateFromString(VariantType type, string value)
    {
        value ??= string.Empty;
        return type switch
        {
            VariantType.Int => new Variant(ParseInt(value)),
            VariantType.Int64 => new Variant(ParseInt64(value)),
            VariantType.Bool => new Variant(ParseBool(value)),
            VariantType.Float => new Variant(ParseFloat(value)),
            VariantType.Double => new Variant(ParseDouble(value)),
            VariantType.Vector2 => new Variant(ParseVector2(value)),
            VariantType.Vector3 => new Variant(ParseVector3(value)),
            VariantType.DoubleVector2 => new Variant(ParseDoubleVector2(value)),
            VariantType.DoubleVector3 => new Variant(ParseDoubleVector3(value)),
            VariantType.Vector4 => new Variant(ParseVector4(value)),
            VariantType.Quaternion => new Variant(ParseQuaternion(value)),
            VariantType.Color => new Variant(ParseColor(value)),
            VariantType.Rect => new Variant(ParseRect(value)),
            VariantType.IntRect => new Variant(ParseIntRect(value)),
            VariantType.Vector2i => new Variant(ParseIntVector2(value)),
            VariantType.Vector3i => new Variant(ParseIntVector3(value)),
            VariantType.Matrix3 => new Variant(ParseMatrix3(value)),
            VariantType.Matrix3x4 => new Variant(ParseMatrix3x4(value)),
            VariantType.Matrix4 => new Variant(ParseMatrix4(value)),
            VariantType.String => new Variant(value),
            VariantType.CString => new Variant(new CString(value)),
            VariantType.Buffer => new Variant(ParseBuffer(value)),
            VariantType.VoidPtr => new Variant(IntPtr.Zero),
            VariantType.ResourceRef => new Variant(ResourceRef.Parse(value)),
            VariantType.ResourceRefList => new Variant(ResourceRefList.Parse(value)),
            _ => default
        };
    }

    /// <summary>
    /// Linear interpolation. Supported for scalars, vectors and some other types.
    /// </summary>
    public Variant Lerp(Variant rhs, float t)
    {
        return _type switch
        {
            VariantType.Float => new Variant(MathDefs.Lerp(GetFloat(), rhs.GetFloat(), t)),
            VariantType.Double => new Variant(GetDouble() * (1.0 - t) + rhs.GetDouble() * t),
            VariantType.Int => new Variant(unchecked((int)(GetInt() * (1.0 - t) + rhs.GetInt() * t))),
            VariantType.Int64 => new Variant(unchecked((long)(GetInt64() * (1.0 - t) + rhs.GetInt64() * t))),
            VariantType.Vector2 => new Variant(GetVector2().Lerp(rhs.GetVector2(), t)),
            VariantType.Vector3 => new Variant(GetVector3().Lerp(rhs.GetVector3(), t)),
            VariantType.DoubleVector2 => new Variant(GetDoubleVector2().Lerp(rhs.GetDoubleVector2(), t)),
            VariantType.DoubleVector3 => new Variant(GetDoubleVector3().Lerp(rhs.GetDoubleVector3(), t)),
            VariantType.Vector4 => new Variant(GetVector4().Lerp(rhs.GetVector4(), t)),
            VariantType.Quaternion => new Variant(GetQuaternion().Slerp(rhs.GetQuaternion(), t)),
            VariantType.Color => new Variant(GetColor().Lerp(rhs.GetColor(), t)),
            VariantType.IntRect => LerpIntRect(rhs, t),
            VariantType.Vector2i => LerpIntVector2(rhs, t),
            VariantType.Vector3i => LerpIntVector3(rhs, t),
            _ => Clone()
        };
    }

    public bool IsZero()
    {
        return _type switch
        {
            VariantType.None => true,
            VariantType.Bool => !_value.Bool,
            VariantType.Int => _value.Int == 0,
            VariantType.Int64 => _value.Int64 == 0,
            VariantType.Float => _value.Float == 0.0f,
            VariantType.Double => _value.Double == 0.0,
            VariantType.Vector2 => _value.Vector2 == Vector2f.Zero,
            VariantType.DoubleVector2 => _value.DoubleVector2 == Vector2d.Zero,
            VariantType.Vector3 => _value.Vector3 == Vector3f.Zero,
            VariantType.DoubleVector3 => DoubleVector3Value == Vector3d.Zero,
            VariantType.Vector4 => _value.Vector4 == Vector4f.Zero,
            VariantType.Quaternion => _value.Quaternion == Quaternion.IDENTITY,
            VariantType.Color => _value.Color == Color.White,
            VariantType.String => string.IsNullOrEmpty(StringValue),
            VariantType.CString => CStringValue.ByteLength == 0,
            VariantType.Buffer => BufferValue.Length == 0,
            VariantType.VoidPtr => _value.VoidPtr == IntPtr.Zero,
            VariantType.ResourceRef => string.IsNullOrEmpty(ResourceRefValue.Name),
            VariantType.ResourceRefList => AllNullOrEmpty(ResourceRefListValue.Names),
            VariantType.VariantVector => VariantVectorValue!.Count == 0,
            VariantType.StringVector => StringVectorValue!.Count == 0,
            VariantType.VariantMap => VariantMapValue!.Count == 0,
            VariantType.IntRect => _value.IntRect == IntRect.Zero,
            VariantType.Vector2i => _value.IntVector2 == Vector2i.Zero,
            VariantType.Vector3i => _value.IntVector3 == Vector3i.Zero,
            VariantType.Matrix3 => Matrix3Value == Matrix3.Identity,
            VariantType.Matrix3x4 => Matrix3x4Value == Matrix3x4.IDENTITY,
            VariantType.Matrix4 => Matrix4Value == Matrix4.IDENTITY,
            VariantType.Rect => _value.Rect == Rect.Zero,
            VariantType.Custom => CustomValue.IsZero(),
            VariantType.StringVariantMap => StringVariantMapValue!.Count == 0,
            VariantType.VariantCurve => VariantCurveValue.KeyFrames.Count == 0,
            _ => true
        };
    }

    public object GetObject()
    {
        return _type switch
        {
            VariantType.None => null!,
            VariantType.Bool => _value.Bool,
            VariantType.Int => _value.Int,
            VariantType.Int64 => _value.Int64,
            VariantType.Float => _value.Float,
            VariantType.Double => _value.Double,
            VariantType.Vector2 => _value.Vector2,
            VariantType.DoubleVector2 => _value.DoubleVector2,
            VariantType.Vector3 => _value.Vector3,
            VariantType.DoubleVector3 => DoubleVector3Value,
            VariantType.Vector4 => _value.Vector4,
            VariantType.Quaternion => _value.Quaternion,
            VariantType.Color => _value.Color,
            VariantType.Rect => _value.Rect,
            VariantType.IntRect => _value.IntRect,
            VariantType.Vector2i => _value.IntVector2,
            VariantType.Vector3i => _value.IntVector3,
            VariantType.String => StringValue,
            VariantType.CString => CStringValue.Clone(),
            VariantType.Buffer => BufferValue.Clone(),
            VariantType.VoidPtr => _value.VoidPtr,
            VariantType.ResourceRef => ResourceRefValue,
            VariantType.ResourceRefList => new ResourceRefList(ResourceRefListValue.Type, ResourceRefListValue.Names),
            VariantType.Matrix3 => Matrix3Value,
            VariantType.Matrix3x4 => Matrix3x4Value,
            VariantType.Matrix4 => Matrix4Value,
            VariantType.VariantVector => new VariantVector(VariantVectorValue!),
            VariantType.StringVector => new StringVector(StringVectorValue!),
            VariantType.VariantMap => new VariantMap(VariantMapValue!),
            VariantType.StringVariantMap => new StringVariantMap(StringVariantMapValue!),
            VariantType.VariantCurve => VariantCurveValue.Clone(),
            VariantType.Custom => CustomValue.Value is ICloneable cloneable ? cloneable.Clone() : CustomValue.Value,
            _ => null!
        };
    }

    public static Variant FromObject(object? value)
    {
        if (value is not null && value.GetType().IsEnum)
            return new Variant(System.Convert.ToInt32(value, CultureInfo.InvariantCulture));
        if (value is IEnumerable<KeyValuePair<StringHash, object?>> objectMap)
        {
            var variantMap = new VariantMap();
            foreach (var pair in objectMap)
                variantMap[pair.Key] = FromObject(pair.Value);
            return new Variant(variantMap);
        }
        if (value is IEnumerable<string> stringCollection)
            return new Variant(new StringVector(stringCollection));

        return value switch
        {
            null => default,
            Variant variant => variant.Clone(),
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
            VectorBuffer typedValue => typedValue,
            ResourceRef typedValue => typedValue,
            ResourceRefList typedValue => typedValue,
            Matrix3 typedValue => typedValue,
            Matrix3x4 typedValue => typedValue,
            Matrix4 typedValue => typedValue,
            VariantVector typedValue => typedValue,
            StringVector typedValue => typedValue,
            VariantMap typedValue => typedValue,
            StringVariantMap typedValue => typedValue,
            VariantCurve typedValue => typedValue,
            CustomVariantValue typedValue => new Variant((CustomVariantValue)typedValue.Clone()),
            string typedValue => typedValue,
            CString typedValue => typedValue,
            IntPtr typedValue => typedValue,
            _ => new Variant(new CustomVariantValue(value))
        };
    }

    public bool Equals(Variant other)
    {
        if (_type != other._type)
            return false;

        return _type switch
        {
            VariantType.None => true,
            VariantType.Bool => _value.Bool == other._value.Bool,
            VariantType.Int => _value.Int == other._value.Int,
            VariantType.Int64 => _value.Int64 == other._value.Int64,
            VariantType.Float => _value.Float.Equals(other._value.Float),
            VariantType.Double => _value.Double.Equals(other._value.Double),
            VariantType.Vector2 => _value.Vector2.Equals(other._value.Vector2),
            VariantType.DoubleVector2 => _value.DoubleVector2.Equals(other._value.DoubleVector2),
            VariantType.Vector3 => _value.Vector3.Equals(other._value.Vector3),
            VariantType.DoubleVector3 => DoubleVector3Value.Equals(other.DoubleVector3Value),
            VariantType.Vector4 => _value.Vector4.Equals(other._value.Vector4),
            VariantType.Quaternion => _value.Quaternion.Equals(other._value.Quaternion),
            VariantType.Color => _value.Color.Equals(other._value.Color),
            VariantType.Rect => _value.Rect.Equals(other._value.Rect),
            VariantType.IntRect => _value.IntRect.Equals(other._value.IntRect),
            VariantType.Vector2i => _value.IntVector2.Equals(other._value.IntVector2),
            VariantType.Vector3i => _value.IntVector3.Equals(other._value.IntVector3),
            VariantType.String => string.Equals(StringValue, other.StringValue, StringComparison.Ordinal),
            VariantType.CString => CStringValue.Equals(other.CStringValue),
            VariantType.Buffer => BufferValue.AsSpan().SequenceEqual(other.BufferValue),
            VariantType.VoidPtr => _value.VoidPtr == other._value.VoidPtr,
            VariantType.ResourceRef => ResourceRefValue.Equals(other.ResourceRefValue),
            VariantType.ResourceRefList => ResourceRefListValue.Equals(other.ResourceRefListValue),
            VariantType.Matrix3 => Matrix3Value.Equals(other.Matrix3Value),
            VariantType.Matrix3x4 => Matrix3x4Value.Equals(other.Matrix3x4Value),
            VariantType.Matrix4 => Matrix4Value.Equals(other.Matrix4Value),
            VariantType.VariantVector => SequenceEqual(VariantVectorValue!, other.VariantVectorValue!),
            VariantType.StringVector => SequenceEqual(StringVectorValue!, other.StringVectorValue!),
            VariantType.VariantMap => DictionaryEqual(VariantMapValue!, other.VariantMapValue!),
            VariantType.StringVariantMap => DictionaryEqual(StringVariantMapValue!, other.StringVariantMapValue!),
            VariantType.VariantCurve => VariantCurveValue.Equals(other.VariantCurveValue),
            VariantType.Custom => CustomValue.Equals(other.CustomValue),
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
            hash.Add(_value.Bool);
            break;
        case VariantType.Int:
            hash.Add(_value.Int);
            break;
        case VariantType.Int64:
            hash.Add(_value.Int64);
            break;
        case VariantType.Float:
            hash.Add(_value.Float);
            break;
        case VariantType.Double:
            hash.Add(_value.Double);
            break;
        case VariantType.Vector2:
            hash.Add(_value.Vector2);
            break;
        case VariantType.DoubleVector2:
            hash.Add(_value.DoubleVector2);
            break;
        case VariantType.Vector3:
            hash.Add(_value.Vector3);
            break;
        case VariantType.DoubleVector3:
            hash.Add(DoubleVector3Value);
            break;
        case VariantType.Vector4:
            hash.Add(_value.Vector4);
            break;
        case VariantType.Quaternion:
            hash.Add(_value.Quaternion);
            break;
        case VariantType.Color:
            hash.Add(_value.Color);
            break;
        case VariantType.Rect:
            hash.Add(_value.Rect);
            break;
        case VariantType.IntRect:
            hash.Add(_value.IntRect);
            break;
        case VariantType.Vector2i:
            hash.Add(_value.IntVector2);
            break;
        case VariantType.Vector3i:
            hash.Add(_value.IntVector3);
            break;
        case VariantType.String:
            hash.Add(StringValue, StringComparer.Ordinal);
            break;
        case VariantType.CString:
            hash.Add(CStringValue);
            break;
        case VariantType.Buffer:
            foreach (var item in BufferValue)
                hash.Add(item);
            break;
        case VariantType.VoidPtr:
            hash.Add(_value.VoidPtr);
            break;
        case VariantType.ResourceRef:
            hash.Add(ResourceRefValue);
            break;
        case VariantType.ResourceRefList:
            hash.Add(ResourceRefListValue);
            break;
        case VariantType.Matrix3:
            hash.Add(Matrix3Value);
            break;
        case VariantType.Matrix3x4:
            hash.Add(Matrix3x4Value);
            break;
        case VariantType.Matrix4:
            hash.Add(Matrix4Value);
            break;
        case VariantType.VariantVector:
            AddSequenceHash(ref hash, VariantVectorValue!);
            break;
        case VariantType.StringVector:
            AddSequenceHash(ref hash, StringVectorValue!);
            break;
        case VariantType.VariantMap:
            AddDictionaryHash(ref hash, VariantMapValue!);
            break;
        case VariantType.StringVariantMap:
            AddDictionaryHash(ref hash, StringVariantMapValue!);
            break;
        case VariantType.VariantCurve:
            hash.Add(VariantCurveValue);
            break;
        case VariantType.Custom:
            hash.Add(CustomValue);
            break;
        }

        return hash.ToHashCode();
    }

    /// <summary>Hash function for containers.</summary>
    public uint ToHash() => unchecked((uint)GetHashCode());

    public override string ToString()
    {
        return _type switch
        {
            VariantType.None => string.Empty,
            VariantType.Int => _value.Int.ToString(CultureInfo.InvariantCulture),
            VariantType.Int64 => _value.Int64.ToString(CultureInfo.InvariantCulture),
            VariantType.Bool => _value.Bool ? "true" : "false",
            VariantType.Float => _value.Float.ToString(CultureInfo.InvariantCulture),
            VariantType.Vector2 => _value.Vector2.ToString(),
            VariantType.Vector3 => _value.Vector3.ToString(),
            VariantType.Vector4 => _value.Vector4.ToString(),
            VariantType.DoubleVector2 => _value.DoubleVector2.ToString(),
            VariantType.DoubleVector3 => DoubleVector3Value.ToString(),
            VariantType.Quaternion => _value.Quaternion.ToString(),
            VariantType.Color => _value.Color.ToString(),
            VariantType.String => StringValue,
            VariantType.CString => CStringValue.ToString(),
            VariantType.Buffer => BufferToString(BufferValue),
            VariantType.VoidPtr => string.Empty,
            VariantType.ResourceRef => ResourceRefValue.ToString(Context.Instance),
            VariantType.ResourceRefList => ResourceRefListValue.ToString(Context.Instance),
            VariantType.IntRect => _value.IntRect.ToString(),
            VariantType.Vector2i => _value.IntVector2.ToString(),
            VariantType.Vector3i => _value.IntVector3.ToString(),
            VariantType.Matrix3 => Matrix3Value.ToString(),
            VariantType.Matrix3x4 => Matrix3x4Value.ToString(),
            VariantType.Matrix4 => Matrix4Value.ToString(),
            VariantType.Double => _value.Double.ToString(CultureInfo.InvariantCulture),
            VariantType.Rect => _value.Rect.ToString(),
            VariantType.Custom => CustomValue.ToString(),
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
    public static implicit operator Variant(VectorBuffer value) => new(value);
    public static implicit operator Variant(IntPtr value) => new(value);
    public static implicit operator Variant(ResourceRef value) => new(value);
    public static implicit operator Variant(ResourceRefList value) => new(value);
    public static implicit operator Variant(Matrix3 value) => new(value);
    public static implicit operator Variant(Matrix3x4 value) => new(value);
    public static implicit operator Variant(Matrix4 value) => new(value);
    public static implicit operator Variant(VariantVector value) => new(value);
    public static implicit operator Variant(StringVector value) => new(value);
    public static implicit operator Variant(VariantMap value) => new(value);
    public static implicit operator Variant(StringVariantMap value) => new(value);
    public static implicit operator Variant(VariantCurve value) => new(value);
    public static implicit operator Variant(string value) => new(value);
    public static implicit operator Variant(CString value) => new(value);
    public static bool operator ==(Variant lhs, Variant rhs) => lhs.Equals(rhs);
    public static bool operator !=(Variant lhs, Variant rhs) => !lhs.Equals(rhs);

    private static TTo Reinterpret<TFrom, TTo>(TFrom value)
    {
        return Unsafe.As<TFrom, TTo>(ref value);
    }

    private static bool ParseBool(string value)
    {
        foreach (var character in value)
        {
            if (character is ' ' or '\t')
                continue;

            return char.ToLowerInvariant(character) is 't' or 'y' or '1';
        }

        return false;
    }

    private static int ParseInt(string value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : 0;
    }

    private static long ParseInt64(string value)
    {
        return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : 0L;
    }

    private static float ParseFloat(string value)
    {
        return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) ? result : 0.0f;
    }

    private static double ParseDouble(string value)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) ? result : 0.0;
    }

    private static byte[] ParseBuffer(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Array.Empty<byte>();

        var tokens = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var result = new byte[tokens.Length];
        for (var index = 0; index < tokens.Length; ++index)
            result[index] = byte.TryParse(tokens[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out var item) ? item : (byte)0;

        return result;
    }

    private static float[] ParseFloatTokens(string value)
    {
        var tokens = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var result = new float[tokens.Length];
        for (var index = 0; index < tokens.Length; ++index)
            result[index] = ParseFloat(tokens[index]);
        return result;
    }

    private static int[] ParseIntTokens(string value)
    {
        var tokens = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var result = new int[tokens.Length];
        for (var index = 0; index < tokens.Length; ++index)
            result[index] = ParseInt(tokens[index]);
        return result;
    }

    private static Vector2f ParseVector2(string value)
    {
        var values = ParseFloatTokens(value);
        return values.Length < 2 ? Vector2f.Zero : new Vector2f(values[0], values[1]);
    }

    private static Vector3f ParseVector3(string value)
    {
        var values = ParseFloatTokens(value);
        return values.Length < 3 ? Vector3f.Zero : new Vector3f(values[0], values[1], values[2]);
    }

    private static Vector2d ParseDoubleVector2(string value)
    {
        var values = ParseFloatTokens(value);
        return values.Length < 2 ? Vector2d.Zero : new Vector2d(values[0], values[1]);
    }

    private static Vector3d ParseDoubleVector3(string value)
    {
        var values = ParseFloatTokens(value);
        return values.Length < 3 ? Vector3d.Zero : new Vector3d(values[0], values[1], values[2]);
    }

    private static Vector4f ParseVector4(string value)
    {
        var values = ParseFloatTokens(value);
        return values.Length < 4 ? Vector4f.Zero : new Vector4f(values[0], values[1], values[2], values[3]);
    }

    private static Quaternion ParseQuaternion(string value)
    {
        var values = ParseFloatTokens(value);
        if (values.Length < 3)
            return Quaternion.IDENTITY;
        if (values.Length < 4)
            return new Quaternion(values[0], values[1], values[2]);
        return new Quaternion(values[0], values[1], values[2], values[3]);
    }

    private static Color ParseColor(string value)
    {
        var values = ParseFloatTokens(value);
        return values.Length < 3 ? new Color() : new Color(values[0], values[1], values[2], values.Length > 3 ? values[3] : 1.0f);
    }

    private static Rect ParseRect(string value)
    {
        var values = ParseFloatTokens(value);
        return values.Length < 4 ? Rect.Zero : new Rect(values[0], values[1], values[2], values[3]);
    }

    private static IntRect ParseIntRect(string value)
    {
        var values = ParseIntTokens(value);
        return values.Length < 4 ? IntRect.Zero : new IntRect(values[0], values[1], values[2], values[3]);
    }

    private static Vector2i ParseIntVector2(string value)
    {
        var values = ParseIntTokens(value);
        return values.Length < 2 ? Vector2i.Zero : new Vector2i(values[0], values[1]);
    }

    private static Vector3i ParseIntVector3(string value)
    {
        var values = ParseIntTokens(value);
        return values.Length < 3 ? Vector3i.Zero : new Vector3i(values[0], values[1], values[2]);
    }

    private Variant LerpIntRect(Variant rhs, float t)
    {
        var s = 1.0f - t;
        var left = GetIntRect();
        var right = rhs.GetIntRect();
        return new Variant(new IntRect(
            unchecked((int)(left.Left * s + right.Left * t)),
            unchecked((int)(left.Top * s + right.Top * t)),
            unchecked((int)(left.Right * s + right.Right * t)),
            unchecked((int)(left.Bottom * s + right.Bottom * t))));
    }

    private Variant LerpIntVector2(Variant rhs, float t)
    {
        var s = 1.0f - t;
        var left = GetIntVector2();
        var right = rhs.GetIntVector2();
        return new Variant(new Vector2i(
            unchecked((int)(left.X * s + right.X * t)),
            unchecked((int)(left.Y * s + right.Y * t))));
    }

    private Variant LerpIntVector3(Variant rhs, float t)
    {
        var s = 1.0f - t;
        var left = GetIntVector3();
        var right = rhs.GetIntVector3();
        return new Variant(new Vector3i(
            unchecked((int)(left.X * s + right.X * t)),
            unchecked((int)(left.Y * s + right.Y * t)),
            unchecked((int)(left.Z * s + right.Z * t))));
    }

    private static Matrix3 ParseMatrix3(string value)
    {
        var values = ParseFloatTokens(value);
        return values.Length < 9
            ? default
            : new Matrix3(values[0], values[1], values[2], values[3], values[4], values[5], values[6], values[7], values[8]);
    }

    private static Matrix3x4 ParseMatrix3x4(string value)
    {
        var values = ParseFloatTokens(value);
        return values.Length < 12
            ? default
            : new Matrix3x4(
                values[0], values[1], values[2], values[3],
                values[4], values[5], values[6], values[7],
                values[8], values[9], values[10], values[11]);
    }

    private static Matrix4 ParseMatrix4(string value)
    {
        var values = ParseFloatTokens(value);
        return values.Length < 16
            ? default
            : new Matrix4(
                values[0], values[1], values[2], values[3],
                values[4], values[5], values[6], values[7],
                values[8], values[9], values[10], values[11],
                values[12], values[13], values[14], values[15]);
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

    private static bool SequenceEqual<T>(IReadOnlyList<T> lhs, IReadOnlyList<T> rhs)
    {
        return ManagedSequence.Equal(lhs, rhs);
    }

    private static bool AllNullOrEmpty(IReadOnlyList<string> names)
    {
        for (var i = 0; i < names.Count; ++i)
        {
            if (!string.IsNullOrEmpty(names[i]))
                return false;
        }

        return true;
    }
}

public sealed class VariantMap : Dictionary<StringHash, Variant>, IDisposable
{
    public VariantMap()
    {
    }

    public VariantMap(IDictionary<StringHash, Variant> dictionary)
        : base(dictionary.ToDictionary(static pair => pair.Key, static pair => pair.Value.Clone()))
    {
    }

    public void Dispose()
    {
        foreach (var value in Values)
        {
            var copy = value;
            copy.Dispose();
        }
        Clear();
    }
}

public sealed class VariantVector : List<Variant>, IDisposable
{
    public VariantVector()
    {
    }

    public VariantVector(IEnumerable<Variant> collection)
        : base(collection.Select(static value => value.Clone()))
    {
    }

    public void Dispose()
    {
        for (var i = Count - 1; i >= 0; --i)
        {
            var value = this[i];
            value.Dispose();
        }
        Clear();
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

public sealed class StringVariantMap : Dictionary<string, Variant>, IDisposable
{
    public StringVariantMap()
        : base(StringComparer.Ordinal)
    {
    }

    public StringVariantMap(IDictionary<string, Variant> dictionary)
        : base(dictionary.ToDictionary(static pair => pair.Key, static pair => pair.Value.Clone()), StringComparer.Ordinal)
    {
    }

    public void Dispose()
    {
        foreach (var value in Values)
        {
            var copy = value;
            copy.Dispose();
        }
        Clear();
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
            VectorBuffer => VariantType.Buffer,
            IntPtr => VariantType.VoidPtr,
            ResourceRef => VariantType.ResourceRef,
            ResourceRefList => VariantType.ResourceRefList,
            Matrix3 => VariantType.Matrix3,
            Matrix3x4 => VariantType.Matrix3x4,
            Matrix4 => VariantType.Matrix4,
            VariantVector => VariantType.VariantVector,
            StringVector => VariantType.StringVector,
            VariantMap => VariantType.VariantMap,
            StringVariantMap => VariantType.StringVariantMap,
            VariantCurve => VariantType.VariantCurve,
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
            VariantType.VoidPtr => typeof(IntPtr),
            VariantType.ResourceRef => typeof(ResourceRef),
            VariantType.ResourceRefList => typeof(ResourceRefList),
            VariantType.Matrix3 => typeof(Matrix3),
            VariantType.Matrix3x4 => typeof(Matrix3x4),
            VariantType.Matrix4 => typeof(Matrix4),
            VariantType.VariantVector => typeof(VariantVector),
            VariantType.StringVector => typeof(StringVector),
            VariantType.VariantMap => typeof(VariantMap),
            VariantType.StringVariantMap => typeof(StringVariantMap),
            VariantType.VariantCurve => typeof(VariantCurve),
            VariantType.String => typeof(string),
            VariantType.CString => typeof(string),
            _ => typeof(object)
        };
    }
}
