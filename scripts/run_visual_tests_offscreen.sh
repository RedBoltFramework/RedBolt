#!/usr/bin/env bash

set -euo pipefail

script_directory="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repository_root="$(cd -- "$script_directory/.." && pwd)"
tests_root="${REDBOLT_TESTS_ROOT:-$repository_root/../RedBolt.Tests}"
reference_root="${REDBOLT_VISUAL_REFERENCE_ROOT:-$tests_root/src/RedBolt/Tests/VisualRegression/References}"
xvfb_run="$(command -v xvfb-run || true)"

if [[ -z "$xvfb_run" ]]; then
    printf 'xvfb-run was not found. Install Xvfb and ensure it is on PATH.\n' >&2
    exit 1
fi

export LIBGL_ALWAYS_SOFTWARE="${LIBGL_ALWAYS_SOFTWARE:-1}"
export MESA_LOADER_DRIVER_OVERRIDE="${MESA_LOADER_DRIVER_OVERRIDE:-llvmpipe}"
export SDL_VIDEODRIVER="${SDL_VIDEODRIVER:-x11}"
export SDL_AUDIODRIVER="${SDL_AUDIODRIVER:-dummy}"
export REDBOLT_RUN_VISUAL_TESTS="${REDBOLT_RUN_VISUAL_TESTS:-1}"
export REDBOLT_RUN_GRAPHICS_BACKEND_LIFECYCLE="${REDBOLT_RUN_GRAPHICS_BACKEND_LIFECYCLE:-1}"
export REDBOLT_RUN_DILIGENT_GL_SMOKE="${REDBOLT_RUN_DILIGENT_GL_SMOKE:-1}"
export REDBOLT_VISUAL_REFERENCE_ROOT="$reference_root"

exec "$xvfb_run" -a -s "-screen 0 256x192x24 -nolisten tcp" \
    dotnet test "$tests_root/RedBolt.Tests.csproj" \
    --configuration Debug --no-restore "$@"
