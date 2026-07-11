using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
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

    [Fact]
    public void Archive_configuration_roundtrips()
    {
        var source = new AxisAdapter();
        source.SetDeadZone(0.25f);
        source.SetInverted(true);
        source.SetNeutralValue(-0.2f);
        source.SetPositiveSensitivity(0.75f);
        source.SetNegativeSensitivity(-0.5f);

        var output = new JSONOutputArchive();
        using (output.OpenUnorderedBlock("axis"))
            source.SerializeInBlock(output);

        var loaded = new AxisAdapter();
        var input = new JSONInputArchive(output.ToJsonString());
        using (input.OpenUnorderedBlock("axis"))
            loaded.SerializeInBlock(input);

        Assert.True(MathDefs.ApproximatelyEquivalent(loaded.GetDeadZone(), 0.25f));
        Assert.True(loaded.IsInverted());
        Assert.True(MathDefs.ApproximatelyEquivalent(loaded.GetNeutralValue(), -0.2f));
        Assert.True(MathDefs.ApproximatelyEquivalent(loaded.GetPositiveSensitivity(), 0.75f));
        Assert.True(MathDefs.ApproximatelyEquivalent(loaded.GetNegativeSensitivity(), -0.5f));
    }
}
