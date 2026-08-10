set(CMAKE_SYSTEM_NAME Windows)
set(CMAKE_SYSTEM_PROCESSOR x86_64)

find_program(_redbolt_mingw_c_compiler NAMES x86_64-w64-mingw32-gcc REQUIRED)
find_program(_redbolt_mingw_cxx_compiler NAMES x86_64-w64-mingw32-g++ REQUIRED)
find_program(_redbolt_mingw_rc_compiler NAMES x86_64-w64-mingw32-windres REQUIRED)
set(CMAKE_C_COMPILER "${_redbolt_mingw_c_compiler}")
set(CMAKE_CXX_COMPILER "${_redbolt_mingw_cxx_compiler}")
set(CMAKE_RC_COMPILER "${_redbolt_mingw_rc_compiler}")

set(CMAKE_CXX_FLAGS_INIT "-I${CMAKE_CURRENT_LIST_DIR}/MinGW/include")

set(CMAKE_FIND_ROOT_PATH_MODE_PROGRAM NEVER)
set(CMAKE_FIND_ROOT_PATH_MODE_LIBRARY ONLY)
set(CMAKE_FIND_ROOT_PATH_MODE_INCLUDE ONLY)
set(CMAKE_FIND_ROOT_PATH_MODE_PACKAGE ONLY)
