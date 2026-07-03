foreach(path IN ITEMS "${REDBOLT_THIRDPARTY_ROOT}" "${REDBOLT_SHARPCPP_ROOT}")
    if(NOT EXISTS "${path}")
        message(FATAL_ERROR "Required RedBolt path is missing: ${path}")
    endif()
endforeach()

foreach(path IN ITEMS
    "${REDBOLT_THIRDPARTY_ROOT}/SDL/CMakeLists.txt"
    "${REDBOLT_THIRDPARTY_ROOT}/Diligent/Graphics/GraphicsEngine/interface/GraphicsTypes.h"
    "${REDBOLT_SHARPCPP_ROOT}/src/SharpCpp.Support/SharpCpp.Support.csproj")
    if(NOT EXISTS "${path}")
        message(FATAL_ERROR "Required RedBolt dependency input is missing: ${path}")
    endif()
endforeach()

message(STATUS "RedBolt dependency configuration is valid")
