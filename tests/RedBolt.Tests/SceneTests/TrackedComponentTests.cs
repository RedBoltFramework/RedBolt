using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class TrackedComponentTests
{
    [Fact]
    public void Registry_tracks_components_and_swap_erases()
    {
        var scene = new Scene();
        var registry = scene.CreateComponent<TestRegistry>();
        var first = scene.CreateComponent<TestTracked>();
        var second = scene.CreateComponent<TestTracked>();

        Assert.Equal(2u, registry.GetNumTrackedComponents());
        Assert.Equal(0u, first.GetIndexInArray());
        Assert.Equal(1u, second.GetIndexInArray());
        Assert.Same(first, registry.GetTrackedComponentByIndex(0));
        Assert.Same(second, registry.GetTrackedComponentByIndex(1));

        first.Remove();

        Assert.Equal(1u, registry.GetNumTrackedComponents());
        Assert.Equal(uint.MaxValue, first.GetIndexInArray());
        Assert.Equal(0u, second.GetIndexInArray());
        Assert.Same(second, registry.GetTrackedComponentByIndex(0));
    }

    [Fact]
    public void Referenced_registry_reuses_index_with_a_new_version()
    {
        var scene = new Scene();
        var registry = scene.CreateComponent<ReferencedTestRegistry>();
        var component = scene.CreateComponent<ReferencedTestComponent>();
        var firstReference = component.GetReference();

        registry.AddTrackedComponent(component);
        firstReference = component.GetReference();

        Assert.NotEqual(ComponentReference.None, firstReference);
        Assert.Same(component, registry.GetTrackedComponentByReference(firstReference));

        component.Remove();
        registry.RemoveTrackedComponent(component);
        Assert.Equal(ComponentReference.None, component.GetReference());

        scene.Root.AddComponent(component);
        registry.AddTrackedComponent(component);
        var secondReference = component.GetReference();

        Assert.NotEqual(firstReference, secondReference);
        Assert.Null(registry.GetTrackedComponentByReference(firstReference));
        Assert.Same(component, registry.GetTrackedComponentByReference(secondReference));
        Assert.Same(component, registry.GetTrackedComponentByReference(firstReference, checkVersion: false));
    }

    [Fact]
    public void Component_reference_helpers_pack_and_unpack_index_and_version()
    {
        var reference = TrackedComponent.ConstructComponentReference(0x123456, 0xab);

        Assert.Equal((uint)0x123456, TrackedComponent.DeconstructComponentReference(reference).Index);
        Assert.Equal(0xabu, TrackedComponent.DeconstructComponentReference(reference).Version);
        Assert.Equal("1193046:171", TrackedComponent.ToString(reference));
        Assert.Equal("(null)", TrackedComponent.ToString(ComponentReference.None));
    }

    private sealed class TestRegistry : TrackedComponentRegistryBase
    {
        public TestRegistry()
            : base(ObjectReflection<TestTracked>.TypeId)
        {
        }
    }

    private sealed class TestTracked : TrackedComponent<TrackedComponentBase, TestRegistry>
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
