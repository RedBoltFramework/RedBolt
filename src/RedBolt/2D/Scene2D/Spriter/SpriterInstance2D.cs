// Copyright (c) 2008-2022 the Urho3D project.
// Copyright (c) 2022-2026 the rbfx project.
// This work is licensed under the terms of the MIT license.

namespace RedBolt.Scene2D.Spriter;

using RedBolt.Graphics;
using RedBolt.Scene;

public sealed class SpriterInstance
{
    private readonly Component _owner;
    private readonly SpriterData _data;
    private bool _looping;
    private float _currentTime;

    public SpriterInstance(Component owner, SpriterData data)
    {
        _owner = owner;
        _data = data;
        SpatialInfo = new SpatialInfo(0, 0, 0, 1, 1);
    }

    public Entity? Entity { get; private set; }
    public Animation? Animation { get; private set; }
    public SpatialInfo SpatialInfo { get; set; }
    public List<SpatialTimelineKey> TimelineKeys { get; } = [];

    public bool SetEntity(int index) => index >= 0 && index < _data.Entities.Count && SetEntity(_data.Entities[index].Name);
    public bool SetEntity(string name)
    {
        var entity = _data.Entities.FirstOrDefault(value => value.Name == name);
        if (entity is null) return false;
        Entity = entity;
        Animation = null;
        TimelineKeys.Clear();
        return true;
    }

    public bool SetAnimation(int index, LoopMode loopMode = LoopMode.Default) =>
        Entity is not null && index >= 0 && index < Entity.Animations.Count && SetAnimation(Entity.Animations[index].Name, loopMode);

    public bool SetAnimation(string name, LoopMode loopMode = LoopMode.Default)
    {
        var animation = Entity?.Animations.FirstOrDefault(value => value.Name == name);
        if (animation is null) return false;
        Animation = animation;
        _looping = loopMode == LoopMode.Default ? animation.Looping : loopMode == LoopMode.ForceLooped;
        _currentTime = 0;
        TimelineKeys.Clear();
        Update(0);
        return true;
    }

    public void Update(float deltaTime)
    {
        if (Animation is null || Animation.MainlineKeys.Count == 0)
            return;
        var lastTime = _currentTime;
        _currentTime += deltaTime;
        if (_currentTime > Animation.Length)
        {
            if (_looping && Animation.Length > 0)
                _currentTime %= Animation.Length;
            else
                _currentTime = Animation.Length;
            if (lastTime != _currentTime && _owner.TryGetNode() is { } node)
            {
                var eventData = new DrawableEvents.AnimationFinishedEvent
                {
                    Node = node, Animation = null, Name = Animation.Name, Looped = _looping,
                };
                node.SendEvent(ref eventData);
            }
        }

        TimelineKeys.Clear();
        var mainline = Animation.MainlineKeys[0];
        foreach (var key in Animation.MainlineKeys)
        {
            if (key.Time <= _currentTime) mainline = key;
            else break;
        }
        foreach (var reference in mainline.BoneRefs)
            AddTimelineKey(reference);
        foreach (var reference in mainline.ObjectRefs)
            AddTimelineKey(reference);
    }

    private void AddTimelineKey(Ref reference)
    {
        var timeline = Animation!.Timelines.First(value => value.Id == reference.Timeline);
        var keyIndex = timeline.Keys.FindIndex(value => value.Id == reference.Key);
        if (keyIndex < 0) return;
        var key = timeline.Keys[keyIndex].Clone();
        if (timeline.Keys.Count > 1 && key.CurveType != CurveType.Instant)
        {
            var nextIndex = keyIndex + 1;
            if (nextIndex >= timeline.Keys.Count)
            {
                if (Animation.Looping) nextIndex = 0;
                else nextIndex = keyIndex;
            }
            if (nextIndex != keyIndex)
            {
                var next = timeline.Keys[nextIndex];
                var nextTime = next.Time < key.Time ? next.Time + Animation.Length : next.Time;
                var currentTime = _currentTime < key.Time ? _currentTime + Animation.Length : _currentTime;
                key.Interpolate(next, key.GetTByCurveType(currentTime, nextTime));
            }
        }
        var parent = reference.Parent >= 0 && reference.Parent < TimelineKeys.Count ? TimelineKeys[reference.Parent].Info : SpatialInfo;
        key.Info = key.Info.UnmapFromParent(parent);
        if (key is SpriteTimelineKey sprite)
            sprite.ZIndex = reference.ZIndex;
        TimelineKeys.Add(key);
    }
}
