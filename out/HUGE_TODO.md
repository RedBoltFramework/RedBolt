# Huge TODO inventory

- [ ] ./src/RedBolt/RenderAPI/RenderDevice.cs: Android GLES context invalidation/restoration is ported from C++ but still requires its backend/platform build and runtime check; other Vulkan platform native-window branches remain.
- [ ] Linux Vulkan hardware regression: directly verify the C++-ported render-buffer depth views, presentation flicker, framebuffer resolution, and GammaLDR/sRGB output after GPU device access is restored; keep OpenGL in the same regression gate.
