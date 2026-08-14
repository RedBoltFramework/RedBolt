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

using Microsoft.Extensions.Logging;
using System.Globalization;
using SystemMath = global::System.Math;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml;
using System.Xml.Linq;

using RedBolt.Bindings.HarfBuzz;
using RedBolt.Core;
using RedBolt.IO;
using RedBolt.Logging;
using RedBolt.RenderAPI;
using TextureAddressMode = global::RedBolt.RenderAPI.TextureAddressMode;
using RedBolt.Resource;
using ResourceType = global::RedBolt.Resource.Resource;

[Reflected]
public sealed partial class Font : ResourceType
{
    private HbBlobT? _blob;
    private HbFaceT? _face;
    private HbFontT? _gpuFont;
    private HbGpuDrawT? _gpuDraw;
    private HarfBuzzGpuAtlas? _gpuAtlas;
    private bool _gpuTextureFailureLogged;
    private readonly Dictionary<uint, HarfBuzzGpuGlyph?> _gpuGlyphs = [];
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

        _blob = HarfBuzz.HbBlobCreateFromFileOrFail(GetAbsoluteFileName());
        if (_blob.IsNull)
        {
            _blob.Dispose();
            _blob = null;
            return false;
        }

        _face = HarfBuzz.HbFaceCreateOrFail(_blob, 0);
        if (_face.IsNull)
        {
            ReleaseFont();
            return false;
        }
        UnitsPerEm = checked((int)HarfBuzz.HbFaceGetUpem(_face));
        if (UnitsPerEm == 0)
        {
            ReleaseFont();
            return false;
        }

        _gpuFont = HarfBuzz.HbFontCreate(_face);
        if (_gpuFont.IsNull)
        {
            Log.GetLogger().LogError("HarfBuzz GPU font initialization failed for font {Font}", Name);
            ReleaseFont();
            return false;
        }
        HarfBuzz.HbOtFontSetFuncs(_gpuFont);
        HarfBuzz.HbFontSetScale(_gpuFont, UnitsPerEm, UnitsPerEm);
        _gpuDraw = HarfBuzz.HbGpuDrawCreateOrFail();
        if (_gpuDraw.IsNull)
        {
            Log.GetLogger().LogError("HarfBuzz GPU encoder initialization failed for font {Font}", Name);
            ReleaseFont();
            return false;
        }
        _gpuAtlas = new HarfBuzzGpuAtlas();

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
            face = new HarfBuzzFontFace(this, _face, scale);
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
        _gpuGlyphs.Clear();
        _gpuTextureFailureLogged = false;
        _gpuAtlas?.Dispose();
        _gpuAtlas = null;
        if (_gpuDraw is not null)
        {
            if (!_gpuDraw.IsNull)
                HarfBuzz.HbGpuDrawDestroy(_gpuDraw);
            _gpuDraw.Dispose();
            _gpuDraw = null;
        }
        if (_gpuFont is not null)
        {
            if (!_gpuFont.IsNull)
                HarfBuzz.HbFontDestroy(_gpuFont);
            _gpuFont.Dispose();
            _gpuFont = null;
        }

        if (_face is not null)
        {
            if (!_face.IsNull)
                HarfBuzz.HbFaceDestroy(_face);
            _face.Dispose();
            _face = null;
        }

        if (_blob is not null)
        {
            if (!_blob.IsNull)
                HarfBuzz.HbBlobDestroy(_blob);
            _blob.Dispose();
            _blob = null;
        }
        UnitsPerEm = 0;
    }

    internal int UnitsPerEm { get; private set; }

    internal Texture2D? GetGpuTexture()
    {
        var texture = _gpuAtlas?.GetTexture();
        if (texture is null && !_gpuTextureFailureLogged)
        {
            _gpuTextureFailureLogged = true;
            Log.GetLogger().LogError(
                "HarfBuzz GPU atlas creation failed for Text3D font {Font}; GPU text geometry is suppressed", Name);
        }
        return texture;
    }

    internal unsafe HarfBuzzGpuGlyph? GetGpuGlyph(uint codepoint)
    {
        if (_gpuGlyphs.TryGetValue(codepoint, out var cached))
            return cached;
        if (_gpuDraw is null || _gpuFont is null || _gpuAtlas is null
            || HarfBuzz.HbGpuDrawGlyphOrFail(_gpuDraw, _gpuFont, codepoint) == 0)
        {
            return CacheGpuFailure(codepoint, "outline encoding failed");
        }

        using var extents = new HbGlyphExtentsT();
        var blob = HarfBuzz.HbGpuDrawEncode(_gpuDraw, extents);
        if (blob.IsNull)
        {
            blob.Dispose();
            return CacheGpuFailure(codepoint, "blob encoding failed");
        }

        try
        {
            var length = checked((int)HarfBuzz.HbBlobGetLength(blob));
            var offset = 0;
            if (length != 0)
            {
                var data = HarfBuzz.HbBlobGetData(blob, out var dataLength);
                if (data == IntPtr.Zero || dataLength != (uint)length || length % HarfBuzzGpuAtlas.TexelSize != 0
                    || !_gpuAtlas.TryAppend(new ReadOnlySpan<byte>((void*)data, length), out offset))
                {
                    return CacheGpuFailure(codepoint, "atlas overflow or malformed blob");
                }
            }

            var glyph = new HarfBuzzGpuGlyph(
                codepoint, offset, extents.XBearing, extents.YBearing, extents.Width, extents.Height);
            _gpuGlyphs.Add(codepoint, glyph);
            return glyph;
        }
        finally
        {
            HarfBuzz.HbGpuDrawRecycleBlob(_gpuDraw, blob);
            blob.Dispose();
        }
    }

    private HarfBuzzGpuGlyph? CacheGpuFailure(uint codepoint, string reason)
    {
        Log.GetLogger().LogError(
            "HarfBuzz GPU glyph failure for font {Font}, glyph {Glyph}, renderer Text3D: {Reason}",
            Name, codepoint, reason);
        _gpuGlyphs[codepoint] = null;
        return null;
    }
}

