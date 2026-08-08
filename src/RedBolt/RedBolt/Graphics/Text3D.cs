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

using SystemMath = global::System.Math;
using System.Text;

using RedBolt.Core;
using RedBolt.Math;
using RedBolt.Resource;
using ResourceType = global::RedBolt.Resource.Resource;

public enum HorizontalAlignment
{
    Left,
    Center,
    Right,
    Custom
}

public enum VerticalAlignment
{
    Top,
    Center,
    Bottom,
    Custom
}

public enum Corner
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight
}

/// <summary>3D text component.</summary>
[Reflected(ObjectCategory.Category_Geometry)]
public sealed partial class Text3D : CustomGeometry
{
    public const float DefaultFontSize = 12.0f;
    private const float TextScaling = 1.0f / 128.0f;

    private sealed class TextLine
    {
        public readonly List<PositionedGlyph> Glyphs = [];
        public float Width;
    }

    private readonly record struct PositionedGlyph(TextFontGlyph Glyph, float X);

    private readonly Technique _textTechnique;
    private readonly Technique _gpuTextTechnique;
    private readonly List<Material> _textMaterials = [];
    private readonly Color[] _colors = [Color.White, Color.White, Color.White, Color.White];
    private readonly List<float> _rowWidths = [];
    private Font? _font;
    private float _fontSize = DefaultFontSize;
    private string _text = string.Empty;
    private HorizontalAlignment _textAlignment;
    private HorizontalAlignment _horizontalAlignment;
    private VerticalAlignment _verticalAlignment;
    private float _rowSpacing = 1.0f;
    private bool _wordWrap;
    private int _width;
    private int _height;
    private int _rowHeight;
    private int _numChars;
    private float _opacity = 1.0f;
    private bool _depthTest = true;

    /// <summary>Construct.</summary>
    public Text3D()
    {
        _textTechnique = new Technique();
        var pass = _textTechnique.CreatePass("alpha");
        pass.SetVertexShader("Text");
        pass.SetPixelShader("Text");
        pass.SetPixelShaderDefines("ALPHAMAP");
        pass.SetBlendMode(BlendMode.Alpha);
        pass.SetDepthWrite(false);

        _gpuTextTechnique = new Technique();
        pass = _gpuTextTechnique.CreatePass("alpha");
        pass.SetVertexShader("HarfBuzzGpuText");
        pass.SetPixelShader("HarfBuzzGpuText");
        pass.SetBlendMode(BlendMode.Alpha);
        pass.SetDepthWrite(false);

        SetNumTextMaterials(1);
        base.SetMaterial(_textMaterials[0]);
    }

    /// <summary>Destruct.</summary>
    public override void Dispose()
    {
        _font = null;
        for (var i = _textMaterials.Count - 1; i >= 0; --i)
            _textMaterials[i].Dispose();
        _textMaterials.Clear();
        _gpuTextTechnique.Dispose();
        _textTechnique.Dispose();
        base.Dispose();
    }

    /// <summary>Set font by looking from resource cache by name and font size. Return true if successful.</summary>
    public bool SetFont(string fontName, float size = DefaultFontSize)
    {
        var font = GetSubsystem<ResourceCache>()!.GetResourceOrNull<Font>(fontName);
        return SetFont(font, size);
    }

    /// <summary>Set font and font size. Return true if successful.</summary>
    public bool SetFont(Font? font, float size = DefaultFontSize)
    {
        _font = font;
        _fontSize = SystemMath.Max(size, 1.0f);
        Rebuild();
        return _font is not null;
    }

    /// <summary>Set font size only while retaining the existing font. Return true if successful.</summary>
    public bool SetFontSize(float size)
    {
        _fontSize = SystemMath.Max(size, 1.0f);
        Rebuild();
        return _font is not null;
    }

    /// <summary>Set text. Text is assumed to be either ASCII or UTF8-encoded.</summary>
    public void SetText(string text)
    {
        _text = text ?? string.Empty;
        Rebuild();
    }

    /// <summary>Set horizontal and vertical alignment.</summary>
    public void SetAlignment(HorizontalAlignment horizontalAlignment, VerticalAlignment verticalAlignment)
    {
        _horizontalAlignment = horizontalAlignment;
        _verticalAlignment = verticalAlignment;
        Rebuild();
    }

    /// <summary>Set horizontal alignment.</summary>
    public void SetHorizontalAlignment(HorizontalAlignment alignment)
    {
        _horizontalAlignment = alignment;
        Rebuild();
    }

