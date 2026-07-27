using RedBolt;
using RedBolt.IO;

namespace RedBolt.Tests;

public sealed class VectorBufferTests
{
    [Fact]
    public void VectorBuffer_is_extensible_copyable_non_pod_class()
    {
        Assert.True(typeof(VectorBuffer).IsClass);
        Assert.False(typeof(VectorBuffer).IsSealed);
        Assert.True(typeof(ICloneable).IsAssignableFrom(typeof(VectorBuffer)));

        using var source = new VectorBuffer([1, 2, 3]);
        source.SetName("Source");
        source.Seek(2);

        using var clone = Assert.IsType<VectorBuffer>(source.Clone());
        Assert.NotSame(source, clone);
        Assert.NotSame(source.GetBuffer(), clone.GetBuffer());
        Assert.Equal(source.Name, clone.Name);
        Assert.Equal(source.Size, clone.Size);
        Assert.Equal(source.Position, clone.Position);

        clone.GetBuffer()[0] = 9;
        Assert.Equal(1, source.GetBuffer()[0]);
    }
}
