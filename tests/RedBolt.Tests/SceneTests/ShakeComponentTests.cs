using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class ShakeComponentTests
{
    [Fact]
    public void Trauma_drives_offset_and_resets_when_finished()
    {
        var scene = new Scene();
        var node = scene.CreateChild("Shake");
        var shake = node.CreateComponent<ShakeComponent>();
        shake.SetShiftRange(new Vector3f(1.0f, 2.0f, 3.0f));
        shake.SetRotationRange(new Vector3f(4.0f, 5.0f, 6.0f));
        shake.SetTrauma(1.0f);

        scene.Update(0.1f);
        Assert.False(node.Position.ApproximatelyEquivalent(Vector3f.Zero));
        Assert.False(node.Rotation.ApproximatelyEquivalent(Quaternion.Identity));

        scene.Update(2.0f);
        Assert.True(node.Position.ApproximatelyEquivalent(Vector3f.Zero));
        Assert.True(node.Rotation.ApproximatelyEquivalent(Quaternion.Identity));
        Assert.Equal(UpdateEvent.UseNoEvent, shake.GetUpdateEventMask());
    }

    [Fact]
    public void Configuration_helpers_work()
    {
        var shake = new ShakeComponent();
        shake.SetTimeScale(10.0f);
        shake.SetTraumaPower(3.0f);
        shake.SetTraumaFalloff(2.0f);
        shake.AddTrauma(0.5f);

        Assert.True(MathDefs.ApproximatelyEquivalent(shake.GetTimeScale(), 10.0f));
        Assert.True(MathDefs.ApproximatelyEquivalent(shake.GetTraumaPower(), 3.0f));
        Assert.True(MathDefs.ApproximatelyEquivalent(shake.GetTraumaFalloff(), 2.0f));
        Assert.True(MathDefs.ApproximatelyEquivalent(shake.GetTrauma(), 0.5f));
    }
}
