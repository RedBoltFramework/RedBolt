namespace RedBolt.Tests;

using RedBolt.Particles;

public class SparseSpanTests
{
    [Fact]
    public void IndexerUsesSparseIndices()
    {
        int[] data = [10, 20, 30, 40];
        int[] indices = [3, 1, 2, 0];
        var span = new SparseSpan<int>(data.AsSpan(), indices.AsSpan());

        Assert.Equal(40, span[0]);
        Assert.Equal(20, span[1]);
        Assert.Equal(30, span[2]);
        Assert.Equal(10, span[3]);
    }

    [Fact]
    public void IndexerReturnsMutableReference()
    {
        int[] data = [10, 20, 30];
        int[] indices = [2, 0];
        var span = new SparseSpan<int>(data, indices);

        span[1] = 50;

        Assert.Equal(50, data[0]);
    }
}
