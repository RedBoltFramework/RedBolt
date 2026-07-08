#pragma once

#include <cstdint>

#ifdef REDBOLT_NATIVE_BUILD
#define RB_IMGUI_EXPORT __attribute__((visibility("default")))
#else
#define RB_IMGUI_EXPORT
#endif

extern "C"
{

// ImGui context management (not available through generated bindings because ImGuiContext is forward-declared)
RB_IMGUI_EXPORT void* ImGuiBackend_CreateContext(void* sharedFontAtlas);
RB_IMGUI_EXPORT void ImGuiBackend_DestroyContext(void* ctx);
RB_IMGUI_EXPORT void* ImGuiBackend_GetCurrentContext();
RB_IMGUI_EXPORT void ImGuiBackend_SetCurrentContext(void* ctx);
RB_IMGUI_EXPORT void ImGuiBackend_SetDisplaySize(float width, float height);

// SDL2 backend
RB_IMGUI_EXPORT bool ImGuiBackend_InitForOther(void* sdlWindow);
RB_IMGUI_EXPORT bool ImGuiBackend_InitForOpenGL(void* sdlWindow, void* glContext);
RB_IMGUI_EXPORT bool ImGuiBackend_InitForD3D(void* sdlWindow);
RB_IMGUI_EXPORT void ImGuiBackend_Shutdown();
RB_IMGUI_EXPORT bool ImGuiBackend_ProcessEvent(void* sdlEvent);
RB_IMGUI_EXPORT void ImGuiBackend_NewFrame();

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

// ImGui font atlas helpers
RB_IMGUI_EXPORT bool ImGuiBackend_BuildFontAtlas();

}

