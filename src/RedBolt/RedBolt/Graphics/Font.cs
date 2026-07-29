//
// Copyright (c) 2008-2022 the Urho3D project.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//

namespace RedBolt.Graphics;

using System.Globalization;
using SystemMath = global::System.Math;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml;
using System.Xml.Linq;

using RedBolt.Bindings.HarfBuzz;
using RedBolt.Core;
using RedBolt.IO;
using RedBolt.RenderAPI;
using TextureAddressMode = global::RedBolt.RenderAPI.TextureAddressMode;
using RedBolt.Resource;
using ResourceType = global::RedBolt.Resource.Resource;

[Reflected]
public sealed partial class Font : ResourceType
{
    private HbBlobT? _blob;
    private HbFaceT? _face;
    private readonly Dictionary<int, TextFontFace> _faces = [];
    private SdfFontData? _sdfData;
    private bool _sdfFont;

    public override void Dispose()
    {
        ReleaseFaces();
        ReleaseFont();
        base.Dispose();
    }

    public override bool BeginLoad(Deserializer source)
    {
        ReleaseFaces();
        ReleaseFont();
        _sdfData = null;
        _sdfFont = false;

        if (string.Equals(Path.GetExtension(Name), ".sdf", StringComparison.OrdinalIgnoreCase))
        {
            _sdfData = SdfFontData.Load(source, Name);
            _sdfFont = _sdfData is not null;
            if (_sdfData is null)
                return false;

            SetMemoryUse(checked((int)source.Size));
            return true;
        }

        _blob = HarfBuzzNativeBindings.HbBlobCreateFromFileOrFail(GetAbsoluteFileName());
        if (_blob.IsNull)
        {
            _blob.Dispose();
            _blob = null;
            return false;
        }

        _face = HarfBuzzNativeBindings.HbFaceCreateOrFail(_blob, 0);
        if (_face.IsNull || HarfBuzzNativeBindings.HbFaceGetUpem(_face) == 0)
        {
            ReleaseFont();
            return false;
        }

        SetMemoryUse(checked((int)source.Size));
        return true;
    }

    public bool IsSdfFont() => _sdfFont;

    internal TextFontFace? GetFace(float pointSize)
    {
        if (_sdfData is not null)
        {
            if (!_faces.TryGetValue(0, out var sdfFace))
            {
                sdfFace = SdfFontFace.Load(_sdfData);
                if (sdfFace is null)
                    return null;
                _faces.Add(0, sdfFace);
            }
            return sdfFace;
        }

        if (_face is null)
            return null;

        var scale = SystemMath.Max(1, (int)MathF.Round(pointSize * HarfBuzzFontFace.HarfBuzzScale));
        if (!_faces.TryGetValue(scale, out var face))
        {
            face = new HarfBuzzFontFace(_face, scale);
            _faces.Add(scale, face);
        }
        return face;
    }

    private void ReleaseFaces()
    {
        foreach (var face in _faces.Values)
            face.Dispose();
        _faces.Clear();
    }

    private void ReleaseFont()
    {
        if (_face is not null)
        {
            if (!_face.IsNull)
                HarfBuzzNativeBindings.HbFaceDestroy(_face);
            _face.Dispose();
            _face = null;
        }

        if (_blob is not null)
        {
            if (!_blob.IsNull)
                HarfBuzzNativeBindings.HbBlobDestroy(_blob);
            _blob.Dispose();
            _blob = null;
        }
    }
}

internal sealed class TextFontGlyph
{
    public uint Codepoint;
    public float Advance;
    public int BearingX;
    public int BearingY;
    public int Width;
    public int Height;
    public int AtlasX;
    public int AtlasY;
    public int Page;
}

internal abstract class TextFontFace : IDisposable
{
    public abstract bool IsSdf { get; }
    public abstract float PointSize { get; }
    public abstract float RowHeight { get; }
    public abstract int TextureCount { get; }
    public abstract TextFontGlyph? GetGlyph(Rune rune);
    public abstract float GetKerning(uint left, uint right);
    public abstract Texture2D? GetTexture(int index);
    public abstract void Dispose();
}

