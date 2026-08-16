// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt;

using RedBolt.Bindings.Diligent;

#if !REDBOLT_PLATFORM_BROWSER && !REDBOLT_PLATFORM_ANDROID
/// <summary>Vulkan-specific graphics objects exposed by the active backend.</summary>
public interface IVulkanGraphicsApi
{
    ISwapChainVk SwapChain { get; }
}
#endif
