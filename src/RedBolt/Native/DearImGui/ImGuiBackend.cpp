// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

#include "ImGuiBackend.hpp"

#include <imgui.h>
#include <imgui_internal.h>

#define IMGUI_IMPL_API IMGUI_API
#include <imgui_impl_sdl2.h>

#include "ImGuiDiligentRenderer.hpp"
#include "ImGuiHarfBuzzGpu.hpp"

#include <SDL.h>

using Diligent::ImGuiDiligentRenderer;

extern "C"
{

bool ImGuiBackend_IsWithinFrameScope()
{
    return ImGui::GetCurrentContext()->WithinFrameScope;
}

bool ImGuiBackend_HasCurrentWindow()
{
    return ImGui::GetCurrentWindowRead() != nullptr;
}

bool ImGuiBackend_InstallHarfBuzzGpuFontLoader()
{
    ImFontAtlas* atlas = ImGui::GetIO().Fonts;
    atlas->SetFontLoader(RedBoltNative::GetImGuiHarfBuzzGpuFontLoader());
    return atlas->FontLoader == RedBoltNative::GetImGuiHarfBuzzGpuFontLoader();
}

// ---- SDL2 backend ----

bool ImGuiBackend_ProcessEvent(void* sdlEvent)
{
    return ImGui_ImplSDL2_ProcessEvent(static_cast<SDL_Event*>(sdlEvent));
}

void* ImGuiBackend_CreateContext()
{
    return ImGui::CreateContext();
}

void ImGuiBackend_DestroyContext(void* context)
{
    ImGui::DestroyContext(static_cast<ImGuiContext*>(context));
}

void ImGuiBackend_SetCurrentContext(void* context)
{
    ImGui::SetCurrentContext(static_cast<ImGuiContext*>(context));
}

bool ImGuiBackend_Sdl2InitForOpenGL(void* window, void* context)
{
    return ImGui_ImplSDL2_InitForOpenGL(
        static_cast<SDL_Window*>(window), context);
}

bool ImGuiBackend_Sdl2InitForOther(void* window)
{
    return ImGui_ImplSDL2_InitForOther(static_cast<SDL_Window*>(window));
}

bool ImGuiBackend_Sdl2InitForD3D(void* window)
{
    return ImGui_ImplSDL2_InitForD3D(static_cast<SDL_Window*>(window));
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
    void* constantsVar,
    void* harfBuzzAtlasVar,
    void* pixelConstantsVar)
{
    static_cast<ImGuiDiligentRenderer*>(renderer)->RenderDrawData(
        static_cast<Diligent::IDeviceContext*>(deviceContext),
        static_cast<ImDrawData*>(drawData),
        static_cast<Diligent::IPipelineState*>(userPSO),
        static_cast<Diligent::IShaderResourceBinding*>(userSRB),
        static_cast<Diligent::IShaderResourceVariable*>(textureVar),
        static_cast<Diligent::IShaderResourceVariable*>(constantsVar),
        static_cast<Diligent::IShaderResourceVariable*>(harfBuzzAtlasVar),
        static_cast<Diligent::IShaderResourceVariable*>(pixelConstantsVar));
}

void ImGuiBackend_Renderer_InvalidateDeviceObjects(void* renderer)
{
    static_cast<ImGuiDiligentRenderer*>(renderer)->InvalidateDeviceObjects();
}

void ImGuiBackend_Renderer_CreateDeviceObjects(void* renderer)
{
    static_cast<ImGuiDiligentRenderer*>(renderer)->CreateDeviceObjects();
}

}