internal sealed record HarfBuzzGpuGlyph(
    uint Codepoint, int AtlasOffset, int XBearing, int YBearing, int Width, int Height);

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
    public int AtlasOffset;
    public float RenderBearingX;
    public float RenderBearingY;
    public float RenderWidth;
    public float RenderHeight;
}

internal readonly record struct TextShapedGlyph(
    uint GlyphId, float XAdvance, float XOffset, float YOffset);

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

    private readonly Font _owner;
    private readonly HbFontT _font;
    private readonly Dictionary<uint, TextFontGlyph> _glyphs = [];

    public HarfBuzzFontFace(Font owner, HbFaceT face, int scale)
    {
        _owner = owner;
        _font = HarfBuzz.HbFontCreate(face);
        HarfBuzz.HbOtFontSetFuncs(_font);
        HarfBuzz.HbFontSetScale(_font, scale, scale);
        PointSize = scale / (float)HarfBuzzScale;
    }

    public override bool IsSdf => false;

    public override float PointSize { get; }

    public override float RowHeight => PointSize;

    public override int TextureCount => 1;

    public override TextFontGlyph? GetGlyph(Rune rune)
    {
        if (HarfBuzz.HbFontGetGlyph(_font, (uint)rune.Value, 0, out var codepoint) == 0)
            return null;

        return GetGlyph(codepoint);
    }

    public TextFontGlyph? GetGlyph(uint codepoint)
    {
        if (_glyphs.TryGetValue(codepoint, out var glyph))
            return glyph;

        glyph = CreateGlyph(codepoint);
        if (glyph is null)
            return null;
        _glyphs.Add(codepoint, glyph);
        return glyph;
    }

    public List<TextShapedGlyph> Shape(string text)
    {
        var utf8 = Encoding.UTF8.GetBytes(text);
        var buffer = HarfBuzz.HbBufferCreate();
        HbGlyphInfoT? infos = null;
        HbGlyphPositionT? positions = null;
        try
        {
            HarfBuzz.HbBufferAddUtf8(
                buffer, MemoryMarshal.Cast<byte, sbyte>(utf8), utf8.Length, 0, utf8.Length);
            HarfBuzz.HbBufferGuessSegmentProperties(buffer);
            HarfBuzz.HbShape(_font, buffer, null!, 0);

            infos = HarfBuzz.HbBufferGetGlyphInfos(buffer, out var infoCount);
            positions = HarfBuzz.HbBufferGetGlyphPositions(buffer, out var positionCount);
            var count = checked((int)SystemMath.Min(infoCount, positionCount));
            var result = new List<TextShapedGlyph>(count);
            for (var i = 0; i < count; ++i)
            {
                var info = i == 0 ? infos : infos.GetArrayElement((nuint)i);
                var position = i == 0 ? positions : positions.GetArrayElement((nuint)i);
                result.Add(new TextShapedGlyph(
                    (uint)info.Codepoint,
                    (int)position.XAdvance / (float)HarfBuzzScale,
                    (int)position.XOffset / (float)HarfBuzzScale,
                    -(int)position.YOffset / (float)HarfBuzzScale));
                if (i != 0)
                {
                    position.Dispose();
                    info.Dispose();
                }
            }
            return result;
        }
        finally
        {
            positions?.Dispose();
            infos?.Dispose();
            if (!buffer.IsNull)
                HarfBuzz.HbBufferDestroy(buffer);
            buffer.Dispose();
        }
    }

    public override float GetKerning(uint left, uint right)
    {
        return HarfBuzz.HbFontGetGlyphHKerning(_font, left, right) / (float)HarfBuzzScale;
    }

    public override Texture2D? GetTexture(int index)
    {
        if (index != 0)
            return null;
        return _owner.GetGpuTexture();
    }

    public override void Dispose()
    {
        if (!_font.IsNull)
            HarfBuzz.HbFontDestroy(_font);
        _font.Dispose();
        _glyphs.Clear();
    }

    private TextFontGlyph? CreateGlyph(uint codepoint)
    {
        var gpuGlyph = _owner.GetGpuGlyph(codepoint);
        if (gpuGlyph is null)
            return null;

        var outlineScale = PointSize / _owner.UnitsPerEm;
        var renderWidth = gpuGlyph.Width * outlineScale;
        var renderHeight = -gpuGlyph.Height * outlineScale;
        var glyph = new TextFontGlyph
        {
            Codepoint = codepoint,
            Advance = HarfBuzz.HbFontGetGlyphHAdvance(_font, codepoint) / (float)HarfBuzzScale,
            AtlasOffset = gpuGlyph.AtlasOffset,
            RenderBearingX = gpuGlyph.XBearing * outlineScale,
            RenderBearingY = gpuGlyph.YBearing * outlineScale,
            RenderWidth = renderWidth,
            RenderHeight = renderHeight,
            BearingX = (int)MathF.Round(gpuGlyph.XBearing * outlineScale),
            BearingY = (int)MathF.Round(gpuGlyph.YBearing * outlineScale),
            Width = SystemMath.Max(0, (int)MathF.Ceiling(renderWidth)),
            Height = SystemMath.Max(0, (int)MathF.Ceiling(renderHeight))
        };
        return glyph;
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
