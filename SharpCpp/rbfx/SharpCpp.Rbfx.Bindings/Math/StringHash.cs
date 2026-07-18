using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Text;

namespace Urho3D.Math;

[Serializable]
[StructLayout(LayoutKind.Sequential)]
[NativeMarshalling(typeof(StringHashMarshaller))]
public readonly struct StringHash : IEquatable<StringHash>
{
    public StringHash(uint hash)
    {
        Hash = hash;
    }

    public StringHash(string value)
    {
        Hash = Calculate(value);
    }

    public StringHash(global::System.Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        Hash = Calculate(type.Name);
    }

    public uint Hash { get; }

    public static implicit operator StringHash(uint value)
    {
        return new StringHash(value);
    }

    public static implicit operator StringHash(string value)
    {
        return new StringHash(value);
    }

    public static implicit operator StringHash(global::System.Type type)
    {
        return new StringHash(type);
    }

    public static uint Calculate(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var bytes = Encoding.UTF8.GetBytes(value);
        uint result = 2166136261u;

        foreach (var currentByte in bytes)
        {
            result = (result * 16777619u) ^ currentByte;
        }

        return result;
    }

    public static StringHash GetManagedInstance(uint source)
    {
        return StringHashMarshaller.ConvertToManaged(source);
    }

    public static uint GetNativeInstance(StringHash source)
    {
        return StringHashMarshaller.ConvertToUnmanaged(source);
    }

    public bool Equals(StringHash other)
    {
        return Hash == other.Hash;
    }

    public override bool Equals(object? obj)
    {
        return obj is StringHash other && Equals(other);
    }

    public override int GetHashCode()
    {
        return unchecked((int)Hash);
    }

    public override string ToString()
    {
        return $"{Hash:X8}";
    }

    public static bool operator ==(StringHash left, StringHash right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(StringHash left, StringHash right)
    {
        return !left.Equals(right);
    }
}

[CustomMarshaller(typeof(StringHash), MarshalMode.Default, typeof(StringHashMarshaller))]
public static class StringHashMarshaller
{
    public static uint ConvertToUnmanaged(StringHash value)
    {
        return value.Hash;
    }

    public static StringHash ConvertToManaged(uint value)
    {
        return new StringHash(value);
    }
}
