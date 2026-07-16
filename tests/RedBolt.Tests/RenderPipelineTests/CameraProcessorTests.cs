namespace RedBolt.Tests;

public sealed class CameraProcessorTests
{
    [Fact]
    public void SetCameras_caches_cpp_pipeline_state()
    {
        var camera = new Camera();
        camera.SetOrthographic(true);
        camera.SetFlipVertical(true);
        camera.SetUseReflection(true);
        camera.SetUseClipping(true);
        camera.SetFillMode(FillMode.Wireframe);
        var processor = new CameraProcessor();

        processor.SetCameras([camera]);

        Assert.True(processor.IsCameraOrthographic());
        Assert.True(processor.IsCameraClipped());
        Assert.False(processor.IsCameraReversed());
        Assert.Equal(FillMode.Wireframe, processor.GetCameraFillMode());
        Assert.NotEqual(0u, processor.GetPipelineStateHash());
    }

    [Fact]
    public void Render_callbacks_restore_user_flip()
    {
        var camera = new Camera();
        camera.SetFlipVertical(true);
        var processor = new CameraProcessor();
        processor.SetCameras([camera]);

        processor.OnRenderBegin(new CommonFrameInfo());
        Assert.True(camera.GetFlipVertical());

        processor.OnRenderEnd(new CommonFrameInfo());
        Assert.True(camera.GetFlipVertical());
    }
}
