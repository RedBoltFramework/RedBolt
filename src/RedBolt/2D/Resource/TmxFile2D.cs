// Copyright (c) 2008-2022 the Urho3D project.
// Copyright (c) 2022-2026 the rbfx project.
// This work is licensed under the terms of the MIT license.

namespace RedBolt.Resource;

using System.Buffers.Binary;
using System.Globalization;
using System.Xml.Linq;
using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.IO;
using RedBolt.Math;
using RedBolt.Scene2D;

/// <summary>Base class for layers loaded from a TMX file.</summary>
public class TmxLayer2D
{
    public TmxLayer2D(TmxFile2D tmxFile, TileMapLayerType2D type) { TmxFile = tmxFile; LayerType = type; }
    public TmxFile2D TmxFile { get; }
    public TileMapLayerType2D LayerType { get; }
    public string Name { get; protected set; } = string.Empty;
    public int Width { get; protected set; }
    public int Height { get; protected set; }
    public bool IsVisible { get; protected set; } = true;
    protected PropertySet2D? PropertySet;
    public bool HasProperty(string name) => PropertySet?.HasProperty(name) == true;
    public string GetProperty(string name) => PropertySet?.GetProperty(name) ?? string.Empty;

    protected void LoadInfo(XElement element)
    {
        Name = Attribute(element, "name");
        Width = AttributeInt(element, "width");
        Height = AttributeInt(element, "height");
        IsVisible = element.Attribute("visible") is null || AttributeInt(element, "visible") != 0;
        if (element.Element("properties") is { } properties)
        {
            PropertySet = new PropertySet2D();
            PropertySet.Load(properties);
        }
    }

