# RedBolt Port Plan

RedBolt is planned as a first-class C# port of rbfx, using the original rbfx codebase as the behavioral reference and native dependency source. SharpCpp is the binding-generation foundation for native dependencies.

## Documents

| File | Purpose |
|---|---|
| `01-port-strategy.md` | Overall architecture, assumptions, repository layout, sequencing principles. |
| `02-single-cmake-build.md` | One-CMake build plan for C# projects, native dependencies, generated bindings, and samples. |
| `03-sharpcpp-dependency-bindings.md` | How RedBolt uses SharpCpp core to generate dependency bindings from clang-parsed headers. |
| `04-feature-parity-roadmap.md` | rbfx feature parity map and planned deferrals. |
| `05-first-cube-vertical-slice.md` | Minimal cross-cutting feature set required to show the first cube on screen. |
| `06-open-questions.md` | Decisions that should be confirmed before implementation hardens. |

## Immediate Goal

Get a desktop window rendering a lit or unlit cube as fast as possible while exercising the real engine seams: core object model, engine loop, graphics backend, resource cache, scene graph, and input. Anything not needed for that path is deferred, even if it exists in rbfx.

## Working Assumptions

| Area | Assumption |
|---|---|
| Port shape | RedBolt is a C# engine/runtime port, not only generated C# bindings over the rbfx C++ engine. |
| API compatibility | Public APIs should be near-1:1 with rbfx where possible and sensible, with deliberate C#-native substitutions for areas like logging and serialization. |
| Reference source | `/home/rk/Desktop/src/rbfx/rbfx` remains the behavioral reference and initial source for third-party native dependency builds. |
| Binding strategy | SharpCpp generates bindings from selected dependency headers only; no hand-authored dependency bindings are allowed. |
| Build strategy | One top-level CMake project builds native dependencies, generators, generated bindings/thunks, the main `RedBolt` library, optional modules, and samples. |
| Modularization | Namespaces such as `RedBolt.Graphics` do not imply separate assemblies. Required runtime code stays in `RedBolt`; optional features such as future `RedBolt.RmlUi` can be modules. |
| First cube | A procedural cube resource is acceptable for the first screen if it flows through the resource and scene APIs. rbfx model/material parsing can follow immediately after. |
| Runtime performance | Minimize GC pressure from day one, especially in frame update, input, scene traversal, resource lookup, and rendering paths. |
