// Copyright (c) 2008-2022 the Urho3D project.
// Copyright (c) 2022-2026 the rbfx project.
// This work is licensed under the terms of the MIT license.

namespace RedBolt.Scene2D;

using RedBolt.Graphics2D;
using RedBolt.Math;
using RedBolt.Resource;

/// <summary>Tile map orientation.</summary>
public enum Orientation2D { Orthogonal, Isometric, Staggered, Hexagonal }

/// <summary>Tile map information.</summary>
public struct TileMapInfo2D
{
    public Orientation2D Orientation;
    public int Width;
    public int Height;
    public float TileWidth;
    public float TileHeight;

    public readonly float MapWidth => Width * TileWidth;
    public readonly float MapHeight => Orientation switch
    {
        Orientation2D.Staggered => (Height + 1) * 0.5f * TileHeight,
        Orientation2D.Hexagonal => Height * 0.5f * (TileHeight + TileHeight * 0.5f),
        _ => Height * TileHeight,
    };

    public readonly Vector2f ConvertPosition(Vector2f position) => Orientation switch
    {
        Orientation2D.Isometric => ConvertIsometricPosition(position),
        _ => new Vector2f(position.X * Sprite2D.PixelSize, MapHeight - position.Y * Sprite2D.PixelSize),
    };

    public readonly Vector2f TileIndexToPosition(int x, int y) => Orientation switch
    {
        Orientation2D.Isometric => new Vector2f((Width + x - y - 1) * TileWidth * 0.5f,
            (Height * 2 - x - y - 2) * TileHeight * 0.5f),
        Orientation2D.Staggered => new Vector2f((x + (y % 2 == 0 ? 0.0f : 0.5f)) * TileWidth,
            (Height - 1 - y) * 0.5f * TileHeight),
        Orientation2D.Hexagonal => new Vector2f((x + (y % 2 == 0 ? 0.0f : 0.5f)) * TileWidth,
            (Height - 1 - y) * 0.75f * TileHeight),
        _ => new Vector2f(x * TileWidth, (Height - 1 - y) * TileHeight),
    };

    public readonly bool PositionToTileIndex(ref int x, ref int y, Vector2f position)
    {
        switch (Orientation)
        {
            case Orientation2D.Isometric:
                var ox = position.X / TileWidth - Height * 0.5f;
                var oy = position.Y / TileHeight;
                x = (int)(Width - oy + ox);
                y = (int)(Height - oy - ox);
                break;
            case Orientation2D.Staggered:
                y = (int)(Height - 1 - position.Y * 2.0f / TileHeight);
                x = (int)(position.X / TileWidth - (y % 2 == 0 ? 0.0f : 0.5f));
                break;
            case Orientation2D.Hexagonal:
                y = (int)(Height - 1 - position.Y / 0.75f / TileHeight);
                x = (int)(position.X / TileWidth - (y % 2 == 0 ? 0.0f : 0.75f));
                break;
            default:
                x = (int)(position.X / TileWidth);
                y = Height - 1 - (int)(position.Y / TileHeight);
                break;
        }
        return x >= 0 && x < Width && y >= 0 && y < Height;
    }

    private readonly Vector2f ConvertIsometricPosition(Vector2f position)
    {
        var index = position * Sprite2D.PixelSize / TileHeight;
        return new Vector2f((Width + index.X - index.Y) * TileWidth * 0.5f,
            (Height * 2.0f - index.X - index.Y) * TileHeight * 0.5f);
    }
}

public enum TileMapLayerType2D { TileLayer, ObjectGroup, ImageLayer, Invalid = 0xffff }
public enum TileMapObjectType2D { Rectangle, Ellipse, Polygon, Polyline, Tile, Invalid = 0xffff }

/// <summary>String property set loaded from Tiled.</summary>
public class PropertySet2D
{
    private readonly Dictionary<string, string> _properties = [];

    public void Load(XMLElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (element.Node is global::System.Xml.Linq.XElement xmlElement)
            Load(xmlElement);
    }

    internal void Load(global::System.Xml.Linq.XElement element)
    {
        foreach (var property in element.Elements("property"))
            _properties[(string?)property.Attribute("name") ?? string.Empty] =
                (string?)property.Attribute("value") ?? property.Value;
    }
    public bool HasProperty(string name) => _properties.ContainsKey(name);
    public string GetProperty(string name) => _properties.GetValueOrDefault(name, string.Empty);
}

/// <summary>Tile definition.</summary>
public class Tile2D
{
    public const uint FlipHorizontal = 0x80000000u;
    public const uint FlipVertical = 0x40000000u;
    public const uint FlipDiagonal = 0x20000000u;
    public const uint FlipReserved = 0x10000000u;
    public const uint FlipAll = FlipHorizontal | FlipVertical | FlipDiagonal | FlipReserved;

    internal uint RawGid;
    internal PropertySet2D? PropertySet;
    public uint Gid => RawGid & ~FlipAll;
    public bool FlipX => (RawGid & FlipHorizontal) != 0;
    public bool FlipY => (RawGid & FlipVertical) != 0;
    public bool SwapXY => (RawGid & FlipDiagonal) != 0;
    public Sprite2D? Sprite { get; internal set; }
    public bool HasProperty(string name) => PropertySet?.HasProperty(name) == true;
    public string GetProperty(string name) => PropertySet?.GetProperty(name) ?? string.Empty;
}

/// <summary>Object from a Tiled object group.</summary>
public class TileMapObject2D
{
    internal uint RawGid;
    internal PropertySet2D? PropertySet;
    internal readonly List<Vector2f> Points = [];
    public TileMapObjectType2D ObjectType { get; internal set; }
    public string Name { get; internal set; } = string.Empty;
    public string Type { get; internal set; } = string.Empty;
    public Vector2f Position { get; internal set; }
    public Vector2f Size { get; internal set; }
    public uint NumPoints => (uint)Points.Count;
    public Vector2f GetPoint(uint index) => index < Points.Count ? Points[(int)index] : Vector2f.Zero;
    public uint TileGid => RawGid & ~Tile2D.FlipAll;
    public bool TileFlipX => (RawGid & Tile2D.FlipHorizontal) != 0;
    public bool TileFlipY => (RawGid & Tile2D.FlipVertical) != 0;
    public bool TileSwapXY => (RawGid & Tile2D.FlipDiagonal) != 0;
    public Sprite2D? TileSprite { get; internal set; }
    public bool HasProperty(string name) => PropertySet?.HasProperty(name) == true;
    public string GetProperty(string name) => PropertySet?.GetProperty(name) ?? string.Empty;
}
