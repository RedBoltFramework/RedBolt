namespace RedBolt.Tests;

using RedBolt.Core;
using RedBolt.Particles;

public class ParticleGraphPinTests
{
    private sealed class MutableParticleGraphPin : ParticleGraphPin
    {
        public MutableParticleGraphPin(ParticleGraphPinFlag flags, string name, VariantType type)
            : base(flags, name, type)
        {
        }

        public bool ChangeName(string name) => SetName(name);

        public bool ChangeValueType(VariantType type) => SetValueType(type);

        public void ChangeIsInput(bool isInput) => SetIsInput(isInput);
    }

    [Fact]
    public void DefaultConstructorMatchesCppDefaults()
    {
        var pin = new ParticleGraphPin();

        Assert.True(pin.IsInput);
        Assert.Equal(ParticleGraphPinFlag.Input, pin.Flags);
        Assert.Equal(string.Empty, pin.Name);
        Assert.Equal(VariantType.None, pin.RequestedType);
        Assert.Equal(VariantType.None, pin.ValueType);
        Assert.Equal(ParticleGraphContainerType.Auto, pin.MemoryReference.Type);
        Assert.False(pin.Connected);
        Assert.Equal(-1, pin.ConnectedNodeIndex);
        Assert.Equal(0, pin.ConnectedPinIndex);
    }

    [Fact]
    public void ConstructorsStoreRequestedSetup()
    {
        var typed = new ParticleGraphPin(ParticleGraphPinFlag.Input, "value", VariantType.Float,
            ParticleGraphContainerType.Sparse);
        var untyped = new ParticleGraphPin(ParticleGraphPinFlag.Output, "result",
            ParticleGraphContainerType.Scalar);

        Assert.True(typed.IsInput);
        Assert.Equal("value", typed.Name);
        Assert.Equal(new RedBolt.Container.StringHash("value"), typed.NameHash);
        Assert.Equal(VariantType.Float, typed.RequestedType);
        Assert.Equal(ParticleGraphContainerType.Auto, typed.ContainerType);
        Assert.False(untyped.IsInput);
        Assert.Equal(VariantType.None, untyped.RequestedType);
    }

    [Fact]
    public void WithTypeReturnsFreshPinSetup()
    {
        var source = new ParticleGraphPin(ParticleGraphPinFlag.Input, "value", VariantType.Float,
            ParticleGraphContainerType.Sparse);
        Assert.True(source.SetSource(3, 4));

        var result = source.WithType(VariantType.Vector3);

        Assert.NotSame(source, result);
        Assert.Equal(ParticleGraphPinFlag.Input, result.Flags);
        Assert.Equal("value", result.Name);
        Assert.Equal(VariantType.Vector3, result.RequestedType);
        Assert.False(result.Connected);
    }

    [Fact]
    public void SetSourceRejectsOutputAndConnectsInput()
    {
        var output = new ParticleGraphPin(ParticleGraphPinFlag.Output, "out");
        var input = new ParticleGraphPin(ParticleGraphPinFlag.Input, "in");

        Assert.False(output.SetSource(1, 2));
        Assert.False(output.Connected);
        Assert.True(input.SetSource(1, 2));
        Assert.True(input.Connected);
        Assert.Equal(1, input.ConnectedNodeIndex);
        Assert.Equal(2, input.ConnectedPinIndex);
    }

    [Fact]
    public void ProtectedMutatorsRespectFlags()
    {
        var fixedPin = new MutableParticleGraphPin(ParticleGraphPinFlag.Input, "fixed", VariantType.Float);
        var mutablePin = new MutableParticleGraphPin(
            ParticleGraphPinFlag.Input | ParticleGraphPinFlag.MutableName | ParticleGraphPinFlag.MutableType,
            "mutable", VariantType.Float);

        Assert.False(fixedPin.ChangeName("changed"));
        Assert.False(fixedPin.ChangeValueType(VariantType.Vector3));
        Assert.True(mutablePin.ChangeName("changed"));
        Assert.True(mutablePin.ChangeValueType(VariantType.Vector3));
        mutablePin.ChangeIsInput(false);

        Assert.Equal("fixed", fixedPin.Name);
        Assert.Equal(VariantType.Float, fixedPin.RequestedType);
        Assert.Equal("changed", mutablePin.Name);
        Assert.Equal(VariantType.Vector3, mutablePin.RequestedType);
        Assert.False(mutablePin.IsInput);
    }

    [Fact]
    public void CloneCopiesMutableStateIndependently()
    {
        var source = new MutableParticleGraphPin(
            ParticleGraphPinFlag.Input | ParticleGraphPinFlag.MutableName, "source", VariantType.Float);
        Assert.True(source.SetSource(5, 6));

        var clone = source.Clone();
        Assert.True(source.ChangeName("changed"));

        Assert.Equal("changed", source.Name);
        Assert.Equal("source", clone.Name);
        Assert.True(clone.Connected);
        Assert.Equal(5, clone.ConnectedNodeIndex);
        Assert.Equal(6, clone.ConnectedPinIndex);
    }
}
