redbolt_require_path("${REDBOLT_EXTERN_ROOT}" "REDBOLT_EXTERN_ROOT")

if(NOT COMMAND install_third_party_libs)
    function(install_third_party_libs)
    endfunction()
endif()

if(NOT COMMAND group_sources)
    function(group_sources)
    endfunction()
endif()

include(SharpCpp.cmake)
include(SDL.cmake)
include(SDL_image.cmake)
include(Diligent.cmake)
include(DearImGui.cmake)
include(HarfBuzz.cmake)
include(RmlUi.cmake)
include(STB.cmake)
include(TinyGLTF.cmake)
include(LZ4.cmake)
include(WebP.cmake)
include(XAtlas.cmake)
include(Jolt.cmake)
include(Embree.cmake)
add_custom_target(redbolt-native-dependency-batch
    DEPENDS SDL2 redbolt-diligent-build ImGui harfbuzz harfbuzz-raster rmlui_core rmlui_debugger Jolt embree
    COMMENT "Building RedBolt native dependency batch")
