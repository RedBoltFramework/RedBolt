#pragma once

#if DILIGENT_C_INTERFACE

// Diligent's C GetDesc macros cast a reference directly to a pointer when
// consumed from C++. Take the address explicitly for generated C++ shims.
#define REDBOLT_DILIGENT_DEFINE_GET_DESC(Interface, Desc)                                  \
    inline const struct Desc* RedBolt##Interface##_GetDesc(struct Interface* value)        \
    {                                                                                      \
        return reinterpret_cast<const struct Desc*>(                                       \
            &IDeviceObject_GetDesc(reinterpret_cast<struct IDeviceObject*>(value)));       \
    }
REDBOLT_DILIGENT_DEFINE_GET_DESC(IBottomLevelAS, BottomLevelASDesc)
REDBOLT_DILIGENT_DEFINE_GET_DESC(IBuffer, BufferDesc)
REDBOLT_DILIGENT_DEFINE_GET_DESC(IBufferView, BufferViewDesc)
REDBOLT_DILIGENT_DEFINE_GET_DESC(IDeviceMemory, DeviceMemoryDesc)
REDBOLT_DILIGENT_DEFINE_GET_DESC(IFence, FenceDesc)
REDBOLT_DILIGENT_DEFINE_GET_DESC(IPipelineResourceSignature, PipelineResourceSignatureDesc)
REDBOLT_DILIGENT_DEFINE_GET_DESC(IPipelineState, PipelineStateDesc)
REDBOLT_DILIGENT_DEFINE_GET_DESC(IQuery, QueryDesc)
REDBOLT_DILIGENT_DEFINE_GET_DESC(ISampler, SamplerDesc)
REDBOLT_DILIGENT_DEFINE_GET_DESC(IShader, ShaderDesc)
REDBOLT_DILIGENT_DEFINE_GET_DESC(IShaderBindingTable, ShaderBindingTableDesc)
REDBOLT_DILIGENT_DEFINE_GET_DESC(ITexture, TextureDesc)
REDBOLT_DILIGENT_DEFINE_GET_DESC(ITextureView, TextureViewDesc)
REDBOLT_DILIGENT_DEFINE_GET_DESC(ITopLevelAS, TopLevelASDesc)
#undef REDBOLT_DILIGENT_DEFINE_GET_DESC

#undef IBottomLevelAS_GetDesc
#define IBottomLevelAS_GetDesc(This) RedBoltIBottomLevelAS_GetDesc(This)
#undef IBuffer_GetDesc
#define IBuffer_GetDesc(This) RedBoltIBuffer_GetDesc(This)
#undef IBufferView_GetDesc
#define IBufferView_GetDesc(This) RedBoltIBufferView_GetDesc(This)
#undef IDeviceMemory_GetDesc
#define IDeviceMemory_GetDesc(This) RedBoltIDeviceMemory_GetDesc(This)
#undef IFence_GetDesc
#define IFence_GetDesc(This) RedBoltIFence_GetDesc(This)
#undef IPipelineResourceSignature_GetDesc
#define IPipelineResourceSignature_GetDesc(This) RedBoltIPipelineResourceSignature_GetDesc(This)
#undef IPipelineState_GetDesc
#define IPipelineState_GetDesc(This) RedBoltIPipelineState_GetDesc(This)
#undef IQuery_GetDesc
#define IQuery_GetDesc(This) RedBoltIQuery_GetDesc(This)
#undef ISampler_GetDesc
#define ISampler_GetDesc(This) RedBoltISampler_GetDesc(This)
#undef IShader_GetDesc
#define IShader_GetDesc(This) RedBoltIShader_GetDesc(This)
#undef IShaderBindingTable_GetDesc
#define IShaderBindingTable_GetDesc(This) RedBoltIShaderBindingTable_GetDesc(This)
#undef ITexture_GetDesc
#define ITexture_GetDesc(This) RedBoltITexture_GetDesc(This)
#undef ITextureView_GetDesc
#define ITextureView_GetDesc(This) RedBoltITextureView_GetDesc(This)
#undef ITopLevelAS_GetDesc
#define ITopLevelAS_GetDesc(This) RedBoltITopLevelAS_GetDesc(This)

extern "C"
{

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
Uint32 GetTemporaryGLProgramUniformBlockIndexByName(const void* program, const char* name);
Uint32 GetTemporaryGLProgramUniformCount(const void* program);
const char* GetTemporaryGLProgramUniformName(void* program, Uint32 index);
Int32 GetTemporaryGLProgramUniformBlockIndexByIndex(const void* program, Uint32 index);
Int32 GetTemporaryGLProgramUniformBlockOffset(const void* program, Uint32 index);
Uint32 GetTemporaryGLProgramUniformSize(const void* program, Uint32 index);
ISwapChainGL* CreateProxySwapChainGL(IRenderDevice* device, IDeviceContext* deviceContext,
    const SwapChainDesc* swapChainDesc, void* window);
ISwapChainGL* CreateSecondaryProxySwapChainGL(IRenderDevice* device, IDeviceContext* deviceContext,
    const SwapChainDesc* swapChainDesc, void* window);
ISwapChain* CreateProxySwapChainMS(IRenderDevice* device, IDeviceContext* deviceContext,
    ISwapChain* nativeSwapChain, Uint32 depthFormat, Uint32 multiSample);
TextureFormatAttribs RedBoltGetTextureFormatAttribs(TEXTURE_FORMAT format);
IDeviceContextGL* RedBoltQueryIDeviceContextGL(IObject* object);
IRenderDeviceGL* RedBoltQueryIRenderDeviceGL(IObject* object);
#if GLES_SUPPORTED
IRenderDeviceGLES* RedBoltQueryIRenderDeviceGLES(IObject* object);
#endif
#if D3D11_SUPPORTED
IRenderDeviceD3D11* RedBoltQueryIRenderDeviceD3D11(IObject* object);
#endif
#if D3D12_SUPPORTED
IRenderDeviceD3D12* RedBoltQueryIRenderDeviceD3D12(IObject* object);
#endif
#if VULKAN_SUPPORTED
IEngineFactoryVk* RedBoltQueryIEngineFactoryVk(IObject* object);
IRenderDeviceVk* RedBoltQueryIRenderDeviceVk(IObject* object);
ISwapChainVk* RedBoltQueryISwapChainVk(IObject* object);
bool GetVulkanSurfaceExtent(IRenderDeviceVk* device, ISwapChainVk* swapChain, Uint32* width, Uint32* height);
#endif

}

#else

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

#endif
