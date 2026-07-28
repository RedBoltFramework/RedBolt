namespace RedBolt.Tests;

using System.Runtime.InteropServices;

using RedBolt.Particles;

public class ParticleGraphSpanTests
{
    [Fact]
    public void DefaultConstructorCreatesEmptySpan()
    {
        var layout = new ParticleGraphSpan();

        Assert.Equal(0, layout.Offset);
        Assert.Equal(0, layout.Size);
        Assert.True(layout.MakeSpan<int>([]).IsEmpty);
    }

    [Fact]
    public void ConstructorDefinesByteSlice()
    {
        var buffer = new byte[sizeof(int) * 4];
        var values = MemoryMarshal.Cast<byte, int>(buffer.AsSpan());
        values[0] = 10;
        values[1] = 20;
        values[2] = 30;
        values[3] = 40;
        var layout = new ParticleGraphSpan(sizeof(int), sizeof(int) * 2);

        var slice = layout.MakeSpan<int>(buffer);

        Assert.Equal([20, 30], slice.ToArray());
        slice[1] = 50;
        Assert.Equal(50, values[2]);
    }
}
