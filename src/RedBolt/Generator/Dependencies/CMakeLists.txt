redbolt_require_path("${REDBOLT_SHARPCPP_ROOT}" "REDBOLT_SHARPCPP_ROOT")

if(NOT REDBOLT_DEPENDENCIES_GENERATOR_DLL)
    set(REDBOLT_DEPENDENCIES_GENERATOR_DLL
        "${PROJECT_SOURCE_DIR}/build/bin/$<CONFIG>/tools/RedBolt.Generator.Dependencies.dll"
        CACHE FILEPATH "RedBolt dependency generator assembly")
endif()

file(GLOB_RECURSE REDBOLT_SHARPCPP_GENERATOR_INPUTS CONFIGURE_DEPENDS
    "${REDBOLT_SHARPCPP_ROOT}/src/SharpCpp.Model/*.cs"
    "${REDBOLT_SHARPCPP_ROOT}/src/SharpCpp.Metadata/*.cs"
    "${REDBOLT_SHARPCPP_ROOT}/src/SharpCpp.Parsing/*.cs"
    "${REDBOLT_SHARPCPP_ROOT}/src/SharpCpp.Pipeline/*.cs"
    "${REDBOLT_SHARPCPP_ROOT}/src/SharpCpp.Rendering/*.cs"
    "${REDBOLT_SHARPCPP_ROOT}/src/SharpCpp.Support/*.cs"
    "${REDBOLT_SHARPCPP_ROOT}/src/SharpCpp.Model/*.csproj"
    "${REDBOLT_SHARPCPP_ROOT}/src/SharpCpp.Metadata/*.csproj"
    "${REDBOLT_SHARPCPP_ROOT}/src/SharpCpp.Parsing/*.csproj"
    "${REDBOLT_SHARPCPP_ROOT}/src/SharpCpp.Pipeline/*.csproj"
    "${REDBOLT_SHARPCPP_ROOT}/src/SharpCpp.Rendering/*.csproj"
    "${REDBOLT_SHARPCPP_ROOT}/src/SharpCpp.Support/*.csproj"
    "${REDBOLT_SHARPCPP_ROOT}/include/SharpCpp/*.h"
    "${REDBOLT_SHARPCPP_ROOT}/include/SharpCpp/*.hpp")
file(GLOB_RECURSE REDBOLT_DEPENDENCIES_GENERATOR_INPUTS CONFIGURE_DEPENDS
    "${CMAKE_CURRENT_SOURCE_DIR}/*.cs"
    "${CMAKE_CURRENT_SOURCE_DIR}/*.csproj")
list(APPEND REDBOLT_DEPENDENCIES_GENERATOR_INPUTS
    "${CMAKE_SOURCE_DIR}/src/RedBolt/Native/include/RedBolt/RmlHarfBuzzFontEngine.hpp"
    "${CMAKE_SOURCE_DIR}/src/RedBolt/Native/include/RedBolt/RmlUiValueMarshallers.hpp")

set(REDBOLT_SHARPCPP_GENERATOR_INPUTS "${REDBOLT_SHARPCPP_GENERATOR_INPUTS}" CACHE INTERNAL "SharpCpp generator inputs")
set(REDBOLT_DEPENDENCIES_GENERATOR_INPUTS "${REDBOLT_DEPENDENCIES_GENERATOR_INPUTS}" CACHE INTERNAL "RedBolt generator inputs")

