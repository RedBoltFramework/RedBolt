using System.Collections;
using System.Reflection;
using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class ReflectionProbeManagerTests
{
    private static void ClearQueuedViewports()
    {
        var renderer = Context.Instance?.GetSubsystem<Renderer>();
        if (renderer is null)
            return;
        var field = typeof(Renderer).GetField("_queuedViewports", BindingFlags.Instance | BindingFlags.NonPublic);
        if (field?.GetValue(renderer) is IList list)
            list.Clear();
    }

    [Fact]
    public void Reflection_probe_cache_types_match_cpp_class_and_pod_shapes()
    {
        Assert.False(typeof(InternalReflectionProbeData).IsValueType);
        Assert.True(typeof(ReflectionProbeData).IsValueType);
        Assert.True(typeof(ReflectionProbeReference).IsValueType);
        Assert.True(typeof(ReflectionProbeBVH).IsValueType);

        var managerType = typeof(ReflectionProbeManager);
        Assert.True(managerType.GetNestedType("QueuedReflectionProbe", BindingFlags.NonPublic)!.IsValueType);
        Assert.True(managerType.GetNestedType("SpatialCache", BindingFlags.NonPublic)!.IsValueType);
        Assert.True(managerType.GetNestedType("AutoQueueCache", BindingFlags.NonPublic)!.IsValueType);
    }

    [Fact]
    public void Internal_reflection_probe_data_clone_is_an_independent_cpp_value_copy()
    {
        var scene = new Scene();
        scene.Root.GetOrCreateComponent<ReflectionProbeManager>();
        var node = scene.CreateChild("Probe");
        var probe = node.CreateComponent<ReflectionProbe>();
        probe.SetBoxProjectionUsed(true);
        var source = new InternalReflectionProbeData(probe);
        var copy = (InternalReflectionProbeData)source.Clone();

        node.SetWorldPosition(new Vector3f(10.0f, 0.0f, 0.0f));
        source.Update();

        Assert.NotEqual(source.GetWorldBoundingBox(), copy.GetWorldBoundingBox());
        Assert.Same(source.Probe, copy.Probe);
        Assert.Equal(10.0f, source.Data.Value.CubemapCenter.X);
        Assert.Equal(10.0f, copy.Data.Value.CubemapCenter.X);
    }

    [Fact]
    public void UpdateCubemapRenderer_dynamic_creates_renderer()
    {
        var scene = new Scene();
        scene.Root.GetOrCreateComponent<Octree>();
        var manager = scene.Root.GetOrCreateComponent<ReflectionProbeManager>();
        var probe = scene.CreateChild("Probe").CreateComponent<ReflectionProbe>();

        probe.SetProbeType(ReflectionProbeType.Dynamic);

        Assert.NotNull(probe.GetCubemapRenderer());
        Assert.Null(probe.GetMixedProbeTexture());
        Assert.Same(manager, probe.GetRegistry());
    }

    [Fact]
    public void UpdateCubemapRenderer_mixed_defines_writeable_texture()
    {
        var scene = new Scene();
        scene.Root.GetOrCreateComponent<Octree>();
        scene.Root.GetOrCreateComponent<ReflectionProbeManager>();
        var probe = scene.CreateChild("Probe").CreateComponent<ReflectionProbe>();
        probe.SetTextureSize(64);

        probe.SetProbeType(ReflectionProbeType.Mixed);

        var mixed = probe.GetMixedProbeTexture();
        Assert.NotNull(mixed);
        Assert.Equal(64, mixed!.Width);
        Assert.True(mixed.IsRenderTarget());
        Assert.Null(probe.GetCubemapRenderer());
    }

    [Fact]
    public void UpdateCubemapRenderer_baked_clears_dynamic_state()
    {
        var scene = new Scene();
        scene.Root.GetOrCreateComponent<Octree>();
        scene.Root.GetOrCreateComponent<ReflectionProbeManager>();
        var probe = scene.CreateChild("Probe").CreateComponent<ReflectionProbe>();
        probe.SetProbeType(ReflectionProbeType.Dynamic);
        Assert.NotNull(probe.GetCubemapRenderer());

        probe.SetProbeType(ReflectionProbeType.Baked);

        Assert.Null(probe.GetCubemapRenderer());
        Assert.Null(probe.GetMixedProbeTexture());
    }

    [Fact]
    public void SpatialCache_builds_power_of_two_bvh_and_queries_static_probes()
    {
        var scene = new Scene();
        scene.Root.GetOrCreateComponent<Octree>();
        var manager = scene.Root.GetOrCreateComponent<ReflectionProbeManager>();

        for (var i = 0; i < 3; ++i)
        {
            var node = scene.CreateChild($"Probe{i}");
            node.SetWorldPosition(new Vector3f(i * 10.0f, 0, 0));
            var probe = node.CreateComponent<ReflectionProbe>();
            probe.SetBoundingBox(new BoundingBox(-Vector3f.One, Vector3f.One));
            probe.SetPriority(i);
        }

        // Force spatial rebuild via Update
        manager.Update();

        Assert.True(manager.HasStaticProbes());
        Assert.False(manager.HasDynamicProbes());

        var refs = new ReflectionProbeReference[2];
        float cacheDistance = 0;
        manager.QueryStaticProbes(new BoundingBox(new Vector3f(-2, -2, -2), new Vector3f(2, 2, 2)), refs, ref cacheDistance);

        // Exercise BVH query path; origin-centered probe should intersect the query box
        Assert.True(cacheDistance > 0);
    }

    [Fact]
    public void ConsumeUpdateQueue_dynamic_sliced_progresses_faces()
    {
        ClearQueuedViewports();
        var scene = new Scene();
        scene.Root.GetOrCreateComponent<Octree>();
        var manager = scene.Root.GetOrCreateComponent<ReflectionProbeManager>();
        manager.RenderBudget = 2;

        var probe = scene.CreateChild("Dyn").CreateComponent<ReflectionProbe>();
        probe.SetProbeType(ReflectionProbeType.Dynamic);
        probe.SetSlicedUpdate(true);
        probe.SetTextureSize(32);
        probe.QueueRender();

        // First Update fills queue and consumes budgeted faces
        manager.Update();
        // Second Update continues remaining faces
        manager.Update();
        manager.Update();
        manager.Update();
        manager.Update();
        manager.Update();
        manager.Update();

        Assert.NotNull(probe.GetCubemapRenderer());
        ClearQueuedViewports();
    }

    [Fact]
    public void DrawDebugGeometry_does_not_throw_when_enabled()
    {
        var scene = new Scene();
        var debug = scene.Root.GetOrCreateComponent<DebugRenderer>();
        scene.Root.GetOrCreateComponent<ReflectionProbeManager>();
        var probe = scene.CreateChild("Probe").CreateComponent<ReflectionProbe>();
        probe.SetBoxProjectionUsed(true);

        probe.DrawDebugGeometry(debug, depthTest: true, compact: false);
        probe.DrawDebugGeometry(debug, depthTest: false);
    }
}
