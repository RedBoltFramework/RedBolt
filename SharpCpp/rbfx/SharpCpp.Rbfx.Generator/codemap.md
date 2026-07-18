# rbfx/SharpCpp.Rbfx.Generator/

## Responsibility

The rbfx bindings generator is the **phase-1 composition root** that instantiates SharpCpp's generic pipeline components (parsing, pipeline passes, rendering, metadata export) against the concrete rbfx SDK headers. It produces the complete managed projection of the rbfx engine API: C# bindings (`.g.cs`), C++ native glue (`.g.cpp`), and a binding metadata document (`Bindings.json`). It is structurally analogous to `SharpCpp.Tool` but specialized for rbfx rather than being a general-purpose CLI.

## Design Patterns

### Pipeline Composition

`Program.Main` orchestrates the binding pipeline as an explicit sequence of stages, each consuming and transforming a shared `BindingCompilation`:

```
Parse → BootstrapPipeline → ExportFilter → Render → MetadataExport
```

This is a **staged pipeline** pattern — each stage mutates the compilation model in-place, and intermediate diagnostics are logged. There is no scheduler or pass manager; the sequence is hardcoded for rbfx's needs.

### Toolchain Discovery Strategy

`CompilerToolchainDiscovery` implements a **probe-and-fallback** pattern for obtaining correct Clang invocation flags. Given a compiler path from the SDK flags file (which may be `g++`), it:

1. Checks for a `SHARPCPP_RBFX_CLANG_RESOURCE_DIRECTORY` environment override
2. Falls back to probing the compiler (or `clang++`/`clang` on PATH) with `-print-resource-dir`
3. Discovers the target triple via `-dumpmachine`
4. Extracts the system include search path from `-E -x c++ - -v` stderr parsing
5. Filters out Clang built-in include directories that belong to a different Clang major version than the in-process `ClangSharp` assembly
6. Validates that any discovered resource directory has a usable `include/` subdirectory

This ensures libclang sees a consistent set of built-in headers even when the SDK was built with a different toolchain.

### SDK Export Gating

`ApplySdkExportFilter` implements a **dynamic symbol-based filtering** strategy. It invokes `nm -D --defined-only --demangle` on the linked SDK shared library, then cross-references the demangled symbol list against free functions and member functions parsed from headers. Functions declared in headers but absent from the exported symbol table are marked as ignored. This prevents bindings from being emitted for functions that would fail to link at runtime, which is common in SDKs that use visibility attributes or selective symbol export.

### Namespace Flattening

`BuildNamespaceMappings` maps all `Urho3D::*` nested C++ namespaces to the single `Urho3D` C# namespace. This is a deliberate flattening: the managed projection uses a single flat namespace rather than mirroring rbfx's deep C++ namespace hierarchy.

### Output Grouping by Header

`DeriveGroupStem` maps each input header path to a structured output file naming scheme. Headers under `Urho3D/` are stripped of that prefix, and path segments become dotted suffix components. For example, `Urho3D/Core/Context.h` → `SharpCpp.Rbfx.Core.Context.g.cs`. This keeps generated files organized by their SDK subsystem origin.

## Data & Control Flow

### Startup Sequence

