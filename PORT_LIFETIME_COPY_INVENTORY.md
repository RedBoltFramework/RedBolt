# C++ Lifetime and Copy-Semantics Verification Inventory

Generated from refreshed jcodemunch indexes: 2026-07-17

## Status

This is an exhaustive **future inspection inventory** of the currently hand-authored managed port. It is not a claim that semantic parity has been verified and not a list of accepted deviations. The rbfx C++ implementation remains the only authority.

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

Managed files inventoried: **334**  
Files with likely C++ counterpart paths: **277**  
Files requiring counterpart discovery: **57**

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
| `samples/RedBolt.Sample.AnimatingScene/Program.cs` | `Source/Samples/05_AnimatingScene/AnimatingScene.cpp`<br>`Source/Samples/05_AnimatingScene/AnimatingScene.h` | ☐ | ☐ | ☐ |
| `samples/RedBolt.Sample.AnimatingScene/Rotator.cs` | `Source/Samples/Rotator.cpp`<br>`Source/Samples/Rotator.h` | ☐ | ☐ | ☐ |
| `samples/RedBolt.Sample.DynamicGeometry/Program.cs` | `Source/Samples/34_DynamicGeometry/DynamicGeometry.cpp`<br>`Source/Samples/34_DynamicGeometry/DynamicGeometry.h` | ☐ | ☐ | ☐ |
| `samples/RedBolt.Sample.FirstCube/Program.cs` | **UNRESOLVED — search by class name** | ☐ | ☐ | ☐ |
| `samples/RedBolt.Sample.HelloGUI/Program.cs` | `Source/Samples/02_HelloGUI/HelloGUI.cpp`<br>`Source/Samples/02_HelloGUI/HelloGUI.h` | ☐ | ☐ | ☐ |
| `samples/RedBolt.Sample.HelloWorld/Program.cs` | `Source/Samples/01_HelloWorld/HelloWorld.cpp`<br>`Source/Samples/01_HelloWorld/HelloWorld.h` | ☐ | ☐ | ☐ |
| `samples/RedBolt.Sample.MultipleViewports/Program.cs` | `Source/Samples/09_MultipleViewports/MultipleViewports.cpp`<br>`Source/Samples/09_MultipleViewports/MultipleViewports.h` | ☐ | ☐ | ☐ |
| `samples/RedBolt.Sample.RenderToTexture/Program.cs` | `Source/Samples/10_RenderToTexture/RenderToTexture.cpp`<br>`Source/Samples/10_RenderToTexture/RenderToTexture.h` | ☐ | ☐ | ☐ |
| `samples/RedBolt.Sample.RenderToTexture/Rotator.cs` | `Source/Samples/Rotator.cpp`<br>`Source/Samples/Rotator.h` | ☐ | ☐ | ☐ |
| `samples/RedBolt.Sample.SoundEffects/Program.cs` | `Source/Samples/14_SoundEffects/SoundEffects.cpp`<br>`Source/Samples/14_SoundEffects/SoundEffects.h` | ☐ | ☐ | ☐ |
| `samples/RedBolt.Sample.SoundSynthesis/Program.cs` | `Source/Samples/29_SoundSynthesis/SoundSynthesis.cpp`<br>`Source/Samples/29_SoundSynthesis/SoundSynthesis.h` | ☐ | ☐ | ☐ |
| `samples/RedBolt.Sample.StaticScene/Program.cs` | `Source/Samples/04_StaticScene/StaticScene.cpp`<br>`Source/Samples/04_StaticScene/StaticScene.h` | ☐ | ☐ | ☐ |
| `samples/RedBolt.Sample.WindowSettings/Program.cs` | `Source/Samples/54_WindowSettingsDemo/WindowSettingsDemo.cpp`<br>`Source/Samples/54_WindowSettingsDemo/WindowSettingsDemo.h` | ☐ | ☐ | ☐ |
| `src/RedBolt.DearImGui/DearImGui.cs` | `Source/Urho3D/SystemUI/SystemUI.cpp`<br>`Source/Urho3D/SystemUI/SystemUI.h` | ☐ | ☐ | ☐ |
| `src/RedBolt.DearImGui/DearImGuiEvents.cs` | **UNRESOLVED — search by class name** | ☐ | ☐ | ☐ |
| `src/RedBolt.DearImGui/ImGuiDiligentRendererEx.cs` | `Source/Urho3D/SystemUI/ImGuiDiligentRendererEx.cpp`<br>`Source/Urho3D/SystemUI/ImGuiDiligentRendererEx.h` | ☐ | ☐ | ☐ |
| `src/RedBolt/Audio/Audio.cs` | `Source/Urho3D/Audio/Audio.h`<br>`Source/Urho3D/Audio/Audio.cpp` | ☑ | ☑ | ☑ |
| 2026-07-17 outcome | VERIFIED after fixing the extra stored context alias and the incorrect `interpolation_` construction default. Destructor order is `Release()`, SDL release, then base teardown; C++ weak microphone/listener fields are non-owning under the allowed smart-pointer removal. `EnumerateMicrophones` returns an independent collection and `CreateMicrophone` returns the same object reference under the allowed `SharedPtr` removal. | — | — | — |
| `src/RedBolt/Audio/AudioDefs.cs` | `Source/Urho3D/Audio/AudioDefs.h` | ☑ | ☑ | ☑ |
| 2026-07-17 outcome | VERIFIED. This file contains only string constants and a value enum; it has no constructors, destructors, owned fields, or object-copy expressions to port. | — | — | — |
| `src/RedBolt/Audio/AudioEvents.cs` | `Source/Urho3D/Audio/AudioEvents.h` | ☑ | ☑ | ☑ |
| 2026-07-17 outcome | VERIFIED. The allowed struct-event mapping contains only borrowed object references and scalar values; default value construction, destruction, and copying require no cloning or disposal. | — | — | — |
| `src/RedBolt/Audio/BufferedSoundStream.cs` | `Source/Urho3D/Audio/BufferedSoundStream.h`<br>`Source/Urho3D/Audio/BufferedSoundStream.cpp` | ☑ | ☑ | ☑ |
| 2026-07-17 outcome | VERIFIED after fixing both shared-array overloads to retain shared input storage instead of deep-copying it. The default destructor owns only managed standard-container mappings and requires no element disposal. | — | — | — |
| `src/RedBolt/Audio/Microphone.cs` | `Source/Urho3D/Audio/Microphone.h`<br>`Source/Urho3D/Audio/Microphone.cpp` | ☑ | ☑ | ☑ |
| 2026-07-17 outcome | VERIFIED after restoring live `GetData` storage, independent `CopyData`, device-aware `IsEnabled`, and the C++ lock/zero-length update behavior. `Dispose` closes the SDL device before base teardown; the linked stream follows the allowed `SharedPtr` removal. | — | — | — |
| `src/RedBolt/Audio/OggVorbisSoundStream.cs` | `Source/Urho3D/Audio/OggVorbisSoundStream.h`<br>`Source/Urho3D/Audio/OggVorbisSoundStream.cpp` | ☑ | ☑ | ☑ |
| 2026-07-17 outcome | VERIFIED after retaining the compressed-array pin for the decoder lifetime and releasing it after decoder close in `Dispose`. The compressed data remains shared with `Sound`, matching `ea::shared_array` under the allowed smart-pointer removal. | — | — | — |
| `src/RedBolt/Audio/Sound.cs` | `Source/Urho3D/Audio/Sound.h`<br>`Source/Urho3D/Audio/Sound.cpp` | ☑ | ☑ | ☑ |
| 2026-07-17 outcome | VERIFIED after removing a C#-only missing-subsystem/exception fallback from parameter loading. The sound-data getter preserves shared-array aliasing, decoder-stream creation returns a new stream object, copied XML element values use their managed value mapping, and the default destructor owns no disposable native field. | — | — | — |
| `src/RedBolt/Audio/SoundListener.cs` | `Source/Urho3D/Audio/SoundListener.h`<br>`Source/Urho3D/Audio/SoundListener.cpp` | ☑ | ☑ | ☑ |
| 2026-07-17 outcome | VERIFIED. Construction delegates to `Component`; the default destructor has no fields; there are no value-copy expressions in this class. Reflection metadata uses the allowed C# reflection facade. | — | — | — |
| `src/RedBolt/Audio/SoundSource.cs` | `Source/Urho3D/Audio/SoundSource.h`<br>`Source/Urho3D/Audio/SoundSource.cpp` | ☑ | ☑ | ☑ |
| 2026-07-17 outcome | VERIFIED under the user-approved non-null `GetSubsystem<T>()` deviation recorded in `AGENTS.md`, after removing the C#-only empty-resource shortcut in `SetSoundAttr`. C++ smart-pointer copies intentionally retain aliases and follow the allowed smart-pointer removal; returned strings are immutable, and `GetSoundAttr` returns the independent `ResourceRef` record-struct value constructed by C++. | — | — | — |
| `src/RedBolt/Audio/SoundSource3D.cs` | `Source/Urho3D/Audio/SoundSource3D.h`<br>`Source/Urho3D/Audio/SoundSource3D.cpp` | ☑ | ☑ | ☑ |
| 2026-07-17 outcome | VERIFIED after restoring the rbfx getter name `RollAngleoffFactor`. Constructor defaults and copied math temporaries use independent C# value types; the class adds no owned disposable fields. | — | — | — |
| `src/RedBolt/Audio/SoundStream.cs` | `Source/Urho3D/Audio/SoundStream.h`<br>`Source/Urho3D/Audio/SoundStream.cpp` | ☑ | ☑ | ☑ |
| 2026-07-17 outcome | VERIFIED. Constructor defaults match, the default destructor owns no disposable fields after the allowed `RefCounted` removal, and all returns are scalar values. | — | — | — |
| `src/RedBolt/Container/Array2D.cs` | `Source/Urho3D/Container/Array2D.h` | ☑ | ☑ | ☑ |
| 2026-07-17 outcome | VERIFIED after adding `Clone()` for both implicit C++ copy constructors, cloning value-like elements during container copies/fills, deterministically disposing cloned value elements when C++ clears or destroys its vector, and restoring `GetOptional` as a nullable reference to stored data instead of a value copy. | — | — | — |
| `src/RedBolt/Container/CString.cs` | **ALLOWED C# ADDITION — `AGENTS.md` section 10**<br>Related rbfx C-string utilities: `Source/Urho3D/Container/Str.h`<br>`Source/Urho3D/Container/Str.cpp` | ☑ | ☑ | ☑ |
| 2026-07-17 outcome | VERIFIED after adding a byte-for-byte independent `Clone()` for the mutable string value and disposing the temporary unmanaged buffer when static `Format` exits by exception. `Dispose` frees heap storage and resets inline storage; successful `Format` returns a distinct owned object. | — | — | — |
| `src/RedBolt/Container/ConstString.cs` | `Source/Urho3D/Container/ConstString.h` | ☑ | ☑ | ☑ |
| 2026-07-17 outcome | VERIFIED after mapping the implicit C++ copy constructor to an independent `Clone()` wrapper. The owned C++ string maps to immutable managed `string`, the cached hash is a value type, and neither field requires deterministic disposal. | — | — | — |
| `src/RedBolt/Container/IndexAllocator.cs` | `Source/Urho3D/Container/IndexAllocator.h` | ☑ | ☑ | ☑ |
| 2026-07-17 outcome | VERIFIED. `IndexAllocator` is non-copyable in C++ because of its mutex/atomic fields; its vector contains only scalar indices. `IDFamily` construction and `Dispose` acquire/release the ID exactly once, and all returned values are scalars. Standard lock primitives use the allowed managed threading mapping. | — | — | — |
| `src/RedBolt/Container/KeyFrameSet.cs` | `Source/Urho3D/Container/KeyFrameSet.h` | ☑ | ☐ | ☐ |
| 2026-07-17 partial outcome | Constructor shape and scalar returns VERIFIED; `GetKeyFrame` was fixed to return a nullable reference to stored data instead of a value copy. Element destruction and implicit container copies remain dependent on the `Variant`/derived-track lifetime audits and must be revisited with those rows. | — | — | — |
| `src/RedBolt/Container/ManagedSequence.cs` | **ALLOWED C# ADDITION — allocation-free managed sequence comparison** | ☑ | ☑ | ☑ |
| 2026-07-17 outcome | VERIFIED. This stateless helper is an allocation-free indexed replacement for the standard managed `Enumerable.SequenceEqual` operation under the allowed standard-container and render-hot-path deviations. It has no constructors, owned fields, destructors, or object-copying expressions. | — | — | — |
| `src/RedBolt/Container/MultiVector.cs` | `Source/Urho3D/Container/MultiVector.h` | ☑ | ☑ | ☑ |
| 2026-07-17 outcome | VERIFIED after preserving inner-vector identity in `Clear`, restoring reference returns from `EmplaceBack` and the indexer, mapping the implicit copy constructor to `Clone()`, cloning explicitly value-like elements in `PushBack` and `CopyTo`, and deterministically disposing owned cloneable values on pop, clear, shrink, replacement, and container disposal. Pointer-like elements remain aliases. `PipelineBatch` now supplies its implicit memberwise value clone, and the nested `PipelineBatchDesc` in delayed-shadow pairs is explicitly copied at its C++ pair-copy call site. | — | — | — |
| `src/RedBolt/Container/StringHash.cs` | `Source/Tests/Math/StringHash.cpp`<br>`Source/Urho3D/Math/StringHash.cpp`<br>`Source/Urho3D/Math/StringHash.h` | ☑ | ☑ | ☑ |
| 2026-07-17 outcome | VERIFIED after restoring `MutableValue` as a reference to the stored scalar and removing the C#-only setter. Every constructor initializes the sole `uint` field in C++ order; the value type has no destructible fields, and implicit copies and returned values are independent scalar copies. | — | — | — |
| `src/RedBolt/Core/Attribute.cs` | `Source/Urho3D/Core/Attribute.h` | ☑ | ☐ | ☐ |
| 2026-07-17 partial outcome | Constructor ordering and all non-`Variant` fields VERIFIED: name/hash scalars and immutable strings copy independently, enum names and metadata containers are recreated by `Clone()`, and the accessor intentionally remains a shared alias under the allowed smart-pointer removal. Default-value and metadata-element destruction, copy construction/assignment, and `GetMetadata` return semantics remain dependent on the `Variant` audit and must be revisited with that row. | — | — | — |
| `src/RedBolt/Core/CommandLineParser.cs` | **ALLOWED C# REPLACEMENT FOR CLI11 — `AGENTS.md` section 10**<br>`Source/Urho3D/Engine/Application.cpp`<br>`Source/Urho3D/Engine/Application.h`<br>`Source/Urho3D/Engine/Engine.cpp`<br>`Source/Urho3D/Engine/Engine.h` | ☑ | ☑ | ☑ |
| 2026-07-17 outcome | VERIFIED. Parser and option construction independently own their managed strings and alias arrays; their delegate closures have no deterministic teardown behavior corresponding to CLI11 callbacks. The parser is not copied, and parse-result record structs contain only scalar flags and an immutable string. `GetUsage` and failure creation return new immutable strings. | — | — | — |
| `src/RedBolt/Core/Context.cs` | `Source/Urho3D/Core/Context.h`<br>`Source/Urho3D/Core/Context.cpp` | ☑ | ☐ | ☐ |
| 2026-07-17 partial outcome | Constructor and non-`Variant` teardown VERIFIED after porting deterministic context disposal: `ResourceCache` is cleared first, SDL/render subsystems are removed and disposed in rbfx order (with excluded UI omitted and `SystemUI` mapped to `DearImGui`), remaining context-owned subsystems are disposed after cache detachment, and `PluginManager` is retained until last. The C++ engine subsystem registration boundary now also registers `StateManager`. Global-variable element destruction and `GetGlobalVar`/`SetGlobalVar` copy semantics remain dependent on the `Variant` audit. | — | — | — |
| `src/RedBolt/Core/CoreEvents.cs` | `Source/Urho3D/Core/CoreEvents.h` | ☑ | ☑ | ☑ |
| 2026-07-17 outcome | VERIFIED under the allowed struct-event mapping. All event payloads are default-constructed value structs containing only `uint`/`float` scalars (or no fields), require no deterministic destruction, and copy independently by value during dispatch. | — | — | — |
| `src/RedBolt/Core/CustomVariantValue.cs` | `Source/Urho3D/Core/Variant.h`<br>`Source/Urho3D/Core/Variant.cpp` | ☑ | ☑ | ☑ |
| 2026-07-17 outcome | VERIFIED after cloning explicitly value-like payloads during construction and `Clone()`, restoring mutable `TypedValue` access by reference, and deterministically disposing owned cloneable/disposable payloads. Non-cloneable references retain pointer-like aliases. Invocation by `Variant` assignment, clearing, and destruction remains tracked by the `Variant` row. | — | — | — |
| `src/RedBolt/Core/EventHandler.cs` | `Source/Urho3D/Core/Object.h` (`EventHandler`) | ☑ | ☑ | ☑ |
| 2026-07-17 outcome | VERIFIED under the allowed struct-event mapping. Construction preserves borrowed receiver/sender aliases and transfers the managed callable into the subscription; scalar/immutable IDs and event types copy independently. The implicit C++ callable destruction maps to releasing the managed delegate reference when its subscription is removed; the delegate target is borrowed and is not owned or disposed by the handler. No EventHandler copy construction or by-value return occurs. | — | — | — |
| `src/RedBolt/Core/Object.cs` | `Source/Urho3D/Core/Object.h`<br>`Source/Urho3D/Core/Object.cpp` | ☑ | ☑ | ☐ |
| 2026-07-17 partial outcome | Constructor/default state and destructor behavior VERIFIED within the required global-`Context` mapping: `BlockEvents` begins false, and `Dispose()` unsubscribes the receiver before removing it as an event sender. Subsystem caches, event sender/handler, and global maps remain borrowed aliases; strings/types/hashes/flags return immutable or scalar values. `GetGlobalVar` and `SetGlobalVar` remain dependent on the `Variant` copy/assignment audit. | — | — | — |
| `src/RedBolt/Core/ObjectCategory.cs` | `Source/Urho3D/Core/ObjectCategory.h` | ☑ | ☑ | ☑ |
| 2026-07-17 outcome | VERIFIED. This constants-only mapping has no instances or destructor behavior; every C++ `ConstString` global maps to an equal immutable C# string constant, so accesses require no mutable object copy or ownership transfer. | — | — | — |
| `src/RedBolt/Core/ObjectExtensionMethods.cs` | Allowed C# reflection replacement for `URHO3D_OBJECT` metadata (`Source/Urho3D/Core/Object.h`) | ☑ | ☑ | ☑ |
| 2026-07-17 outcome | VERIFIED under the allowed reflection/type-system facade. The class is static and has no construction or destruction state; methods return newly formatted immutable strings or a scalar `StringHash`, with no mutable object copy or ownership transfer. | — | — | — |
| `src/RedBolt/Core/ObjectReflection.cs` | `Source/Urho3D/Core/ObjectReflection.h`<br>`Source/Urho3D/Core/ObjectReflection.cpp` | ☑ | ☐ | ☐ |
| 2026-07-17 partial outcome | Constructor/type/factory setup VERIFIED under the allowed C# reflection facade. `AddAttribute` and `CopyAttributesFrom` already clone C++ by-value `AttributeInfo` entries, while returned attribute/reflection collections preserve borrowed aliases. Deterministic destruction on attribute/metadata removal and deep copy/assignment of attribute defaults and metadata remain dependent on the `AttributeInfo` and `Variant` audits. | — | — | — |
| `src/RedBolt/Core/ObjectRevisionTracker.cs` | `Source/Urho3D/Core/ObjectRevisionTracker.h` | ☑ | ☑ | ☑ |
| 2026-07-17 outcome | VERIFIED. Implicit construction initializes revision to 1, destruction is trivial, and revision updates preserve the nonzero sentinel across unsigned overflow. `GetRevision` returns only a scalar `uint`; no C++ tracker copy-construction call sites exist in the currently ported code. | — | — | — |
| `src/RedBolt/Core/ProcessUtils.cs` | `Source/Urho3D/Core/ProcessUtils.h`<br>`Source/Urho3D/Core/ProcessUtils.cpp` | ☑ | ☑ | ☑ |
| 2026-07-17 outcome | VERIFIED under the allowed C# standard-library mapping. The static utility has no instance lifetime; its persistent argument list is returned as a borrowed read-only view like the C++ const reference, while string/platform/scalar results are independent immutable values. Native `CString` temporaries and the spawned `Process` wrapper use deterministic `using` scopes. | — | — | — |
| `src/RedBolt/Core/Profiler.cs` | `Source/Urho3D/Core/Profiler.h`<br>`Source/Urho3D/Core/Profiler.cpp` | ☑ | ☑ | ☑ |
| 2026-07-17 outcome | VERIFIED. The managed profiler facade is static; color constants are scalars and thread naming retains only an immutable managed string. C++ zone RAII remains compile-time Tracy instrumentation rather than a ported runtime class, and this file has no constructed, destructed, or returned mutable object values. | — | — | — |
| `src/RedBolt/Core/Spline.cs` | `Source/Urho3D/Core/Spline.h`<br>`Source/Urho3D/Core/Spline.cpp` | ☐ | ☐ | ☐ |
| 2026-07-17 partial outcome | Corrected vector-level value semantics: knot-vector construction and `SetKnots` now create independent containers, `Clone()` performs one independent container copy, and `GetKnots` exposes the C++ const-reference result read-only. Element construction/assignment/return and deterministic destruction of knots and scratch vectors remain dependent on the `Variant` audit. | — | — | — |
| `src/RedBolt/Core/StopToken.cs` | `Source/Urho3D/Core/StopToken.h` | ☑ | ☑ | ☑ |
| 2026-07-17 outcome | VERIFIED under the allowed C# threading-primitives mapping. Construction creates one shared stop state, value copies share that state exactly like the C++ `shared_ptr<atomic<bool>>`, and `Stop`/`IsStopped` use volatile access. Managed shared-state reclamation needs no manual destructor, and the only return is a scalar bool. | — | — | — |
| `src/RedBolt/Core/StringHashRegister.cs` | `Source/Urho3D/Core/StringHashRegister.h`<br>`Source/Urho3D/Core/StringHashRegister.cpp` | ☑ | ☑ | ☑ |
| 2026-07-17 outcome | VERIFIED under the allowed C# threading/container mapping. Construction establishes optional locking behavior, lock scopes are deterministic, and the built-in lock object requires no manual destructor. Registered immutable strings have independent value semantics; unsafe getters preserve borrowed views, while copied strings and returned hashes/bools are immutable or scalar values. | — | — | — |
| `src/RedBolt/Core/SubsystemCache.cs` | `Source/Urho3D/Core/SubsystemCache.h`<br>`Source/Urho3D/Core/SubsystemCache.cpp` | ☑ | ☑ | ☑ |
| 2026-07-17 outcome | VERIFIED under the allowed removal of `SharedPtr`: cached entries are borrowed aliases and deterministic subsystem ownership/destruction is handled by `Context`. Construction initializes cache type hashes, and `Clear` detaches both collections before owner-driven teardown. `Get` returns borrowed aliases and `GetContainer` now exposes the C++ const-reference container as a borrowed read-only dictionary view. | — | — | — |
| `src/RedBolt/Core/ThreadSafeCache.cs` | `Source/Urho3D/Core/ThreadSafeCache.h` | ☑ | ☑ | ☑ |
| 2026-07-17 outcome | VERIFIED for all currently ported instantiations under the allowed C# threading-primitives mapping. Cached values are structs with memberwise C++-equivalent copies; `ReflectionProbeData` intentionally aliases its borrowed texture reference. Built-in lock/atomic fields need no manual destructor, and outward call paths consume independent struct snapshots without owned disposable state. | — | — | — |
| `src/RedBolt/Core/Time.cs` | `Source/Urho3D/Core/Timer.h` (`Time`)<br>`Source/Urho3D/Core/Timer.cpp` (`Time`) | ☑ | ☑ | ☑ |
| 2026-07-17 outcome | VERIFIED. Implicit managed construction preserves zeroed frame/timestep/period state and runtime timer support; `Dispose()` resets the timer period before base event teardown, matching the C++ destructor. Frame events use the allowed struct-event mapping, and all returns are scalars or independent immutable timestamp strings. | — | — | — |
| `src/RedBolt/Core/Timer.cs` | `Source/Urho3D/Core/Timer.h`<br>`Source/Urho3D/Core/Timer.cpp` | ☑ | ☑ | ☑ |
| 2026-07-17 outcome | FIXED and VERIFIED for `Timer`, `HiresTimer`, and `TimedCounter`. `TimedCounter` now stores the interval length as the C++ `int` field, including the unsigned-to-signed constructor conversion and unsigned comparison semantics. Constructors otherwise capture the matching initial clocks and initialize counter storage/state in C++ order; destruction is trivial beyond C# standard scalar/array storage. `TimedCounter.GetData` preserves the C++ const-reference result as a borrowed read-only view, all other returns are scalars, and current C++ call paths use timer objects only as members or locals without copy construction. | — | — | — |
| `src/RedBolt/Core/VariantCurve.cs` | `Source/Urho3D/Core/VariantCurve.h`<br>`Source/Urho3D/Core/VariantCurve.cpp` | ☑ | ☑ | ☑ |
| 2026-07-17 outcome | FIXED and VERIFIED. `VariantCurve.Clone()` now reproduces the implicit C++ copy constructor with independent keyframe values and tangent vectors; `Variant` construction stores an independent curve, while `GetVariantCurve` remains the C++ borrowed const-reference view and generic `Get<VariantCurve>` returns a new copy. `Sample` clones the non-interpolated keyframe value returned by copy. `Dispose()` tears down out-tangents, in-tangents, then base keyframes in reverse C++ field order and disposes every owned `Variant`. | — | — | — |
| `src/RedBolt/Core/VariantType.cs` | `Source/Urho3D/Core/Variant.h`<br>`Source/Urho3D/Core/Variant.cpp` | ☑ | ☑ | ☐ |
| 2026-07-17 partial outcome | Corrected the value-copy dependencies exposed by `VariantCurve`: `Variant` now clones buffer, `CString`, resource-list, variant/string-vector, variant/string-map, custom, and curve payloads at copy boundaries; container constructors deep-copy nested variants; generic value-returning getters, `GetObject`, same-type `Convert`, unsupported-type `Lerp`, and `FromObject(Variant)` return independent copies while direct const-reference getters remain borrowed; active owned payloads have deterministic `Dispose()` teardown, including recursive destruction in `VariantVector`, `VariantMap`, and `StringVariantMap`; and `Clear`, `SetBuffer`, `SetCustom`, and `FromString` destroy the old active payload before assignment as C++ `SetType` does. The complete assignment/call-site audit remains open because ordinary C# struct assignment cannot invoke the C++ copy constructor automatically. | — | — | — |
| `src/RedBolt/Core/WorkQueue.cs` | `Source/Urho3D/Core/WorkQueue.h`<br>`Source/Urho3D/Core/WorkQueue.cpp` | ☑ | ☑ | ☑ |
| 2026-07-17 outcome | FIXED and VERIFIED under the allowed thread-pool rebuild. Construction now matches the C++ constructor and relies on `Engine::RunFrame`/`Engine.RunFrame` for `Update` instead of adding a C#-only frame-event subscription. `Dispose()` waits for already-running managed pool tasks, resets the global queue/thread-index state in the C++ destructor body position, releases pending task collections in reverse field order, then performs base event teardown. Deprecated `GetFreeItem` returns a new item and `AddWorkItem` returns that same item alias like the C++ `SharedPtr` path; all other returns are scalars, indices, or borrowed queue references. | — | — | — |
| `src/RedBolt/Engine/Application.cs` | `Source/Urho3D/Engine/Application.h`<br>`Source/Urho3D/Engine/Application.cpp` | ☑ | ☑ | ☑ |
| 2026-07-17 outcome | FIXED and VERIFIED under the allowed global `Context`, struct-event, and `CommandLineParser` mappings. Construction creates the owned `Engine` before subscribing to log events. Engine/log shutdown was removed from `Run` because C++ keeps the engine field alive after the loop; `Dispose()` now destroys the owned engine before base event teardown, matching reverse field/base destruction. Command-line and run-loop temporaries are independent managed values, error strings are immutable copies, and all outward returns are scalars or borrowed subsystem/parser surfaces. Application owners must use deterministic disposal; test owners were corrected here and sample owners remain tracked by their individual inventory rows. | — | — | — |
| `src/RedBolt/Engine/ApplicationFlavor.cs` | `Source/Urho3D/Engine/ApplicationFlavor.h`<br>`Source/Urho3D/Engine/ApplicationFlavor.cpp` | ☑ | ☑ | ☑ |
| 2026-07-17 outcome | FIXED and VERIFIED. Flavor-map construction, the public map/initializer-list constructor mappings, `ApplicationFlavorPattern.Clone()`, and `ApplicationFlavor.Clone()` now deep-copy both the dictionary and nested tag sets, reproducing C++ container value semantics. Static const flavors are exposed as independent copies so their nested managed containers cannot mutate global constants. Their only destructed fields are standard managed string containers with no disposable elements, and `Matches`/`ToString` return scalar/nullable or immutable string values. Current engine/config call paths pass flavors read-only; mutating copies must use `Clone()` at future assignment sites. | — | — | — |
| `src/RedBolt/Engine/ConfigFile.cs` | `Source/Urho3D/Engine/ConfigFile.h`<br>`Source/Urho3D/Engine/ConfigFile.cpp` | ☑ | ☑ | ☑ |
| 2026-07-17 outcome | FIXED and VERIFIED. `ConfigVariableDefinition` and `ConfigFlavor` now provide explicit deep-copy operations for their owned `Variant`, flavor-map, and `StringVariantMap` fields. `ConfigFile.Dispose()` destroys current variables, definitions, flavor entries, then the base object in reverse C++ field/base order; reload and archive-input replacement paths likewise dispose old flavor/map contents before clearing, and JSON/map/value temporaries have deterministic scopes. Variable/default/flavor assignment clones C++ values and disposes overwritten values. `GetFlavorVariables` and `GetChangedVariables` return independently owned deep maps, preserve C++ stable penalty ordering without copying flavor owners, and all current call sites dispose them. `GetVariable` preserves the C++ borrowed const-reference result as `ref readonly` storage from the current/default maps. Missing-subsystem guards were omitted under the agreed non-null `GetSubsystem<T>()` deviation. | — | — | — |
| `src/RedBolt/Engine/Engine.cs` | `Source/Urho3D/Engine/Engine.h`<br>`Source/Urho3D/Engine/Engine.cpp` | ☐ | ☐ | ☐ |
| 2026-07-17 partial outcome | Corrected the constructor boundary exposed by the non-null subsystem contract: the managed `Engine` now creates its context, registers the constructor-time subsystems, and populates default configuration during construction like C++, rather than deferring those actions until `Initialize`. Under the explicitly approved staged-resource deviation, construction also sets the working directory to the directory containing the RedBolt assembly; relative resource and output paths use that same directory instead of the host process base directory. Shutdown now releases the owned `ConfigFile` and context even when initialization never ran, matching destruction after command-line early exits. `PopulateDefaultParameters` now gives command-line priority to the same log-rotation definitions as C++ and no longer gives it to `ConfigName`. The complete `Engine` constructor/member-order, destructor-field, and value-return audit remains open. | — | — | — |
| `src/RedBolt/Engine/EngineCommandLine.cs` | `Source/Urho3D/Engine/Engine.cpp` (`Engine::DefineParameters`) | ☑ | ☑ | ☑ |
| 2026-07-17 outcome | FIXED and VERIFIED under the allowed `CommandLineParser` mapping. The managed static registration/parsing class has no constructed instance or destructed fields. Callback captures are borrowed parser/parameter references whose lifetime is bounded by `Application.Run`; parser inputs and returned strings are immutable managed values, while numeric/enum parsers return scalars. The missing C++ log-rotation options were restored, and `--windowed` now changes only the borderless flag like C++ rather than also clearing fullscreen. | — | — | — |
| `src/RedBolt/Engine/EngineEvents.cs` | `Source/Urho3D/Engine/EngineEvents.h` | ☑ | ☑ | ☑ |
| 2026-07-17 outcome | FIXED and VERIFIED under the allowed struct-event mapping. Payload structs contain only immutable managed strings or no fields; explicit parameterless construction now initializes string payloads to the C++ empty-value state. There are no disposable fields, and ordinary event copies reproduce the value payload because strings are immutable. | — | — | — |
| `src/RedBolt/Engine/EngineParameter.cs` | `Source/Urho3D/Engine/EngineDefs.h` | ☑ | ☑ | ☑ |
| 2026-07-17 outcome | VERIFIED. This static constant-name surface has no instances, destructed fields, methods, or expression returns. `const string` values are compile-time immutable values and require no copy or teardown handling; C# diagnostic parameter names remain accepted deviations. | — | — | — |
| `src/RedBolt/Engine/EngineParameterBinding.cs` | `Source/Urho3D/Engine/Engine.cpp` (`Engine::PopulateDefaultParameters`, `SetParameter`, `HasParameter`, `GetParameter`) | ☑ | ☑ | ☑ |
| 2026-07-17 outcome | VERIFIED as the reflection-based adapter between the allowed managed parameter surface and C++ `ConfigFile`/`Variant` storage. Its static property cache borrows runtime `PropertyInfo` metadata for process lifetime and owns only the standard managed dictionary. Coercion returns independent boxed scalars, enum values, native integers, or immutable strings; it neither owns nor disposes the `EngineParameters` instance. All callers bound returned values to the engine/config lifetime. | — | — | — |
| `src/RedBolt/Engine/EngineParameters.cs` | `Source/Urho3D/Engine/EngineDefs.h`<br>`Source/Urho3D/Engine/Engine.cpp` (`Engine::PopulateDefaultParameters`, `Initialize`) | ☑ | ☑ | ☑ |
| 2026-07-17 outcome | VERIFIED as the strongly typed managed mapping of the C++ engine `StringVariantMap`, including the already-recorded diagnostic parameters and backend/UI naming deviations. Every stored value is a scalar, nullable scalar, enum, native integer, or immutable string, so `MemberwiseClone` reproduces the independent C++ parameter-map value copy without shared mutable payloads. There are no disposable fields; getters return scalars or immutable strings, and `Engine.Initialize` clones the application parameter set before retaining it. | — | — | — |
| `src/RedBolt/Engine/StateManager.cs` | `Source/Urho3D/Engine/StateManager.h`<br>`Source/Urho3D/Engine/StateManager.cpp` | ☐ | ☐ | ☐ |
| 2026-07-17 partial outcome | Core state lifetime and copy semantics FIXED and VERIFIED under the allowed built-in UI exclusion, struct-event mapping, and smart-pointer removal. `ApplicationState` now preserves typed viewport aliases, renderer viewport/fog activation state, mouse state, and viewport-before-base teardown. Each queued `StringVariantMap` is deep-copied at enqueue and dequeue boundaries and disposed at the matching C++ scope; reset/destruction drains the queue before clearing the weak state cache. The cache uses standard `WeakReference<ApplicationState>` to retain C++ non-owning behavior without restoring `WeakPtr`. Transition subscription replacement, event order, overloads, duration clamping, and source control flow are reconciled and covered by focused tests. The compile-gated `URHO3D_ACTIONS` constructor field, update, getter, and `AddAction` return remain explicit `TODO: port` items until the optional Actions subsystem exists, so this row remains incomplete. | — | — | — |
| `src/RedBolt/Engine/StateManagerEvents.cs` | `Source/Urho3D/Engine/StateManagerEvents.h` | ☑ | ☑ | ☑ |
| 2026-07-17 outcome | VERIFIED under the allowed struct-event mapping. All four payload structs default-construct and copy independent `StringHash` scalar values, own no resources, require no deterministic destruction, and are dispatched by value without borrowed or owned reference fields. | — | — | — |
| `src/RedBolt/Graphics/AnimatedModel.cs` | `Source/Urho3D/Graphics/AnimatedModel.h`<br>`Source/Urho3D/Graphics/AnimatedModel.cpp` | ☑ | ☐ | ☐ |
| 2026-07-17 partial outcome | Constructor defaults, field initialization order, and factory registration VERIFIED. The C++ destructor body is now ported: after component detachment it removes the bone hierarchy only for the last model, unsubscribes the managed reload event, releases owned collections in reverse field order, disposes replaced/owned software animators, and then performs base teardown. `animationStateSource_` now uses standard `WeakReference<AnimationStateSource>` under the allowed `WeakPtr` removal, while model, geometry, node, and returned skeleton/morph/container references preserve their C++ borrowed aliases. Per-model geometry-bone mappings and morph containers are independently copied; morph byte storage intentionally aliases like C++ `shared_array`. Final destructor/field and copy-expression closure remains dependent on the pending `Skeleton`/`Bone`, `SoftwareModelAnimator`, `ModelMorph`/`VertexBufferMorph`, and `StaticModel` audits; Node component-removal ordering must also be reconciled before the whole class shape can be declared complete. | — | — | — |
| `src/RedBolt/Graphics/Animation.cs` | `Source/Urho3D/Graphics/Animation.h`<br>`Source/Urho3D/Graphics/Animation.cpp` | ☑ | ☐ | ☐ |
| 2026-07-17 partial outcome | Constructor defaults, field initialization order, inlined `ObjectRevisionTracker` state, and factory registration VERIFIED. The default C++ destructor is now ported for Animation-owned fields in reverse declaration order: trigger `Variant` payloads are destroyed first, followed by owned variant tracks and skeletal tracks; removal, replacement, reset, and resize paths perform the same element destruction. `AnimationTriggerPoint` now exposes the implicit C++ value copy through `ICloneable.Clone()`, and trigger insertion/replacement plus `Animation.Clone()` independently copy reference-backed `Variant` payloads. Variant-track cloning now deep-copies keyframes and tangents without the previous state-changing `Commit()` call, and statement/scope temporaries for XML resources and default serialization values are deterministically disposed. Final destructor and copy-expression closure remains dependent on the pending `ResourceWithMetadata`, `AnimationTrack`, and repository-wide `Variant` call-site audits; the nullable managed `GetTrigger` value and `AnimationController` pending-trigger tuple must also be reconciled with the C++ borrowed trigger pointer. | — | — | — |
| `src/RedBolt/Graphics/AnimationController.cs` | `Source/Urho3D/Graphics/AnimationController.h`<br>`Source/Urho3D/Graphics/AnimationController.cpp` | ☑ | ☐ | ☐ |
| 2026-07-17 partial outcome | Constructor defaults, base construction, field initialization order, and factory registration VERIFIED. The default C++ destructor is now represented by deterministic teardown of controller-owned attribute `Variant` payloads and containers in reverse declaration order before base teardown; per-frame attribute-buffer replacement also destroys the previous payload. `AnimationInstance` now implements the implicit C++ value copy by independently cloning `AnimationParameters` while preserving the shared `AnimationState` alias, and replacement/sort call sites use that copy. State-vector synchronization, animated-model connection, skeletal track resolution, renderer frame statistics, and animation-attribute subspan serialization now follow the C++ update path. Final destructor and copy-expression closure remains dependent on the pending `AnimationStateSource`, `AnimationState`, `Variant`, `Skeleton`/`Bone`, and `AnimatedModel` audits; the pending-trigger tuple still requires reconciliation with the C++ borrowed trigger pointer after `AnimationTriggerPoint` representation is settled. | — | — | — |
| `src/RedBolt/Graphics/AnimationParameters.cs` | `Source/Urho3D/Graphics/AnimationController.h`<br>`Source/Urho3D/Graphics/AnimationController.cpp` | ☑ | ☑ | ☑ |
| 2026-07-17 outcome | VERIFIED. All five construction paths preserve C++ defaults, delegated resource lookup uses the required global `Context` mapping, and field initialization/copy order matches the embedded C++ class. The implicit destructor owns only scalar values and standard managed string mappings; the `Animation` reference is borrowed. The implicit C++ copy constructor and assignment are mapped to typed `ICloneable.Clone()`: `WrappedScalar<float>` is independently reconstructed while the animation pointer alias is preserved, and all controller copy sites use it. Fluent helpers, epsilon-bound delayed removal, wrapped-time updates, variant and binary serialization, equality, and merge checks match the complete C++ bodies. `ToVariantSpan` performs C++ assignment destruction before replacing existing owned `Variant` payloads, and controller attribute serialization writes directly into resized subspans like C++. | — | — | — |
| `src/RedBolt/Graphics/AnimationState.cs` | `Source/Urho3D/Graphics/AnimationState.h`<br>`Source/Urho3D/Graphics/AnimationState.cpp` | ☑ | ☑ | ☑ |
| 2026-07-17 outcome | VERIFIED after restoring C++ weak ownership for the controller, model, node-track targets, and animated-attribute targets; static-property and animation-revision track invalidation; numeric morph indices; and the original hash-combine sequence. The explicit default C++ destructor maps to `Dispose` in reverse field order. `AnimationState` is non-copyable through its `RefCounted` base, track structs preserve pointer aliasing, and sampled attribute `Variant` temporaries are independently copied and deterministically disposed at their C++ scope and assignment boundaries. | — | — | — |
| `src/RedBolt/Graphics/AnimationStateSource.cs` | `Source/Urho3D/Graphics/AnimationStateSource.h` | ☑ | ☑ | ☑ |
| 2026-07-18 outcome | VERIFIED after restoring the public constructor, C++ member ordering, and base-owned state-vector teardown. Construction delegates to `Component` and initializes the shared state vector before the scalar reset flag. `Dispose` releases the vector's strong aliases before component teardown, while `AnimationController` now leaves that base field to the base destructor. The live read-only state view preserves the C++ const-reference alias, the reset flag remains value-initialized to false, and copying is prohibited by the non-copyable `RefCounted` base chain. | — | — | — |
| `src/RedBolt/Graphics/AnimationTrack.cs` | `Source/Urho3D/Graphics/AnimationTrack.h`<br>`Source/Urho3D/Graphics/AnimationTrack.cpp` | ☑ | ☑ | ☑ |
| 2026-07-18 outcome | VERIFIED after implementing the implicit C++ value-copy and destruction behavior for both track types. `AnimationTrack.Clone()` independently copies the keyframe vector and all track metadata, while `VariantAnimationTrack.Clone()` independently copies inherited variant keyframes and tangents plus the derived weight. Track removal, replacement, and animation teardown now destroy owned track storage at the matching C++ map lifetime boundaries, and `Animation.Clone` and `SetTracks` invoke the typed copy operations instead of expanding them at each call site. | — | — | — |
| `src/RedBolt/Graphics/BillboardSet.cs` | `Source/Urho3D/Graphics/BillboardSet.h`<br>`Source/Urho3D/Graphics/BillboardSet.cpp` | ☑ | ☑ | ☑ |
| 2026-07-18 outcome | VERIFIED after restoring the explicit default destructor, C++ constructor defaults, null pointer behavior for invalid billboard indices, and `fmod` animation-LOD wrapping. Derived teardown invalidates the transient sorted-billboard aliases first, disposes index/vertex/geometry ownership in reverse declaration order, destroys the value-owned billboard vector, and then delegates to `Drawable`. The C#-only duplicate billboard accessors were removed in favor of the C++ mutable-reference API. `Billboard` is a plain value type, while `BillboardSet` remains non-copyable through its component base chain; final base-batch and GPU-buffer internals remain tracked by the separate Drawable, Geometry, IndexBuffer, and VertexBuffer inventory rows. | — | — | — |
| `src/RedBolt/Graphics/Camera.cs` | `Source/Urho3D/Graphics/Camera.h`<br>`Source/Urho3D/Graphics/Camera.cpp` | ☑ | ☑ | ☑ |
| 2026-07-18 outcome | VERIFIED after restoring C++ non-owning semantics for the cached Graphics subsystem and frame-selected Zone. Both are stored as weak managed references, while getters resolve the live borrowed targets without extending their lifetimes. Camera remains non-copyable through its Component base chain; cached matrices are independent value copies, the cached Frustum intentionally preserves the C++ const-reference alias, and split frusta are independently constructed return values. The explicit default C++ destructor requires no derived teardown because node listener removal occurs in Node before detachment and the remaining Camera fields are values or non-owning references. | — | — | — |
| `src/RedBolt/Graphics/CameraOperator.cs` | `Source/Urho3D/Graphics/CameraOperator.h`<br>`Source/Urho3D/Graphics/CameraOperator.cpp` | ☑ | ☑ | ☑ |
| 2026-07-18 outcome | VERIFIED after restoring the weak tracked-node vector and Node's prerequisite weak listener storage. Expired tracked nodes preserve their C++ vector slots, resolve as null, serialize as ID zero, and are skipped when building the point cloud. The component is non-copyable through its Component base chain. Its explicit default destructor now clears the point cache, deterministically destroys the owned Variant ID vector, releases weak node observations, and delegates to the base event teardown in reverse field order. Node listener notification paths now purge expired weak observers with the same swap-removal and ordered-erasure behavior as C++. | — | — | — |
| `src/RedBolt/Graphics/CubemapRenderer.cs` | `Source/Urho3D/Graphics/CubemapRenderer.h`<br>`Source/Urho3D/Graphics/CubemapRenderer.cpp` | ☑ | ☑ | ☑ |
| 2026-07-18 outcome | VERIFIED after restoring the weak Scene, override-final-texture, current-viewport-texture, and current-filtered-texture relationships. Active weak texture wrappers are retargeted without per-update allocation. `CubemapUpdateParameters.Clone()` reproduces its implicit C++ value copy with an independent weak wrapper targeting the same borrowed texture, const-reference parameters use `in`, and the current-parameter assignment invokes the explicit copy. Renderer-owned camera nodes, viewports, render pipeline, internal textures, and cached pipeline-state aliases preserve the C++ ownership shape; cached states are not forcibly disposed because the authoritative cache is weak and other shared owners may retain them. The explicit empty C++ destructor requires no derived teardown beyond the existing managed event-subscription release and removal of temporary scene children used by the documented freestanding-node TODO. `CubemapRenderer` remains non-copyable through its Object base. | — | — | — |
| `src/RedBolt/Graphics/CustomGeometry.cs` | `Source/Urho3D/Graphics/CustomGeometry.h`<br>`Source/Urho3D/Graphics/CustomGeometry.cpp` | ☑ | ☑ | ☑ |
| 2026-07-18 outcome | VERIFIED after restoring `CustomGeometryVertex` as a plain independent value, removing the parallel C#-only material ownership list, and preserving materials as borrowed batch references with the mutable cached `ResourceRefList` attribute. Geometry and vertex-buffer ownership is released deterministically in the effective C++ lifetime order, while `Clear` and geometry-vector resizing dispose removed owned objects without resetting state that C++ retains. `CustomGeometry` remains non-copyable through `Drawable`. The C++ element-mask layout, direct mapped-buffer commit, mutable vertex-pointer semantics, and ray-query call path were also restored; the final `VertexBuffer::ClearDataLost` statement remains an explicit TODO because the managed VertexBuffer has not yet ported DeviceObject data-loss state and is tracked by its own inventory row. | — | — | — |
| `src/RedBolt/Graphics/DebugGeometry.cs` | `DebugLine` and `DebugTriangle` in `Source/Urho3D/Graphics/DebugRenderer.h` | ☑ | ☑ | ☑ |
| 2026-07-18 outcome | VERIFIED. This managed file contains the two plain C++ value structs declared by `DebugRenderer.h`, not an upstream `DebugGeometry` class. `DebugLine` and `DebugTriangle` preserve declaration order, zero-default construction, value constructors, and documentation. Their `Vector3f` and `uint` fields are value-owned, ordinary assignment produces independent copies, and insertion into `DebugRenderer`'s managed value lists reproduces the C++ temporary construction followed by vector value storage. Neither type owns resources or has custom destruction; no `Dispose` or `Clone` operation is required. | — | — | — |
| `src/RedBolt/Graphics/DebugRenderer.cs` | `Source/Urho3D/Graphics/DebugRenderer.h`<br>`Source/Urho3D/Graphics/DebugRenderer.cpp` | ☑ | ☑ | ☑ |
| 2026-07-18 outcome | VERIFIED after restoring the C++ weak camera observation, independent `Frustum` value assignment in `SetView`, constructor-time vertex-buffer debug naming, and deterministic derived-resource cleanup before base event teardown. The renderer owns its dynamic vertex buffer and static pipeline-state cache, borrows the camera through a weak reference, stores debug primitives by value, and remains non-copyable through `Component`. Focused lifetime/value tests pass 20/20; the required build and MultipleViewports visual check pass, and the full suite remains at its two established unrelated failures with 1216 tests passing. | — | — | — |
| `src/RedBolt/Graphics/DecalSet.cs` | `Source/Urho3D/Graphics/DecalSet.h`<br>`Source/Urho3D/Graphics/DecalSet.cpp` | ☑ | ☑ | ☑ |
| 2026-07-18 outcome | VERIFIED after restoring `DecalVertex` as a plain independent value and implementing the C++ `Decal` value-copy behavior as a deep `Clone` of its vertex and index containers. `DecalSet` remains non-copyable through `Drawable`; it deterministically releases its owned geometry, vertex buffer, and index buffer, stores decals as independent values, and clones `Bone` values while retaining their nodes as borrowed weak listeners. The complete C++ CPU-geometry and skinned-stream face extraction, bone-index remapping, shared tangent generation, and unavailable-CPU-data behavior were restored. Focused tests pass 8/8; the required build and MultipleViewports visual check pass. The full suite passes 1216/1221: the two established unrelated failures remain, and the three known order-dependent `DrawableProcessorTests` failures pass 8/8 in isolation. | — | — | — |
| `src/RedBolt/Graphics/Drawable.cs` | `Source/Urho3D/Graphics/Drawable.h`<br>`Source/Urho3D/Graphics/Drawable.cpp` | ☑ | ☑ | ☑ |
| 2026-07-18 outcome | VERIFIED after restoring the C++ abstract world-bounding-box contract, exact property-setter side effects, octree update-queued state, cancellation-before-removal teardown, and coupled cached-zone/reflection resets. `Drawable` remains non-copyable through `Component`; disposal now removes it from the octree before releasing cached camera/probe observations, `SourceBatch` values, pipeline tracking, and base-component state. Cached zones and per-frame cameras are borrowed weak observations, cached reflection probe arrays have independent fixed-size value-copy semantics, and explicit `SourceBatch` copy construction preserves C++ shallow pointer/reference aliases without cloning geometry, material, transforms, or instance data. The missing `StaticModel::OnWorldBoundingBoxUpdate` override and detached-node test fixtures were reconciled with upstream. Focused Drawable/SourceBatch/reflection/octree tests pass 25/25 and OctreeQuery tests pass 5/5; the required build and MultipleViewports visual check pass, and the full suite is at its two established unrelated failures with 1222/1224 tests passing. | — | — | — |
| `src/RedBolt/Graphics/DrawableEvents.cs` | `Source/Urho3D/Graphics/DrawableEvents.h` | ☑ | ☑ | ☑ |
| 2026-07-18 outcome | VERIFIED. The five struct-based event payloads preserve the C++ declaration and field order. Node, Animation, ParticleEffect, and Terrain-related object pointers remain borrowed aliases; the particle-finished effect field now has the exact `ParticleEffect` type instead of the C#-only `object` widening. Animation names, times, loop flags, and trigger data retain value semantics, and the trigger producer now deep-clones `Variant` data to reproduce assignment into the C++ event-data map without cloning borrowed Node or Animation pointers. The header declares no constructors, copy constructors, or destructors for these macro-backed payload definitions. The focused event test passes; the required build and MultipleViewports visual check pass, and the full suite remains at its two established unrelated failures with 1222/1224 tests passing. | — | — | — |
| `src/RedBolt/Graphics/Geometry.cs` | `Source/Urho3D/Graphics/Geometry.h`<br>`Source/Urho3D/Graphics/Geometry.cpp` | ☑ | ☑ | ☑ |
| 2026-07-18 outcome | VERIFIED after restoring C++ vector-resize behavior that preserves retained vertex streams and dependency subscriptions, deterministic reverse-order release of raw-data aliases and pipeline subscriptions before ID-family/base teardown, and direct shared access to vertex/index shadow storage without per-query repacking. Geometry holds strong shared references to its vertex and index buffers without cloning or exclusively disposing them, while pipeline subscriptions are non-owning and raw override arrays retain shared-array alias semantics. Ray queries now reproduce the C++ fallback to non-indexed vertex traversal when index shadow data is unavailable. Geometry remains non-copyable through Object, acquires one family ID during construction, and releases it during disposal. Focused Geometry and dependent graphics tests pass 58/58; the required build and MultipleViewports visual check pass, and the full suite remains at its two established unrelated failures with 1223/1225 tests passing. | — | — | — |
| `src/RedBolt/Graphics/GlobalIllumination.cs` | `Source/Urho3D/Graphics/GlobalIllumination.h`<br>`Source/Urho3D/Graphics/GlobalIllumination.cpp` | ☑ | ☑ | ☑ |
| 2026-07-18 outcome | VERIFIED for lifetime and copy semantics. The non-copyable Component owns value-equivalent `ResourceRef`, offset, tetrahedral-mesh, and baked-probe state; mesh reset and reload failure now replace the complete C++ value instead of partially redefining retained lists. The default derived destructor delegates deterministic event teardown to Component/Object, temporary compile resources remain scoped aliases, and world-origin updates mutate the owned mesh in place. GI serialization now includes both mesh and baked data, preserves rbfx binary byte-vector layout, regenerates ambient data on input, and uses the nullable temporary-resource call path without C#-only exception fallbacks. Debug edge storage preserves C++ thread-local reuse without per-frame allocation. The reflected reload editor action remains an explicit `TODO: port` until the managed ObjectReflection facade can express actions; this does not change the verified construction, destruction, ownership, or copy result. Focused archive/light-probe tests pass 20/20; the required build and MultipleViewports visual check pass, and the full suite remains at its two established unrelated failures with 1224/1226 tests passing. | — | — | — |
| `src/RedBolt/Graphics/Graphics.cs` | `Source/Urho3D/Graphics/Graphics.h`<br>`Source/Urho3D/Graphics/Graphics.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Graphics/GraphicsBackend.cs` | **UNRESOLVED — search by class name** | ☐ | ☐ | ☐ |
| `src/RedBolt/Graphics/GraphicsDefs.cs` | `Source/Urho3D/Graphics/GraphicsDefs.h`<br>`Source/Urho3D/Graphics/GraphicsDefs.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Graphics/GraphicsEvents.cs` | `Source/Urho3D/Graphics/GraphicsEvents.h` | ☐ | ☐ | ☐ |
| `src/RedBolt/Graphics/GraphicsUtils.cs` | `Source/Urho3D/Graphics/GraphicsUtils.h`<br>`Source/Urho3D/Graphics/GraphicsUtils.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Graphics/ImageFileWriter.cs` | **UNRESOLVED — search by class name** | ☐ | ☐ | ☐ |
| `src/RedBolt/Graphics/IndexBuffer.cs` | `Source/Urho3D/Graphics/IndexBuffer.h`<br>`Source/Urho3D/Graphics/IndexBuffer.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Graphics/Light.cs` | `Source/Urho3D/Graphics/Light.h`<br>`Source/Urho3D/Graphics/Light.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Graphics/LightBaker.cs` | `Source/Urho3D/Graphics/LightBaker.h`<br>`Source/Urho3D/Graphics/LightBaker.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Graphics/LightBakingSettings.cs` | `Source/Urho3D/Graphics/LightBakingSettings.h` | ☐ | ☐ | ☐ |
| `src/RedBolt/Graphics/LightProbeGroup.cs` | `Source/Urho3D/Graphics/LightProbeGroup.h`<br>`Source/Urho3D/Graphics/LightProbeGroup.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Graphics/Material.cs` | `Source/Urho3D/Graphics/Material.h`<br>`Source/Urho3D/Graphics/Material.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Graphics/Model.cs` | `Source/Urho3D/Graphics/Model.h`<br>`Source/Urho3D/Graphics/Model.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Graphics/ModelView.cs` | `Source/Urho3D/Graphics/ModelView.h`<br>`Source/Urho3D/Graphics/ModelView.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Graphics/OcclusionBuffer.cs` | `Source/Urho3D/Graphics/OcclusionBuffer.h`<br>`Source/Urho3D/Graphics/OcclusionBuffer.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Graphics/Octree.cs` | `Source/Urho3D/Graphics/Octree.h`<br>`Source/Urho3D/Graphics/Octree.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Graphics/OctreeQuery.cs` | `Source/Urho3D/Graphics/OctreeQuery.h`<br>`Source/Urho3D/Graphics/OctreeQuery.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Graphics/OutlineGroup.cs` | `Source/Urho3D/Graphics/OutlineGroup.h`<br>`Source/Urho3D/Graphics/OutlineGroup.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Graphics/ParticleEffect.cs` | `Source/Urho3D/Graphics/ParticleEffect.h`<br>`Source/Urho3D/Graphics/ParticleEffect.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Graphics/ParticleEmitter.cs` | `Source/Urho3D/Graphics/ParticleEmitter.h`<br>`Source/Urho3D/Graphics/ParticleEmitter.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Graphics/PipelineStateTracker.cs` | `Source/Urho3D/Graphics/PipelineStateTracker.h`<br>`Source/Urho3D/Graphics/PipelineStateTracker.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Graphics/ReflectionProbe.cs` | `Source/Urho3D/Graphics/ReflectionProbe.h`<br>`Source/Urho3D/Graphics/ReflectionProbe.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Graphics/ReflectionProbeData.cs` | `Source/Urho3D/Graphics/ReflectionProbeData.h` | ☐ | ☐ | ☐ |
| `src/RedBolt/Graphics/RenderSurface.cs` | `Source/Urho3D/Graphics/RenderSurface.h`<br>`Source/Urho3D/Graphics/RenderSurface.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Graphics/Renderer.cs` | `Source/Urho3D/Graphics/Renderer.h`<br>`Source/Urho3D/Graphics/Renderer.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Graphics/RibbonTrail.cs` | `Source/Urho3D/Graphics/RibbonTrail.h`<br>`Source/Urho3D/Graphics/RibbonTrail.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Graphics/Shader.cs` | `Source/Urho3D/Graphics/Shader.h`<br>`Source/Urho3D/Graphics/Shader.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Graphics/ShaderCompiler.cs` | `Source/Urho3D/Shader/ShaderCompiler.cpp`<br>`Source/Urho3D/Shader/ShaderCompiler.h` | ☐ | ☐ | ☐ |
| `src/RedBolt/Graphics/ShaderDefs.cs` | `Source/Urho3D/Shader/ShaderDefs.h` | ☐ | ☐ | ☐ |
| `src/RedBolt/Graphics/ShaderOptimizer.cs` | `Source/Urho3D/Shader/ShaderOptimizer.cpp`<br>`Source/Urho3D/Shader/ShaderOptimizer.h` | ☐ | ☐ | ☐ |
| `src/RedBolt/Graphics/ShaderSourceLogger.cs` | `Source/Urho3D/Shader/ShaderSourceLogger.cpp`<br>`Source/Urho3D/Shader/ShaderSourceLogger.h` | ☐ | ☐ | ☐ |
| `src/RedBolt/Graphics/ShaderTranslator.cs` | `Source/Urho3D/Shader/ShaderTranslator.cpp`<br>`Source/Urho3D/Shader/ShaderTranslator.h` | ☐ | ☐ | ☐ |
| `src/RedBolt/Graphics/ShaderVariation.cs` | `Source/Urho3D/Graphics/ShaderVariation.h`<br>`Source/Urho3D/Graphics/ShaderVariation.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Graphics/Skeleton.cs` | `Source/Urho3D/Graphics/Skeleton.h`<br>`Source/Urho3D/Graphics/Skeleton.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Graphics/Skybox.cs` | `Source/Urho3D/Graphics/Skybox.h`<br>`Source/Urho3D/Graphics/Skybox.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Graphics/SoftwareModelAnimator.cs` | `Source/Urho3D/Graphics/SoftwareModelAnimator.h`<br>`Source/Urho3D/Graphics/SoftwareModelAnimator.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Graphics/StaticModel.cs` | `Source/Urho3D/Graphics/StaticModel.h`<br>`Source/Urho3D/Graphics/StaticModel.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Graphics/StaticModelGroup.cs` | `Source/Urho3D/Graphics/StaticModelGroup.h`<br>`Source/Urho3D/Graphics/StaticModelGroup.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Graphics/Tangent.cs` | `Source/Urho3D/Graphics/Tangent.h`<br>`Source/Urho3D/Graphics/Tangent.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Graphics/Technique.cs` | `Source/Urho3D/Graphics/Technique.h`<br>`Source/Urho3D/Graphics/Technique.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Graphics/Terrain.cs` | `Source/Urho3D/Graphics/Terrain.h`<br>`Source/Urho3D/Graphics/Terrain.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Graphics/TerrainPatch.cs` | `Source/Urho3D/Graphics/TerrainPatch.h`<br>`Source/Urho3D/Graphics/TerrainPatch.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Graphics/Texture.cs` | `Source/Urho3D/Graphics/Texture.h`<br>`Source/Urho3D/Graphics/Texture.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Graphics/Texture2D.cs` | `Source/Urho3D/Graphics/Texture2D.h`<br>`Source/Urho3D/Graphics/Texture2D.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Graphics/Texture2DArray.cs` | `Source/Urho3D/Graphics/Texture2DArray.h`<br>`Source/Urho3D/Graphics/Texture2DArray.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Graphics/Texture3D.cs` | `Source/Urho3D/Graphics/Texture3D.h`<br>`Source/Urho3D/Graphics/Texture3D.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Graphics/TextureCube.cs` | `Source/Urho3D/Graphics/TextureCube.h`<br>`Source/Urho3D/Graphics/TextureCube.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Graphics/VertexBuffer.cs` | `Source/Urho3D/Graphics/VertexBuffer.h`<br>`Source/Urho3D/Graphics/VertexBuffer.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Graphics/VertexElement.cs` | **UNRESOLVED — search by class name** | ☐ | ☐ | ☐ |
| `src/RedBolt/Graphics/Viewport.cs` | `Source/Urho3D/Graphics/Viewport.h`<br>`Source/Urho3D/Graphics/Viewport.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Graphics/Zone.cs` | `Source/Urho3D/Graphics/Zone.h`<br>`Source/Urho3D/Graphics/Zone.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/IO/AbstractFile.cs` | `Source/Urho3D/IO/AbstractFile.h` | ☐ | ☐ | ☐ |
| `src/RedBolt/IO/Archive.cs` | `Source/Urho3D/IO/Archive.h`<br>`Source/Urho3D/IO/Archive.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/IO/ArchiveSerialization.TetrahedralMesh.cs` | **UNRESOLVED — search by class name** | ☐ | ☐ | ☐ |
| `src/RedBolt/IO/ArchiveSerialization.cs` | `Source/Urho3D/IO/ArchiveSerialization.h`<br>`Source/Urho3D/IO/ArchiveSerialization.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/IO/Base64Archive.cs` | `Source/Urho3D/IO/Base64Archive.h`<br>`Source/Urho3D/IO/Base64Archive.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/IO/BinaryArchive.cs` | `Source/Urho3D/IO/BinaryArchive.h`<br>`Source/Urho3D/IO/BinaryArchive.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/IO/Compression.cs` | `Source/Urho3D/IO/Compression.h`<br>`Source/Urho3D/IO/Compression.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/IO/Deserializer.cs` | `Source/Urho3D/IO/Deserializer.h`<br>`Source/Urho3D/IO/Deserializer.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/IO/File.cs` | `Source/Urho3D/IO/File.h`<br>`Source/Urho3D/IO/File.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/IO/FileIdentifier.cs` | `Source/Urho3D/IO/FileIdentifier.h`<br>`Source/Urho3D/IO/FileIdentifier.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/IO/FileWatcher.cs` | `Source/Urho3D/IO/FileWatcher.h`<br>`Source/Urho3D/IO/FileWatcher.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/IO/IODefs.cs` | `Source/Urho3D/IO/IODefs.h` | ☐ | ☐ | ☐ |
| `src/RedBolt/IO/IOEvents.cs` | `Source/Urho3D/IO/IOEvents.h` | ☐ | ☐ | ☐ |
| `src/RedBolt/IO/MemoryBuffer.cs` | `Source/Urho3D/IO/MemoryBuffer.h`<br>`Source/Urho3D/IO/MemoryBuffer.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/IO/MountPoint.cs` | `Source/Urho3D/IO/MountPoint.h`<br>`Source/Urho3D/IO/MountPoint.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/IO/MountedAliasRoot.cs` | `Source/Urho3D/IO/MountedAliasRoot.h`<br>`Source/Urho3D/IO/MountedAliasRoot.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/IO/MountedDirectory.cs` | `Source/Urho3D/IO/MountedDirectory.h`<br>`Source/Urho3D/IO/MountedDirectory.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/IO/MountedExternalMemory.cs` | `Source/Urho3D/IO/MountedExternalMemory.h`<br>`Source/Urho3D/IO/MountedExternalMemory.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/IO/MountedRoot.cs` | `Source/Urho3D/IO/MountedRoot.h`<br>`Source/Urho3D/IO/MountedRoot.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/IO/MultiFileWatcher.cs` | `Source/Urho3D/IO/MultiFileWatcher.h`<br>`Source/Urho3D/IO/MultiFileWatcher.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/IO/NamedPipe.cs` | `Source/Urho3D/IO/NamedPipe.h`<br>`Source/Urho3D/IO/NamedPipe.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/IO/PackageFile.cs` | `Source/Urho3D/IO/PackageFile.h`<br>`Source/Urho3D/IO/PackageFile.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/IO/ScanFlags.cs` | `Source/Urho3D/IO/ScanFlags.h` | ☐ | ☐ | ☐ |
| `src/RedBolt/IO/Serializer.cs` | `Source/Urho3D/IO/Serializer.h`<br>`Source/Urho3D/IO/Serializer.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/IO/VectorBuffer.cs` | `Source/Urho3D/IO/VectorBuffer.h`<br>`Source/Urho3D/IO/VectorBuffer.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/IO/VirtualFileSystem.cs` | `Source/Urho3D/IO/VirtualFileSystem.h`<br>`Source/Urho3D/IO/VirtualFileSystem.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Input/AxisAdapter.cs` | `Source/Urho3D/Input/AxisAdapter.h`<br>`Source/Urho3D/Input/AxisAdapter.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Input/DirectionAggregator.cs` | `Source/Urho3D/Input/DirectionAggregator.h`<br>`Source/Urho3D/Input/DirectionAggregator.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Input/DirectionalPadAdapter.cs` | `Source/Urho3D/Input/DirectionalPadAdapter.h`<br>`Source/Urho3D/Input/DirectionalPadAdapter.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Input/FreeFlyController.cs` | `Source/Urho3D/Input/FreeFlyController.h`<br>`Source/Urho3D/Input/FreeFlyController.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Input/IDearImGuiInputSink.cs` | **UNRESOLVED — search by class name** | ☐ | ☐ | ☐ |
| `src/RedBolt/Input/Input.cs` | `Source/Urho3D/Input/Input.h`<br>`Source/Urho3D/Input/Input.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Input/InputConstants.cs` | `Source/Urho3D/Input/InputConstants.h` | ☐ | ☐ | ☐ |
| `src/RedBolt/Input/InputEvents.cs` | `Source/Urho3D/Input/InputEvents.h` | ☐ | ☐ | ☐ |
| `src/RedBolt/Input/InputMap.cs` | `Source/Urho3D/Input/InputMap.h`<br>`Source/Urho3D/Input/InputMap.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Input/MoveAndOrbitComponent.cs` | `Source/Urho3D/Input/MoveAndOrbitComponent.h`<br>`Source/Urho3D/Input/MoveAndOrbitComponent.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Input/MoveAndOrbitController.cs` | `Source/Urho3D/Input/MoveAndOrbitController.h`<br>`Source/Urho3D/Input/MoveAndOrbitController.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Input/MultitouchAdapter.cs` | `Source/Urho3D/Input/MultitouchAdapter.h`<br>`Source/Urho3D/Input/MultitouchAdapter.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Input/PointerAdapter.cs` | `Source/Urho3D/Input/PointerAdapter.h`<br>`Source/Urho3D/Input/PointerAdapter.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Logging/Log.cs` | `Source/Urho3D/IO/Log.cpp`<br>`Source/Urho3D/IO/Log.h` | ☐ | ☐ | ☐ |
| `src/RedBolt/Math/ApproximateEqualityComparer.cs` | **UNRESOLVED — search by class name** | ☐ | ☐ | ☐ |
| `src/RedBolt/Math/AreaAllocator.cs` | `Source/Urho3D/Math/AreaAllocator.h`<br>`Source/Urho3D/Math/AreaAllocator.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Math/BoundingBox.cs` | `Source/Urho3D/Math/BoundingBox.h`<br>`Source/Urho3D/Math/BoundingBox.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Math/Color.cs` | `Source/Urho3D/Math/Color.h`<br>`Source/Urho3D/Math/Color.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Math/EaseMath.cs` | `Source/Urho3D/Math/EaseMath.h` | ☐ | ☐ | ☐ |
| `src/RedBolt/Math/Frustum.cs` | `Source/Urho3D/Math/Frustum.h`<br>`Source/Urho3D/Math/Frustum.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Math/IApproximateEquatable.cs` | **UNRESOLVED — search by class name** | ☐ | ☐ | ☐ |
| `src/RedBolt/Math/IKChain.cs` | **UNRESOLVED — search by class name** | ☐ | ☐ | ☐ |
| `src/RedBolt/Math/IKFabrikChain.cs` | **UNRESOLVED — search by class name** | ☐ | ☐ | ☐ |
| `src/RedBolt/Math/IKSpineChain.cs` | **UNRESOLVED — search by class name** | ☐ | ☐ | ☐ |
| `src/RedBolt/Math/IntRect.cs` | **UNRESOLVED — search by class name** | ☐ | ☐ | ☐ |
| `src/RedBolt/Math/IntVector2.cs` | `Source/Tests/Math/IntVector2.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Math/IntVector3.cs` | `Source/Tests/Math/IntVector3.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Math/Intersection.cs` | **UNRESOLVED — search by class name** | ☐ | ☐ | ☐ |
| `src/RedBolt/Math/InverseKinematicsChains.cs` | **UNRESOLVED — search by class name** | ☐ | ☐ | ☐ |
| `src/RedBolt/Math/InverseKinematicsTypes.cs` | **UNRESOLVED — search by class name** | ☐ | ☐ | ☐ |
| `src/RedBolt/Math/MathDefs.cs` | `Source/Urho3D/Math/MathDefs.h`<br>`Source/Urho3D/Math/MathDefs.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Math/Matrix2.cs` | `Source/Urho3D/Math/Matrix2.h`<br>`Source/Urho3D/Math/Matrix2.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Math/Matrix3.cs` | `Source/Urho3D/Math/Matrix3.h`<br>`Source/Urho3D/Math/Matrix3.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Math/Matrix3x4.cs` | `Source/Urho3D/Math/Matrix3x4.h`<br>`Source/Urho3D/Math/Matrix3x4.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Math/Matrix4.cs` | `Source/Urho3D/Math/Matrix4.h`<br>`Source/Urho3D/Math/Matrix4.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Math/NumericRange.cs` | `Source/Urho3D/Math/NumericRange.h` | ☐ | ☐ | ☐ |
| `src/RedBolt/Math/PerlinNoise.cs` | `Source/Urho3D/Math/PerlinNoise.h`<br>`Source/Urho3D/Math/PerlinNoise.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Math/Plane.cs` | `Source/Urho3D/Math/Plane.h`<br>`Source/Urho3D/Math/Plane.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Math/Polyhedron.cs` | `Source/Urho3D/Math/Polyhedron.h`<br>`Source/Urho3D/Math/Polyhedron.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Math/Quaternion.cs` | `Source/Urho3D/Math/Quaternion.h`<br>`Source/Urho3D/Math/Quaternion.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Math/RandomEngine.cs` | `Source/Urho3D/Math/RandomEngine.h`<br>`Source/Urho3D/Math/RandomEngine.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Math/Ray.cs` | `Source/Urho3D/Math/Ray.h`<br>`Source/Urho3D/Math/Ray.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Math/Rect.cs` | `Source/Urho3D/Math/Rect.h`<br>`Source/Urho3D/Math/Rect.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Math/Sphere.cs` | `Source/Urho3D/Math/Sphere.h`<br>`Source/Urho3D/Math/Sphere.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Math/SphericalHarmonics.cs` | `Source/Urho3D/Math/SphericalHarmonics.h`<br>`Source/Urho3D/Math/SphericalHarmonics.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Math/TetrahedralMesh.Implementation.cs` | `Source/Urho3D/Math/TetrahedralMesh.h`<br>`Source/Urho3D/Math/TetrahedralMesh.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Math/TetrahedralMesh.cs` | `Source/Urho3D/Math/TetrahedralMesh.h`<br>`Source/Urho3D/Math/TetrahedralMesh.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Math/TetrahedralMeshTypes.cs` | **UNRESOLVED — search by class name** | ☐ | ☐ | ☐ |
| `src/RedBolt/Math/Transform.cs` | `Source/Urho3D/Math/Transform.h`<br>`Source/Urho3D/Math/Transform.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Math/Vector2.cs` | `Source/Urho3D/Math/Vector2.h` | ☐ | ☐ | ☐ |
| `src/RedBolt/Math/Vector3.cs` | `Source/Urho3D/Math/Vector3.h` | ☐ | ☐ | ☐ |
| `src/RedBolt/Math/Vector4.cs` | `Source/Urho3D/Math/Vector4.h`<br>`Source/Urho3D/Math/Vector4.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Math/VectorMarshallers.cs` | **UNRESOLVED — search by class name** | ☐ | ☐ | ☐ |
| `src/RedBolt/Math/WrappedScalar.cs` | `Source/Urho3D/Math/WrappedScalar.h` | ☐ | ☐ | ☐ |
| `src/RedBolt/RenderAPI/ConstantBufferCollection.cs` | `Source/Urho3D/RenderAPI/ConstantBufferCollection.h` | ☐ | ☐ | ☐ |
| `src/RedBolt/RenderAPI/DeviceObject.cs` | `Source/Urho3D/RenderAPI/DeviceObject.h`<br>`Source/Urho3D/RenderAPI/DeviceObject.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/RenderAPI/DrawCommandDescription.cs` | **UNRESOLVED — search by class name** | ☐ | ☐ | ☐ |
| `src/RedBolt/RenderAPI/DrawCommandQueue.cs` | `Source/Urho3D/RenderAPI/DrawCommandQueue.h`<br>`Source/Urho3D/RenderAPI/DrawCommandQueue.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/RenderAPI/PipelineState.cs` | `Source/Urho3D/RenderAPI/PipelineState.h`<br>`Source/Urho3D/RenderAPI/PipelineState.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/RenderAPI/PipelineStateCache.cs` | **UNRESOLVED — search by class name** | ☐ | ☐ | ☐ |
| `src/RedBolt/RenderAPI/RawBuffer.cs` | `Source/Urho3D/RenderAPI/RawBuffer.h`<br>`Source/Urho3D/RenderAPI/RawBuffer.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/RenderAPI/RawShader.cs` | `Source/Urho3D/RenderAPI/RawShader.h`<br>`Source/Urho3D/RenderAPI/RawShader.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/RenderAPI/RawTexture.cs` | `Source/Urho3D/RenderAPI/RawTexture.h`<br>`Source/Urho3D/RenderAPI/RawTexture.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/RenderAPI/RenderAPIDefs.cs` | `Source/Urho3D/RenderAPI/RenderAPIDefs.h` | ☐ | ☐ | ☐ |
| `src/RedBolt/RenderAPI/RenderAPIUtils.cs` | `Source/Urho3D/RenderAPI/RenderAPIUtils.h`<br>`Source/Urho3D/RenderAPI/RenderAPIUtils.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/RenderAPI/RenderContext.cs` | `Source/Urho3D/RenderAPI/RenderContext.h`<br>`Source/Urho3D/RenderAPI/RenderContext.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/RenderAPI/RenderDevice.cs` | `Source/Urho3D/RenderAPI/RenderDevice.h`<br>`Source/Urho3D/RenderAPI/RenderDevice.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/RenderAPI/RenderPool.cs` | `Source/Urho3D/RenderAPI/RenderPool.h`<br>`Source/Urho3D/RenderAPI/RenderPool.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/RenderAPI/RenderScope.cs` | `Source/Urho3D/RenderAPI/RenderScope.h`<br>`Source/Urho3D/RenderAPI/RenderScope.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/RenderAPI/RenderTargetView.cs` | `Source/Urho3D/RenderAPI/RenderTargetView.h`<br>`Source/Urho3D/RenderAPI/RenderTargetView.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/RenderAPI/ShaderBytecode.cs` | `Source/Urho3D/RenderAPI/ShaderBytecode.h`<br>`Source/Urho3D/RenderAPI/ShaderBytecode.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/RenderAPI/ShaderProgramReflection.cs` | `Source/Urho3D/RenderAPI/ShaderProgramReflection.h`<br>`Source/Urho3D/RenderAPI/ShaderProgramReflection.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/RenderPipeline/BatchCompositor.cs` | `Source/Urho3D/RenderPipeline/BatchCompositor.h`<br>`Source/Urho3D/RenderPipeline/BatchCompositor.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/RenderPipeline/BatchRenderer.cs` | `Source/Urho3D/RenderPipeline/BatchRenderer.h`<br>`Source/Urho3D/RenderPipeline/BatchRenderer.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/RenderPipeline/BatchStateCache.cs` | `Source/Urho3D/RenderPipeline/BatchStateCache.h`<br>`Source/Urho3D/RenderPipeline/BatchStateCache.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/RenderPipeline/CameraProcessor.cs` | `Source/Urho3D/RenderPipeline/CameraProcessor.h`<br>`Source/Urho3D/RenderPipeline/CameraProcessor.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/RenderPipeline/DrawCommandCompositor.cs` | **UNRESOLVED — search by class name** | ☐ | ☐ | ☐ |
| `src/RedBolt/RenderPipeline/DrawableProcessor.cs` | `Source/Urho3D/RenderPipeline/DrawableProcessor.h`<br>`Source/Urho3D/RenderPipeline/DrawableProcessor.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/RenderPipeline/InstancingBuffer.cs` | `Source/Urho3D/RenderPipeline/InstancingBuffer.h`<br>`Source/Urho3D/RenderPipeline/InstancingBuffer.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/RenderPipeline/LightAccumulator.cs` | `Source/Urho3D/RenderPipeline/LightAccumulator.h` | ☐ | ☐ | ☐ |
| `src/RedBolt/RenderPipeline/LightProcessor.cs` | `Source/Urho3D/RenderPipeline/LightProcessor.h`<br>`Source/Urho3D/RenderPipeline/LightProcessor.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/RenderPipeline/LightProcessorQuery.cs` | `Source/Urho3D/RenderPipeline/LightProcessorQuery.h`<br>`Source/Urho3D/RenderPipeline/LightProcessorQuery.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/RenderPipeline/LightmapRenderPipeline.cs` | `Source/Urho3D/RenderPipeline/LightmapRenderPipeline.h`<br>`Source/Urho3D/RenderPipeline/LightmapRenderPipeline.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/RenderPipeline/OutlineScenePass.cs` | `Source/Urho3D/RenderPipeline/OutlineScenePass.h`<br>`Source/Urho3D/RenderPipeline/OutlineScenePass.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/RenderPipeline/PipelineBatch.cs` | **UNRESOLVED — search by class name** | ☐ | ☐ | ☐ |
| `src/RedBolt/RenderPipeline/PipelineBatchSortKey.cs` | `Source/Urho3D/RenderPipeline/PipelineBatchSortKey.h` | ☐ | ☐ | ☐ |
| `src/RedBolt/RenderPipeline/PipelineStateBuilder.cs` | `Source/Urho3D/RenderPipeline/PipelineStateBuilder.h`<br>`Source/Urho3D/RenderPipeline/PipelineStateBuilder.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/RenderPipeline/RenderBuffer.cs` | `Source/Urho3D/RenderPipeline/RenderBuffer.h`<br>`Source/Urho3D/RenderPipeline/RenderBuffer.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/RenderPipeline/RenderBufferManager.cs` | `Source/Urho3D/RenderPipeline/RenderBufferManager.h`<br>`Source/Urho3D/RenderPipeline/RenderBufferManager.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/RenderPipeline/RenderPass.cs` | `Source/Urho3D/RenderPipeline/RenderPass.h`<br>`Source/Urho3D/RenderPipeline/RenderPass.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/RenderPipeline/RenderPath.cs` | `Source/Urho3D/RenderPipeline/RenderPath.h`<br>`Source/Urho3D/RenderPipeline/RenderPath.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/RenderPipeline/RenderPathBuiltInPasses.cs` | **UNRESOLVED — search by class name** | ☐ | ☐ | ☐ |
| `src/RedBolt/RenderPipeline/RenderPipeline.cs` | `Source/Urho3D/RenderPipeline/RenderPipeline.h`<br>`Source/Urho3D/RenderPipeline/RenderPipeline.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/RenderPipeline/RenderPipelineDebugger.cs` | `Source/Urho3D/RenderPipeline/RenderPipelineDebugger.h`<br>`Source/Urho3D/RenderPipeline/RenderPipelineDebugger.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/RenderPipeline/RenderPipelineDefs.cs` | `Source/Urho3D/RenderPipeline/RenderPipelineDefs.h`<br>`Source/Urho3D/RenderPipeline/RenderPipelineDefs.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/RenderPipeline/ScenePass.cs` | `Source/Urho3D/RenderPipeline/ScenePass.h`<br>`Source/Urho3D/RenderPipeline/ScenePass.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/RenderPipeline/SceneProcessor.cs` | `Source/Urho3D/RenderPipeline/SceneProcessor.h`<br>`Source/Urho3D/RenderPipeline/SceneProcessor.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/RenderPipeline/ShaderConsts.cs` | `Source/Urho3D/RenderPipeline/ShaderConsts.h` | ☐ | ☐ | ☐ |
| `src/RedBolt/RenderPipeline/ShaderProgramCompositor.cs` | `Source/Urho3D/RenderPipeline/ShaderProgramCompositor.h`<br>`Source/Urho3D/RenderPipeline/ShaderProgramCompositor.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/RenderPipeline/ShadowMapAllocator.cs` | `Source/Urho3D/RenderPipeline/ShadowMapAllocator.h`<br>`Source/Urho3D/RenderPipeline/ShadowMapAllocator.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/RenderPipeline/ShadowSplitProcessor.cs` | `Source/Urho3D/RenderPipeline/ShadowSplitProcessor.h`<br>`Source/Urho3D/RenderPipeline/ShadowSplitProcessor.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/RenderPipeline/SharedRenderPassState.cs` | `Source/Urho3D/RenderPipeline/SharedRenderPassState.h`<br>`Source/Urho3D/RenderPipeline/SharedRenderPassState.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/RenderPipeline/StaticPipelineStateCache.cs` | `Source/Urho3D/RenderPipeline/StaticPipelineStateCache.h`<br>`Source/Urho3D/RenderPipeline/StaticPipelineStateCache.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/RenderPipeline/StereoRenderPipeline.cs` | `Source/Urho3D/RenderPipeline/StereoRenderPipeline.h`<br>`Source/Urho3D/RenderPipeline/StereoRenderPipeline.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/RenderPipeline/ViewportColorRenderBuffer.cs` | **UNRESOLVED — search by class name** | ☐ | ☐ | ☐ |
| `src/RedBolt/RenderPipeline/ViewportDepthStencilRenderBuffer.cs` | **UNRESOLVED — search by class name** | ☐ | ☐ | ☐ |
| `src/RedBolt/Resource/AsyncLoadState.cs` | **UNRESOLVED — search by class name** | ☐ | ☐ | ☐ |
| `src/RedBolt/Resource/BackgroundLoader.cs` | `Source/Urho3D/Resource/BackgroundLoader.h`<br>`Source/Urho3D/Resource/BackgroundLoader.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Resource/BinaryFile.cs` | `Source/Urho3D/Resource/BinaryFile.h`<br>`Source/Urho3D/Resource/BinaryFile.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Resource/Decompress.cs` | `Source/Urho3D/Resource/Decompress.h`<br>`Source/Urho3D/Resource/Decompress.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Resource/Graph.cs` | `Source/Urho3D/Resource/Graph.h`<br>`Source/Urho3D/Resource/Graph.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Resource/GraphNode.cs` | `Source/Urho3D/Resource/GraphNode.h`<br>`Source/Urho3D/Resource/GraphNode.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Resource/GraphPin.cs` | `Source/Urho3D/Resource/GraphPin.h`<br>`Source/Urho3D/Resource/GraphPin.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Resource/Image.cs` | `Source/Urho3D/Resource/Image.h`<br>`Source/Urho3D/Resource/Image.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Resource/ImageCube.cs` | `Source/Urho3D/Resource/ImageCube.h`<br>`Source/Urho3D/Resource/ImageCube.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Resource/ImageDDS.cs` | `Source/Urho3D/Resource/ImageDDS.h`<br>`Source/Urho3D/Resource/ImageDDS.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Resource/ImageSDL.cs` | **UNRESOLVED — search by class name** | ☐ | ☐ | ☐ |
| `src/RedBolt/Resource/JSONArchive.cs` | `Source/Urho3D/Resource/JSONArchive.h`<br>`Source/Urho3D/Resource/JSONArchive.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Resource/JSONFile.cs` | `Source/Urho3D/Resource/JSONFile.h`<br>`Source/Urho3D/Resource/JSONFile.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Resource/JSONResource.cs` | **UNRESOLVED — search by class name** | ☐ | ☐ | ☐ |
| `src/RedBolt/Resource/JSONResourceBase.cs` | **UNRESOLVED — search by class name** | ☐ | ☐ | ☐ |
| `src/RedBolt/Resource/JSONValue.cs` | `Source/Urho3D/Resource/JSONValue.h`<br>`Source/Urho3D/Resource/JSONValue.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Resource/Localization.cs` | `Source/Urho3D/Resource/Localization.h`<br>`Source/Urho3D/Resource/Localization.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Resource/PListFile.cs` | `Source/Urho3D/Resource/PListFile.h`<br>`Source/Urho3D/Resource/PListFile.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Resource/PListValue.cs` | **UNRESOLVED — search by class name** | ☐ | ☐ | ☐ |
| `src/RedBolt/Resource/Resource.cs` | `Source/Urho3D/Resource/Resource.h`<br>`Source/Urho3D/Resource/Resource.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Resource/ResourceCache.cs` | `Source/Urho3D/Resource/ResourceCache.h`<br>`Source/Urho3D/Resource/ResourceCache.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Resource/ResourceEvents.cs` | `Source/Urho3D/Resource/ResourceEvents.h` | ☐ | ☐ | ☐ |
| `src/RedBolt/Resource/ResourceLoading.cs` | **UNRESOLVED — search by class name** | ☐ | ☐ | ☐ |
| `src/RedBolt/Resource/ResourceRef.cs` | **UNRESOLVED — search by class name** | ☐ | ☐ | ☐ |
| `src/RedBolt/Resource/ResourceRefList.cs` | **UNRESOLVED — search by class name** | ☐ | ☐ | ☐ |
| `src/RedBolt/Resource/ResourceRouter.cs` | **UNRESOLVED — search by class name** | ☐ | ☐ | ☐ |
| `src/RedBolt/Resource/ResourceWithMetadata.cs` | **UNRESOLVED — search by class name** | ☐ | ☐ | ☐ |
| `src/RedBolt/Resource/SerializableResource.cs` | `Source/Urho3D/Resource/SerializableResource.h`<br>`Source/Urho3D/Resource/SerializableResource.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Resource/SimpleResource.cs` | **UNRESOLVED — search by class name** | ☐ | ☐ | ☐ |
| `src/RedBolt/Resource/XMLArchive.cs` | `Source/Urho3D/Resource/XMLArchive.h`<br>`Source/Urho3D/Resource/XMLArchive.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Resource/XMLElement.cs` | `Source/Urho3D/Resource/XMLElement.h`<br>`Source/Urho3D/Resource/XMLElement.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Resource/XMLFile.cs` | `Source/Urho3D/Resource/XMLFile.h`<br>`Source/Urho3D/Resource/XMLFile.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Resource/XPathQuery.cs` | **UNRESOLVED — search by class name** | ☐ | ☐ | ☐ |
| `src/RedBolt/Scene/Animatable.cs` | `Source/Urho3D/Scene/Animatable.h`<br>`Source/Urho3D/Scene/Animatable.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Scene/AnimationDefs.cs` | `Source/Urho3D/Scene/AnimationDefs.h` | ☐ | ☐ | ☐ |
| `src/RedBolt/Scene/Component.cs` | `Source/Urho3D/Scene/Component.h`<br>`Source/Urho3D/Scene/Component.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Scene/ComponentSearchFlag.cs` | **UNRESOLVED — search by class name** | ☐ | ☐ | ☐ |
| `src/RedBolt/Scene/ISceneUpdateListener.cs` | **UNRESOLVED — search by class name** | ☐ | ☐ | ☐ |
| `src/RedBolt/Scene/LogicComponent.cs` | `Source/Urho3D/Scene/LogicComponent.h`<br>`Source/Urho3D/Scene/LogicComponent.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Scene/Node.cs` | `Source/Urho3D/Scene/Node.h`<br>`Source/Urho3D/Scene/Node.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Scene/NodePrefab.cs` | `Source/Urho3D/Scene/NodePrefab.h`<br>`Source/Urho3D/Scene/NodePrefab.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Scene/ObjectAnimation.cs` | `Source/Urho3D/Scene/ObjectAnimation.h`<br>`Source/Urho3D/Scene/ObjectAnimation.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Scene/PrefabReader.cs` | `Source/Urho3D/Scene/PrefabReader.h`<br>`Source/Urho3D/Scene/PrefabReader.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Scene/PrefabReference.cs` | `Source/Urho3D/Scene/PrefabReference.h`<br>`Source/Urho3D/Scene/PrefabReference.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Scene/PrefabResource.cs` | `Source/Urho3D/Scene/PrefabResource.h`<br>`Source/Urho3D/Scene/PrefabResource.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Scene/PrefabTypes.cs` | `Source/Urho3D/Scene/PrefabTypes.h` | ☐ | ☐ | ☐ |
| `src/RedBolt/Scene/PrefabWriter.cs` | `Source/Urho3D/Scene/PrefabWriter.h`<br>`Source/Urho3D/Scene/PrefabWriter.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Scene/Scene.cs` | `Source/Urho3D/Scene/Scene.h`<br>`Source/Urho3D/Scene/Scene.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Scene/SceneEvents.cs` | `Source/Urho3D/Scene/SceneEvents.h` | ☐ | ☐ | ☐ |
| `src/RedBolt/Scene/SceneLookupFlag.cs` | **UNRESOLVED — search by class name** | ☐ | ☐ | ☐ |
| `src/RedBolt/Scene/SceneResolver.cs` | `Source/Urho3D/Scene/SceneResolver.h`<br>`Source/Urho3D/Scene/SceneResolver.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Scene/SceneResource.cs` | `Source/Urho3D/Scene/SceneResource.h`<br>`Source/Urho3D/Scene/SceneResource.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Scene/Serializable.cs` | `Source/Urho3D/Scene/Serializable.h`<br>`Source/Urho3D/Scene/Serializable.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Scene/ShakeComponent.cs` | `Source/Urho3D/Scene/ShakeComponent.h`<br>`Source/Urho3D/Scene/ShakeComponent.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Scene/SplinePath.cs` | `Source/Urho3D/Scene/SplinePath.h`<br>`Source/Urho3D/Scene/SplinePath.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Scene/TrackedComponent.cs` | `Source/Urho3D/Scene/TrackedComponent.h`<br>`Source/Urho3D/Scene/TrackedComponent.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Scene/TransformSpace.cs` | **UNRESOLVED — search by class name** | ☐ | ☐ | ☐ |
| `src/RedBolt/Scene/UnknownComponent.cs` | `Source/Urho3D/Scene/UnknownComponent.h`<br>`Source/Urho3D/Scene/UnknownComponent.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Scene/UpdateEvent.cs` | **UNRESOLVED — search by class name** | ☐ | ☐ | ☐ |
| `src/RedBolt/Scene/ValueAnimation.cs` | `Source/Urho3D/Scene/ValueAnimation.h`<br>`Source/Urho3D/Scene/ValueAnimation.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Scene/ValueAnimationInfo.cs` | `Source/Urho3D/Scene/ValueAnimationInfo.h`<br>`Source/Urho3D/Scene/ValueAnimationInfo.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Scene/WorldOrigin.cs` | `Source/Urho3D/Scene/WorldOrigin.h`<br>`Source/Urho3D/Scene/WorldOrigin.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Scene/WorldOriginUpdateMode.cs` | **UNRESOLVED — search by class name** | ☐ | ☐ | ☐ |

## Outcome notation for future passes

Add one indented note below the relevant row or in a dated follow-up section:

- `VERIFIED`: complete relevant C++ declarations, bodies, fields, and call paths match.
- `DEVIATION`: exact C++ → C# difference, with involved symbols and required faithful port.
- `ALLOWED`: exact AGENTS.md allowed-deviation clause.
- `BLOCKED`: searches performed and the missing authoritative C++ source.
- `N/A`: no class/member in that category, confirmed from the complete C++ class declaration.

## Completion rule

A file is complete only when all ported types in it have been reviewed, including nested types and implicit/defaulted special members. A green build or test is not verification. If C# already matches the initially inspected C++ method but a symptom remains, continue through the authoritative C++ call path until the actual missing or divergent port is found.
