namespace RedBolt.Tests;

using RedBolt.Core;
using RedBolt.Particles;

public class ParticleGraphBufferLayoutTests
{
    [Fact]
    public void AllocateUsesOneValueForScalarAndCapacityForOtherContainers()
    {
        var layout = new ParticleGraphBufferLayout();
        layout.Reset(3);

        var scalar = layout.Allocate(ParticleGraphContainerType.Scalar, VariantType.Float);
        var span = layout.Allocate(ParticleGraphContainerType.Span, VariantType.Vector3);
        var sparse = layout.Allocate(ParticleGraphContainerType.Sparse, VariantType.Int);

        Assert.Equal(0, scalar);
        Assert.Equal(1, span);
        Assert.Equal(2, sparse);
        Assert.Equal(0, layout[scalar].Offset);
        Assert.Equal(sizeof(float), layout[scalar].Size);
        Assert.Equal(sizeof(float), layout[span].Offset);
        Assert.Equal(12 * 3, layout[span].Size);
        Assert.Equal(sizeof(float) + 12 * 3, layout[sparse].Offset);
        Assert.Equal(sizeof(int) * 3, layout[sparse].Size);
        Assert.Equal(sizeof(float) + 12 * 3 + sizeof(int) * 3, layout.RequiredMemory);
    }

    [Fact]
    public void ResetRetainsAllocatedSpansAndRestartsPosition()
    {
        var layout = new ParticleGraphBufferLayout();
        layout.Reset(2);
        var first = layout.Allocate(ParticleGraphContainerType.Span, VariantType.Int);

        layout.Reset(4);
        var second = layout.Allocate(ParticleGraphContainerType.Span, VariantType.Int);

        Assert.Equal(0, first);
        Assert.Equal(1, second);
        Assert.Equal(0, layout[first].Offset);
        Assert.Equal(sizeof(int) * 2, layout[first].Size);
        Assert.Equal(0, layout[second].Offset);
        Assert.Equal(sizeof(int) * 4, layout[second].Size);
        Assert.Equal(sizeof(int) * 4, layout.RequiredMemory);
    }
}
