namespace RedBolt;

using RedBolt.Bindings.Diligent;

/// <summary>Vulkan-specific graphics objects exposed by the active backend.</summary>
public interface IVulkanGraphicsApi
{
    ISwapChainVk SwapChain { get; }
}
