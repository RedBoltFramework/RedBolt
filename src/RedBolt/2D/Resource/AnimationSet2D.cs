// Copyright (c) 2008-2022 the Urho3D project.
// Copyright (c) 2022-2026 the rbfx project.
// This work is licensed under the terms of the MIT license.

namespace RedBolt.Resource;

using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.IO;
using RedBolt.Math;
using RedBolt.Scene2D.Spriter;

/// <summary>Spriter animation set.</summary>
[Reflected]
public partial class AnimationSet2D : Resource
{
    private readonly Dictionary<uint, Sprite2D> _sprites = [];
    private SpriterData? _spriterData;
    private SpriteSheet2D? _spriteSheet;
    private Sprite2D? _sprite;
    private bool _hasSpriteSheet;
    private string _spriteSheetPath = string.Empty;

    public override void Dispose()
    {
        _sprites.Clear();
        _spriteSheet = null;
        _sprite = null;
        _spriterData = null;
        base.Dispose();
    }

    public override bool BeginLoad(Deserializer source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (string.IsNullOrEmpty(Name))
            SetName(source.Name);
        if (!string.Equals(Path.GetExtension(source.Name), ".scml", StringComparison.OrdinalIgnoreCase) || source.Size == 0)
            return false;
        var data = new byte[checked((int)source.Size)];
        if (source.Read(data) != source.Size)
            return false;
        _spriterData = new SpriterData();
        if (!_spriterData.Load(data))
        {
            _spriterData = null;
            return false;
        }
        var parent = GetParentPath(Name);
        _spriteSheetPath = $"{parent}{Path.GetFileNameWithoutExtension(Name)}.xml";
        var cache = GetSubsystem<ResourceCache>();
        _hasSpriteSheet = cache?.Exists(_spriteSheetPath) == true;
        if (AsyncLoadState == AsyncLoadState.Loading && cache is not null)
        {
            if (_hasSpriteSheet)
                cache.BackgroundLoadResource<SpriteSheet2D>(_spriteSheetPath, true, this);
            else
            {
                foreach (var file in _spriterData.Folders.SelectMany(folder => folder.Files))
                    cache.BackgroundLoadResource<Texture2D>($"{parent}{file.Name}", true, this);
            }
        }
        SetMemoryUse(data.Length);
        return true;
    }

    public override bool EndLoad()
    {
        if (_spriterData is null)
            return false;
        _sprites.Clear();
        _sprite = null;
        var cache = GetSubsystem<ResourceCache>();
        if (cache is null)
            return false;
        var parent = GetParentPath(Name);
        if (_hasSpriteSheet)
        {
            _spriteSheet = cache.GetResource<SpriteSheet2D>(_spriteSheetPath);
            if (_spriteSheet is null)
                return false;
            foreach (var file in _spriterData.Folders.SelectMany(folder => folder.Files))
            {
                var sprite = _spriteSheet.GetSprite(Path.GetFileNameWithoutExtension(file.Name));
                if (sprite is null)
                    return false;
                var hotSpot = new Vector2f(file.PivotX, file.PivotY);
                if (sprite.Offset != Vector2i.Zero)
                {
                    var pivotX = file.Width * hotSpot.X;
                    var pivotY = file.Height * (1.0f - hotSpot.Y);
                    hotSpot.X = (sprite.Offset.X + pivotX) / sprite.Rectangle.Width;
                    hotSpot.Y = 1.0f - (sprite.Offset.Y + pivotY) / sprite.Rectangle.Height;
                }
                sprite.HotSpot = hotSpot;
                AddSprite(file, sprite);
            }
        }
        else
        {
            foreach (var file in _spriterData.Folders.SelectMany(folder => folder.Files))
            {
                var texture = cache.GetResource<Texture2D>($"{parent}{file.Name}");
                if (texture is null)
                    return false;
                var sprite = new Sprite2D
                {
                    Texture = texture,
                    Rectangle = new IntRect(0, 0, texture.Width, texture.Height),
                    HotSpot = new Vector2f(file.PivotX, file.PivotY),
                };
                AddSprite(file, sprite);
            }
        }
        return _sprites.Count != 0;
    }

    public uint NumAnimations => _spriterData?.Entities.FirstOrDefault()?.Animations.Count is { } count ? (uint)count : 0;
    public string GetAnimation(uint index) => _spriterData?.Entities.FirstOrDefault()?.Animations.ElementAtOrDefault((int)index)?.Name ?? string.Empty;
    public bool HasAnimation(string name) => _spriterData?.Entities.Any(entity => entity.Animations.Any(animation => animation.Name == name)) == true;
    public Sprite2D? Sprite => _sprite;
    public SpriterData? SpriterData => _spriterData;
    public Sprite2D? GetSpriterFileSprite(int folderId, int fileId) => _sprites.GetValueOrDefault(MakeKey(folderId, fileId));

    private void AddSprite(SpriterFile file, Sprite2D sprite)
    {
        _sprite ??= sprite;
        _sprites[MakeKey(file.Folder.Id, file.Id)] = sprite;
    }
    private static uint MakeKey(int folderId, int fileId) => unchecked((uint)((folderId << 16) | (fileId & 0xffff)));
    private static string GetParentPath(string name)
    {
        var parent = Path.GetDirectoryName(name)?.Replace('\\', '/') ?? string.Empty;
        return string.IsNullOrEmpty(parent) ? string.Empty : parent + "/";
    }
}
