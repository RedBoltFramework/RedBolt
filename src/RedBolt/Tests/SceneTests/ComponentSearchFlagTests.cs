namespace RedBolt.Tests;

using RedBolt;
using RedBolt.Core;
using RedBolt.Scene;

[Collection(nameof(HeadlessEngineCollection))]
public sealed partial class ComponentSearchFlagTests
{
    [Fact]
    public void ComponentSearchFlag_matches_rbfx_value_type_and_values()
    {
        Assert.True(typeof(ComponentSearchFlag).IsEnum);
        Assert.True(typeof(ComponentSearchFlag).IsValueType);
        Assert.Equal(typeof(int), Enum.GetUnderlyingType(typeof(ComponentSearchFlag)));

        Assert.Equal(0x0, (int)ComponentSearchFlag.None);
        Assert.Equal(0x1, (int)ComponentSearchFlag.Self);
        Assert.Equal(0x2, (int)ComponentSearchFlag.Parent);
        Assert.Equal(0x6, (int)ComponentSearchFlag.ParentRecursive);
        Assert.Equal(0x8, (int)ComponentSearchFlag.Children);
        Assert.Equal(0x18, (int)ComponentSearchFlag.ChildrenRecursive);
        Assert.Equal(0x100, (int)ComponentSearchFlag.Derived);
        Assert.Equal(0x200, (int)ComponentSearchFlag.EnabledOnly);
        Assert.Equal(0x7, (int)ComponentSearchFlag.SelfOrParentRecursive);
        Assert.Equal(0x19, (int)ComponentSearchFlag.SelfOrChildrenRecursive);
        Assert.Equal(ComponentSearchFlag.SelfOrChildrenRecursive, ComponentSearchFlag.Default);
    }

    [Fact]
    public void ComponentSearchFlag_distinguishes_one_level_and_recursive_searches()
    {
        using var scene = new Scene();
        var grandparent = scene.CreateChild("Grandparent");
        var parent = grandparent.CreateChild("Parent");
        var current = parent.CreateChild("Current");
        var child = current.CreateChild("Child");
        var grandchild = child.CreateChild("Grandchild");

        grandparent.CreateComponent<SearchProbe>();
        parent.CreateComponent<SearchProbe>();
        current.CreateComponent<SearchProbe>();
        child.CreateComponent<SearchProbe>();
        grandchild.CreateComponent<SearchProbe>();

        Assert.Equal([parent], GetNodes(current.FindComponents<SearchProbe>(ComponentSearchFlag.Parent)));
        Assert.Equal([parent, grandparent], GetNodes(current.FindComponents<SearchProbe>(ComponentSearchFlag.ParentRecursive)));
        Assert.Equal([child], GetNodes(current.FindComponents<SearchProbe>(ComponentSearchFlag.Children)));
        Assert.Equal([child, grandchild], GetNodes(current.FindComponents<SearchProbe>(ComponentSearchFlag.ChildrenRecursive)));
        Assert.Equal([current, parent, grandparent], GetNodes(current.FindComponents<SearchProbe>(ComponentSearchFlag.SelfOrParentRecursive)));
        Assert.Equal([current, child, grandchild], GetNodes(current.FindComponents<SearchProbe>()));

        Assert.Same(parent, current.FindComponent<SearchProbe>(ComponentSearchFlag.Parent)!.Node);
        Assert.Same(child, current.FindComponent(typeof(SearchProbe), ComponentSearchFlag.Children)!.Node);
    }

    [Fact]
    public void ComponentSearchFlag_applies_derived_and_enabled_filters_like_rbfx()
    {
        using var scene = new Scene();
        var grandparent = scene.CreateChild("Grandparent");
        var parent = grandparent.CreateChild("Parent");
        var current = parent.CreateChild("Current");

        grandparent.CreateComponent<SearchProbe>();
        var parentProbe = parent.CreateComponent<SearchProbe>();
        current.CreateComponent<SearchProbe>();
        parentProbe.SetEnabled(false);

        Assert.Empty(current.FindComponents<Component>(ComponentSearchFlag.Self));
        Assert.Single(current.FindComponents<Component>(ComponentSearchFlag.Self | ComponentSearchFlag.Derived));
        Assert.Equal([grandparent], GetNodes(current.FindComponents<SearchProbe>(
            ComponentSearchFlag.ParentRecursive | ComponentSearchFlag.EnabledOnly)));
    }

    private static IReadOnlyList<Node> GetNodes(IReadOnlyList<SearchProbe> components)
    {
        return components.Select(static component => component.Node!).ToArray();
    }

    [Reflected]
    private sealed partial class SearchProbe : Component
    {
    }
}
