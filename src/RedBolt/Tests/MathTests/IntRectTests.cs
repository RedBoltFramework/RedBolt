using System.Runtime.InteropServices;

namespace RedBolt.Tests;

public sealed class IntRectTests
{
    [Fact]
    public void Is_sequential_sixteen_byte_value_type()
    {
        Assert.True(typeof(IntRect).IsValueType);
        Assert.Equal(16, Marshal.SizeOf<IntRect>());
        Assert.Equal(IntRect.Zero, new IntRect());
    }

    [Fact]
    public void Copy_is_independent_and_data_aliases_contiguous_fields()
    {
        var rect = new IntRect(1, 2, 3, 4);
        var copy = rect;
        var data = rect.Data();

        rect.Left = 5;

        Assert.Equal(new IntRect(1, 2, 3, 4), copy);
        Assert.Equal(5, data[0]);
        Assert.Equal(2, data[1]);
        Assert.Equal(3, data[2]);
        Assert.Equal(4, data[3]);
    }

    [Fact]
    public void Clip_merge_and_containment_match_native_boundaries()
    {
        var clipped = new IntRect(0, 0, 4, 4);
        clipped.Clip(new IntRect(1, 2, 3, 5));
        Assert.Equal(new IntRect(1, 2, 3, 4), clipped);

        clipped.Clip(new IntRect(4, 0, 5, 1));
        Assert.Equal(IntRect.Zero, clipped);

        clipped.Merge(new IntRect(2, 3, 6, 7));
        Assert.Equal(new IntRect(2, 3, 6, 7), clipped);
        Assert.True(clipped.Contains(new Vector2i(2, 3)));
        Assert.False(clipped.Contains(new Vector2i(6, 7)));
    }

    [Fact]
    public void Constructors_operators_properties_and_hash_match_native_values()
    {
        var rect = new IntRect(new[] { 1, 2, 5, 8 });

        Assert.Equal(new Vector2i(1, 2), rect.Min);
        Assert.Equal(new Vector2i(5, 8), rect.Max);
        Assert.Equal(new Vector2i(4, 6), rect.Size);
        Assert.Equal(new IntRect(2, 4, 10, 16), rect * 2.0f);
        Assert.Equal(new IntRect(0, 1, 2, 4), rect / 2.0f);
        Assert.Equal(new IntRect(2, 3, 6, 9), rect + new IntRect(1, 1, 1, 1));
        Assert.Equal(new IntRect(0, 1, 4, 7), rect - new IntRect(1, 1, 1, 1));
        Assert.Equal(Intersection.Inside, rect.IsInside(new IntRect(2, 3, 4, 7)));
        Assert.Equal(Intersection.Intersects, rect.IsInside(new IntRect(0, 3, 4, 7)));
        Assert.Equal("1 2 5 8", rect.ToString());
        Assert.Equal(1u * 31u * 31u * 31u + 5u * 31u * 31u + 2u * 31u + 8u, rect.ToHash());
    }

    [Fact]
    public void Iterator_visits_each_point_row_by_row()
    {
        var rect = new IntRect(1, 2, 3, 4);
        var iterator = rect.Begin();
        var end = rect.End();
        var points = new List<Vector2i>();

        while (iterator != end)
        {
            points.Add(iterator.Current);
            iterator++;
        }

        Assert.Equal(
            [new Vector2i(1, 2), new Vector2i(2, 2), new Vector2i(1, 3), new Vector2i(2, 3)],
            points);
    }
}
