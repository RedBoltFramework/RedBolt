// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

#include "DiligentSupport.hpp"

#include <RenderDeviceGL.h>
#include <SwapChainGL.h>
#include <SwapChainBase.hpp>
#include <DefaultRawMemoryAllocator.hpp>
#include <ObjectBase.hpp>
#include <DebugOutput.h>
#include <SDL.h>

#include <cstring>
#include <stdexcept>
#include <string>
#include <vector>

namespace Diligent
{

namespace
{

// Port of rbfx RenderDevice.cpp SkipInfoMessage + DebugMessageCallback.
// C++ formats as "[diligent] {msg}" then skips messages starting with these prefixes.
bool SkipInfoMessage(const char* message)
{
    if (message == nullptr)
        return false;

    static const char* skipPrefixes[] = {
        "Compiler output for shader",
        "Failed shader full source",
    };
    for (const char* prefix : skipPrefixes)
    {
        const auto len = std::strlen(prefix);
        if (std::strncmp(message, prefix, len) == 0)
            return true;
    }
    return false;
}

DebugMessageCallbackType g_PreviousDebugMessageCallback = nullptr;

void RedBoltDebugMessageCallback(DEBUG_MESSAGE_SEVERITY severity, const Char* message, const Char* function,
    const Char* file, int line)
{
    if (severity == DEBUG_MESSAGE_SEVERITY_INFO && SkipInfoMessage(message))
        return;

    if (g_PreviousDebugMessageCallback != nullptr)
        g_PreviousDebugMessageCallback(severity, message, function, file, line);
}

} // namespace

void InstallDebugMessageCallback()
{
    // C++: Diligent::SetDebugMessageCallback(&DebugMessageCallback); in RenderDevice ctor.
    if (g_PreviousDebugMessageCallback != nullptr)
        return;

    g_PreviousDebugMessageCallback = DebugMessageCallback;
    SetDebugMessageCallback(&RedBoltDebugMessageCallback);
}

class TemporaryGLProgram
{
public:
    Uint32 programObject_{};
    std::vector<std::string> vertexAttributeNames_;
    std::vector<Uint32> vertexAttributeLocations_;
    std::string uniformName_;
    TemporaryGLProgram(IShader* vertexShader, IShader* pixelShader, IShader* domainShader, IShader* hullShader,
        IShader* geometryShader, bool separablePrograms)
    {
    programObject_ = glCreateProgram();
    if (!programObject_)
        return;

    if (separablePrograms)
        glProgramParameteri(programObject_, GL_PROGRAM_SEPARABLE, GL_TRUE);

    IShader* shaders[] = {vertexShader, pixelShader, domainShader, hullShader, geometryShader};
    for (IShader* shader : shaders)
    {
        if (shader && (!separablePrograms || shader->GetDesc().ShaderType == SHADER_TYPE_VERTEX))
        {
            glAttachShader(programObject_, static_cast<IShaderGL*>(shader)->GetGLShaderHandle());
            if (glGetError() != GL_NO_ERROR)
                return;
        }
    }

    glLinkProgram(programObject_);
    if (glGetError() != GL_NO_ERROR)
        return;

    int isLinked = GL_FALSE;
    glGetProgramiv(programObject_, GL_LINK_STATUS, &isLinked);
    if (glGetError() != GL_NO_ERROR || !isLinked)
        return;

    int numActiveAttribs = 0;
    int maxNameLength = 0;
    glGetProgramiv(programObject_, GL_ACTIVE_ATTRIBUTES, &numActiveAttribs);
    glGetProgramiv(programObject_, GL_ACTIVE_ATTRIBUTE_MAX_LENGTH, &maxNameLength);

    std::string attributeName(maxNameLength, '\0');
    for (int attribIndex = 0; attribIndex < numActiveAttribs; ++attribIndex)
    {
        int attributeSize = 0;
        unsigned attributeType = 0;
        glGetActiveAttrib(programObject_, attribIndex, maxNameLength, nullptr, &attributeSize, &attributeType,
            attributeName.data());
        const int location = glGetAttribLocation(programObject_, attributeName.c_str());
        if (location == -1)
            continue;
        vertexAttributeNames_.emplace_back(attributeName.c_str());
        vertexAttributeLocations_.push_back(static_cast<Uint32>(location));
    }
    }

