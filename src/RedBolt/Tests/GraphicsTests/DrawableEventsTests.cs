using Xunit;

using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Scene;
namespace RedBolt.Tests;

public sealed class DrawableEventsTests
{
    [Fact]
    public void Drawable_events_preserve_payload_shapes()
    {
        Node node = null!;
        Variant data = new(42);
        ParticleEffect effect = new();

        DrawableEvents.BoneHierarchyCreatedEvent boneHierarchyCreated = new() { Node = node };
        DrawableEvents.AnimationTriggerEvent animationTrigger = new()
        {
            Node = node,
            Animation = new Animation { Name = "Walk" },
            Name = "Walk",
            Time = 0.5f,
            Data = data
        };
        DrawableEvents.AnimationFinishedEvent animationFinished = new()
        {
            Node = node,
            Animation = animationTrigger.Animation,
            Name = animationTrigger.Name,
            Looped = true
        };
        DrawableEvents.ParticleEffectFinishedEvent particleEffectFinished = new()
        {
            Node = node,
            Effect = effect
        };
        DrawableEvents.TerrainCreatedEvent terrainCreated = new() { Node = node };

        Assert.Same(node, boneHierarchyCreated.Node);
        Assert.Same(node, animationTrigger.Node);
        Assert.NotNull(animationTrigger.Animation);
        Assert.Equal("Walk", animationTrigger.Name);
        Assert.Equal(0.5f, animationTrigger.Time);
        Assert.Equal(data, animationTrigger.Data);
        Assert.Same(animationTrigger.Animation, animationFinished.Animation);
        Assert.Equal("Walk", animationFinished.Name);
        Assert.True(animationFinished.Looped);
        Assert.Same(node, particleEffectFinished.Node);
        Assert.Same(effect, particleEffectFinished.Effect);
        Assert.Same(node, terrainCreated.Node);
    }
}
