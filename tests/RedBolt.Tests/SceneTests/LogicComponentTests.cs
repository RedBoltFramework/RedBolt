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
        Assert.True(MathDefs.ApproximatelyEquivalent(logic.LastUpdateTimeStep, 0.5f));
        Assert.True(MathDefs.ApproximatelyEquivalent(logic.LastPostUpdateTimeStep, 0.5f));
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

    private sealed class CountingLogic : LogicComponent
    {
        public int StartCount { get; private set; }
        public int DelayedStartCount { get; private set; }
        public int StopCount { get; private set; }
        public int UpdateCount { get; private set; }
        public int PostUpdateCount { get; private set; }
        public float LastUpdateTimeStep { get; private set; }
        public float LastPostUpdateTimeStep { get; private set; }

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
    }
}
