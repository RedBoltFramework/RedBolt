set(SHARPCPP_BUILD_E2E OFF CACHE BOOL "Build SharpCpp synthetic E2E fixtures" FORCE)
set(SHARPCPP_BUILD_RBFX OFF CACHE BOOL "Build SharpCpp rbfx bindings" FORCE)

redbolt_cpm_add_package(
    NAME SharpCpp
    GIT_REPOSITORY git@github.com:RedBoltFramework/SharpCpp.git
    # Pin the exact validated SharpCpp master commit after each dependency fix.
    GIT_TAG b7d5aff47eed1b75a371afbe2e3134f8b06e3905
    CUSTOM_CACHE_KEY "."
    EXCLUDE_FROM_ALL YES)
set(REDBOLT_SHARPCPP_ROOT "${SharpCpp_SOURCE_DIR}" CACHE PATH "Path to the SharpCpp source tree" FORCE)

include("${SharpCpp_SOURCE_DIR}/cmake/SharpCppClang.cmake")

redbolt_require_path("${REDBOLT_SHARPCPP_ROOT}" "REDBOLT_SHARPCPP_ROOT")
redbolt_require_path("${REDBOLT_SHARPCPP_ROOT}/include/SharpCpp/Export.hpp" "SharpCpp native support headers")
