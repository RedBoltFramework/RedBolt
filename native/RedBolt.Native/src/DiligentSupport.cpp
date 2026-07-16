#include <RedBolt/DiligentSupport.hpp>

#include <RenderDeviceGL.h>
#include <SwapChainGL.h>
#include <SwapChainBase.hpp>
#include <DefaultRawMemoryAllocator.hpp>
#include <ObjectBase.hpp>
#include <SDL.h>

#include <stdexcept>
#include <string>
#include <vector>

namespace Diligent
{

const ShaderCodeVariableDesc* GetShaderCodeBufferVariableAt(const ShaderCodeBufferDesc& self, Uint32 index)
{
    if (self.pVariables == nullptr || index >= self.NumVariables)
        throw std::out_of_range("Diligent shader reflection variable access out of range.");

    return &self.pVariables[index];
}

class TemporaryGLProgram
{
public:
    Uint32 programObject_{};
    std::vector<std::string> vertexAttributeNames_;
    std::vector<Uint32> vertexAttributeLocations_;
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
        if (programObject_)
            glDeleteProgram(programObject_);
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

#if PLATFORM_IOS || PLATFORM_TVOS
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

ISwapChain* CreateProxySwapChainGL(IRenderDevice* device, IDeviceContext* deviceContext,
    const SwapChainDesc& swapChainDesc, void* window)
{
    auto& defaultAllocator = DefaultRawMemoryAllocator::GetAllocator();
    auto swapChain = NEW_RC_OBJ(defaultAllocator, "ProxySwapChainGL instance", ProxySwapChainGL)(
        device, deviceContext, swapChainDesc, static_cast<SDL_Window*>(window));
    RefCntAutoPtr<ISwapChain> result{swapChain};
    return result.Detach();
}

ISwapChain* CreateSecondaryProxySwapChainGL(IRenderDevice* device, IDeviceContext* deviceContext,
    const SwapChainDesc& swapChainDesc, void* window)
{
    auto& defaultAllocator = DefaultRawMemoryAllocator::GetAllocator();
    auto secondarySwapChain = NEW_RC_OBJ(defaultAllocator, "Secondary ProxySwapChainGL instance", ProxySwapChainGL)(
        device, deviceContext, swapChainDesc, static_cast<SDL_Window*>(window));
    RefCntAutoPtr<ISwapChain> result{secondarySwapChain};
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

} // namespace Diligent