set(REDBOLT_SDL_GENERATED_DIR "${REDBOLT_NATIVE_GENERATED_DIR}/sdl" CACHE INTERNAL "SDL binding output")
set(REDBOLT_DILIGENT_GENERATED_DIR "${REDBOLT_NATIVE_GENERATED_DIR}/diligent" CACHE INTERNAL "Diligent binding output")
set(REDBOLT_IMGUI_GENERATED_DIR "${REDBOLT_NATIVE_GENERATED_DIR}/dearimgui" CACHE INTERNAL "Dear ImGui binding output")
set(REDBOLT_STB_GENERATED_DIR "${REDBOLT_NATIVE_GENERATED_DIR}/stb" CACHE INTERNAL "STB binding output")
set(REDBOLT_LZ4_GENERATED_DIR "${REDBOLT_NATIVE_GENERATED_DIR}/lz4" CACHE INTERNAL "LZ4 binding output")
set(REDBOLT_HARFBUZZ_GENERATED_DIR "${REDBOLT_NATIVE_GENERATED_DIR}/harfbuzz" CACHE INTERNAL "HarfBuzz binding output")
set(REDBOLT_RMLUI_GENERATED_DIR "${REDBOLT_NATIVE_GENERATED_DIR}/rmlui" CACHE INTERNAL "RmlUi binding output")
set(REDBOLT_WEBP_GENERATED_DIR "${REDBOLT_NATIVE_GENERATED_DIR}/webp" CACHE INTERNAL "WebP binding output")
set(REDBOLT_XATLAS_GENERATED_DIR "${REDBOLT_NATIVE_GENERATED_DIR}/xatlas" CACHE INTERNAL "xatlas binding output")
set(REDBOLT_GLSLANG_GENERATED_DIR "${REDBOLT_NATIVE_GENERATED_DIR}/glslang" CACHE INTERNAL "glslang binding output")
set(REDBOLT_SPIRV_CROSS_GENERATED_DIR "${REDBOLT_NATIVE_GENERATED_DIR}/spirv-cross" CACHE INTERNAL "SPIRV-Cross binding output")
set(REDBOLT_SPIRV_TOOLS_GENERATED_DIR "${REDBOLT_NATIVE_GENERATED_DIR}/spirv-tools" CACHE INTERNAL "SPIRV-Tools binding output")
set(REDBOLT_SHADER_COMPILER_GENERATED_DIR "${REDBOLT_NATIVE_GENERATED_DIR}/shader-compiler" CACHE INTERNAL "Shader compiler binding output")
set(REDBOLT_JOLT_GENERATED_DIR "${REDBOLT_NATIVE_GENERATED_DIR}/jolt" CACHE INTERNAL "Jolt binding output")

set(REDBOLT_SDL_BINDINGS_STEM "RedBolt.Bindings.Sdl" CACHE INTERNAL "SDL binding stem")
set(REDBOLT_DILIGENT_BINDINGS_STEM "RedBolt.Bindings.Diligent" CACHE INTERNAL "Diligent binding stem")
set(REDBOLT_IMGUI_BINDINGS_STEM "RedBolt.Bindings.DearImGui" CACHE INTERNAL "Dear ImGui binding stem")
set(REDBOLT_STB_BINDINGS_STEM "RedBolt.Bindings.Stb" CACHE INTERNAL "STB binding stem")
set(REDBOLT_LZ4_BINDINGS_STEM "RedBolt.Bindings.Lz4" CACHE INTERNAL "LZ4 binding stem")
set(REDBOLT_HARFBUZZ_BINDINGS_STEM "RedBolt.Bindings.HarfBuzz" CACHE INTERNAL "HarfBuzz binding stem")
set(REDBOLT_RMLUI_BINDINGS_STEM "RedBolt.Bindings.RmlUi" CACHE INTERNAL "RmlUi binding stem")
set(REDBOLT_WEBP_BINDINGS_STEM "RedBolt.Bindings.WebP" CACHE INTERNAL "WebP binding stem")
set(REDBOLT_XATLAS_BINDINGS_STEM "RedBolt.Bindings.XAtlas" CACHE INTERNAL "xatlas binding stem")
set(REDBOLT_GLSLANG_BINDINGS_STEM "RedBolt.Bindings.Glslang" CACHE INTERNAL "glslang binding stem")
set(REDBOLT_SPIRV_CROSS_BINDINGS_STEM "RedBolt.Bindings.SpirVCross" CACHE INTERNAL "SPIRV-Cross binding stem")
set(REDBOLT_SPIRV_TOOLS_BINDINGS_STEM "RedBolt.Bindings.SpirVTools" CACHE INTERNAL "SPIRV-Tools binding stem")
set(REDBOLT_SHADER_COMPILER_BINDINGS_STEM "RedBolt.Bindings.ShaderCompiler" CACHE INTERNAL "Shader compiler binding stem")
set(REDBOLT_JOLT_BINDINGS_STEM "RedBolt.Bindings.Jolt" CACHE INTERNAL "Jolt binding stem")

