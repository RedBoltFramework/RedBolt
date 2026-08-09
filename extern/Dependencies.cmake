include("${CMAKE_SOURCE_DIR}/cmake/CPM.cmake")
find_package(Git REQUIRED)

function(redbolt_add_dependency name repository revision)
    cmake_parse_arguments(PARSE_ARGV 3 ARG "WITH_SUBMODULES" "" "")
    set(_redbolt_source_dir "${REDBOLT_EXTERN_ROOT}/${name}")
    if(EXISTS "${_redbolt_source_dir}/.git")
        execute_process(
            COMMAND "${GIT_EXECUTABLE}" rev-parse HEAD
            WORKING_DIRECTORY "${_redbolt_source_dir}"
            OUTPUT_VARIABLE _redbolt_head
            OUTPUT_STRIP_TRAILING_WHITESPACE
            ERROR_QUIET)
    endif()
    if("${_redbolt_head}" STREQUAL "${revision}")
        CPMAddPackage(
            NAME "${name}"
            SOURCE_DIR "${_redbolt_source_dir}"
            DOWNLOAD_ONLY YES)
        return()
    endif()

    if(ARG_WITH_SUBMODULES)
        CPMAddPackage(
            NAME "${name}"
            GIT_REPOSITORY "${repository}"
            GIT_TAG "${revision}"
            GIT_SHALLOW FALSE
            GIT_SUBMODULES_RECURSE TRUE
            DOWNLOAD_ONLY YES
            SOURCE_DIR "${_redbolt_source_dir}")
    else()
        # CPM drops an empty GIT_SUBMODULES value, so use an exclude-only
        # pathspec to suppress FetchContent's default recursive checkout.
        CPMAddPackage(
            NAME "${name}"
            GIT_REPOSITORY "${repository}"
            GIT_TAG "${revision}"
            GIT_SHALLOW FALSE
            GIT_SUBMODULES ":!*"
            GIT_SUBMODULES_RECURSE FALSE
            DOWNLOAD_ONLY YES
            SOURCE_DIR "${_redbolt_source_dir}")
    endif()
endfunction()

redbolt_add_dependency(Diligent https://github.com/DiligentGraphics/DiligentCore.git d4fe9c85a2011b4fd030a3cd107f5c46c94fbe8b WITH_SUBMODULES)
redbolt_add_dependency(DotRecast https://github.com/ikpil/DotRecast.git e6db95f5620590bd47d1349db73263e812bb1df6)
redbolt_add_dependency(Embree https://github.com/RenderKit/embree.git f590db83ef6559387df7f6d8725c34fb7acf851d)
redbolt_add_dependency(JoltPhysics https://github.com/jrouwe/JoltPhysics.git 341ac92c7a8a63a4a37729ad6eca21f9a5a51e13)
redbolt_add_dependency(LZ4 https://github.com/lz4/lz4.git 0774d05537f9762f838f7ab541b7765f1a729cb5)
redbolt_add_dependency(RmlUi https://github.com/mikke89/RmlUi.git 2230d1a6e8e0848ed87a5761e2a5160b2a175ba4)
redbolt_add_dependency(SDL_image https://github.com/libsdl-org/SDL_image.git 1fe71e1086e00b73999a8b63ad6afabb8ce43b23)
redbolt_add_dependency(STB https://github.com/nothings/stb.git 31c1ad37456438565541f4919958214b6e762fb4)
redbolt_add_dependency(SharpCpp https://github.com/RedBoltFramework/SharpCpp.git 5745b0d98d18659aee6dd53e310a2a2c03ee895b)
redbolt_add_dependency(WebP https://chromium.googlesource.com/webm/libwebp 733c91e461c18cf1127c9ed0a80dccbcfed599d3)
redbolt_add_dependency(harfbuzz https://github.com/harfbuzz/harfbuzz.git 56feae4035bdd48f62ba2b8d8c16232d4d89b3a4)
redbolt_add_dependency(imgui https://github.com/RedBoltFramework/imgui.git 49ed37b2bcf2d219b47833cb6a5bde83c49a309d)
redbolt_add_dependency(libdatachannel https://github.com/rbfx/libdatachannel.git 400c57e7fc1e4f4ce866135da028722083517ac9 WITH_SUBMODULES)
redbolt_add_dependency(openssl https://github.com/openssl/openssl.git 29708a562a1887a91de0fa6ca668c71871accde9)
redbolt_add_dependency(tinygltf https://github.com/syoyo/tinygltf.git 488a70a3df62a4df1a736e9e56fb8836580c4888)
redbolt_add_dependency(xatlas https://github.com/jpcy/xatlas.git 413be25a3106d4bd894697d2d3e0001881caa97e)
