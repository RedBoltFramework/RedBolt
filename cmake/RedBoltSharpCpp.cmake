function(redbolt_add_native_binding_generation)
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
    set(owned_header_inputs)
    foreach(owned_header IN LISTS ARG_OWNED_HEADERS)
        if(NOT owned_header STREQUAL "")
            list(APPEND owned_header_args --owned-header "${owned_header}")
            if(IS_DIRECTORY "${owned_header}")
                file(GLOB_RECURSE owned_header_files CONFIGURE_DEPENDS
                    LIST_DIRECTORIES FALSE
                    "${owned_header}/*.h"
                    "${owned_header}/*.hh"
                    "${owned_header}/*.hpp"
                    "${owned_header}/*.hxx"
                    "${owned_header}/*.inc"
                    "${owned_header}/*.inl")
                list(APPEND owned_header_inputs ${owned_header_files})
            else()
                list(APPEND owned_header_inputs "${owned_header}")
            endif()
        endif()
    endforeach()

    set(binding_generator_command
        "${DOTNET_EXECUTABLE}" "${REDBOLT_DEPENDENCIES_GENERATOR_DLL}")
    if(EMSCRIPTEN)
        list(PREPEND binding_generator_command "${_redbolt_emscripten_environment}")
    endif()

    add_custom_command(
        OUTPUT
            "${ARG_OUTPUT_CS}"
            "${ARG_OUTPUT_CPP}"
            "${generated_aliases}"
            "${ARG_OUTPUT_METADATA}"
        COMMAND ${binding_generator_command}
            --surface "${ARG_SURFACE}"
            --header "${ARG_HEADER}"
            --include-name "${ARG_HEADER_INCLUDE_PATH}"
            --output-dir "${ARG_OUTPUT_DIR}"
            --file-stem "${ARG_FILE_STEM}"
            --metadata "${ARG_OUTPUT_METADATA}"
            --sharpcpp-root "${REDBOLT_SHARPCPP_ROOT}"
            --redbolt-root "${CMAKE_SOURCE_DIR}"
            --compiler "${CMAKE_CXX_COMPILER}"
            ${include_args}
            ${define_args}
            ${owned_header_args}
        DEPENDS
            "${ARG_HEADER}"
            ${owned_header_inputs}
            "${REDBOLT_DEPENDENCIES_GENERATOR_DLL}"
            ${REDBOLT_DEPENDENCIES_GENERATOR_INPUTS}
            ${REDBOLT_SHARPCPP_GENERATOR_INPUTS}
        WORKING_DIRECTORY "${PROJECT_SOURCE_DIR}"
        COMMENT "Generating ${ARG_SURFACE} bindings"
        VERBATIM)

    add_custom_target(${ARG_TARGET}
        DEPENDS
            "${ARG_OUTPUT_CS}"
            "${ARG_OUTPUT_CPP}"
            "${generated_aliases}"
            "${ARG_OUTPUT_METADATA}")
endfunction()
