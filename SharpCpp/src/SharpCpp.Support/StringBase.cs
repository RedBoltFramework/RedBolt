using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace SharpCpp.Support;

/// <summary>
/// Managed UTF-8 string bridge that wraps a native std::string.
/// Extends <see cref="NativeObject"/> and implements <see cref="IReadOnlyList{T}"/> of <see cref="char"/>
/// and <see cref="IEquatable{T}"/> of <see cref="string"/>.
/// </summary>
public abstract class StringBase : NativeObject, IReadOnlyList<char>, IEquatable<string>
{
    /// <summary>
    /// Initializes a new instance wrapping the given native std::string handle.
    /// </summary>
    /// <param name="handle">Native handle to the std::string.</param>
    /// <param name="ownsHandle"><see langword="true"/> if this instance owns the native handle; otherwise <see langword="false"/>.</param>
    protected StringBase(nint handle, bool ownsHandle)
        : base(handle, ownsHandle)
    {
    }

    /// <summary>
    /// Initializes a new instance and assigns the given initial value.
    /// </summary>
    /// <param name="handle">Native handle to the std::string.</param>
    /// <param name="ownsHandle"><see langword="true"/> if this instance owns the native handle; otherwise <see langword="false"/>.</param>
    /// <param name="value">Initial string value to assign, or <see langword="null"/> to leave the native string empty.</param>
    protected StringBase(nint handle, bool ownsHandle, string? value)
        : base(handle, ownsHandle)
    {
        Assign(value);
    }

    /// <summary>
    /// When overridden in a derived class, gets the length of the native string.
    /// </summary>
    /// <returns>The length of the native string in bytes.</returns>
    protected virtual ulong GetCountCore() => throw new NotImplementedException();

    /// <summary>
    /// When overridden in a derived class, gets a borrowed pointer to the native string's UTF-8 bytes.
    /// </summary>
    /// <returns>A borrowed pointer that remains valid until the native string is mutated.</returns>
    protected virtual nint GetDataCore() => 0;

    /// <summary>
    /// When overridden in a derived class, clears the native string contents.
    /// </summary>
    protected virtual void ClearCore() => throw new NotImplementedException();

    /// <summary>
    /// When overridden in a derived class, reads the UTF-8 byte at the given index from the native string.
    /// </summary>
    /// <param name="index">The zero-based byte index.</param>
    /// <returns>The UTF-8 byte at the specified index.</returns>
    protected virtual byte GetAtCore(ulong index) => throw new NotImplementedException();

    /// <summary>
    /// When overridden in a derived class, appends a UTF-8 byte to the native string.
    /// </summary>
    /// <param name="value">The UTF-8 byte to append.</param>
    protected virtual void AddCore(byte value) => throw new NotImplementedException();

    /// <summary>
    /// Replaces the native string contents with the UTF-8 encoding of the given managed string.
    /// </summary>
    /// <param name="value">The managed string whose UTF-8 encoding will replace the native contents.
    /// Pass <see langword="null"/> to clear the native string.</param>
    public void Assign(string? value)
    {
        ClearCore();
        foreach (var utf8Byte in Encoding.UTF8.GetBytes(value ?? string.Empty))
            AddCore(utf8Byte);
    }

    /// <summary>
    /// Replaces the native string contents from a UTF-8 byte span.
    /// </summary>
    /// <param name="value">A read-only span of UTF-8 bytes to copy into the native string.</param>
    public void Assign(ReadOnlySpan<byte> value)
    {
        ClearCore();
        foreach (var utf8Byte in value)
            AddCore(utf8Byte);
    }

    /// <summary>
    /// Copies the native string to a managed byte array.
    /// </summary>
    /// <returns>A new byte array containing the UTF-8 bytes of the native string.</returns>
    public byte[] ToUtf8Bytes()
    {
        var span = AsUtf8Span();
        return span.IsEmpty ? Array.Empty<byte>() : span.ToArray();
    }

    private byte[] CopyToUtf8Bytes()
    {
        var byteCount = checked((int)GetCountCore());
        if (byteCount == 0) return Array.Empty<byte>();
        var buffer = new byte[byteCount];
        for (var index = 0; index < buffer.Length; index++)
            buffer[index] = GetAtCore((ulong)index);
        return buffer;
    }

    /// <summary>
    /// Returns a read-only span over the native string's UTF-8 bytes when the generated wrapper exposes native storage.
    /// </summary>
    /// <returns>A read-only span of UTF-8 bytes. The span remains valid until this string is mutated.</returns>
    public unsafe ReadOnlySpan<byte> AsUtf8Span()
    {
        var byteCount = checked((int)GetCountCore());
        if (byteCount == 0)
        {
            return ReadOnlySpan<byte>.Empty;
        }

        var data = GetDataCore();
        return data == 0
            ? CopyToUtf8Bytes()
            : new ReadOnlySpan<byte>((void*)data, byteCount);
    }

    /// <summary>
    /// Gets the character count of the string.
    /// </summary>
    public int Count => ToString().Length;

    /// <summary>
    /// Gets the character at the given index.
    /// </summary>
    /// <param name="index">The zero-based character index.</param>
    /// <returns>The character at the specified index.</returns>
    public char this[int index] => ToString()[index];

    /// <summary>
    /// Determines whether this string equals the given managed string.
    /// </summary>
    /// <param name="other">The managed string to compare with.</param>
    /// <returns><see langword="true"/> if the native string content equals the given string; otherwise <see langword="false"/>.</returns>
    public bool Equals(string? other)
        => string.Equals(ToString(), other, StringComparison.Ordinal);

    /// <summary>
    /// Determines whether this instance equals another object.
    /// Handles both <see cref="StringBase"/> and <see cref="string"/> comparisons.
    /// </summary>
    /// <param name="obj">The object to compare with this instance.</param>
    /// <returns><see langword="true"/> if <paramref name="obj"/> is a <see cref="StringBase"/> or <see cref="string"/>
    /// with equal content; otherwise <see langword="false"/>.</returns>
    public override bool Equals(object? obj) => obj switch
    {
        StringBase other => Equals(other.ToString()),
        string other => Equals(other),
        _ => false
    };

    /// <summary>
    /// Returns the hash code for this string.
    /// </summary>
    /// <returns>A 32-bit signed hash code based on the ordinal value of the decoded string.</returns>
    public override int GetHashCode()
        => StringComparer.Ordinal.GetHashCode(ToString());

    /// <summary>
    /// Decodes the native UTF-8 string to a managed string.
    /// </summary>
    /// <returns>A managed string containing the decoded UTF-8 content.</returns>
    public override string ToString()
    {
        var buffer = ToUtf8Bytes();
        return buffer.Length == 0 ? string.Empty : Encoding.UTF8.GetString(buffer);
    }

    /// <summary>
    /// Returns an enumerator over the characters of this string.
    /// </summary>
    /// <returns>An enumerator that can be used to iterate through the characters.</returns>
    public IEnumerator<char> GetEnumerator()
        => ((IEnumerable<char>)ToString()).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
