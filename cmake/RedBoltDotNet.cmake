include_guard(GLOBAL)

function(redbolt_get_configurations output_var)
    if(CMAKE_CONFIGURATION_TYPES)
        set(configurations ${CMAKE_CONFIGURATION_TYPES})
    elseif(CMAKE_BUILD_TYPE)
        set(configurations ${CMAKE_BUILD_TYPE})
    else()
        set(configurations Debug)
    endif()

    list(REMOVE_DUPLICATES configurations)
    set(${output_var} ${configurations} PARENT_SCOPE)
endfunction()

function(redbolt_initialize_dotnet_consumer)
    set(options)
    set(oneValueArgs CONSUMER_ROOT OUTPUT_ROOT INTERMEDIATE_ROOT DOTNET_STATE_DIR PROPS_FILE COMPAT_PROPS_FILE GENERATED_BINDINGS_DIR SHARPCPP_ROOT)
    set(multiValueArgs)
    cmake_parse_arguments(ARG "${options}" "${oneValueArgs}" "${multiValueArgs}" ${ARGN})

    get_property(already_initialized GLOBAL PROPERTY REDBOLT_DOTNET_CONSUMER_INITIALIZED)
    if(already_initialized)
        return()
    endif()

    if("${ARG_CONSUMER_ROOT}" STREQUAL "")
        set(consumer_root "${CMAKE_SOURCE_DIR}")
    else()
        get_filename_component(consumer_root "${ARG_CONSUMER_ROOT}" ABSOLUTE BASE_DIR "${CMAKE_SOURCE_DIR}")
    endif()

    if("${ARG_OUTPUT_ROOT}" STREQUAL "")
        set(output_root "${CMAKE_BINARY_DIR}/bin")
    else()
        get_filename_component(output_root "${ARG_OUTPUT_ROOT}" ABSOLUTE BASE_DIR "${CMAKE_BINARY_DIR}")
    endif()

    if("${ARG_INTERMEDIATE_ROOT}" STREQUAL "")
        set(intermediate_root "${CMAKE_BINARY_DIR}/obj/managed")
    else()
        get_filename_component(intermediate_root "${ARG_INTERMEDIATE_ROOT}" ABSOLUTE BASE_DIR "${CMAKE_BINARY_DIR}")
    endif()

    if("${ARG_DOTNET_STATE_DIR}" STREQUAL "")
        set(dotnet_state_dir "${CMAKE_BINARY_DIR}/cmake/dotnet")
    else()
        get_filename_component(dotnet_state_dir "${ARG_DOTNET_STATE_DIR}" ABSOLUTE BASE_DIR "${CMAKE_BINARY_DIR}")
    endif()

    if("${ARG_PROPS_FILE}" STREQUAL "")
        set(build_props_file "${dotnet_state_dir}/RedBolt.CMake.props")
    else()
        get_filename_component(build_props_file "${ARG_PROPS_FILE}" ABSOLUTE BASE_DIR "${CMAKE_BINARY_DIR}")
    endif()

    if("${ARG_COMPAT_PROPS_FILE}" STREQUAL "")
        set(compat_props_file "${consumer_root}/.CMakeLast.props")
    else()
        get_filename_component(compat_props_file "${ARG_COMPAT_PROPS_FILE}" ABSOLUTE BASE_DIR "${consumer_root}")
    endif()

    if("${ARG_GENERATED_BINDINGS_DIR}" STREQUAL "")
        set(generated_bindings_dir "")
    else()
        get_filename_component(generated_bindings_dir "${ARG_GENERATED_BINDINGS_DIR}" ABSOLUTE BASE_DIR "${CMAKE_BINARY_DIR}")
    endif()

    if("${ARG_SHARPCPP_ROOT}" STREQUAL "")
        set(sharpcpp_root "")
        set(sharpcpp_compat_props_file "")
    else()
        get_filename_component(sharpcpp_root "${ARG_SHARPCPP_ROOT}" ABSOLUTE BASE_DIR "${consumer_root}")
        set(sharpcpp_compat_props_file "${sharpcpp_root}/.CMakeLast.props")
    endif()

    set_property(GLOBAL PROPERTY REDBOLT_DOTNET_CONSUMER_ROOT "${consumer_root}")
    set_property(GLOBAL PROPERTY REDBOLT_DOTNET_OUTPUT_ROOT "${output_root}")
    set_property(GLOBAL PROPERTY REDBOLT_DOTNET_INTERMEDIATE_ROOT "${intermediate_root}")
    set_property(GLOBAL PROPERTY REDBOLT_DOTNET_STATE_DIR "${dotnet_state_dir}")
    set_property(GLOBAL PROPERTY REDBOLT_DOTNET_BUILD_PROPS_FILE "${build_props_file}")
    set_property(GLOBAL PROPERTY REDBOLT_DOTNET_COMPAT_PROPS_FILE "${compat_props_file}")
    set_property(GLOBAL PROPERTY REDBOLT_DOTNET_GENERATED_BINDINGS_DIR "${generated_bindings_dir}")
    set_property(GLOBAL PROPERTY REDBOLT_DOTNET_SHARPCPP_ROOT "${sharpcpp_root}")
    set_property(GLOBAL PROPERTY REDBOLT_DOTNET_SHARPCPP_COMPAT_PROPS_FILE "${sharpcpp_compat_props_file}")
    set_property(GLOBAL PROPERTY REDBOLT_DOTNET_CONSUMER_INITIALIZED TRUE)

    set(REDBOLT_DOTNET_CMAKE_PROPS_FILE "${build_props_file}" PARENT_SCOPE)
    set(REDBOLT_DOTNET_COMPAT_PROPS_FILE "${compat_props_file}" PARENT_SCOPE)

    get_property(finalize_deferred GLOBAL PROPERTY REDBOLT_DOTNET_FINALIZE_DEFERRED)
    if(NOT finalize_deferred)
        cmake_language(DEFER CALL redbolt_finalize_dotnet_consumer)
        set_property(GLOBAL PROPERTY REDBOLT_DOTNET_FINALIZE_DEFERRED TRUE)
    endif()
