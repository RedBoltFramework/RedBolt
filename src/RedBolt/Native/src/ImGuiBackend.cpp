#include "RedBolt/ImGuiBackend.hpp"

#include <imgui.h>
#include <imgui_internal.h>

#define IMGUI_IMPL_API IMGUI_API
#include <imgui_impl_sdl2.h>

#include "ImGuiDiligentRenderer.hpp"

#include <SDL.h>

using Diligent::ImGuiDiligentRenderer;

extern "C"
{

// ---- ImGui context management ----

void* ImGuiBackend_CreateContext(void* sharedFontAtlas)
{
    return ImGui::CreateContext(static_cast<ImFontAtlas*>(sharedFontAtlas));
}

void ImGuiBackend_DestroyContext(void* ctx)
{
    ImGui::DestroyContext(static_cast<ImGuiContext*>(ctx));
}

void* ImGuiBackend_GetCurrentContext()
{
    return ImGui::GetCurrentContext();
}

void ImGuiBackend_SetCurrentContext(void* ctx)
{
    ImGui::SetCurrentContext(static_cast<ImGuiContext*>(ctx));
}

void ImGuiBackend_SetDisplaySize(float width, float height)
{
    ImGui::GetIO().DisplaySize = ImVec2(width, height);
}

bool ImGuiBackend_IsWithinFrameScope()
{
    return ImGui::GetCurrentContext()->WithinFrameScope;
}

bool ImGuiBackend_HasCurrentWindow()
{
    return ImGui::GetCurrentWindowRead() != nullptr;
}

// ---- SDL2 backend ----

bool ImGuiBackend_InitForOther(void* sdlWindow)
{
    return ImGui_ImplSDL2_InitForOther(static_cast<SDL_Window*>(sdlWindow));
}

bool ImGuiBackend_InitForOpenGL(void* sdlWindow, void* glContext)
{
    return ImGui_ImplSDL2_InitForOpenGL(static_cast<SDL_Window*>(sdlWindow), glContext);
}

bool ImGuiBackend_InitForD3D(void* sdlWindow)
{
    return ImGui_ImplSDL2_InitForD3D(static_cast<SDL_Window*>(sdlWindow));
}

void ImGuiBackend_Shutdown()
{
    ImGui_ImplSDL2_Shutdown();
}

bool ImGuiBackend_ProcessEvent(void* sdlEvent)
{
    return ImGui_ImplSDL2_ProcessEvent(static_cast<SDL_Event*>(sdlEvent));
}

void ImGuiBackend_NewFrame()
{
    ImGui_ImplSDL2_NewFrame();
}

// ---- Diligent renderer ----

void* ImGuiBackend_CreateRenderer(
    void* renderDevice,
    uint32_t backBufferFmt,
    uint32_t depthBufferFmt,
    uint32_t initialVBSize,
    uint32_t initialIBSize)
{
    auto* device = static_cast<Diligent::IRenderDevice*>(renderDevice);
    return new ImGuiDiligentRenderer(
        device,
        static_cast<Diligent::TEXTURE_FORMAT>(backBufferFmt),
        static_cast<Diligent::TEXTURE_FORMAT>(depthBufferFmt),
        initialVBSize,
        initialIBSize);
}

void ImGuiBackend_DestroyRenderer(void* renderer)
{
    delete static_cast<ImGuiDiligentRenderer*>(renderer);
}

void ImGuiBackend_Renderer_NewFrame(
    void* renderer,
    uint32_t width,
    uint32_t height,
    uint32_t surfacePreTransform)
{
    static_cast<ImGuiDiligentRenderer*>(renderer)->NewFrame(
        width, height, static_cast<Diligent::SURFACE_TRANSFORM>(surfacePreTransform));
}

void ImGuiBackend_Renderer_RenderDrawData(
    void* renderer,
    void* deviceContext,
    void* drawData,
    void* userPSO,
    void* userSRB,
    void* textureVar,
    void* constantsVar)
{
    static_cast<ImGuiDiligentRenderer*>(renderer)->RenderDrawData(
        static_cast<Diligent::IDeviceContext*>(deviceContext),
        static_cast<ImDrawData*>(drawData),
        static_cast<Diligent::IPipelineState*>(userPSO),
        static_cast<Diligent::IShaderResourceBinding*>(userSRB),
        static_cast<Diligent::IShaderResourceVariable*>(textureVar),
        static_cast<Diligent::IShaderResourceVariable*>(constantsVar));
}

void ImGuiBackend_Renderer_InvalidateDeviceObjects(void* renderer)
{
    static_cast<ImGuiDiligentRenderer*>(renderer)->InvalidateDeviceObjects();
}

void ImGuiBackend_Renderer_CreateDeviceObjects(void* renderer)
{
    static_cast<ImGuiDiligentRenderer*>(renderer)->CreateDeviceObjects();
}

// ---- Font atlas help ----

bool ImGuiBackend_BuildFontAtlas()
{
    ImFontAtlas* atlas = ImGui::GetIO().Fonts;
    if (!atlas)
        return false;

    unsigned char* pixels;
    int width, height;
    atlas->GetTexDataAsRGBA32(&pixels, &width, &height);
    return true;
}

}
