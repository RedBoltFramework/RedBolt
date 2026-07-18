# SharpCpp rbfx NuGet Packaging

This folder builds SDK-style NuGet packages with `dotnet pack`. It intentionally
does not use new `.nuspec` files.

## Package Set

- `SharpCpp.Rbfx.Bindings` contains the managed rbfx bindings, `SharpCpp.Support`,
  and the rbfx source generator analyzer.
- `SharpCpp.Rbfx.Bindings.runtime.<rid>` contains native runtime files for one
  runtime identifier, including `SharpCpp.Rbfx.Native` and the copied rbfx native
  dependencies from the shared `build/bin` output directory. It depends on
  `SharpCpp.Rbfx.Bindings`, so applications may reference the RID package as the
  all-in-one dependency.
- `SharpCpp.Rbfx.CoreData`, `SharpCpp.Rbfx.Data`, and
  `SharpCpp.Rbfx.EditorData` contain resource folders copied from the downloaded
  rbfx SDK archive when present.

## Create Packages

```bash
./packaging/nuget/package.sh create --version 0.1.0-alpha --rid linux-x64
```

The script downloads the matching prebuilt rbfx DLL SDK archive from
`https://github.com/rbfx/rbfx/releases/download/latest`, builds the binding
project (which drives the auxiliary native CMake target through MSBuild), stages
files, and runs `dotnet pack`.
Package target-framework paths are derived from the repository `TargetFramework`
property. Use `--target-framework TFM` only when explicitly overriding it.

Use `--skip-fetch` when `build/package/nuget/in/<sdk-name>/` already exists. Use
`--skip-build` to package an already-built `build/bin/<Configuration>` tree.
Package staging, package output, downloaded SDKs, and the local repository all
remain below `build/package/nuget/`.

## Runtime IDs

This pipeline packages one RID per invocation. Downloading SDK archives for other
platforms is not enough to produce their runtime packages from a Linux host: the
generated C++ glue library (`SharpCpp.Rbfx.Native`) must also be compiled and
linked for each target RID with a compatible compiler/toolchain and SDK binary
set. The generated managed assembly is portable, but the native glue is not.

To produce all runtime packages, run this same package step on CI runners for
each RID, or provide cross-compilation toolchain files that can build and link
`SharpCpp.Rbfx.Native` for those SDKs.

Known SDK archive mappings:

- `linux-x64`: `rebelfork-sdk-linux-gcc-x64-dll-latest.7z`
- `osx-x64`: `rebelfork-sdk-macos-clang-x64-dll-latest.7z`
- `osx-arm64`: `rebelfork-sdk-macos-clang-arm64-dll-latest.7z`
- `win-x64`: `rebelfork-sdk-windows-msvc-x64-dll-latest.7z`
- `uap-x64`: `rebelfork-sdk-uwp-msvc-x64-dll-latest.7z`

## Local Repository

```bash
./packaging/nuget/package.sh local-repo
```

This imports packages from `build/package/nuget/out/` into
`build/package/nuget/local-repository/` for local restore tests.