    ~TemporaryGLProgram()
    {
        ReleaseHandle();
    }

    void ReleaseHandle()
    {
        if (programObject_)
        {
            glDeleteProgram(programObject_);
            programObject_ = 0;
        }
    }
};

void* CreateTemporaryGLProgram(IShader* vertexShader, IShader* pixelShader, IShader* domainShader, IShader* hullShader,
    IShader* geometryShader, bool separablePrograms)
{
    return new TemporaryGLProgram{vertexShader, pixelShader, domainShader, hullShader, geometryShader, separablePrograms};
}

void DeleteTemporaryGLProgram(void* program)
{
    delete static_cast<TemporaryGLProgram*>(program);
}

void ReleaseTemporaryGLProgramHandle(void* program)
{
    static_cast<TemporaryGLProgram*>(program)->ReleaseHandle();
}

Uint32 GetTemporaryGLProgramVertexAttributeCount(const void* program)
{
    return static_cast<Uint32>(static_cast<const TemporaryGLProgram*>(program)->vertexAttributeNames_.size());
}

const char* GetTemporaryGLProgramVertexAttributeName(const void* program, Uint32 index)
{
    return static_cast<const TemporaryGLProgram*>(program)->vertexAttributeNames_.at(index).c_str();
}

Uint32 GetTemporaryGLProgramVertexAttributeLocation(const void* program, Uint32 index)
{
    return static_cast<const TemporaryGLProgram*>(program)->vertexAttributeLocations_.at(index);
}

bool BindTemporaryGLProgramVertexAttributes(const void* program, IPipelineState* pipelineState)
{
    const auto temporaryProgram = static_cast<const TemporaryGLProgram*>(program);
    const auto pipelineStateGl = static_cast<IPipelineStateGL*>(pipelineState);
    const Uint32 programObject = pipelineStateGl->GetGLProgramHandle(SHADER_TYPE_VERTEX);
    int isLinked = GL_FALSE;
    glGetProgramiv(programObject, GL_LINK_STATUS, &isLinked);
    if (!isLinked)
        return false;

    for (Uint32 i = 0; i < temporaryProgram->vertexAttributeNames_.size(); ++i)
        glBindAttribLocation(programObject, temporaryProgram->vertexAttributeLocations_[i],
            temporaryProgram->vertexAttributeNames_[i].c_str());
    return true;
}

Uint32 GetTemporaryGLProgramUniformBlockCount(const void* program)
{
    GLint count = 0;
    glGetProgramiv(static_cast<const TemporaryGLProgram*>(program)->programObject_, GL_ACTIVE_UNIFORM_BLOCKS, &count);
    return static_cast<Uint32>(count);
}

const char* GetTemporaryGLProgramUniformBlockName(void* program, Uint32 index)
{
    auto* temporaryProgram = static_cast<TemporaryGLProgram*>(program);
    char name[256]{};
    glGetActiveUniformBlockName(temporaryProgram->programObject_, index, sizeof(name), nullptr, name);
    temporaryProgram->uniformName_ = name;
    return temporaryProgram->uniformName_.c_str();
}

Uint32 GetTemporaryGLProgramUniformBlockDataSize(const void* program, Uint32 index)
{
    GLint size = 0;
    glGetActiveUniformBlockiv(static_cast<const TemporaryGLProgram*>(program)->programObject_, index,
        GL_UNIFORM_BLOCK_DATA_SIZE, &size);
    return static_cast<Uint32>(size);
}

Uint32 GetTemporaryGLProgramUniformBlockIndex(const void* program, const char* name)
{
    return glGetUniformBlockIndex(static_cast<const TemporaryGLProgram*>(program)->programObject_, name);
}

Uint32 GetTemporaryGLProgramUniformCount(const void* program)
{
    GLint count = 0;
    glGetProgramiv(static_cast<const TemporaryGLProgram*>(program)->programObject_, GL_ACTIVE_UNIFORMS, &count);
    return static_cast<Uint32>(count);
}

const char* GetTemporaryGLProgramUniformName(void* program, Uint32 index)
{
    auto* temporaryProgram = static_cast<TemporaryGLProgram*>(program);
    char name[256]{};
    glGetActiveUniform(temporaryProgram->programObject_, index, sizeof(name), nullptr, nullptr, nullptr, name);
    temporaryProgram->uniformName_ = name;
    return temporaryProgram->uniformName_.c_str();
}

Int32 GetTemporaryGLProgramUniformBlockIndex(const void* program, Uint32 index)
{
    GLint blockIndex = 0;
    glGetActiveUniformsiv(static_cast<const TemporaryGLProgram*>(program)->programObject_, 1, &index,
        GL_UNIFORM_BLOCK_INDEX, &blockIndex);
    return blockIndex;
}

Int32 GetTemporaryGLProgramUniformBlockOffset(const void* program, Uint32 index)
{
    GLint blockOffset = 0;
    glGetActiveUniformsiv(static_cast<const TemporaryGLProgram*>(program)->programObject_, 1, &index,
        GL_UNIFORM_OFFSET, &blockOffset);
    return blockOffset;
}

Uint32 GetTemporaryGLProgramUniformSize(const void* program, Uint32 index)
{
    GLint elementCount = 0;
    GLenum type = 0;
    char name[256]{};
    glGetActiveUniform(static_cast<const TemporaryGLProgram*>(program)->programObject_, index, sizeof(name), nullptr,
        &elementCount, &type, name);

    Uint32 numElements = 0;
    Uint32 numVectors = 1;
    switch (type)
    {
    case GL_BOOL:
    case GL_INT:
    case GL_FLOAT: numElements = 1; break;
    case GL_FLOAT_VEC2: numElements = 2; break;
    case GL_FLOAT_VEC3: numElements = 3; break;
    case GL_FLOAT_VEC4: numElements = 4; break;
    case GL_FLOAT_MAT3:
        numElements = 3;
        numVectors = 3;
        break;
    case GL_FLOAT_MAT3x4:
        numElements = 4;
        numVectors = 3;
        break;
    case GL_FLOAT_MAT4:
        numElements = 4;
        numVectors = 4;
        break;
    default: return 0;
    }

    if (elementCount > 1 || numVectors > 1)
    {
        const Uint32 alignment = 4 * sizeof(float);
        const Uint32 elementSize = numElements * sizeof(float);
        return elementCount * numVectors * ((elementSize + alignment - 1) / alignment) * alignment;
    }
    return numElements * sizeof(float);
}

bool GetTemporaryGLProgramUniformType(const void* program, Uint32 index, Uint32& variableClass, Uint32& basicType,
    Uint32& numRows, Uint32& numColumns, Uint32& arraySize)
{
    GLint elementCount = 0;
    GLenum type = 0;
    char name[256]{};
    glGetActiveUniform(static_cast<const TemporaryGLProgram*>(program)->programObject_, index, sizeof(name), nullptr,
        &elementCount, &type, name);

    // Mirrors Diligent GLDataTypeToShaderCodeVariableDesc (GLTypeConversions.cpp) for the uniform types
    // supported by GetTemporaryGLProgramUniformSize, so GL program reflection matches native shader reflection.
    SHADER_CODE_VARIABLE_CLASS resultClass = SHADER_CODE_VARIABLE_CLASS_UNKNOWN;
    SHADER_CODE_BASIC_TYPE resultBasicType = SHADER_CODE_BASIC_TYPE_UNKNOWN;
    Uint32 resultRows = 0;
    Uint32 resultColumns = 0;
    switch (type)
    {
    case GL_BOOL:
        resultClass = SHADER_CODE_VARIABLE_CLASS_SCALAR;
        resultBasicType = SHADER_CODE_BASIC_TYPE_BOOL;
        resultRows = 1;
        resultColumns = 1;
        break;
    case GL_INT:
        resultClass = SHADER_CODE_VARIABLE_CLASS_SCALAR;
        resultBasicType = SHADER_CODE_BASIC_TYPE_INT;
        resultRows = 1;
        resultColumns = 1;
        break;
    case GL_FLOAT:
        resultClass = SHADER_CODE_VARIABLE_CLASS_SCALAR;
        resultBasicType = SHADER_CODE_BASIC_TYPE_FLOAT;
        resultRows = 1;
        resultColumns = 1;
        break;
    case GL_FLOAT_VEC2:
    case GL_FLOAT_VEC3:
    case GL_FLOAT_VEC4:
        resultClass = SHADER_CODE_VARIABLE_CLASS_VECTOR;
        resultBasicType = SHADER_CODE_BASIC_TYPE_FLOAT;
        resultRows = type == GL_FLOAT_VEC2 ? 2u : type == GL_FLOAT_VEC3 ? 3u : 4u;
        resultColumns = 1;
        break;
    case GL_FLOAT_MAT3:
        resultClass = SHADER_CODE_VARIABLE_CLASS_MATRIX_COLUMNS;
        resultBasicType = SHADER_CODE_BASIC_TYPE_FLOAT;
        resultRows = 3;
        resultColumns = 3;
        break;
    case GL_FLOAT_MAT3x4:
        resultClass = SHADER_CODE_VARIABLE_CLASS_MATRIX_COLUMNS;
        resultBasicType = SHADER_CODE_BASIC_TYPE_FLOAT;
        resultRows = 4;
        resultColumns = 3;
        break;
    case GL_FLOAT_MAT4:
        resultClass = SHADER_CODE_VARIABLE_CLASS_MATRIX_COLUMNS;
        resultBasicType = SHADER_CODE_BASIC_TYPE_FLOAT;
        resultRows = 4;
        resultColumns = 4;
        break;
    default: break;
    }

    variableClass = static_cast<Uint32>(resultClass);
    basicType = static_cast<Uint32>(resultBasicType);
    numRows = resultRows;
    numColumns = resultColumns;
    // Diligent reports non-array variables with ArraySize == 0.
    arraySize = elementCount > 1 ? static_cast<Uint32>(elementCount) : 0u;
    return resultClass != SHADER_CODE_VARIABLE_CLASS_UNKNOWN;
}

class ProxySwapChainGL : public SwapChainBase<ISwapChainGL>
{
public:
    using TBase = SwapChainBase<ISwapChainGL>;

