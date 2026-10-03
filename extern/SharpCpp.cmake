set(SHARPCPP_BUILD_E2E OFF CACHE BOOL "Build SharpCpp synthetic E2E fixtures" FORCE)
set(SHARPCPP_BUILD_RBFX OFF CACHE BOOL "Build SharpCpp rbfx bindings" FORCE)

# MSBuild passes the monorepo's SharpCpp checkout (RedBoltSharpCppRoot) when this repository is checked out inside the monorepo; otherwise use CPM.
if(REDBOLT_SHARPCPP_ROOT PATH_EQUAL "${REDBOLT_DEPENDENCY_ROOT}/SharpCpp")
    redbolt_cpm_add_package(
        NAME SharpCpp
        GIT_REPOSITORY git@github.com:RedBoltFramework/SharpCpp.git
        GIT_TAG master
        CUSTOM_CACHE_KEY "."
        EXCLUDE_FROM_ALL YES)
else()
    CPMAddPackage(
        NAME SharpCpp
        SOURCE_DIR "${REDBOLT_SHARPCPP_ROOT}"
        EXCLUDE_FROM_ALL YES)
endif()
set(REDBOLT_SHARPCPP_ROOT "${SharpCpp_SOURCE_DIR}" CACHE PATH "Path to the SharpCpp source tree" FORCE)

include("${SharpCpp_SOURCE_DIR}/cmake/SharpCppClang.cmake")

redbolt_require_path("${REDBOLT_SHARPCPP_ROOT}" "REDBOLT_SHARPCPP_ROOT")
redbolt_require_path("${REDBOLT_SHARPCPP_ROOT}/include/SharpCpp/Export.hpp" "SharpCpp native support headers")
