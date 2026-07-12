using Xunit;

namespace RedBolt.Tests;

public sealed class AnimationParametersTests
{
    [Fact]
    public void Animation_parameters_preserve_lifecycle_and_wrapped_time()
    {
        var animation = new Animation("Walk");
        animation.SetLength(2.0f);

        var parameters = new AnimationParameters(animation)
            .Looped()
            .StartBone("Root")
            .Layer(2)
            .Additive()
            .Time(1.5f)
            .Weight(0.5f)
            .Speed(2.0f)
            .AutoFadeOut(0.25f)
            .KeepOnCompletion()
            .KeepOnZeroWeight();

        Assert.Same(animation, parameters.GetAnimation());
        Assert.Equal(animation.AnimationNameHash, parameters.GetAnimationName());
        Assert.True(parameters.IsLooped);
        Assert.Equal("Root", parameters.StartBoneName);
        Assert.Equal(2u, parameters.LayerIndex);
        Assert.Equal(AnimationBlendMode.Additive, parameters.BlendMode);
        Assert.Equal(0.5f, parameters.WeightValue);
        Assert.Equal(0.5f, parameters.TargetWeight);
        Assert.Equal(2.0f, parameters.SpeedValue);
        Assert.False(parameters.RemoveOnCompletion);
        Assert.False(parameters.RemoveOnZeroWeight);

        var range = parameters.Update(1.0f);

        Assert.Equal(0.5f, parameters.GetTime());
        Assert.False(range.IsEmpty());
        Assert.True(range.ContainsInclusive(1.75f));

        Assert.True(parameters.RemoveDelayed(0.2f));
        Assert.True(parameters.RemoveOnZeroWeight);
        Assert.Equal(0.0f, parameters.TargetWeight);
        Assert.Equal(0.2f, parameters.TargetWeightDelay);
        Assert.False(parameters.RemoveDelayed(0.4f));
    }
}