    internal static string Attribute(XElement element, string name) => (string?)element.Attribute(name) ?? string.Empty;
    internal static int AttributeInt(XElement element, string name) =>
        int.TryParse(Attribute(element, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;
    internal static uint AttributeUInt(XElement element, string name) =>
        uint.TryParse(Attribute(element, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;
    internal static float AttributeFloat(XElement element, string name) =>
        float.TryParse(Attribute(element, name), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0.0f;
}

/// <summary>TMX tile layer.</summary>
public class TmxTileLayer2D : TmxLayer2D
{
    private Tile2D?[] _tiles = [];
    public TmxTileLayer2D(TmxFile2D tmxFile) : base(tmxFile, TileMapLayerType2D.TileLayer) { }

    public bool Load(XMLElement element, TileMapInfo2D info)
    {
        ArgumentNullException.ThrowIfNull(element);
        return element.Node is XElement xmlElement && Load(xmlElement);
    }

    internal bool Load(XElement element)
    {
        LoadInfo(element);
        var data = element.Element("data");
        if (data is null || data.Attribute("compression") is not null)
            return false;

        var gids = new List<uint>(Width * Height);
        var encoding = Attribute(data, "encoding");
        if (string.IsNullOrEmpty(encoding) || encoding == "xml")
            gids.AddRange(data.Elements("tile").Select(tile => AttributeUInt(tile, "gid")));
        else if (encoding == "csv")
        {
            foreach (var value in data.Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                gids.Add(uint.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var gid) ? gid : 0u);
        }
        else if (encoding == "base64")
        {
            try
            {
                var bytes = Convert.FromBase64String(string.Concat(data.Value.Where(character => !char.IsWhiteSpace(character))));
                for (var offset = 0; offset + 4 <= bytes.Length; offset += 4)
                    gids.Add(BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4)));
            }
            catch (FormatException) { return false; }
        }
        else
            return false;

        if (gids.Count < Width * Height)
            return false;
        _tiles = new Tile2D?[Width * Height];
        for (var index = 0; index < _tiles.Length; ++index)
        {
            var rawGid = gids[index];
            if (rawGid == 0)
                continue;
            var gid = rawGid & ~Tile2D.FlipAll;
            _tiles[index] = new Tile2D
            {
                RawGid = rawGid,
                Sprite = TmxFile.GetTileSprite(gid),
                PropertySet = TmxFile.GetTilePropertySet(gid),
            };
        }
        return true;
    }

    public Tile2D? GetTile(int x, int y) => x < 0 || x >= Width || y < 0 || y >= Height ? null : _tiles[y * Width + x];
}

/// <summary>TMX object group.</summary>
public class TmxObjectGroup2D : TmxLayer2D
{
    private readonly List<TileMapObject2D> _objects = [];
    public TmxObjectGroup2D(TmxFile2D tmxFile) : base(tmxFile, TileMapLayerType2D.ObjectGroup) { }
    public uint NumObjects => (uint)_objects.Count;
    public TileMapObject2D? GetObject(uint index) => index < _objects.Count ? _objects[(int)index] : null;

    public bool Load(XMLElement element, TileMapInfo2D info)
    {
        ArgumentNullException.ThrowIfNull(element);
        return element.Node is XElement xmlElement && Load(xmlElement, info);
    }

    public void StoreObject(XMLElement element, TileMapObject2D value, TileMapInfo2D info, bool isTile = false)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(value);
        if (element.Node is XElement xmlElement)
            StoreObject(xmlElement, value, info, isTile);
    }

    internal bool Load(XElement element, TileMapInfo2D info)
    {
        LoadInfo(element);
        foreach (var objectElement in element.Elements("object"))
        {
            var value = new TileMapObject2D();
            StoreObject(objectElement, value, info);
            _objects.Add(value);
        }
        return true;
    }

    internal void StoreObject(XElement element, TileMapObject2D value, TileMapInfo2D info, bool isTile = false)
    {
        value.Name = Attribute(element, "name");
        value.Type = Attribute(element, "type");
        value.ObjectType = element.Attribute("gid") is not null ? TileMapObjectType2D.Tile
            : element.Element("polygon") is not null ? TileMapObjectType2D.Polygon
            : element.Element("polyline") is not null ? TileMapObjectType2D.Polyline
            : element.Element("ellipse") is not null ? TileMapObjectType2D.Ellipse
            : TileMapObjectType2D.Rectangle;
        var position = new Vector2f(AttributeFloat(element, "x"), AttributeFloat(element, "y"));
        var size = new Vector2f(AttributeFloat(element, "width"), AttributeFloat(element, "height"));
        if (isTile)
            position.Y = info.MapHeight / Sprite2D.PixelSize - (size.Y - position.Y);

        switch (value.ObjectType)
        {
            case TileMapObjectType2D.Rectangle:
            case TileMapObjectType2D.Ellipse:
                value.Position = info.ConvertPosition(new Vector2f(position.X, position.Y + size.Y));
                value.Size = size * Sprite2D.PixelSize;
                break;
            case TileMapObjectType2D.Tile:
                value.Position = info.ConvertPosition(position);
                value.RawGid = AttributeUInt(element, "gid");
                value.TileSprite = TmxFile.GetTileSprite(value.TileGid);
                value.Size = element.Attribute("width") is not null || element.Attribute("height") is not null
                    ? size * Sprite2D.PixelSize
                    : value.TileSprite is { } sprite
                        ? new Vector2f(sprite.Rectangle.Width, sprite.Rectangle.Height) * Sprite2D.PixelSize
                        : Vector2f.Zero;
                break;
            case TileMapObjectType2D.Polygon:
            case TileMapObjectType2D.Polyline:
                var pointElement = element.Element(value.ObjectType == TileMapObjectType2D.Polygon ? "polygon" : "polyline");
                foreach (var token in Attribute(pointElement!, "points").Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    var coordinates = token.Split(',');
                    if (coordinates.Length == 2 && float.TryParse(coordinates[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
                        && float.TryParse(coordinates[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
                        value.Points.Add(info.ConvertPosition(position + new Vector2f(x, y)));
                }
                value.Position = value.Points.Count == 0 ? info.ConvertPosition(position) : value.Points[0];
                break;
        }
        if (element.Element("properties") is { } properties)
        {
            value.PropertySet = new PropertySet2D();
            value.PropertySet.Load(properties);
        }
    }
}

/// <summary>TMX image layer.</summary>
public class TmxImageLayer2D : TmxLayer2D
{
    public TmxImageLayer2D(TmxFile2D tmxFile) : base(tmxFile, TileMapLayerType2D.ImageLayer) { }
    public Vector2f Position { get; private set; }
    public string Source { get; private set; } = string.Empty;
    public Sprite2D? Sprite { get; private set; }

    public bool Load(XMLElement element, TileMapInfo2D info)
    {
        ArgumentNullException.ThrowIfNull(element);
        return element.Node is XElement xmlElement && Load(xmlElement, info);
    }

    internal bool Load(XElement element, TileMapInfo2D info)
    {
        LoadInfo(element);
        var image = element.Element("image");
        if (image is null)
            return false;
        Position = new Vector2f(AttributeFloat(element, "offsetx") * Sprite2D.PixelSize,
            info.MapHeight - AttributeFloat(element, "offsety") * Sprite2D.PixelSize);
        Source = Attribute(image, "source");
        var texture = TmxFile.LoadTexture(Source);
        if (texture is null)
            return false;
        Sprite = new Sprite2D
        {
            Texture = texture,
            Rectangle = new IntRect(0, 0, texture.Width, texture.Height),
            HotSpot = new Vector2f(0.0f, 1.0f),
            TextureEdgeOffset = TmxFile.SpriteTextureEdgeOffset,
        };
        return true;
    }
}

/// <summary>Tiled TMX resource.</summary>
[Reflected]
public partial class TmxFile2D : Resource
{
    private XMLFile? _loadXml;
    private readonly Dictionary<uint, Sprite2D> _sprites = [];
    private readonly Dictionary<uint, PropertySet2D> _propertySets = [];
    private readonly Dictionary<uint, List<TileMapObject2D>> _collisionShapes = [];
    private readonly List<TmxLayer2D> _layers = [];
    private float _edgeOffset;

    public TileMapInfo2D Info { get; private set; }
    public uint NumLayers => (uint)_layers.Count;
    public float SpriteTextureEdgeOffset
    {
        get => _edgeOffset;
        set { _edgeOffset = value; foreach (var sprite in _sprites.Values) sprite.TextureEdgeOffset = value; }
    }

    public override void Dispose()
    {
        _loadXml?.Dispose();
        _loadXml = null;
        foreach (var sprite in _sprites.Values)
            sprite.Dispose();
        _sprites.Clear();
        _propertySets.Clear();
        _collisionShapes.Clear();
        _layers.Clear();
        base.Dispose();
    }

    public override bool BeginLoad(Deserializer source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (string.IsNullOrEmpty(Name))
            SetName(source.Name);
        _loadXml?.Dispose();
        _loadXml = new XMLFile();
        if (!_loadXml.Load(source) || _loadXml.Document.Root?.Name.LocalName != "map")
        {
            _loadXml.Dispose();
            _loadXml = null;
            return false;
        }
        SetMemoryUse(checked((int)source.Size));
        return true;
    }

    public override bool EndLoad()
    {
        var root = _loadXml?.Document.Root;
        if (root is null)
            return false;
        var orientation = (string?)root.Attribute("orientation");
        var orientationValue = orientation switch
        {
            "orthogonal" => Orientation2D.Orthogonal,
            "isometric" => Orientation2D.Isometric,
            "staggered" => Orientation2D.Staggered,
            "hexagonal" => Orientation2D.Hexagonal,
            _ => (Orientation2D?)null,
        };
        if (orientationValue is null)
            return FinishLoad(false);
        Info = new TileMapInfo2D
        {
            Orientation = orientationValue.Value,
            Width = TmxLayer2D.AttributeInt(root, "width"),
            Height = TmxLayer2D.AttributeInt(root, "height"),
            TileWidth = TmxLayer2D.AttributeFloat(root, "tilewidth") * Sprite2D.PixelSize,
            TileHeight = TmxLayer2D.AttributeFloat(root, "tileheight") * Sprite2D.PixelSize,
        };
        foreach (var sprite in _sprites.Values)
            sprite.Dispose();
        _sprites.Clear();
        _propertySets.Clear();
        _collisionShapes.Clear();
        _layers.Clear();

        foreach (var child in root.Elements())
        {
            var success = child.Name.LocalName switch
            {
                "tileset" => LoadTileSet(child),
                "layer" => LoadTileLayer(child),
                "objectgroup" => LoadObjectGroup(child),
                "imagelayer" => LoadImageLayer(child),
                _ => true,
            };
            if (!success)
                return FinishLoad(false);
        }
        return FinishLoad(true);
    }

    public bool SetInfo(Orientation2D orientation, int width, int height, float tileWidth, float tileHeight)
    {
        if (_layers.Count != 0)
            return false;
        Info = new TileMapInfo2D { Orientation = orientation, Width = width, Height = height,
            TileWidth = tileWidth * Sprite2D.PixelSize, TileHeight = tileHeight * Sprite2D.PixelSize };
        return true;
    }
    public void AddLayer(uint index, TmxLayer2D layer)
    {
        ArgumentNullException.ThrowIfNull(layer);
        if (index > _layers.Count) _layers.Add(layer); else _layers.Insert((int)index, layer);
    }
    public void AddLayer(TmxLayer2D layer) { ArgumentNullException.ThrowIfNull(layer); _layers.Add(layer); }
    public Sprite2D? GetTileSprite(uint gid) => _sprites.GetValueOrDefault(gid);
    public IReadOnlyList<TileMapObject2D> GetTileCollisionShapes(uint gid) =>
        _collisionShapes.TryGetValue(gid, out var shapes) ? shapes : Array.Empty<TileMapObject2D>();
    public PropertySet2D? GetTilePropertySet(uint gid) => _propertySets.GetValueOrDefault(gid);
    public TmxLayer2D? GetLayer(uint index) => index < _layers.Count ? _layers[(int)index] : null;

    internal Texture2D? LoadTexture(string source) => GetSubsystem<ResourceCache>()?.GetResource<Texture2D>(ParentPath(Name) + source);

    private bool LoadTileLayer(XElement child)
    {
        var layer = new TmxTileLayer2D(this);
        if (!layer.Load(child)) return false;
        _layers.Add(layer);
        return true;
    }
    private bool LoadObjectGroup(XElement child)
    {
        var layer = new TmxObjectGroup2D(this);
        if (!layer.Load(child, Info)) return false;
        _layers.Add(layer);
        return true;
    }
    private bool LoadImageLayer(XElement child)
    {
        var layer = new TmxImageLayer2D(this);
        if (!layer.Load(child, Info)) return false;
        _layers.Add(layer);
        return true;
    }

    private bool LoadTileSet(XElement reference)
    {
        var firstGid = TmxLayer2D.AttributeUInt(reference, "firstgid");
        XElement tileSet;
        if (reference.Attribute("source") is { } sourceAttribute)
        {
            var file = GetSubsystem<ResourceCache>()?.GetTempResource<XMLFile>(ParentPath(Name) + sourceAttribute.Value);
            tileSet = file?.Document.Root is { Name.LocalName: "tileset" } root ? new XElement(root) : null!;
            file?.Dispose();
            if (tileSet is null)
                return false;
        }
        else
            tileSet = reference;

        var tileWidth = TmxLayer2D.AttributeInt(tileSet, "tilewidth");
        var tileHeight = TmxLayer2D.AttributeInt(tileSet, "tileheight");
        var spacing = TmxLayer2D.AttributeInt(tileSet, "spacing");
        var margin = TmxLayer2D.AttributeInt(tileSet, "margin");
        if (tileSet.Element("image") is { } atlasImage)
        {
            var texture = LoadTexture(TmxLayer2D.Attribute(atlasImage, "source"));
            if (texture is null || tileWidth <= 0 || tileHeight <= 0)
                return false;
            var imageWidth = TmxLayer2D.AttributeInt(atlasImage, "width");
            var imageHeight = TmxLayer2D.AttributeInt(atlasImage, "height");
            if (imageWidth == 0) imageWidth = texture.Width;
            if (imageHeight == 0) imageHeight = texture.Height;
            var hotSpot = Vector2f.Zero;
            if (tileSet.Element("tileoffset") is { } offset)
                hotSpot = new Vector2f(TmxLayer2D.AttributeFloat(offset, "x") / tileWidth,
                    TmxLayer2D.AttributeFloat(offset, "y") / tileHeight);
            var gid = firstGid;
            for (var y = margin; y + tileHeight <= imageHeight - margin; y += tileHeight + spacing)
            for (var x = margin; x + tileWidth <= imageWidth - margin; x += tileWidth + spacing)
                _sprites[gid++] = NewSprite(texture, new IntRect(x, y, x + tileWidth, y + tileHeight), hotSpot);
        }

        foreach (var tileElement in tileSet.Elements("tile"))
        {
            var gid = firstGid + TmxLayer2D.AttributeUInt(tileElement, "id");
            if (tileElement.Element("image") is { } image)
            {
                var texture = LoadTexture(TmxLayer2D.Attribute(image, "source"));
                if (texture is null)
                    return false;
                _sprites[gid] = NewSprite(texture, new IntRect(0, 0, texture.Width, texture.Height), Vector2f.Zero);
                tileWidth = texture.Width;
                tileHeight = texture.Height;
            }
            if (tileElement.Element("objectgroup") is { } collisionGroup)
            {
                var parser = new TmxObjectGroup2D(this);
                var shapes = new List<TileMapObject2D>();
                foreach (var objectElement in collisionGroup.Elements("object"))
                {
                    var shape = new TileMapObject2D();
                    var clone = new XElement(objectElement);
                    var y = TmxLayer2D.AttributeFloat(clone, "y");
                    clone.SetAttributeValue("y", Info.MapHeight / Sprite2D.PixelSize - (tileHeight - y));
                    parser.StoreObject(clone, shape, Info);
                    shapes.Add(shape);
                }
                _collisionShapes[gid] = shapes;
            }
            if (tileElement.Element("properties") is { } properties)
            {
                var propertySet = new PropertySet2D();
                propertySet.Load(properties);
                _propertySets[gid] = propertySet;
            }
        }
        return true;
    }

    private Sprite2D NewSprite(Texture2D texture, IntRect rectangle, Vector2f hotSpot) => new()
    {
        Texture = texture, Rectangle = rectangle, HotSpot = hotSpot, TextureEdgeOffset = _edgeOffset,
    };
    private bool FinishLoad(bool result)
    {
        _loadXml?.Dispose();
        _loadXml = null;
        return result;
    }
    private static string ParentPath(string path)
    {
        var separator = path.LastIndexOf('/');
        return separator < 0 ? string.Empty : path[..(separator + 1)];
    }
}
