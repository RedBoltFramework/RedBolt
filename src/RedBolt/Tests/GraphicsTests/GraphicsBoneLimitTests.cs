namespace RedBolt.Tests;

using RedBolt.Graphics;

public sealed class GraphicsBoneLimitTests
{
    [Fact]
    public void Graphics_bone_limit_defaults_and_can_be_overridden()
    {
        GraphicsSystem.SetMaxBones(0u);
        Assert.Equal(128u, GraphicsSystem.GetMaxBones());

        GraphicsSystem.SetMaxBones(96u);
        Assert.Equal(96u, GraphicsSystem.GetMaxBones());

        GraphicsSystem.SetMaxBones(0u);
    }
}
