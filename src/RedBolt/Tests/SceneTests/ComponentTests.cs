namespace RedBolt.Tests;

using RedBolt;
using RedBolt.Scene;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class ComponentTests
{
    [Fact]
    public void Component_matches_rbfx_class_kind_and_defaults()
    {
        Assert.True(typeof(Component).IsClass);
        Assert.False(typeof(Component).IsAbstract);
        Assert.False(typeof(ICloneable).IsAssignableFrom(typeof(Component)));

        using var component = new Component();

        Assert.Equal(0u, component.Id);
        Assert.Null(component.TryGetNode());
        Assert.Null(component.Scene);
        Assert.True(component.Enabled);
        Assert.False(component.IsEnabledEffective);
        Assert.Equal(int.MaxValue, component.GetIndexInParent());
        Assert.False(component.HasAuxiliaryData());
        Assert.Equal(0, component.GetNumAttributes());
    }

    [Fact]
    public void AutoRemoveMode_matches_rbfx_values()
    {
        Assert.Equal(0, (int)AutoRemoveMode.Disabled);
        Assert.Equal(1, (int)AutoRemoveMode.Component);
        Assert.Equal(2, (int)AutoRemoveMode.Node);
    }

    [Fact]
    public void Component_attachment_lifecycle_matches_rbfx_order()
    {
        using var scene = new Scene();
        var node = scene.CreateChild("Node");
        using var component = new LifecycleProbe();

        node.AddComponent(component);

        Assert.Equal(["node:set", "scene:set", "dirty"], component.Transitions);
        Assert.Same(node, component.Node);
        Assert.Same(scene, component.Scene);
        Assert.NotEqual(0u, component.Id);
        Assert.Equal(0, component.GetIndexInParent());

        node.RemoveComponent(component);

        Assert.Equal(["node:set", "scene:set", "dirty", "scene:clear", "node:clear"], component.Transitions);
        Assert.Null(component.TryGetNode());
        Assert.Null(component.Scene);
        Assert.Equal(0u, component.Id);
        Assert.Equal(int.MaxValue, component.GetIndexInParent());
    }

    private sealed class LifecycleProbe : Component
    {
        public List<string> Transitions { get; } = [];

        protected override void OnNodeSet(Node? previousNode, Node? currentNode)
        {
            Transitions.Add(currentNode is null ? "node:clear" : "node:set");
        }

        protected override void OnSceneSet(Scene? previousScene, Scene? scene)
        {
            Transitions.Add(scene is null ? "scene:clear" : "scene:set");
        }

        protected override void OnMarkedDirty(Node node)
        {
            Transitions.Add("dirty");
        }
    }
}
