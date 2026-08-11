if(REDBOLT_ANDROID_BUILD OR ANDROID)
    find_program(_redbolt_binding_cxx_compiler NAMES clang++-20 clang++ REQUIRED)
    string(REGEX REPLACE "^android-" "" _redbolt_android_api "${ANDROID_PLATFORM}")
    if(ANDROID_ABI STREQUAL "x86_64")
        set(_redbolt_binding_target_triple "x86_64-linux-android${_redbolt_android_api}")
    elseif(ANDROID_ABI STREQUAL "arm64-v8a")
        set(_redbolt_binding_target_triple "aarch64-linux-android${_redbolt_android_api}")
    else()
        message(FATAL_ERROR "Unsupported Android ABI for binding generation: ${ANDROID_ABI}")
    endif()
    get_filename_component(_redbolt_android_compiler_bin "${CMAKE_CXX_COMPILER}" DIRECTORY)
    get_filename_component(_redbolt_android_toolchain_root "${_redbolt_android_compiler_bin}" DIRECTORY)
    set(_redbolt_binding_sysroot "${_redbolt_android_toolchain_root}/sysroot")
else()
    set(_redbolt_binding_cxx_compiler "${CMAKE_CXX_COMPILER}")
    set(_redbolt_binding_target_triple "")
    set(_redbolt_binding_sysroot "")
endif()
set(REDBOLT_BINDING_CXX_COMPILER "${_redbolt_binding_cxx_compiler}" CACHE FILEPATH
    "C++ compiler used to discover headers for native binding generation" FORCE)
set(REDBOLT_BINDING_TARGET_TRIPLE "${_redbolt_binding_target_triple}" CACHE INTERNAL
    "Target triple used for native binding generation" FORCE)
set(REDBOLT_BINDING_SYSROOT "${_redbolt_binding_sysroot}" CACHE INTERNAL
    "Sysroot used for native binding generation" FORCE)

function(redbolt_add_native_binding_generation)
    set(options)
    set(oneValueArgs TARGET SURFACE HEADER HEADER_INCLUDE_PATH FILE_STEM OUTPUT_DIR OUTPUT_CS OUTPUT_CPP OUTPUT_METADATA NATIVE_LIBRARY_NAME COMPILER)
    set(multiValueArgs INCLUDE_DIRS DEFINES OWNED_HEADERS)
    cmake_parse_arguments(ARG "${options}" "${oneValueArgs}" "${multiValueArgs}" ${ARGN})

    foreach(required TARGET SURFACE HEADER HEADER_INCLUDE_PATH FILE_STEM OUTPUT_DIR OUTPUT_CS OUTPUT_CPP OUTPUT_METADATA NATIVE_LIBRARY_NAME)
        if(NOT ARG_${required})
            message(FATAL_ERROR "redbolt_add_native_binding_generation missing ${required}")
        endif()
    endforeach()

    set(generated_aliases "${ARG_OUTPUT_DIR}/${ARG_FILE_STEM}.Bindings.Aliases.g.cs")
    set(generator_log "${ARG_OUTPUT_DIR}/${ARG_FILE_STEM}.log")

    # Keep the ClangSharp parser's include search path aligned with the native
    # compiler. This matters for custom sysroots and include directories that
    # are supplied through CMAKE_CXX_FLAGS.
    set(compiler_flag_include_dirs)
    separate_arguments(compiler_flags NATIVE_COMMAND "${CMAKE_CXX_FLAGS}")
    list(LENGTH compiler_flags compiler_flag_count)
    set(compiler_flag_index 0)
    while(compiler_flag_index LESS compiler_flag_count)
        list(GET compiler_flags ${compiler_flag_index} compiler_flag)
        if(compiler_flag MATCHES "^-I(.+)$")
            list(APPEND compiler_flag_include_dirs "${CMAKE_MATCH_1}")
        elseif(compiler_flag MATCHES "^-isystem(.+)$")
            list(APPEND compiler_flag_include_dirs "${CMAKE_MATCH_1}")
        elseif(compiler_flag MATCHES "^-idirafter(.+)$")
            list(APPEND compiler_flag_include_dirs "${CMAKE_MATCH_1}")
        elseif(compiler_flag MATCHES "^-iquote(.+)$")
            list(APPEND compiler_flag_include_dirs "${CMAKE_MATCH_1}")
        elseif(compiler_flag STREQUAL "-I" OR
               compiler_flag STREQUAL "-isystem" OR
               compiler_flag STREQUAL "-idirafter" OR
               compiler_flag STREQUAL "-iquote")
            math(EXPR compiler_flag_index "${compiler_flag_index} + 1")
            if(compiler_flag_index LESS compiler_flag_count)
                list(GET compiler_flags ${compiler_flag_index} compiler_flag_include_dir)
                list(APPEND compiler_flag_include_dirs "${compiler_flag_include_dir}")
            endif()
        endif()
        math(EXPR compiler_flag_index "${compiler_flag_index} + 1")
    endwhile()

    set(include_args)
    foreach(include_dir IN LISTS
            REDBOLT_BINDING_INCLUDE_DIRS
            ARG_INCLUDE_DIRS
            compiler_flag_include_dirs)
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

    set(binding_compiler "${REDBOLT_BINDING_CXX_COMPILER}")
    if(ARG_COMPILER)
        set(binding_compiler "${ARG_COMPILER}")
    endif()

    set(binding_toolchain_args)
    if(REDBOLT_BINDING_TARGET_TRIPLE)
        list(APPEND binding_toolchain_args --compiler-target "${REDBOLT_BINDING_TARGET_TRIPLE}")
    endif()
    if(REDBOLT_BINDING_SYSROOT)
        list(APPEND binding_toolchain_args --compiler-sysroot "${REDBOLT_BINDING_SYSROOT}")
    endif()

    set(binding_native_library_name "${ARG_NATIVE_LIBRARY_NAME}")
    if(ANDROID)
        set(binding_native_library_name "lib${ARG_NATIVE_LIBRARY_NAME}.so")
    endif()

    add_custom_command(
        OUTPUT
            "${ARG_OUTPUT_CS}"
            "${ARG_OUTPUT_CPP}"
            "${generated_aliases}"
            "${ARG_OUTPUT_METADATA}"
            "${generator_log}"
        COMMAND ${binding_generator_command}
            --surface "${ARG_SURFACE}"
            --header "${ARG_HEADER}"
            --include-name "${ARG_HEADER_INCLUDE_PATH}"
            --output-dir "${ARG_OUTPUT_DIR}"
            --file-stem "${ARG_FILE_STEM}"
            --metadata "${ARG_OUTPUT_METADATA}"
            --log "${generator_log}"
            --sharpcpp-root "${REDBOLT_SHARPCPP_ROOT}"
            --redbolt-root "${CMAKE_SOURCE_DIR}"
            --native-library-name "${binding_native_library_name}"
            --compiler "${binding_compiler}"
            ${binding_toolchain_args}
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
            "${ARG_OUTPUT_METADATA}"
            "${generator_log}")
endfunction()
