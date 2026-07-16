include_guard(GLOBAL)

file(GLOB_RECURSE REDBOLT_SHARPCPP_GENERATOR_INPUTS CONFIGURE_DEPENDS
    "${REDBOLT_SHARPCPP_ROOT}/src/SharpCpp.Model/*.cs"
    "${REDBOLT_SHARPCPP_ROOT}/src/SharpCpp.Metadata/*.cs"
    "${REDBOLT_SHARPCPP_ROOT}/src/SharpCpp.Parsing/*.cs"
    "${REDBOLT_SHARPCPP_ROOT}/src/SharpCpp.Pipeline/*.cs"
    "${REDBOLT_SHARPCPP_ROOT}/src/SharpCpp.Rendering/*.cs"
    "${REDBOLT_SHARPCPP_ROOT}/src/SharpCpp.Support/*.cs"
    "${REDBOLT_SHARPCPP_ROOT}/src/SharpCpp.Tool/*.cs"
    "${REDBOLT_SHARPCPP_ROOT}/include/SharpCpp/*.h"
    "${REDBOLT_SHARPCPP_ROOT}/include/SharpCpp/*.hpp")

file(GLOB REDBOLT_DEPENDENCIES_GENERATOR_INPUTS CONFIGURE_DEPENDS
    "${CMAKE_CURRENT_SOURCE_DIR}/generators/RedBolt.Dependencies.Generator/*.cs")

set(REDBOLT_DEPENDENCIES_GENERATOR_PROJECT
    "${CMAKE_CURRENT_SOURCE_DIR}/generators/RedBolt.Dependencies.Generator/RedBolt.Dependencies.Generator.csproj")
set(REDBOLT_DEPENDENCIES_GENERATOR_DLL
    "${CMAKE_BINARY_DIR}/bin/$<CONFIG>/RedBolt.Dependencies.Generator.dll")
set(REDBOLT_DEPENDENCIES_GENERATOR_STAMP
    "${CMAKE_BINARY_DIR}/cmake/dotnet/RedBolt.Dependencies.Generator-$<CONFIG>.stamp")

function(redbolt_ensure_native_binding_generator)
    get_property(generator_initialized GLOBAL PROPERTY REDBOLT_DEPENDENCIES_GENERATOR_INITIALIZED)
    if(generator_initialized)
        return()
    endif()

    add_custom_command(
        OUTPUT "${REDBOLT_DEPENDENCIES_GENERATOR_STAMP}"
        COMMAND "${DOTNET_EXECUTABLE}" build
            "${REDBOLT_DEPENDENCIES_GENERATOR_PROJECT}"
            --configuration $<CONFIG>
            --no-restore
            "-p:RedBoltCMakePropsPath=${REDBOLT_DOTNET_CMAKE_PROPS_FILE}"
            "-p:SharpCppCMakePropsPath=${REDBOLT_DOTNET_CMAKE_PROPS_FILE}"
        COMMAND "${CMAKE_COMMAND}" -E touch "${REDBOLT_DEPENDENCIES_GENERATOR_STAMP}"
        DEPENDS
            "${REDBOLT_DOTNET_CMAKE_PROPS_FILE}"
            "${CMAKE_CURRENT_SOURCE_DIR}/Directory.Build.props"
            "${CMAKE_CURRENT_SOURCE_DIR}/cmake/dotnet/RedBolt.Build.props"
            "${REDBOLT_DEPENDENCIES_GENERATOR_PROJECT}"
            ${REDBOLT_DEPENDENCIES_GENERATOR_INPUTS}
            ${REDBOLT_SHARPCPP_GENERATOR_INPUTS}
        WORKING_DIRECTORY "${CMAKE_CURRENT_SOURCE_DIR}"
        COMMENT "Building RedBolt native binding generator"
        VERBATIM)

    set_property(GLOBAL PROPERTY REDBOLT_DEPENDENCIES_GENERATOR_INITIALIZED TRUE)
endfunction()

