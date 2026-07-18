#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd -- "$SCRIPT_DIR/../.." && pwd)"
PACKAGE_ROOT="$REPO_ROOT/build/package/nuget"
IN_DIR="$PACKAGE_ROOT/in"
STAGE_DIR="$PACKAGE_ROOT/stage"
OUT_DIR="$PACKAGE_ROOT/out"
LOCAL_REPOSITORY_DIR="$PACKAGE_ROOT/local-repository"
RELEASE_DOWNLOAD_BASE="https://github.com/rbfx/rbfx/releases/download/latest"

DOTNET="${DOTNET:-dotnet}"
NUGET="${NUGET:-nuget}"

declare -A SDK_ASSETS=(
  [linux-x64]="rebelfork-sdk-linux-gcc-x64-dll-latest.7z"
  [osx-x64]="rebelfork-sdk-macos-clang-x64-dll-latest.7z"
  [osx-arm64]="rebelfork-sdk-macos-clang-arm64-dll-latest.7z"
  [win-x64]="rebelfork-sdk-windows-msvc-x64-dll-latest.7z"
  [uap-x64]="rebelfork-sdk-uwp-msvc-x64-dll-latest.7z"
)

usage() {
  cat >&2 <<'EOF'
Usage: package.sh <action> [options]

Actions:
  fetch-sdk     Download and extract one rbfx prebuilt DLL SDK archive
  fetch-sdks    Download and extract all known rbfx prebuilt DLL SDK archives
  create        Build, stage, and pack SDK-style NuGet packages
  local-repo    Create a local NuGet repository from packages in out/

Use 'package.sh <action> --help' for action-specific options.
EOF
}

default_rid() {
  local kernel machine
  kernel="$(uname -s)"
  machine="$(uname -m)"

  case "$kernel" in
    Linux) echo "linux-x64" ;;
    Darwin)
      if [ "$machine" = "arm64" ]; then
        echo "osx-arm64"
      else
        echo "osx-x64"
      fi
      ;;
    MINGW*|MSYS*|CYGWIN*) echo "win-x64" ;;
    *)
      echo "Error: cannot infer runtime identifier for $kernel/$machine" >&2
      return 1
      ;;
  esac
}

sdk_asset_for_rid() {
  local rid="$1"
  local asset="${SDK_ASSETS[$rid]:-}"
  if [ -z "$asset" ]; then
    echo "Error: no SDK asset mapping for RID '$rid'. Pass --sdk-asset or --sdk-root." >&2
    return 1
  fi
  echo "$asset"
}

download_file() {
  local url="$1"
  local output="$2"

  if command -v curl >/dev/null 2>&1; then
    curl --fail --location --show-error --output "$output" "$url"
  elif command -v wget >/dev/null 2>&1; then
    wget -O "$output" "$url"
  else
    echo "Error: curl or wget is required to download '$url'." >&2
    return 1
  fi
}

extract_archive() {
  local archive="$1"

  if command -v 7z >/dev/null 2>&1; then
    7z x -y "-o$IN_DIR" "$archive"
  elif command -v 7za >/dev/null 2>&1; then
    7za x -y "-o$IN_DIR" "$archive"
  else
    echo "Error: 7z or 7za is required to extract '$archive'." >&2
    return 1
  fi
}

normalize_sdk_cmake_exports() {
  local sdk_root="$1"
  local cmake_file="$sdk_root/share/Urho3D/CMake/Urho3D.cmake"
  local debian_dbus_path="/usr/lib/x86_64-linux-gnu/libdbus-1.so"

  if [ ! -f "$cmake_file" ] || [ -e "$debian_dbus_path" ]; then
    return 0
  fi

  if command -v pkg-config >/dev/null 2>&1 && pkg-config --exists dbus-1; then
    sed -i "s#$debian_dbus_path#dbus-1#g" "$cmake_file"
    echo "Normalized missing DBus link path in $cmake_file"
  fi
}

fetch_sdk() {
  local rid=""
  local asset=""
  local force=0

  while [ $# -gt 0 ]; do
    case "$1" in
      --rid)
        rid="$2"
        shift 2
        ;;
      --sdk-asset)
        asset="$2"
        shift 2
        ;;
      --force)
        force=1
        shift
        ;;
      --help|-h)
        cat <<'EOF'
