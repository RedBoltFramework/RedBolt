namespace RedBolt.Tests;

using RedBolt.Particles;

public class ParticleGraphPinRefTests
{
    [Fact]
    public void DefaultConstructorUsesAutoContainerAndZeroIndex()
    {
        var pinRef = new ParticleGraphPinRef();

        Assert.Equal(ParticleGraphContainerType.Auto, pinRef.Type);
        Assert.Equal(0, pinRef.Index);
    }

    [Fact]
    public void ConstructorStoresContainerAndIndex()
    {
        var pinRef = new ParticleGraphPinRef(ParticleGraphContainerType.Sparse, 42);

        Assert.Equal(ParticleGraphContainerType.Sparse, pinRef.Type);
        Assert.Equal(42, pinRef.Index);
    }

    [Fact]
    public void AssignmentCopiesValue()
    {
        var source = new ParticleGraphPinRef(ParticleGraphContainerType.Scalar, 7);
        var copy = source;

        copy.Index = 9;

        Assert.Equal(7, source.Index);
        Assert.Equal(9, copy.Index);
    }
}
