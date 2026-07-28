using System.Runtime.CompilerServices;

using RedBolt.Core;
using RedBolt.Scene;
using SceneType = global::RedBolt.Scene.Scene;

namespace RedBolt.Tests;

public sealed partial class SceneResolverTests
{
    [Fact]
    public void Resolve_rewrites_node_component_and_node_vector_ids()
    {
        using var scene = new SceneType();
        var sourceNode = scene.CreateChild("Source");
        var probe = sourceNode.CreateComponent<ResolverProbe>(9u);
        var targetNode = scene.CreateChild("Target", 77u);
        var targetComponent = targetNode.CreateComponent<ResolverProbe>(88u);

        probe.SetAttribute("Node ID", 7u);
        probe.SetAttribute("Component ID", 8u);
        probe.SetAttribute("Node IDs", new VariantVector { new Variant(2u), new Variant(7u), new Variant(999u) });

        using var resolver = new SceneResolver();
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

    [Fact]
    public void Class_shape_and_weak_ownership_match_native()
    {
        Assert.False(typeof(SceneResolver).IsSealed);
        Assert.True(typeof(IDisposable).IsAssignableFrom(typeof(SceneResolver)));

        using var resolver = new SceneResolver();
        var nodeReference = AddTemporaryNode(resolver);
        var componentReference = AddTemporaryComponent(resolver);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.False(nodeReference.TryGetTarget(out _));
        Assert.False(componentReference.TryGetTarget(out _));
        resolver.Resolve();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<Node> AddTemporaryNode(SceneResolver resolver)
    {
        var node = new Node();
        resolver.AddNode(1u, node);
        return new WeakReference<Node>(node);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<ResolverProbe> AddTemporaryComponent(SceneResolver resolver)
    {
        var component = new ResolverProbe();
        resolver.AddComponent(1u, component);
        return new WeakReference<ResolverProbe>(component);
    }

    [Reflected]
    private sealed partial class ResolverProbe : Component
    {
        [Serialized(
            "Node ID",
            Mode = AttributeMode.NodeId
        )]
        private uint _nodeId;
        [Serialized(
            "Component ID",
            Mode = AttributeMode.ComponentId
        )]
        private uint _componentId;
        [Serialized(
            "Node IDs",
            Mode = AttributeMode.NodeIdVector
        )]
        private VariantVector _nodeIds = [];

        public uint NodeId => _nodeId;

        public uint ComponentId => _componentId;

        public VariantVector NodeIds => _nodeIds;


        private void SetNodeIds(VariantVector value)
        {
            _nodeIds.Dispose();
            _nodeIds = new VariantVector(value);
        }

        public override void Dispose()
        {
            _nodeIds.Dispose();
            base.Dispose();
        }
    }
}
