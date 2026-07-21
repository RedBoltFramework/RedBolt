namespace RedBolt.Tests;

public sealed class RenderPipelineSettingsTests
{
    [Fact]
    public void Batch_renderer_settings_have_value_semantics()
    {
        var source = new BatchRendererSettings();
        var copy = source;

        copy.CubemapBoxProjection = true;

        Assert.False(source.CubemapBoxProjection);
        Assert.True(copy.CubemapBoxProjection);
        Assert.Equal(DrawableAmbientMode.Directional, source.AmbientMode);
        Assert.Equal(new Vector2f(0.0000001f, 0.9f), source.VarianceShadowMapParams);
    }

    [Fact]
    public void Batch_renderer_helpers_use_stack_only_value_storage()
    {
        var assembly = typeof(BatchRenderer).Assembly;
        Assert.True(assembly.GetType("RedBolt.ObjectParameterBuilder", true)!.IsValueType);
        Assert.True(assembly.GetType("RedBolt.DrawCommandCompositor", true)!.IsByRefLike);
    }

    [Fact]
    public void Propagate_implied_settings_matches_default_pipeline_rules()
    {
        var settings = new RenderPipelineSettings();
        settings.SceneProcessor.LightingMode = DirectLightingMode.DeferredBlinnPhong;
        var instancingBuffer = settings.InstancingBuffer;
        instancingBuffer.EnableInstancing = true;
        settings.InstancingBuffer = instancingBuffer;

        settings.PropagateImpliedSettings();

        Assert.True(settings.RenderBufferManager.ColorUsableWithMultipleRenderTargets);
        Assert.True(settings.RenderBufferManager.StencilBuffer);
        Assert.True(settings.RenderBufferManager.ReadableDepth);
        Assert.False(settings.RenderBufferManager.InheritMultiSampleLevel);
        Assert.Equal(10, settings.InstancingBuffer.NumInstancingTexCoords);
    }
}