foreach(binding SDL DILIGENT IMGUI STB LZ4 HARFBUZZ RMLUI WEBP XATLAS GLSLANG SPIRV_CROSS SPIRV_TOOLS SHADER_COMPILER JOLT)
    set(REDBOLT_${binding}_BINDINGS_CS
        "${REDBOLT_${binding}_GENERATED_DIR}/${REDBOLT_${binding}_BINDINGS_STEM}.Bindings.g.cs"
        CACHE INTERNAL "${binding} generated C# binding")
    set(REDBOLT_${binding}_BINDINGS_CPP
        "${REDBOLT_${binding}_GENERATED_DIR}/${REDBOLT_${binding}_BINDINGS_STEM}.Bindings.g.cpp"
        CACHE INTERNAL "${binding} generated C++ binding")
    set(REDBOLT_${binding}_BINDINGS_JSON
        "${REDBOLT_${binding}_GENERATED_DIR}/${REDBOLT_${binding}_BINDINGS_STEM}.json"
        CACHE INTERNAL "${binding} binding metadata")
endforeach()

set(REDBOLT_SDL_BUILD_INCLUDE_DIR "${CMAKE_BINARY_DIR}/deps/SDL/include" CACHE INTERNAL "SDL generated include directory")
set(REDBOLT_SDL_CONFIG_INCLUDE_DIR "${CMAKE_BINARY_DIR}/deps/SDL/include-config-$<LOWER_CASE:$<CONFIG>>" CACHE INTERNAL "SDL configuration include directory")
set(REDBOLT_SDL_PARSE_SOURCE "${REDBOLT_SDL_GENERATED_DIR}/SDLAll.cpp" CACHE INTERNAL "SDL parse source")
set(REDBOLT_DILIGENT_PARSE_HEADER "${REDBOLT_DILIGENT_GENERATED_DIR}/DiligentAll.hpp" CACHE INTERNAL "Diligent parse header")
set(REDBOLT_IMGUI_PARSE_SOURCE "${REDBOLT_IMGUI_GENERATED_DIR}/ImGuiAll.cpp" CACHE INTERNAL "Dear ImGui parse source")
set(REDBOLT_STB_PARSE_HEADER "${REDBOLT_STB_GENERATED_DIR}/StbVorbisAll.hpp" CACHE INTERNAL "STB parse header")
set(REDBOLT_LZ4_PARSE_HEADER "${REDBOLT_LZ4_GENERATED_DIR}/Lz4All.hpp" CACHE INTERNAL "LZ4 parse header")
set(REDBOLT_HARFBUZZ_PARSE_HEADER "${REDBOLT_HARFBUZZ_GENERATED_DIR}/HarfBuzzAll.h" CACHE INTERNAL "HarfBuzz parse header")
set(REDBOLT_RMLUI_PARSE_HEADER "${REDBOLT_RMLUI_GENERATED_DIR}/RmlUiAll.hpp" CACHE INTERNAL "RmlUi parse header")
set(REDBOLT_WEBP_PARSE_HEADER "${REDBOLT_WEBP_GENERATED_DIR}/WebPAll.hpp" CACHE INTERNAL "WebP parse header")
set(REDBOLT_XATLAS_PARSE_HEADER "${REDBOLT_XATLAS_GENERATED_DIR}/XAtlasAll.hpp" CACHE INTERNAL "xatlas parse header")
set(REDBOLT_SHADER_COMPILER_PARSE_HEADER "${REDBOLT_SHADER_COMPILER_GENERATED_DIR}/ShaderCompilerAll.hpp" CACHE INTERNAL "Shader compiler parse header")
set(REDBOLT_JOLT_PARSE_HEADER "${REDBOLT_JOLT_GENERATED_DIR}/JoltAll.hpp" CACHE INTERNAL "Jolt parse header")
set(REDBOLT_JOLT_SUPPORT_INCLUDE_DIR "${CMAKE_SOURCE_DIR}/src/RedBolt/Native/include" CACHE INTERNAL "RedBolt Jolt support include directory")
set(REDBOLT_JOLT_SUPPORT_HEADER "${REDBOLT_JOLT_SUPPORT_INCLUDE_DIR}/RedBolt/JoltMarshallers.hpp" CACHE INTERNAL "RedBolt Jolt marshaller header")

