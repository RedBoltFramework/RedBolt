using System.Buffers;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace Ea;

public static class StringFormattingExtensions
{
    public static void Format(this String target, ref StringUtf8FormatHandler handler)
    {
        ArgumentNullException.ThrowIfNull(target);

        try
        {
            target.Assign(handler.WrittenSpan);
        }
        finally
        {
            handler.Dispose();
        }
    }

    public static void Format(
        this String target,
        IFormatProvider? provider,
        [InterpolatedStringHandlerArgument("provider")] ref StringUtf8FormatHandler handler)
    {
        ArgumentNullException.ThrowIfNull(target);
        _ = provider;

        try
        {
            target.Assign(handler.WrittenSpan);
        }
        finally
        {
            handler.Dispose();
        }
    }
}

[InterpolatedStringHandler]
public ref struct StringUtf8FormatHandler
{
    private const int InitialCapacity = 64;

    private readonly IFormatProvider? _formatProvider;
    private byte[] _buffer;
    private int _length;

    public StringUtf8FormatHandler(int literalLength, int formattedCount)
        : this(literalLength, formattedCount, CultureInfo.InvariantCulture)
    {
    }

    public StringUtf8FormatHandler(int literalLength, int formattedCount, IFormatProvider? formatProvider)
    {
        _formatProvider = formatProvider ?? CultureInfo.InvariantCulture;

        var estimatedLength = Encoding.UTF8.GetMaxByteCount(literalLength) + formattedCount * 16;
        _buffer = ArrayPool<byte>.Shared.Rent(Math.Max(InitialCapacity, estimatedLength));
        _length = 0;
    }

    public readonly ReadOnlySpan<byte> WrittenSpan => _buffer.AsSpan(0, _length);

    public void AppendLiteral(string value)
    {
        AppendUtf16(value);
    }

    public void AppendFormatted(string? value)
    {
        AppendUtf16(value);
    }

    public void AppendFormatted(string? value, string? format)
    {
        _ = format;
        AppendUtf16(value);
    }

    public void AppendFormatted(string? value, int alignment)
    {
        var start = _length;
        AppendUtf16(value);
        ApplyAlignment(start, alignment);
    }

    public void AppendFormatted(string? value, int alignment, string? format)
    {
        _ = format;
        var start = _length;
        AppendUtf16(value);
        ApplyAlignment(start, alignment);
    }

    public void AppendFormatted(ReadOnlySpan<byte> value)
    {
        AppendUtf8(value);
    }

    public void AppendFormatted<T>(T value)
    {
        AppendFormattedCore(value, format: null);
    }

    public void AppendFormatted<T>(T value, string? format)
    {
        AppendFormattedCore(value, format);
    }

    public void AppendFormatted<T>(T value, int alignment)
    {
        var start = _length;
        AppendFormattedCore(value, format: null);
        ApplyAlignment(start, alignment);
    }

    public void AppendFormatted<T>(T value, int alignment, string? format)
    {
        var start = _length;
        AppendFormattedCore(value, format);
        ApplyAlignment(start, alignment);
    }

    public void Dispose()
    {
        if (_buffer.Length == 0)
        {
            return;
        }

        ArrayPool<byte>.Shared.Return(_buffer, clearArray: true);
        _buffer = Array.Empty<byte>();
        _length = 0;
    }

    private void AppendFormattedCore<T>(T value, string? format)
    {
        switch (value)
        {
            case null:
                return;

            case IUtf8SpanFormattable utf8Formattable:
                AppendUtf8Formattable(utf8Formattable, format);
                return;

            case IFormattable formattable:
                AppendUtf16(formattable.ToString(format, _formatProvider));
                return;

            default:
                AppendUtf16(value.ToString());
                return;
        }
    }

    private void AppendUtf8Formattable(IUtf8SpanFormattable value, string? format)
    {
        var capacity = 32;

        while (true)
        {
            var destination = Reserve(capacity);
            if (value.TryFormat(destination, out var written, format, _formatProvider))
            {
                _length += written;
                return;
            }

            capacity = checked(capacity * 2);
        }
    }

    private void AppendUtf16(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        AppendUtf16(value.AsSpan());
    }

    private void AppendUtf16(ReadOnlySpan<char> value)
    {
        if (value.IsEmpty)
        {
            return;
        }

        var destination = Reserve(Encoding.UTF8.GetMaxByteCount(value.Length));
        _length += Encoding.UTF8.GetBytes(value, destination);
    }

    private void AppendUtf8(ReadOnlySpan<byte> value)
    {
        if (value.IsEmpty)
        {
            return;
        }

        value.CopyTo(Reserve(value.Length));
        _length += value.Length;
    }

    private Span<byte> Reserve(int length)
    {
        EnsureCapacity(checked(_length + length));
        return _buffer.AsSpan(_length);
    }

    private void EnsureCapacity(int length)
    {
        if (length <= _buffer.Length)
        {
            return;
        }

        var nextLength = Math.Max(length, _buffer.Length * 2);
        var nextBuffer = ArrayPool<byte>.Shared.Rent(nextLength);
        _buffer.AsSpan(0, _length).CopyTo(nextBuffer);
        ArrayPool<byte>.Shared.Return(_buffer, clearArray: true);
        _buffer = nextBuffer;
    }

    private void ApplyAlignment(int start, int alignment)
    {
        if (alignment == 0)
        {
            return;
        }

        var width = _length - start;
        var padding = Math.Abs(alignment) - width;
        if (padding <= 0)
        {
            return;
        }

        EnsureCapacity(checked(_length + padding));

        if (alignment > 0)
        {
            _buffer.AsSpan(start, width).CopyTo(_buffer.AsSpan(start + padding, width));
            _buffer.AsSpan(start, padding).Fill((byte)' ');
        }
        else
        {
            _buffer.AsSpan(_length, padding).Fill((byte)' ');
        }

        _length += padding;
    }
}
