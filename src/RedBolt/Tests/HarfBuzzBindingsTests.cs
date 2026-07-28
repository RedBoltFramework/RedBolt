namespace RedBolt.Tests;

using System;
using System.IO;

using RedBolt.Bindings.HarfBuzz;

public sealed class HarfBuzzBindingsTests
{
    [Fact]
    public void Raster_draw_produces_a8_glyph_image()
    {
        var fontPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "Data", "Fonts", "Anonymous Pro.ttf"));
        Assert.True(File.Exists(fontPath));

        var blob = HarfBuzzNativeBindings.HbBlobCreateFromFileOrFail(fontPath);
        Assert.False(blob.IsNull);
        try
        {
            var face = HarfBuzzNativeBindings.HbFaceCreateOrFail(blob, 0);
            Assert.False(face.IsNull);
            try
            {
                var font = HarfBuzzNativeBindings.HbFontCreate(face);
                Assert.False(font.IsNull);
                try
                {
                    HarfBuzzNativeBindings.HbOtFontSetFuncs(font);
                    HarfBuzzNativeBindings.HbFontSetScale(font, 32 * 64, 32 * 64);
                    Assert.NotEqual(0, HarfBuzzNativeBindings.HbFontGetGlyph(font, 'A', 0, out var glyph));

                    using var glyphExtents = new HbGlyphExtentsT();
                    Assert.NotEqual(0, HarfBuzzNativeBindings.HbFontGetGlyphExtents(font, glyph, glyphExtents));

                    var draw = HarfBuzzNativeBindings.HbRasterDrawCreateOrFail();
                    Assert.False(draw.IsNull);
                    try
                    {
                        Assert.NotEqual(0, HarfBuzzNativeBindings.HbRasterDrawSetGlyphExtents(draw, glyphExtents));
                        Assert.NotEqual(0, HarfBuzzNativeBindings.HbRasterDrawGlyphOrFail(draw, font, glyph));

                        var image = HarfBuzzNativeBindings.HbRasterDrawRender(draw);
                        Assert.False(image.IsNull);
                        try
                        {
                            using var rasterExtents = new HbRasterExtentsT();
                            HarfBuzzNativeBindings.HbRasterImageGetExtents(image, rasterExtents);
                            Assert.Equal(HbRasterFormatT.HbRasterFormatA8,
                                HarfBuzzNativeBindings.HbRasterImageGetFormat(image));
                            Assert.NotEqual(0u, rasterExtents.Width);
                            Assert.NotEqual(0u, rasterExtents.Height);
                            Assert.NotEqual(IntPtr.Zero,
                                HarfBuzzNativeBindings.HbRasterImageGetBuffer(image));
                        }
                        finally
                        {
                            HarfBuzzNativeBindings.HbRasterImageDestroy(image);
                            image.Dispose();
                        }
                    }
                    finally
                    {
                        HarfBuzzNativeBindings.HbRasterDrawDestroy(draw);
                        draw.Dispose();
                    }
                }
                finally
                {
                    HarfBuzzNativeBindings.HbFontDestroy(font);
                    font.Dispose();
                }
            }
            finally
            {
                HarfBuzzNativeBindings.HbFaceDestroy(face);
                face.Dispose();
            }
        }
        finally
        {
            HarfBuzzNativeBindings.HbBlobDestroy(blob);
            blob.Dispose();
        }
    }
}
