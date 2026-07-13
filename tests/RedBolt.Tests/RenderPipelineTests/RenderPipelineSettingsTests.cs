namespace RedBolt.Tests;

public sealed class RenderPipelineSettingsTests
{
    [Fact]
    public void Propagate_implied_settings_matches_default_pipeline_rules()
    {
        var settings = new RenderPipelineSettings();
        settings.SceneProcessor.LightingMode = DirectLightingMode.DeferredBlinnPhong;
        settings.InstancingBuffer.EnableInstancing = true;

        settings.PropagateImpliedSettings();

        Assert.True(settings.RenderBufferManager.ColorUsableWithMultipleRenderTargets);
        Assert.True(settings.RenderBufferManager.StencilBuffer);
        Assert.True(settings.RenderBufferManager.ReadableDepth);
        Assert.False(settings.RenderBufferManager.InheritMultiSampleLevel);
        Assert.Equal(10u, settings.InstancingBuffer.NumInstancingTexCoords);
    }
}
