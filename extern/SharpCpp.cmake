set(SHARPCPP_BUILD_E2E OFF CACHE BOOL "Build SharpCpp synthetic E2E fixtures" FORCE)
set(SHARPCPP_BUILD_RBFX OFF CACHE BOOL "Build SharpCpp rbfx bindings" FORCE)

CPMAddPackage(
    NAME SharpCpp
    GIT_REPOSITORY git@github.com:RedBoltFramework/SharpCpp.git
    GIT_TAG master
    CUSTOM_CACHE_KEY "."
    EXCLUDE_FROM_ALL YES)
set(REDBOLT_SHARPCPP_ROOT "${SharpCpp_SOURCE_DIR}" CACHE PATH "Path to the SharpCpp source tree" FORCE)

redbolt_require_path("${REDBOLT_SHARPCPP_ROOT}" "REDBOLT_SHARPCPP_ROOT")
redbolt_require_path("${REDBOLT_SHARPCPP_ROOT}/include/SharpCpp/Export.hpp" "SharpCpp native support headers")
