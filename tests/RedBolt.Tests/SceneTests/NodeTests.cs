using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class NodeTests
{
    [Fact]
    public void SetAttribute_name_updates_property()
    {
        var scene = new Scene();
        var node = scene.CreateChild("Original");
        Assert.True(node.SetAttribute("Name", "Renamed"));
        Assert.Equal("Renamed", node.Name);
    }

    [Fact]
    public void SetAttribute_position_updates_property()
    {
        var scene = new Scene();
        var node = scene.CreateChild("Mover");
        Assert.True(node.SetAttribute("Position", new Vector3(1.0f, 2.0f, 3.0f)));
        Assert.Equal(new Vector3(1.0f, 2.0f, 3.0f), node.Position);
    }

    [Fact]
    public void Tags_add_remove_has_work()
    {
        var scene = new Scene();
        var node = scene.CreateChild("Tagged");
        node.SetTags(["editor", "dynamic"]);
        Assert.True(node.HasTag("editor"));
        Assert.True(node.RemoveTag("dynamic"));
        Assert.False(node.HasTag("dynamic"));
    }

    [Fact]
    public void Variables_get_set_remove_work()
    {
        var scene = new Scene();
        var node = scene.CreateChild("Vars");
        node.SetVar("Health", 100);
        Assert.Equal(100, node.GetVar("Health"));
        node.SetVarByHash(new StringHash("Armor"), 50);
        Assert.Null(node.GetVar("Armor"));
        Assert.True(node.RemoveVar("Health"));
        Assert.Null(node.GetVar("Health"));
    }

    [Fact]
    public void Transform2d_helpers_canonicalize_z_components()
    {
        var scene = new Scene();
        var node = scene.CreateChild("TwoD");
        node.SetPosition(new Vector3(1.0f, 2.0f, 3.0f));
        node.SetScale(new Vector3(4.0f, 5.0f, 6.0f));

        node.SetPosition2D(new Vector2(7.0f, 8.0f));
        Assert.Equal(0.0f, node.Position.Z);

        node.SetScale2D(new Vector2(9.0f, 10.0f));
        Assert.Equal(1.0f, node.Scale.Z);

        node.SetTransform2D(new Vector2(11.0f, 12.0f), 30.0f, new Vector2(13.0f, 14.0f));
        Assert.Equal(0.0f, node.Position.Z);
        Assert.Equal(1.0f, node.Scale.Z);
    }

    [Fact]
    public void FindComponents_honors_exact_vs_derived_search()
    {
        var scene = new Scene();
        var node = scene.CreateChild("Search");
        node.CreateComponent<Camera>();

        Assert.Empty(node.FindComponents<Component>(ComponentSearchFlag.Self));
        Assert.Single(node.FindComponents<Component>(ComponentSearchFlag.Self | ComponentSearchFlag.Derived));
    }

    [Fact]
    public void Local_to_world_transform_math_works()
    {
        var scene = new Scene();
        var parent = scene.CreateChild("Parent");
        parent.SetTransform(new Vector3(1.0f, 2.0f, 3.0f), Quaternion.Identity, new Vector3(2.0f, 3.0f, 4.0f));
        var child = parent.CreateChild("Child");
        child.SetPosition(new Vector3(0.5f, 1.0f, 1.5f));

        var expectedWorld = new Vector3(2.0f, 5.0f, 9.0f);
        Assert.True(child.WorldPosition.ApproximatelyEquivalent(expectedWorld));
        Assert.True(parent.WorldToLocal(expectedWorld).ApproximatelyEquivalent(child.Position));
    }

    [Fact]
    public void Matrix_world_transform_works()
    {
        var scene = new Scene();
        var parent = scene.CreateChild("Parent");
        parent.SetTransform(new Vector3(10.0f, 0.0f, 0.0f), Quaternion.Identity, new Vector3(2.0f, 2.0f, 2.0f));
        var child = parent.CreateChild("Child");
        child.SetWorldTransform(new Matrix3x4(new Vector3(12.0f, 2.0f, 0.0f), Quaternion.Identity, new Vector3(4.0f, 4.0f, 4.0f)));

        Assert.True(child.GetWorldPosition().ApproximatelyEquivalent(new Vector3(12.0f, 2.0f, 0.0f)));
        Assert.True(child.GetWorldScale().ApproximatelyEquivalent(new Vector3(4.0f, 4.0f, 4.0f)));
        Assert.True(child.GetPosition().ApproximatelyEquivalent(new Vector3(1.0f, 1.0f, 0.0f)));
    }

    [Fact]
    public void Matrix_local_transform_works()
    {
        var scene = new Scene();
        var node = scene.CreateChild("MatrixLocal");
        node.SetTransformMatrix(new Matrix3x4(new Vector3(1.0f, 2.0f, 3.0f), Quaternion.Identity, new Vector3(2.0f, 3.0f, 4.0f)));
        Assert.True(node.GetPosition().ApproximatelyEquivalent(new Vector3(1.0f, 2.0f, 3.0f)));
        Assert.True(node.GetScale().ApproximatelyEquivalent(new Vector3(2.0f, 3.0f, 4.0f)));
    }

    [Fact]
    public void Rotate_around_works()
    {
        var scene = new Scene();
        var node = scene.CreateChild("Rotate");
        node.SetPosition(new Vector3(1.0f, 0.0f, 0.0f));
        node.RotateAround(Vector3.Zero, Quaternion.FromAngleAxis(90.0f, Vector3.Forward), TransformSpace.Parent);
        Assert.True(node.GetPosition().ApproximatelyEquivalent(new Vector3(0.0f, 1.0f, 0.0f), MathDefs.LargeEpsilon));
    }

    [Fact]
    public void Scale_modifiers_work()
    {
        var scene = new Scene();
        var node = scene.CreateChild("Scale");
        node.SetPosition(new Vector3(2.0f, 0.0f, 0.0f));
        node.ScaleBy(new Vector3(2.0f, 3.0f, 4.0f));
        node.ScaleAround(Vector3.Zero, new Vector3(2.0f, 1.0f, 1.0f), TransformSpace.Parent);
        Assert.True(node.GetPosition().ApproximatelyEquivalent(new Vector3(4.0f, 0.0f, 0.0f)));
        Assert.True(node.GetScale().ApproximatelyEquivalent(new Vector3(4.0f, 3.0f, 4.0f)));
    }

    [Fact]
    public void Reparenting_preserves_world_transform()
    {
        var scene = new Scene();
        var parent = scene.CreateChild("OriginalParent");
        parent.SetTransform(new Vector3(1.0f, 2.0f, 3.0f), Quaternion.Identity, new Vector3(2.0f, 3.0f, 4.0f));
        var child = parent.CreateChild("Child");
        child.SetPosition(new Vector3(0.5f, 1.0f, 1.5f));
        var expectedWorld = child.WorldPosition;

        var newParent = scene.CreateChild("NewParent");
        newParent.SetPosition(new Vector3(-1.0f, -2.0f, -3.0f));
        child.SetParent(newParent);

        Assert.Same(newParent, child.Parent);
        Assert.True(child.WorldPosition.ApproximatelyEquivalent(expectedWorld));
        Assert.Null(parent.GetChild("Child"));
    }

    [Fact]
    public void RemoveChild_detaches()
    {
        var scene = new Scene();
        var parent = scene.CreateChild("Parent");
        var child = parent.CreateChild("Child");
        parent.RemoveChild(child);
        Assert.Null(child.Parent);
        Assert.Equal(0u, parent.GetNumChildren());
    }

    [Fact]
    public void RemoveChildren_non_recursive_works()
    {
        var scene = new Scene();
        var parent = scene.CreateChild("Parent");
        parent.CreateChild("Nested");
        scene.Root.RemoveChildren(recursive: false);
        Assert.Equal(0u, scene.Root.GetNumChildren());
        Assert.Null(parent.Parent);
    }

    [Fact]
    public void Direct_child_hierarchy_helpers_work()
    {
        var scene = new Scene();
        var parent = scene.CreateChild("Parent");
        parent.CreateChild("First");
        var second = parent.CreateChild("Second");
        var grandchild = second.CreateChild("Grandchild");

        Assert.Same(second, parent.GetDirectChildFor(grandchild));
        Assert.True(grandchild.IsChildOf(parent));
    }

    [Fact]
    public void Child_filtering_by_tag_works()
    {
        var scene = new Scene();
        var parent = scene.CreateChild("Parent");
        var first = parent.CreateChild("First");
        first.AddTag("selectable");
        parent.CreateChild("Second");
        Assert.Same(first, parent.GetChildrenWithTag("selectable").Single());
    }

    [Fact]
    public void Child_filtering_by_component_works()
    {
        var scene = new Scene();
        var parent = scene.CreateChild("Parent");
        parent.CreateChild("A");
        var b = parent.CreateChild("B");
        b.CreateComponent<StaticModel>();
        Assert.Same(b, parent.GetChildrenWithComponent<StaticModel>(recursive: true).Single());
    }

    [Fact]
    public void Reorder_child_works()
    {
        var scene = new Scene();
        var parent = scene.CreateChild("Parent");
        parent.CreateChild("First");
        var second = parent.CreateChild("Second");
        parent.ReorderChild(second, 0);
        Assert.Same(second, parent.GetChild(0));
        Assert.Equal(0u, second.GetIndexInParent());
    }

    [Fact]
    public void Component_reordering_works()
    {
        var scene = new Scene();
        var node = scene.CreateChild("Node");
        var camera = node.CreateComponent<Camera>();
        var model = node.CreateComponent<StaticModel>();
        node.ReorderComponent(model, 0);
        Assert.Equal(0u, model.GetIndexInParent());
        Assert.Equal(1u, camera.GetIndexInParent());
    }

    [Fact]
    public void Component_queries_work()
    {
        var scene = new Scene();
        var node = scene.CreateChild("Node");
        var model = node.CreateComponent<StaticModel>();
        node.CreateComponent<Camera>();

        Assert.Equal(2u, node.GetNumComponents());
        Assert.Same(model, node.GetComponent(0));
        Assert.NotNull(node.GetNthComponent<Camera>(0));
        Assert.True(node.HasComponent<StaticModel>());
    }

    [Fact]
    public void Clone_component_copies_data()
    {
        var scene = new Scene();
        var source = scene.CreateChild("Source");
        var original = source.CreateComponent<Camera>();
        original.SetFov(75.0f);

        var target = scene.CreateChild("Target");
        var cloned = target.CloneComponent(original) as Camera;
        Assert.NotNull(cloned);
        Assert.NotEqual(original.Id, cloned.Id);
        Assert.True(MathDefs.ApproximatelyEquivalent(cloned.GetFov(), 75.0f));
        Assert.Same(target, cloned.Node);
    }

    [Fact]
    public void Clone_recursive_with_filters_works()
    {
        var scene = new Scene();
        var source = scene.CreateChild("Source");
        source.SetTags(["cloneable"]);
        source.SetVar("Score", 5);
        source.SetTransform(new Vector3(3.0f, 4.0f, 5.0f), Quaternion.Identity, new Vector3(2.0f, 2.0f, 2.0f));
        source.CreateComponent<Camera>().SetFov(65.0f);
        source.CreateComponent<Octree>().SetTemporary(true);
        source.CreateChild("CloneChild").SetPosition(new Vector3(1.0f, 0.0f, 0.0f));
        source.CreateTemporaryChild("TempChild");

        var target = scene.CreateChild("Target");
        var cloned = source.Clone(target);
        Assert.NotNull(cloned);
        Assert.Same(target, cloned.Parent);
        Assert.NotEqual(source.Id, cloned.Id);
        Assert.Equal("Source", cloned.Name);
        Assert.True(cloned.HasTag("cloneable"));
        Assert.Equal(5, cloned.GetVar("Score"));
        Assert.True(cloned.Position.ApproximatelyEquivalent(source.Position));
        Assert.NotNull(cloned.GetChild("CloneChild"));
        Assert.Null(cloned.GetChild("TempChild"));
        Assert.Equal(65.0f, cloned.GetComponent<Camera>()?.GetFov());
        Assert.False(cloned.HasComponent<Octree>());
    }

    [Fact]
    public void Static_collection_helpers_work()
    {
        var scene = new Scene();
        var a = scene.CreateChild("A").CreateComponent<Camera>();
        var b = scene.CreateChild("B").CreateComponent<Camera>();
        Assert.Equal(2, Node.GetNodes([a, b]).Count);
    }

    [Fact]
    public void Temporary_flag_is_effective_through_hierarchy()
    {
        var scene = new Scene();
        var parent = scene.CreateChild("TempParent", temporary: true);
        Assert.True(parent.CreateChild("TempChild").IsTemporaryEffective());
    }
}
