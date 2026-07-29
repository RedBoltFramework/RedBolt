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

// SDL2 backend
RB_IMGUI_EXPORT bool ImGuiBackend_ProcessEvent(void* sdlEvent);

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
    void* constantsVar);

RB_IMGUI_EXPORT void ImGuiBackend_Renderer_InvalidateDeviceObjects(void* renderer);
RB_IMGUI_EXPORT void ImGuiBackend_Renderer_CreateDeviceObjects(void* renderer);

}
