using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class AnimatableTests
{
    [Fact]
    public void Node_attribute_animation_time_works()
    {
        var scene = new Scene();
        var node = scene.CreateChild("Animated");
        var animation = new ValueAnimation("Animations/NodePosition");
        animation.SetKeyFrame(0.0f, new Vector3(0.0f, 0.0f, 0.0f));
        animation.SetKeyFrame(1.0f, new Vector3(10.0f, 0.0f, 0.0f));

        node.SetAttributeAnimation("Position", animation);
        node.SetAnimationTime(0.5f);

        Assert.True(node.Position.ApproximatelyEquivalent(new Vector3(5.0f, 0.0f, 0.0f)));
        Assert.Same(animation, node.GetAttributeAnimation("Position"));
    }

    [Fact]
    public void Component_attribute_animation_time_works()
    {
        var scene = new Scene();
        var camera = scene.CreateChild("Camera").CreateComponent<Camera>();
        var animation = new ValueAnimation("Animations/Fov");
        animation.SetKeyFrame(0.0f, 45.0f);
        animation.SetKeyFrame(1.0f, 90.0f);

        camera.SetAttributeAnimation("FOV", animation, WrapMode.Clamp, 2.0f);
        camera.SetAttributeAnimationTime("FOV", 0.5f);

        Assert.True(MathDefs.ApproximatelyEquivalent(camera.GetFov(), 67.5f));
        Assert.Equal(WrapMode.Clamp, camera.GetAttributeAnimationWrapMode("FOV"));
        Assert.True(MathDefs.ApproximatelyEquivalent(camera.GetAttributeAnimationSpeed("FOV"), 2.0f));
    }

    [Fact]
    public void Attribute_animation_can_be_removed()
    {
        var node = new Scene().CreateChild("Animated");
        var animation = new ValueAnimation("Animations/NodePosition");
        animation.SetKeyFrame(0.0f, new Vector3(0.0f, 0.0f, 0.0f));
        animation.SetKeyFrame(1.0f, new Vector3(1.0f, 0.0f, 0.0f));

        node.SetAttributeAnimation("Position", animation);
        node.RemoveAttributeAnimation("Position");

        Assert.Null(node.GetAttributeAnimation("Position"));
    }
}
