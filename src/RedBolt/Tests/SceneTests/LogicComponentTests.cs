using RedBolt;

using RedBolt.Math;
using RedBolt.Scene;
namespace RedBolt.Tests;
using Scene = global::RedBolt.Scene.Scene;


[Collection(nameof(HeadlessEngineCollection))]
public sealed class LogicComponentTests
{
    public LogicComponentTests()
    {
        TestTypeRegistration.RegisterFactory<CountingLogic>();
        TestTypeRegistration.RegisterFactory<CustomEventLogic>();
    }

    [Fact]
    public void LogicComponent_matches_rbfx_concrete_class_shape()
    {
        var type = typeof(LogicComponent);

        Assert.Equal(typeof(Component), type.BaseType);
        Assert.False(type.IsAbstract);
        Assert.False(type.IsSealed);
        Assert.False(type.IsValueType);
        Assert.False(typeof(ICloneable).IsAssignableFrom(type));
        Assert.Null(type.Assembly.GetType("RedBolt.ISceneUpdateListener"));

        using var logic = new LogicComponent();
        Assert.Equal(
            UpdateEvent.UseUpdate | UpdateEvent.UsePostUpdate | UpdateEvent.UseFixedUpdate | UpdateEvent.UseFixedPostUpdate,
            logic.GetUpdateEventMask());
        Assert.False(logic.IsDelayedStartCalled());
    }

    [Fact]
    public void Scene_update_lifecycle_works()
    {
        var scene = new Scene();
        var logic = scene.CreateChild("LogicNode").CreateComponent<CountingLogic>();

        scene.Update(0.25f);
        scene.Update(0.5f);

        Assert.Equal(1, logic.StartCount);
        Assert.Equal(1, logic.DelayedStartCount);
        Assert.Equal(2, logic.UpdateCount);
        Assert.Equal(2, logic.PostUpdateCount);
        Assert.True(MathDefs.Equals(logic.LastUpdateTimeStep, 0.5f));
        Assert.True(MathDefs.Equals(logic.LastPostUpdateTimeStep, 0.5f));
    }

    [Fact]
    public void Stop_lifecycle_works()
    {
        var scene = new Scene();
        var node = scene.CreateChild("LogicNode");
        var logic = node.CreateComponent<CountingLogic>();
        scene.Update(0.1f);
        node.RemoveComponent(logic);
        Assert.Equal(1, logic.StopCount);
    }

    [Fact]
    public void Delayed_start_is_not_replayed_after_reattach()
    {
        var scene = new Scene();
        var firstNode = scene.CreateChild("First");
        var secondNode = scene.CreateChild("Second");
        var logic = firstNode.CreateComponent<CountingLogic>();

        scene.Update(0.1f);
        firstNode.RemoveComponent(logic);
        secondNode.AddComponent(logic);
        scene.Update(0.1f);

        Assert.Equal(2, logic.StartCount);
        Assert.Equal(1, logic.StopCount);
        Assert.Equal(1, logic.DelayedStartCount);
    }

    [Fact]
    public void World_origin_updates_follow_event_mask()
    {
        var scene = new Scene();
        var logic = scene.CreateChild("LogicNode").CreateComponent<CountingLogic>();
        logic.SetUpdateEventMask(UpdateEvent.UseWorldOriginUpdate | UpdateEvent.UseWorldOriginPostUpdate);

        scene.UpdateWorldOrigin(new Vector3i(100, -20, 300));

        Assert.Equal(1, logic.WorldOriginUpdateCount);
        Assert.Equal(1, logic.WorldOriginPostUpdateCount);
    }

    [Fact]
    public void Disabled_component_unsubscribes_until_reenabled()
    {
        using var scene = new Scene();
        var logic = scene.CreateChild("LogicNode").CreateComponent<CountingLogic>();

        logic.SetEnabled(false);
        scene.Update(0.1f);

        Assert.Equal(0, logic.DelayedStartCount);
        Assert.Equal(0, logic.UpdateCount);
        Assert.Equal(0, logic.PostUpdateCount);

        logic.SetEnabled(true);
        scene.Update(0.1f);

        Assert.Equal(1, logic.DelayedStartCount);
        Assert.Equal(1, logic.UpdateCount);
        Assert.Equal(1, logic.PostUpdateCount);
    }

    [Fact]
    public void Delayed_start_runs_once_without_requested_update_events()
    {
        using var scene = new Scene();
        var logic = new CountingLogic();
        logic.SetUpdateEventMask(UpdateEvent.UseNoEvent);
        scene.CreateChild("LogicNode").AddComponent(logic);

        scene.Update(0.1f);
        scene.Update(0.1f);

        Assert.Equal(1, logic.DelayedStartCount);
        Assert.Equal(0, logic.UpdateCount);
        Assert.Equal(0, logic.PostUpdateCount);
    }

    [Fact]
    public void Logic_update_uses_scene_event_subscription_order()
    {
        using var scene = new Scene();
        using var before = new TestEventReceiver();
        using var after = new TestEventReceiver();
        var calls = new List<string>();
        before.SubscribeToEvent<SceneEvents.SceneUpdateEvent>(scene, HandleBefore);
        var logic = new OrderedLogic(calls);
        logic.SetUpdateEventMask(UpdateEvent.UseUpdate);
        scene.CreateChild("LogicNode").AddComponent(logic);
        after.SubscribeToEvent<SceneEvents.SceneUpdateEvent>(scene, HandleAfter);

        scene.Update(0.1f);

        Assert.Equal(["Before", "Logic", "After"], calls);

        void HandleBefore(ref SceneEvents.SceneUpdateEvent eventData) => calls.Add("Before");
        void HandleAfter(ref SceneEvents.SceneUpdateEvent eventData) => calls.Add("After");
    }

