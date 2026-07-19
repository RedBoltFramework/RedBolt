# Port Strategy

## Goal

Create a C# port of rbfx with the same long-term feature set: desktop, mobile, web, multiple graphics backends, scene/resources/input/audio/UI/physics/networking/editor/tooling, and the code-first workflow described by rbfx.

The first milestone is intentionally much smaller: show a cube in a RedBolt window through C# engine code while building native dependencies and generated bindings from the same CMake project.

## Non-Goals For The First Milestone

| Deferred area | Reason |
|---|---|
| Full rbfx API parity | Too broad; first cube only needs a small vertical API. |
| Editor, player, samples browser | Not required to validate runtime architecture. |
| Physics, navigation, networking, XR, UI, audio | Independent subsystems that can land after graphics/scene/resources/input. |
| Full asset pipeline | Procedural or minimal file resources unblock first render faster. |
| Full shader translator and material graph | Hardcoded or minimal shader path is enough for first cube. |
| Runtime hot reload and file watcher | Useful later, not needed for first screen. |
| Mobile, web, UWP | Desktop first reduces CMake and backend complexity. |

## Architecture Direction

RedBolt should be a C# runtime with native dependencies behind SharpCpp-generated bindings. Binding code is generated from clang-parsed dependency headers only; RedBolt should not add hand-authored native facade headers, C ABI forwarding functions, P/Invoke declarations, or C# binding wrappers for dependency APIs.

Namespaces are not deployable module boundaries. Required engine systems should live in the main `RedBolt` managed library even when they use distinct namespaces like `RedBolt.Graphics`. Separate managed modules are reserved for optional or independently useful features such as `RedBolt.RmlUi`.

The public API should closely mirror rbfx where it makes sense: type names, component model, resource cache usage, scene graph concepts, graphics abstractions, event names, and sample structure should be recognizable to rbfx users. Deviations should be explicit and motivated by C# runtime realities. Known intentional deviations are C#-native logging and C#-native serialization instead of porting rbfx logging and serialization internals 1:1.

| Namespace or module | Packaging | Responsibility |
|---|---|---|
| `RedBolt.Core` | Main `RedBolt` library | Object identity, C#-native logging integration, events, type metadata, math, time, subsystem registry, diagnostics. |
| `RedBolt.Engine` | Main `RedBolt` library | Application host, engine parameters, lifecycle, frame loop, update ordering, subsystem startup/shutdown. |
| `RedBolt.Graphics` | Main `RedBolt` library | Graphics abstraction, backend selection, swapchain, render pipeline, camera, mesh/material/shader abstractions. Graphics is required, not an optional module, and should stay as close to rbfx graphics architecture as practical. |
| `RedBolt.Resources` | Main `RedBolt` library | File system abstraction, resource cache, resource loaders, resource lifetime, basic dependency tracking. |
| `RedBolt.Scene` | Main `RedBolt` library | Scene, node, component, transform hierarchy, update dispatch, renderable collection. |
| `RedBolt.Input` | Main `RedBolt` library | Window events, keyboard/mouse state, frame input snapshots, exit handling. |
| `RedBolt.Bindings.*` | Usually internal to owning library/module | Generated C# bindings over selected native dependency APIs parsed by SharpCpp. |
| `RedBolt.RmlUi` | Optional managed module, future | RmlUI integration when UI support is added. |
| `RedBolt.Native` | Native shared library | SharpCpp-generated native thunks linked to SDL, Diligent, and later other native dependencies. |
| `RedBolt.Generators.*` | Build-time tools | SharpCpp-based dependency binding generators. |

## Proposed Repository Layout

```text
RedBolt/
  CMakeLists.txt
  CMakePresets.json
  Directory.Build.props
  Directory.Packages.props
  RedBolt.slnx
  cmake/
    RedBoltSharpCpp.cmake
  extern/
    SDL/
    Diligent/
    ImGui/
    Compression/
    thirdparty/
      SharpCpp/                         # git submodule
  native/
    RedBolt.Native/
      CMakeLists.txt
      smoke/
  generators/
    RedBolt.Dependencies.Generator/
  src/
    RedBolt/
      Core/
      Engine/
      Graphics/
      Resources/
      Scene/
      Input/
  modules/
    RedBolt.RmlUi/                       # future optional module
  samples/
    RedBolt.Sample.FirstCube/
  tests/
    RedBolt.Tests.*
  doc/
```

## Design Principles

| Principle | Consequence |
|---|---|
| First-cube slice wins | Prefer narrow interfaces and explicit deferrals over broad framework scaffolding. |
| Namespace does not imply module | Keep required runtime code in the main `RedBolt` library; split only optional or independently useful features. |
| rbfx-shaped public API | Mirror rbfx public surface where practical; choose C#-native alternatives only when they are materially better for .NET. |
| C# owns engine behavior | Native code should be dependency code or SharpCpp-generated thunk code, not RedBolt-authored binding facades. |
| No manual bindings | Do not write hand-authored interop declarations, C ABI forwarding functions, or facade headers for dependency APIs. |
| Keep generated bindings behind public APIs | Public RedBolt APIs should be stable C# APIs, not direct dependency API leaks. |
| Build from source | Native dependencies come from the rbfx third-party tree and are built by the RedBolt CMake configure. |
| Generated output belongs in build tree | No generated C# or C++ should be committed unless explicitly needed for bootstrapping. |
| Low GC pressure from day one | Avoid per-frame allocations in update, input, scene traversal, resource lookup, and rendering. Use structs, spans, pooled buffers, cached delegates, and reusable collections where they matter. |
| Validate every vertical slice | Each port phase should produce a visible sample or focused test, not only libraries. |

## Phasing

| Phase | Name | Outcome |
|---|---|---|
| 0 | Bootstrap | CMake configures, SharpCpp core projects build, native dependency source paths resolve. |
| 1 | Native dependency slice | SDL and Diligent dependency sources/headers compile in one build. |
| 2 | Generated bindings | SharpCpp generator emits C# bindings and native thunks from dependency headers into the build tree. |
| 3 | Engine skeleton | C# application host runs a frame loop and processes window/input events. |
| 4 | First cube | Scene graph renders a procedural cube through resource, scene, camera, and graphics systems. |
| 5 | Asset loading | Replace procedural cube path with rbfx-compatible model/material/shader resource loading. |
| 6 | Subsystem expansion | Add UI, audio, physics, navigation, networking, tools, editor, and platform breadth. |

## Definition Of Done For First Cube

| Requirement | Expected result |
|---|---|
| Configure | The C# build graph configures native dependencies under `build/native/linux-gcc-x64-dll`. |
| Build | `dotnet build RedBolt.slnx --configuration Debug` builds native and managed outputs. |
| Run | FirstCube opens a desktop window and displays a cube. |
| Input | Escape or window close exits cleanly. |
| Architecture | Cube path touches `Core`, `Engine`, `Graphics`, `Resources`, `Scene`, and `Input`. |
| Diagnostics | Startup logs show selected graphics backend, resource roots, and frame-loop shutdown. |
