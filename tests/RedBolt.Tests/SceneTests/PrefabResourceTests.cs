namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class PrefabResourceTests
{
    private readonly HeadlessEngineFixture _fixture;

    public PrefabResourceTests(HeadlessEngineFixture fixture)
    {
        this._fixture = fixture;
        if (!Context.Instance.IsReflected<Node>())
            Node.RegisterObject(Context.Instance);
        if (!Context.Instance.IsReflected<Scene>())
            Scene.RegisterObject(Context.Instance);
    }

    private static AttributePrefab CreateAttribute(string name, Variant value)
    {
        var result = new AttributePrefab(name);
        result.SetValue(ref value);
        return result;
    }

    [Fact]
    public void Mutable_node_prefab_and_slices_follow_scene_layout()
    {
        var resource = new PrefabResource("Assets/Tree.prefab");
        var scenePrefab = resource.GetMutableScenePrefab();
        scenePrefab.Node.Attributes.Add(CreateAttribute("Name", new Variant("Scene")));

        var rootPrefab = resource.GetMutableNodePrefab();
        rootPrefab.Node.Attributes.Add(CreateAttribute("Name", new Variant("Root")));
        var childPrefab = new NodePrefab();
        childPrefab.Node.Attributes.Add(CreateAttribute("Name", new Variant("Branch")));
        rootPrefab.Children.Add(childPrefab);

        Assert.Same(scenePrefab, resource.GetScenePrefab());
        Assert.Same(rootPrefab, resource.GetNodePrefab());
        Assert.Same(childPrefab, resource.GetNodePrefabSlice("Branch"));
        Assert.Same(childPrefab, resource.GetNodePrefabSlice("#0"));
        Assert.Equal("Root", rootPrefab.GetNodeName());
        Assert.False(rootPrefab.IsEmpty());
    }

    [Fact]
    public void Prefab_resource_roundtrips_through_simple_resource_archive()
    {
        var resource = new PrefabResource("Tree.prefab");
        var rootPrefab = resource.GetMutableNodePrefab();
        rootPrefab.Node.Attributes.Add(CreateAttribute("Name", new Variant("Root")));
        rootPrefab.Node.Attributes.Add(CreateAttribute("Next Node ID", new Variant(12u)));

        using var stream = new MemoryStream();
        Assert.True(resource.Save(stream));

        stream.Position = 0;
        var loaded = new PrefabResource();
        Assert.True(loaded.Load(stream));
        Assert.Equal("Root", loaded.GetNodePrefab().GetNodeName());
    }

    [Fact]
    public void Normalize_ids_removes_scene_id_attributes()
    {
        var resource = new PrefabResource();
        var scenePrefab = resource.GetMutableScenePrefab();
        scenePrefab.Node.Attributes.Add(CreateAttribute("Next Node ID", new Variant(3u)));
        scenePrefab.Node.Attributes.Add(CreateAttribute("Next Component ID", new Variant(4u)));
        scenePrefab.Node.Attributes.Add(CreateAttribute("Name", new Variant("Scene")));

        resource.NormalizeIds();

        Assert.Single(scenePrefab.Node.Attributes);
        Assert.Equal("Name", scenePrefab.Node.Attributes[0].Name);
    }

    [Fact]
    public void Instantiate_reference_creates_prefab_reference_and_instance()
    {
        var scene = new Scene();
        var resource = new PrefabResource("Tree.prefab");
        var rootPrefab = resource.GetMutableNodePrefab();
        rootPrefab.Node.Attributes.Add(CreateAttribute("Name", new Variant("Tree")));
        rootPrefab.Children.Add(new NodePrefab());

        var instance = resource.InstantiateReference(scene.Root);

        Assert.NotNull(instance);
        Assert.Equal("Tree", instance!.Name);
        Assert.NotNull(instance.GetComponent<PrefabReference>());
        Assert.Single(instance.Children);
    }

    [Fact]
    public void Background_loads_prefab_resource_references()
    {
        var resources = _fixture.Resources;
        var tempDir = Path.Combine(Path.GetTempPath(), $"redbolt-prefab-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(tempDir, "Data"));
        var dependencyPath = Path.Combine(tempDir, "Data", "dependency.bin");
        System.IO.File.WriteAllText(dependencyPath, "dependency");

        try
        {
            resources.AddResourceDir(tempDir);
            var sourceResource = new PrefabResource("Tree.prefab");
            var component = new SerializablePrefab();
            component.SetType("Node");
            component.Attributes.Add(CreateAttribute(
                "Dependency", new Variant(new ResourceRef("BinaryFile", "Data/dependency.bin"))));
            sourceResource.GetMutableNodePrefab().Components.Add(component);

            using var stream = new MemoryStream();
            Assert.True(sourceResource.Save(stream));
            Assert.Equal(new ResourceRef("BinaryFile", "Data/dependency.bin"),
                Assert.Single(Assert.Single(sourceResource.GetNodePrefab().Components).Attributes).Value.GetResourceRef());
            stream.Position = 0;

            var loadedResource = new PrefabResource("Tree.prefab");
            loadedResource.SetAsyncLoadState(AsyncLoadState.Loading);
            Assert.True(loadedResource.BeginLoad(stream));
            var loadedValue = Assert.Single(Assert.Single(loadedResource.GetNodePrefab().Components).Attributes).Value;
            Assert.Equal(VariantType.ResourceRef, loadedValue.Type);
            Assert.Equal(new ResourceRef("BinaryFile", "Data/dependency.bin"), loadedValue.GetResourceRef());
            resources.FinishBackgroundResources(1000);

            var dependency = resources.GetExistingResource<BinaryFile>("Data/dependency.bin");
            Assert.NotNull(dependency);
            Assert.Equal("dependency", dependency!.GetText());
        }
        finally
        {
            resources.ReleaseResource<BinaryFile>("Data/dependency.bin");
            resources.RemoveResourceDir(tempDir);
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Legacy_xml_prefab_is_converted_to_node_prefab()
    {
        const string xml = """
            <scene id="1">
              <attribute name="Name" value="LegacyScene" />
              <node id="42">
                <attribute name="Name" value="LegacyTree" />
              </node>
            </scene>
            """;
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml));
        var resource = new PrefabResource("Legacy.scene");

        Assert.True(resource.Load(stream));
        Assert.Equal("LegacyScene", resource.GetScenePrefab().GetNodeName());
        Assert.Equal("LegacyTree", resource.GetNodePrefab().GetNodeName());
    }
}
