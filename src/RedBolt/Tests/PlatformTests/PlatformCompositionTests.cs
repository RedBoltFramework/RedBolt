using RedBolt;

namespace RedBolt.Tests;

public sealed class PlatformCompositionTests
{
    [Fact]
    public void Linux_platform_exposes_OpenGL_and_Vulkan_backends()
    {
        var runtimeServices = Context.Instance.RuntimeServices;

        Assert.True(runtimeServices.Platform.SupportsFileLogging);
        Assert.IsType<OpenGLGraphicsBackend>(runtimeServices.GetGraphicsBackend(RenderBackend.OpenGL));
        Assert.IsType<VulkanGraphicsBackend>(runtimeServices.GetGraphicsBackend(RenderBackend.Vulkan));
        Assert.NotNull(runtimeServices.GetGraphicsApi<OpenGLGraphicsBackend>(RenderBackend.OpenGL));
        Assert.Null(runtimeServices.GetGraphicsApi<VulkanGraphicsBackend>(RenderBackend.OpenGL));
    }

    [Fact]
    public void Linux_platform_rejects_unavailable_backend()
    {
        var runtimeServices = Context.Instance.RuntimeServices;

        Assert.Throws<InvalidOperationException>(() =>
            runtimeServices.GetGraphicsBackend(RenderBackend.D3D11));
    }
}
