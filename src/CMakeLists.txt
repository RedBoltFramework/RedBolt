add_subdirectory(RedBolt/Generator/Dependencies)

add_subdirectory(RedBolt/Native/Sdl)
add_subdirectory(RedBolt/Native/Diligent)
add_subdirectory(RedBolt/Native/DearImGui)
add_subdirectory(RedBolt/Native/Stb)
add_subdirectory(RedBolt/Native/TinyGltf)
add_subdirectory(RedBolt/Native/Lz4)
add_subdirectory(RedBolt/Native/HarfBuzz)
add_subdirectory(RedBolt/Native/RmlUi)
add_subdirectory(RedBolt/Native/WebP)
add_subdirectory(RedBolt/Native/XAtlas)
add_subdirectory(RedBolt/Native/Embree)
add_subdirectory(RedBolt/Native/Glslang)
add_subdirectory(RedBolt/Native/SpirVCross)
add_subdirectory(RedBolt/Native/SpirVTools)
add_subdirectory(RedBolt/Native/ShaderCompiler)
add_subdirectory(RedBolt/Native/LibDataChannel)

add_custom_target(redbolt-generate-native-bindings
    DEPENDS
        redbolt-generate-sdl-bindings
        redbolt-generate-diligent-bindings
        redbolt-generate-dearimgui-bindings
        redbolt-generate-stb-bindings
        redbolt-generate-tinygltf-bindings
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
add_dependencies(redbolt-generate-native-bindings
    redbolt-generate-libdatachannel-bindings)

add_subdirectory(RedBolt/Native)

if(EMSCRIPTEN)
    add_subdirectory(RedBolt/Sample/Web)
endif()
