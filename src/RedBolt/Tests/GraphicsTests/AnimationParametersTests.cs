using Xunit;

using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.Resource;
namespace RedBolt.Tests;

public sealed class AnimationParametersTests
{
    [Fact]
    public void Animation_parameters_preserve_lifecycle_and_wrapped_time()
    {
        var animation = new Animation { Name = "Walk" };
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
        Assert.Equal(2, parameters.LayerIndex);
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

    [Fact]
    public void Animation_parameters_copy_independent_value_state()
    {
        var animation = new Animation { Name = "Walk" };
        animation.SetLength(2.0f);
        var parameters = new AnimationParameters(animation, 0.25f, 1.75f)
            .Looped()
            .StartBone("Root")
            .Layer(2)
            .Time(1.0f)
            .Weight(0.5f);
        parameters.InstanceIndex = 3;
        parameters.Removed = true;
        parameters.Merged = true;

        var copy = parameters.Clone();

        Assert.NotSame(parameters, copy);
        Assert.Same(animation, copy.GetAnimation());
        Assert.Equal(parameters.GetAnimationTime(), copy.GetAnimationTime());
        Assert.Equal(parameters, copy);
        Assert.Equal(parameters.Removed, copy.Removed);
        Assert.Equal(parameters.Merged, copy.Merged);

        copy.SetTimeRange(0.0f, 2.0f);
        copy.SetTime(0.5f);
        copy.StartBoneName = "Other";

        Assert.Equal(1.0f, parameters.GetTime());
        Assert.Equal(0.25f, parameters.GetAnimationTime().Min());
        Assert.Equal(1.75f, parameters.GetAnimationTime().Max());
        Assert.Equal("Root", parameters.StartBoneName);
    }

    [Fact]
    public void Animation_parameters_match_removal_epsilon_and_variant_assignment_lifetimes()
    {
        var parameters = new AnimationParameters();
        parameters.RemoveOnZeroWeight = true;
        parameters.TargetWeight = MathDefs.LargeEpsilon;
        parameters.TargetWeightDelay = 0.1f;

        Assert.True(parameters.RemoveDelayed(0.4f));
        Assert.Equal(0.1f, parameters.TargetWeightDelay);

        var variants = new Variant[AnimationParameters.NumVariants];
        using var source = new CString("This CString is long enough to use owned heap storage.");
        variants[0] = new Variant(source);
        var replacedValue = variants[0].GetCString();

        parameters.ToVariantSpan(variants);

        Assert.Equal(0, replacedValue.ByteLength);
        Assert.Equal(VariantType.ResourceRef, variants[0].Type);
        for (var i = 0; i < variants.Length; ++i)
            variants[i].Dispose();
    }
}
