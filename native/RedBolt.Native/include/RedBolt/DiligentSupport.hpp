#pragma once

#ifndef GLEW_STATIC
#define GLEW_STATIC
#endif
#include <GL/glew.h>

#include <EngineFactoryOpenGL.h>
#include <DeviceContextGL.h>
#include <PipelineStateGL.h>
#include <RenderDeviceGL.h>
#include <ShaderGL.h>
#include <SwapChainGL.h>

#if VULKAN_SUPPORTED
#define DILIGENT_USE_VOLK 1
#if DILIGENT_USE_VOLK
#define VK_NO_PROTOTYPES
#endif
#include <vulkan/vulkan.h>
#if DILIGENT_USE_VOLK
#include <volk.h>
#endif
#endif

#if GLES_SUPPORTED
#include <RenderDeviceGLES.h>
#endif

#if D3D11_SUPPORTED
#include <RenderDeviceD3D11.h>
#endif
#if D3D12_SUPPORTED
#include <RenderDeviceD3D12.h>
#endif
#if VULKAN_SUPPORTED
#include <DeviceContextVk.h>
#include <EngineFactoryVk.h>
#include <RenderDeviceVk.h>
#include <SwapChainVk.h>
#endif

namespace Diligent
{

const ShaderCodeVariableDesc* GetShaderCodeBufferVariableAt(const ShaderCodeBufferDesc& self, Uint32 index);

void* CreateTemporaryGLProgram(IShader* vertexShader, IShader* pixelShader, IShader* domainShader, IShader* hullShader,
    IShader* geometryShader, bool separablePrograms);
void DeleteTemporaryGLProgram(void* program);
Uint32 GetTemporaryGLProgramVertexAttributeCount(const void* program);
const char* GetTemporaryGLProgramVertexAttributeName(const void* program, Uint32 index);
Uint32 GetTemporaryGLProgramVertexAttributeLocation(const void* program, Uint32 index);
bool BindTemporaryGLProgramVertexAttributes(const void* program, IPipelineState* pipelineState);
ISwapChain* CreateProxySwapChainGL(IRenderDevice* device, IDeviceContext* deviceContext,
    const SwapChainDesc& swapChainDesc, void* window);
ISwapChain* CreateSecondaryProxySwapChainGL(IRenderDevice* device, IDeviceContext* deviceContext,
    const SwapChainDesc& swapChainDesc, void* window);
ISwapChain* CreateProxySwapChainMS(IRenderDevice* device, IDeviceContext* deviceContext,
    ISwapChain* nativeSwapChain, Uint32 depthFormat, Uint32 multiSample);
#if VULKAN_SUPPORTED
bool GetVulkanSurfaceExtent(IRenderDeviceVk* device, ISwapChainVk* swapChain, Uint32& width, Uint32& height);
#endif

} // namespace Diligent
