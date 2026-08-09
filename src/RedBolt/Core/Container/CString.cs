namespace RedBolt.Container;

using System.Buffers;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

using RedBolt.Input;

public sealed class CString : IEquatable<CString>, IEquatable<string?>, ICloneable, IDisposable
{
    [InlineArray(SmallBufferSize)]
    private struct SmallBuffer
    {
        public byte Element0;
    }

    private const int SmallBufferSize = 16;
    private const string InvalidFormatMessage = "Input string was not in a correct format.";

    private static readonly Encoding Utf8 = Encoding.UTF8;

    private SmallBuffer _data;
    private int _byteLength;
    private int _capacity;

    private bool UsesSmallBuffer => _capacity <= SmallBufferSize;
    private IntPtr HeapPointer
    {
        get => ReadHeapPointer();
        set => WriteHeapPointer(value);
    }
    private Span<byte> WritableStorage => GetStorage();
    private ReadOnlySpan<byte> Storage => GetStorage();
    private ReadOnlySpan<byte> TerminatedBytes => Storage[..(_byteLength + 1)];

    public static CString Empty { get; } = new(string.Empty);

    public CString()
    {
        InitializeSmallBuffer();
    }

    public CString(string value) : this()
    {
        Set(value);
    }

    public CString(ReadOnlySpan<byte> value) : this()
    {
        Set(value);
    }

    public IntPtr Pointer
    {
        get
        {
            if (UsesSmallBuffer)
                MoveToHeapStorage(SmallBufferSize + 1);

            return HeapPointer;
        }
    }

    public ReadOnlySpan<byte> Bytes => TerminatedBytes;

    public ReadOnlySpan<byte> ContentBytes => Storage[.._byteLength];

    public int Length => Utf8.GetCharCount(ContentBytes);

    public int ByteLength => _byteLength;

    public int Capacity => _capacity;

    public void Clear() => SetByteLength(0);

    public void Set(string value)
    {
        value ??= string.Empty;
        Set(value.AsSpan());
    }

    public void Set(ReadOnlySpan<char> value)
    {
        var rawByteLength = Utf8.GetByteCount(value);
        EnsureCapacity(rawByteLength + 1);
        Utf8.GetBytes(value, WritableStorage[..rawByteLength]);
        SetByteLength(rawByteLength);
    }

    public void Set(ReadOnlySpan<byte> value)
    {
        EnsureCapacity(value.Length + 1);
        value.CopyTo(WritableStorage);
        SetByteLength(value.Length);
    }

    public void Resize(int byteLength)
    {
        var previousByteLength = _byteLength;
        EnsureCapacity(byteLength + 1);
        if (byteLength > previousByteLength)
            WritableStorage.Slice(previousByteLength, byteLength - previousByteLength).Clear();
        SetByteLength(byteLength);
    }

    public string ConvertToString() => Utf8.GetString(ContentBytes);

    public static CString Format(string format, ReadOnlySpan<object?> args)
    {
        var value = new CString();
        try
        {
            value.FormatInPlace(format, args);
            return value;
        }
        catch
        {
            value.Dispose();
            throw;
        }
    }

    public static CString Format(string format, params object?[] args)
    {
        return Format(format, (ReadOnlySpan<object?>)args);
    }

