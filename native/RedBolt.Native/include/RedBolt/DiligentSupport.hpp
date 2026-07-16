#pragma once

#ifndef GLEW_STATIC
#define GLEW_STATIC
#endif
#include <GL/glew.h>

#include <EngineFactoryOpenGL.h>
#include <PipelineStateGL.h>
#include <RenderDeviceGL.h>
#include <ShaderGL.h>

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
ISwapChain* CreateSecondaryProxySwapChainGL(IRenderDevice* device, IDeviceContext* deviceContext,
    const SwapChainDesc& swapChainDesc, void* window);

} // namespace Diligent
