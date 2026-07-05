add_library(RedBolt.Native SHARED
    "${REDBOLT_SDL_BINDINGS_CPP}"
    "${REDBOLT_DILIGENT_BINDINGS_CPP}")

set_source_files_properties(
    "${REDBOLT_SDL_BINDINGS_CPP}"
    "${REDBOLT_DILIGENT_BINDINGS_CPP}"
    PROPERTIES GENERATED TRUE)

add_dependencies(RedBolt.Native redbolt-generate-native-bindings)

set_target_properties(RedBolt.Native PROPERTIES
    OUTPUT_NAME "RedBolt.Native"
    RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin/$<CONFIG>"
    LIBRARY_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin/$<CONFIG>"
    ARCHIVE_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/lib/$<CONFIG>")

target_compile_definitions(RedBolt.Native PRIVATE REDBOLT_NATIVE_BUILD)
target_compile_options(RedBolt.Native PRIVATE
    "$<$<CXX_COMPILER_ID:GNU>:-Wno-attributes>"
    "$<$<CXX_COMPILER_ID:Clang>:-Wno-attributes>")
target_link_libraries(RedBolt.Native
    PRIVATE
        SDL2::SDL2
        PkgConfig::REDBOLT_SDL2_IMAGE
        Diligent-GraphicsEngine
        Diligent-GraphicsEngineOpenGL-static
        redbolt-native-support)

add_executable(redbolt-native-smoke-exe
    smoke/RedBoltNativeSmoke.cpp)

set_target_properties(redbolt-native-smoke-exe PROPERTIES
    RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin/$<CONFIG>")

target_link_libraries(redbolt-native-smoke-exe PRIVATE SDL2::SDL2 redbolt-native-support)

add_custom_target(redbolt-native-smoke
    COMMAND redbolt-native-smoke-exe
    DEPENDS redbolt-native-smoke-exe RedBolt.Native
    WORKING_DIRECTORY "${CMAKE_BINARY_DIR}/bin/$<CONFIG>"
    COMMENT "Running C++ RedBolt.Native smoke")
