CPMAddPackage(
    NAME IconFontCppHeaders
    GIT_REPOSITORY https://github.com/juliettef/IconFontCppHeaders.git
    GIT_TAG 7d6ff1f4ba51e7a2b142be39457768abece1549c
    CUSTOM_CACHE_KEY "."
    PATCHES "${CMAKE_CURRENT_LIST_DIR}/IconFontCppHeaders.patch"
    DOWNLOAD_ONLY YES)
set(REDBOLT_ICON_FONT_CPP_HEADERS_ROOT "${IconFontCppHeaders_SOURCE_DIR}" CACHE INTERNAL "IconFontCppHeaders source root")
set(REDBOLT_FONT_AWESOME_ICONS_SOURCE "${REDBOLT_ICON_FONT_CPP_HEADERS_ROOT}/IconsFontAwesome6.cs" CACHE INTERNAL "Font Awesome C# constants")

redbolt_require_path("${REDBOLT_FONT_AWESOME_ICONS_SOURCE}" "Font Awesome C# constants")
redbolt_require_path("${REDBOLT_ICON_FONT_CPP_HEADERS_ROOT}/licence.txt" "IconFontCppHeaders license")
