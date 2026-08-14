// Copyright (c) 2008-2022 the Urho3D project.
// Copyright (c) 2022-2026 the rbfx project.
// This work is licensed under the terms of the MIT license.

namespace RedBolt.Scene2D;

using RedBolt.Core;
using RedBolt.Graphics2D;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.Scene;
using RedBolt.Scene2D.Spriter;

/// <summary>Animation loop mode.</summary>
public enum LoopMode2D { Default, ForceLooped, ForceClamped }

/// <summary>Animated Spriter sprite component.</summary>
[Reflected(ObjectCategory.Category_Urho2D, InheritAttributesOrder = 1)]
public partial class AnimatedSprite2D : StaticSprite2D
{
    private AnimationSet2D? _animationSet;
    private SpriterInstance? _spriterInstance;
    private string _entity = string.Empty;
    private string _animationName = string.Empty;
    private LoopMode2D _loopMode;
    private float _speed = 1.0f;

    [Serialized("Animation Set", Setter = nameof(SetAnimationSetAttr))]
    private ResourceRef _animationSetAttr = new(typeof(AnimationSet2D).GetTypeId());

    public override void Dispose()
    {
        _spriterInstance = null;
        _animationSet = null;
        base.Dispose();
    }

    public override void OnSetEnabled()
    {
        base.OnSetEnabled();
        if (Scene is null)
            return;
        if (IsEnabledEffective)
            SubscribeToEvent<SceneEvents.ScenePostUpdateEvent>(Scene, HandleScenePostUpdate);
        else
            UnsubscribeFromEvent<SceneEvents.ScenePostUpdateEvent>();
    }

    public AnimationSet2D? AnimationSet
    {
        get => _animationSet;
        set
        {
            if (ReferenceEquals(_animationSet, value))
                return;
            _spriterInstance = null;
            _animationSet = value;
            Sprite = value?.Sprite;
            _animationName = string.Empty;
            _loopMode = LoopMode2D.Default;
            if (value?.SpriterData is not { } data)
                return;
            _spriterInstance = new SpriterInstance(this, data);
            if (data.Entities.Count != 0)
            {
                if (string.IsNullOrEmpty(_entity))
                    _entity = data.Entities[0].Name;
                _spriterInstance.SetEntity(_entity);
            }
        }
    }

    [Serialized("Entity")]
    public string Entity
    {
        get => _entity;
        set
        {
            if (_entity == value)
                return;
            _entity = value;
            _spriterInstance?.SetEntity(value);
        }
    }

    [Serialized("Animation", Setter = nameof(SetAnimationAttr))]
    public string Animation { get => _animationName; private set => _animationName = value; }

    [Serialized("Loop Mode")]
    public LoopMode2D LoopMode { get => _loopMode; set { _loopMode = value; if (!string.IsNullOrEmpty(_animationName)) SetAnimation(_animationName, value); } }

    [Serialized("Speed")]
    public float Speed { get => _speed; set => _speed = value; }

    public void SetAnimation(string name, LoopMode2D loopMode = LoopMode2D.Default)
    {
        _animationName = name;
        _loopMode = loopMode;
        if (_animationSet?.HasAnimation(name) != true || _spriterInstance is null)
            return;
        if (string.IsNullOrEmpty(_entity) && _animationSet.SpriterData?.Entities.FirstOrDefault() is { } entity)
            _entity = entity.Name;
        _spriterInstance.SetEntity(_entity);
        _spriterInstance.SetAnimation(name, (Spriter.LoopMode)loopMode);
        UpdateAnimation(0.0f);
    }

