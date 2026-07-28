namespace RedBolt.Tests;

using RedBolt.Graphics;

public sealed class LightBakingSettingsTests
{
    [Fact]
    public void Aggregated_defaults_match_cpp()
    {
        var settings = new LightBakingSettings();

        Assert.Equal(512u, settings.Charting.LightmapSize);
        Assert.Equal("RenderPaths/LightmapGBuffer.xml", settings.GeometryBufferBaking.RenderPathName);
        Assert.Equal(10u, settings.DirectChartTracing.MaxSamples);
        Assert.Equal(32u, settings.DirectProbesTracing.MaxSamples);
        Assert.Equal(10u, settings.IndirectChartTracing.MaxSamples);
        Assert.Equal(64u, settings.IndirectProbesTracing.MaxSamples);
        Assert.Equal(2u, settings.IndirectChartTracing.MaxBounces);
        Assert.Equal(2, settings.DirectFilter.KernelRadius);
        Assert.Equal(5, settings.IndirectFilter.KernelRadius);
        Assert.Equal(Vector3f.One * 128.0f, settings.Incremental.ChunkSize);
        Assert.Equal("GI.bin", settings.Incremental.GIDataFileName);
    }

    [Fact]
    public void Tracing_constructors_match_cpp()
    {
        var direct = new DirectLightTracingSettings(27);
        var indirect = new IndirectLightTracingSettings(35, 6);

        Assert.Equal(1u, direct.NumTasks);
        Assert.Equal(27u, direct.MaxSamples);
        Assert.Equal(1u, indirect.NumTasks);
        Assert.Equal(35u, indirect.MaxSamples);
        Assert.Equal(6u, indirect.MaxBounces);
        Assert.Equal(8u, IndirectLightTracingSettings.MaximumBounces);
    }

    [Fact]
    public void Aggregated_settings_copy_independently()
    {
        var source = new LightBakingSettings();
        var copy = source;

        copy.Charting.LightmapSize = 1024;
        copy.Incremental.OutputDirectory = "Baked";

        Assert.Equal(512u, source.Charting.LightmapSize);
        Assert.Equal(string.Empty, source.Incremental.OutputDirectory);
        Assert.Equal(1024u, copy.Charting.LightmapSize);
        Assert.Equal("Baked", copy.Incremental.OutputDirectory);
    }
}
