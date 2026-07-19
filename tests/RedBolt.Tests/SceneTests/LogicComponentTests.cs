using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class LogicComponentTests
{
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
}
