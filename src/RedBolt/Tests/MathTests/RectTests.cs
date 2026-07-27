using System.Runtime.InteropServices;
using RedBolt;

namespace RedBolt.Tests;

public sealed class RectTests
{
    [Fact]
    public void Shape_and_defaults_match_native()
    {
        Assert.True(typeof(Rect).IsValueType);
        Assert.True(typeof(ICloneable).IsAssignableFrom(typeof(Rect)));
        Assert.Equal(16, Marshal.SizeOf<Rect>());
        Assert.Equal(0, Marshal.OffsetOf<Rect>(nameof(Rect.Min)).ToInt32());
        Assert.Equal(8, Marshal.OffsetOf<Rect>(nameof(Rect.Max)).ToInt32());

        var rect = new Rect();
        Assert.False(rect.Defined);
        Assert.Equal(new Vector2f(float.PositiveInfinity, float.PositiveInfinity), rect.Min);
        Assert.Equal(new Vector2f(float.NegativeInfinity, float.NegativeInfinity), rect.Max);
    }

    [Fact]
    public void Copies_are_independent()
    {
        var source = new Rect(1.0f, 2.0f, 3.0f, 4.0f);
        var copy = new Rect(source);
        var clone = source.Clone();
        source.Define(Vector2f.Zero);

        Assert.Equal(new Rect(1.0f, 2.0f, 3.0f, 4.0f), copy);
        Assert.Equal(copy, clone);
    }

    [Fact]
    public void Data_is_contiguous_and_non_allocating()
    {
        var rect = new Rect(1.0f, 2.0f, 3.0f, 4.0f);
        Assert.Equal(new[] { 1.0f, 2.0f, 3.0f, 4.0f }, rect.Data().ToArray());

        rect.Min.X = 5.0f;
        Assert.Equal(5.0f, rect.Data()[0]);
    }

    [Fact]
    public void Operators_and_bounds_match_native()
    {
        var lhs = new Rect(1.0f, 2.0f, 3.0f, 4.0f);
        var rhs = new Rect(2.0f, 3.0f, 4.0f, 5.0f);
        Assert.Equal(new Rect(3.0f, 5.0f, 7.0f, 9.0f), lhs + rhs);
        Assert.Equal(new Rect(-1.0f, -1.0f, -1.0f, -1.0f), lhs - rhs);
        Assert.Equal(new Rect(2.0f, 4.0f, 6.0f, 8.0f), lhs * 2.0f);
        Assert.Equal(new Rect(0.5f, 1.0f, 1.5f, 2.0f), lhs / 2.0f);
        Assert.Equal(Intersection.Inside, lhs.IsInside(new Vector2f(1.0f, 2.0f)));
        Assert.Equal(Intersection.Outside, lhs.IsInside(new Vector2f(4.0f, 2.0f)));
    }

    [Fact]
    public void Merge_clip_and_string_match_native()
    {
        var rect = new Rect();
        rect.Merge(new Vector2f(1.0f, 2.0f));
        rect.Merge(new Vector2f(3.0f, 4.0f));
        Assert.Equal(new Rect(1.0f, 2.0f, 3.0f, 4.0f), rect);

        rect.Clip(new Rect(2.0f, 3.0f, 5.0f, 6.0f));
        Assert.Equal(new Rect(2.0f, 3.0f, 3.0f, 4.0f), rect);
        Assert.Equal("2 3 3 4", rect.ToString());

        rect.Clip(new Rect(10.0f, 10.0f, 11.0f, 11.0f));
        Assert.False(rect.Defined);
    }
}
