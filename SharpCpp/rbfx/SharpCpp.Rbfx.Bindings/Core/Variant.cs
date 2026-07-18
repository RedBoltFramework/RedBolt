namespace Urho3D.Core;

public partial class Variant
{
    public VariantType Type => GetType();

    public static implicit operator int(Variant value)
        => value.Int;

    public static implicit operator Variant(int value)
        => new(value);

    public static implicit operator long(Variant value)
        => value.Int64;

    public static implicit operator Variant(long value)
        => new(value);

    public static implicit operator uint(Variant value)
        => value.UInt;

    public static implicit operator Variant(uint value)
        => new(value);

    public static implicit operator ulong(Variant value)
        => value.UInt64;

    public static implicit operator Variant(ulong value)
        => new((ImU64)value);

    public static implicit operator global::Urho3D.Math.StringHash(Variant value)
        => value.StringHash;

    public static implicit operator Variant(global::Urho3D.Math.StringHash value)
        => new(value);

    public static implicit operator bool(Variant value)
        => value.Bool;

    public static implicit operator Variant(bool value)
        => new(value);

    public static implicit operator float(Variant value)
        => value.Float;

    public static implicit operator Variant(float value)
        => new(value);

    public static implicit operator double(Variant value)
        => value.Double;

    public static implicit operator Variant(double value)
        => new(value);

    public static implicit operator Vector2(Variant value)
        => value.Vector2;

    public static implicit operator Variant(Vector2 value)
        => new(value);

    public static implicit operator Vector3(Variant value)
        => value.Vector3;

    public static implicit operator Variant(Vector3 value)
        => new(value);

    public static implicit operator DoubleVector2(Variant value)
        => value.DoubleVector2;

    public static implicit operator Variant(DoubleVector2 value)
        => new(value);

    public static implicit operator DoubleVector3(Variant value)
        => value.DoubleVector3;

    public static implicit operator Variant(DoubleVector3 value)
        => new(value);

    public static implicit operator global::Urho3D.Math.Vector4(Variant value)
        => value.Vector4;

    public static implicit operator Variant(global::Urho3D.Math.Vector4 value)
        => new(value);

    public static implicit operator global::Urho3D.Math.Quaternion(Variant value)
        => value.Quaternion;

    public static implicit operator Variant(global::Urho3D.Math.Quaternion value)
        => new(value);

    public static implicit operator global::Urho3D.Math.Color(Variant value)
        => value.Color;

    public static implicit operator Variant(global::Urho3D.Math.Color value)
        => new(value);

    public static implicit operator Variant(string? value)
    {
        using var text = new global::Ea.String(value ?? string.Empty);
        return new Variant(text);
    }

    public static implicit operator ByteVector(Variant value)
        => value.Buffer;

    public static implicit operator global::System.IntPtr(Variant value)
        => value.VoidPtr;

    public static implicit operator Variant(global::System.IntPtr value)
        => new(value);

    public static implicit operator ResourceRef(Variant value)
        => value.ResourceRef;

    public static implicit operator Variant(ResourceRef value)
        => new(value);

    public static implicit operator ResourceRefList(Variant value)
        => value.ResourceRefList;

    public static implicit operator Variant(ResourceRefList value)
        => new(value);

    public static implicit operator VariantVector(Variant value)
        => value.VariantVector;

    public static implicit operator Variant(VariantVector value)
        => new(value);

    public static implicit operator VariantMap(Variant value)
        => value.VariantMap;

    public static implicit operator Variant(VariantMap value)
        => new(value);

    public static implicit operator StringVector(Variant value)
        => value.StringVector;

    public static implicit operator Variant(StringVector value)
        => new(value);

    public static implicit operator global::Ea.UnorderedMap<global::Ea.String, Variant>(Variant value)
        => value.StringVariantMap;

    public static implicit operator Variant(global::Ea.UnorderedMap<global::Ea.String, Variant> value)
        => new(value);

    public static implicit operator global::Urho3D.Math.Rect(Variant value)
        => value.Rect;

    public static implicit operator Variant(global::Urho3D.Math.Rect value)
        => new(value);

    public static implicit operator global::Urho3D.Math.IntRect(Variant value)
        => value.IntRect;

