using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace RedBolt;

public sealed class CString : IEquatable<CString>, IEquatable<string?>, IDisposable
{
    private static readonly Encoding Utf8 = Encoding.UTF8;

    private IntPtr _bytes;
    private int _byteLength;
    private int _capacity;
    private bool _disposed;

    public static CString Empty { get; } = new(string.Empty);

    public CString()
    {
        _bytes = Marshal.AllocHGlobal(1);
        _capacity = 1;
    }

    public CString(string value)
    {
        _bytes = Marshal.AllocHGlobal(1);
        _capacity = 1;
        Set(value);
    }

    public IntPtr Pointer
    {
        get
        {
            ThrowIfDisposed();
            return _bytes;
        }
    }

    public ReadOnlySpan<byte> Bytes
    {
        get
        {
            ThrowIfDisposed();
            unsafe { return new ReadOnlySpan<byte>((void*)_bytes, _byteLength + 1); }
        }
    }

    public int Length
    {
        get
        {
            ThrowIfDisposed();
            return Utf8.GetCharCount(Bytes[.._byteLength]);
        }
    }

    public int ByteLength
    {
        get
        {
            ThrowIfDisposed();
            return _byteLength;
        }
    }

    public void Clear()
    {
        ThrowIfDisposed();
        _byteLength = 0;
        Marshal.WriteByte(_bytes, 0, 0);
    }

    public string ConvertToString()
    {
        ThrowIfDisposed();
        return Utf8.GetString(Bytes[.._byteLength]);
    }

    public static CString Format(string format, ReadOnlySpan<object?> args)
    {
        var value = new CString();
        value.FormatInPlace(format, args);
        return value;
    }

    public static CString Format(string format, params object?[] args)
    {
        ArgumentNullException.ThrowIfNull(format);
        var value = new CString();
        value.FormatInPlace(format, args);
        return value;
    }

    public void FormatInPlace(string format, ReadOnlySpan<object?> args)
    {
        ArgumentNullException.ThrowIfNull(format);
        ThrowIfDisposed();

        Clear();

        if (format.Length == 0)
            return;

        var formatSpan = format.AsSpan();
        var segmentStart = 0;
        for (var index = 0; index < formatSpan.Length; ++index)
        {
            var current = formatSpan[index];
            if (current == '{')
            {
                AppendText(formatSpan.Slice(segmentStart, index - segmentStart));

                if (index + 1 < formatSpan.Length && formatSpan[index + 1] == '{')
                {
                    AppendText('{');
                    segmentStart = index + 2;
                    ++index;
                    continue;
                }

                ParsePlaceholder(formatSpan, index + 1, args, out var consumed);
                segmentStart = consumed;
                index = consumed - 1;
                continue;
            }

            if (current == '}')
            {
                AppendText(formatSpan.Slice(segmentStart, index - segmentStart));

                if (index + 1 < formatSpan.Length && formatSpan[index + 1] == '}')
                {
                    AppendText('}');
                    segmentStart = index + 2;
                    ++index;
                    continue;
                }

                throw new FormatException("Input string was not in a correct format.");
            }
        }

        if (segmentStart < formatSpan.Length)
            AppendText(formatSpan[segmentStart..]);
    }

    public void FormatInPlace(string format, params object?[] args)
    {
        ArgumentNullException.ThrowIfNull(format);
        FormatInPlace(format, (ReadOnlySpan<object?>)args);
    }

    public void FormatInPlace([InterpolatedStringHandlerArgument("")] CStringInterpolatedStringHandler handler)
    {
    }

    public bool Equals(CString? other)
    {
        if (other is null)
            return false;

        ThrowIfDisposed();
        return _byteLength == other._byteLength && Bytes[.._byteLength].SequenceEqual(other.Bytes[..other._byteLength]);
    }

    public bool Equals(string? other)
    {
        ThrowIfDisposed();
        return other is not null && EqualsUtf8BytesString(Bytes[.._byteLength], other);
    }

    public override bool Equals(object? obj)
    {
        return obj is CString cstring
            ? Equals(cstring)
            : obj is string value && Equals(value);
    }

    public override int GetHashCode()
    {
        ThrowIfDisposed();

        var bytes = Bytes;
        var hash = 0x811C9DC5u;
        for (var i = 0; i < _byteLength; ++i)
            hash = (hash ^ bytes[i]) * 16777619;

        return unchecked((int)hash);
    }

    public override string ToString()
    {
        return ConvertToString();
    }

    public static implicit operator CString(string value) => new(value);

