#!/usr/bin/env bash

set -euo pipefail

script_directory="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repository_root="$(cd -- "$script_directory/.." && pwd)"
tests_root="${REDBOLT_TESTS_ROOT:-$repository_root/../RedBolt.Tests}"
reference_root="${REDBOLT_VISUAL_REFERENCE_ROOT:-$tests_root/src/RedBolt/Tests/VisualRegression/References}"
xvfb_root="${REDBOLT_XVFB_ROOT:-${XDG_DATA_HOME:-$HOME/.local/share}/redbolt/xvfb}"
cmake_root="${REDBOLT_CMAKE_ROOT:-${XDG_DATA_HOME:-$HOME/.local/share}/uv/tools/cmake/bin}"

if [[ -x "$cmake_root/cmake" ]]; then
    PATH="$cmake_root:$PATH"
fi

if [[ -x "$xvfb_root/usr/bin/xvfb-run" ]]; then
    xvfb_run="$xvfb_root/usr/bin/xvfb-run"
    xvfb_path="$xvfb_root/usr/bin"
    xvfb_lib="$xvfb_root/usr/lib/x86_64-linux-gnu"
else
    xvfb_run="$(command -v xvfb-run || true)"
    xvfb_path=""
    xvfb_lib=""
fi

if [[ -z "$xvfb_run" ]]; then
    printf 'xvfb-run was not found. Set REDBOLT_XVFB_ROOT or install a user-local Xvfb bundle.\n' >&2
    exit 1
fi

if [[ -n "$xvfb_path" ]]; then
    PATH="$xvfb_path:$PATH"
fi
if [[ -n "$xvfb_lib" ]]; then
    LD_LIBRARY_PATH="$xvfb_lib${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
fi

export PATH LD_LIBRARY_PATH
export LIBGL_ALWAYS_SOFTWARE="${LIBGL_ALWAYS_SOFTWARE:-1}"
export MESA_LOADER_DRIVER_OVERRIDE="${MESA_LOADER_DRIVER_OVERRIDE:-llvmpipe}"
export SDL_VIDEODRIVER="${SDL_VIDEODRIVER:-x11}"
export SDL_AUDIODRIVER="${SDL_AUDIODRIVER:-dummy}"
export REDBOLT_RUN_VISUAL_TESTS="${REDBOLT_RUN_VISUAL_TESTS:-1}"
export REDBOLT_RUN_GRAPHICS_BACKEND_LIFECYCLE="${REDBOLT_RUN_GRAPHICS_BACKEND_LIFECYCLE:-1}"
export REDBOLT_RUN_DILIGENT_GL_SMOKE="${REDBOLT_RUN_DILIGENT_GL_SMOKE:-1}"
export REDBOLT_VISUAL_REFERENCE_ROOT="$reference_root"

if [[ -z "${LD_PRELOAD:-}" && -f "$repository_root/build/xvfb-redirect.so" ]]; then
    export LD_PRELOAD="$repository_root/build/xvfb-redirect.so"
fi

exec "$xvfb_run" -a -s "-screen 0 256x192x24 -nolisten tcp" \
    dotnet test "$tests_root/RedBolt.Tests.csproj" \
    --configuration Debug --no-restore "$@"
