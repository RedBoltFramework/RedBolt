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
| `src/RedBolt/Container/Array2D.cs` | `Source/Urho3D/Container/Array2D.h` | ☐ | ☐ | ☐ |
| `src/RedBolt/Container/CString.cs` | **UNRESOLVED — search by class name** | ☐ | ☐ | ☐ |
| `src/RedBolt/Container/ConstString.cs` | `Source/Urho3D/Container/ConstString.h` | ☐ | ☐ | ☐ |
| `src/RedBolt/Container/IndexAllocator.cs` | `Source/Urho3D/Container/IndexAllocator.h` | ☐ | ☐ | ☐ |
| `src/RedBolt/Container/KeyFrameSet.cs` | `Source/Urho3D/Container/KeyFrameSet.h` | ☐ | ☐ | ☐ |
| `src/RedBolt/Container/ManagedSequence.cs` | **UNRESOLVED — search by class name** | ☐ | ☐ | ☐ |
| `src/RedBolt/Container/MultiVector.cs` | `Source/Urho3D/Container/MultiVector.h` | ☐ | ☐ | ☐ |
| `src/RedBolt/Container/StringHash.cs` | `Source/Tests/Math/StringHash.cpp`<br>`Source/Urho3D/Math/StringHash.cpp`<br>`Source/Urho3D/Math/StringHash.h` | ☐ | ☐ | ☐ |
| `src/RedBolt/Core/Attribute.cs` | `Source/Urho3D/Core/Attribute.h` | ☐ | ☐ | ☐ |
| `src/RedBolt/Core/CommandLineParser.cs` | **UNRESOLVED — search by class name** | ☐ | ☐ | ☐ |
| `src/RedBolt/Core/Context.cs` | `Source/Urho3D/Core/Context.h`<br>`Source/Urho3D/Core/Context.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Core/CoreEvents.cs` | `Source/Urho3D/Core/CoreEvents.h` | ☐ | ☐ | ☐ |
| `src/RedBolt/Core/CustomVariantValue.cs` | **UNRESOLVED — search by class name** | ☐ | ☐ | ☐ |
| `src/RedBolt/Core/EventHandler.cs` | **UNRESOLVED — search by class name** | ☐ | ☐ | ☐ |
| `src/RedBolt/Core/Object.cs` | `Source/Urho3D/Core/Object.h`<br>`Source/Urho3D/Core/Object.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Core/ObjectCategory.cs` | `Source/Urho3D/Core/ObjectCategory.h` | ☐ | ☐ | ☐ |
| `src/RedBolt/Core/ObjectExtensionMethods.cs` | **UNRESOLVED — search by class name** | ☐ | ☐ | ☐ |
| `src/RedBolt/Core/ObjectReflection.cs` | `Source/Urho3D/Core/ObjectReflection.h`<br>`Source/Urho3D/Core/ObjectReflection.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Core/ObjectRevisionTracker.cs` | `Source/Urho3D/Core/ObjectRevisionTracker.h` | ☐ | ☐ | ☐ |
| `src/RedBolt/Core/ProcessUtils.cs` | `Source/Urho3D/Core/ProcessUtils.h`<br>`Source/Urho3D/Core/ProcessUtils.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Core/Profiler.cs` | `Source/Urho3D/Core/Profiler.h`<br>`Source/Urho3D/Core/Profiler.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Core/Spline.cs` | `Source/Urho3D/Core/Spline.h`<br>`Source/Urho3D/Core/Spline.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Core/StopToken.cs` | `Source/Urho3D/Core/StopToken.h` | ☐ | ☐ | ☐ |
| `src/RedBolt/Core/StringHashRegister.cs` | `Source/Urho3D/Core/StringHashRegister.h`<br>`Source/Urho3D/Core/StringHashRegister.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Core/SubsystemCache.cs` | `Source/Urho3D/Core/SubsystemCache.h`<br>`Source/Urho3D/Core/SubsystemCache.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Core/ThreadSafeCache.cs` | `Source/Urho3D/Core/ThreadSafeCache.h` | ☐ | ☐ | ☐ |
| `src/RedBolt/Core/Time.cs` | **UNRESOLVED — search by class name** | ☐ | ☐ | ☐ |
| `src/RedBolt/Core/Timer.cs` | `Source/Urho3D/Core/Timer.h`<br>`Source/Urho3D/Core/Timer.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Core/VariantCurve.cs` | `Source/Urho3D/Core/VariantCurve.h`<br>`Source/Urho3D/Core/VariantCurve.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Core/VariantType.cs` | **UNRESOLVED — search by class name** | ☐ | ☐ | ☐ |
| `src/RedBolt/Core/WorkQueue.cs` | `Source/Urho3D/Core/WorkQueue.h`<br>`Source/Urho3D/Core/WorkQueue.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Engine/Application.cs` | `Source/Urho3D/Engine/Application.h`<br>`Source/Urho3D/Engine/Application.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Engine/ApplicationFlavor.cs` | `Source/Urho3D/Engine/ApplicationFlavor.h`<br>`Source/Urho3D/Engine/ApplicationFlavor.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Engine/ConfigFile.cs` | `Source/Urho3D/Engine/ConfigFile.h`<br>`Source/Urho3D/Engine/ConfigFile.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Engine/Engine.cs` | `Source/Urho3D/Engine/Engine.h`<br>`Source/Urho3D/Engine/Engine.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Engine/EngineCommandLine.cs` | **UNRESOLVED — search by class name** | ☐ | ☐ | ☐ |
| `src/RedBolt/Engine/EngineEvents.cs` | `Source/Urho3D/Engine/EngineEvents.h` | ☐ | ☐ | ☐ |
| `src/RedBolt/Engine/EngineParameter.cs` | **UNRESOLVED — search by class name** | ☐ | ☐ | ☐ |
| `src/RedBolt/Engine/EngineParameterBinding.cs` | **UNRESOLVED — search by class name** | ☐ | ☐ | ☐ |
| `src/RedBolt/Engine/EngineParameters.cs` | **UNRESOLVED — search by class name** | ☐ | ☐ | ☐ |
| `src/RedBolt/Engine/StateManager.cs` | `Source/Urho3D/Engine/StateManager.h`<br>`Source/Urho3D/Engine/StateManager.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Engine/StateManagerEvents.cs` | `Source/Urho3D/Engine/StateManagerEvents.h` | ☐ | ☐ | ☐ |
| `src/RedBolt/Graphics/AnimatedModel.cs` | `Source/Urho3D/Graphics/AnimatedModel.h`<br>`Source/Urho3D/Graphics/AnimatedModel.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Graphics/Animation.cs` | `Source/Urho3D/Graphics/Animation.h`<br>`Source/Urho3D/Graphics/Animation.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Graphics/AnimationController.cs` | `Source/Urho3D/Graphics/AnimationController.h`<br>`Source/Urho3D/Graphics/AnimationController.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Graphics/AnimationParameters.cs` | **UNRESOLVED — search by class name** | ☐ | ☐ | ☐ |
| `src/RedBolt/Graphics/AnimationState.cs` | `Source/Urho3D/Graphics/AnimationState.h`<br>`Source/Urho3D/Graphics/AnimationState.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Graphics/AnimationStateSource.cs` | `Source/Urho3D/Graphics/AnimationStateSource.h` | ☐ | ☐ | ☐ |
| `src/RedBolt/Graphics/AnimationTrack.cs` | `Source/Urho3D/Graphics/AnimationTrack.h`<br>`Source/Urho3D/Graphics/AnimationTrack.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Graphics/BillboardSet.cs` | `Source/Urho3D/Graphics/BillboardSet.h`<br>`Source/Urho3D/Graphics/BillboardSet.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Graphics/Camera.cs` | `Source/Urho3D/Graphics/Camera.h`<br>`Source/Urho3D/Graphics/Camera.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Graphics/CameraOperator.cs` | `Source/Urho3D/Graphics/CameraOperator.h`<br>`Source/Urho3D/Graphics/CameraOperator.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Graphics/CubemapRenderer.cs` | `Source/Urho3D/Graphics/CubemapRenderer.h`<br>`Source/Urho3D/Graphics/CubemapRenderer.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Graphics/CustomGeometry.cs` | `Source/Urho3D/Graphics/CustomGeometry.h`<br>`Source/Urho3D/Graphics/CustomGeometry.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Graphics/DebugGeometry.cs` | **UNRESOLVED — search by class name** | ☐ | ☐ | ☐ |
| `src/RedBolt/Graphics/DebugRenderer.cs` | `Source/Urho3D/Graphics/DebugRenderer.h`<br>`Source/Urho3D/Graphics/DebugRenderer.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Graphics/DecalSet.cs` | `Source/Urho3D/Graphics/DecalSet.h`<br>`Source/Urho3D/Graphics/DecalSet.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Graphics/Drawable.cs` | `Source/Urho3D/Graphics/Drawable.h`<br>`Source/Urho3D/Graphics/Drawable.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Graphics/DrawableEvents.cs` | `Source/Urho3D/Graphics/DrawableEvents.h` | ☐ | ☐ | ☐ |
| `src/RedBolt/Graphics/Geometry.cs` | `Source/Urho3D/Graphics/Geometry.h`<br>`Source/Urho3D/Graphics/Geometry.cpp` | ☐ | ☐ | ☐ |
| `src/RedBolt/Graphics/GlobalIllumination.cs` | `Source/Urho3D/Graphics/GlobalIllumination.h`<br>`Source/Urho3D/Graphics/GlobalIllumination.cpp` | ☐ | ☐ | ☐ |
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