Usage: package.sh fetch-sdk [--rid RID] [--sdk-asset ASSET] [--force]

Downloads and extracts one rbfx prebuilt DLL SDK archive into build/package/nuget/in/.
If --rid is omitted, the host RID is inferred.
EOF
        return 0
        ;;
      *)
        echo "Error: unknown fetch-sdk option '$1'." >&2
        return 1
        ;;
    esac
  done

  if [ -z "$rid" ]; then
    rid="$(default_rid)"
  fi

  if [ -z "$asset" ]; then
    asset="$(sdk_asset_for_rid "$rid")"
  fi

  local archive="$IN_DIR/$asset"
  local sdk_dir="$IN_DIR/${asset%.7z}"

  mkdir -p "$IN_DIR"

  if [ ! -f "$archive" ] || [ "$force" -eq 1 ]; then
    echo "Downloading $asset"
    download_file "$RELEASE_DOWNLOAD_BASE/$asset" "$archive"
  fi

  if [ ! -d "$sdk_dir" ] || [ "$force" -eq 1 ]; then
    echo "Extracting $asset"
    rm -rf "$sdk_dir"
    extract_archive "$archive"
  fi

  normalize_sdk_cmake_exports "$sdk_dir"

  echo "$sdk_dir"
}

fetch_sdks() {
  local rid
  for rid in "${!SDK_ASSETS[@]}"; do
    fetch_sdk --rid "$rid" "$@" >/dev/null
  done
}

require_file() {
  local path="$1"
  if [ ! -f "$path" ]; then
    echo "Error: required file is missing: $path" >&2
    return 1
  fi
}

copy_optional_related_files() {
  local directory="$1"
  local stem="$2"
  local destination="$3"
  local extension

  for extension in dll pdb xml; do
    if [ -f "$directory/$stem.$extension" ]; then
      cp -a "$directory/$stem.$extension" "$destination/"
    fi
  done
}

copy_native_glob() {
  local pattern="$1"
  local destination="$2"
  local matched=0
  local file

  shopt -s nullglob
  for file in $pattern; do
    cp -a "$file" "$destination/"
    matched=1
  done
  shopt -u nullglob

  return "$matched"
}

detect_target_framework() {
  local package_project="$SCRIPT_DIR/packages/SharpCpp.Rbfx.Bindings/SharpCpp.Rbfx.Bindings.Package.csproj"
  local target_framework

  target_framework="$($DOTNET msbuild "$package_project" -getProperty:TargetFramework -nologo)"
  target_framework="${target_framework//$'\r'/}"
  target_framework="${target_framework//$'\n'/}"

  if [ -z "$target_framework" ]; then
    echo "Error: unable to detect package TargetFramework from $package_project" >&2
    return 1
  fi

  echo "$target_framework"
}

