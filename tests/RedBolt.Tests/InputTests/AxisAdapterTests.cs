using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class AxisAdapterTests
{
    [Fact]
    public void AxisAdapter_matches_native_copyable_non_pod_class_kind()
    {
        Assert.True(typeof(AxisAdapter).IsClass);
        Assert.False(typeof(AxisAdapter).IsSealed);
        Assert.True(typeof(ICloneable).IsAssignableFrom(typeof(AxisAdapter)));
    }

    [Fact]
    public void Clone_has_independent_value_semantics()
    {
        var source = new AxisAdapter();
        source.SetDeadZone(0.25f);
        source.SetInverted(true);
        source.SetNeutralValue(-0.2f);
        source.SetPositiveSensitivity(0.75f);
        source.SetNegativeSensitivity(-0.5f);

        var clone = (AxisAdapter)source.Clone();
        clone.SetDeadZone(0.5f);
        clone.SetInverted(false);
        clone.SetNeutralValue(0.4f);
        clone.SetPositiveSensitivity(1.0f);
        clone.SetNegativeSensitivity(0.25f);

        Assert.Equal(0.25f, source.GetDeadZone());
        Assert.True(source.IsInverted());
        Assert.Equal(-0.2f, source.GetNeutralValue());
        Assert.Equal(0.75f, source.GetPositiveSensitivity());
        Assert.Equal(-0.5f, source.GetNegativeSensitivity());
    }

    [Fact]
    public void Default_archive_configuration_is_optional()
    {
        var adapter = new AxisAdapter();
        var output = new JSONOutputArchive();
        using (output.OpenUnorderedBlock("axis"))
            adapter.SerializeInBlock(output);

        var json = output.ToJsonString();
        Assert.DoesNotContain("deadZone", json, StringComparison.Ordinal);
        Assert.DoesNotContain("inverted", json, StringComparison.Ordinal);
        Assert.DoesNotContain("neutral", json, StringComparison.Ordinal);
        Assert.DoesNotContain("posSensitivity", json, StringComparison.Ordinal);
        Assert.DoesNotContain("negSensitivity", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Dead_zone_and_sensitivity_work()
    {
        var adapter = new AxisAdapter();
        adapter.SetDeadZone(0.2f);
        adapter.SetSensitivity(1.0f);

        Assert.True(MathDefs.Equals(adapter.Transform(0.1f), 0.0f));
        Assert.True(MathDefs.Equals(adapter.Transform(0.6f), 0.25f, 0.001f));
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
        Assert.True(MathDefs.Equals(adapter.Transform(0.6f), -0.5f, 0.001f));
    }

    [Fact]
    public void Getters_work()
    {
        var adapter = new AxisAdapter();
        adapter.SetDeadZone(0.2f);
        adapter.SetNeutralValue(-1.0f);
        adapter.SetSensitivity(1.0f);
        adapter.SetNegativeSensitivity(-0.5f);

        Assert.True(MathDefs.Equals(adapter.GetDeadZone(), 0.2f));
        Assert.True(MathDefs.Equals(adapter.GetNeutralValue(), -1.0f));
        Assert.True(MathDefs.Equals(adapter.GetPositiveSensitivity(), 1.0f));
        Assert.True(MathDefs.Equals(adapter.GetNegativeSensitivity(), -0.5f));
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

        Assert.True(MathDefs.Equals(loaded.GetDeadZone(), 0.25f));
        Assert.True(loaded.IsInverted());
        Assert.True(MathDefs.Equals(loaded.GetNeutralValue(), -0.2f));
        Assert.True(MathDefs.Equals(loaded.GetPositiveSensitivity(), 0.75f));
        Assert.True(MathDefs.Equals(loaded.GetNegativeSensitivity(), -0.5f));
    }
}
