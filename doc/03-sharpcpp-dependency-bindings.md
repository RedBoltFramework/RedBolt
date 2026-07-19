# SharpCpp Dependency Bindings

## Goal

Generate RedBolt dependency bindings through SharpCpp only. SharpCpp parses selected native dependency headers with ClangSharp, applies the SharpCpp bootstrap pipeline, renders generated C# and generated C++ thunk code, and writes metadata under the CMake build tree.

There are no hand-authored dependency bindings in RedBolt. Do not add RedBolt-owned binding facade headers, C ABI forwarding functions, P/Invoke declarations, or C# wrapper binding classes for SDL, Diligent, or future native dependencies.

## Binding Policy

| Rule | Rationale |
|---|---|
| Generate from dependency headers only | The source of truth is the copied dependency header tree parsed by ClangSharp. |
| No manual interop code | Avoids drift between native declarations, generated thunks, and managed signatures. |
| Keep generated output in the build tree | Generated `.g.cs`, `.g.cpp`, and metadata files are reproducible artifacts, not source. |
| Keep public RedBolt APIs hand-authored | Engine APIs should be stable C# code above generated bindings, not direct dependency leaks. |
| Shape the surface in the generator | Header selection and pruning are generator configuration, not handwritten bindings. |
| Reuse SharpCpp core unchanged | Project-specific behavior belongs in RedBolt's generator and CMake inputs, not manual binding files. |

## Current SharpCpp Assets To Reuse

| Asset | Use in RedBolt |
|---|---|
| `SharpCpp.Model` | Binding AST, diagnostics, type model. |
| `SharpCpp.Metadata` | Metadata export/import for cross-module generation. |
| `SharpCpp.Parsing` | ClangSharp-backed header parsing. |
| `SharpCpp.Pipeline` | Bootstrap passes, validation, degradation. |
| `SharpCpp.Rendering` | C# and C++ thunk rendering. |
| `SharpCpp.Support` | Managed `NativeObject`, handle ownership, wrapper registry, UTF-8 helpers. |
| `include/SharpCpp` | Native marshaller support and P/Invoke export macros. |

## Generator Project

`generators/RedBolt.Dependencies.Generator` is a C# console project that references SharpCpp source projects and performs the standard SharpCpp flow.

```text
RedBolt.Dependencies.Generator
  references SharpCpp.Model
  references SharpCpp.Metadata
  references SharpCpp.Parsing
  references SharpCpp.Pipeline
  references SharpCpp.Rendering
  builds clang arguments from CMake include paths, defines, compiler, and platform
  parses generated umbrella headers that include copied dependency headers
  marks the copied dependency headers as owned declarations
  applies BindingCompilationBootstrapPipeline
  prunes unsupported declarations from the parsed dependency surface
  renders generated .g.cs and .g.cpp files with BindingOutputRenderer
  exports binding metadata with BindingMetadataExporter
```

## Generator Inputs

| Input | Source |
|---|---|
| SDL headers | All `SDL*.h` headers from `build/native/<preset>/deps/SDL/include` after the SDL build target prepares the build include directory. |
| Diligent headers | `extern/thirdparty/Diligent/Graphics/GraphicsEngine/interface/*.h` selected by the generator target. |
| Parse umbrella headers | Generated under `build/native/<preset>/generated/redbolt-native/...` by CMake. |
| Compile flags | CMake include directories, platform defines, compiler target, and system include discovery. |
| SharpCpp support headers | `extern/thirdparty/SharpCpp/include/SharpCpp`. |
| Output root | `build/native/<preset>/generated/redbolt-native`. |

## Current Generated Surfaces

| Surface | Parsed dependency headers | Generated output |
|---|---|---|
| SDL | All `SDL*.h` headers in the SDL build include directory | `RedBolt.Bindings.Sdl.*` C# and SDL thunk `.g.cpp`. |
| Diligent | Selected Diligent public engine and platform headers | `RedBolt.Bindings.Diligent.*` C# and native thunks. |
| Dear ImGui | `imgui.h` | `RedBolt.Bindings.DearImGui.*` C# and native thunks. |
| STB, LZ4, WebP | Copied dependency headers | Individual binding assemblies and native thunks. |
| glslang, SPIRV-Cross, SPIRV-Tools | Copied shader-toolchain headers | Individual binding assemblies and native thunks. |

The SDL surface is generated from the SDL build include directory and the SDL binding target depends on the SDL build target. The current Diligent surface validates enum/type generation from real Diligent headers. Expanding Diligent should happen by adding dependency headers or generator shaping rules, not by writing wrappers.

## Native Library Role

`RedBolt.Native` is not a hand-authored binding library. It compiles SharpCpp-generated C++ thunk files and links them to the copied native dependencies. If a generated surface has no required native exports, its generated C# may still be compiled into `RedBolt` without adding source files to `RedBolt.Native`.

## Public API Boundary

Generated bindings are an internal implementation detail. Hand-authored RedBolt engine code may create stable public C# APIs above the generated dependency bindings, but it must not define interop signatures or native forwarding functions manually.

Examples of allowed hand-authored code:

| Allowed | Reason |
|---|---|
| `RedBolt.Engine.ApplicationHost` calling generated SDL bindings | Engine behavior above the binding layer. |
| `RedBolt.Graphics.GraphicsDevice` owning generated Diligent handles | Public API and lifecycle policy above generated bindings. |
| Tests that call generated methods by name or reflection | Verification of generated artifacts, not a replacement binding layer. |

Examples of disallowed code:

| Disallowed | Reason |
|---|---|
| `RedBoltNative/Bindings/*.hpp` declaring wrapper functions | Hand-authored binding surface. |
| `extern "C" RedBolt_*` functions forwarding to SDL/Diligent | Manual ABI binding layer. |
| `[LibraryImport]` declarations written in source | Manual managed binding. |
| `NativeDependencySmoke` style C# wrappers over generated calls | Hand-authored binding wrapper instead of generated API use. |

## Success Criteria

| Check | Expected outcome |
|---|---|
| Generator build | RedBolt generator builds without requiring SharpCpp rbfx SDK targets. |
| Generator run | Dependency headers parse and generated files are written under build tree. |
| Native thunk build | Generated `.g.cpp` compiles and links to native dependencies when needed. |
| Managed build | Main `RedBolt` library compiles with generated C# and references `SharpCpp.Support`. |
| Runtime smoke | Managed smoke loads `RedBolt.Native` and invokes generated dependency bindings. |