internal sealed class HarfBuzzFontFace : TextFontFace
{
    public const int HarfBuzzScale = 64;
    private const int AtlasSize = 1024;
    private const int AtlasPadding = 1;

    private readonly HbFontT _font;
    private readonly Dictionary<uint, TextFontGlyph> _glyphs = [];
    private readonly byte[] _atlasPixels = new byte[AtlasSize * AtlasSize];
    private Texture2D? _texture;
    private int _atlasX = AtlasPadding;
    private int _atlasY = AtlasPadding;
    private int _atlasRowHeight;

    public HarfBuzzFontFace(HbFaceT face, int scale)
    {
        _font = HarfBuzzNativeBindings.HbFontCreate(face);
        HarfBuzzNativeBindings.HbOtFontSetFuncs(_font);
        HarfBuzzNativeBindings.HbFontSetScale(_font, scale, scale);
        PointSize = scale / (float)HarfBuzzScale;
    }

    public override bool IsSdf => false;

    public override float PointSize { get; }

    public override float RowHeight => PointSize;

    public override int TextureCount => 1;

    public override TextFontGlyph? GetGlyph(Rune rune)
    {
        if (HarfBuzzNativeBindings.HbFontGetGlyph(_font, (uint)rune.Value, 0, out var codepoint) == 0)
            return null;

        if (_glyphs.TryGetValue(codepoint, out var glyph))
        {
            EnsureTexture();
            return glyph;
        }

        glyph = RasterizeGlyph(codepoint);
        _glyphs.Add(codepoint, glyph);
        EnsureTexture();
        if (_texture is not null && glyph.Width != 0 && glyph.Height != 0)
        {
            _texture.SetData(0, glyph.AtlasX, glyph.AtlasY, glyph.Width, glyph.Height,
                GetAtlasRegion(glyph.AtlasX, glyph.AtlasY, glyph.Width, glyph.Height));
        }
        return glyph;
    }

    public override float GetKerning(uint left, uint right)
    {
        return HarfBuzzNativeBindings.HbFontGetGlyphHKerning(_font, left, right) / (float)HarfBuzzScale;
    }

    public override Texture2D? GetTexture(int index)
    {
        if (index != 0)
            return null;
        EnsureTexture();
        return _texture;
    }

    public override void Dispose()
    {
        _texture?.Dispose();
        _texture = null;
        if (!_font.IsNull)
            HarfBuzzNativeBindings.HbFontDestroy(_font);
        _font.Dispose();
        _glyphs.Clear();
    }