    ProxySwapChainGL(IReferenceCounters* refCounters, IRenderDevice* device, IDeviceContext* deviceContext,
        const SwapChainDesc& swapChainDesc, SDL_Window* window)
        : TBase(refCounters, device, deviceContext, swapChainDesc)
        , window_(window)
    {
        InitializeParameters();
        CreateDummyBuffers();
    }

    void DILIGENT_CALL_TYPE Present(Uint32 syncInterval) override { SDL_GL_SwapWindow(window_); }

    void DILIGENT_CALL_TYPE SetFullscreenMode(const DisplayModeAttribs& displayMode) override
    {
        VERIFY_EXPR(false && "Fullscreen mode cannot be set through the proxy swap chain");
    }

    void DILIGENT_CALL_TYPE SetWindowedMode() override
    {
        VERIFY_EXPR(false && "Windowed mode cannot be set through the proxy swap chain");
    }

    void DILIGENT_CALL_TYPE Resize(Uint32 newWidth, Uint32 newHeight, SURFACE_TRANSFORM newPreTransform) override
    {
        if (newPreTransform == SURFACE_TRANSFORM_OPTIMAL)
            newPreTransform = SURFACE_TRANSFORM_IDENTITY;
        VERIFY_EXPR(newPreTransform == SURFACE_TRANSFORM_IDENTITY && "Unsupported pre-transform");

        if (TBase::Resize(newWidth, newHeight, newPreTransform))
            CreateDummyBuffers();
    }

