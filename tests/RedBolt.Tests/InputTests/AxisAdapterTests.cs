using RedBolt;

namespace RedBolt.Tests;

public sealed class AxisAdapterTests
{
    [Fact]
    public void Dead_zone_and_sensitivity_work()
    {
        var adapter = new AxisAdapter();
        adapter.SetDeadZone(0.2f);
        adapter.SetSensitivity(1.0f);

        Assert.True(MathDefs.ApproximatelyEquivalent(adapter.Transform(0.1f), 0.0f));
        Assert.True(MathDefs.ApproximatelyEquivalent(adapter.Transform(0.6f), 0.25f, 0.001f));
    }

    [Fact]
    public void Inversion_works()
    {
        var adapter = new AxisAdapter();
        adapter.SetDeadZone(0.2f);
        adapter.SetSensitivity(1.0f);
        adapter.SetInverted(true);
        adapter.SetNegativeSensitivity(0.0f);

        Assert.True(adapter.IsInverted());
        Assert.True(MathDefs.ApproximatelyEquivalent(adapter.Transform(0.6f), -0.5f, 0.001f));
    }

    [Fact]
    public void Getters_work()
    {
        var adapter = new AxisAdapter();
        adapter.SetDeadZone(0.2f);
        adapter.SetNeutralValue(-1.0f);
        adapter.SetSensitivity(1.0f);
        adapter.SetNegativeSensitivity(-0.5f);

        Assert.True(MathDefs.ApproximatelyEquivalent(adapter.GetDeadZone(), 0.2f));
        Assert.True(MathDefs.ApproximatelyEquivalent(adapter.GetNeutralValue(), -1.0f));
        Assert.True(MathDefs.ApproximatelyEquivalent(adapter.GetPositiveSensitivity(), 1.0f));
        Assert.True(MathDefs.ApproximatelyEquivalent(adapter.GetNegativeSensitivity(), -0.5f));
    }
}
