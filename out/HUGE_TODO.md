# Huge TODO inventory

- [ ] ./src/RedBolt/RenderAPI/RenderDevice.cs: Android GLES context invalidation/restoration is ported from C++ but still requires an Android platform build/runtime check; Vulkan context invalidation/restoration and its platform build remain.
- [ ] MultipleViewports rendering is washed out again; compare the complete C++ color-space, texture, material, lighting, render-path, and output call paths and port the concrete deviations.
- [ ] Verify engine default graphics-backend selection against C++ so samples do not need to select a backend manually; port any remaining deviation.