    /// <summary>Set vertical alignment.</summary>
    public void SetVerticalAlignment(VerticalAlignment alignment)
    {
        _verticalAlignment = alignment;
        Rebuild();
    }

    /// <summary>Set row alignment.</summary>
    public void SetTextAlignment(HorizontalAlignment alignment)
    {
        _textAlignment = alignment;
        Rebuild();
    }

    /// <summary>Set row spacing, 1.0 for original font spacing.</summary>
    public void SetRowSpacing(float spacing)
    {
        _rowSpacing = spacing;
        Rebuild();
    }

    /// <summary>Set wordwrap.</summary>
    public void SetWordwrap(bool enable)
    {
        _wordWrap = enable;
        Rebuild();
    }

    /// <summary>Set text width.</summary>
    public void SetWidth(int width)
    {
        _width = SystemMath.Max(width, 0);
        Rebuild();
    }

    /// <summary>Set color on all corners.</summary>
    public void SetColor(Color color)
    {
        for (var i = 0; i < _colors.Length; ++i)
            _colors[i] = color;
        Rebuild();
    }

    /// <summary>Set color on one corner.</summary>
    public void SetColor(Corner corner, Color color)
    {
        _colors[(int)corner] = color;
        Rebuild();
    }

    /// <summary>Set opacity.</summary>
    public void SetOpacity(float opacity)
    {
        _opacity = SystemMath.Clamp(opacity, 0.0f, 1.0f);
        Rebuild();
    }

    /// <summary>Set whether to enable the depth test when rendering the text.</summary>
    public void SetDepthTest(bool enable)
    {
        _depthTest = enable;
        _textTechnique.GetPass("alpha")!.SetDepthTestMode(enable ? CompareMode.LessEqual : CompareMode.Always);
        _gpuTextTechnique.GetPass("alpha")!.SetDepthTestMode(enable ? CompareMode.LessEqual : CompareMode.Always);
    }

    /// <summary>Return font.</summary>
    public Font? GetFont() => _font;

    /// <summary>Return font size.</summary>
    public float GetFontSize() => _fontSize;

    /// <summary>Return text.</summary>
    public string GetText() => _text;

    /// <summary>Return row alignment.</summary>
    public HorizontalAlignment GetTextAlignment() => _textAlignment;

    /// <summary>Return horizontal alignment.</summary>
    public HorizontalAlignment GetHorizontalAlignment() => _horizontalAlignment;

    /// <summary>Return vertical alignment.</summary>
    public VerticalAlignment GetVerticalAlignment() => _verticalAlignment;

    /// <summary>Return row spacing.</summary>
    public float GetRowSpacing() => _rowSpacing;

    /// <summary>Return wordwrap mode.</summary>
    public bool GetWordwrap() => _wordWrap;

    /// <summary>Return text width.</summary>
    public int GetWidth() => SystemMath.Max(_width, _rowWidths.Count == 0 ? 0 : (int)MathF.Ceiling(_rowWidths.Max()));

    /// <summary>Return text height.</summary>
    public int GetHeight() => _height;

    /// <summary>Return row height.</summary>
    public int GetRowHeight() => _rowHeight;

    /// <summary>Return number of rows.</summary>
    public int GetNumRows() => _rowWidths.Count;

    /// <summary>Return number of characters.</summary>
    public int GetNumChars() => _numChars;

    /// <summary>Return width of row by index.</summary>
    public int GetRowWidth(int index) => index >= 0 && index < _rowWidths.Count
        ? (int)MathF.Ceiling(_rowWidths[index])
        : 0;

    /// <summary>Return corner color.</summary>
    public Color GetColor(Corner corner) => _colors[(int)corner];

    /// <summary>Return opacity.</summary>
    public float GetOpacity() => _opacity;

    /// <summary>Return whether the depth test is enabled when rendering the text.</summary>
    public bool GetDepthTest() => _depthTest;

    public void SetFontAttr(ResourceRef value)
    {
        SetFont(string.IsNullOrEmpty(value.Name)
            ? null
            : GetSubsystem<ResourceCache>()!.GetResourceOrNull<Font>(value.Name), _fontSize);
    }

    [Serialized("Font", DefaultSource = nameof(GetFontAttributeDefault), Setter = nameof(SetFontAttr))]
    public ResourceRef GetFontAttr() => ResourceType.GetResourceRef(_font, ObjectReflection<Font>.TypeId);

    [Serialized("Font Size")]
    private float SerializedFontSize
    {
        get => _fontSize;
        set => SetFontSize(value);
    }