set(REDBOLT_DILIGENT_SUPPORT_INCLUDE_DIR "${CMAKE_SOURCE_DIR}/src/RedBolt/Native/include" CACHE INTERNAL "RedBolt native support include directory" FORCE)
set(REDBOLT_DILIGENT_SUPPORT_HEADER "${REDBOLT_DILIGENT_SUPPORT_INCLUDE_DIR}/RedBolt/DiligentSupport.hpp" CACHE INTERNAL "RedBolt Diligent support header")
set(REDBOLT_IMGUI_INCLUDE_DIR "${REDBOLT_IMGUI_SOURCE_ROOT}" CACHE INTERNAL "Dear ImGui include directory")
set(REDBOLT_IMGUI_BACKEND_HEADER "${REDBOLT_DILIGENT_SUPPORT_INCLUDE_DIR}/RedBolt/ImGuiBackend.hpp" CACHE INTERNAL "Dear ImGui backend header")

set(REDBOLT_GLSLANG_SOURCE_ROOT "${REDBOLT_DILIGENT_SOURCE_ROOT}/ThirdParty/glslang" CACHE INTERNAL "glslang source root")
set(REDBOLT_SPIRV_CROSS_SOURCE_ROOT "${REDBOLT_DILIGENT_SOURCE_ROOT}/ThirdParty/SPIRV-Cross" CACHE INTERNAL "SPIRV-Cross source root")
set(REDBOLT_SPIRV_TOOLS_SOURCE_ROOT "${REDBOLT_DILIGENT_SOURCE_ROOT}/ThirdParty/SPIRV-Tools" CACHE INTERNAL "SPIRV-Tools source root")
set(REDBOLT_GLSLANG_C_HEADER "${REDBOLT_GLSLANG_SOURCE_ROOT}/glslang/Public/resource_limits_c.h" CACHE INTERNAL "glslang C header")
set(REDBOLT_SPIRV_CROSS_C_HEADER "${REDBOLT_SPIRV_CROSS_SOURCE_ROOT}/spirv_cross_c.h" CACHE INTERNAL "SPIRV-Cross C header")
set(REDBOLT_SPIRV_TOOLS_C_HEADER "${REDBOLT_SPIRV_TOOLS_SOURCE_ROOT}/include/spirv-tools/libspirv.h" CACHE INTERNAL "SPIRV-Tools C header")

redbolt_require_path("${REDBOLT_STB_VORBIS_HEADER}" "stb_vorbis header")
redbolt_require_path("${REDBOLT_STB_IMAGE_HEADER}" "stb_image header")
redbolt_require_path("${REDBOLT_STB_IMAGE_WRITE_HEADER}" "stb_image_write header")

file(MAKE_DIRECTORY
    "${REDBOLT_SDL_GENERATED_DIR}"
    "${REDBOLT_DILIGENT_GENERATED_DIR}"
    "${REDBOLT_IMGUI_GENERATED_DIR}"
    "${REDBOLT_STB_GENERATED_DIR}"
    "${REDBOLT_LZ4_GENERATED_DIR}"
    "${REDBOLT_HARFBUZZ_GENERATED_DIR}"
    "${REDBOLT_RMLUI_GENERATED_DIR}"
    "${REDBOLT_WEBP_GENERATED_DIR}"
    "${REDBOLT_XATLAS_GENERATED_DIR}"
    "${REDBOLT_GLSLANG_GENERATED_DIR}"
    "${REDBOLT_SPIRV_CROSS_GENERATED_DIR}"
    "${REDBOLT_SPIRV_TOOLS_GENERATED_DIR}"
    "${REDBOLT_SHADER_COMPILER_GENERATED_DIR}"
    "${REDBOLT_JOLT_GENERATED_DIR}")

