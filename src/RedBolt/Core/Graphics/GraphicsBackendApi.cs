namespace RedBolt;

using RedBolt.Bindings.Diligent;

#if !REDBOLT_PLATFORM_BROWSER && !REDBOLT_PLATFORM_ANDROID
/// <summary>Vulkan-specific graphics objects exposed by the active backend.</summary>
public interface IVulkanGraphicsApi
{
    ISwapChainVk SwapChain { get; }
}
#endif