    [Serialized("Text")]
    public string Text
    {
        get => _text;
        set => SetText(value);
    }

    [Serialized("Text Alignment", EnumNames = ["Left", "Center", "Right", "Custom"])]
    public HorizontalAlignment TextAlignment
    {
        get => _textAlignment;
        set => SetTextAlignment(value);
    }

    [Serialized("Row Spacing")]
    public float RowSpacing
    {
        get => _rowSpacing;
        set => SetRowSpacing(value);
    }

    [Serialized("Word Wrap")]
    public bool Wordwrap
    {
        get => _wordWrap;
        set => SetWordwrap(value);
    }

    [Serialized("Depth Test")]
    public bool DepthTest
    {
        get => _depthTest;
        set => SetDepthTest(value);
    }

    [Serialized("Width")]
    private int SerializedWidth
    {
        get => _width;
        set => SetWidth(value);
    }

    [Serialized("Horiz Alignment", EnumNames = ["Left", "Center", "Right", "Custom"])]
    public HorizontalAlignment HorizontalTextAlignment
    {
        get => _horizontalAlignment;
        set => SetHorizontalAlignment(value);
    }

    [Serialized("Vert Alignment", EnumNames = ["Top", "Center", "Bottom", "Custom"])]
    public VerticalAlignment VerticalTextAlignment
    {
        get => _verticalAlignment;
        set => SetVerticalAlignment(value);
    }

    [Serialized("Opacity")]
    public float Opacity
    {
        get => _opacity;
        set => SetOpacity(value);
    }

    [Serialized("Color", Setter = nameof(SetColor))]
    public Color ColorAttribute => _colors[0];

    [Serialized("Top Left Color")]
    public Color TopLeftColor
    {
        get => _colors[(int)Corner.TopLeft];
        set => SetColor(Corner.TopLeft, value);
    }

    [Serialized("Top Right Color")]
    public Color TopRightColor
    {
        get => _colors[(int)Corner.TopRight];
        set => SetColor(Corner.TopRight, value);
    }

    [Serialized("Bottom Left Color")]
    public Color BottomLeftColor
    {
        get => _colors[(int)Corner.BottomLeft];
        set => SetColor(Corner.BottomLeft, value);
    }

    [Serialized("Bottom Right Color")]
    public Color BottomRightColor
    {
        get => _colors[(int)Corner.BottomRight];
        set => SetColor(Corner.BottomRight, value);
    }

    private void Rebuild()
    {
        var face = _font?.GetFace(_fontSize);
        var numPages = SystemMath.Max(face?.TextureCount ?? 0, 1);
        Clear();
        SetNumGeometries((uint)numPages);
        for (var page = 0; page < numPages; ++page)
            BeginGeometry((uint)page, PrimitiveType.TriangleList);
        _rowWidths.Clear();
        _height = 0;
        _rowHeight = 0;
        _numChars = 0;

        if (face is null || string.IsNullOrEmpty(_text))
        {
            Commit();
            UpdateTextMaterials(face, numPages);
            return;
        }

        var lines = Layout(face);
        var contentWidth = 0.0f;
        for (var i = 0; i < lines.Count; ++i)
        {
            _rowWidths.Add(lines[i].Width);
            contentWidth = SystemMath.Max(contentWidth, lines[i].Width);
        }

        var blockWidth = SystemMath.Max(_width, contentWidth);
        _rowHeight = (int)MathF.Ceiling(face.RowHeight);
        var lineAdvance = _rowHeight * _rowSpacing;
        _height = (int)MathF.Ceiling(lines.Count * lineAdvance);
        var textScale = face.IsSdf ? _fontSize / face.PointSize : 1.0f;
        var originX = _horizontalAlignment switch
        {
            HorizontalAlignment.Center => -blockWidth * 0.5f,
            HorizontalAlignment.Right => -blockWidth,
            _ => 0.0f
        };
        var originY = _verticalAlignment switch
        {
            VerticalAlignment.Center => -_height * 0.5f,
            VerticalAlignment.Bottom => -_height,
            _ => 0.0f
        };

        Texture2D? gpuTexture = null;
        if (!face.IsSdf)
        {
            gpuTexture = face.GetTexture(0);
            if (gpuTexture is null)
            {
                Commit();
                UpdateTextMaterials(null, numPages);
                return;
            }
        }

        for (var page = 0; page < numPages; ++page)
        {
            BeginGeometry((uint)page, PrimitiveType.TriangleList);
            var texture = face.IsSdf ? face.GetTexture(page) : gpuTexture;
            for (var rowIndex = 0; rowIndex < lines.Count; ++rowIndex)
            {
                var line = lines[rowIndex];
                var rowOffset = _textAlignment switch
                {
                    HorizontalAlignment.Center => (blockWidth - line.Width) * 0.5f,
                    HorizontalAlignment.Right => blockWidth - line.Width,
                    _ => 0.0f
                };
                var baseline = originY + rowIndex * lineAdvance + face.PointSize;
                for (var glyphIndex = 0; glyphIndex < line.Glyphs.Count; ++glyphIndex)
                {
                    var positionedGlyph = line.Glyphs[glyphIndex];
                    var glyph = positionedGlyph.Glyph;
                    if (glyph.Page != page || glyph.Width == 0 || glyph.Height == 0 || !face.IsSdf && glyph.AtlasOffset == 0)
                        continue;

                    var left = originX + rowOffset + positionedGlyph.X
                        + (face.IsSdf ? glyph.BearingX : glyph.RenderBearingX);
                    var top = baseline - (face.IsSdf ? glyph.BearingY : glyph.RenderBearingY);
                    AddGlyph(
                        glyph,
                        left,
                        top,
                        originX,
                        originY,
                        blockWidth,
                        _height,
                        texture?.Width ?? 1,
                        texture?.Height ?? 1,
                        textScale,
                        face.IsSdf);
                }
            }
        }

        Commit();
        UpdateTextMaterials(face, numPages, gpuTexture);
    }

