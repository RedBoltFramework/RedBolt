using System.Text;
using Xunit;

using RedBolt.Container;
namespace RedBolt.Tests;

public sealed class CStringTests
{
    [Fact]
    public void Constructor_adds_zero_terminator_and_matches_utf8()
    {
        var value = new CString("høla");

        var expectedBytes = Encoding.UTF8.GetBytes("høla");

        Assert.Equal(expectedBytes.Length + 1, value.ByteLength + 1);
        Assert.Equal(expectedBytes.Length + 1, value.Bytes.Length);
        Assert.Equal(0, value.Bytes[^1]);
        Assert.Equal(expectedBytes, value.Bytes[..^1].ToArray());
        Assert.Equal("høla", value.ConvertToString());
    }

    [Fact]
    public void Small_values_use_inline_storage_until_capacity_is_exceeded()
    {
        var value = new CString("inline");

        value.FormatInPlace("12345678901234");
        Assert.Equal(16, GetCapacity(value));

        value.FormatInPlace("123456789012345");
        Assert.Equal(16, GetCapacity(value));

        value.FormatInPlace("1234567890123456");
        Assert.True(GetCapacity(value) > 16);
        Assert.Equal("1234567890123456", value.ConvertToString());
    }

    [Fact]
    public void Implicit_conversion_from_string()
    {
        CString value = "hello";

        Assert.Equal("hello", value.ConvertToString());
    }

    [Fact]
    public void Equals_string_and_cstring_semantics()
    {
        var first = new CString("abc");
        var second = new CString("abc");

        Assert.True(first == second);
        Assert.True(first.Equals("abc"));
        Assert.True(first.Equals(second));
        Assert.False(first == new CString("xyz"));
    }

    [Fact]
    public void Format_formats_and_replaces_internal_buffer()
    {
        var value = new CString("seed");

        value.FormatInPlace("point {0}-{1:00}", 5, 7);

        Assert.Equal("point 5-07", value.ConvertToString());
        Assert.Equal((byte)0, value.Bytes[^1]);
    }

    [Fact]
    public void Static_Format_returns_formatted_string()
    {
        using var value = CString.Format("value={0}", 12);

        Assert.Equal("value=12", value.ConvertToString());
        Assert.Equal((byte)0, value.Bytes[^1]);
    }

    [Fact]
    public void Clone_owns_an_independent_utf8_buffer()
    {
        using var value = new CString("1234567890123456");
        using var clone = (CString)value.Clone();

        value.FormatInPlace("changed");

        Assert.Equal("1234567890123456", clone.ConvertToString());
        Assert.NotEqual(value.Pointer, clone.Pointer);
    }

    [Fact]
    public void FormatInPlace_does_not_allocate_when_buffer_is_large_enough()
    {
        var value = new CString("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA");
        var args = new object?[] { 17, 42 };

        value.FormatInPlace("x{0}-y{1:00}", args);

        var allocatedBytes = MeasureAllocatedBytes(() => value.FormatInPlace("x{0}-y{1:00}", args));

        Assert.Equal(0, allocatedBytes);
        Assert.Equal("x17-y42", value.ConvertToString());
    }

    [Fact]
    public void Equals_and_hash_do_not_allocate()
    {
        var value = new CString("høla");
        var other = new CString("høla");

        _ = value.Equals("høla");
        _ = value.Equals(other);
        _ = value.GetHashCode();

        var allocatedBytes = MeasureAllocatedBytes(() =>
        {
            _ = value.Equals("høla");
            _ = value.Equals(other);
            _ = value.GetHashCode();
        });

        Assert.Equal(0, allocatedBytes);
    }

    [Fact]
    public void Interpolated_FormatInPlace_does_not_allocate_for_numeric_values()
    {
        var value = new CString("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA");

        value.FormatInPlace($"x{17}-y{42.5f:F1}");

        var allocatedBytes = MeasureAllocatedBytes(() => value.FormatInPlace($"x{17}-y{42.5f:F1}"));

        Assert.Equal(0, allocatedBytes);
        Assert.Equal("x17-y42.5", value.ConvertToString());
    }

    [Fact]
    public void Interpolated_FormatInPlace_does_not_allocate_for_string_or_cstring_values()
    {
        var value = new CString("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA");
        var other = new CString("world");
        const string text = "hello";

        value.FormatInPlace($"{text} {other}!");

        var allocatedBytes = MeasureAllocatedBytes(() => value.FormatInPlace($"{text} {other}!"));

        Assert.Equal(0, allocatedBytes);
        Assert.Equal("hello world!", value.ConvertToString());
    }

    [Fact]
    public void Interpolated_FormatInPlace_matches_static_scene_hot_path_without_allocations()
    {
        var heapSizeLine = new CString("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA");
        var gen0CollectionsLine = new CString("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA");
        var totalAllocatedLine = new CString("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA");
        var fpsLine = new CString("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA");

        heapSizeLine.FormatInPlace($"Heap Size: {12.5:F1} MB");
        gen0CollectionsLine.FormatInPlace($"Gen 0 Collections: {7}");
        totalAllocatedLine.FormatInPlace($"Total Allocated: {42.5:F1} MB");
        fpsLine.FormatInPlace($"FPS: {120.0f:F1}");

        var allocatedBytes = MeasureAllocatedBytes(() =>
        {
            heapSizeLine.FormatInPlace($"Heap Size: {12.5:F1} MB");
            gen0CollectionsLine.FormatInPlace($"Gen 0 Collections: {7}");
            totalAllocatedLine.FormatInPlace($"Total Allocated: {42.5:F1} MB");
            fpsLine.FormatInPlace($"FPS: {120.0f:F1}");
        });

        Assert.Equal(0, allocatedBytes);
    }

    [Fact]
    public void ConvertToString_is_the_explicit_allocating_path()
    {
        var value = new CString("hello");

        var allocatedBytes = MeasureAllocatedBytes(() => _ = value.ConvertToString());

        Assert.True(allocatedBytes > 0);
    }

    [Fact]
    public void FormatInPlace_rejects_types_that_require_hidden_string_allocations()
    {
        var value = new CString();
        var argument = new HiddenAllocatingType();

        var exception = Assert.Throws<FormatException>(() => value.FormatInPlace("value={0}", argument));

        Assert.Contains(nameof(HiddenAllocatingType), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Dispose_releases_heap_storage_and_resets_to_inline_storage()
    {
        var value = new CString("1234567890123456");
        var heapPointer = value.Pointer;

        value.Dispose();
        value.Dispose();

        Assert.Equal(0, value.ByteLength);
        Assert.Equal((byte)0, value.Bytes[0]);
        Assert.NotEqual(heapPointer, value.Pointer);

        value.FormatInPlace("hello");

        Assert.Equal("hello", value.ConvertToString());
    }

    private static long MeasureAllocatedBytes(Action action)
    {
        action();

        var beforeBytes = GC.GetAllocatedBytesForCurrentThread();
        action();
        var afterBytes = GC.GetAllocatedBytesForCurrentThread();
        return afterBytes - beforeBytes;
    }

    private static int GetCapacity(CString value)
    {
        var field = typeof(CString).GetField("_capacity", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(field);
        return (int)field.GetValue(value)!;
    }

    private sealed class HiddenAllocatingType
    {
        public override string ToString() => "hidden";
    }
}
