namespace RedBolt.Tests;

using RedBolt.Actions;
using RedBolt.Core;
using RedBolt.Scene;

[Collection(nameof(HeadlessEngineCollection))]
public sealed partial class MiscTests
{
    [Fact]
    public void Remove_self_removes_node_from_parent()
    {
        using var manager = new ActionManager(false);
        using var scene = new Scene();
        using var node = scene.CreateChild();
        using var action = new RemoveSelf();

        Assert.NotNull(manager.AddAction(action, node));
        manager.Update(0.5f);

        Assert.Equal(0, scene.GetNumChildren());
        Assert.Null(node.Parent);
    }

    [Fact]
    public void Show_sets_visible_attribute_once()
    {
        Context.Instance.RegisterAssembly(typeof(MiscTests).Assembly);
        using var manager = new ActionManager(false);
        using var target = new MiscTarget();
        using var action = new Show();

        Assert.Equal(AttributeActionConstants.IsVisibleAttribute, action.AttributeName);
        Assert.NotNull(manager.AddAction(action, target));
        manager.Update(0.0f);
        Assert.True(target.IsVisible);

        target.IsVisible = false;
        manager.Update(1.0f);
        Assert.False(target.IsVisible);
    }

    [Fact]
    public void Hide_clears_visible_attribute_once()
    {
        Context.Instance.RegisterAssembly(typeof(MiscTests).Assembly);
        using var manager = new ActionManager(false);
        using var target = new MiscTarget { IsVisible = true };
        using var action = new Hide();

        Assert.Equal(AttributeActionConstants.IsVisibleAttribute, action.AttributeName);
        Assert.NotNull(manager.AddAction(action, target));
        manager.Update(0.0f);
        Assert.False(target.IsVisible);

        target.IsVisible = true;
        manager.Update(1.0f);
        Assert.True(target.IsVisible);
    }

    [Fact]
    public void Enable_sets_enabled_attribute_once()
    {
        Context.Instance.RegisterAssembly(typeof(MiscTests).Assembly);
        using var manager = new ActionManager(false);
        using var target = new MiscTarget();
        using var action = new Enable();

        Assert.Equal(AttributeActionConstants.IsEnabledAttribute, action.AttributeName);
        Assert.NotNull(manager.AddAction(action, target));
        manager.Update(0.0f);
        Assert.True(target.IsEnabled);

        target.IsEnabled = false;
        manager.Update(1.0f);
        Assert.False(target.IsEnabled);
    }

    [Fact]
    public void Disable_clears_enabled_attribute_once()
    {
        Context.Instance.RegisterAssembly(typeof(MiscTests).Assembly);
        using var manager = new ActionManager(false);
        using var target = new MiscTarget { IsEnabled = true };
        using var action = new Disable();

        Assert.Equal(AttributeActionConstants.IsEnabledAttribute, action.AttributeName);
        Assert.NotNull(manager.AddAction(action, target));
        manager.Update(0.0f);
        Assert.False(target.IsEnabled);

        target.IsEnabled = true;
        manager.Update(1.0f);
        Assert.True(target.IsEnabled);
    }

    [Fact]
    public void Blink_toggles_enabled_attribute_and_restores_original()
    {
        Context.Instance.RegisterAssembly(typeof(MiscTests).Assembly);
        using var manager = new ActionManager(false);
        using var target = new MiscTarget();
        using var action = new Blink();
        action.SetDuration(1.0f);
        action.SetNumOfBlinks(0);
        Assert.Equal(1u, action.NumOfBlinks);
        action.SetNumOfBlinks(2);

        Assert.Equal(AttributeActionConstants.IsEnabledAttribute, action.AttributeName);
        Assert.NotNull(manager.AddAction(action, target));
        manager.Update(0.0f);
        Assert.True(target.IsEnabled);
        manager.Update(0.3f);
        Assert.False(target.IsEnabled);
        manager.Update(0.7f);
        Assert.False(target.IsEnabled);
    }

    [Fact]
    public void Delay_time_waits_for_duration_without_changing_target()
    {
        using var manager = new ActionManager(false);
        using var target = new MiscTarget { IsEnabled = true, IsVisible = true };
        using var action = new DelayTime();
        action.SetDuration(1.0f);

        var state = manager.AddAction(action, target);
        Assert.NotNull(state);
        manager.Update(0.0f);
        Assert.False(state.IsDone);
        manager.Update(0.5f);
        Assert.False(state.IsDone);
        Assert.True(target.IsEnabled);
        Assert.True(target.IsVisible);
        manager.Update(0.5f);
        Assert.True(state.IsDone);
        Assert.True(target.IsEnabled);
        Assert.True(target.IsVisible);
    }

    [Reflected]
    private sealed partial class MiscTarget : Serializable
    {
        [Serialized("Is Visible")]
        public bool IsVisible { get; set; }

        [Serialized("Is Enabled")]
        public bool IsEnabled { get; set; }
    }
}
