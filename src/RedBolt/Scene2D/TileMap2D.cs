// Copyright (c) 2008-2022 the Urho3D project.
// Copyright (c) 2022-2026 the rbfx project.
// This work is licensed under the terms of the MIT license.

namespace RedBolt.Scene2D;

using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.Scene;
using ResourceType = RedBolt.Resource.Resource;

/// <summary>Tile map component.</summary>
[Reflected(ObjectCategory.Category_Urho2D, InheritAttributesOrder = 1)]
public partial class TileMap2D : Component
{
    private TmxFile2D? _tmxFile;
    private Node? _rootNode;
    private readonly List<TileMapLayer2D> _layers = [];

    [Serialized("Tmx File", Setter = nameof(SetTmxFileAttr))]
    private ResourceRef _tmxFileAttr = new(typeof(TmxFile2D).GetTypeId());

    public TmxFile2D? TmxFile
    {
        get => _tmxFile;
        set
        {
            if (ReferenceEquals(_tmxFile, value))
                return;
            _rootNode?.RemoveAllChildren();
            _layers.Clear();
            _tmxFile = value;
            if (value is null || Node is null)
                return;
            _rootNode ??= Node.CreateTemporaryChild("_root_");
            for (uint index = 0; index < value.NumLayers; ++index)
            {
                var tmxLayer = value.GetLayer(index);
                if (tmxLayer is null)
                    continue;
                var layerNode = _rootNode.CreateTemporaryChild(tmxLayer.Name);
                var layer = layerNode.CreateComponent<TileMapLayer2D>();
                layer.Initialize(this, tmxLayer);
                layer.DrawOrder = (int)index * 10;
                _layers.Add(layer);
            }
        }
    }

    public TileMapInfo2D Info => _tmxFile?.Info ?? default;
    public uint NumLayers => (uint)_layers.Count;
    public TileMapLayer2D? GetLayer(uint index) => index < _layers.Count ? _layers[(int)index] : null;
    public Vector2f TileIndexToPosition(int x, int y) => Info.TileIndexToPosition(x, y);
    public bool PositionToTileIndex(ref int x, ref int y, Vector2f position) => Info.PositionToTileIndex(ref x, ref y, position);
    public IReadOnlyList<TileMapObject2D> GetTileCollisionShapes(uint gid) =>
        _tmxFile?.GetTileCollisionShapes(gid) ?? Array.Empty<TileMapObject2D>();

    public override void DrawDebugGeometry(DebugRenderer? debug, bool depthTest)
    {
        if (debug is null || Node is null)
            return;
        var info = Info;
        var width = info.MapWidth;
        var height = info.MapHeight;
        if (info.Orientation == Orientation2D.Isometric)
        {
            AddDebugLoop(debug, depthTest,
                new Vector2f(0.0f, height * 0.5f), new Vector2f(width * 0.5f, 0.0f),
                new Vector2f(width, height * 0.5f), new Vector2f(width * 0.5f, height));
        }
        else
            AddDebugLoop(debug, depthTest, Vector2f.Zero, new Vector2f(width, 0.0f), new Vector2f(width, height), new Vector2f(0.0f, height));
        foreach (var layer in _layers)
            layer.DrawDebugGeometry(debug, depthTest);
    }

    public void DrawDebugGeometry()
    {
        var debug = Scene?.GetComponent<DebugRenderer>();
        if (debug is not null)
            DrawDebugGeometry(debug, false);
    }

    private void AddDebugLoop(DebugRenderer debug, bool depthTest, params Vector2f[] points)
    {
        var transform = Node.GetWorldTransform();
        for (var index = 0; index < points.Length; ++index)
            debug.AddLine(transform * new Vector3f(points[index].X, points[index].Y, 0.0f),
                transform * new Vector3f(points[(index + 1) % points.Length].X, points[(index + 1) % points.Length].Y, 0.0f),
                Color.Red, depthTest);
    }

    private void SetTmxFileAttr(ResourceRef value)
    {
        _tmxFileAttr = value;
        TmxFile = GetSubsystem<ResourceCache>()?.GetResource<TmxFile2D>(value.Name);
    }
    public ResourceRef GetTmxFileAttr() => ResourceType.GetResourceRef(_tmxFile, typeof(TmxFile2D).GetTypeId());
}