    private List<TextLine> Layout(TextFontFace face)
    {
        var lines = new List<TextLine> { new() };
        uint previousGlyph = 0;
        foreach (var rune in _text.EnumerateRunes())
        {
            if (rune.Value == '\r')
                continue;
            if (rune.Value == '\n')
            {
                lines.Add(new TextLine());
                previousGlyph = 0;
                continue;
            }

            ++_numChars;
            var glyph = face.GetGlyph(rune);
            if (glyph is null)
                continue;

            var line = lines[^1];
            var kerning = previousGlyph == 0 ? 0.0f : face.GetKerning(previousGlyph, glyph.Codepoint);
            if (_wordWrap && _width > 0 && line.Glyphs.Count != 0
                && line.Width + kerning + glyph.Advance > _width)
            {
                line = new TextLine();
                lines.Add(line);
                kerning = 0.0f;
            }

            var x = line.Width + kerning;
            line.Glyphs.Add(new PositionedGlyph(glyph, x));
            line.Width = x + glyph.Advance;
            previousGlyph = glyph.Codepoint;
        }
        return lines;
    }

    private void AddGlyph(
        TextFontGlyph glyph,
        float left,
        float top,
        float originX,
        float originY,
        float blockWidth,
        float blockHeight,
        int textureWidth,
        int textureHeight,
        float textScale,
        bool isSdf)
    {
        if (!isSdf)
        {
            AddGpuGlyph(glyph, left, top, originX, originY, blockWidth, blockHeight, textScale);
            return;
        }

        var right = left + glyph.Width;
        var bottom = top + glyph.Height;
        var textureLeft = glyph.AtlasX / (float)textureWidth;
        var textureTop = glyph.AtlasY / (float)textureHeight;
        var textureRight = (glyph.AtlasX + glyph.Width) / (float)textureWidth;
        var textureBottom = (glyph.AtlasY + glyph.Height) / (float)textureHeight;

        AddVertex(left, top, textureLeft, textureTop, textScale,
            GetColor(left, top, originX, originY, blockWidth, blockHeight));
        AddVertex(left, bottom, textureLeft, textureBottom, textScale,
            GetColor(left, bottom, originX, originY, blockWidth, blockHeight));
        AddVertex(right, bottom, textureRight, textureBottom, textScale,
            GetColor(right, bottom, originX, originY, blockWidth, blockHeight));
        AddVertex(left, top, textureLeft, textureTop, textScale,
            GetColor(left, top, originX, originY, blockWidth, blockHeight));
        AddVertex(right, bottom, textureRight, textureBottom, textScale,
            GetColor(right, bottom, originX, originY, blockWidth, blockHeight));
        AddVertex(right, top, textureRight, textureTop, textScale,
            GetColor(right, top, originX, originY, blockWidth, blockHeight));
    }