```
Program.Main(args)
  ├── GeneratorOptions.Parse(args)
  │     ├── Extracts --sdk-root, --flags-file, --generated-root, --project-root
  │     ├── Extracts optional --sdk-library, --log-file
  │     └── Validates all paths exist (or may-exist for generated-root)
  │
  ├── CreateLogWriter(options.LogFilePath)
  │     └── Opens StreamWriter for generator log
  │
  ├── SdkParseFlags.Load(options.FlagsFilePath)
  │     ├── Reads key=value lines from flags file
  │     ├── Separates compiler_path, language_standard, include_directory[*], define[*], clang_argument[*]
  │     ├── Filters architecture-specific defines (URHO3D_SSE, BT_USE_SSE, etc.)
  │     ├── Filters clang arguments that don't affect parsing (warning flags, arch tuning, SIMD codegen)
  │     └── Normalizes language standard to gnu++20 minimum
  │
  ├── BuildClangArguments(includeRoot, sdkFlags)
  │     ├── Sets -xc++, -std=
  │     ├── Adds -Wno-* warning suppression arguments
  │     ├── Collects -I directives from SDK flags + include root
  │     ├── Adds -D defines from SDK flags + compatibility defines (SDL_DISABLE_*, IMGUI_DISABLE_*)
  │     ├── Adds SDK additional clang arguments + SIMD disable flags (-mno-sse*, -mno-avx*)
  │     ├── CompilerToolchainDiscovery.Discover(sdkFlags.CompilerPath)
  │     │     ├── Resolves toolchain compiler (prefers clang if available)
  │     │     ├── Discovers target triple via compiler -dumpmachine
  │     │     ├── Discovers parser ClangSharp major version from loaded assembly
  │     │     ├── Discovers verbose include search paths via -E -v
  │     │     ├── Checks SHARPCPP_RBFX_CLANG_RESOURCE_DIRECTORY env var for override
  │     │     ├── Discovers clang resource directory via -print-resource-dir
  │     │     ├── Validates resource directory Clang version matches parser Clang version
  │     │     └── Filters built-in include dirs that don't belong to the matched resource dir
  │     └── Appends --target=, -resource-dir=, -isystem flags from toolchain info
  │
  ├── new ClangSharpHeaderParser().Parse(request)
  │     └── Returns BindingCompilation with parsed records, functions, enums, typedefs
  │
  ├── BuildNamespaceMappings(compilation)
  │     └── Creates flat mapping: all Urho3D::Sub::Name → Urho3D
  │
  ├── BindingCompilationBootstrapPipeline.Apply(compilation, marshallerConfigs)
  │     └── Runs pipeline passes (marshaller attribute resolution, type rewriting, etc.)
  │
  ├── ApplySdkExportFilter(compilation, sdkLibraryPath)
  │     ├── Runs nm -D --defined-only --demangle on SDK shared library
  │     ├── Cross-references parsed function signatures against exported symbols
  │     └── Marks unexported free functions and member functions as ignored
  │
  ├── BindingOutputRenderer.Render(compilation, stem, groupStemFunc)
  │     └── Returns RenderedOutput collection (files grouped by header origin)
  │
  ├── BindingMetadataExporter.Export(compilation, options)
  │     └── Returns BindingMetadataDocument for downstream import
  │
  ├── Write rendered files to options.GeneratedRoot
  ├── Write Bindings.json metadata to GeneratedRoot
  └── Log summary and return exit code
```

### Logging

Dual-output logging (console + file) via `Log()` helper. Errors and exceptions are written to both streams. Warning summaries for ignored records are emitted at the end of the run.

## Integration Points

### Consumes

| Dependency | Usage |
|---|---|
| `SharpCpp.Model` | `BindingCompilation`, `RecordBinding`, `FunctionBinding`, `EnumBinding`, `MetadataKeys` |
| `SharpCpp.Metadata` | `BindingMetadataExporter`, `BindingMetadataExportOptions`, `BindingMetadataSerializer` |
| `SharpCpp.Parsing` | `ClangSharpHeaderParser`, `ClangSharpParseRequest` |
| `SharpCpp.Pipeline` | `BindingCompilationBootstrapPipeline` |
| `SharpCpp.Rendering` | `BindingOutputRenderer`, `RenderedOutput` |
| `SharpCpp.Support` | (transitive dependency for output consistency) |
| rbfx SDK headers | Parsed input (via `Urho3DAll.h` umbrella header) |
| rbfx SDK shared library | `--sdk-library` for export symbol filtering (`.so`/`.dylib`/`.dll`) |
| `urho3d-sdk-flags.txt` | Compiler path, language standard, include dirs, defines, clang args |
| `include/SharpCpp/Marshaller.hpp` | Marshaller configuration consumed by the bootstrap pipeline |

### Produces

| Artifact | Location | Consumer |
|---|---|---|
| `SharpCpp.Rbfx.*.g.cs` | `RBFX_GENERATED_ROOT/` | `SharpCpp.Rbfx.Bindings.csproj` (wildcard Compile) |
| `SharpCpp.Rbfx.*.g.cpp` | `RBFX_GENERATED_ROOT/` | `SharpCppRbfx_Native` CMake target (Unity build) |
| `SharpCpp.Rbfx.Bindings.json` | `RBFX_GENERATED_ROOT/` | `Rbfx.UserCode.Generator` (`--import-binding-metadata`) |

### Entry Point

MSBuild builds the generator DLL through a `ProjectReference` before invoking the auxiliary CMake graph. CMake's `sharpcpp-generate-rbfx` custom command then runs that already-built DLL with positional options and records the generated-source stamp.

### Associated Configuration

- `ParseCompatibilityDefines`: SDL and ImGui header-compatibility macros that suppress platform-specific SIMD detection at parse time.
- `ParseSimdDisableArguments`: `-mno-sse*`, `-mno-avx*` flags that prevent libclang from implicitly enabling SIMD intrinsics based on host CPU detection.
- `ParseWarningDisableArguments`: `-Wno-deprecated-literal-operator` and `-Wno-pragma-once-outside-header` to suppress noisy libclang diagnostics.
- `EntryPointPrefix = "scrbfx"`: Unique prefix for native entry point symbols generated in the C++ glue.
