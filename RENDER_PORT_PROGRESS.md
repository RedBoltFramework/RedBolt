# Render Port Progress

Keep this file concise. Remove stale guesses, completed low-value notes, and superseded plans.

## Goal

Incrementally port rbfx `Source/Urho3D/RenderAPI` and `Source/Urho3D/RenderPipeline` into RedBolt without losing 1:1 behavior or required hookups.

## Current State

- RedBolt has a managed renderer backed by generated SDL and Diligent bindings, but it is still transitioning from direct `Graphics` ownership to rbfx-style `RenderAPI` and `RenderPipeline` architecture.
- The managed global `Context` is active: `Engine` owns it, subsystems use concurrent registration and lookup, and named events are main-thread-only.
- No manual native bindings are permitted. Missing dependency access must be fixed through the generator or SharpCpp.

## Core Port Checkpoint

- `AttributeInfo` now matches the available `Core/Attribute.h` value contracts more closely: accessor-backed attributes, cloning, typed metadata fallback, enum name conversion, scope hints, and safe empty `AttributeHandle` calls are covered.
- The remaining `AttributeInfo` gap is the wider rbfx `Variant`-typed metadata/default-value representation; it depends on completing the managed `Variant` surface.
- `Context` now has rbfx-style explicit `StringHash` subsystem registration, lookup, removal, cache access, and managed reflection type-name/attribute forwarding in addition to its existing generic managed APIs.
- `Object` now forwards rbfx dynamic subsystem lookup and full subsystem-cache access through the managed global `Context`.
- `Variant` now includes the explicit RedBolt-only `CString` extension after all upstream enum values. It preserves `CString` instances for native-pointer consumers and serializes as UTF-8 text; resource reference string output now follows rbfx's `type-name;payload` form, including an empty-list trailing separator.
- Managed `Variant` getters now follow rbfx conversion/default behavior instead of throwing on type mismatch, including numeric conversions, quaternion/matrix identity defaults, `Vector4` to `Color` aliasing, and no-allocation empty collection defaults. `Get<T>()` dispatches to the same contracts without boxing normal value types.
- `Variant.ToString()` now matches rbfx's Context dependency for resource-reference string serialization: no active Context produces an empty string, while an active Context enables registered type-name output.
- `Variant.Convert(...)` and immutable `Variant.FromString(...)` now cover rbfx's scalar conversion behavior plus string parsing for scalars, vectors, colors, quaternions, rectangles, integer vectors, matrices, buffers, resource references, and the RedBolt `CString` extension. The rbfx `VoidPtr` contract is represented by `IntPtr`; RefCounted pointers, custom values, and curves remain partial.
- `Variant.Lerp(...)` now follows rbfx's type-directed interpolation for numeric values, vectors, quaternions, colors, integer rectangles, and integer vectors; unsupported types return the left-hand value unchanged.
- `Variant` now accepts `VectorBuffer` as rbfx does and returns copied `VectorBuffer` views of stored buffers. `byte[]` construction now copies input storage, matching rbfx `VariantBuffer` value semantics.
- `ObjectReflectionRegistry` now manages C# `Type`-backed reflection records, factories, categories, attribute ownership, metadata, lookup/removal notifications, and generic attribute shortcuts. `Context` derives from it, while reflection identity uses managed type names instead of rbfx static type methods.
- `Object` now exposes instance type name/hash, C# assignability-based type checks, category through managed `Type` metadata, and event-type-specific unsubscription through the allowed struct-event model. Sender-specific handlers take priority over a receiver's non-specific handler, matching rbfx behavior without `URHO3D_OBJECT`-style static helpers.
- `Variant`, `Object`/`Context`, and `WorkQueue` remain partial Core items and must continue to be reconciled directly against rbfx before marking complete.

## Verification

- `cmake --preset dev`, `cmake --build --preset Debug --target restore`, and `cmake --build --preset Debug --target RedBolt` passed on 2026-07-10.
- `dotnet test tests/RedBolt.Tests/RedBolt.Tests.csproj --configuration Debug` passed: 528 tests.
- `out/build/dev/bin/Debug/RedBolt.Sample.StaticScene --timeout 2 --screenshot-on-exit /tmp/screenshot.png` produced the expected non-black mushroom scene.

## Next Useful Checks

- Continue partial Core reconciliation with `Variant`, then `Object`/`Context` reflection and event-receiver behavior, preserving the allowed managed event model.
- Continue moving graphics ownership behind `RenderContext`, `DrawCommandQueue`, `ConstantBufferCollection`, and `PipelineStateCache`, then replace direct `Graphics.RenderScene` rendering with pipeline-owned processors and pass flow.