stage_packages() {
  local rid="$1"
  local configuration="$2"
  local sdk_root="$3"
  local target_framework="$4"
  local bin_dir="$REPO_ROOT/build/bin/$configuration"

  if [ ! -d "$bin_dir" ]; then
    echo "Error: SharpCpp output directory is missing: $bin_dir" >&2
    return 1
  fi

  rm -rf "$STAGE_DIR"
  mkdir -p \
    "$STAGE_DIR/managed/ref/$target_framework" \
    "$STAGE_DIR/managed/lib/$target_framework" \
    "$STAGE_DIR/managed/analyzers/dotnet/cs" \
    "$STAGE_DIR/runtime/$rid/native" \
    "$STAGE_DIR/data"

  require_file "$bin_dir/SharpCpp.Rbfx.Bindings.dll"
  require_file "$bin_dir/SharpCpp.Support.dll"
  require_file "$bin_dir/SharpCpp.Rbfx.SourceGenerator.dll"

  copy_optional_related_files "$bin_dir" "SharpCpp.Rbfx.Bindings" "$STAGE_DIR/managed/lib/$target_framework"
  copy_optional_related_files "$bin_dir" "SharpCpp.Support" "$STAGE_DIR/managed/lib/$target_framework"
  copy_optional_related_files "$bin_dir" "SharpCpp.Rbfx.Bindings" "$STAGE_DIR/managed/ref/$target_framework"
  copy_optional_related_files "$bin_dir" "SharpCpp.Support" "$STAGE_DIR/managed/ref/$target_framework"
  copy_optional_related_files "$bin_dir" "SharpCpp.Rbfx.SourceGenerator" "$STAGE_DIR/managed/analyzers/dotnet/cs"

  case "$rid" in
    linux-*)
      require_file "$bin_dir/libSharpCpp.Rbfx.Native.so"
      cp -a "$bin_dir/libSharpCpp.Rbfx.Native.so" "$STAGE_DIR/runtime/$rid/native/"
      copy_native_glob "$sdk_root/bin/*.so" "$STAGE_DIR/runtime/$rid/native" || true
      copy_native_glob "$sdk_root/bin/*.so.*" "$STAGE_DIR/runtime/$rid/native" || true
      ;;
    osx-*)
      require_file "$bin_dir/libSharpCpp.Rbfx.Native.dylib"
      cp -a "$bin_dir/libSharpCpp.Rbfx.Native.dylib" "$STAGE_DIR/runtime/$rid/native/"
      copy_native_glob "$sdk_root/bin/*.dylib" "$STAGE_DIR/runtime/$rid/native" || true
      ;;
    win-*|uap-*)
      require_file "$bin_dir/SharpCpp.Rbfx.Native.dll"
      cp -a "$bin_dir/SharpCpp.Rbfx.Native.dll" "$STAGE_DIR/runtime/$rid/native/"
      copy_native_glob "$sdk_root/bin/*.dll" "$STAGE_DIR/runtime/$rid/native" || true
      ;;
    *)
      echo "Error: unsupported runtime identifier '$rid'." >&2
      return 1
      ;;
  esac

  if ! compgen -G "$STAGE_DIR/runtime/$rid/native/*" >/dev/null; then
    echo "Error: no native runtime files were staged for '$rid' from '$bin_dir'." >&2
    return 1
  fi

  local data_name
  for data_name in CoreData Data EditorData; do
    if [ -d "$sdk_root/bin/$data_name" ]; then
      mkdir -p "$STAGE_DIR/data/$data_name"
      cp -a "$sdk_root/bin/$data_name/." "$STAGE_DIR/data/$data_name/"
    fi
  done
}

pack_project() {
  local project="$1"
  local version="$2"
  local rid="$3"
  local target_framework="$4"

  "$DOTNET" pack "$project" \
    --configuration Release \
    --nologo \
    --verbosity minimal \
    --output "$OUT_DIR" \
    --source "$OUT_DIR" \
    -p:PackageVersion="$version" \
    -p:SharpCppNuGetStageRoot="$STAGE_DIR" \
    -p:SharpCppRuntimeIdentifier="$rid" \
    -p:SharpCppPackageTargetFramework="$target_framework"
}

create_packages() {
  local version=""
  local rid=""
  local configuration="Release"
  local configure_preset="dev"
  local sdk_root=""
  local sdk_asset=""
  local target_framework=""
  local skip_fetch=0
  local skip_build=0

  while [ $# -gt 0 ]; do
    case "$1" in
      --version|-v)
        version="$2"
        shift 2
        ;;
      --rid)
        rid="$2"
        shift 2
        ;;
      --configuration|-c)
        configuration="$2"
        shift 2
        ;;
      --configure-preset)
        configure_preset="$2"
        shift 2
        ;;
      --sdk-root)
        sdk_root="$2"
        shift 2
        ;;
      --sdk-asset)
        sdk_asset="$2"
        shift 2
        ;;
      --target-framework)
        target_framework="$2"
        shift 2
        ;;
      --skip-fetch)
        skip_fetch=1
        shift
        ;;
      --skip-build)
        skip_build=1
        shift
        ;;
      --help|-h)
        cat <<'EOF'
Usage: package.sh create --version VERSION [options]

Options:
  --version, -v VERSION      Package version. Suffix -alpha or -dev appends the git SHA.
  --rid RID                 Runtime package RID. Defaults to host RID.
  --configuration, -c NAME   Build configuration. Defaults to Release.
  --configure-preset NAME    Auxiliary CMake preset. Defaults to dev.
  --sdk-root PATH            Existing extracted rbfx SDK root.
  --sdk-asset NAME           Override rbfx SDK release asset name.
  --target-framework TFM     Override package target framework. Defaults to repo TargetFramework.
  --skip-fetch               Do not download/extract the SDK.
  --skip-build               Do not build package inputs.
