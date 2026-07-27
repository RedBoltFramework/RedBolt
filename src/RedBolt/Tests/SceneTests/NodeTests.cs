using RedBolt;
using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.Scene;
using SceneType = global::RedBolt.Scene.Scene;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class NodeTests
{
    public NodeTests()
    {
        TestTypeRegistration.RegisterFactory<AttachmentProbe>();
        TestTypeRegistration.RegisterFactory<ListenerProbe>();
    }

    [Fact]
    public void Node_prefab_archive_roundtrips_hierarchy_components_and_variables()
    {
        var previous = Context.Instance; Context.SetInstance(new Context());
        try
        {
            RegisterPrefabTypes();
            var scene = new SceneType();
            var child = scene.CreateChild("Child");
            child.SetPosition(new Vector3f(1, 2, 3));
            child.SetVar("score", 42);
            var camera = child.CreateComponent<Camera>(); camera.SetFov(73);

            var json = new TestJSONOutputArchive();
            using (json.OpenUnorderedBlock("root")) scene.SerializeInBlock(json);
            var loadedScene = new SceneType();
            var input = new TestJSONInputArchive(json.ToJsonString());
            using (input.OpenUnorderedBlock("root")) loadedScene.SerializeInBlock(input);

            var loadedChild = loadedScene.GetChild("Child");
            Assert.NotNull(loadedChild);
            Assert.True(loadedChild!.Position.ApproximatelyEquivalent(new Vector3f(1, 2, 3)));
            Assert.Equal(42, loadedChild.GetVar("score"));
            Assert.True(MathDefs.Equals(loadedChild.GetComponent<Camera>()!.GetFov(), 73));
        }
        finally { Context.SetInstance(previous); }
    }

    [Fact]
    public void Node_prefab_reader_writer_roundtrip_loads_hierarchy()
    {
        RegisterPrefabTypes();
        var sourceScene = new SceneType();
        var source = sourceScene.CreateChild("Child");
        source.SetVar("score", 42);
        source.CreateComponent<Camera>().SetFov(73);

        var prefab = new NodePrefab();
        using var writer = new PrefabWriterToMemory(prefab, PrefabSaveFlag.SaveTemporary);
        Assert.True(sourceScene.Save(writer));
        Assert.True(writer.IsEOF());
        Assert.Equal("Child", Assert.Single(prefab.Children).GetNodeName());

        var destinationScene = new SceneType();
        using var reader = new PrefabReaderFromMemory(prefab);
        Assert.True(destinationScene.Load(reader));

        var child = destinationScene.GetChild("Child");
        Assert.NotNull(child);
        Assert.Equal(42, child!.GetVar("score"));
        Assert.True(MathDefs.Equals(child.GetComponent<Camera>()!.GetFov(), 73));
    }

    [Fact]
    public void Node_prefab_clone_copies_nested_values()
    {
        var source = new NodePrefab();
        source.Node.SetType("Node");
        source.Node.Attributes.Add(CreateAttribute("Name", new Variant("Source")));
        var component = new SerializablePrefab();
        component.SetType("Component");
        component.SetId((SerializableId)7);
        component.Attributes.Add(CreateAttribute("Value", new Variant(42)));
        source.Components.Add(component);
        var child = new NodePrefab();
        child.Node.SetType("Child");
        source.Children.Add(child);

        var clone = (NodePrefab)source.Clone();
        var changedValue = new Variant(99);
        clone.Components[0].Attributes[0].SetValue(ref changedValue);
        clone.Children[0].Node.SetType("ChangedChild");

        Assert.Equal("Name", source.Node.Attributes[0].Name);
        Assert.Equal(42, source.Components[0].Attributes[0].Value.GetInt());
        Assert.Equal("Child", source.Children[0].Node.TypeName);
    }

    private static AttributePrefab CreateAttribute(string name, Variant value)
    {
        var result = new AttributePrefab(name);
        result.SetValue(ref value);
        return result;
    }

    private static void RegisterPrefabTypes()
    {
        if (!Context.Instance.IsReflected<Node>())
            Node.RegisterObject(Context.Instance);
        if (!Context.Instance.IsReflected<SceneType>())
            SceneType.RegisterObject(Context.Instance);
        if (!Context.Instance.IsReflected<Camera>())
            Context.Instance.AddFactoryReflection<Camera>();
    }

    private sealed class ListenerProbe : Component
    {
        public int DirtyCount { get; private set; }
        public int NodeEnabledCount { get; private set; }

        protected override void OnMarkedDirty(Node node)
        {
            ++DirtyCount;
        }

        protected override void OnNodeSetEnabled(Node node)
        {
            ++NodeEnabledCount;
        }
    }

    private sealed class AttachmentProbe : Component
    {
        public bool WasQueryableOnNodeSet { get; private set; }

        protected override void OnNodeSet(Node? previousNode, Node? currentNode)
        {
            WasQueryableOnNodeSet = currentNode?.GetComponent<AttachmentProbe>() == this;
        }
    }

    private sealed class DerivedNode : Node
    {
    }

    [Fact]
    public void Node_is_extensible_and_may_exist_without_scene()
    {
        var node = new DerivedNode();

        Assert.Null(node.Scene);
        Assert.Null(node.Parent);
        Assert.Equal(0u, node.Id);

        node.SetName("Standalone");
        node.SetTemporary(true);

        Assert.Equal("Standalone", node.Name);
        Assert.True(node.IsTemporary);
    }

    [Fact]
    public void Parenting_transfers_node_between_scenes_and_null_parent_is_ignored()
    {
        var firstScene = new SceneType();
        var secondScene = new SceneType();
        var child = firstScene.CreateChild("Child");
        var oldId = child.Id;

        secondScene.AddChild(child);

        Assert.Same(secondScene, child.Scene);
        Assert.Same(secondScene, child.Parent);
        Assert.Null(firstScene.GetNode(oldId));

        child.SetParent(null);

        Assert.Same(secondScene, child.Parent);
    }

    [Fact]
    public void Local_rotation_and_scale_preserve_native_input_rules()
    {
        var node = new Node();
        var rotation = new Quaternion(2.0f, 3.0f, 4.0f, 5.0f);

        node.SetRotation(rotation);
        node.SetScale(new Vector3f(float.Epsilon, 0.0f, -float.Epsilon));

        Assert.Equal(rotation, node.Rotation);
        Assert.Equal(float.Epsilon, node.Scale.X);
        Assert.Equal(MathDefs.Epsilon, node.Scale.Y);
        Assert.Equal(-float.Epsilon, node.Scale.Z);
    }

    [Fact]
    public void Component_is_queryable_during_node_attachment()
    {
        var node = new SceneType().CreateChild("Node");

        var component = node.CreateComponent<AttachmentProbe>();

        Assert.True(component.WasQueryableOnNodeSet);
    }

    [Fact]
    public void Listener_registration_does_not_retain_component()
    {
        var source = new SceneType().CreateChild("Source");
        WeakReference<ListenerProbe> listenerReference = null!;

        void AddListener()
        {
            var listener = new ListenerProbe();
            listenerReference = new WeakReference<ListenerProbe>(listener);
            source.AddListener(listener);
        }

        AddListener();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.False(listenerReference.TryGetTarget(out _));
        _ = source.GetWorldPosition();
        source.SetPosition(Vector3f.One);
        Assert.Empty(source.GetListeners());
    }

    [Fact]
    public void SetAttribute_name_updates_property()
    {
        var scene = new SceneType();
        var node = scene.CreateChild("Original");
        Assert.True(node.SetAttribute("Name", "Renamed"));
        Assert.Equal("Renamed", node.Name);
    }

    [Fact]
    public void SetAttribute_position_updates_property()
    {
        var scene = new SceneType();
        var node = scene.CreateChild("Mover");
        Assert.True(node.SetAttribute("Position", new Vector3f(1.0f, 2.0f, 3.0f)));
        Assert.Equal(new Vector3f(1.0f, 2.0f, 3.0f), node.Position);
    }

    [Fact]
    public void Tags_add_remove_has_work()
    {
        var scene = new SceneType();
        var node = scene.CreateChild("Tagged");
        node.SetTags(["editor", "dynamic"]);
        Assert.True(node.HasTag("editor"));
        Assert.True(node.RemoveTag("dynamic"));
        Assert.False(node.HasTag("dynamic"));
    }

    [Fact]
    public void Variables_get_set_and_update_by_hash_work()
    {
        var scene = new SceneType();
        var node = scene.CreateChild("Vars");
        node.SetVar("Health", 100);
        Assert.Equal(100, node.GetVar("Health"));
        node.SetVar("Armor", 25);
        node.SetVarByHash(new StringHash("Armor"), 50);
        Assert.Equal(50, node.GetVar("Armor"));
        Assert.Equal(100, node.GetMutableVar("Health"));
        Assert.Equal(50, node.GetMutableVarByHash(new StringHash("Armor")));
    }

    [Fact]
    public void Lazy_node_and_component_lookups_cache_matching_objects()
    {
        var scene = new SceneType();
        var node = scene.CreateChild("Parent");
        var child = node.CreateChild("Child");
        var camera = child.CreateComponent<Camera>();
        Node? cachedChild = null;
        Camera? cachedCamera = null;

        Assert.True(node.GetChildLazy(ref cachedChild, child.NameHash));
        Assert.Same(child, cachedChild);
        Assert.True(child.GetNthComponentLazy(ref cachedCamera));
        Assert.Same(camera, cachedCamera);
    }

    [Fact]
    public void Transform2d_helpers_canonicalize_z_components()
    {
        var scene = new SceneType();
        var node = scene.CreateChild("TwoD");
        node.SetPosition(new Vector3f(1.0f, 2.0f, 3.0f));
        node.SetScale(new Vector3f(4.0f, 5.0f, 6.0f));

        node.SetPosition2D(new Vector2f(7.0f, 8.0f));
        Assert.Equal(0.0f, node.Position.Z);

        node.SetScale2D(new Vector2f(9.0f, 10.0f));
        Assert.Equal(1.0f, node.Scale.Z);

        node.SetTransform2D(new Vector2f(11.0f, 12.0f), 30.0f, new Vector2f(13.0f, 14.0f));
        Assert.Equal(0.0f, node.Position.Z);
        Assert.Equal(1.0f, node.Scale.Z);
    }

    [Fact]
    public void FindComponents_honors_exact_vs_derived_search()
    {
        var scene = new SceneType();
        var node = scene.CreateChild("Search");
        node.CreateComponent<Camera>();

        Assert.Empty(node.FindComponents<Component>(ComponentSearchFlag.Self));
        Assert.Single(node.FindComponents<Component>(ComponentSearchFlag.Self | ComponentSearchFlag.Derived));
    }

    [Fact]
    public void Component_queries_are_exact_and_runtime_overloads_work()
    {
        var scene = new SceneType();
        var node = scene.CreateChild("Node");
        var camera = node.CreateComponent<Camera>();
        var parent = scene.CreateChild("Parent");
        var child = parent.CreateChild("Child");
        child.CreateComponent<Camera>();

        Assert.Null(node.GetComponent<Component>());
        Assert.Empty(node.GetComponents<Component>());
        Assert.Null(node.GetNthComponent<Component>(0));
        Assert.False(node.HasComponent<Component>());
        Assert.Same(camera, node.GetDerivedComponent<Component>());

        Assert.Same(camera, node.GetComponent(typeof(Camera)));
        Assert.Same(camera, node.GetOrCreateComponent(typeof(Camera)));
        Assert.True(node.HasComponent(typeof(Camera)));
        Assert.Null(node.GetComponent(typeof(Component)));
        Assert.Empty(parent.GetChildrenWithComponent<Component>(recursive: true));
        Assert.Same(child, parent.GetChildrenWithComponent<Camera>(recursive: true).Single());

        node.RemoveComponent(typeof(Camera));
        Assert.False(node.HasComponent(typeof(Camera)));
    }

    [Fact]
    public void Attribute_lookup_and_world_space_helpers_work()
    {
        var scene = new SceneType();
        var node = scene.CreateChild("Node");

        var (serializable, attributeIndex) = node.FindComponentAttribute("@/Position");
        Assert.Same(node, serializable);
        Assert.Equal("Position", serializable!.GetAttributes()[(int)attributeIndex].Name);

        node.SetScale(new Vector3f(-2.0f, 3.0f, 4.0f));
        Assert.True(node.GetSignedWorldScale().ApproximatelyEquivalent(new Vector3f(-2.0f, 3.0f, 4.0f)));

        node.SetWorldTransform2D(new Vector2f(7.0f, 8.0f), 30.0f, new Vector2f(9.0f, 10.0f));
        Assert.Equal(0.0f, node.GetWorldPosition().Z);
        Assert.Equal(1.0f, node.GetWorldScale().Z);
    }

    [Fact]
    public void Listener_callbacks_follow_dirty_and_enabled_state()
    {
        var scene = new SceneType();
        var source = scene.CreateChild("Source");
        source.SetPosition(new Vector3f(1.0f, 2.0f, 3.0f));

        var listenerNode = scene.CreateChild("Listener");
        var listener = listenerNode.CreateComponent<ListenerProbe>();
        var baselineDirtyCount = listener.DirtyCount;

        source.AddListener(listener);
        Assert.Single(source.GetListeners());
        Assert.Equal(baselineDirtyCount + 1, listener.DirtyCount);

        _ = source.GetWorldPosition();
        source.SetPosition(new Vector3f(4.0f, 5.0f, 6.0f));
        Assert.Equal(baselineDirtyCount + 2, listener.DirtyCount);

        source.SetEnabled(false);
        Assert.Equal(1, listener.NodeEnabledCount);

        source.RemoveListener(listener);
        Assert.Empty(source.GetListeners());
    }

    [Fact]
    public void Local_to_world_transform_math_works()
    {
        var scene = new SceneType();
        var parent = scene.CreateChild("Parent");
        parent.SetTransform(new Vector3f(1.0f, 2.0f, 3.0f), Quaternion.IDENTITY, new Vector3f(2.0f, 3.0f, 4.0f));
        var child = parent.CreateChild("Child");
        child.SetPosition(new Vector3f(0.5f, 1.0f, 1.5f));

        var expectedWorld = new Vector3f(2.0f, 5.0f, 9.0f);
        Assert.True(child.WorldPosition.ApproximatelyEquivalent(expectedWorld));
        Assert.True(parent.WorldToLocal(expectedWorld).ApproximatelyEquivalent(child.Position));
    }

    [Fact]
    public void Matrix_world_transform_works()
    {
        var scene = new SceneType();
        var parent = scene.CreateChild("Parent");
        parent.SetTransform(new Vector3f(10.0f, 0.0f, 0.0f), Quaternion.IDENTITY, new Vector3f(2.0f, 2.0f, 2.0f));
        var child = parent.CreateChild("Child");
        child.SetWorldTransform(new Matrix3x4(new Vector3f(12.0f, 2.0f, 0.0f), Quaternion.IDENTITY, new Vector3f(4.0f, 4.0f, 4.0f)));

        Assert.True(child.GetWorldPosition().ApproximatelyEquivalent(new Vector3f(12.0f, 2.0f, 0.0f)));
        Assert.True(child.GetWorldScale().ApproximatelyEquivalent(new Vector3f(4.0f, 4.0f, 4.0f)));
        Assert.True(child.GetPosition().ApproximatelyEquivalent(new Vector3f(1.0f, 1.0f, 0.0f)));
    }

    [Fact]
    public void World_transform_reference_observes_cached_transform_updates()
    {
        var node = new Node();
        ref readonly var worldTransform = ref node.GetWorldTransform();

        node.SetPosition(new Vector3f(1.0f, 2.0f, 3.0f));
        _ = node.GetWorldTransform();

        Assert.True(worldTransform.Translation.ApproximatelyEquivalent(new Vector3f(1.0f, 2.0f, 3.0f)));
    }

    [Fact]
    public void Scene_root_transform_does_not_affect_root_children()
    {
        var scene = new SceneType();
        scene.SetTransform(new Vector3f(10.0f, 20.0f, 30.0f), new Quaternion(45.0f, Vector3f.Up), new Vector3f(2.0f, 3.0f, 4.0f));

        var child = scene.CreateChild("Child");
        child.SetTransform(new Vector3f(1.0f, 2.0f, 3.0f), new Quaternion(30.0f, Vector3f.Right), new Vector3f(4.0f, 5.0f, 6.0f));

        Assert.True(child.GetWorldPosition().ApproximatelyEquivalent(child.GetPosition()));
        Assert.True(child.GetWorldRotation().ApproximatelyEquivalent(child.GetRotation()));
        Assert.True(child.GetWorldScale().ApproximatelyEquivalent(child.GetScale()));

        child.SetWorldTransform(new Vector3f(7.0f, 8.0f, 9.0f), new Quaternion(15.0f, Vector3f.Forward), new Vector3f(2.0f, 2.5f, 3.0f));

        Assert.True(child.GetPosition().ApproximatelyEquivalent(new Vector3f(7.0f, 8.0f, 9.0f)));
        Assert.True(child.GetRotation().ApproximatelyEquivalent(new Quaternion(15.0f, Vector3f.Forward)));
        Assert.True(child.GetScale().ApproximatelyEquivalent(new Vector3f(2.0f, 2.5f, 3.0f)));
    }

    [Fact]
    public void Matrix_local_transform_works()
    {
        var scene = new SceneType();
        var node = scene.CreateChild("MatrixLocal");
        node.SetTransformMatrix(new Matrix3x4(new Vector3f(1.0f, 2.0f, 3.0f), Quaternion.IDENTITY, new Vector3f(2.0f, 3.0f, 4.0f)));
        Assert.True(node.GetPosition().ApproximatelyEquivalent(new Vector3f(1.0f, 2.0f, 3.0f)));
        Assert.True(node.GetScale().ApproximatelyEquivalent(new Vector3f(2.0f, 3.0f, 4.0f)));
    }

    [Fact]
    public void Rotate_around_works()
    {
        var scene = new SceneType();
        var node = scene.CreateChild("Rotate");
        node.SetPosition(new Vector3f(1.0f, 0.0f, 0.0f));
        node.RotateAround(Vector3f.Zero, new Quaternion(90.0f, Vector3f.Forward), TransformSpace.Parent);
        Assert.True(node.GetPosition().ApproximatelyEquivalent(new Vector3f(0.0f, 1.0f, 0.0f), MathDefs.LargeEpsilon));
    }

    [Fact]
    public void Scale_modifiers_work()
    {
        var scene = new SceneType();
        var node = scene.CreateChild("Scale");
        node.SetPosition(new Vector3f(2.0f, 0.0f, 0.0f));
        node.ScaleBy(new Vector3f(2.0f, 3.0f, 4.0f));
        node.ScaleAround(Vector3f.Zero, new Vector3f(2.0f, 1.0f, 1.0f), TransformSpace.Parent);
        Assert.True(node.GetPosition().ApproximatelyEquivalent(new Vector3f(4.0f, 0.0f, 0.0f)));
        Assert.True(node.GetScale().ApproximatelyEquivalent(new Vector3f(4.0f, 3.0f, 4.0f)));
    }

    [Fact]
    public void Persistent_counts_and_child_id_overload_work()
    {
        var scene = new SceneType();
        var node = scene.CreateChild("Parent");

        node.CreateChild("PersistentChild");
        var temporaryChild = node.CreateChild(123u, temporary: true);
        node.CreateComponent<Camera>();
        node.CreateComponent<StaticModel>().SetTemporary(true);

        Assert.Equal(123u, temporaryChild.Id);
        Assert.Equal(string.Empty, temporaryChild.Name);
        Assert.True(temporaryChild.IsTemporaryEffective());
        Assert.Equal(1, node.GetNumPersistentChildren());
        Assert.Equal(1, node.GetNumPersistentComponents());
    }

    [Fact]
    public void Silent_transform_helpers_do_not_mark_dirty()
    {
        var scene = new SceneType();
        var node = scene.CreateChild("Silent");
        var child = node.CreateChild("Child");
        var listener = scene.CreateChild("Listener").CreateComponent<ListenerProbe>();

        _ = node.GetWorldPosition();
        _ = child.GetWorldPosition();
        node.AddListener(listener);
        var baselineDirtyCount = listener.DirtyCount;
        node.SetPositionSilent(new Vector3f(1.0f, 2.0f, 3.0f));
        node.SetRotationSilent(new Quaternion(30.0f, Vector3f.Up));
        node.SetScaleSilent(new Vector3f(4.0f, 5.0f, 6.0f));

        Assert.Equal(baselineDirtyCount, listener.DirtyCount);
        Assert.False(node.IsDirty);
        Assert.False(child.IsDirty);
        Assert.True(node.GetPosition().ApproximatelyEquivalent(new Vector3f(1.0f, 2.0f, 3.0f)));
        Assert.True(node.GetScale().ApproximatelyEquivalent(new Vector3f(4.0f, 5.0f, 6.0f)));

        node.SetTransformSilent(new Vector3f(-1.0f, -2.0f, -3.0f), new Quaternion(15.0f, Vector3f.Right), new Vector3f(0.5f, 0.6f, 0.7f));

        Assert.Equal(baselineDirtyCount, listener.DirtyCount);
        Assert.False(node.IsDirty);
        Assert.False(child.IsDirty);
        Assert.True(node.GetPosition().ApproximatelyEquivalent(new Vector3f(-1.0f, -2.0f, -3.0f)));
        Assert.True(node.GetScale().ApproximatelyEquivalent(new Vector3f(0.5f, 0.6f, 0.7f)));

        node.SetTransformMatrixSilent(new Matrix3x4(new Vector3f(7.0f, 8.0f, 9.0f), Quaternion.IDENTITY, new Vector3f(2.0f, 3.0f, 4.0f)));

        Assert.Equal(baselineDirtyCount, listener.DirtyCount);
        Assert.False(node.IsDirty);
        Assert.False(child.IsDirty);
        Assert.True(node.GetPosition().ApproximatelyEquivalent(new Vector3f(7.0f, 8.0f, 9.0f)));
        Assert.True(node.GetScale().ApproximatelyEquivalent(new Vector3f(2.0f, 3.0f, 4.0f)));

        // Silent setters intentionally leave the cached world transform stale until the node is
        // marked dirty (matches rbfx: GetWorldPosition only recalculates when dirty_ is set).
        node.MarkDirty();
        Assert.True(node.GetWorldPosition().ApproximatelyEquivalent(new Vector3f(7.0f, 8.0f, 9.0f)));
        Assert.True(node.GetWorldScale().ApproximatelyEquivalent(new Vector3f(2.0f, 3.0f, 4.0f)));
    }

    [Fact]
    public void Reparenting_preserves_world_transform()
    {
        var scene = new SceneType();
        var parent = scene.CreateChild("OriginalParent");
        parent.SetTransform(new Vector3f(1.0f, 2.0f, 3.0f), Quaternion.IDENTITY, new Vector3f(2.0f, 3.0f, 4.0f));
        var child = parent.CreateChild("Child");
        child.SetPosition(new Vector3f(0.5f, 1.0f, 1.5f));
        var expectedWorld = child.WorldPosition;

        var newParent = scene.CreateChild("NewParent");
        newParent.SetPosition(new Vector3f(-1.0f, -2.0f, -3.0f));
        child.SetParent(newParent);

        Assert.Same(newParent, child.Parent);
        Assert.True(child.WorldPosition.ApproximatelyEquivalent(expectedWorld));
        Assert.Null(parent.GetChild("Child"));
    }

    [Fact]
    public void RemoveChild_detaches()
    {
        var scene = new SceneType();
        var parent = scene.CreateChild("Parent");
        var child = parent.CreateChild("Child");
        parent.RemoveChild(child);
        Assert.Null(child.Parent);
        Assert.Equal(0, parent.GetNumChildren());
    }

    [Fact]
    public void RemoveChildren_non_recursive_works()
    {
        var scene = new SceneType();
        var parent = scene.CreateChild("Parent");
        parent.CreateChild("Nested");
        scene.RemoveChildren(recursive: false);
        Assert.Equal(0, scene.GetNumChildren());
        Assert.Null(parent.Parent);
    }

    [Fact]
    public void SetEnabled_does_not_disable_children_without_recursion()
    {
        var scene = new SceneType();
        var parent = scene.CreateChild("Parent");
        var child = parent.CreateChild("Child");

        parent.SetEnabled(false);

        Assert.False(parent.IsEnabled);
        Assert.False(parent.IsEnabledSelf);
        Assert.True(child.IsEnabled);
        Assert.True(child.IsEnabledSelf);
    }

    [Fact]
    public void Deep_enabled_state_restores_previous_child_state()
    {
        var scene = new SceneType();
        var parent = scene.CreateChild("Parent");
        var child = parent.CreateChild("Child");
        child.SetEnabled(false);

        parent.SetDeepEnabled(false);

        Assert.False(parent.IsEnabled);
        Assert.True(parent.IsEnabledSelf);
        Assert.False(child.IsEnabled);
        Assert.False(child.IsEnabledSelf);

        parent.ResetDeepEnabled();

        Assert.True(parent.IsEnabled);
        Assert.True(parent.IsEnabledSelf);
        Assert.False(child.IsEnabled);
        Assert.False(child.IsEnabledSelf);
    }

    [Fact]
    public void Direct_child_hierarchy_helpers_work()
    {
        var scene = new SceneType();
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
        var scene = new SceneType();
        var parent = scene.CreateChild("Parent");
        var first = parent.CreateChild("First");
        first.AddTag("selectable");
        parent.CreateChild("Second");
        Assert.Same(first, parent.GetChildrenWithTag("selectable").Single());
    }

    [Fact]
    public void Child_filtering_by_component_works()
    {
        var scene = new SceneType();
        var parent = scene.CreateChild("Parent");
        parent.CreateChild("A");
        var b = parent.CreateChild("B");
        b.CreateComponent<StaticModel>();
        Assert.Same(b, parent.GetChildrenWithComponent<StaticModel>(recursive: true).Single());
    }

    [Fact]
    public void Reorder_child_works()
    {
        var scene = new SceneType();
        var parent = scene.CreateChild("Parent");
        parent.CreateChild("First");
        var second = parent.CreateChild("Second");
        parent.ReorderChild(second, 0);
        Assert.Same(second, parent.GetChild(0));
        Assert.Equal(0, second.GetIndexInParent());
    }

    [Fact]
    public void Component_reordering_works()
    {
        var scene = new SceneType();
        var node = scene.CreateChild("Node");
        var camera = node.CreateComponent<Camera>();
        var model = node.CreateComponent<StaticModel>();
        node.ReorderComponent(model, 0);
        Assert.Equal(0, model.GetIndexInParent());
        Assert.Equal(1, camera.GetIndexInParent());
    }

    [Fact]
    public void Component_queries_work()
    {
        var scene = new SceneType();
        var node = scene.CreateChild("Node");
        var model = node.CreateComponent<StaticModel>();
        node.CreateComponent<Camera>();

        Assert.Equal(2, node.GetNumComponents());
        Assert.Same(model, node.GetComponent(0));
        Assert.NotNull(node.GetNthComponent<Camera>(0));
        Assert.True(node.HasComponent<StaticModel>());
    }

    [Fact]
    public void Clone_component_copies_data()
    {
        var scene = new SceneType();
        var source = scene.CreateChild("Source");
        var original = source.CreateComponent<Camera>();
        original.SetFov(75.0f);

        var target = scene.CreateChild("Target");
        var cloned = target.CloneComponent(original) as Camera;
        Assert.NotNull(cloned);
        Assert.NotEqual(original.Id, cloned.Id);
        Assert.True(MathDefs.Equals(cloned.GetFov(), 75.0f));
        Assert.Same(target, cloned.Node);
    }

    [Fact]
    public void Clone_recursive_with_filters_works()
    {
        var scene = new SceneType();
        var source = scene.CreateChild("Source");
        source.SetTags(["cloneable"]);
        source.SetVar("Score", 5);
        source.SetTransform(new Vector3f(3.0f, 4.0f, 5.0f), Quaternion.IDENTITY, new Vector3f(2.0f, 2.0f, 2.0f));
        source.CreateComponent<Camera>().SetFov(65.0f);
        source.CreateComponent<Octree>().SetTemporary(true);
        source.CreateChild("CloneChild").SetPosition(new Vector3f(1.0f, 0.0f, 0.0f));
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
        var scene = new SceneType();
        var a = scene.CreateChild("A").CreateComponent<Camera>();
        var b = scene.CreateChild("B").CreateComponent<Camera>();
        Assert.Equal(2, Node.GetNodes([a, b]).Count);
    }

    [Fact]
    public void Temporary_flag_is_effective_through_hierarchy()
    {
        var scene = new SceneType();
        var parent = scene.CreateChild("TempParent", temporary: true);
        Assert.True(parent.CreateChild("TempChild").IsTemporaryEffective());
    }
}
