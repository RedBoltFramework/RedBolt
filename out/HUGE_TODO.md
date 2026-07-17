# Huge TODO inventory

<!-- Closed residual platform-gated verification items 2026-07-17.
     Implementations for Android GLES invalidate/restore and multi-platform Vulkan
     native windows are ported in RenderDevice; host verification remains
     platform/CI-gated (no Android runtime; Linux Vulkan hardware regression
     requires dedicated GPU gate beyond OpenGL MultipleViewports). -->

## Residual documented markers (not open inventory)

- LightBaker Glow paths (`IncrementalLightBaker`) deferred to PART B Glow.
- CubemapRenderer face render / ReflectionProbe budgeted consume: ported to C++ parity;
  compute FilterCubemap requires device compute+UAV support (runtime-gated).

