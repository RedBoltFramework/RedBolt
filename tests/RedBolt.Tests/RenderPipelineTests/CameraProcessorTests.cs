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

        Assert.True(processor.IsCameraOrthographic);
        Assert.True(processor.IsCameraClipped);
        Assert.False(processor.IsCameraReversed);
        Assert.Equal(FillMode.Wireframe, processor.CameraFillMode);
        Assert.NotEqual(0u, processor.PipelineStateHash);
    }

    [Fact]
    public void Render_callbacks_restore_user_flip()
    {
        var camera = new Camera();
        camera.SetFlipVertical(true);
        var processor = new CameraProcessor();
        processor.SetCameras([camera]);

        processor.OnRenderBegin(new FrameInfo());
        Assert.True(camera.GetFlipVertical());

        processor.OnRenderEnd(new FrameInfo());
        Assert.True(camera.GetFlipVertical());
    }

    [Fact]
    public void Dispose_releases_camera_vector_before_base()
    {
        var processor = new CameraProcessor();
        processor.SetCameras([new Camera()]);

        processor.Dispose();

        var cameras = typeof(CameraProcessor).GetField("_cameras", System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.NonPublic)!.GetValue(processor) as System.Collections.ICollection;
        Assert.NotNull(cameras);
        Assert.Equal(0, cameras.Count);
        Assert.False(typeof(CameraProcessor).IsSealed);
    }
}
