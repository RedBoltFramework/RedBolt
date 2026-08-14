// Copyright (c) 2008-2022 the Urho3D project.
// Copyright (c) 2022-2026 the rbfx project.
// This work is licensed under the terms of the MIT license.

namespace RedBolt.Resource;

using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.IO;
using RedBolt.Math;

/// <summary>Sprite sheet resource.</summary>
[Reflected]
public partial class SpriteSheet2D : Resource
{
    private readonly Dictionary<string, Sprite2D> _sprites = new(StringComparer.Ordinal);
    private PListFile? _loadPListFile;
    private XMLFile? _loadXmlFile;
    private JSONFile? _loadJsonFile;
    private string _loadTextureName = string.Empty;
    private Texture2D? _texture;

    public override void Dispose()
    {
        _loadPListFile?.Dispose();
        _loadPListFile = null;
        _loadXmlFile?.Dispose();
        _loadXmlFile = null;
        _loadJsonFile?.Dispose();
        _loadJsonFile = null;
        foreach (var sprite in _sprites.Values)
            sprite.Dispose();
        _sprites.Clear();
        _texture = null;
        base.Dispose();
    }

    public override bool BeginLoad(Deserializer source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (string.IsNullOrEmpty(Name))
            SetName(source.Name);
        _sprites.Clear();
        _loadTextureName = string.Empty;
        var extension = Path.GetExtension(source.Name);
        if (string.Equals(extension, ".plist", StringComparison.OrdinalIgnoreCase))
            return BeginLoadPList(source);
        if (string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase))
            return BeginLoadJson(source);
        if (!string.Equals(extension, ".xml", StringComparison.OrdinalIgnoreCase))
            return false;
        _loadXmlFile = new XMLFile();
        if (!_loadXmlFile.Load(source))
        {
            _loadXmlFile.Dispose();
            _loadXmlFile = null;
            return false;
        }
        SetMemoryUse(checked((int)source.Size));
        var root = _loadXmlFile.GetRoot("TextureAtlas");
        if (!root.NotNull)
            return false;
        var parent = Path.GetDirectoryName(Name)?.Replace('\\', '/') ?? string.Empty;
        _loadTextureName = string.IsNullOrEmpty(parent)
            ? root.GetAttribute("imagePath")
            : $"{parent}/{root.GetAttribute("imagePath")}";
        if (AsyncLoadState == AsyncLoadState.Loading)
            GetSubsystem<ResourceCache>()?.BackgroundLoadResource<Texture2D>(_loadTextureName, true, this);
        return true;
    }

    public override bool EndLoad()
    {
        if (_loadPListFile is not null)
            return EndLoadPList();
        if (_loadJsonFile is not null)
            return EndLoadJson();
        if (_loadXmlFile is null)
            return false;
        var cache = GetSubsystem<ResourceCache>();
        _texture = cache?.GetResource<Texture2D>(_loadTextureName);
        if (_texture is null)
            return false;
        var root = _loadXmlFile.GetRoot("TextureAtlas");
        for (var element = root.GetChild("SubTexture"); element.NotNull; element = element.GetNext("SubTexture"))
        {
            var x = element.GetInt("x");
            var y = element.GetInt("y");
            var width = element.GetInt("width");
            var height = element.GetInt("height");
            var offset = Vector2i.Zero;
            var hotSpot = new Vector2f(0.5f, 0.5f);
            if (element.HasAttribute("frameWidth") && element.HasAttribute("frameHeight"))
            {
                offset = new Vector2i(element.GetInt("frameX"), element.GetInt("frameY"));
                hotSpot.X = (offset.X + element.GetInt("frameWidth") * 0.5f) / width;
                hotSpot.Y = 1.0f - (offset.Y + element.GetInt("frameHeight") * 0.5f) / height;
            }
            DefineSprite(element.GetAttribute("name"), new IntRect(x, y, x + width, y + height), hotSpot, offset);
        }
        _loadXmlFile.Dispose();
        _loadXmlFile = null;
        _loadTextureName = string.Empty;
        return true;
    }

    public override bool Save(Serializer destination)
    {
        using var xml = new XMLFile();
        var root = xml.CreateRoot("TextureAtlas");
        return Save(root) && xml.Save(destination);
    }

    public bool Save(XMLElement root)
    {
        if (root.IsNull)
            return false;
        if (_texture is not null)
            root.SetAttribute("imagePath", _texture.Name);
        foreach (var (name, sprite) in _sprites)
        {
            var element = root.CreateChild("SubTexture");
            var rectangle = sprite.Rectangle;
            element.SetAttribute("name", name);
            element.SetInt("x", rectangle.Left);
            element.SetInt("y", rectangle.Top);
            element.SetInt("width", rectangle.Width);
            element.SetInt("height", rectangle.Height);
            element.SetInt("frameX", sprite.Offset.X);
            element.SetInt("frameY", sprite.Offset.Y);
            var hotSpot = new Vector2f(sprite.HotSpot.X, 1.0f - sprite.HotSpot.Y);
            var frame = ((hotSpot * rectangle.Size.ToVector2() - sprite.Offset.ToVector2()) * 2.0f).ToIntVector2();
            element.SetInt("frameWidth", frame.X);
            element.SetInt("frameHeight", frame.Y);
        }
        return true;
    }

    public Texture2D? Texture { get => _texture; set { _loadTextureName = string.Empty; _texture = value; } }
    public IReadOnlyDictionary<string, Sprite2D> SpriteMapping => _sprites;
    public Sprite2D? GetSprite(string name) => _sprites.GetValueOrDefault(name);

    public void DefineSprite(string name, IntRect rectangle, Vector2f? hotSpot = null, Vector2i? offset = null)
    {
        if (_texture is null || _sprites.ContainsKey(name))
            return;
        var sprite = new Sprite2D();
        sprite.SetName(name);
        sprite.Texture = _texture;
        sprite.Rectangle = rectangle;
        sprite.HotSpot = hotSpot ?? new Vector2f(0.5f, 0.5f);
        sprite.Offset = offset ?? Vector2i.Zero;
        sprite.SetSpriteSheet(this);
        _sprites[name] = sprite;
    }

    private bool BeginLoadPList(Deserializer source)
    {
        _loadPListFile = new PListFile();
        if (!_loadPListFile.Load(source))
        {
            _loadPListFile.Dispose();
            _loadPListFile = null;
            return false;
        }
        SetMemoryUse(checked((int)source.Size));
        if (!_loadPListFile.Root.TryGetValue("metadata", out var metadataValue) ||
            !metadataValue.ValueMap.TryGetValue("realTextureFileName", out var textureValue))
            return false;
        _loadTextureName = ParentPath(Name) + textureValue.String;
        if (AsyncLoadState == AsyncLoadState.Loading)
            GetSubsystem<ResourceCache>()?.BackgroundLoadResource<Texture2D>(_loadTextureName, true, this);
        return true;
    }

    private bool EndLoadPList()
    {
        _texture = GetSubsystem<ResourceCache>()?.GetResource<Texture2D>(_loadTextureName);
        if (_texture is null || _loadPListFile is null ||
            !_loadPListFile.Root.TryGetValue("frames", out var framesValue))
            return FinishPList(false);
        foreach (var (fileName, value) in framesValue.ValueMap)
        {
            var info = value.ValueMap;
            if (info.TryGetValue("rotated", out var rotated) && rotated.Bool ||
                !info.TryGetValue("frame", out var frameValue) ||
                !info.TryGetValue("sourceColorRect", out var colorRectValue))
                continue;
            var rectangle = frameValue.IntRect;
            var sourceColorRect = colorRectValue.IntRect;
            var hotSpot = new Vector2f(0.5f, 0.5f);
            var offset = Vector2i.Zero;
            if ((sourceColorRect.Left != 0 || sourceColorRect.Top != 0) &&
                info.TryGetValue("sourceSize", out var sourceSizeValue))
            {
                offset = new Vector2i(-sourceColorRect.Left, -sourceColorRect.Top);
                var sourceSize = sourceSizeValue.IntVector2;
                hotSpot.X = (offset.X + sourceSize.X * 0.5f) / rectangle.Width;
                hotSpot.Y = 1.0f - (offset.Y + sourceSize.Y * 0.5f) / rectangle.Height;
            }
            DefineSprite(Path.GetFileNameWithoutExtension(fileName), rectangle, hotSpot, offset);
        }
        return FinishPList(true);
    }

    private bool FinishPList(bool result)
    {
        _loadPListFile?.Dispose();
        _loadPListFile = null;
        _loadTextureName = string.Empty;
        return result;
    }

    private bool BeginLoadJson(Deserializer source)
    {
        _loadJsonFile = new JSONFile();
        if (!_loadJsonFile.Load(source) || _loadJsonFile.Root.IsNull)
        {
            _loadJsonFile.Dispose();
            _loadJsonFile = null;
            return false;
        }
        SetMemoryUse(checked((int)source.Size));
        _loadTextureName = ParentPath(Name) + _loadJsonFile.Root.Get("imagePath").GetString();
        if (AsyncLoadState == AsyncLoadState.Loading)
            GetSubsystem<ResourceCache>()?.BackgroundLoadResource<Texture2D>(_loadTextureName, true, this);
        return !string.IsNullOrEmpty(_loadTextureName);
    }

    private bool EndLoadJson()
    {
        _texture = GetSubsystem<ResourceCache>()?.GetResource<Texture2D>(_loadTextureName);
        if (_texture is null || _loadJsonFile is null)
            return FinishJson(false);
        foreach (var value in _loadJsonFile.Root.Get("subtextures").GetArray())
        {
            var x = value.Get("x").GetInt();
            var y = value.Get("y").GetInt();
            var width = value.Get("width").GetInt();
            var height = value.Get("height").GetInt();
            if (width <= 0 || height <= 0)
                continue;
            var hotSpot = new Vector2f(0.5f, 0.5f);
            var offset = Vector2i.Zero;
            var frameWidth = value.Get("frameWidth");
            var frameHeight = value.Get("frameHeight");
            if (!frameWidth.IsNull && !frameHeight.IsNull)
            {
                offset = new Vector2i(value.Get("frameX").GetInt(), value.Get("frameY").GetInt());
                hotSpot.X = (offset.X + frameWidth.GetInt() * 0.5f) / width;
                hotSpot.Y = 1.0f - (offset.Y + frameHeight.GetInt() * 0.5f) / height;
            }
            DefineSprite(value.Get("name").GetString(), new IntRect(x, y, x + width, y + height), hotSpot, offset);
        }
        return FinishJson(true);
    }

    private bool FinishJson(bool result)
    {
        _loadJsonFile?.Dispose();
        _loadJsonFile = null;
        _loadTextureName = string.Empty;
        return result;
    }

    private static string ParentPath(string name)
    {
        var parent = Path.GetDirectoryName(name)?.Replace('\\', '/') ?? string.Empty;
        return string.IsNullOrEmpty(parent) ? string.Empty : parent + "/";
    }
}
