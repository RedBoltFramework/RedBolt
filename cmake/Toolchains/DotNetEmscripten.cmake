if(DEFINED ENV{DOTNET_ROOT} AND NOT "$ENV{DOTNET_ROOT}" STREQUAL "")
    set(_redbolt_dotnet_root "$ENV{DOTNET_ROOT}")
else()
    find_program(_redbolt_dotnet_executable dotnet REQUIRED)
    file(REAL_PATH "${_redbolt_dotnet_executable}" _redbolt_dotnet_executable)
    get_filename_component(_redbolt_dotnet_root "${_redbolt_dotnet_executable}" DIRECTORY)
endif()

set(_redbolt_dotnet_pack_roots "${_redbolt_dotnet_root}")
if(DEFINED ENV{DOTNET_CLI_HOME} AND NOT "$ENV{DOTNET_CLI_HOME}" STREQUAL "")
    list(APPEND _redbolt_dotnet_pack_roots "$ENV{DOTNET_CLI_HOME}/.dotnet")
endif()
if(DEFINED ENV{HOME} AND NOT "$ENV{HOME}" STREQUAL "")
    list(APPEND _redbolt_dotnet_pack_roots
        "$ENV{HOME}/.dotnet"
        "$ENV{HOME}/.local/share/dotnet"
        "$ENV{HOME}/.local")
endif()
list(REMOVE_DUPLICATES _redbolt_dotnet_pack_roots)

function(_redbolt_latest_emscripten_pack output pattern)
    set(_redbolt_pack_versions)
    foreach(_redbolt_pack_root IN LISTS _redbolt_dotnet_pack_roots)
        file(GLOB _redbolt_root_pack_versions LIST_DIRECTORIES TRUE
            "${_redbolt_pack_root}/packs/${pattern}/*")
        list(APPEND _redbolt_pack_versions ${_redbolt_root_pack_versions})
    endforeach()
    if(NOT _redbolt_pack_versions)
        message(FATAL_ERROR
            "The installed .NET SDK does not contain ${pattern}. Install the wasm-tools workload.")
    endif()
    list(SORT _redbolt_pack_versions COMPARE NATURAL ORDER DESCENDING)
    list(GET _redbolt_pack_versions 0 _redbolt_pack)
    set(${output} "${_redbolt_pack}" PARENT_SCOPE)
endfunction()

_redbolt_latest_emscripten_pack(_redbolt_emscripten_sdk "Microsoft.NET.Runtime.Emscripten.*.Sdk.*")
_redbolt_latest_emscripten_pack(_redbolt_emscripten_node "Microsoft.NET.Runtime.Emscripten.*.Node.*")
_redbolt_latest_emscripten_pack(_redbolt_emscripten_cache "Microsoft.NET.Runtime.Emscripten.*.Cache.*")

set(ENV{EMSDK_PATH} "${_redbolt_emscripten_sdk}/tools")
set(ENV{DOTNET_EMSCRIPTEN_LLVM_ROOT} "${_redbolt_emscripten_sdk}/tools/bin")
set(ENV{DOTNET_EMSCRIPTEN_NODE_JS} "${_redbolt_emscripten_node}/tools/bin/node")
set(ENV{DOTNET_EMSCRIPTEN_BINARYEN_ROOT} "${_redbolt_emscripten_sdk}/tools")
set(ENV{EM_CACHE} "${_redbolt_emscripten_cache}/tools/emscripten/cache")

include("${_redbolt_emscripten_sdk}/tools/emscripten/cmake/Modules/Platform/Emscripten.cmake")

set(_redbolt_emscripten_environment "${CMAKE_BINARY_DIR}/redbolt-dotnet-emscripten-env")
file(WRITE "${_redbolt_emscripten_environment}"
"#!/usr/bin/env sh
export EMSDK_PATH='${_redbolt_emscripten_sdk}/tools'
export DOTNET_EMSCRIPTEN_LLVM_ROOT='${_redbolt_emscripten_sdk}/tools/bin'
export DOTNET_EMSCRIPTEN_NODE_JS='${_redbolt_emscripten_node}/tools/bin/node'
export DOTNET_EMSCRIPTEN_BINARYEN_ROOT='${_redbolt_emscripten_sdk}/tools'
export EM_CACHE='${_redbolt_emscripten_cache}/tools/emscripten/cache'
exec \"$@\"
")
file(CHMOD "${_redbolt_emscripten_environment}"
    PERMISSIONS OWNER_READ OWNER_WRITE OWNER_EXECUTE GROUP_READ GROUP_EXECUTE WORLD_READ WORLD_EXECUTE)

set(CMAKE_C_COMPILER_LAUNCHER "${_redbolt_emscripten_environment}" CACHE FILEPATH "" FORCE)
set(CMAKE_CXX_COMPILER_LAUNCHER "${_redbolt_emscripten_environment}" CACHE FILEPATH "" FORCE)
set(CMAKE_C_LINKER_LAUNCHER "${_redbolt_emscripten_environment}" CACHE FILEPATH "" FORCE)
set(CMAKE_CXX_LINKER_LAUNCHER "${_redbolt_emscripten_environment}" CACHE FILEPATH "" FORCE)

foreach(_redbolt_tool AR RANLIB)
    set(_redbolt_real_tool "${CMAKE_${_redbolt_tool}}")
    string(TOLOWER "${_redbolt_tool}" _redbolt_tool_name)
    set(_redbolt_tool_wrapper "${CMAKE_BINARY_DIR}/redbolt-em${_redbolt_tool_name}")
    file(WRITE "${_redbolt_tool_wrapper}"
"#!/usr/bin/env sh
exec '${_redbolt_emscripten_environment}' '${_redbolt_real_tool}' \"$@\"
")
    file(CHMOD "${_redbolt_tool_wrapper}"
        PERMISSIONS OWNER_READ OWNER_WRITE OWNER_EXECUTE GROUP_READ GROUP_EXECUTE WORLD_READ WORLD_EXECUTE)
    set(CMAKE_${_redbolt_tool} "${_redbolt_tool_wrapper}" CACHE FILEPATH "" FORCE)
endforeach()
