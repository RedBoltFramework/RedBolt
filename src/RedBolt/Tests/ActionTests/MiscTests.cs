namespace RedBolt.Tests;

using RedBolt.Actions;
using RedBolt.Scene;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class MiscTests
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
}