    GLuint DILIGENT_CALL_TYPE GetDefaultFBO() const override { return defaultFBO_; }

    ITextureView* DILIGENT_CALL_TYPE GetCurrentBackBufferRTV() override { return renderTargetView_; }
    ITextureView* DILIGENT_CALL_TYPE GetDepthBufferDSV() override { return depthStencilView_; }

private:
    void InitializeParameters()
    {
        if (m_SwapChainDesc.PreTransform == SURFACE_TRANSFORM_OPTIMAL)
            m_SwapChainDesc.PreTransform = SURFACE_TRANSFORM_IDENTITY;

#if PLATFORM_IOS || PLATFORM_TVOS || PLATFORM_ANDROID
        glGetIntegerv(GL_FRAMEBUFFER_BINDING, reinterpret_cast<GLint*>(&defaultFBO_));
#endif

        int width{};
        int height{};
        SDL_GL_GetDrawableSize(window_, &width, &height);
        m_SwapChainDesc.Width = static_cast<Uint32>(width);
        m_SwapChainDesc.Height = static_cast<Uint32>(height);

        m_SwapChainDesc.ColorBufferFormat = IsSRGB() ? TEX_FORMAT_RGBA8_UNORM_SRGB : TEX_FORMAT_RGBA8_UNORM;
        m_SwapChainDesc.DepthBufferFormat = GetDepthStencilFormat();
    }

