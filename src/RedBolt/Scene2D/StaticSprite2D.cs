// Copyright (c) 2008-2022 the Urho3D project.
// Copyright (c) 2022-2026 the rbfx project.
// This work is licensed under the terms of the MIT license.

namespace RedBolt.Scene2D;

using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Graphics2D;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.Scene;

/// <summary>Static sprite component.</summary>
[Reflected(ObjectCategory.Category_Urho2D, InheritAttributesOrder = 1)]
public partial class StaticSprite2D : Drawable2D
{
    protected Sprite2D? SpriteValue;
    protected BlendMode BlendModeValue = BlendMode.Alpha;
    protected bool FlipXValue;
    protected bool FlipYValue;
    protected bool SwapXYValue;
    protected Color ColorValue = Color.White;
    protected bool UseHotSpotValue;
    protected bool UseDrawRectValue;
    protected bool UseTextureRectValue;
    protected Vector2f HotSpotValue = new(0.5f, 0.5f);
    protected Rect DrawRectValue;
    protected Rect TextureRectValue;
    protected Material? CustomMaterialValue;

    public StaticSprite2D()
    {
        SourceBatches.Add(new SourceBatch2D { Owner = this });
    }

    public Sprite2D? Sprite
    {
        get => SpriteValue;
        set
        {
            if (ReferenceEquals(SpriteValue, value))
                return;
            SpriteValue = value;
            UpdateDrawRect();
            UpdateMaterial();
            MarkSourceBatchesDirty();
        }
    }

    [Serialized("Sprite", Setter = nameof(SetSpriteAttr))]
    private ResourceRef _spriteAttr = new(typeof(Sprite2D).GetTypeId());

    [Serialized("Blend Mode")]
    public BlendMode BlendMode { get => BlendModeValue; set { BlendModeValue = value; UpdateMaterial(); MarkSourceBatchesDirty(); } }
    [Serialized("Flip X")]
    public bool FlipX { get => FlipXValue; set => SetFlip(value, FlipYValue, SwapXYValue); }
    [Serialized("Flip Y")]
    public bool FlipY { get => FlipYValue; set => SetFlip(FlipXValue, value, SwapXYValue); }
    public bool SwapXY { get => SwapXYValue; set => SetFlip(FlipXValue, FlipYValue, value); }
    [Serialized("Color")]
    public Color Color { get => ColorValue; set { ColorValue = value; MarkSourceBatchesDirty(); } }
    public float Alpha { get => ColorValue.A; set { ColorValue.A = value; MarkSourceBatchesDirty(); } }
    [Serialized("Hot Spot")]
    public Vector2f HotSpot { get => HotSpotValue; set { HotSpotValue = value; UpdateDrawRect(); MarkSourceBatchesDirty(); } }
    [Serialized("Use Hot Spot")]
    public bool UseHotSpot { get => UseHotSpotValue; set { UseHotSpotValue = value; UpdateDrawRect(); MarkSourceBatchesDirty(); } }
    [Serialized("Draw Rectangle")]
    public Rect DrawRect { get => DrawRectValue; set { DrawRectValue = value; if (UseDrawRectValue) MarkSourceBatchesDirty(); } }
    [Serialized("Use Draw Rectangle")]
    public bool UseDrawRect { get => UseDrawRectValue; set { UseDrawRectValue = value; UpdateDrawRect(); MarkSourceBatchesDirty(); } }
    [Serialized("Texture Rectangle")]
    public Rect TextureRect { get => TextureRectValue; set { TextureRectValue = value; if (UseTextureRectValue) MarkSourceBatchesDirty(); } }
    [Serialized("Use Texture Rectangle")]
    public bool UseTextureRect { get => UseTextureRectValue; set { UseTextureRectValue = value; MarkSourceBatchesDirty(); } }

    public Material? CustomMaterial
    {
        get => CustomMaterialValue;
        set { CustomMaterialValue = value; UpdateMaterial(); MarkSourceBatchesDirty(); }
    }

    [Serialized("Custom material", Setter = nameof(SetCustomMaterialAttr))]
    private ResourceRef _customMaterialAttr = new(typeof(Material).GetTypeId());

    public void SetFlip(bool flipX, bool flipY, bool swapXY = false)
    {
        if (FlipXValue == flipX && FlipYValue == flipY && SwapXYValue == swapXY)
            return;
        FlipXValue = flipX;
        FlipYValue = flipY;
        SwapXYValue = swapXY;
        MarkSourceBatchesDirty();
    }

    protected override void OnDrawOrderChanged()
    {
        SourceBatches[0].DrawOrder = DrawOrder;
        MarkSourceBatchesDirty();
    }

    protected override void UpdateSourceBatches()
    {
        var vertices = SourceBatches[0].Vertices;
        vertices.Clear();
        if (SpriteValue is null)
            return;
        if (!UseTextureRectValue && !SpriteValue.GetTextureRectangle(out TextureRectValue, FlipXValue, FlipYValue))
            return;
        var min = DrawRectValue.Min;
        var max = DrawRectValue.Max;
        var uvMin = TextureRectValue.Min;
        var uvMax = TextureRectValue.Max;
        var uv0 = uvMin;
        var uv1 = SwapXYValue ? new Vector2f(uvMax.X, uvMin.Y) : new Vector2f(uvMin.X, uvMax.Y);
        var uv2 = uvMax;
        var uv3 = SwapXYValue ? new Vector2f(uvMin.X, uvMax.Y) : new Vector2f(uvMax.X, uvMin.Y);
        var color = ColorValue.ToUInt();
        var v0 = new Vertex2D { Position = new Vector3f(min.X, min.Y, 0), Color = color, Uv = uv0 };
        var v1 = new Vertex2D { Position = new Vector3f(min.X, max.Y, 0), Color = color, Uv = uv1 };
        var v2 = new Vertex2D { Position = new Vector3f(max.X, max.Y, 0), Color = color, Uv = uv2 };
        var v3 = new Vertex2D { Position = new Vector3f(max.X, min.Y, 0), Color = color, Uv = uv3 };
        vertices.AddRange([v0, v1, v2, v0, v2, v3]);
    }

    protected override void OnSceneSet(Scene? previousScene, Scene? scene)
    {
        base.OnSceneSet(previousScene, scene);
        UpdateMaterial();
    }

    protected void UpdateMaterial()
    {
        SourceBatches[0].Material = CustomMaterialValue ??
            (SpriteValue?.Texture is { } texture && Renderer is { } renderer
                ? renderer.GetMaterial(texture, BlendModeValue)
                : null);
    }

    protected void UpdateDrawRect()
    {
        if (UseDrawRectValue || SpriteValue is null)
            return;
        if (UseHotSpotValue)
            SpriteValue.GetDrawRectangle(out DrawRectValue, HotSpotValue, FlipXValue, FlipYValue);
        else
            SpriteValue.GetDrawRectangle(out DrawRectValue, FlipXValue, FlipYValue);
    }

    private void SetSpriteAttr(ResourceRef value)
    {
        _spriteAttr = value;
        Sprite = Sprite2D.LoadFromResourceRef(this, value);
    }
    public ResourceRef GetSpriteAttr() => Sprite2D.SaveToResourceRef(SpriteValue);

    private void SetCustomMaterialAttr(ResourceRef value)
    {
        _customMaterialAttr = value;
        CustomMaterial = GetSubsystem<ResourceCache>()?.GetResource<Material>(value.Name);
    }
    public ResourceRef GetCustomMaterialAttr() => Resource.GetResourceRef(CustomMaterialValue, typeof(Material).GetTypeId());
}
