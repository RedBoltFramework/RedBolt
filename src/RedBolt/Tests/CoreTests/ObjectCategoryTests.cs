namespace RedBolt.Tests;

public class ObjectCategoryTests
{
    [Fact]
    public void ConstantsMatchRbfxValues()
    {
        Assert.Equal("Component/Audio", ObjectCategory.Category_Audio);
        Assert.Equal("Component/Geometry", ObjectCategory.Category_Geometry);
        Assert.Equal("Component/Inverse Kinematics", ObjectCategory.Category_IK);
        Assert.Equal("Component/Logic", ObjectCategory.Category_Logic);
        Assert.Equal("Component/Navigation", ObjectCategory.Category_Navigation);
        Assert.Equal("Component/Network", ObjectCategory.Category_Network);
        Assert.Equal("Component/Physics", ObjectCategory.Category_Physics);
        Assert.Equal("Component/Physics2D", ObjectCategory.Category_Physics2D);
        Assert.Equal("Component/RmlUI", ObjectCategory.Category_RmlUI);
        Assert.Equal("Component/Scene", ObjectCategory.Category_Scene);
        Assert.Equal("Component/Subsystem", ObjectCategory.Category_Subsystem);
        Assert.Equal("Component/Urho2D", ObjectCategory.Category_Urho2D);
        Assert.Equal("Component/Plugin", ObjectCategory.Category_Plugin);
        Assert.Equal("Component/User", ObjectCategory.Category_User);
        Assert.Equal("RenderPass", ObjectCategory.Category_RenderPass);
        Assert.Equal("Transformer", ObjectCategory.Category_Transformer);
        Assert.Equal("UI", ObjectCategory.Category_UI);
    }
}