    void CreateDummyBuffers()
    {
        if (m_SwapChainDesc.Width == 0 || m_SwapChainDesc.Height == 0)
            return;

        RefCntAutoPtr<IRenderDeviceGL> deviceGL(m_pRenderDevice, IID_RenderDeviceGL);

        TextureDesc dummyTexDesc;
        dummyTexDesc.Name = "Back buffer proxy";
        dummyTexDesc.Type = RESOURCE_DIM_TEX_2D;
        dummyTexDesc.Format = m_SwapChainDesc.ColorBufferFormat;
        dummyTexDesc.Width = m_SwapChainDesc.Width;
        dummyTexDesc.Height = m_SwapChainDesc.Height;
        dummyTexDesc.BindFlags = BIND_RENDER_TARGET;
        RefCntAutoPtr<ITexture> dummyRenderTarget;
        deviceGL->CreateDummyTexture(dummyTexDesc, RESOURCE_STATE_RENDER_TARGET, &dummyRenderTarget);
        renderTargetView_ = dummyRenderTarget->GetDefaultView(TEXTURE_VIEW_RENDER_TARGET);

        dummyTexDesc.Name = "Depth buffer proxy";
        dummyTexDesc.Format = m_SwapChainDesc.DepthBufferFormat;
        dummyTexDesc.BindFlags = BIND_DEPTH_STENCIL;
        RefCntAutoPtr<ITexture> dummyDepthBuffer;
        deviceGL->CreateDummyTexture(dummyTexDesc, RESOURCE_STATE_DEPTH_WRITE, &dummyDepthBuffer);
        depthStencilView_ = dummyDepthBuffer->GetDefaultView(TEXTURE_VIEW_DEPTH_STENCIL);
    }

    bool IsSRGB() const
    {
        int effectiveSRGB{};
        if (SDL_GL_GetAttribute(SDL_GL_FRAMEBUFFER_SRGB_CAPABLE, &effectiveSRGB) != 0)
            return false;
        return effectiveSRGB != 0;
    }

    TEXTURE_FORMAT GetDepthStencilFormat() const
    {
        static const TEXTURE_FORMAT defaultFormat = TEX_FORMAT_D24_UNORM_S8_UINT;

        int effectiveDepthBits{};
        if (SDL_GL_GetAttribute(SDL_GL_DEPTH_SIZE, &effectiveDepthBits) != 0)
            return defaultFormat;
        int effectiveStencilBits{};
        if (SDL_GL_GetAttribute(SDL_GL_STENCIL_SIZE, &effectiveStencilBits) != 0)
            return defaultFormat;

        if (effectiveDepthBits == 16 && effectiveStencilBits == 0)
            return TEX_FORMAT_D16_UNORM;
        else if (effectiveDepthBits == 24 && effectiveStencilBits == 0)
            return TEX_FORMAT_D24_UNORM_S8_UINT;
        else if (effectiveDepthBits == 24 && effectiveStencilBits == 8)
            return TEX_FORMAT_D24_UNORM_S8_UINT;
        else if (effectiveDepthBits == 32 && effectiveStencilBits == 0)
            return TEX_FORMAT_D32_FLOAT;
        else if (effectiveDepthBits == 32 && effectiveStencilBits == 8)
            return TEX_FORMAT_D32_FLOAT_S8X24_UINT;
        else
            return defaultFormat;
    }

    SDL_Window* window_{};

    RefCntAutoPtr<ITextureView> renderTargetView_;
    RefCntAutoPtr<ITextureView> depthStencilView_;

