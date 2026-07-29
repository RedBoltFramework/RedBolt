namespace RedBolt.Tests;

using RedBolt.Core;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.RmlUI;

[Collection(nameof(GraphicsEngineCollection))]
public sealed class RmlCanvasComponentTests
{
    private readonly GraphicsEngineFixture _fixture;

    public RmlCanvasComponentTests(GraphicsEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Component_manages_offscreen_render_target()
    {
        RmlUI.RegisterRmlUILibrary(_fixture.Context);

        using var component = new RmlCanvasComponent();
        component.SetClearColor(Color.Red);
        component.SetUISize(new Vector2i(64, 64));

        var texture = component.GetTexture();
        Assert.NotNull(texture);
        Assert.Equal(64, texture.Width);
        Assert.Equal(64, texture.Height);
        Assert.Equal(new Vector2i(64, 64), component.GetUI().GetRmlContext().GetDimensions());
        Assert.NotNull(texture.GetRenderSurface());
        Assert.False(component.GetUI().GetBlockEvents());

        using var image = new Image();
        Assert.True(texture.GetImage(image));
        Assert.Equal(Color.Red, image.GetPixel(0, 0));

        component.SetEnabled(false);
        Assert.True(component.GetUI().GetBlockEvents());
        component.SetEnabled(true);
        Assert.False(component.GetUI().GetBlockEvents());
        Assert.NotNull(_fixture.Context.GetReflection(typeof(RmlCanvasComponent).GetClassName()));
    }
}
