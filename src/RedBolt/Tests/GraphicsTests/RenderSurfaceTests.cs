namespace RedBolt.Tests;

using RedBolt;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.RenderAPI;

public sealed class RenderSurfaceTests
{
    [Fact]
    public void Render_surface_matches_cpp_reference_type()
    {
        Assert.False(typeof(RenderSurface).IsValueType);
        Assert.False(typeof(ICloneable).IsAssignableFrom(typeof(RenderSurface)));
    }

    [Fact]
    public void Render_surface_tracks_texture_views_viewports_links_and_update_state()
    {
        var texture = new Texture2D { Name = "Textures/RenderSurface" };
        Assert.True(texture.SetSize(64, 32, TextureFormat.TexFormatRgba8Unorm,
            TextureFlag.BindRenderTarget | TextureFlag.NoMultiSampledAutoResolve, 4));

        var surface = new RenderSurface(texture, 2);
        var linkedSurface = new RenderSurface(texture, 3);
        var viewport = (Viewport?)null;

        Assert.Equal(64, surface.GetWidth());
        Assert.Equal(32, surface.GetHeight());
        Assert.Equal(new Vector2i(64, 32), surface.GetSize());
        Assert.Equal(4, surface.GetMultiSample());
        Assert.False(surface.GetAutoResolve());
        Assert.Same(texture, surface.GetParentTexture());
        Assert.Equal(2, surface.GetSlice());
        Assert.Equal(RenderTargetView.TextureSlice(texture, 2), surface.GetView());

        surface.SetNumViewports(2);
        Assert.Equal(2, surface.GetNumViewports());
        Assert.Null(surface.GetViewport(1));
        surface.SetViewport(3, viewport);
        Assert.Equal(4, surface.GetNumViewports());
        Assert.Null(surface.GetViewport(3));

        surface.SetLinkedRenderTarget(surface);
        surface.SetLinkedDepthStencil(surface);
        Assert.Null(surface.GetLinkedRenderTarget());
        Assert.Null(surface.GetLinkedDepthStencil());

        surface.SetLinkedRenderTarget(linkedSurface);
        surface.SetLinkedDepthStencil(linkedSurface);
        Assert.Same(linkedSurface, surface.GetLinkedRenderTarget());
        Assert.Same(linkedSurface, surface.GetLinkedDepthStencil());

        Assert.False(surface.IsUpdateQueued());
        surface.QueueUpdate();
        Assert.True(surface.IsUpdateQueued());
        surface.ResetUpdateQueued();
        Assert.False(surface.IsUpdateQueued());

        surface.SetUpdateMode(RenderSurfaceUpdateMode.ManualUpdate);
        Assert.Equal(RenderSurfaceUpdateMode.ManualUpdate, surface.GetUpdateMode());
        Assert.False(surface.IsResolveDirty());
        surface.SetResolveDirty(true);
        Assert.True(surface.IsResolveDirty());

        using var graphics = new Graphics();
        Assert.Equal(new Vector2i(64, 32), RenderSurface.GetSize(graphics, surface));
        Assert.Equal(new IntRect(0, 0, 64, 32), RenderSurface.GetRect(graphics, surface));

        surface.Dispose();
        Assert.Equal(0, surface.GetNumViewports());
        Assert.Null(surface.GetLinkedRenderTarget());
        Assert.Null(surface.GetLinkedDepthStencil());
    }

    [Fact]
    public void Headless_texture_does_not_create_owned_render_surfaces()
    {
        var texture = new Texture2D { Name = "Textures/OwnedRenderSurface" };
        Assert.True(texture.SetSize(64, 32, TextureFormat.TexFormatRgba8Unorm, TextureFlag.BindRenderTarget));
        Assert.Null(texture.GetRenderSurface());

        texture.Dispose();
    }
}
