using RedBolt;
using RedBolt.Core;
using RedBolt.Scene;
using SceneType = global::RedBolt.Scene.Scene;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class TrackedComponentTests
{
    public TrackedComponentTests()
    {
        TestTypeRegistration.RegisterFactory<TestRegistry>();
        TestTypeRegistration.RegisterFactory<TestTracked>();
        TestTypeRegistration.RegisterFactory<ReferencedTestRegistry>();
        TestTypeRegistration.RegisterFactory<ReferencedTestComponent>();
    }

    [Fact]
    public void Registry_tracks_components_and_swap_erases()
    {
        var scene = new SceneType();
        var registry = scene.CreateComponent<TestRegistry>();
        var first = scene.CreateComponent<TestTracked>();
        var second = scene.CreateComponent<TestTracked>();

        Assert.Equal(2, registry.NumTrackedComponents);
        Assert.Equal(0, first.IndexInArray);
        Assert.Equal(1, second.IndexInArray);
        Assert.Same(first, registry.GetTrackedComponentByIndex(0));
        Assert.Same(second, registry.GetTrackedComponentByIndex(1));

        first.Remove();

        Assert.Equal(1, registry.NumTrackedComponents);
        Assert.Equal(-1, first.IndexInArray);
        Assert.Equal(0, second.IndexInArray);
        Assert.Same(second, registry.GetTrackedComponentByIndex(0));
    }

    [Fact]
    public void Referenced_registry_reuses_index_with_a_new_version()
    {
        var scene = new SceneType();
        var registry = scene.CreateComponent<ReferencedTestRegistry>();
        var component = scene.CreateComponent<ReferencedTestComponent>();
        var firstReference = component.Reference;

        registry.AddTrackedComponent(component);
        firstReference = component.Reference;

        Assert.NotEqual(ComponentReference.None, firstReference);
        Assert.Same(component, registry.GetTrackedComponentByReference(firstReference));

        component.Remove();
        registry.RemoveTrackedComponent(component);
        Assert.Equal(ComponentReference.None, component.Reference);

        scene.AddComponent(component);
        registry.AddTrackedComponent(component);
        var secondReference = component.Reference;

        Assert.NotEqual(firstReference, secondReference);
        Assert.Null(registry.GetTrackedComponentByReference(firstReference));
        Assert.Same(component, registry.GetTrackedComponentByReference(secondReference));
        Assert.Same(component, registry.GetTrackedComponentByReference(firstReference, checkVersion: false));
    }

    [Fact]
    public void Component_reference_helpers_pack_and_unpack_index_and_version()
    {
        var reference = TrackedComponent.ConstructComponentReference(0x123456, 0xab);

        Assert.Equal(0x123456, TrackedComponent.DeconstructComponentReference(reference).Index);
        Assert.Equal(0xabu, TrackedComponent.DeconstructComponentReference(reference).Version);
        Assert.Equal("1193046:171", TrackedComponent.ToString(reference));
        Assert.Equal("(null)", TrackedComponent.ToString(ComponentReference.None));
    }

    [Fact]
    public void Registry_disposal_releases_owned_containers()
    {
        var registry = new ReferencedTestRegistry();
        var component = new ReferencedTestComponent();
        registry.AddTrackedComponent(component);

        Assert.Single(registry.TrackedComponents);
        Assert.Equal(2, registry.ReferenceIndexUpperBound);

        registry.Dispose();

        Assert.Empty(registry.TrackedComponents);
        Assert.Equal(0, registry.ReferenceIndexUpperBound);
    }

    private sealed class TestRegistry : TrackedComponentRegistryBase
    {
        public TestRegistry()
            : base(ObjectReflection<TestTracked>.TypeId)
        {
        }
    }

    private sealed class TestTracked : TrackedComponent<TestRegistry>
    {
        public TestTracked()
        {
        }
    }

    private sealed class ReferencedTestRegistry : ReferencedComponentRegistryBase
    {
        public ReferencedTestRegistry()
            : base(ObjectReflection<ReferencedTestComponent>.TypeId)
        {
        }
    }

    private sealed class ReferencedTestComponent : ReferencedComponentBase
    {
        public ReferencedTestComponent()
        {
        }

        public override void ReconnectToRegistry()
        {
        }
    }
}
