namespace RedBolt.Tests;

public sealed class RenderPipelineSettingsTests
{
    [Fact]
    public void Native_settings_pods_have_value_semantics()
    {
        Assert.True(typeof(RenderBufferManagerSettings).IsValueType);
        Assert.True(typeof(LightProcessorCacheSettings).IsValueType);
        Assert.True(typeof(DrawableProcessorSettings).IsValueType);
        Assert.True(typeof(InstancingBufferSettings).IsValueType);
        Assert.True(typeof(BatchRendererSettings).IsValueType);
        Assert.True(typeof(ShadowMapAllocatorSettings).IsValueType);
        Assert.True(typeof(OcclusionBufferSettings).IsValueType);
        Assert.True(typeof(SceneProcessorSettings).IsValueType);
        Assert.True(typeof(ShaderProgramCompositorSettings).IsValueType);
        Assert.True(typeof(RenderPipelineSettings).IsValueType);

        var source = new RenderPipelineSettings();
        var copy = source;
        var sceneProcessor = copy.SceneProcessor;
        sceneProcessor.MaxPixelLights = 17;
        copy.SceneProcessor = sceneProcessor;

        Assert.Equal(4, source.SceneProcessor.MaxPixelLights);
        Assert.Equal(17, copy.SceneProcessor.MaxPixelLights);
    }

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
        var sceneProcessor = settings.SceneProcessor;
        sceneProcessor.LightingMode = DirectLightingMode.DeferredBlinnPhong;
        settings.SceneProcessor = sceneProcessor;
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
