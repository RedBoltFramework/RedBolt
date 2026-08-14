#pragma once

#include <cstdint>

#ifdef REDBOLT_NATIVE_BUILD
#define RB_IMGUI_EXPORT __attribute__((visibility("default")))
#else
#define RB_IMGUI_EXPORT
#endif

extern "C"
{

RB_IMGUI_EXPORT bool ImGuiBackend_IsWithinFrameScope();
RB_IMGUI_EXPORT bool ImGuiBackend_HasCurrentWindow();
RB_IMGUI_EXPORT bool ImGuiBackend_InstallHarfBuzzGpuFontLoader();

// SDL2 backend
RB_IMGUI_EXPORT bool ImGuiBackend_ProcessEvent(void* sdlEvent);

// Opaque ImGui context and SDL backend shims. The public ImGui headers only
// forward-declare ImGuiContext and SDL_Window, so these small ABI-safe
// functions keep those implementation details out of generated bindings.
RB_IMGUI_EXPORT void* ImGuiBackend_CreateContext();
RB_IMGUI_EXPORT void ImGuiBackend_DestroyContext(void* context);
RB_IMGUI_EXPORT void ImGuiBackend_SetCurrentContext(void* context);
RB_IMGUI_EXPORT bool ImGuiBackend_Sdl2InitForOpenGL(void* window, void* context);
RB_IMGUI_EXPORT bool ImGuiBackend_Sdl2InitForOther(void* window);
RB_IMGUI_EXPORT bool ImGuiBackend_Sdl2InitForD3D(void* window);

// Diligent renderer
RB_IMGUI_EXPORT void* ImGuiBackend_CreateRenderer(
    void* renderDevice,
    uint32_t backBufferFmt,
    uint32_t depthBufferFmt,
    uint32_t initialVBSize,
    uint32_t initialIBSize);

RB_IMGUI_EXPORT void ImGuiBackend_DestroyRenderer(void* renderer);

RB_IMGUI_EXPORT void ImGuiBackend_Renderer_NewFrame(
    void* renderer,
    uint32_t width,
    uint32_t height,
    uint32_t surfacePreTransform);

RB_IMGUI_EXPORT void ImGuiBackend_Renderer_RenderDrawData(
    void* renderer,
    void* deviceContext,
    void* drawData,
    void* userPSO,
    void* userSRB,
    void* textureVar,
    void* constantsVar,
    void* harfBuzzAtlasVar,
    void* pixelConstantsVar);

RB_IMGUI_EXPORT void ImGuiBackend_Renderer_InvalidateDeviceObjects(void* renderer);
RB_IMGUI_EXPORT void ImGuiBackend_Renderer_CreateDeviceObjects(void* renderer);

}
