using System.Reflection;

using RedBolt;
using RedBolt.Bindings.Diligent;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class GraphicsRenderTargetViewTests
{
    private readonly HeadlessEngineFixture fixture;

    public GraphicsRenderTargetViewTests(HeadlessEngineFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public void Graphics_resolves_cubemap_face_render_target_views_as_2d_slices()
    {
        WithInitializedGraphics(() =>
        {
            var texture = new TextureCube("Textures/RenderTargetCube");
            Assert.True(texture.SetSize(16, TextureFormat.RGBA8));

            var firstView = ResolveRenderTargetView(RenderTargetView.TextureSlice(texture, 3u));
            var secondView = ResolveRenderTargetView(RenderTargetView.TextureSlice(texture, 3u));
            Assert.Same(firstView, secondView);

            var desc = firstView.GetDesc();
            Assert.Equal(TextureViewType.TextureViewRenderTarget, desc.ViewType);
            Assert.Equal(ResourceDimension.ResourceDimTex2d, desc.TextureDim);
            Assert.Equal((uint)3, desc.FirstSlice);
            Assert.Equal((uint)1, desc.NumSlices);
        });
    }

    [Fact]
    public void Graphics_resolves_depth_array_slice_views_as_2d_read_only_depth_targets()
    {
        WithInitializedGraphics(() =>
        {
            var texture = new Texture2DArray("Textures/RenderTargetArray");
            Assert.True(texture.SetLayers(4u, 16, 16, TextureFormat.Depth24Stencil8));

            var firstView = ResolveRenderTargetView(RenderTargetView.ReadOnlyDepthSlice(texture, 2u));
            var secondView = ResolveRenderTargetView(RenderTargetView.ReadOnlyDepthSlice(texture, 2u));
            Assert.Same(firstView, secondView);

            var desc = firstView.GetDesc();
            Assert.Equal(TextureViewType.TextureViewReadOnlyDepthStencil, desc.ViewType);
            Assert.Equal(ResourceDimension.ResourceDimTex2d, desc.TextureDim);
            Assert.Equal((uint)2, desc.FirstSlice);
            Assert.Equal((uint)1, desc.NumSlices);
        });
    }

    private ITextureView ResolveRenderTargetView(RenderTargetView view)
    {
        var method = typeof(Graphics).GetMethod("ResolveRenderTargetView", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Graphics.ResolveRenderTargetView was not found.");
        return Assert.IsAssignableFrom<ITextureView>(method.Invoke(fixture.Graphics, [view]));
    }

    private void WithInitializedGraphics(Action action)
    {
        var parameters = new EngineParameters
        {
            ApplicationName = "RedBolt.Tests.GraphicsRenderTargetViewTests",
            Headless = false,
            WindowWidth = 64,
            WindowHeight = 64
        };

        fixture.Input.Initialize(parameters);
        fixture.Graphics.Initialize(parameters, fixture.Input);
        try
        {
            action();
        }
        finally
        {
            fixture.Graphics.Shutdown();
            fixture.Input.Shutdown();
        }
    }
}
