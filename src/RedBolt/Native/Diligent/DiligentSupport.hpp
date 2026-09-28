// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

#pragma once

#if PLATFORM_WEB || PLATFORM_ANDROID
#include <GLES3/gl3.h>
#ifndef GL_PROGRAM_SEPARABLE
#define GL_PROGRAM_SEPARABLE 0x8258
#endif
#else
#ifndef GLEW_STATIC
#define GLEW_STATIC
#endif
#include <GL/glew.h>
#endif

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
#include <d3d11.h>
#include <EngineFactoryD3D11.h>
#include <RenderDeviceD3D11.h>
#endif
#if D3D12_SUPPORTED
#include <d3d12.h>
#include <EngineFactoryD3D12.h>
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

/// Install Diligent debug message callback matching rbfx RenderDevice filtering
/// (skips noisy "Compiler output for shader" / "Failed shader full source" INFO logs).
void InstallDebugMessageCallback();

void* CreateTemporaryGLProgram(IShader* vertexShader, IShader* pixelShader, IShader* domainShader, IShader* hullShader,
    IShader* geometryShader, bool separablePrograms);
void ReleaseTemporaryGLProgramHandle(void* program);
void DeleteTemporaryGLProgram(void* program);
Uint32 GetTemporaryGLProgramVertexAttributeCount(const void* program);
const char* GetTemporaryGLProgramVertexAttributeName(const void* program, Uint32 index);
Uint32 GetTemporaryGLProgramVertexAttributeLocation(const void* program, Uint32 index);
bool BindTemporaryGLProgramVertexAttributes(const void* program, IPipelineState* pipelineState);
Uint32 GetTemporaryGLProgramUniformBlockCount(const void* program);
const char* GetTemporaryGLProgramUniformBlockName(void* program, Uint32 index);
Uint32 GetTemporaryGLProgramUniformBlockDataSize(const void* program, Uint32 index);
Uint32 GetTemporaryGLProgramUniformBlockIndex(const void* program, const char* name);
Uint32 GetTemporaryGLProgramUniformCount(const void* program);
const char* GetTemporaryGLProgramUniformName(void* program, Uint32 index);
Int32 GetTemporaryGLProgramUniformBlockIndex(const void* program, Uint32 index);
Int32 GetTemporaryGLProgramUniformBlockOffset(const void* program, Uint32 index);
Uint32 GetTemporaryGLProgramUniformSize(const void* program, Uint32 index);
bool GetTemporaryGLProgramUniformType(const void* program, Uint32 index, Uint32& variableClass, Uint32& basicType,
    Uint32& numRows, Uint32& numColumns, Uint32& arraySize);
ISwapChainGL* CreateProxySwapChainGL(IRenderDevice* device, IDeviceContext* deviceContext,
    const SwapChainDesc& swapChainDesc, void* window);
ISwapChainGL* CreateSecondaryProxySwapChainGL(IRenderDevice* device, IDeviceContext* deviceContext,
    const SwapChainDesc& swapChainDesc, void* window);
ISwapChain* CreateProxySwapChainMS(IRenderDevice* device, IDeviceContext* deviceContext,
    ISwapChain* nativeSwapChain, Uint32 depthFormat, Uint32 multiSample);
#if VULKAN_SUPPORTED
bool GetVulkanSurfaceExtent(IRenderDeviceVk* device, ISwapChainVk* swapChain, Uint32& width, Uint32& height);
#endif

} // namespace Diligent
