// Copyright (c) 2008-2022 the Urho3D project.
// Copyright (c) 2022-2026 the rbfx project.
// This work is licensed under the terms of the MIT license.

namespace RedBolt.Graphics2D;

using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.Scene;

/// <summary>2D vertex.</summary>
public struct Vertex2D : IEquatable<Vertex2D>
{
    public Vector3f Position;
    public uint Color;
    public Vector2f Uv;

    public readonly bool Equals(Vertex2D other) => Position == other.Position && Color == other.Color && Uv == other.Uv;
    public override readonly bool Equals(object? obj) => obj is Vertex2D other && Equals(other);
    public override readonly int GetHashCode() => HashCode.Combine(Position, Color, Uv);
}

/// <summary>2D source batch.</summary>
public sealed class SourceBatch2D
{
    public Drawable2D? Owner;
    public float Distance;
    public int DrawOrder;
    public Material? Material;
    public List<Vertex2D> Vertices { get; } = [];
}

/// <summary>Base class for 2D visible components.</summary>
[Reflected(ObjectCategory.Category_Urho2D, Factory = false, InheritAttributesOrder = 1)]
public abstract partial class Drawable2D : Drawable
{
    private WeakReference<Renderer2D>? _renderer;
    private int _layer;
    private int _orderInLayer;

    protected readonly List<SourceBatch2D> SourceBatches = [];
    protected bool SourceBatchesDirty = true;

    protected Drawable2D()
        : base(DrawableFlags.Geometry2D)
    {
    }

    public override void Dispose()
    {
        if (_renderer is not null && _renderer.TryGetTarget(out var renderer))
            renderer.RemoveDrawable(this);
        SourceBatches.Clear();
        base.Dispose();
    }

    [Serialized("Layer")]
    public int Layer
    {
        get => _layer;
        set
        {
            if (_layer == value)
                return;
            _layer = value;
            OnDrawOrderChanged();
        }
    }

    [Serialized("Order in Layer")]
    public int OrderInLayer
    {
        get => _orderInLayer;
        set
        {
            if (_orderInLayer == value)
                return;
            _orderInLayer = value;
            OnDrawOrderChanged();
        }
    }

    public IReadOnlyList<SourceBatch2D> GetSourceBatches()
    {
        if (SourceBatchesDirty)
        {
            UpdateSourceBatches();
            SourceBatchesDirty = false;
        }
        return SourceBatches;
    }

    protected int DrawOrder => unchecked((_layer << 16) | _orderInLayer);
    protected Renderer2D? Renderer => _renderer is not null && _renderer.TryGetTarget(out var value) ? value : null;
    protected abstract void OnDrawOrderChanged();
    protected abstract void UpdateSourceBatches();

    protected void MarkSourceBatchesDirty()
    {
        SourceBatchesDirty = true;
        _worldBoundingBoxDirty = true;
    }

    protected override void OnSceneSet(Scene? previousScene, Scene? scene)
    {
        // Do not call Drawable.OnSceneSet. 2D drawables are collected by Renderer2D instead of the octree.
        if (previousScene is not null && _renderer is not null && _renderer.TryGetTarget(out var previousRenderer))
            previousRenderer.RemoveDrawable(this);
        if (scene is not null)
        {
            var renderer = scene.GetOrCreateComponent<Renderer2D>();
            _renderer = new WeakReference<Renderer2D>(renderer);
            if (IsEnabledEffective)
                renderer.AddDrawable(this);
        }
        else
            _renderer = null;
    }

    public override void OnSetEnabled()
    {
        if (_renderer is null || !_renderer.TryGetTarget(out var renderer))
            return;
        if (IsEnabledEffective)
            renderer.AddDrawable(this);
        else
            renderer.RemoveDrawable(this);
    }

    protected override void OnMarkedDirty(Node node)
    {
        base.OnMarkedDirty(node);
        MarkSourceBatchesDirty();
    }

    protected override void OnWorldBoundingBoxUpdate()
    {
        var boundingBox = new BoundingBox();
        boundingBox.Clear();
        foreach (var batch in GetSourceBatches())
        {
            foreach (var vertex in batch.Vertices)
                boundingBox.Merge(vertex.Position);
        }
        _boundingBox = boundingBox;
        SetWorldBoundingBox(!boundingBox.Defined || TryGetNode() is null
            ? boundingBox
            : boundingBox.Transformed(Node.GetWorldTransform()));
    }
}
