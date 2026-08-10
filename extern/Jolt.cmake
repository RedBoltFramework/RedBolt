set(OBJECT_LAYER_BITS 32 CACHE STRING "Use 16 collision layer and mask bits" FORCE)

set(JPH_BUILD_SHARED_LIBS OFF CACHE BOOL "Build Jolt statically" FORCE)
set(OVERRIDE_CXX_FLAGS OFF CACHE BOOL "Keep RedBolt compiler flags" FORCE)
set(INTERPROCEDURAL_OPTIMIZATION OFF CACHE BOOL "Disable Jolt LTO" FORCE)
set(ENABLE_ALL_WARNINGS OFF CACHE BOOL "Disable Jolt warnings as errors" FORCE)
set(ENABLE_INSTALL OFF CACHE BOOL "Disable Jolt installation" FORCE)
set(ENABLE_OBJECT_STREAM OFF CACHE BOOL "Disable Jolt object streams" FORCE)
set(CPP_RTTI_ENABLED ON CACHE BOOL "Enable RTTI for generated bindings" FORCE)
set(DEBUG_RENDERER_IN_DEBUG_AND_RELEASE OFF CACHE BOOL "Disable Jolt debug renderer" FORCE)
set(DEBUG_RENDERER_IN_DISTRIBUTION OFF CACHE BOOL "Disable Jolt debug renderer" FORCE)
set(PROFILER_IN_DEBUG_AND_RELEASE OFF CACHE BOOL "Disable Jolt profiler" FORCE)
set(PROFILER_IN_DISTRIBUTION OFF CACHE BOOL "Disable Jolt profiler" FORCE)
set(JPH_USE_DX12 OFF CACHE BOOL "Disable Jolt DirectX compute" FORCE)
set(JPH_USE_VK OFF CACHE BOOL "Disable Jolt Vulkan compute" FORCE)
set(JPH_USE_MTL OFF CACHE BOOL "Disable Jolt Metal compute" FORCE)
set(JPH_USE_CPU_COMPUTE OFF CACHE BOOL "Disable Jolt CPU compute" FORCE)
set(USE_SSE4_1 OFF CACHE BOOL "Build portable Jolt binaries" FORCE)
set(USE_SSE4_2 OFF CACHE BOOL "Build portable Jolt binaries" FORCE)
set(USE_AVX OFF CACHE BOOL "Build portable Jolt binaries" FORCE)
set(USE_AVX2 OFF CACHE BOOL "Build portable Jolt binaries" FORCE)
set(USE_AVX512 OFF CACHE BOOL "Build portable Jolt binaries" FORCE)
set(USE_LZCNT OFF CACHE BOOL "Build portable Jolt binaries" FORCE)
set(USE_TZCNT OFF CACHE BOOL "Build portable Jolt binaries" FORCE)
set(USE_F16C OFF CACHE BOOL "Build portable Jolt binaries" FORCE)
set(USE_FMADD OFF CACHE BOOL "Build portable Jolt binaries" FORCE)

CPMAddPackage(
    NAME JoltPhysics
    GIT_REPOSITORY https://github.com/jrouwe/JoltPhysics.git
    GIT_TAG 341ac92c7a8a63a4a37729ad6eca21f9a5a51e13
    CUSTOM_CACHE_KEY "."
    SOURCE_SUBDIR Build
    EXCLUDE_FROM_ALL YES)
set(REDBOLT_JOLT_SOURCE_ROOT "${JoltPhysics_SOURCE_DIR}" CACHE INTERNAL "Jolt Physics source root")
redbolt_require_path("${REDBOLT_JOLT_SOURCE_ROOT}/Build/CMakeLists.txt" "Jolt Physics build")
set_target_properties(Jolt PROPERTIES POSITION_INDEPENDENT_CODE ON)
set(REDBOLT_JOLT_INCLUDE_DIR "${REDBOLT_JOLT_SOURCE_ROOT}" CACHE INTERNAL "Jolt Physics include directory")
set(REDBOLT_JOLT_LIBRARY Jolt CACHE INTERNAL "RedBolt Jolt Physics target")
