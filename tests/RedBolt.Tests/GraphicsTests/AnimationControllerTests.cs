using Xunit;

namespace RedBolt.Tests;

public sealed class AnimationControllerTests
{
    [Fact]
    public void Animation_controller_registers_logic_factory()
    {
        var context = new Context();
        AnimationController.RegisterObject(context);

        using var controller = Assert.IsType<AnimationController>(
            context.CreateObject(ObjectReflection<AnimationController>.TypeId));
        Assert.Equal(ObjectCategory.Category_Logic, context.GetReflection<AnimationController>().Category);

        var animations = controller.Attributes.Single(attribute => attribute.Name == "Animations");
        Assert.Equal(
            new StringVector
            {
                "Animation Count",
                "   Animation",
                "   Is Looped",
                "   Remove On Completion",
                "   Layer",
                "   Blend Mode",
                "   Start Bone",
                "   Auto Fade Out Time",
                "   Time",
                "   Min Time",
                "   Max Time",
                "   Speed",
                "   Remove On Zero Weight",
                "   Weight",
                "   Target Weight",
                "   Target Weight Delay"
            },
            animations.GetMetadata<StringVector>(AttributeMetadata.VectorStructElements));

        var updatePose = controller.Attributes.Single(attribute => attribute.Name == "Update Pose");
        Assert.True(updatePose.GetMetadata<bool>(AttributeMetadata.IsAction));
        Assert.Equal(string.Empty, updatePose.Accessor!.GetObject(controller));
        updatePose.Accessor!.SetObject(controller, true);
    }

    [Fact]
    public void Animation_controller_manages_instances_and_high_level_operations()
    {
        var animation = new Animation { Name = "Walk" };
        animation.SetLength(2.0f);
        var controller = new Scene().CreateChild("Animated").CreateComponent<AnimationController>();

        var parameters = new AnimationParameters(animation)
            .Layer(1)
            .Looped()
            .Weight(0.5f);

        controller.AddAnimation(parameters);

        Assert.Equal(1, controller.GetNumAnimations());
        Assert.Equal(1, controller.GetAnimationLayer(0));
        Assert.Same(animation, controller.GetAnimationParameters(0).GetAnimation());
        Assert.NotSame(parameters, controller.GetAnimationParameters(0));
        Assert.Empty(controller.GetAnimationStates());
        controller.Update(0.0f);
        Assert.Single(controller.GetAnimationStates());
        Assert.True(controller.IsPlaying(animation));

        Assert.True(controller.UpdateAnimationTime(animation, 1.0f));
        Assert.Equal(1.0f, controller.GetAnimationParameters(0).GetTime());
        Assert.True(controller.UpdateAnimationSpeed(animation, 2.0f));
        Assert.Equal(2.0f, controller.GetAnimationParameters(0).SpeedValue);

        var existingIndex = controller.PlayExisting(new AnimationParameters(animation).Layer(1).Speed(3.0f));
        Assert.Equal(0, existingIndex);
        Assert.Equal(3.0f, controller.GetAnimationParameters(0).SpeedValue);

        controller.Fade(animation, 0.0f, 0.0f);
        Assert.Equal(0.0f, controller.GetLastAnimationParameters(animation)!.TargetWeight);

        controller.StopAll();
        controller.Update(0.0f);

        Assert.Equal(0, controller.GetNumAnimations());
        Assert.Empty(controller.GetAnimationStates());
        Assert.False(controller.IsPlaying(animation));
    }

    [Fact]
    public void Animation_controller_replaces_and_merges_instances()
    {
        var keepAnimation = new Animation { Name = "Keep" };
        keepAnimation.SetLength(4.0f);
        var filteredAnimation = new Animation { Name = "Filtered" };
        filteredAnimation.SetLength(4.0f);
        var controller = new Scene().CreateChild("Animated").CreateComponent<AnimationController>();

        controller.AddAnimation(new AnimationParameters(keepAnimation).Layer(1).Weight(0.25f));
        controller.AddAnimation(new AnimationParameters(filteredAnimation).Layer(2).Weight(0.75f));
        Assert.Empty(controller.GetAnimationStates());
        controller.Update(0.0f);
        var originalState = controller.GetAnimationStates()[0];

        var replacement = new AnimationParameters(keepAnimation).Layer(1).Weight(1.0f);
        controller.ReplaceAnimations(new[] { replacement }, 0.5f, 0.25f, new[] { 1 });

        var keepIndex = controller.FindLastAnimation(keepAnimation, 1);
        Assert.NotEqual(-1, keepIndex);
        Assert.Equal(0.5f, controller.GetAnimationParameters(keepIndex).GetTime());
        Assert.Equal(0.25f, controller.GetAnimationParameters(keepIndex).WeightValue);
        Assert.Equal(1.0f, controller.GetAnimationParameters(keepIndex).TargetWeight);
        Assert.Equal(0.25f, controller.GetAnimationParameters(keepIndex).TargetWeightDelay);
        Assert.Equal(0.75f, controller.GetAnimationParameters(0).TargetWeight);
        Assert.Same(originalState, controller.GetAnimationStates()[0]);

        controller.ReplaceAnimations(ReadOnlySpan<AnimationParameters>.Empty, 0.0f, 0.0f);
        controller.Update(0.0f);

        Assert.Equal(0, controller.GetNumAnimations());
        Assert.Empty(controller.GetAnimationStates());
    }

    [Fact]
    public void Animation_controller_connects_states_to_attached_animated_model()
    {
        var node = new Scene().CreateChild("AnimatedModel");
        node.CreateComponent<AnimatedModel>();
        var controller = node.CreateComponent<AnimationController>();
        var animation = new Animation { Name = "Walk" };

        controller.AddAnimation(new AnimationParameters(animation));

        Assert.Empty(controller.GetAnimationStates());
        controller.Update(0.0f);
        Assert.Same(node.GetComponent<AnimatedModel>(), controller.GetAnimationStates()[0].GetModel());
    }
}
