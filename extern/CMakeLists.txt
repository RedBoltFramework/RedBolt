redbolt_require_path("${REDBOLT_THIRDPARTY_ROOT}" "REDBOLT_THIRDPARTY_ROOT")
redbolt_require_path("${REDBOLT_SHARPCPP_ROOT}" "REDBOLT_SHARPCPP_ROOT")
redbolt_require_path("${REDBOLT_SHARPCPP_ROOT}/include/SharpCpp/Export.hpp" "SharpCpp native support headers")

if(NOT COMMAND install_third_party_libs)
    function(install_third_party_libs)
    endfunction()
endif()

if(NOT COMMAND group_sources)
    function(group_sources)
    endfunction()
endif()

add_subdirectory(SDL)
add_subdirectory(Diligent)
add_subdirectory(ImGui)
add_subdirectory(Compression)

add_custom_target(redbolt-native-dependency-batch
    DEPENDS SDL2 redbolt-diligent-build ImGui
    COMMENT "Building RedBolt native dependency batch")
