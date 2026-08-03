add_subdirectory(RedBolt/Generator/Dependencies)

add_subdirectory(RedBolt/Bindings/Sdl)
add_subdirectory(RedBolt/Bindings/Diligent)
add_subdirectory(RedBolt/Bindings/DearImGui)
add_subdirectory(RedBolt/Bindings/Stb)
add_subdirectory(RedBolt/Bindings/Lz4)
add_subdirectory(RedBolt/Bindings/HarfBuzz)
add_subdirectory(RedBolt/Bindings/RmlUi)
add_subdirectory(RedBolt/Bindings/WebP)
add_subdirectory(RedBolt/Bindings/XAtlas)
add_subdirectory(RedBolt/Bindings/Embree)
add_subdirectory(RedBolt/Bindings/Glslang)
add_subdirectory(RedBolt/Bindings/SpirVCross)
add_subdirectory(RedBolt/Bindings/SpirVTools)
add_subdirectory(RedBolt/Bindings/ShaderCompiler)
add_subdirectory(RedBolt/Bindings/Jolt)

add_custom_target(redbolt-generate-native-bindings
    DEPENDS
        redbolt-generate-sdl-bindings
        redbolt-generate-diligent-bindings
        redbolt-generate-dearimgui-bindings
        redbolt-generate-stb-bindings
        redbolt-generate-lz4-bindings
        redbolt-generate-harfbuzz-bindings
        redbolt-generate-rmlui-bindings
        redbolt-generate-webp-bindings
        redbolt-generate-xatlas-bindings
        redbolt-generate-embree-bindings
        redbolt-generate-glslang-bindings
        redbolt-generate-spirv-cross-bindings
        redbolt-generate-spirv-tools-bindings
        redbolt-generate-shader-compiler-bindings
        redbolt-generate-jolt-bindings)

add_subdirectory(RedBolt/Native)

if(EMSCRIPTEN)
    add_subdirectory(RedBolt/Sample/Web)
endif()