    public static bool operator ==(CString? left, CString? right)
    {
        if (left is null && right is null)
            return true;
        if (left is null || right is null)
            return false;

        return left.Equals(right);
    }

    public static bool operator !=(CString? left, CString? right)
    {
        return !(left == right);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        if (_bytes != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_bytes);
            _bytes = IntPtr.Zero;
        }

        _capacity = 0;
        _byteLength = 0;
        _disposed = true;
    }

    private void Set(string value)
    {
        ThrowIfDisposed();

        value ??= string.Empty;
        var rawByteLength = Utf8.GetByteCount(value);
        EnsureCapacity(rawByteLength + 1);
        unsafe { Utf8.GetBytes(value.AsSpan(), new Span<byte>((void*)_bytes, rawByteLength)); }
        _byteLength = rawByteLength;
        Marshal.WriteByte(_bytes, _byteLength, 0);
    }

    private void ParsePlaceholder(ReadOnlySpan<char> format, int index, ReadOnlySpan<object?> args, out int consumed)
    {
        if (index >= format.Length)
            throw new FormatException("Input string was not in a correct format.");

        var position = index;
        if (position >= format.Length || format[position] < '0' || format[position] > '9')
            throw new FormatException("Input string was not in a correct format.");

        var argumentIndex = 0;
        while (position < format.Length)
        {
            var current = format[position];
            if (current < '0' || current > '9')
                break;

            argumentIndex = (argumentIndex * 10) + (current - '0');
            ++position;
        }

        if (position >= format.Length)
            throw new FormatException("Input string was not in a correct format.");

        if (argumentIndex >= args.Length)
            throw new FormatException($"Index ({argumentIndex}) must be non-negative and less than the size of the argument list.");

        if (format[position] == ',')
        {
            ++position;
            if (position >= format.Length)
                throw new FormatException("Input string was not in a correct format.");

            var hasSign = format[position] == '+' || format[position] == '-';
            if (hasSign)
                ++position;

            while (position < format.Length && format[position] >= '0' && format[position] <= '9')
                ++position;

            if (position >= format.Length)
                throw new FormatException("Input string was not in a correct format.");
        }

        ReadOnlySpan<char> argFormat = default;
        if (format[position] == ':')
        {
            ++position;
            var formatSpecStart = position;
            while (position < format.Length && format[position] != '}')
                ++position;

            if (position >= format.Length)
                throw new FormatException("Input string was not in a correct format.");

            argFormat = format.Slice(formatSpecStart, position - formatSpecStart);
        }

        if (position >= format.Length || format[position] != '}')
            throw new FormatException("Input string was not in a correct format.");

        AppendArg(args[argumentIndex], argFormat);
        consumed = position + 1;
    }

    private void AppendArg(object? value, ReadOnlySpan<char> format)
    {
        if (value is null)
            return;

        switch (value)
        {
            case string text:
                AppendText(text.AsSpan());
                return;
            case CString cstring:
                AppendBytes(cstring.Bytes[..cstring._byteLength]);
                return;
            case char text:
                AppendText(text);
                return;
            case ISpanFormattable spanFormattable:
                AppendSpanFormattable(spanFormattable, format);
                return;
            default:
                throw new FormatException($"Formatting type '{value.GetType().FullName}' would allocate. Convert it explicitly before passing it to CString.");
        }
    }

    private void AppendSpanFormattable<T>(T value, ReadOnlySpan<char> format)
        where T : ISpanFormattable
    {
        Span<char> chars = stackalloc char[256];

        if (!value.TryFormat(chars, out var written, format, CultureInfo.InvariantCulture))
        {
            chars = stackalloc char[512];
            if (!value.TryFormat(chars, out written, format, CultureInfo.InvariantCulture))
                throw new FormatException($"The argument value '{value}' cannot be formatted with span-based formatting.");
        }

        AppendText(chars[..written]);
    }

    private void AppendBytes(ReadOnlySpan<byte> value)
    {
        if (value.IsEmpty)
            return;

        EnsureCapacity(_byteLength + value.Length + 1);
        unsafe { value.CopyTo(new Span<byte>((void*)(_bytes + _byteLength), value.Length)); }
        _byteLength += value.Length;
        Marshal.WriteByte(_bytes, _byteLength, 0);
    }

    private void AppendText(ReadOnlySpan<char> text)
    {
        if (text.IsEmpty)
            return;

        var requiredLength = Utf8.GetByteCount(text);
        EnsureCapacity(_byteLength + requiredLength + 1);
        unsafe { Utf8.GetBytes(text, new Span<byte>((void*)(_bytes + _byteLength), requiredLength)); }
        _byteLength += requiredLength;
        Marshal.WriteByte(_bytes, _byteLength, 0);
    }

    private void AppendText(char value)
    {
        Span<char> chars = stackalloc char[1];
        chars[0] = value;
        AppendText(chars);
    }

    private void EnsureCapacity(int requiredCapacity)
    {
        if (_capacity >= requiredCapacity)
            return;

        var newCapacity = _capacity == 0 ? 1 : _capacity;
        while (newCapacity < requiredCapacity)
            newCapacity *= 2;

        _bytes = Marshal.ReAllocHGlobal(_bytes, (IntPtr)newCapacity);
        _capacity = newCapacity;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(CString));
    }

    private static bool EqualsUtf8BytesString(ReadOnlySpan<byte> valueBytes, string text)
    {
        var charSpan = text.AsSpan();
        if (valueBytes.Length == 0)
            return charSpan.IsEmpty;

        var expectedByteLength = Utf8.GetByteCount(charSpan);
        if (expectedByteLength != valueBytes.Length)
            return false;

        var byteOffset = 0;
        var charOffset = 0;

        Span<byte> expected = stackalloc byte[4];

        while (charOffset < charSpan.Length)
        {
            var rune = (uint)charSpan[charOffset++];
            if (rune is >= 0xD800u and <= 0xDBFFu)
            {
                if (charOffset < charSpan.Length && (uint)charSpan[charOffset] is >= 0xDC00u and <= 0xDFFFu)
                {
                    rune = (uint)char.ConvertToUtf32((char)rune, charSpan[charOffset++]);
                }
                else
                {
                    rune = 0xFFFD;
                }
            }
            else if (rune is >= 0xDC00u and <= 0xDFFFu)
            {
                rune = 0xFFFD;
            }

            var encoded = EncodeUtf8(rune, expected);
            if (encoded < 0)
                return false;

            if (byteOffset + encoded > valueBytes.Length)
                return false;

            if (!valueBytes.Slice(byteOffset, encoded).SequenceEqual(expected[..encoded]))
                return false;

            byteOffset += encoded;
        }

        return byteOffset == valueBytes.Length;
    }

    private static int EncodeUtf8(uint rune, Span<byte> destination)
    {
        if (rune <= 0x7Fu)
        {
            destination[0] = (byte)rune;
            return 1;
        }

        if (rune <= 0x7FFu)
        {
            destination[0] = (byte)(0xC0u | (rune >> 6));
            destination[1] = (byte)(0x80u | (rune & 0x3Fu));
            return 2;
        }

        if (rune <= 0xFFFFu)
        {
            destination[0] = (byte)(0xE0u | (rune >> 12));
            destination[1] = (byte)(0x80u | ((rune >> 6) & 0x3Fu));
            destination[2] = (byte)(0x80u | (rune & 0x3Fu));
            return 3;
        }

        if (rune <= 0x10FFFFu)
        {
            destination[0] = (byte)(0xF0u | (rune >> 18));
            destination[1] = (byte)(0x80u | ((rune >> 12) & 0x3Fu));
            destination[2] = (byte)(0x80u | ((rune >> 6) & 0x3Fu));
            destination[3] = (byte)(0x80u | (rune & 0x3Fu));
            return 4;
        }

        return -1;
    }

    [InterpolatedStringHandler]
    public ref struct CStringInterpolatedStringHandler
    {
        private readonly CString _target;

        public CStringInterpolatedStringHandler(int literalLength, int formattedCount, CString target)
        {
            ArgumentNullException.ThrowIfNull(target);

            this._target = target;
            this._target.Clear();
        }

        public void AppendLiteral(string value)
        {
            _target.AppendText(value.AsSpan());
        }

        public void AppendFormatted(string? value)
        {
            if (!string.IsNullOrEmpty(value))
                _target.AppendText(value.AsSpan());
        }

        public void AppendFormatted(ReadOnlySpan<char> value)
        {
            _target.AppendText(value);
        }

        public void AppendFormatted(char value)
        {
            _target.AppendText(value);
        }

        public void AppendFormatted(CString? value)
        {
            if (value is null)
                return;

            _target.AppendBytes(value.Bytes[..value._byteLength]);
        }

        public void AppendFormatted<T>(T value)
            where T : ISpanFormattable
        {
            _target.AppendSpanFormattable(value, default);
        }

        public void AppendFormatted<T>(T value, string? format)
            where T : ISpanFormattable
        {
            _target.AppendSpanFormattable(value, format.AsSpan());
        }
    }
}
