# C++ Lifetime and Copy-Semantics Verification Inventory

Generated from refreshed jcodemunch indexes: 2026-07-22

## Status

This is the pending portion of the exhaustive **future inspection inventory** of the currently hand-authored managed port. Rows are removed once constructors, destruction, and copy/assignment have all been verified. The remaining rows are not claims that semantic parity has been verified and are not a list of accepted deviations. The rbfx C++ implementation remains the only authority.

The repository indexes were refreshed before generating this inventory. Candidate counterparts were resolved from `Source/Urho3D` and `Source/Samples` by matching relative path first and basename second. Every unresolved entry must be searched again by class name, namespace, callers, and related classes before inspection.

## Scope

Included:

- `src/RedBolt/**/*.cs`
- `src/RedBolt.DearImGui/**/*.cs`
- `samples/**/*.cs`

Excluded:

- Generated native bindings and generated/build-output files.
- Tests and vendored third-party code.
- Global aliases/usings files that contain no ported class implementation.

Pending managed files inventoried: **1**
Pending files with likely C++ counterpart paths: **1**
Pending files requiring counterpart discovery: **0**

## Required review for every row

### Constructors

- [ ] Open the complete C++ class declaration and record bases and every field in declaration order.
- [ ] Read all constructor declarations and complete bodies, including delegated constructors, base initialization, initializer lists, default member initializers, and relevant call sites.
- [ ] Verify C# preserves the observable C++ initialization order: bases first, then fields in declaration order, then constructor body.
- [ ] Verify every overload, default value, side effect, ownership decision, and copy/move performed during construction.
- [ ] Identify implicit/defaulted constructors that may not appear as out-of-line bodies.

### Destructors and automatic field destruction

- [ ] Read the complete C++ destructor declaration/body and teardown call path.
- [ ] Enumerate **every field whose destructor runs automatically in C++**, even when the containing destructor is empty, defaulted, or implicit.
- [ ] Classify each mapped field as value, borrowed alias, owned managed object, owned native wrapper, container of owned values, or non-owning pointer/reference.
- [ ] Verify the C# `Dispose()` performs destructor-body actions and explicitly disposes every owned/disposable mapped field at the corresponding lifetime boundary.
- [ ] Verify reverse member-destruction order, derived-before-base teardown, callback/event unregistration, container clearing, and native release behavior.
- [ ] Verify RAII locals and temporaries use deterministic C# scope and are neither retained too long nor disposed too early.

### Returns by copy and copy-producing expressions

- [ ] Read every complete C++ method body returning a class/struct/container/value alias by value.
- [ ] Inspect named local returns, conditional/comma expressions, aggregate/braced expressions, and getter results.
- [ ] Inspect pass-by-value parameters, local initialization, assignment, lambda captures, container insertion/emplacement, and algorithm callbacks.
- [ ] Read copy/move constructors and copy/move assignment operators of every involved type.
- [ ] At each C# call site, preserve independent value copies with generated copy constructors or `ICloneable.Clone()` only where C++ copies; preserve aliases where C++ uses pointer/reference semantics.
- [ ] For generated wrappers, distinguish borrowed native storage from owned copies before mutation, retention, assignment, return, or disposal.
- [ ] Do not assume C# struct assignment is sufficient when fields contain managed references or native handles.

## Inventory

Check a column only after completing the corresponding full C++ review and recording the outcome.

| Managed implementation file | Likely authoritative rbfx file(s) | Constructors | Destructors + fields | Copy returns/expressions |
|---|---|:---:|:---:|:---:|
| `src/RedBolt/RenderAPI/RenderDevice.cs` | `Source/Urho3D/RenderAPI/RenderDevice.h`<br>`Source/Urho3D/RenderAPI/RenderDevice.cpp` | ☐ | ☐ | ☐ |
| 2026-07-22 partial outcome | Corrected the native graphics construction boundary: headless initialization no longer creates `RenderDevice` or `PipelineStateCache`; graphical initialization registers `RenderDevice` only after the native device, immediate context, swap chain, and views exist; `RenderContext`, render pool, and default queue are therefore constructed against live native state; and pipeline-state-cache registration follows `RenderDevice::PostInitialize`, matching `Graphics::InitializePipelineStateCache`. `UpdateWindowSettings` now preserves the native selective assignment behavior instead of replacing immutable multisample and other settings, and `UpdateSwapChainSize` writes SDL's actual window size back before resizing the native swap chain. The complete `RenderContext` constructor, empty destructor plus automatic field destruction, method bodies, borrowed render-target state, and noncopyable semantics are now closed and its fully completed inventory row was removed. `RenderDevice` remains open because SDL window and Diligent handle ownership still resides in managed `Input`/`Graphics` rather than the native class shape. | — | — | — |
| 2026-07-22 partial outcome | Corrected native constructor-state parity at the managed graphics initialization boundary: `RenderDevice` now stores the actual requested `WindowSettings`, applies the native external-window transition and validation, then records SDL's effective window size, OpenGL multisampling, and sRGB capability after context creation. Swap-chain render-target views therefore report the effective multisample value from the same retained settings object as native. Focused settings/view tests pass, the complete Linux/Web solution builds with zero errors, all 1,754 tests pass, and MultipleViewports renders the expected populated main scene and inset viewport. Constructor/field ownership, complete method bodies, and reverse member-destruction closure remain open. | — | — | — |
| 2026-07-21 partial outcome | Corrected engine teardown ordering against native `Graphics::Close` and `RenderDevice::~RenderDevice`: managed shutdown now releases `RenderDevice` while the Graphics-owned immediate context is still live, so the destructor's `WaitForIdle` executes at the native lifetime boundary. Removed the managed-only `OnEngineShutdown` path that sent `DeviceObjectEvent.Destroy` before normal disposal and caused a second destroy dispatch later. The focused lifecycle suite confirms one destroy dispatch and pointer detachment; full tests and MultipleViewports teardown pass. Constructor/field ownership, complete method bodies, and reverse member-destruction closure remain open. | — | — | — |
| 2026-07-19 partial outcome | The complete native fullscreen-mode enumeration, sorting/deduplication, closest-mode selection, swap-chain-size/DPI getters, window-settings validation/update, and WindowSettingsDemo call paths were reviewed. `FullscreenMode` values remain independent POD structs, mode containers own independent values, and `GetClosestFullscreenModeIndex` now uses the approved managed `int` index mapping throughout its implementation, callers, and focused native-parity test. The complete `RenderDevice` declaration-wide constructor, destructor, fields, device-object ownership, and copy audit remains pending. | — | — | — |

## Outcome notation for future passes

Add one indented note below the relevant row or in a dated follow-up section:

- `VERIFIED`: complete relevant C++ declarations, bodies, fields, and call paths match.
- `DEVIATION`: exact C++ → C# difference, with involved symbols and required faithful port.
- `ALLOWED`: exact AGENTS.md allowed-deviation clause.
- `BLOCKED`: searches performed and the missing authoritative C++ source.
- `N/A`: no class/member in that category, confirmed from the complete C++ class declaration.

## Completion rule

A file is complete only when all ported types in it have been reviewed, including nested types and implicit/defaulted special members. A green build or test is not verification. If C# already matches the initially inspected C++ method but a symptom remains, continue through the authoritative C++ call path until the actual missing or divergent port is found.
