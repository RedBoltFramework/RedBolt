namespace RedBolt.Tests;

using RedBolt.Particles;

public class EmitterTests
{
    [Fact]
    public void EmitFromValuesMatchNativeOrder()
    {
        Assert.Equal(0, (int)EmitFrom.Base);
        Assert.Equal(1, (int)EmitFrom.Volume);
        Assert.Equal(2, (int)EmitFrom.Surface);
        Assert.Equal(3, (int)EmitFrom.Edge);
        Assert.Equal(4, (int)EmitFrom.Vertex);
    }

    [Fact]
    public void GetEmitFromNamesReturnsNativeNameTable()
    {
        Assert.Equal(["Base", "Volume", "Surface", "Edge", "Vertex", null], Emitter.GetEmitFromNames());
    }
}
