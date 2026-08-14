// Copyright (c) 2008-2022 the Urho3D project.
// Copyright (c) 2022-2026 the rbfx project.
// This work is licensed under the terms of the MIT license.

namespace RedBolt.Scene2D;

using RedBolt.Core;
using RedBolt.Graphics2D;
using RedBolt.Math;
using RedBolt.Resource;

/// <summary>Nine-slice stretchable sprite component.</summary>
[Reflected(ObjectCategory.Category_Urho2D, InheritAttributesOrder = 1)]
public partial class StretchableSprite2D : StaticSprite2D
{
    private IntRect _border;

    [Serialized("Border")]
    public IntRect Border { get => _border; set { _border = value; MarkSourceBatchesDirty(); } }

    protected override void UpdateSourceBatches()
    {
        var vertices = SourceBatches[0].Vertices;
        vertices.Clear();
        if (SpriteValue is null)
            return;
        if (!UseTextureRectValue && !SpriteValue.GetTextureRectangle(out TextureRectValue, FlipXValue, FlipYValue))
            return;

        var size = DrawRectValue.Size;
        var left = System.Math.Clamp(_border.Left * Sprite2D.PixelSize, 0.0f, size.X);
        var right = System.Math.Clamp(_border.Right * Sprite2D.PixelSize, 0.0f, size.X - left);
        var bottom = System.Math.Clamp(_border.Bottom * Sprite2D.PixelSize, 0.0f, size.Y);
        var top = System.Math.Clamp(_border.Top * Sprite2D.PixelSize, 0.0f, size.Y - bottom);
        var signedScale = Node.GetSignedWorldScale();
        var xs = PrepareXYCoords(DrawRectValue.Min.X, DrawRectValue.Max.X, left, right, signedScale.X);
        var ys = PrepareXYCoords(DrawRectValue.Min.Y, DrawRectValue.Max.Y, bottom, top, signedScale.Y);
        var us = new[] { TextureRectValue.Min.X, TextureRectValue.Min.X + left / size.X,
            TextureRectValue.Max.X - right / size.X, TextureRectValue.Max.X };
        var vs = new[] { TextureRectValue.Min.Y, TextureRectValue.Min.Y - bottom / size.Y,
            TextureRectValue.Max.Y + top / size.Y, TextureRectValue.Max.Y };
        var inverseWorld = Node.GetWorldTransform().Inverse;
        var worldPosition = Node.GetWorldPosition();
        var worldRotation = Node.GetWorldRotation();
        var color = ColorValue.ToUInt();
        for (var x = 0; x < 3; ++x)
        {
            for (var y = 0; y < 3; ++y)
            {
                if (xs[x] == xs[x + 1] || ys[y] == ys[y + 1])
                    continue;
                var v0 = new Vertex2D { Position = ToLocal(xs[x], ys[y]), Color = color, Uv = new Vector2f(us[x], vs[y]) };
                var v1 = new Vertex2D { Position = ToLocal(xs[x], ys[y + 1]), Color = color, Uv = new Vector2f(us[x], vs[y + 1]) };
                var v2 = new Vertex2D { Position = ToLocal(xs[x + 1], ys[y + 1]), Color = color, Uv = new Vector2f(us[x + 1], vs[y + 1]) };
                var v3 = new Vertex2D { Position = ToLocal(xs[x + 1], ys[y]), Color = color, Uv = new Vector2f(us[x + 1], vs[y]) };
                vertices.AddRange([v0, v1, v2, v0, v2, v3]);
            }
        }

        Vector3f ToLocal(float x, float y) =>
            inverseWorld * (worldPosition + worldRotation * new Vector3f(x, y, 0.0f));
    }

    private static float[] PrepareXYCoords(float low, float high, float lowBorder, float highBorder, float scale)
    {
        var coordinates = new float[4];
        coordinates[0] = low * scale;
        coordinates[3] = high * scale;

        var scaleSign = MathDefs.Sign(scale);
        var borderSize = lowBorder + highBorder;
        if (borderSize > scaleSign * (coordinates[3] - coordinates[0]))
            coordinates[1] = coordinates[2] = scale * (low + lowBorder * (high - low) / borderSize);
        else
        {
            var absoluteScale = MathF.Abs(scale);
            coordinates[1] = (low * absoluteScale + lowBorder) * scaleSign;
            coordinates[2] = (high * absoluteScale - highBorder) * scaleSign;
        }
        return coordinates;
    }
}