file(CONFIGURE OUTPUT "${REDBOLT_STB_PARSE_HEADER}" CONTENT [=[
#pragma once
#define STB_VORBIS_HEADER_ONLY
#include <stb_vorbis.c>
#include <stb_image.h>
#include <stb_image_write.h>
STBIWDEF unsigned char* stbi_write_png_to_mem(
    const unsigned char* pixels, int stride_bytes, int x, int y, int n, int* out_len);
struct stb_vorbis { int opaque; };
]=] @ONLY)
file(CONFIGURE OUTPUT "${REDBOLT_LZ4_PARSE_HEADER}" CONTENT [=[
#pragma once
#include <lz4.h>
#include <lz4hc.h>
]=] @ONLY)
file(CONFIGURE OUTPUT "${REDBOLT_HARFBUZZ_PARSE_HEADER}" CONTENT [=[
#pragma once
#include <hb.h>
#include <hb-ot.h>
#include <hb-raster.h>
]=] @ONLY)
file(CONFIGURE OUTPUT "${REDBOLT_RMLUI_PARSE_HEADER}" CONTENT [=[
#pragma once
#include <RmlUi/Debugger.h>
#include <RmlUi/Core/Core.h>
#include <RmlUi/Core/Context.h>
#include <RmlUi/Core/ContextInstancer.h>
#include <RmlUi/Core/ComputedValues.h>
#include <RmlUi/Core/DataModelHandle.h>
#include <RmlUi/Core/Element.h>
#include <RmlUi/Core/ElementDocument.h>
#include <RmlUi/Core/Event.h>
#include <RmlUi/Core/EventListener.h>
#include <RmlUi/Core/EventListenerInstancer.h>
#include <RmlUi/Core/Factory.h>
#include <RmlUi/Core/FileInterface.h>
#include <RmlUi/Core/FontEngineInterface.h>
#include <RmlUi/Core/Plugin.h>
#include <RmlUi/Core/RenderInterfaceCompatibility.h>
#include <RmlUi/Core/SystemInterface.h>
#include <RmlUi/Core/Variant.h>
#include <RedBolt/RmlHarfBuzzFontEngine.hpp>
]=] @ONLY)
file(CONFIGURE OUTPUT "${REDBOLT_WEBP_PARSE_HEADER}" CONTENT [=[
#pragma once
#include <webp/decode.h>
#include <webp/encode.h>
]=] @ONLY)
file(CONFIGURE OUTPUT "${REDBOLT_XATLAS_PARSE_HEADER}" CONTENT [=[
#pragma once
#include <cstddef>
#include <xatlas.h>
]=] @ONLY)
file(CONFIGURE OUTPUT "${REDBOLT_SHADER_COMPILER_PARSE_HEADER}" CONTENT [=[
#pragma once
#include <RedBolt/ShaderCompilerSupport.hpp>
]=] @ONLY)
file(CONFIGURE OUTPUT "${REDBOLT_JOLT_PARSE_HEADER}" CONTENT [=[
#pragma once
#include <RedBolt/JoltMarshallers.hpp>
#include <Jolt/RegisterTypes.h>
#include <Jolt/Core/Factory.h>
#include <Jolt/Core/JobSystemThreadPool.h>
#include <Jolt/Core/TempAllocator.h>
#include <Jolt/Physics/Body/Body.h>
#include <Jolt/Physics/Body/BodyCreationSettings.h>
#include <Jolt/Physics/Body/MassProperties.h>
#include <Jolt/Physics/Collision/BroadPhase/BroadPhaseLayerInterfaceMask.h>
#include <Jolt/Physics/Collision/BroadPhase/BroadPhaseLayerInterfaceTable.h>
#include <Jolt/Physics/Collision/BroadPhase/ObjectVsBroadPhaseLayerFilterMask.h>
#include <Jolt/Physics/Collision/BroadPhase/ObjectVsBroadPhaseLayerFilterTable.h>
#include <Jolt/Physics/Collision/ContactListener.h>
#include <Jolt/Physics/Collision/NarrowPhaseQuery.h>
#include <Jolt/Physics/Collision/ObjectLayerPairFilterMask.h>
#include <Jolt/Physics/Collision/ObjectLayerPairFilterTable.h>
#include <Jolt/Physics/Collision/Shape/BoxShape.h>
#include <Jolt/Physics/Collision/Shape/CapsuleShape.h>
#include <Jolt/Physics/Collision/Shape/CylinderShape.h>
#include <Jolt/Physics/Collision/Shape/RotatedTranslatedShape.h>
#include <Jolt/Physics/Collision/Shape/ScaledShape.h>
#include <Jolt/Physics/Collision/Shape/SphereShape.h>
#include <Jolt/Physics/Collision/TransformedShape.h>
#include <Jolt/Physics/SoftBody/SoftBodyCreationSettings.h>
#include <Jolt/Physics/PhysicsSystem.h>
]=] @ONLY)
file(CONFIGURE OUTPUT "${REDBOLT_SDL_PARSE_SOURCE}" CONTENT [=[
#include <SDL_config.h>
#include <SDL.h>
#include <SDL_image.h>

#ifndef SDL_VIDEO_DISABLED
#include <SDL_syswm.h>
#endif

#if defined(SDL_VIDEO_VULKAN)
#include <SDL_vulkan.h>
#endif

#if defined(REDBOLT_GENERATE_SDL_TEST_BINDINGS)
#include <SDL_test.h>
#endif
]=] @ONLY)
file(CONFIGURE OUTPUT "${REDBOLT_DILIGENT_PARSE_HEADER}" CONTENT [=[
#pragma once
#include <cstdint>
#if !PLATFORM_WEB
#undef UINTPTR_MAX
#define UINTPTR_MAX UINT64_MAX
#endif
#include <EngineFactoryOpenGL.h>
#include <GraphicsAccessories.hpp>
#include <RedBolt/DiligentSupport.hpp>
]=] @ONLY)
file(CONFIGURE OUTPUT "${REDBOLT_IMGUI_PARSE_SOURCE}" CONTENT [=[
#include <SDL.h>
#include <imgui.h>
#define IMGUI_IMPL_API IMGUI_API
#include <imgui_impl_sdl2.h>
#include <RedBolt/ImGuiBackend.hpp>
]=] @ONLY)

