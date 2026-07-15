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
        node.SetPosition(new Vector3f(10.0f, 20.0f, 30.0f));
        node.SetRotation(new Quaternion(5.0f, 10.0f, 15.0f));
        var originalPosition = node.Position;
        var originalRotation = node.Rotation;
        var shake = node.CreateComponent<ShakeComponent>();
        shake.SetShiftRange(new Vector3f(1.0f, 2.0f, 3.0f));
        shake.SetRotationRange(new Vector3f(4.0f, 5.0f, 6.0f));
        shake.SetTrauma(1.0f);

        scene.Update(0.1f);
        Assert.False(node.Position.ApproximatelyEquivalent(originalPosition));
        Assert.False(node.Rotation.ApproximatelyEquivalent(originalRotation));

        scene.Update(2.0f);
        Assert.True(node.Position.ApproximatelyEquivalent(originalPosition));
        Assert.True(node.Rotation.ApproximatelyEquivalent(originalRotation));
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
        Assert.True(MathDefs.ApproximatelyEquivalent(shake.GetTraumaFalloff(), 3.0f));
        Assert.True(MathDefs.ApproximatelyEquivalent(shake.GetTrauma(), 0.5f));
    }
}