    public void FormatInPlace(string format, ReadOnlySpan<object?> args)
    {
        ArgumentNullException.ThrowIfNull(format);

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

                throw InvalidFormat();
            }
        }

        if (segmentStart < formatSpan.Length)
            AppendText(formatSpan[segmentStart..]);
    }

    public void FormatInPlace(string format, params object?[] args)
    {
        FormatInPlace(format, (ReadOnlySpan<object?>)args);
    }

    public void FormatInPlace([InterpolatedStringHandlerArgument("")] CStringInterpolatedStringHandler handler)
    {
    }

    public bool Equals(CString? other)
    {
        if (other is null)
            return false;

        return _byteLength == other._byteLength && ContentBytes.SequenceEqual(other.ContentBytes);
    }

    public bool Equals(string? other)
    {
        return other is not null && EqualsUtf8BytesString(ContentBytes, other);
    }

    public bool Equals(ReadOnlySpan<byte> other) => ContentBytes.SequenceEqual(other);

    public override bool Equals(object? obj)
    {
        return obj is CString cstring
            ? Equals(cstring)
            : obj is string value && Equals(value);
    }

    public override int GetHashCode()
    {
        var bytes = ContentBytes;
        var hash = 0x811C9DC5u;
        for (var i = 0; i < bytes.Length; ++i)
            hash = (hash ^ bytes[i]) * 16777619;

        return unchecked((int)hash);
    }

    public override string ToString() => ConvertToString();

    public static implicit operator CString(string value) => new(value);

    public static bool operator ==(CString? left, CString? right)
    {
        return ReferenceEquals(left, right) || (left is not null && right is not null && left.Equals(right));
    }

    public static bool operator !=(CString? left, CString? right) => !(left == right);

    public object Clone()
    {
        var result = new CString();
        result.EnsureCapacity(_byteLength + 1);
        ContentBytes.CopyTo(result.WritableStorage);
        result.SetByteLength(_byteLength);
        return result;
    }

    public void Dispose()
    {
        if (!UsesSmallBuffer)
            Marshal.FreeHGlobal(HeapPointer);

        InitializeSmallBuffer();
    }

    private void ParsePlaceholder(ReadOnlySpan<char> format, int index, ReadOnlySpan<object?> args, out int consumed)
    {
        if (index >= format.Length)
            throw InvalidFormat();

        var position = index;
        if (position >= format.Length || !IsAsciiDigit(format[position]))
            throw InvalidFormat();

        var argumentIndex = 0;
        while (position < format.Length)
        {
            var current = format[position];
            if (!IsAsciiDigit(current))
                break;

            argumentIndex = (argumentIndex * 10) + (current - '0');
            ++position;
        }

        if (position >= format.Length)
            throw InvalidFormat();

        if (argumentIndex >= args.Length)
            throw new FormatException($"Index ({argumentIndex}) must be non-negative and less than the size of the argument list.");

        if (format[position] == ',')
        {
            ++position;
            if (position >= format.Length)
                throw InvalidFormat();

            var hasSign = format[position] == '+' || format[position] == '-';
            if (hasSign)
                ++position;

            while (position < format.Length && IsAsciiDigit(format[position]))
                ++position;

            if (position >= format.Length)
                throw InvalidFormat();
        }

        ReadOnlySpan<char> argFormat = default;
        if (format[position] == ':')
        {
            ++position;
            var formatSpecStart = position;
            while (position < format.Length && format[position] != '}')
                ++position;

            if (position >= format.Length)
                throw InvalidFormat();

            argFormat = format.Slice(formatSpecStart, position - formatSpecStart);
        }

        if (position >= format.Length || format[position] != '}')
            throw InvalidFormat();

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
                AppendBytes(cstring.ContentBytes);
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

        var appendedByteLength = _byteLength + value.Length;
        EnsureCapacity(appendedByteLength + 1);
        value.CopyTo(WritableStorage.Slice(_byteLength, value.Length));
        SetByteLength(appendedByteLength);
    }

    private void AppendText(ReadOnlySpan<char> text)
    {
        if (text.IsEmpty)
            return;

        var requiredLength = Utf8.GetByteCount(text);
        var appendedByteLength = _byteLength + requiredLength;
        EnsureCapacity(appendedByteLength + 1);
        Utf8.GetBytes(text, WritableStorage.Slice(_byteLength, requiredLength));
        SetByteLength(appendedByteLength);
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

        if (UsesSmallBuffer)
            MoveToHeapStorage(newCapacity);
        else
            HeapPointer = Marshal.ReAllocHGlobal(HeapPointer, (IntPtr)newCapacity);

        _capacity = newCapacity;
    }

    private void InitializeSmallBuffer()
    {
        _capacity = SmallBufferSize;
        SetByteLength(0);
    }

    private unsafe IntPtr ReadHeapPointer()
    {
        fixed (SmallBuffer* buffer = &_data)
            return *(IntPtr*)buffer;
    }

    private unsafe void WriteHeapPointer(IntPtr value)
    {
        fixed (SmallBuffer* buffer = &_data)
            *(IntPtr*)buffer = value;
    }

    private Span<byte> GetStorage()
    {
        if (UsesSmallBuffer)
            return _data;

        unsafe { return new Span<byte>((void*)HeapPointer, _capacity); }
    }

    private void MoveToHeapStorage(int newCapacity)
    {
        var newBuffer = Marshal.AllocHGlobal(newCapacity);
        unsafe { TerminatedBytes.CopyTo(new Span<byte>((void*)newBuffer, _byteLength + 1)); }
        HeapPointer = newBuffer;
        _capacity = newCapacity;
    }

    private void SetByteLength(int value)
    {
        _byteLength = value;
        WritableStorage[_byteLength] = 0;
    }

    private static bool EqualsUtf8BytesString(ReadOnlySpan<byte> valueBytes, string text)
    {
        var chars = text.AsSpan();
        var expectedByteLength = Utf8.GetByteCount(chars);
        if (expectedByteLength != valueBytes.Length)
            return false;

        var byteOffset = 0;
        Span<byte> expected = stackalloc byte[4];
        while (!chars.IsEmpty)
        {
            var status = Rune.DecodeFromUtf16(chars, out var rune, out var charsConsumed);
            if (status != OperationStatus.Done)
            {
                rune = Rune.ReplacementChar;
                charsConsumed = 1;
            }

            var encoded = rune.EncodeToUtf8(expected);
            if (!valueBytes.Slice(byteOffset, encoded).SequenceEqual(expected[..encoded]))
                return false;

            byteOffset += encoded;
            chars = chars[charsConsumed..];
        }

        return byteOffset == valueBytes.Length;
    }

    private static bool IsAsciiDigit(char value) => value is >= '0' and <= '9';

    private static FormatException InvalidFormat() => new(InvalidFormatMessage);

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

            _target.AppendBytes(value.ContentBytes);
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
