using RedBolt;

namespace RedBolt.Tests;

public sealed class ResourceEventsTests
{
    [Fact]
    public void Resource_events_preserve_cpp_fields()
    {
        var fileChanged = new ResourceEvents.FileChanged
        {
            FileName = "Textures/Diffuse.png",
            ResourceName = "Materials/Default.xml"
        };
        var loadFailed = new ResourceEvents.LoadFailed
        {
            ResourceName = "Models/Box.mdl"
        };
        var resourceNotFound = new ResourceEvents.ResourceNotFound
        {
            ResourceName = "Textures/Missing.png"
        };
        var unknownResourceType = new ResourceEvents.UnknownResourceType
        {
            ResourceType = new StringHash("Texture")
        };
        var backgroundLoaded = new ResourceEvents.ResourceBackgroundLoaded
        {
            ResourceName = "Scenes/Main.xml",
            Success = true,
            Resource = null
        };

        Assert.Equal("Textures/Diffuse.png", fileChanged.FileName);
        Assert.Equal("Materials/Default.xml", fileChanged.ResourceName);
        Assert.Equal("Models/Box.mdl", loadFailed.ResourceName);
        Assert.Equal("Textures/Missing.png", resourceNotFound.ResourceName);
        Assert.Equal(new StringHash("Texture"), unknownResourceType.ResourceType);
        Assert.Equal("Scenes/Main.xml", backgroundLoaded.ResourceName);
        Assert.True(backgroundLoaded.Success);
        Assert.Null(backgroundLoaded.Resource);
    }

    [Fact]
    public void Resource_events_with_no_parameters_are_empty_structs()
    {
        Assert.Empty(typeof(ResourceEvents.ReloadStarted).GetFields());
        Assert.Empty(typeof(ResourceEvents.ReloadFinished).GetFields());
        Assert.Empty(typeof(ResourceEvents.ReloadFailed).GetFields());
        Assert.Empty(typeof(ResourceEvents.ChangeLanguage).GetFields());
    }
}