    public static implicit operator Variant(global::Urho3D.Math.IntRect value)
        => new(value);

    public static implicit operator IntVector2(Variant value)
        => value.IntVector2;

    public static implicit operator Variant(IntVector2 value)
        => new(value);

    public static implicit operator IntVector3(Variant value)
        => value.IntVector3;

    public static implicit operator Variant(IntVector3 value)
        => new(value);

    public static implicit operator Variant(global::Urho3D.Container.RefCounted value)
        => new(value);

    public static implicit operator global::Urho3D.Math.Matrix3(Variant value)
        => value.Matrix3;

    public static implicit operator Variant(global::Urho3D.Math.Matrix3 value)
        => new(value);

    public static implicit operator global::Urho3D.Math.Matrix3x4(Variant value)
        => value.Matrix3x4;

    public static implicit operator Variant(global::Urho3D.Math.Matrix3x4 value)
        => new(value);

    public static implicit operator global::Urho3D.Math.Matrix4(Variant value)
        => value.Matrix4;

    public static implicit operator Variant(global::Urho3D.Math.Matrix4 value)
        => new(value);

    public static implicit operator VariantCurve(Variant value)
        => value.VariantCurve;

    public static implicit operator Variant(VariantCurve value)
        => new(value);

    public static VariantType GetVariantType(global::System.Type type)
    {
        if (type == typeof(sbyte) || type == typeof(short) || type == typeof(int))
            return VariantType.VarInt;
        if (type == typeof(bool))
            return VariantType.VarBool;
        if (type == typeof(float))
            return VariantType.VarFloat;
        if (type == typeof(Vector2))
            return VariantType.VarVector2;
        if (type == typeof(Vector3))
            return VariantType.VarVector3;
        if (type == typeof(global::Urho3D.Math.Vector4))
            return VariantType.VarVector4;
        if (type == typeof(DoubleVector2))
            return VariantType.VarDoublevector2;
        if (type == typeof(DoubleVector3))
            return VariantType.VarDoublevector3;
        if (type == typeof(global::Urho3D.Math.Quaternion))
            return VariantType.VarQuaternion;
        if (type == typeof(global::Urho3D.Math.Color))
            return VariantType.VarColor;
        if (type == typeof(string) || type == typeof(global::Ea.String))
            return VariantType.VarString;
        if (type == typeof(ByteVector))
            return VariantType.VarBuffer;
        if (type == typeof(global::System.IntPtr))
            return VariantType.VarVoidptr;
        if (type == typeof(ResourceRef))
            return VariantType.VarResourceref;
        if (type == typeof(ResourceRefList))
            return VariantType.VarResourcereflist;
        if (type == typeof(VariantVector))
            return VariantType.VarVariantvector;
        if (type == typeof(VariantMap))
            return VariantType.VarVariantmap;
        if (type == typeof(StringVector))
            return VariantType.VarStringvector;
        if (type == typeof(global::Ea.UnorderedMap<global::Ea.String, Variant>))
            return VariantType.VarStringvariantmap;
        if (type == typeof(global::Urho3D.Math.IntRect))
            return VariantType.VarIntrect;
        if (type == typeof(IntVector2))
            return VariantType.VarIntvector2;
        if (type == typeof(global::Urho3D.Container.RefCounted))
            return VariantType.VarPtr;
        if (type == typeof(global::Urho3D.Math.Matrix3))
            return VariantType.VarMatrix3;
        if (type == typeof(global::Urho3D.Math.Matrix3x4))
            return VariantType.VarMatrix3x4;
        if (type == typeof(global::Urho3D.Math.Matrix4))
            return VariantType.VarMatrix4;
        if (type == typeof(double))
            return VariantType.VarDouble;
        if (type == typeof(global::Urho3D.Math.Rect))
            return VariantType.VarRect;
        if (type == typeof(IntVector3))
            return VariantType.VarIntvector3;
        if (type == typeof(long) || type == typeof(ulong))
            return VariantType.VarInt64;
        if (type == typeof(VariantCurve))
            return VariantType.VarVariantcurve;

        return VariantType.VarNone;
    }
}
