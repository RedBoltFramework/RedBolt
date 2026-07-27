using System.Runtime.CompilerServices;

using RedBolt;
using RedBolt.Container;
using RedBolt.Math;

namespace RedBolt.Tests;

public sealed class Array2DTests
{
    [Fact]
    public void Dimensions_use_row_major_offsets()
    {
        var dimensions = new ArrayDimensions2D(3, 2);

        Assert.Equal(3, dimensions.GetWidth());
        Assert.Equal(2, dimensions.GetHeight());
        Assert.Equal(6u, dimensions.GetCapacity());
        Assert.Equal(new Vector2i(3, 2), dimensions.GetSize());
        Assert.Equal(new IntRect(Vector2i.Zero, new Vector2i(3, 2)), dimensions.GetRect());
        Assert.True(dimensions.Contains(new Vector2i(2, 1)));
        Assert.False(dimensions.Contains(new Vector2i(3, 1)));
        Assert.Equal(5u, dimensions.IndexToOffset(new Vector2i(2, 1)));
        Assert.Equal(new Vector2i(2, 1), dimensions.OffsetToIndex(5));
    }

    [Fact]
    public void Index_helpers_clamp_and_wrap()
    {
        var dimensions = new ArrayDimensions2D(3, 2);

        Assert.Equal(new Vector2i(0, 1), dimensions.ClampIndex(new Vector2i(-1, 9)));
        Assert.Equal(new Vector2i(2, 1), dimensions.ClampIndex(new Vector2i(9, 9)));
        Assert.Equal(new Vector2i(2, 1), dimensions.WrapIndex(new Vector2i(-1, 3)));
    }

    [Fact]
    public void Array_access_reset_fill_swap_and_enumeration_match()
    {
        var array = new Array2D<int>(3, 2, 7);

        array[new Vector2i(1, 1)] = 9;
        Assert.Equal(9, array.Get(new Vector2i(1, 1)));
        Assert.Equal(7, array.GetWrapped(new Vector2i(-1, -1)));
        Assert.Equal(7, array.GetClamped(new Vector2i(9, 9)));
        Assert.True(Unsafe.IsNullRef(ref array.GetOptional(new Vector2i(-1, 0))));
        Assert.True(Unsafe.IsNullRef(ref new Array2D<string>(3, 2).GetOptional(new Vector2i(-1, 0))));
        Assert.Equal(6, array.Begin().Length);
        Assert.Equal(0, array.End().Length);
        Assert.Equal(44, array.Sum());

        array.Fill(3);
        Assert.Equal(18, array.Sum());

        var other = new Array2D<int>(1, 1, 5);
        array.Swap(other);
        Assert.Equal(1, array.Dim().GetWidth());
        Assert.Equal(5, array.Get(new Vector2i(0, 0)));
        Assert.Equal(3, other.Get(new Vector2i(0, 0)));

        array.Reset(2, 2, 4);
        Assert.Equal(16, array.Sum());
        array.Reset();
        Assert.Equal(0u, array.GetCapacity());
    }

    [Fact]
    public void Clone_and_dispose_preserve_cpp_value_lifetime()
    {
        var value = new CopyableValue(7);
        var array = new Array2D<CopyableValue>(2, 1, value);
        var clone = (Array2D<CopyableValue>)array.Clone();

        Assert.NotSame(value, array.Get(new Vector2i(0, 0)));
        Assert.NotSame(array.Get(new Vector2i(0, 0)), array.Get(new Vector2i(1, 0)));
        Assert.NotSame(array.Get(new Vector2i(0, 0)), clone.Get(new Vector2i(0, 0)));

        var first = array.Get(new Vector2i(0, 0));
        array.Dispose();
        Assert.True(first.IsDisposed);
        Assert.False(clone.Get(new Vector2i(0, 0)).IsDisposed);

        clone.Dispose();
    }

    private sealed class CopyableValue(int value) : ICloneable, IDisposable
    {
        public int Value { get; } = value;
        public bool IsDisposed { get; private set; }

        public object Clone() => new CopyableValue(Value);

        public void Dispose() => IsDisposed = true;
    }
}

file static class Array2DExtensions
{
    public static int Sum(this Array2D<int> array)
    {
        var result = 0;
        foreach (var value in array)
            result += value;
        return result;
    }
}
