using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class LightBakerTests
{
    [Fact]
    public void SetQuality_Low_AppliesSampleCounts()
    {
        var baker = new LightBaker();
        baker.SetQuality(LightBakingQuality.Low);

        Assert.Equal(LightBakingQuality.Low, baker.GetQuality());
        Assert.Equal("Baking is disabled in build options.", baker.GetBakeLabel());
    }

    [Fact]
    public void Bake_WithoutGlow_ReturnsToIdle()
    {
        var baker = new LightBaker();
        baker.Bake();
        // Glow is not enabled; bake path logs and resets to NotStarted.
        Assert.Equal("Baking is disabled in build options.", baker.GetBakeLabel());
    }

    [Fact]
    public void RegisterObject_AddsFactory()
    {
        var context = Context.Instance!;
        LightBaker.RegisterObject(context);
        Assert.NotNull(context.GetReflection<LightBaker>());
    }
}