    [Fact]
    public void Virtual_update_event_types_select_typed_struct_events()
    {
        using var scene = new Scene();
        using var otherScene = new Scene();
        var logic = scene.CreateChild("LogicNode").CreateComponent<CustomEventLogic>();

        Assert.Equal(typeof(CustomUpdateEvent), logic.GetUpdateEvent().Type);
        Assert.Equal(typeof(CustomPostUpdateEvent), logic.GetPostUpdateEvent().Type);

        var ignoredUpdate = new CustomUpdateEvent { TimeStep = 1.0f };
        otherScene.SendEvent(ref ignoredUpdate);

        var update = new CustomUpdateEvent { TimeStep = 0.25f };
        scene.SendEvent(ref update);
        var postUpdate = new CustomPostUpdateEvent { TimeStep = 0.5f };
        scene.SendEvent(ref postUpdate);

        Assert.Equal(1, logic.DelayedStartCount);
        Assert.Equal(1, logic.UpdateCount);
        Assert.Equal(1, logic.PostUpdateCount);
        Assert.True(MathDefs.Equals(0.25f, logic.LastUpdateTimeStep));
        Assert.True(MathDefs.Equals(0.5f, logic.LastPostUpdateTimeStep));

        scene.Update(0.1f);

        Assert.Equal(1, logic.UpdateCount);
        Assert.Equal(1, logic.PostUpdateCount);
    }

    [Fact]
    public void Typed_custom_update_dispatch_does_not_allocate_after_warmup()
    {
        using var scene = new Scene();
        scene.CreateChild("LogicNode").CreateComponent<CustomEventLogic>();
        var update = new CustomUpdateEvent { TimeStep = 0.25f };

        for (var i = 0; i < 16; ++i)
            scene.SendEvent(ref update);

        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 128; ++i)
            scene.SendEvent(ref update);
        var allocatedAfter = GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal(allocatedBefore, allocatedAfter);
    }

    private sealed class CountingLogic : LogicComponent
    {
        public int StartCount { get; private set; }
        public int DelayedStartCount { get; private set; }
        public int StopCount { get; private set; }
        public int UpdateCount { get; private set; }
        public int PostUpdateCount { get; private set; }
        public float LastUpdateTimeStep { get; private set; }
        public float LastPostUpdateTimeStep { get; private set; }
        public int WorldOriginUpdateCount { get; private set; }
        public int WorldOriginPostUpdateCount { get; private set; }

        public override void Start() => ++StartCount;
        public override void DelayedStart() => ++DelayedStartCount;
        public override void Stop() => ++StopCount;

        public override void Update(float timeStep)
        {
            ++UpdateCount;
            LastUpdateTimeStep = timeStep;
        }

        public override void PostUpdate(float timeStep)
        {
            ++PostUpdateCount;
            LastPostUpdateTimeStep = timeStep;
        }

        public override void UpdateWorldOrigin(IntVector3 oldOrigin, IntVector3 newOrigin, IntVector3 delta) => ++WorldOriginUpdateCount;
        public override void PostUpdateWorldOrigin(IntVector3 oldOrigin, IntVector3 newOrigin, IntVector3 delta) => ++WorldOriginPostUpdateCount;
    }

    private sealed class OrderedLogic : LogicComponent
    {
        private readonly List<string> _calls;

        public OrderedLogic(List<string> calls)
        {
            _calls = calls;
        }

        public override void Update(float timeStep) => _calls.Add("Logic");
    }

    private struct CustomUpdateEvent : SceneEvents.ISceneUpdateEvent
    {
        public float TimeStep;

        float SceneEvents.ISceneUpdateEvent.TimeStep => TimeStep;
    }

    private struct CustomPostUpdateEvent : SceneEvents.ISceneUpdateEvent
    {
        public float TimeStep;

        float SceneEvents.ISceneUpdateEvent.TimeStep => TimeStep;
    }

    private sealed class CustomEventLogic : LogicComponent
    {
        public int DelayedStartCount { get; private set; }
        public int UpdateCount { get; private set; }
        public int PostUpdateCount { get; private set; }
        public float LastUpdateTimeStep { get; private set; }
        public float LastPostUpdateTimeStep { get; private set; }

        public CustomEventLogic()
        {
            SetUpdateEventMask(UpdateEvent.UseUpdate | UpdateEvent.UsePostUpdate);
        }

        public override SceneUpdateEventType GetUpdateEvent() => SceneUpdateEventType.Get<CustomUpdateEvent>();
        public override SceneUpdateEventType GetPostUpdateEvent() => SceneUpdateEventType.Get<CustomPostUpdateEvent>();
        public override void DelayedStart() => ++DelayedStartCount;

        public override void Update(float timeStep)
        {
            ++UpdateCount;
            LastUpdateTimeStep = timeStep;
        }

        public override void PostUpdate(float timeStep)
        {
            ++PostUpdateCount;
            LastPostUpdateTimeStep = timeStep;
        }
    }
}
