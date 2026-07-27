using RedBolt;
using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.Scene;

namespace RedBolt.Tests;

internal sealed class TestAnimatable : Animatable
{
    public TestAnimatable()
    {
        RegisterAttribute(
            "Value",
            typeof(float),
            self => ((TestAnimatable)self).Value,
            (self, value) => ((TestAnimatable)self).Value = value is float typedValue ? typedValue : 0.0f,
            0.0f);
    }

    public float Value { get; private set; }

    public int AddedCount { get; private set; }

    public int RemovedCount { get; private set; }

    public void Update(float timeStep)
    {
        UpdateAttributeAnimations(timeStep);
    }

    protected override void OnAttributeAnimationAdded()
    {
        ++AddedCount;
    }

    protected override void OnAttributeAnimationRemoved()
    {
        ++RemovedCount;
    }
}

[Collection(nameof(HeadlessEngineCollection))]
public sealed class AnimatableTests
{
    [Fact]
    public void Class_kinds_and_bases_match_rbfx()
    {
        Assert.True(typeof(Animatable).IsAbstract);
        Assert.False(typeof(AttributeAnimationInfo).IsValueType);
        Assert.Equal(typeof(Serializable), typeof(Node).BaseType);
        Assert.Equal(typeof(Serializable), typeof(Component).BaseType);
    }

    [Fact]
    public void Attribute_animation_time_works()
    {
        var animatable = new TestAnimatable();
        var animation = new ValueAnimation { Name = "Animations/Value" };
        animation.SetKeyFrame(0.0f, 0.0f);
        animation.SetKeyFrame(1.0f, 10.0f);

        animatable.SetAttributeAnimation("Value", animation);
        animatable.SetAnimationTime(0.5f);

        Assert.True(MathDefs.Equals(animatable.Value, 5.0f));
        Assert.Same(animation, animatable.GetAttributeAnimation("Value"));
        Assert.Equal(1, animatable.AddedCount);
    }

    [Fact]
    public void Once_attribute_animation_is_removed_when_finished()
    {
        var animatable = new TestAnimatable();
        var animation = new ValueAnimation { Name = "Animations/Value" };
        animation.SetKeyFrame(0.0f, 0.0f);
        animation.SetKeyFrame(1.0f, 10.0f);

        animatable.SetAttributeAnimation("Value", animation, WrapMode.Once);
        animatable.Update(1.0f);

        Assert.Null(animatable.GetAttributeAnimation("Value"));
        Assert.Equal(1, animatable.RemovedCount);
    }

    [Fact]
    public void Attribute_animation_copy_resets_time_and_keeps_target_and_configuration()
    {
        var animatable = new TestAnimatable();
        var animation = new ValueAnimation { Name = "Animations/Value" };
        animation.SetKeyFrame(0.0f, 0.0f);
        animation.SetKeyFrame(1.0f, 10.0f);
        var info = new AttributeAnimationInfo(animatable, animatable.GetAttributes()[1], animation, WrapMode.Clamp, 2.0f);
        info.SetTime(0.5f);

        var clone = Assert.IsType<AttributeAnimationInfo>(info.Clone());

        Assert.Same(animatable, clone.Target);
        Assert.Same(animation, clone.Animation);
        Assert.Same(info.AttributeInfo, clone.AttributeInfo);
        Assert.Equal(WrapMode.Clamp, clone.WrapMode);
        Assert.True(MathDefs.Equals(clone.Speed, 2.0f));
        Assert.True(MathDefs.Equals(clone.Time, 0.0f));
    }
}
