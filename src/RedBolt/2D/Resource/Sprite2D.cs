// Copyright (c) 2008-2022 the Urho3D project.
// Copyright (c) 2022-2026 the rbfx project.
// This work is licensed under the terms of the MIT license.

namespace RedBolt.Resource;

using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.IO;
using RedBolt.Math;
using RedBolt.RenderAPI;

/// <summary>2D sprite resource.</summary>
[Reflected]
public partial class Sprite2D : Resource
{
    public const float PixelSize = 0.01f;

    private Texture2D? _texture;
    private Texture2D? _loadTexture;
    private IntRect _rectangle;
    private Vector2f _hotSpot = new(0.5f, 0.5f);
    private Vector2i _offset;
    private WeakReference<SpriteSheet2D>? _spriteSheet;
    private float _edgeOffset = MathDefs.LargeEpsilon;

    public override void Dispose()
    {
        if (!ReferenceEquals(_loadTexture, _texture))
            _loadTexture?.Dispose();
        _loadTexture = null;
        _texture = null;
        _spriteSheet = null;
        base.Dispose();
    }

    public override bool BeginLoad(Deserializer source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (string.IsNullOrEmpty(Name))
            SetName(source.Name);
        _loadTexture = _texture ?? new Texture2D();
        _loadTexture.SetName(Name);
        if (_loadTexture.BeginLoad(source))
            return true;
        if (ReferenceEquals(_loadTexture, _texture))
            _texture = null;
        _loadTexture = null;
        return false;
    }

    public override bool EndLoad()
    {
        if (_loadTexture is null || !_loadTexture.EndLoad())
        {
            if (ReferenceEquals(_loadTexture, _texture))
                _texture = null;
            _loadTexture = null;
            return false;
        }
        Texture = _loadTexture;
        Rectangle = new IntRect(0, 0, _texture!.Width, _texture.Height);
        _loadTexture = null;
        return true;
    }

    public Texture2D? Texture
    {
        get => _texture;
        set
        {
            _texture = value;
            if (value?.GetAddressMode(TextureCoordinate.U) == TextureAddressMode.Wrap)
            {
                value.SetAddressMode(TextureCoordinate.U, TextureAddressMode.Clamp);
                value.SetAddressMode(TextureCoordinate.V, TextureAddressMode.Clamp);
            }
        }
    }

    public IntRect Rectangle { get => _rectangle; set => _rectangle = value; }
    public Vector2f HotSpot { get => _hotSpot; set => _hotSpot = value; }
    public Vector2i Offset { get => _offset; set => _offset = value; }
    public float TextureEdgeOffset { get => _edgeOffset; set => _edgeOffset = value; }
    public SpriteSheet2D? SpriteSheet => _spriteSheet is not null && _spriteSheet.TryGetTarget(out var value) ? value : null;

    public bool GetDrawRectangle(out Rect rect, bool flipX = false, bool flipY = false) =>
        GetDrawRectangle(out rect, _hotSpot, flipX, flipY);

    public bool GetDrawRectangle(out Rect rect, Vector2f hotSpot, bool flipX = false, bool flipY = false)
    {
        rect = default;
        if (_rectangle.Width == 0 || _rectangle.Height == 0)
            return false;
        var width = _rectangle.Width * PixelSize;
        var height = _rectangle.Height * PixelSize;
        var hotSpotX = flipX ? 1.0f - hotSpot.X : hotSpot.X;
        var hotSpotY = flipY ? 1.0f - hotSpot.Y : hotSpot.Y;
        rect.Min = new Vector2f(-width * hotSpotX, -height * hotSpotY);
        rect.Max = new Vector2f(width * (1.0f - hotSpotX), height * (1.0f - hotSpotY));
        return true;
    }

    public bool GetTextureRectangle(out Rect rect, bool flipX = false, bool flipY = false)
    {
        rect = default;
        if (_texture is null || _texture.Width == 0 || _texture.Height == 0)
            return false;
        var inverseWidth = 1.0f / _texture.Width;
        var inverseHeight = 1.0f / _texture.Height;
        rect.Min.X = (_rectangle.Left + _edgeOffset) * inverseWidth;
        rect.Max.X = (_rectangle.Right - _edgeOffset) * inverseWidth;
        rect.Min.Y = (_rectangle.Bottom - _edgeOffset) * inverseHeight;
        rect.Max.Y = (_rectangle.Top + _edgeOffset) * inverseHeight;
        if (flipX)
            (rect.Min.X, rect.Max.X) = (rect.Max.X, rect.Min.X);
        if (flipY)
            (rect.Min.Y, rect.Max.Y) = (rect.Max.Y, rect.Min.Y);
        return true;
    }

    public static ResourceRef SaveToResourceRef(Sprite2D? sprite)
    {
        if (sprite?.SpriteSheet is not { } sheet)
            return GetResourceRef(sprite, typeof(Sprite2D).GetTypeId());
        return new ResourceRef(typeof(SpriteSheet2D).GetTypeId(), $"{sheet.Name}@{sprite.Name}");
    }

    public static Sprite2D? LoadFromResourceRef(RedObject owner, ResourceRef value)
    {
        ArgumentNullException.ThrowIfNull(owner);
        var cache = owner.GetSubsystem<ResourceCache>();
        if (value.Type == typeof(Sprite2D).GetTypeId())
            return cache?.GetResource<Sprite2D>(value.Name);
        if (value.Type != typeof(SpriteSheet2D).GetTypeId())
            return null;
        var separator = value.Name.LastIndexOf('@');
        if (separator <= 0 || separator == value.Name.Length - 1)
            return null;
        return cache?.GetResource<SpriteSheet2D>(value.Name[..separator])?.GetSprite(value.Name[(separator + 1)..]);
    }

    /// <summary>Set the sprite sheet that owns this sprite.</summary>
    public void SetSpriteSheet(SpriteSheet2D? sheet) =>
        _spriteSheet = sheet is null ? null : new WeakReference<SpriteSheet2D>(sheet);
}