function(redbolt_add_native_binding_generation)
    redbolt_ensure_native_binding_generator()

    set(options)
    set(oneValueArgs TARGET SURFACE HEADER HEADER_INCLUDE_PATH FILE_STEM OUTPUT_DIR OUTPUT_CS OUTPUT_CPP OUTPUT_METADATA)
    set(multiValueArgs INCLUDE_DIRS DEFINES OWNED_HEADERS)
    cmake_parse_arguments(ARG "${options}" "${oneValueArgs}" "${multiValueArgs}" ${ARGN})

    foreach(required TARGET SURFACE HEADER HEADER_INCLUDE_PATH FILE_STEM OUTPUT_DIR OUTPUT_CS OUTPUT_CPP OUTPUT_METADATA)
        if(NOT ARG_${required})
            message(FATAL_ERROR "redbolt_add_native_binding_generation missing ${required}")
        endif()
    endforeach()

    set(generated_aliases "${ARG_OUTPUT_DIR}/${ARG_FILE_STEM}.Bindings.Aliases.g.cs")

    set(include_args)
    foreach(include_dir IN LISTS REDBOLT_BINDING_INCLUDE_DIRS ARG_INCLUDE_DIRS CMAKE_CXX_IMPLICIT_INCLUDE_DIRECTORIES)
        if(NOT include_dir STREQUAL "")
            list(APPEND include_args --include "${include_dir}")
        endif()
    endforeach()

    set(define_args)
    foreach(define IN LISTS REDBOLT_BINDING_DEFINES ARG_DEFINES)
        if(NOT define STREQUAL "")
            list(APPEND define_args --define "${define}")
        endif()
    endforeach()

    set(owned_header_args)
    foreach(owned_header IN LISTS ARG_OWNED_HEADERS)
        if(NOT owned_header STREQUAL "")
            list(APPEND owned_header_args --owned-header "${owned_header}")
        endif()
    endforeach()

    add_custom_command(
        OUTPUT
            "${ARG_OUTPUT_CS}"
            "${ARG_OUTPUT_CPP}"
            "${generated_aliases}"
            "${ARG_OUTPUT_METADATA}"
        COMMAND "${DOTNET_EXECUTABLE}" "${REDBOLT_DEPENDENCIES_GENERATOR_DLL}"
            --surface "${ARG_SURFACE}"
            --header "${ARG_HEADER}"
            --include-name "${ARG_HEADER_INCLUDE_PATH}"
            --output-dir "${ARG_OUTPUT_DIR}"
            --file-stem "${ARG_FILE_STEM}"
            --metadata "${ARG_OUTPUT_METADATA}"
            --sharpcpp-root "${REDBOLT_SHARPCPP_ROOT}"
            --compiler "${CMAKE_CXX_COMPILER}"
            ${include_args}
            ${define_args}
            ${owned_header_args}
        DEPENDS
            "${ARG_HEADER}"
            ${ARG_OWNED_HEADERS}
            "${REDBOLT_DOTNET_CMAKE_PROPS_FILE}"
            "${CMAKE_CURRENT_SOURCE_DIR}/Directory.Build.props"
            "${CMAKE_CURRENT_SOURCE_DIR}/cmake/dotnet/RedBolt.Build.props"
            "${REDBOLT_DEPENDENCIES_GENERATOR_PROJECT}"
            "${REDBOLT_DEPENDENCIES_GENERATOR_STAMP}"
            ${REDBOLT_DEPENDENCIES_GENERATOR_INPUTS}
            ${REDBOLT_SHARPCPP_GENERATOR_INPUTS}
        WORKING_DIRECTORY "${CMAKE_CURRENT_SOURCE_DIR}"
        COMMENT "Generating RedBolt native bindings"
        VERBATIM)

    add_custom_target(${ARG_TARGET}
        DEPENDS
            "${ARG_OUTPUT_CS}"
            "${ARG_OUTPUT_CPP}"
            "${generated_aliases}"
            "${ARG_OUTPUT_METADATA}")
endfunction()