    private void AddGpuGlyph(
        TextFontGlyph glyph,
        float left,
        float top,
        float originX,
        float originY,
        float blockWidth,
        float blockHeight,
        float textScale)
    {
        var width = glyph.RenderWidth;
        var height = glyph.RenderHeight;
        var paddingX = HarfBuzzGpuUv.GetPadding(width);
        var paddingY = HarfBuzzGpuUv.GetPadding(height);
        var localLeft = -paddingX / width;
        var localTop = -paddingY / height;
        var localRight = 1.0f + paddingX / width;
        var localBottom = 1.0f + paddingY / height;
        left -= paddingX;
        top -= paddingY;
        var right = left + width + 2.0f * paddingX;
        var bottom = top + height + 2.0f * paddingY;

        AddGpuVertex(left, top, glyph.AtlasOffset, localLeft, localTop, textScale,
            GetColor(left, top, originX, originY, blockWidth, blockHeight));
        AddGpuVertex(left, bottom, glyph.AtlasOffset, localLeft, localBottom, textScale,
            GetColor(left, bottom, originX, originY, blockWidth, blockHeight));
        AddGpuVertex(right, bottom, glyph.AtlasOffset, localRight, localBottom, textScale,
            GetColor(right, bottom, originX, originY, blockWidth, blockHeight));
        AddGpuVertex(left, top, glyph.AtlasOffset, localLeft, localTop, textScale,
            GetColor(left, top, originX, originY, blockWidth, blockHeight));
        AddGpuVertex(right, bottom, glyph.AtlasOffset, localRight, localBottom, textScale,
            GetColor(right, bottom, originX, originY, blockWidth, blockHeight));
        AddGpuVertex(right, top, glyph.AtlasOffset, localRight, localTop, textScale,
            GetColor(right, top, originX, originY, blockWidth, blockHeight));
    }

    private void AddGpuVertex(
        float x, float y, int atlasOffset, float localX, float localY, float textScale, Color color)
    {
        DefineVertex(new Vector3f(x * TextScaling * textScale, -y * TextScaling * textScale, 0.0f));
        DefineColor(color);
        DefineTexCoord(HarfBuzzGpuUv.Pack(atlasOffset, localX, localY));
    }

    private void AddVertex(float x, float y, float u, float v, float textScale, Color color)
    {
        DefineVertex(new Vector3f(x * TextScaling * textScale, -y * TextScaling * textScale, 0.0f));
        DefineColor(color);
        DefineTexCoord(new Vector2f(u, v));
    }

    private void UpdateTextMaterials(TextFontFace? face, int count, Texture2D? gpuTexture = null)
    {
        SetNumTextMaterials(count);
        _textTechnique.GetPass("alpha")!.SetPixelShaderDefines(face?.IsSdf == true
            ? "SIGNED_DISTANCE_FIELD"
            : "ALPHAMAP");
        for (var i = 0; i < count; ++i)
        {
            var material = _textMaterials[i];
            var isGpu = face is not null && !face.IsSdf;
            material.SetTechnique(0, isGpu ? _gpuTextTechnique : _textTechnique);
            material.SetTexture("Albedo", isGpu ? null : face?.GetTexture(i));
            material.SetTexture("HarfBuzzAtlas", isGpu ? gpuTexture : null);
            base.SetMaterial((uint)i, material);
        }
    }

    private void SetNumTextMaterials(int count)
    {
        while (_textMaterials.Count < count)
        {
            var material = new Material();
            material.SetTechnique(0, _textTechnique);
            material.SetCullMode(CullMode.None);
            material.SetOcclusion(false);
            _textMaterials.Add(material);
        }
        while (_textMaterials.Count > count)
        {
            _textMaterials[^1].Dispose();
            _textMaterials.RemoveAt(_textMaterials.Count - 1);
        }
    }

    private Color GetColor(
        float x,
        float y,
        float originX,
        float originY,
        float width,
        float height)
    {
        var u = width == 0.0f ? 0.0f : SystemMath.Clamp((x - originX) / width, 0.0f, 1.0f);
        var v = height == 0.0f ? 0.0f : SystemMath.Clamp((y - originY) / height, 0.0f, 1.0f);
        var top = _colors[(int)Corner.TopLeft].Lerp(_colors[(int)Corner.TopRight], u);
        var bottom = _colors[(int)Corner.BottomLeft].Lerp(_colors[(int)Corner.BottomRight], u);
        var color = top.Lerp(bottom, v);
        color.A *= _opacity;
        return color;
    }

    private static object? GetFontAttributeDefault() => new ResourceRef(ObjectReflection<Font>.TypeId);
}
