namespace RedBolt.Tests;

public sealed class SceneResolverTests
{
    [Fact]
    public void Resolve_rewrites_node_component_and_node_vector_ids()
    {
        var scene = new Scene();
        var sourceNode = scene.CreateChild("Source");
        var probe = sourceNode.CreateComponent<ResolverProbe>(9u);
        var targetNode = scene.CreateChild("Target", 77u);
        var targetComponent = targetNode.CreateComponent<ResolverProbe>(88u);

        probe.SetAttribute("Node ID", 7u);
        probe.SetAttribute("Component ID", 8u);
        probe.SetAttribute("Node IDs", new VariantVector { new Variant(2u), new Variant(7u), new Variant(999u) });

        var resolver = new SceneResolver();
        resolver.AddNode(7u, targetNode);
        resolver.AddComponent(9u, probe);
        resolver.AddComponent(8u, targetComponent);
        resolver.Resolve();

        Assert.Equal(targetNode.Id, probe.NodeId);
        Assert.Equal(targetComponent.Id, probe.ComponentId);
        Assert.Equal(
            [new Variant(2u), new Variant(targetNode.Id), new Variant(0u)],
            probe.NodeIds);
    }

    private sealed class ResolverProbe : Component
    {
        private uint _nodeId;
        private uint _componentId;
        private VariantVector _nodeIds = [];

        public ResolverProbe()
        {
            RegisterAttribute(
                "Node ID",
                typeof(uint),
                self => ((ResolverProbe)self)._nodeId,
                (self, value) => ((ResolverProbe)self)._nodeId = value is uint id ? id : 0u,
                0u,
                AttributeMode.NodeId);
            RegisterAttribute(
                "Component ID",
                typeof(uint),
                self => ((ResolverProbe)self)._componentId,
                (self, value) => ((ResolverProbe)self)._componentId = value is uint id ? id : 0u,
                0u,
                AttributeMode.ComponentId);
            RegisterAttribute(
                "Node IDs",
                typeof(VariantVector),
                self => ((ResolverProbe)self)._nodeIds,
                (self, value) => ((ResolverProbe)self)._nodeIds = value as VariantVector ?? [],
                new VariantVector(),
                AttributeMode.NodeIdVector);
        }

        public uint NodeId => _nodeId;

        public uint ComponentId => _componentId;

        public VariantVector NodeIds => _nodeIds;
    }
}
