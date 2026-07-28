namespace RedBolt.Tests;

using RedBolt.Bindings.RmlUi;
using RedBolt.RmlUI;

[Collection(nameof(GraphicsEngineCollection))]
public sealed class RmlRendererTests
{
    private readonly GraphicsEngineFixture _fixture;

    public RmlRendererTests(GraphicsEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public unsafe void RmlRenderer_matches_texture_callback_behavior()
    {
        using var renderer = new RmlRenderer();
        Assert.NotEqual(nint.Zero, renderer.GetAdaptedInterface());

        var size = new RmlUiLegacyVector2i { X = 1, Y = 1 };
        Span<byte> pixels = [1, 2, 3, 4];
        nuint generatedTexture = 0;
        fixed (byte* source = pixels)
        {
            Assert.True(renderer.GenerateTexture(
                (nint)(&generatedTexture), (nint)source, (nint)(&size)));
        }
        Assert.NotEqual(0u, generatedTexture);
        renderer.ReleaseTexture(generatedTexture);

        using var path = new Std.String();
        path.Assign("Textures/Missing.png");
        var missingSize = new RmlUiLegacyVector2i { X = -1, Y = -1 };
        nuint missingTexture = 0;
        Assert.True(renderer.LoadTexture((nint)(&missingTexture), (nint)(&missingSize), path));
        Assert.NotEqual(0u, missingTexture);
        Assert.Equal(-1, missingSize.X);
        Assert.Equal(-1, missingSize.Y);
        renderer.ReleaseTexture(missingTexture);
        renderer.ReleaseTexture(0);
    }

    [Fact]
    public unsafe void RmlRenderer_submits_legacy_geometry()
    {
        RbfxTestData.AddResourceDirs(_fixture.Resources);
        try
        {
            using var renderer = new RmlRenderer();
            Span<RmlUiLegacyVertex> vertices =
            [
                new()
                {
                    Position = new RmlUiLegacyVector2f { X = 0, Y = 0 },
                    Colour = uint.MaxValue
                },
                new()
                {
                    Position = new RmlUiLegacyVector2f { X = 32, Y = 0 },
                    Colour = uint.MaxValue
                },
                new()
                {
                    Position = new RmlUiLegacyVector2f { X = 0, Y = 32 },
                    Colour = uint.MaxValue
                }
            ];
            Span<int> indices = [0, 1, 2];
            var translation = new RmlUiLegacyVector2f();

            Assert.True(_fixture.Graphics.BeginFrame());
            try
            {
                var renderContext = _fixture.RenderDevice.GetRenderContext();
                renderContext.SetSwapChainRenderTargets();
                renderContext.SetFullViewport();
                renderer.BeginRendering();
                fixed (RmlUiLegacyVertex* vertexData = vertices)
                fixed (int* indexData = indices)
                {
                    renderer.RenderGeometry(
                        (nint)vertexData, vertices.Length, (nint)indexData, indices.Length, 0, (nint)(&translation));
                }
                renderer.EndRendering();
            }
            finally
            {
                _fixture.Graphics.EndFrame();
            }
        }
        finally
        {
            RbfxTestData.RemoveResourceDirs(_fixture.Resources);
        }
    }
}