    protected override void UpdateSourceBatches()
    {
        foreach (var batch in SourceBatches)
            batch.Vertices.Clear();
        if (_spriterInstance?.Animation is null || _animationSet is null)
            return;

        var keys = _spriterInstance.TimelineKeys.OfType<SpriteTimelineKey>().OrderBy(key => key.ZIndex).ToArray();
        while (SourceBatches.Count < keys.Length)
            SourceBatches.Add(new SourceBatch2D { Owner = this });
        while (SourceBatches.Count > keys.Length)
            SourceBatches.RemoveAt(SourceBatches.Count - 1);

        for (var index = 0; index < keys.Length; ++index)
        {
            var key = keys[index];
            var sprite = _animationSet.GetSpriterFileSprite(key.FolderId, key.FileId);
            if (sprite?.Texture is null)
                continue;
            var batch = SourceBatches[index];
            batch.DrawOrder = DrawOrder + key.ZIndex;
            batch.Material = CustomMaterialValue ?? Renderer?.GetMaterial(sprite.Texture, BlendModeValue);

            var info = key.Info;
            var position = new Vector3f((FlipXValue ? -info.X : info.X) * Sprite2D.PixelSize,
                (FlipYValue ? -info.Y : info.Y) * Sprite2D.PixelSize, 0.0f);
            var angle = FlipXValue != FlipYValue ? -info.Angle : info.Angle;
            var transform = new Matrix3x4(position, new Quaternion(angle), new Vector3f(info.ScaleX, info.ScaleY, 1.0f));
            if (!(key.UseDefaultPivot
                ? sprite.GetDrawRectangle(out var drawRect, FlipXValue, FlipYValue)
                : sprite.GetDrawRectangle(out drawRect, new Vector2f(key.PivotX, key.PivotY), FlipXValue, FlipYValue)) ||
                !sprite.GetTextureRectangle(out var textureRect, FlipXValue, FlipYValue))
                continue;

            var finalColor = ColorValue;
            finalColor.A *= info.Alpha;
            var color = finalColor.ToUInt();
            var v0 = MakeVertex(transform, drawRect.Min.X, drawRect.Min.Y, textureRect.Min.X, textureRect.Min.Y, color);
            var v1 = MakeVertex(transform, drawRect.Min.X, drawRect.Max.Y, textureRect.Min.X, textureRect.Max.Y, color);
            var v2 = MakeVertex(transform, drawRect.Max.X, drawRect.Max.Y, textureRect.Max.X, textureRect.Max.Y, color);
            var v3 = MakeVertex(transform, drawRect.Max.X, drawRect.Min.Y, textureRect.Max.X, textureRect.Min.Y, color);
            batch.Vertices.AddRange([v0, v1, v2, v0, v2, v3]);
        }
    }

    protected override void OnSceneSet(Scene? previousScene, Scene? scene)
    {
        if (previousScene is not null)
            UnsubscribeFromEvent<SceneEvents.ScenePostUpdateEvent>();
        base.OnSceneSet(previousScene, scene);
        if (scene is not null && IsEnabledEffective)
            SubscribeToEvent<SceneEvents.ScenePostUpdateEvent>(scene, HandleScenePostUpdate);
    }

    private void HandleScenePostUpdate(ref SceneEvents.ScenePostUpdateEvent eventData) => UpdateAnimation(eventData.TimeStep);
    private void UpdateAnimation(float timeStep)
    {
        if (_spriterInstance?.Animation is null)
            return;
        _spriterInstance.Update(timeStep * _speed);
        MarkSourceBatchesDirty();
    }

    private static Vertex2D MakeVertex(Matrix3x4 transform, float x, float y, float u, float v, uint color) => new()
    {
        Position = transform * new Vector3f(x, y, 0.0f), Color = color, Uv = new Vector2f(u, v),
    };

    private void SetAnimationSetAttr(ResourceRef value)
    {
        _animationSetAttr = value;
        AnimationSet = GetSubsystem<ResourceCache>()?.GetResource<AnimationSet2D>(value.Name);
    }
    public ResourceRef GetAnimationSetAttr() => Resource.GetResourceRef(_animationSet, typeof(AnimationSet2D).GetTypeId());
    private void SetAnimationAttr(string value) => SetAnimation(value, _loopMode);
}