set(REDBOLT_SDL_OWNED_HEADERS "${REDBOLT_SDL_BUILD_INCLUDE_DIR};${REDBOLT_SDL_IMAGE_HEADER}" CACHE INTERNAL "SDL owned headers")
set(REDBOLT_DILIGENT_OWNED_HEADERS "${REDBOLT_DILIGENT_INCLUDE_DIRS};${REDBOLT_DILIGENT_SUPPORT_INCLUDE_DIR};${REDBOLT_DILIGENT_SUPPORT_HEADER}" CACHE INTERNAL "Diligent owned headers" FORCE)
set(REDBOLT_IMGUI_OWNED_HEADERS "${REDBOLT_IMGUI_INCLUDE_DIR};${REDBOLT_IMGUI_SOURCE_ROOT}/backends;${REDBOLT_IMGUI_BACKEND_HEADER}" CACHE INTERNAL "Dear ImGui owned headers" FORCE)
set(REDBOLT_BINDING_INCLUDE_DIRS
    "${REDBOLT_SHARPCPP_ROOT}/include"
    ${REDBOLT_DILIGENT_INCLUDE_DIRS}
    CACHE INTERNAL "Include directories used by RedBolt SharpCpp dependency generation")
set(_redbolt_binding_defines ${REDBOLT_DILIGENT_BINDING_DEFINES})
if(CMAKE_CONFIGURATION_TYPES OR CMAKE_BUILD_TYPE MATCHES "^(Release|Distribution|ReleaseASAN|ReleaseUBSAN|ReleaseTSAN|ReleaseCoverage)$")
    list(APPEND _redbolt_binding_defines NDEBUG)
endif()
set(REDBOLT_BINDING_DEFINES
    ${_redbolt_binding_defines}
    CACHE INTERNAL "Compile definitions used by RedBolt SharpCpp dependency generation" FORCE)