    GLuint defaultFBO_{};
};

ISwapChainGL* CreateProxySwapChainGL(IRenderDevice* device, IDeviceContext* deviceContext,
    const SwapChainDesc& swapChainDesc, void* window)
{
    auto& defaultAllocator = DefaultRawMemoryAllocator::GetAllocator();
    auto swapChain = NEW_RC_OBJ(defaultAllocator, "ProxySwapChainGL instance", ProxySwapChainGL)(
        device, deviceContext, swapChainDesc, static_cast<SDL_Window*>(window));
    RefCntAutoPtr<ISwapChainGL> result{swapChain};
    return result.Detach();
}

ISwapChainGL* CreateSecondaryProxySwapChainGL(IRenderDevice* device, IDeviceContext* deviceContext,
    const SwapChainDesc& swapChainDesc, void* window)
{
    auto& defaultAllocator = DefaultRawMemoryAllocator::GetAllocator();
    auto secondarySwapChain = NEW_RC_OBJ(defaultAllocator, "Secondary ProxySwapChainGL instance", ProxySwapChainGL)(
        device, deviceContext, swapChainDesc, static_cast<SDL_Window*>(window));
    RefCntAutoPtr<ISwapChainGL> result{secondarySwapChain};
    return result.Detach();
}

class ProxySwapChainMS : public ObjectBase<ISwapChain>
{
public:
    using TBase = ObjectBase<ISwapChain>;

    ProxySwapChainMS(IReferenceCounters* refCounters, IRenderDevice* device,
        IDeviceContext* deviceContext, ISwapChain* nativeSwapChain, TEXTURE_FORMAT depthFormat,
        Uint32 multiSample)
        : TBase(refCounters)
        , depthFormat_(depthFormat)
        , multiSample_(multiSample)
        , nativeSwapChain_(nativeSwapChain)
        , renderDevice_(device)
        , immediateContext_(deviceContext)
    {
        CreateDepthStencil();
        CreateRenderTarget();
        UpdateDesc();
    }

    /// Implement ISwapChainGL
    /// @{
    void DILIGENT_CALL_TYPE Present(Uint32 SyncInterval) override
    {
        if (msaaRenderTarget_)
        {
            ITexture* currentBackBuffer = nativeSwapChain_->GetCurrentBackBufferRTV()->GetTexture();

            ResolveTextureSubresourceAttribs resolveAttribs;
            resolveAttribs.SrcTextureTransitionMode = RESOURCE_STATE_TRANSITION_MODE_TRANSITION;
            resolveAttribs.DstTextureTransitionMode = RESOURCE_STATE_TRANSITION_MODE_TRANSITION;
            immediateContext_->ResolveTextureSubresource(msaaRenderTarget_, currentBackBuffer, resolveAttribs);
        }

        nativeSwapChain_->Present(SyncInterval);
    }

    const SwapChainDesc& DILIGENT_CALL_TYPE GetDesc() const override { return swapChainDesc_; }

    void DILIGENT_CALL_TYPE Resize(Uint32 newWidth, Uint32 newHeight, SURFACE_TRANSFORM NewTransform) override
    {
        const SwapChainDesc& swapChainDesc = nativeSwapChain_->GetDesc();
        const Uint32 oldWidth = swapChainDesc.Width;
        const Uint32 oldHeight = swapChainDesc.Height;

        nativeSwapChain_->Resize(newWidth, newHeight, NewTransform);
        UpdateDesc();

        if (swapChainDesc.Width != oldWidth || swapChainDesc.Height != oldHeight)
        {
            CreateDepthStencil();
            CreateRenderTarget();
        }
    }

    void DILIGENT_CALL_TYPE SetFullscreenMode(const DisplayModeAttribs& DisplayMode) override
    {
        VERIFY_EXPR(false && "Fullscreen mode cannot be set through the proxy swap chain");
    }

    void DILIGENT_CALL_TYPE SetWindowedMode() override
    {
        VERIFY_EXPR(false && "Fullscreen mode cannot be set through the proxy swap chain");
    }

    void DILIGENT_CALL_TYPE SetMaximumFrameLatency(Uint32 MaxLatency) override
    {
        nativeSwapChain_->SetMaximumFrameLatency(MaxLatency);
    }

    ITextureView* DILIGENT_CALL_TYPE GetCurrentBackBufferRTV() override
    {
        return multiSample_ > 1 ? msaaRenderTargetView_ : nativeSwapChain_->GetCurrentBackBufferRTV();
    }

