namespace RedBolt.Tests;

public sealed class DrawableTests
{
    [Fact]
    public void Drawable_exposes_batches_and_mutable_light_probe_hint()
    {
        using var drawable = new CustomGeometry();

        Assert.Empty(drawable.GetBatches());

        ref var hint = ref drawable.GetMutableLightProbeTetrahedronHint();
        Assert.Equal(uint.MaxValue, hint);

        hint = 12u;

        Assert.Equal(12u, drawable.GetMutableLightProbeTetrahedronHint());
    }
}