EOF
        return 0
        ;;
      *)
        echo "Error: unknown create option '$1'." >&2
        return 1
        ;;
    esac
  done

  if [ -z "$version" ]; then
    echo "Error: --version is required." >&2
    return 1
  fi

  case "$version" in
    *-alpha|*-dev)
      version="$version-$(git -C "$REPO_ROOT" rev-parse --short HEAD)"
      ;;
  esac

  if [ -z "$rid" ]; then
    rid="$(default_rid)"
  fi

  if [ -z "$sdk_root" ]; then
    if [ -z "$sdk_asset" ]; then
      sdk_asset="$(sdk_asset_for_rid "$rid")"
    fi
    sdk_root="$IN_DIR/${sdk_asset%.7z}"
  fi

  if [ "$skip_fetch" -eq 0 ]; then
    local fetch_args=(--rid "$rid")
    if [ -n "$sdk_asset" ]; then
      fetch_args+=(--sdk-asset "$sdk_asset")
    fi
    fetch_sdk "${fetch_args[@]}" >/dev/null
  fi

  if [ ! -d "$sdk_root" ]; then
    echo "Error: SDK root does not exist: $sdk_root" >&2
    return 1
  fi

  normalize_sdk_cmake_exports "$sdk_root"

  if [ "$skip_build" -eq 0 ]; then
    "$DOTNET" build "$REPO_ROOT/rbfx/SharpCpp.Rbfx.Bindings/SharpCpp.Rbfx.Bindings.csproj" \
      --configuration "$configuration" \
      --nologo \
      -p:SharpCppNativePreset="$configure_preset" \
      -p:SharpCppRbfxSdkRoot="$sdk_root"
  fi

  if [ -z "$target_framework" ]; then
    target_framework="$(detect_target_framework)"
  fi

  echo "Staging packages for $version ($rid, $configuration, $target_framework)"
  stage_packages "$rid" "$configuration" "$sdk_root" "$target_framework"

  rm -rf "$OUT_DIR"
  mkdir -p "$OUT_DIR"

  pack_project "$SCRIPT_DIR/packages/SharpCpp.Rbfx.Bindings/SharpCpp.Rbfx.Bindings.Package.csproj" "$version" "$rid" "$target_framework"
  pack_project "$SCRIPT_DIR/packages/SharpCpp.Rbfx.Bindings.Runtime/SharpCpp.Rbfx.Bindings.Runtime.Package.csproj" "$version" "$rid" "$target_framework"

  if [ -d "$STAGE_DIR/data/CoreData" ]; then
    pack_project "$SCRIPT_DIR/packages/SharpCpp.Rbfx.CoreData/SharpCpp.Rbfx.CoreData.Package.csproj" "$version" "$rid" "$target_framework"
  fi
  if [ -d "$STAGE_DIR/data/Data" ]; then
    pack_project "$SCRIPT_DIR/packages/SharpCpp.Rbfx.Data/SharpCpp.Rbfx.Data.Package.csproj" "$version" "$rid" "$target_framework"
  fi
  if [ -d "$STAGE_DIR/data/EditorData" ]; then
    pack_project "$SCRIPT_DIR/packages/SharpCpp.Rbfx.EditorData/SharpCpp.Rbfx.EditorData.Package.csproj" "$version" "$rid" "$target_framework"
  fi

  echo "Packages created in $OUT_DIR"
}

create_local_repository() {
  rm -rf "$LOCAL_REPOSITORY_DIR"
  mkdir -p "$LOCAL_REPOSITORY_DIR"

  shopt -s nullglob
  local package
  for package in "$OUT_DIR"/*.nupkg; do
    "$NUGET" add "$package" -source "$LOCAL_REPOSITORY_DIR"
  done
  shopt -u nullglob
}

if [ $# -eq 0 ]; then
  usage
  exit 1
fi

action="$1"
shift

case "$action" in
  fetch-sdk)
    fetch_sdk "$@"
    ;;
  fetch-sdks)
    fetch_sdks "$@"
    ;;
  create)
    create_packages "$@"
    ;;
  local-repo|local-repository)
    create_local_repository "$@"
    ;;
  --help|-h|help)
    usage
    ;;
  *)
    echo "Error: unknown action '$action'." >&2
    usage
    exit 1
    ;;
esac