endfunction()

function(redbolt_ensure_dotnet_consumer_initialized)
    get_property(consumer_initialized GLOBAL PROPERTY REDBOLT_DOTNET_CONSUMER_INITIALIZED)
    if(NOT consumer_initialized)
        redbolt_initialize_dotnet_consumer()
    endif()
endfunction()

function(redbolt_write_last_props)
    redbolt_ensure_dotnet_consumer_initialized()

    redbolt_get_configurations(configurations)
    list(GET configurations 0 default_configuration)

    get_property(output_root GLOBAL PROPERTY REDBOLT_DOTNET_OUTPUT_ROOT)
    get_property(intermediate_root GLOBAL PROPERTY REDBOLT_DOTNET_INTERMEDIATE_ROOT)
    get_property(build_props_file GLOBAL PROPERTY REDBOLT_DOTNET_BUILD_PROPS_FILE)
    get_property(compat_props_file GLOBAL PROPERTY REDBOLT_DOTNET_COMPAT_PROPS_FILE)
    get_property(generated_bindings_dir GLOBAL PROPERTY REDBOLT_DOTNET_GENERATED_BINDINGS_DIR)
    get_property(sharpcpp_root GLOBAL PROPERTY REDBOLT_DOTNET_SHARPCPP_ROOT)
    get_property(sharpcpp_compat_props_file GLOBAL PROPERTY REDBOLT_DOTNET_SHARPCPP_COMPAT_PROPS_FILE)

    string(REPLACE "\\" "/" output_root "${output_root}")
    string(REPLACE "\\" "/" intermediate_root "${intermediate_root}")
    string(REPLACE "\\" "/" cmake_build_dir "${CMAKE_BINARY_DIR}")
    string(REPLACE "\\" "/" generated_bindings_dir "${generated_bindings_dir}")
    string(REPLACE "\\" "/" sharpcpp_root "${sharpcpp_root}")
    string(REPLACE "\\" "/" sharpcpp_compat_props_file "${sharpcpp_compat_props_file}")

    set(managed_define_constants "$(DefineConstants)")
    if(D3D11_SUPPORTED)
        string(APPEND managed_define_constants ";REDBOLT_D3D11_SUPPORTED")
    endif()
    if(D3D12_SUPPORTED)
        string(APPEND managed_define_constants ";REDBOLT_D3D12_SUPPORTED")
    endif()
    if(GL_SUPPORTED)
        string(APPEND managed_define_constants ";REDBOLT_GL_SUPPORTED")
    endif()
    if(VULKAN_SUPPORTED)
        string(APPEND managed_define_constants ";REDBOLT_VULKAN_SUPPORTED")
    endif()
    if(GLES_SUPPORTED)
        string(APPEND managed_define_constants ";REDBOLT_GLES_SUPPORTED")
    endif()
    if(PLATFORM_ANDROID)
        string(APPEND managed_define_constants ";REDBOLT_PLATFORM_ANDROID")
    endif()

    set(content "<Project>\n")
    if(NOT sharpcpp_compat_props_file STREQUAL "")
        string(APPEND content
            "  <PropertyGroup>\n"
            "    <SharpCppCompatPropsPath>${sharpcpp_compat_props_file}</SharpCppCompatPropsPath>\n"
            "  </PropertyGroup>\n"
            "  <Import Project=\"$(SharpCppCompatPropsPath)\" Condition=\"Exists('$(SharpCppCompatPropsPath)')\" />\n")
    endif()

    string(APPEND content
        "  <PropertyGroup Condition=\"'$(Configuration)' == ''\">\n"
        "    <DefineConstants>${managed_define_constants}</DefineConstants>\n"
        "    <RedBoltCMakeBuildDir>${cmake_build_dir}</RedBoltCMakeBuildDir>\n"
        "    <RedBoltOutputDir>${output_root}/${default_configuration}</RedBoltOutputDir>\n"
        "    <RedBoltIntermediateRoot>${intermediate_root}/${default_configuration}</RedBoltIntermediateRoot>\n"
        "    <RedBoltGeneratedBindingsDir>${generated_bindings_dir}</RedBoltGeneratedBindingsDir>\n"
        "    <RedBoltSharpCppRoot>${sharpcpp_root}</RedBoltSharpCppRoot>\n"
        "    <SharpCppOutputDir>${output_root}/${default_configuration}</SharpCppOutputDir>\n"
        "    <SharpCppIntermediateRoot>${intermediate_root}/${default_configuration}</SharpCppIntermediateRoot>\n"
        "  </PropertyGroup>\n")

    foreach(configuration IN LISTS configurations)
        string(APPEND content
            "  <PropertyGroup Condition=\"'$(Configuration)' == '${configuration}'\">\n"
            "    <DefineConstants>${managed_define_constants}</DefineConstants>\n"
            "    <RedBoltCMakeBuildDir>${cmake_build_dir}</RedBoltCMakeBuildDir>\n"
            "    <RedBoltOutputDir>${output_root}/${configuration}</RedBoltOutputDir>\n"
            "    <RedBoltIntermediateRoot>${intermediate_root}/${configuration}</RedBoltIntermediateRoot>\n"
            "    <RedBoltGeneratedBindingsDir>${generated_bindings_dir}</RedBoltGeneratedBindingsDir>\n"
            "    <RedBoltSharpCppRoot>${sharpcpp_root}</RedBoltSharpCppRoot>\n"
            "    <SharpCppOutputDir>${output_root}/${configuration}</SharpCppOutputDir>\n"
            "    <SharpCppIntermediateRoot>${intermediate_root}/${configuration}</SharpCppIntermediateRoot>\n"
            "  </PropertyGroup>\n")
    endforeach()

    string(APPEND content "</Project>\n")

    get_filename_component(build_props_dir "${build_props_file}" DIRECTORY)
    file(MAKE_DIRECTORY "${build_props_dir}")
    file(WRITE "${build_props_file}" "${content}")

    if(NOT compat_props_file STREQUAL "")
        get_filename_component(compat_props_dir "${compat_props_file}" DIRECTORY)
        file(MAKE_DIRECTORY "${compat_props_dir}")
        file(WRITE "${compat_props_file}" "${content}")
    endif()
endfunction()

function(redbolt_finalize_dotnet_consumer)
    redbolt_ensure_dotnet_consumer_initialized()
    redbolt_write_last_props()
endfunction()
