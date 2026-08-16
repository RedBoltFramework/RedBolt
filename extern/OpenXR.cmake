set(BUILD_API_LAYERS OFF CACHE BOOL "Build OpenXR API layers" FORCE)
set(BUILD_CONFORMANCE_TESTS OFF CACHE BOOL "Build OpenXR conformance tests" FORCE)
set(BUILD_TESTS OFF CACHE BOOL "Build OpenXR tests" FORCE)
set(BUILD_ALL_EXTENSIONS OFF CACHE BOOL "Build all OpenXR extensions" FORCE)

# The SDK's legacy sample graphics wrapper is created even when all samples and
# tests are disabled. Suppress graphics package discovery only while adding the
# SDK; Diligent performs its own discovery immediately afterwards.
set(CMAKE_DISABLE_FIND_PACKAGE_OpenGL TRUE)
set(CMAKE_DISABLE_FIND_PACKAGE_OpenGLES TRUE)
set(CMAKE_DISABLE_FIND_PACKAGE_EGL TRUE)
CPMAddPackage(
    NAME OpenXR
    GITHUB_REPOSITORY KhronosGroup/OpenXR-SDK-Source
    GIT_TAG fb6b2b45d32e3169af77351248a1790f030773be
    EXCLUDE_FROM_ALL YES)
set(REDBOLT_OPENXR_INCLUDE_DIR "${CPM_PACKAGE_OpenXR_BINARY_DIR}/include")
set(REDBOLT_OPENXR_INCLUDE_DIRS
    "${CPM_PACKAGE_OpenXR_BINARY_DIR}/include"
    "${CPM_PACKAGE_OpenXR_SOURCE_DIR}/include")
unset(CMAKE_DISABLE_FIND_PACKAGE_OpenGL)
unset(CMAKE_DISABLE_FIND_PACKAGE_OpenGLES)
unset(CMAKE_DISABLE_FIND_PACKAGE_EGL)