    ITextureView* DILIGENT_CALL_TYPE GetDepthBufferDSV() override { return depthBufferView_; }
    /// @}

private:
    void CreateDepthStencil()
    {
        const SwapChainDesc& swapChainDesc = nativeSwapChain_->GetDesc();

        TextureDesc desc;
        desc.Name = "Main depth buffer";
        desc.Type = RESOURCE_DIM_TEX_2D;
        desc.Width = swapChainDesc.Width;
        desc.Height = swapChainDesc.Height;
        desc.Format = depthFormat_;
        desc.SampleCount = multiSample_;
        desc.Usage = USAGE_DEFAULT;
        desc.BindFlags = BIND_DEPTH_STENCIL;

        RefCntAutoPtr<ITexture> depthBuffer;
        renderDevice_->CreateTexture(desc, nullptr, &depthBuffer);

        depthBufferView_ = depthBuffer->GetDefaultView(TEXTURE_VIEW_DEPTH_STENCIL);
    }

    void CreateRenderTarget()
    {
        if (multiSample_ <= 1)
            return;

        const SwapChainDesc& swapChainDesc = nativeSwapChain_->GetDesc();

        TextureDesc desc;
        desc.Name = "Main depth buffer";
        desc.Type = RESOURCE_DIM_TEX_2D;
        desc.Width = swapChainDesc.Width;
        desc.Height = swapChainDesc.Height;
        desc.Format = swapChainDesc.ColorBufferFormat;
        desc.SampleCount = multiSample_;
        desc.Usage = USAGE_DEFAULT;
        desc.BindFlags = BIND_RENDER_TARGET;

        RefCntAutoPtr<ITexture> renderTarget;
        renderDevice_->CreateTexture(desc, nullptr, &renderTarget);

        msaaRenderTarget_ = renderTarget;
        msaaRenderTargetView_ = msaaRenderTarget_->GetDefaultView(TEXTURE_VIEW_RENDER_TARGET);
    }

    void UpdateDesc()
    {
        swapChainDesc_ = nativeSwapChain_->GetDesc();
        swapChainDesc_.DepthBufferFormat = depthFormat_;
    }

    const TEXTURE_FORMAT depthFormat_{};
    const Uint32 multiSample_{};

    const RefCntAutoPtr<ISwapChain> nativeSwapChain_;
    const RefCntAutoPtr<IRenderDevice> renderDevice_;
    const RefCntAutoPtr<IDeviceContext> immediateContext_;

    RefCntAutoPtr<ITextureView> depthBufferView_;
    RefCntAutoPtr<ITexture> msaaRenderTarget_;
    RefCntAutoPtr<ITextureView> msaaRenderTargetView_;

    SwapChainDesc swapChainDesc_;
};

ISwapChain* CreateProxySwapChainMS(IRenderDevice* device, IDeviceContext* deviceContext,
    ISwapChain* nativeSwapChain, Uint32 depthFormat, Uint32 multiSample)
{
    auto& defaultAllocator = DefaultRawMemoryAllocator::GetAllocator();
    auto proxySwapChain = NEW_RC_OBJ(defaultAllocator, "ProxySwapChainMS instance", ProxySwapChainMS)(
        device, deviceContext, nativeSwapChain, static_cast<TEXTURE_FORMAT>(depthFormat), multiSample);
    RefCntAutoPtr<ISwapChain> result{proxySwapChain};
    return result.Detach();
}

#if VULKAN_SUPPORTED
bool GetVulkanSurfaceExtent(IRenderDeviceVk* device, ISwapChainVk* swapChain, Uint32& width, Uint32& height)
{
#if PLATFORM_WIN32
    if (volkInitialize() != VK_SUCCESS)
        return false;
    volkLoadInstance(device->GetVkInstance());
#endif
    VkSurfaceCapabilitiesKHR surfCapabilities = {};
    const VkResult err = vkGetPhysicalDeviceSurfaceCapabilitiesKHR(
        device->GetVkPhysicalDevice(), swapChain->GetVkSurface(), &surfCapabilities);
    if (err != VK_SUCCESS || surfCapabilities.currentExtent.width == 0xFFFFFFFF)
        return false;

    width = surfCapabilities.currentExtent.width;
    height = surfCapabilities.currentExtent.height;
    return true;
}
#endif

} // namespace Diligent
