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

    [Reflected]
    private sealed partial class MiscTarget : Serializable
    {
        [Serialized("Is Visible")]
        public bool IsVisible { get; set; }
    }
}
