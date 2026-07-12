using Xunit;

namespace RedBolt.Tests;

public sealed class AnimationControllerTests
{
    [Fact]
    public void Animation_controller_manages_instances_and_high_level_operations()
    {
        var animation = new Animation("Walk");
        animation.SetLength(2.0f);
        var controller = new AnimationController();

        var parameters = new AnimationParameters(animation)
            .Layer(1)
            .Looped()
            .Weight(0.5f);

        controller.AddAnimation(parameters);

        Assert.Equal(1u, controller.GetNumAnimations());
        Assert.Equal(1u, controller.GetAnimationLayer(0));
        Assert.Same(animation, controller.GetAnimationParameters(0).GetAnimation());
        Assert.NotSame(parameters, controller.GetAnimationParameters(0));
        Assert.Single(controller.GetAnimationStates());
        Assert.True(controller.IsPlaying(animation));

        Assert.True(controller.UpdateAnimationTime(animation, 1.0f));
        Assert.Equal(1.0f, controller.GetAnimationParameters(0).GetTime());
        Assert.True(controller.UpdateAnimationSpeed(animation, 2.0f));
        Assert.Equal(2.0f, controller.GetAnimationParameters(0).SpeedValue);

        var existingIndex = controller.PlayExisting(new AnimationParameters(animation).Layer(1).Speed(3.0f));
        Assert.Equal(0u, existingIndex);
        Assert.Equal(3.0f, controller.GetAnimationParameters(0).SpeedValue);

        controller.Fade(animation, 0.0f, 0.0f);
        Assert.Equal(0.0f, controller.GetLastAnimationParameters(animation)!.TargetWeight);

        controller.StopAll();
        controller.Update(0.0f);

        Assert.Equal(0u, controller.GetNumAnimations());
        Assert.False(controller.IsPlaying(animation));
    }
}
