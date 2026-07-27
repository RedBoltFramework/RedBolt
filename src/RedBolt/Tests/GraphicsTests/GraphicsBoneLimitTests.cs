namespace RedBolt.Tests;

public sealed class GraphicsBoneLimitTests
{
    [Fact]
    public void Graphics_bone_limit_defaults_and_can_be_overridden()
    {
        Graphics.SetMaxBones(0u);
        Assert.Equal(128u, Graphics.GetMaxBones());

        Graphics.SetMaxBones(96u);
        Assert.Equal(96u, Graphics.GetMaxBones());

        Graphics.SetMaxBones(0u);
    }
}
