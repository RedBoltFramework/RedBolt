namespace RedBolt.Tests;

using RedBolt.Actions;
using RedBolt.IO;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class EaseTests
{
    [Fact]
    public void Action_ease_uses_inner_duration_and_identity_easing()
    {
        using var manager = new ActionManager(false);
        using var innerAction = new DelayTime();
        innerAction.SetDuration(2.0f);
        using var action = new ActionEase();

        Assert.Equal(1.1920929E-07f, action.Duration);
        action.SetInnerAction(innerAction);

        Assert.Same(innerAction, action.InnerAction);
        Assert.Equal(2.0f, action.Duration);
        Assert.Equal(0.25f, action.Ease(0.25f));
    }

    [Fact]
    public void Action_ease_reverses_inner_action()
    {
        using var manager = new ActionManager(false);
        using var innerAction = new DelayTime();
        innerAction.SetDuration(2.0f);
        using var action = new ActionEase();
        action.SetInnerAction(innerAction);

        using var reversed = Assert.IsType<ActionEase>(action.Reverse());

        Assert.NotNull(reversed.InnerAction);
        Assert.Equal(1.1920929E-07f, reversed.Duration);
        Assert.NotSame(innerAction, reversed.InnerAction);
        Assert.Same(innerAction, reversed.InnerAction.Reverse());
    }

    [Fact]
    public void Action_ease_serializes_inner_action()
    {
        using var manager = new ActionManager(false);
        using var innerAction = new DelayTime();
        innerAction.SetDuration(2.0f);
        using var action = new ActionEase();
        action.SetInnerAction(innerAction);

        using var output = new TestJSONOutputArchive();
        using (output.OpenUnorderedBlock("action"))
            action.SerializeInBlock(output);

        using var input = new TestJSONInputArchive(output.ToJsonString());
        using var restored = new ActionEase();
        using (input.OpenUnorderedBlock("action"))
            restored.SerializeInBlock(input);

        var restoredInner = Assert.IsType<DelayTime>(restored.InnerAction);
        Assert.Equal(2.0f, restoredInner.Duration);
    }
}