    private TextFontGlyph RasterizeGlyph(uint codepoint)
    {
        var glyph = new TextFontGlyph
        {
            Codepoint = codepoint,
            Advance = HarfBuzzNativeBindings.HbFontGetGlyphHAdvance(_font, codepoint) / (float)HarfBuzzScale
        };

        using var glyphExtents = new HbGlyphExtentsT();
        if (HarfBuzzNativeBindings.HbFontGetGlyphExtents(_font, codepoint, glyphExtents) == 0)
            return glyph;

        var draw = HarfBuzzNativeBindings.HbRasterDrawCreateOrFail();
        if (draw.IsNull)
        {
            draw.Dispose();
            return glyph;
        }

        try
        {
            HarfBuzzNativeBindings.HbRasterDrawSetScaleFactor(draw, HarfBuzzScale, HarfBuzzScale);
            if (HarfBuzzNativeBindings.HbRasterDrawSetGlyphExtents(draw, glyphExtents) == 0
                || HarfBuzzNativeBindings.HbRasterDrawGlyphOrFail(draw, _font, codepoint) == 0)
            {
                return glyph;
            }

            var image = HarfBuzzNativeBindings.HbRasterDrawRender(draw);
            if (image.IsNull)
            {
                image.Dispose();
                return glyph;
            }

            try
            {
                if (HarfBuzzNativeBindings.HbRasterImageGetFormat(image) != HbRasterFormatT.HbRasterFormatA8)
                    return glyph;

                using var extents = new HbRasterExtentsT();
                HarfBuzzNativeBindings.HbRasterImageGetExtents(image, extents);
                glyph.BearingX = extents.XOrigin;
                glyph.BearingY = extents.YOrigin + checked((int)extents.Height);
                glyph.Width = checked((int)extents.Width);
                glyph.Height = checked((int)extents.Height);
                if (glyph.Width == 0 || glyph.Height == 0 || !AllocateAtlas(glyph))
                    return glyph;

                CopyGlyph(
                    HarfBuzzNativeBindings.HbRasterImageGetBuffer(image),
                    checked((int)extents.Stride),
                    glyph);
                return glyph;
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

    private bool AllocateAtlas(TextFontGlyph glyph)
    {
        if (_atlasX + glyph.Width + AtlasPadding > AtlasSize)
        {
            _atlasX = AtlasPadding;
            _atlasY += _atlasRowHeight + AtlasPadding;
            _atlasRowHeight = 0;
        }

        if (_atlasY + glyph.Height + AtlasPadding > AtlasSize)
        {
            glyph.Width = 0;
            glyph.Height = 0;
            return false;
        }

        glyph.AtlasX = _atlasX;
        glyph.AtlasY = _atlasY;
        _atlasX += glyph.Width + AtlasPadding;
        _atlasRowHeight = SystemMath.Max(_atlasRowHeight, glyph.Height);
        return true;
    }

    private unsafe void CopyGlyph(IntPtr source, int stride, TextFontGlyph glyph)
    {
        var sourcePixels = new ReadOnlySpan<byte>((void*)source, checked(stride * glyph.Height));
        for (var row = 0; row < glyph.Height; ++row)
        {
            var sourceRow = sourcePixels.Slice((glyph.Height - row - 1) * stride, glyph.Width);
            sourceRow.CopyTo(_atlasPixels.AsSpan(
                (glyph.AtlasY + row) * AtlasSize + glyph.AtlasX,
                glyph.Width));
        }
    }

    private byte[] GetAtlasRegion(int x, int y, int width, int height)
    {
        var result = new byte[checked(width * height)];
        for (var row = 0; row < height; ++row)
        {
            _atlasPixels.AsSpan((y + row) * AtlasSize + x, width)
                .CopyTo(result.AsSpan(row * width, width));
        }
        return result;
    }

    private void EnsureTexture()
    {
        if (_texture is not null || Context.Instance.GetSubsystem<Graphics>()?.GetNativeRenderDevice() is null)
            return;

        _texture = new Texture2D();
        _texture.SetNumLevels(1);
        _texture.SetFilterMode(TextureFilterMode.Bilinear);
        _texture.SetAddressMode(TextureCoordinate.U, TextureAddressMode.Clamp);
        _texture.SetAddressMode(TextureCoordinate.V, TextureAddressMode.Clamp);
        _texture.SetSize(AtlasSize, AtlasSize, TextureFormat.TexFormatR8Unorm);
        _texture.SetData(0, 0, 0, AtlasSize, AtlasSize, _atlasPixels);
    }
}

internal sealed class SdfFontData
{
    public float PointSize;
    public float RowHeight;
    public readonly List<string> Pages = [];
    public readonly Dictionary<uint, TextFontGlyph> Glyphs = [];
    public readonly Dictionary<uint, float> Kernings = [];

    public static SdfFontData? Load(Deserializer source, string resourceName)
    {
        var bytes = new byte[checked((int)source.Size)];
        if (source.Read(bytes) != source.Size)
            return null;

        try
        {
            var document = XDocument.Parse(Encoding.UTF8.GetString(bytes));
            var root = document.Root;
            var info = root?.Element("info");
            var common = root?.Element("common");
            var pages = root?.Element("pages");
            var chars = root?.Element("chars");
            if (root?.Name.LocalName != "font" || info is null || common is null || pages is null || chars is null
                || !TryGetInt(info, "size", out var pointSize)
                || !TryGetInt(common, "lineHeight", out var rowHeight)
                || !TryGetInt(common, "pages", out var pageCount))
            {
                return null;
            }

            var result = new SdfFontData
            {
                PointSize = pointSize,
                RowHeight = rowHeight
            };
            var directory = Path.GetDirectoryName(resourceName)?.Replace('\\', '/');
            foreach (var page in pages.Elements("page"))
            {
                var fileName = (string?)page.Attribute("file");
                if (string.IsNullOrEmpty(fileName))
                    return null;
                result.Pages.Add(string.IsNullOrEmpty(directory) ? fileName : $"{directory}/{fileName}");
            }
            if (result.Pages.Count != pageCount)
                return null;

            foreach (var character in chars.Elements("char"))
            {
                if (!TryGetInt(character, "id", out var id)
                    || !TryGetInt(character, "x", out var x)
                    || !TryGetInt(character, "y", out var y)
                    || !TryGetInt(character, "width", out var width)
                    || !TryGetInt(character, "height", out var height)
                    || !TryGetInt(character, "xoffset", out var xOffset)
                    || !TryGetInt(character, "yoffset", out var yOffset)
                    || !TryGetInt(character, "xadvance", out var xAdvance)
                    || !TryGetInt(character, "page", out var page))
                {
                    return null;
                }

                result.Glyphs[unchecked((uint)id)] = new TextFontGlyph
                {
                    Codepoint = unchecked((uint)id),
                    Advance = xAdvance,
                    BearingX = xOffset,
                    BearingY = pointSize - yOffset,
                    Width = width,
                    Height = height,
                    AtlasX = x,
                    AtlasY = y,
                    Page = page
                };
            }

            var kernings = root.Element("kernings");
            if (kernings is not null)
            {
                foreach (var kerning in kernings.Elements("kerning"))
                {
                    if (!TryGetInt(kerning, "first", out var first)
                        || !TryGetInt(kerning, "second", out var second)
                        || !TryGetFloat(kerning, "amount", out var amount))
                    {
                        return null;
                    }
                    result.Kernings[GetKerningKey(unchecked((uint)first), unchecked((uint)second))] = (int)amount;
                }
            }
            return result;
        }
        catch (XmlException)
        {
            return null;
        }
    }

    public static uint GetKerningKey(uint left, uint right) => unchecked(left << 16 | right);

    private static bool TryGetInt(XElement element, string name, out int value)
    {
        return int.TryParse(
            (string?)element.Attribute(name),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out value);
    }

    private static bool TryGetFloat(XElement element, string name, out float value)
    {
        return float.TryParse(
            (string?)element.Attribute(name),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out value);
    }
}

internal sealed class SdfFontFace : TextFontFace
{
    private readonly SdfFontData _data;
    private readonly Texture2D[] _textures;

    private SdfFontFace(SdfFontData data, Texture2D[] textures)
    {
        _data = data;
        _textures = textures;
    }

    public override bool IsSdf => true;

    public override float PointSize => _data.PointSize;

    public override float RowHeight => _data.RowHeight;

    public override int TextureCount => _textures.Length;

    public static SdfFontFace? Load(SdfFontData data)
    {
        var cache = Context.Instance.GetSubsystem<ResourceCache>()!;
        var textures = new Texture2D[data.Pages.Count];
        for (var i = 0; i < textures.Length; ++i)
        {
            using var image = cache.GetTempResource<Image>(data.Pages[i]);
            if (image is null)
            {
                DisposeTextures(textures, i);
                return null;
            }

            var texture = new Texture2D();
            texture.SetName(data.Pages[i]);
            texture.SetNumLevels(1);
            texture.SetAddressMode(TextureCoordinate.U, TextureAddressMode.Clamp);
            texture.SetAddressMode(TextureCoordinate.V, TextureAddressMode.Clamp);
            if (!texture.SetData(image))
            {
                texture.Dispose();
                DisposeTextures(textures, i);
                return null;
            }
            textures[i] = texture;
        }
        return new SdfFontFace(data, textures);
    }

    public override TextFontGlyph? GetGlyph(Rune rune)
    {
        return _data.Glyphs.GetValueOrDefault(unchecked((uint)rune.Value));
    }

    public override float GetKerning(uint left, uint right)
    {
        return _data.Kernings.GetValueOrDefault(SdfFontData.GetKerningKey(left, right));
    }

    public override Texture2D? GetTexture(int index)
    {
        return index >= 0 && index < _textures.Length ? _textures[index] : null;
    }

    public override void Dispose()
    {
        DisposeTextures(_textures, _textures.Length);
    }

    private static void DisposeTextures(Texture2D[] textures, int count)
    {
        for (var i = count - 1; i >= 0; --i)
            textures[i]?.Dispose();
    }
}
