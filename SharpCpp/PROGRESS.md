# SharpCpp Restart Progress

## Goal

Bootstrap an independent SharpCpp line from `doc/`, selectively adapting small bounded pieces from `../SharpCpp`.

## What Works Today

All 5 test suites pass (model, metadata, rendering, E2E, rbfx-E2E) on the last clean verified baseline. The full pipeline — C++ headers → generated C# wrappers + native thunks — runs end-to-end for both synthetic fixtures and the real rbfx SDK.

Current worktree verification passes with generic complex C++ values crossing callback/native ABI as pointers by default, while explicitly configured value marshallers such as `StringHash`, math structs, and smart-pointer projections keep their configured ABI. Thin managed convenience wrappers remain acceptable when they delegate to generated APIs without handwritten native entrypoints; rbfx event subscription ownership is now covered by generated `Object::SubscribeToEvent<T>` wrappers discovered from C++ template instantiations.

### Model & Metadata
- Binding model: records, enums, free functions, methods, constructors, fields, operators, templates/generics
- Metadata export/import (deterministic JSON, suppress-duplicate-emission contract)
- Diagnostics, source references, `CodeWriter`

### Parser (ClangSharp-backed)
- Parses records, enums, free functions, member methods/fields/constructors, operators, class/function templates
- Managed ClangSharp API (`Decl` subtypes) — no raw `CXCursor`/`VisitChildren`
- `[[sharpcpp::ignore]]`, `[[sharpcpp::rename]]`, `[[sharpcpp::director]]`, `[[sharpcpp::flags]]`, `[[sharpcpp::instantiate]]`
- Parse requests can force selected records to be director-capable before member parsing
- Documentation comment extraction (`///`, `/** */`)
- Marshaller config extraction via AST walk (not regex)

### Rendering
- Generated `.Bindings.g.cs`, `.Bindings.Aliases.g.cs`, `.Bindings.g.cpp`
- Fully-qualified C# type names (`global::Ns.Type`)
- Per-group code splitting (one file per header group)
- Unity-build compatible native thunks

### Type System
- Primitives, handles/records, enums, references, pointers
- `Marshaller<T>` / `ReturnMarshaller<T>` for custom type interop
- Wrapped record value returns (`T` returned as an owned generated wrapper handle) without per-type marshaller configuration
- Managed type overrides (`managed_type`, `managed_base_type`, `managed_base_type_constraints`, `managed_static_interfaces`)
- Container wrappers: `vector`, `list`, `map`, `unordered_map`
- Native-backed fixed-size array wrappers: `std::array<T, N>` / `eastl::array<T, N>` → `Std.Array<T>` / `Ea.Array<T>` and native C array fields `T[N]` → `SharpCpp.NativeArray<T>` with runtime length validation
- Native-backed shared dynamic array wrappers: `ea::shared_array<T>` / `eastl::shared_array<T>` → `Ea.SharedArray<T>` with unsafe indexed access
- Math primitives as value types: `Vector3`, `Matrix3`, `StringHash`, etc.
- String support: `const char*`, `std::string_view`, `ea::string_view` / `eastl::string_view` → `ReadOnlySpan<byte>`, `const char**` → `out string`, `std::string` → `StringBase`
- Managed-to-native callback parameters: `std::function<>`, `ea::function<>`, and `eastl::function<>` → `Action<>` / `Func<>` with generated custom marshallers

### Inheritance & Polymorphism
- Single inheritance with managed class hierarchy
- Polymorphic wrapping: runtime type ID, instance caches, `CreateManagedWrapper`
- Director/Managed virtual overrides (native→managed dispatch)
- Multiple inheritance: projected secondary-base methods, managed `I*` interfaces
- Protected member access for director-capable records, including protected handle fields
- Base-call delegation (`callbase_*` entrypoints)

### Generics / Templates
- Generic records (`BoxOf<T>`) and free functions
- Template member methods inside generic records
- Explicit template member methods on non-generic records when concrete instantiations are known
- Explicit instantiation metadata (`[[sharpcpp::instantiate]]`)
- Handle-typed generic arguments

### Runtime Support (`SharpCpp.Support`)
- `NativeObject : SafeHandle` with lifecycle management
- `DefaultNativeInstanceCache`, `NativeObjectRegistry`
- `StringBase : NativeObject, IReadOnlyList<char>`
- `BorrowedNativeHandle` / `OwnedNativeHandle` marker policies for generated handle marshallers
- `Utf8SpanReturnMarshaller` for null-terminated `const char*` returns
- `Utf8SpanAbiMarshaller` / `Utf8SpanAbiReturnMarshaller` for explicit UTF-8 ptr+len span ABI
- Managed callback ABI and native shared-state callback wrapper support for managed-to-native function-object parameters
- Native-backed `ArrayBase<T>` / `SharedArrayBase<T>` wrappers for fixed arrays and shared dynamic arrays

### rbfx Consumer Slice
- `rbfx/SharpCpp.Rbfx.Generator` parses `Urho3DAll.h` against SDK CMake flags
- Optional RmlUI / ImGui SDK submodules are detected from rbfx compile definitions and parsed through an explicit generated umbrella when enabled
- `SharpPtr<T>` / `WeakPtr<T>` projection via marshaller configuration
- `ea::string` / `eastl::string` projected as `Ea.String`
- Cross-module composition: `rbfx/Rbfx.UserCode` imports upstream metadata, extends wrappers
- rbfx inheritable SDK classes are marked director-capable at parse time so abstract constructors can support managed subclasses without weakening the core abstract-constructor gate
- rbfx macro headers are molded into managed-only AST nodes by the rbfx generator and rendered by the normal grouped renderer, generating surfaces like `CoreEvents`, `EngineDefs`, and `EngineEvents`
- rbfx accessor pairs are promoted to generated C# properties through configured marshaller patterns, with reserved SDK getters and projected secondary-base methods kept as ordinary methods
- RmlUI bootstrap generation keeps the narrow data-model/document surface needed by the sample, including generated `Context.CreateDataModel(...)` and `ElementDocument.Show(...)`
- RmlUI `Variant` string access now uses generated explicit template instantiations (`Assign<Ea.String>` / `Get<Ea.String>`) instead of custom `SetString` / `GetString` bridge exports

### Infrastructure
- Structured logging (`ILogger` with elapsed timestamps)
- The `SharpCpp.Native` dummy C# project can configure/build either SharpCpp's standalone CMake tree or a consumer-owned CMake tree through configurable source, preset, binary-directory, target, and optional-graph properties. Embedded consumers can disable the synthetic E2E and rbfx native graphs while building the exported `sharpcpp-native` target in their own tree. Native configure/build invocations share a cross-process lock so multi-platform solution builds cannot race in shared dependency sources.
- Degradation pass: auto-ignores unsupported constructs with diagnostic reasons
- Record reference return support (`T&` → handle ABI)
- Type alias pipeline (`using` / `typedef` → metadata-backed C# using directives)
- rbfx `FlagSet<TEnum>` lowering to public `[Flags]` enum surfaces

### Current Audit

- `global::SharpCpp.Support.NativeObject.GetHandle(...)` renderer usage has been reviewed against generated managed-handle marshaller support. The live renderer already passes plain wrapper parameters for non-ref/out handle imports when `CanUseManagedHandleParameterImport(...)` applies, including generic parameters substituted with concrete handle types. Remaining renderer uses are currently required for custom marshaller bodies, callback/native handle returns, explicit `nint self` imports, ref/out fallback paths, or generic array element setters whose imports still use raw handle ABI.

## Remaining Work

### Parser Breadth
- [ ] Additional template shapes beyond current fixture patterns
- [ ] Richer default-argument forms and attribute-placement variants
- [ ] More real-world header patterns (the parser works on rbfx SDK but hasn't been stress-tested broadly)

### Generic Breadth
- [ ] Additional generic member shapes beyond single-parameter method templates
- [ ] Polymorphic-generic combinations (generic records with virtual methods / directors)
- [ ] Richer phase-2 metadata for generic surfaces

### Multiple Inheritance
- [ ] Fuller interface inheritance policy for secondary bases
- [ ] Virtual-base inheritance (currently explicitly unsupported)
- [ ] Edge cases: protected/virtual/secondary-base interactions

### Validation & Degradation
- [x] Placeholder records now use `GetManagedTypeHead` to strip `::` qualifiers from `ManagedName`
- [x] Remaining leak: nested generic argument type names (e.g. `Urho3D::BaseVector3<float>` inside `Ea.Vector<...>`) now properly caught via placeholder-record detection and post-instantiation `HasUnresolvedNamespaceColons` check
- [x] More explicit early diagnostics for unsupported combinations — placeholder record types are now flagged in both degradation and validation passes
- [x] Const/non-const method overloads that collapse to the same managed callable signature are silently pruned, including const overloads already marked with duplicate-surface degradation after their non-const counterpart received a primary unsupported-surface reason
- [x] rbfx `FlagSet<T>` bitwise helper operators are silently excluded before blocked-type dependency degradation, avoiding generated unsupported-operator comments
- [x] rbfx `ResourceRef` by-value returns are supported via owned-handle marshaller metadata instead of record-value degradation
- [ ] Hardened fail-fast gates before render/native compile (improved: C++ compile errors dropped from 222 to 160 in rbfx SDK; remaining are pre-existing)

### Cross-Module Composition
- [ ] Validate downstream native support requirements for imported surfaces
- [ ] Extend beyond the first `rbfx/Rbfx.UserCode` → `SharpCpp.Rbfx.Bindings` slice

### Type System Growth
- [ ] Additional container families (set, deque, etc.)
- [ ] Native C array fields with handle-backed elements need per-element conversion semantics before they can be safely exposed
- [ ] Return-string lifetime: evaluate `ReadOnlySpan<char>` for `const char*` returns
- [ ] Custom C# marshaller for `IntPtr`→`string` P/Invoke (replacing renderer-side `PtrToStringUTF8`)

## Key Decisions

- `SharpCpp.*` managed assembly identities, `sharpcpp-*` CMake targets
- `SharpCpp.slnx` is the primary build graph; CMake is a native-only auxiliary invoked by the SDK-style `SharpCpp.Native` project
- all build outputs are rooted under `build/`, with CMake trees at `build/native/<preset>` and shared runtime artifacts at `build/bin/<Configuration>`
- native consumers may embed SharpCpp with `add_subdirectory(...)` and link `SharpCpp::NativeSupport` without enabling the rbfx or E2E development graph
- Independent from `../SharpCpp`; reuse is copy/adapt only
- Project split follows `doc/11-project-shape-blueprint.md`
- ClangSharp-backed parser (not permanently hardcoded model)
- Attribute-driven customization writable through C# AST API
- Landed polymorphic wrapping + directors before template/generic projection
- All type-specific behaviors configured through C++/C# marshallers, never hardcoded in core

## Recently Fixed

- **The build is C#-centric and directly consumable**:
  - Central MSBuild properties place managed/native outputs, intermediates, restore state, generated files, and package artifacts below `build/`.
  - Generated binding projects depend on one `SharpCpp.Native` orchestration project; ordinary `ProjectReference` edges build all generator tools before its single `sharpcpp-native` CMake invocation.
  - CMake no longer restores or builds managed projects. Its root is small, build responsibilities live in directory-local `CMakeLists.txt` files, and embedded use exposes `SharpCpp::NativeSupport` without requiring .NET or an rbfx SDK.
  - A clean `dotnet build SharpCpp.slnx --configuration Debug` now produces managed bindings plus synthetic, rbfx, and user-code native prerequisites in the correct order.

- **Full generated-source builds and isolated test outputs are green**:
  - CMake now treats generated binding stamps as configure dependencies, so generated `.g.cpp` files are compiled in the same build instead of leaving stub-only native libraries with missing exports.
  - Test projects use isolated output directories, the default build stages their native runtime libraries, and E2E tests receive their generated-output roots explicitly.
  - Parser and renderer fixes cover concurrent Clang parsing, protected flattened-field access, explicit protected constructors, token-spaced lambda and enum defaults, and nested field ABI marshalling.
  - Unsupported-default convenience overloads preserve ordinary optional prefix parameters while retaining unexpressible parameters as required when necessary.
  - Verification: `dotnet build SharpCpp.slnx --configuration Debug` and `dotnet test SharpCpp.slnx --configuration Debug --no-build --no-restore` pass all five suites.

- **Generic method defaults now preserve native `T()` semantics through nullable sentinels**:
  - Generic method wrappers now render unexpressible default parameters such as C++ `T default_value = T()` as nullable optional managed parameters, e.g. `T? defaultValue = default`.
  - A `null` nullable sentinel on an `UnexpressibleDefault` generic parameter dispatches to generated `__ArgsN` P/Invoke/native thunks so the C++ template call omits the argument and applies the native `T()` default instead of passing a null configured value such as `Ea.String`.
  - Explicit non-null values still dispatch through the full generic thunk; generated native default thunks use the required C++ `template` disambiguator for dependent member-template calls.
  - Added rendering regression coverage for `Rml::Variant::Get<T>(T default_value = T())` on a non-generic record.
  - Verification: focused rendering regression, `cmake --build --preset Debug`, and `ctest --preset Debug` pass.

- **SDK-style NuGet packaging pipeline for rbfx bindings was added**:
  - Added `packaging/nuget/package.sh` with SDK download/extract, staging, package creation, and local-repository actions. Package creation uses SDK-style `.csproj` package projects and `dotnet pack`, not `.nuspec`.
  - Added packages for `SharpCpp.Rbfx.Bindings`, `SharpCpp.Rbfx.Bindings.runtime.<rid>`, `SharpCpp.Rbfx.CoreData`, `SharpCpp.Rbfx.Data`, and `SharpCpp.Rbfx.EditorData`.
  - Runtime packages stage native files under `runtimes/<rid>/native/`, include the source generator analyzer directly, and depend on the matching managed bindings package version.
  - Package target-framework paths are derived from the repository `TargetFramework` property and can be overridden with `--target-framework`, avoiding hardcoded `net8.0` package layout changes on runtime upgrades.
  - Added `sharpcpp-rbfx-sourcegenerator` and `sharpcpp-package-rbfx-inputs` CMake targets plus package-focused CMake build presets.
  - Hardened rbfx SDK packaging on non-Debian Linux hosts by normalizing the SDK's hardcoded DBus CMake export from `/usr/lib/x86_64-linux-gnu/libdbus-1.so` to `dbus-1` when `pkg-config` can resolve DBus.
  - Made alias pre-scans tolerate ClangSharp child declaration materialization failures, fixing parsing of the refreshed rbfx SDK release headers.
  - Verification: `./packaging/nuget/package.sh create --version 0.0.0-dev --rid linux-x64 --skip-fetch` produced all five packages, a temporary consumer restored and built against `SharpCpp.Rbfx.Bindings.runtime.linux-x64`, `cmake --build --preset Debug` passes, and `ctest --preset Debug` passes.

- **Managed wrapper instance caches are now selected by actual native type**:
  - `CreateManagedWrapper` is now a thin null-check plus `NativeObjectRegistry.Create<T>(...)`; polymorphic paths no longer probe the static/root wrapper cache before native metadata resolution.
  - `NativeObjectRegistry.Create<T>` now owns cache lookup and registration for wrappers it creates, using native dynamic type metadata to select the resolved type cache before creating a wrapper.
  - Generated handle constructors no longer register in instance caches, so generated base constructors cannot insert derived objects into base/root caches. Public managed constructors register through `NativeObjectRegistry.RegisterInstance<T>(this)` after native construction.
  - Generated dynamic-type registration uses a module initializer instead of explicit `EnsureRegistered()` calls on the wrapper hot path.
  - Verification: `dotnet test tests/SharpCpp.Tests.Rendering/SharpCpp.Tests.Rendering.csproj`, `cmake --build --preset Debug`, `dotnet test tests/SharpCpp.Tests.E2E/SharpCpp.Tests.E2E.csproj`, and `ctest --preset Debug` pass.

- **Marshaller sync-back now belongs to the marshaller lifetime and field access goes through invoke helpers**:
  - `ConvertToCpp` / `ConvertToAbi` marshaller methods are non-`const` and `noexcept`; invalid ABI fail-fast paths now call `std::terminate()` with the previous diagnostic strings preserved as comments.
  - `ParameterMarshaller<T>` wraps configured marshallers in an RAII adapter that calls `Sync()` from the destructor when present, so generated thunks no longer emit explicit `detail::SyncMarshaller(...)` calls.
  - Removed explicit sync helper machinery from `MarshallerSupport.hpp`; invocation helpers now rely on scoped marshaller destruction for sync-back.
  - Field getters/setters now render through `MarshallerInvoker::get_field` / `set_field`, including protected director helpers, fixed arrays, and borrowed-storage fields. Protected helpers take member pointers through the director type so inherited protected fields remain C++-access legal.
  - Protected method helpers now render through `MarshallerInvoker::invoke_*` / `invoke_owned_*` instead of manually constructing parameter and return marshallers; the rbfx `Application.__call_HandleLogMessage(...)` helper now dispatches through `invoke_instance` directly.
  - Verification: `dotnet test tests/SharpCpp.Tests.Model/SharpCpp.Tests.Model.csproj --logger "console;verbosity=minimal"`, `dotnet test tests/SharpCpp.Tests.Rendering/SharpCpp.Tests.Rendering.csproj --logger "console;verbosity=minimal"`, `cmake --build --preset Debug`, `ctest --preset Debug`, and `git diff --check` pass.

- **Alias-backed value-type parameter coverage is no longer skipped**:
  - Re-enabled `Bootstrap_pipeline_preserves_alias_backed_value_type_parameters_and_emits_alias_metadata`; the parser now preserves enough alias identity through the standard parse/bootstrap path for `Urho3D::Vector3` value-type parameters and alias metadata to validate normally.
  - Verification: focused test and full model suite pass with `Skipped: 0`.

- **Generated rbfx `Object.SubscribeToEvent(...)` overloads replaced the manual event-subscription ownership helper**:
  - Removed the handwritten `EventHandler.TransferToNativeOwner()` / `Object.SubscribeToEvent(...)` helper path; generated wrappers now call native `Urho3D::Object::SubscribeToEvent<T>`, which allocates and transfers `EventHandler` ownership to the native `Object` event list.
  - Added rbfx parse-umbrella explicit template instantiations for both native `SubscribeToEvent<Urho3D::EventHandler::HandlerFunction>` overloads, with parser discovery from ClangSharp `FunctionTemplateDecl.Specializations` AST facts rather than file prescans.
  - Function-template specialization arguments now map through ClangSharp `CXType` facts and declaration-qualified record/enum identities, avoiding ambiguous text-spelling matches when multiple namespaces contain the same type name.
  - Removed the parser's broad known-type text fallback that scanned previously parsed member type spellings; template argument identity now comes from AST type facts plus configured marshaller/type metadata.
  - Relaxed bootstrap validation and extended generic rendering so non-generic records can emit concrete overloads/imports for supported generic method instantiations.
  - Fixed concrete generic callback import helper naming to normalize through public alias-shaped callback signatures, so `EventHandler` construction and generated `SubscribeToEvent` imports share the same emitted callback marshaller.
  - Fixed pointer-qualified record-value generic returns so `Variant*` collection helpers use borrowed `ret_t<Urho3D::Variant*>` / `MarshallerInvoker::invoke_as` instead of owned `Variant**` ABI.
  - Verification: `dotnet test tests/SharpCpp.Tests.Model/SharpCpp.Tests.Model.csproj --logger "console;verbosity=minimal"`, `dotnet test tests/SharpCpp.Tests.Rendering/SharpCpp.Tests.Rendering.csproj --logger "console;verbosity=minimal"`, `cmake --build --preset Debug --target sharpcpp-rbfx-application sharpcpp-rbfx-tests-e2e-managed`, and `ctest --preset Debug` pass.

- **`Rbfx.Application` no longer double-deletes subscribed update event handlers on shutdown**:
  - Added additive rbfx `Object.SubscribeToEvent(...)` helpers that mirror the native template helpers: create an `EventHandler`, call generated `SubscribeToEventManual(...)`, then mark the managed wrapper as transferred to native ownership.
  - Added an `EventHandler.TransferToNativeOwner()` partial that severs the managed SafeHandle without deleting the native handler, because `Urho3D::Object` owns and deletes subscribed handlers from its intrusive list.
  - Updated the sample application to use `SubscribeToEvent(CoreEvents.Update, OnUpdate)` instead of storing and disposing an owning `EventHandler` wrapper.
  - Added rbfx E2E coverage that subscribes, sends an event, and disposes the receiver without corrupting the native event-handler list.
  - Verification: focused event-handler E2E, `cmake --build --preset Debug --target sharpcpp-rbfx-application sharpcpp-rbfx-tests-e2e-managed`, `git diff --check`, and `ctest --preset Debug` pass.

- **Default native marshalling now uses pointer ABI for complex C++ values**:
  - Replaced the temporary RmlUI-specific `DataModelHandle` callback fix with a generic default `Marshaller<T>` / `ReturnMarshaller<T>` rule: scalar, enum, and pointer types pass by value; unconfigured complex class/record values pass by pointer; unconfigured complex value returns allocate owned storage.
  - Explicit marshaller configurations still win. `ReturnMarshaller<T>` now uses the generic owned-return rule only for the primary default marshaller; configured value types such as `Urho3D::StringHash` and projected families such as `Urho3D::SharedPtr<T>` / `WeakPtr<T>` keep their configured scalar/pointee-pointer ABI.
  - Added compile-time ABI assertions for default complex values, `StringHash` returns, and rbfx smart-pointer projection returns.
  - Verified generated Rml `DataEventFunc` callbacks now receive `DataModelHandle` as `nint arg0` and wrap it through `DataModelHandle.CreateManagedWrapper(arg0, ownsHandle: false)` without any Rml-specific native marshaller header.
  - Verification: `dotnet test tests/SharpCpp.Tests.Rendering/SharpCpp.Tests.Rendering.csproj --logger "console;verbosity=minimal"`, focused rbfx E2E regressions, `cmake --build --preset Debug`, and `ctest --preset Debug` pass.

- **Managed rbfx RTTI source generation no longer skips user subclasses with inherited native registration**:
  - Tightened `ManagedObjectRttiGenerator` so inherited `INativeObjectRegistration<>` interfaces and inherited RTTI methods do not count as direct/manual implementations on user subclasses.
  - Confirmed `GeneratedManagedApplication.ManagedObjectRtti.g.cs` is emitted with instance RTTI overrides and static managed type-info registration for managed `Application` subclasses.
  - Verification: focused managed RTTI E2E, `cmake --build --preset Debug`, and `ctest --preset Debug` pass.

- **Generic record-value returns now use the owned handle ABI after type substitution**:
  - Generic rendering now decides return ownership from the substituted concrete return type, so `TValue` returns such as `Urho3D::Variant` in `Ea.UnorderedMap<TKey, TValue>.GetValueCore` emit the same owned managed return marshaller as non-generic record-value returns.
  - Native generic thunks now export `arg_t<T*>` and dispatch through `MarshallerInvoker::invoke_owned*` for substituted record-value returns, matching the non-generic ABI and avoiding by-value/native-object P/Invoke mismatches.
  - Added rbfx E2E coverage for reading `Ea.UnorderedMap<StringHash, Variant>` event data directly and through `EventHandler.Invoke`, reproducing and fixing the previous native crash.
  - Verification: rendering tests, `cmake --build --preset Debug`, focused rbfx E2E regression, and `ctest --preset Debug` pass.

- **Inherited protected field access now binds through derived director entrypoints**:
  - Derived director records now render protected field helpers for inherited protected fields and expose hidden managed protected properties that call derived-record native entrypoints.
  - Protected field access remains high-performance: each native thunk performs one director cast to the accessor record instead of scanning possible derived directors or referencing director classes across generated C++ groups.
  - Fixed the `DemoApplication : Application : Object` case where accessing inherited `Object.Context` from a managed subclass could hit the director-backed-instance safeguard despite the native instance being an `Application` director.
  - Verification: focused rendering tests, `cmake --build --preset Debug`, and `ctest --preset Debug` pass.

- **Pointer/reference decisions no longer use C++ spelling checks in protected field and generic helper paths**:
  - Removed protected-field pointer spelling checks; native protected field helper planning now uses the field storage type directly and borrows storage only for non-pointer handle fields with marshaller-backed wrapper facts.
  - Added `FieldBinding.IsReference`, `FieldBinding.IsPointer`, and `MarshallerDescriptor.CppIsReference`, populated parser reference/pointer branches from ClangSharp type facts, and used those facts in degradation, protected field planning, and forwarding-reference generic rendering.
  - Replaced fixed-array handle-element detection with `BindingTypeKind.Handle` instead of `CppTypeName.EndsWith("*")`.
  - Added regression coverage for AST reference-field degradation and marshaller-backed pointer fields using normal ABI handling instead of borrowed storage.
  - Verification: focused tests, full model/rendering tests, `cmake --build --preset Debug`, and `ctest --preset Debug` pass.

- **Legacy UTF-8 pointer return fallback no longer has a core model predicate**:
  - Removed `MarshallerDescriptor.RequiresUtf8Conversion`, which inferred string conversion policy from `PublicTypeName == "string"` inside the core model.
  - Kept the existing `Marshal.PtrToStringUTF8` fallback localized in rendering for legacy `marshaller_type`-only return shapes until a support custom marshaller contract replaces it.
  - Added renderer regression coverage for the legacy fallback so future marshaller work can intentionally remove it.
  - Verification: focused UTF-8 renderer tests and full rendering tests pass.

- **Unresolved managed C++ namespace detection is centralized on the binding type model**:
  - Added `BindingType.HasUnresolvedManagedNamespaceSeparators` as the single structural check for public managed type names that still contain C++ `::` separators outside the valid `global::` prefix.
  - Removed duplicate helpers from validation, degradation, and generic rendering code paths.
  - Added direct model coverage for nested generic arguments with valid `global::` type names and invalid unmapped C++ spellings.
  - Verification: focused model tests, full model tests, and full rendering tests pass.

- **Generic validation no longer hardcodes rbfx/STL namespace roots in the core pipeline**:
  - Replaced the `Urho3D` / `ea` / `eastl` / `std` / `SharpCpp` allowlist in `ValidateBootstrapSurfacePass` with namespace-prefix checks derived from `BindingCompilation.NamespaceMappings`.
  - Preserved the validation boundary for suppress-emission placeholders: mapped native namespaces are allowed, unmapped namespaces are filtered, and non-suppressed placeholders still fail regardless of mappings.
  - Added regression coverage for mapped nested namespaces, unmapped suppress-emission placeholders, and non-suppressed placeholders.
  - Verification: focused validation tests and full model tests pass.

- **Protected wrapper field storage no longer depends on C++ container-name substrings in the core model**:
  - Removed `MarshallerDescriptor.RequiresBorrowedFieldStorage`, which inferred storage policy from `vector` / `list` / `map` C++ spelling fragments.
  - Renderer field planning now uses explicit wrapper facts already present on resolved records (`MarshallerExposedMethod`) instead of asking the model to classify concrete C++ type families.
  - Added regression coverage that borrowed protected field storage requires wrapper metadata and that a `custom_map_like` C++ spelling does not trigger borrowed storage by substring.
  - Verification: focused rendering regression tests pass.

- **`Rbfx.Application` RmlUI text binding setup no longer uses one-line wrapper callbacks**:
  - Replaced the separate `Bind*Value` / `SetNoteValue` methods with direct text-buffer binding helpers in the data-model setup.
  - Verification: `cmake --build --preset Debug` and `ctest --preset Debug` pass.

- **`Rbfx.Application` keeps RmlUI text state in reusable `Ea.String` buffers**:
  - Replaced per-value managed `string` RmlUI backing fields with disposed `Ea.String` instances so data-model getters can assign native text directly via generated `Rml.Variant.Assign<Ea.String>`.
  - Updated Rml note/event setters to copy generated `Variant.Get<Ea.String>` results into the existing buffer instead of replacing managed strings.
  - Verification: `cmake --build --preset Debug` and `ctest --preset Debug` pass.

- **RmlUI/ImGui ownership is header-path based and Rml Variant string APIs now use generated template instantiations**:
  - `RbfxRmlUiSurfacePass` and `RbfxImGuiSurfacePass` classify third-party declarations by parsed `HeaderIncludePath`; name allowlists now remain conservative exposure policy, not ownership detection.
  - Added generated explicit `Rml::Variant` string template instantiations for public `Variant::operator=<T>` and `Variant::Get<T>`, yielding `Variant.Assign<Ea.String>(...)` and `Variant.Get<Ea.String>(...)` without a custom Rml value marshaller header.
  - Extended generic method rendering so non-generic records can emit explicit generic member method instantiations, including forwarding-reference signature collapse for `T&&` with reference type arguments.
  - Removed `rbfx/include/SharpCpp/detail/RbfxRmlValueMarshallers.hpp` from the rbfx generator inputs and deleted the file.
  - Updated `Rbfx.Application` to consume generated `Rml.Variant.Assign<Ea.String>` / `Get<Ea.String>` instead of `SetString` / `GetString`.
  - Verification: focused `RbfxSubmoduleSurfacePassTests`, focused renderer regression, `cmake --build --preset Debug`, and `ctest --preset Debug --output-on-failure` all pass.

- **Generated RmlUI document/data-model APIs replaced stale manual P/Invoke conveniences**:
  - Narrowed `RbfxRmlUiSurfacePass` to keep `Rml::DataTypeRegister`, `Rml::ModalFlag`, and `Rml::FocusFlag`, allowing the normal generator to emit `Rml.Context.CreateDataModel(...)` and `Rml.ElementDocument.Show(...)` from SDK headers.
  - Allowed the public Rml callback aliases `Rml::DataGetFunc`, `Rml::DataSetFunc`, and `Rml::DataEventFunc` through the Rml bootstrap surface, so `DataModelConstructor.BindFunc(...)` and `BindEventCallback(...)` now generate through the normal callback marshaller path.
  - Earlier transitional `Variant.SetString(Ea.String)` / `Variant.GetString()` helpers were removed; the canonical surface is now generated from explicit public template instantiations.
  - Deleted the stale `RmlUiConvenience.cs` manual P/Invoke file; its two imports either lacked a native implementation or are now generated.
  - Removed generated-covered handwritten Rml data-model dirty-variable P/Invokes/native exports and kept only generated `Ea.String` APIs.
  - Removed the unused handwritten `DataModelConstructor_GetModelHandle` native export because generated `GetModelHandle()` is now the canonical surface.
  - Deleted the remaining handwritten Rml `BindFunc`/`BindEventCallback`, variant string, and variant-list native bridge files; `VariantList` is now consumed as the generated `Ea.Vector<Rml.Variant>` alias.
  - Remaining runtime bridges are WorkQueue callbacks, ref-counted runtime, and managed type-info registry.
  - Verification: focused `RbfxSubmoduleSurfacePassTests`, `cmake --build --preset Debug`, and `ctest --preset Debug --output-on-failure` all pass.

- **rbfx accessor pairs now generate C# properties instead of requiring handwritten conveniences**:
  - Added `GenerateAccessorPropertiesPass` to annotate coherent getter/setter pairs as renderer properties without adding type-specific core rules.
  - Configured the rbfx generator with v1-style `Get`/`Set` and `Is`/`Set` patterns, plus reserved getter filtering for APIs that must remain methods such as type-info and dirty-state calls.
  - Suppressed property promotion for projected secondary-base methods so generated interfaces and concrete projected methods stay contract-compatible.
  - Removed generated-covered handwritten helpers for `Viewport.Scene` / `Viewport.Camera`, `Node.Position`, `RmlUI.RmlContext`, `RmlUI.IsHoveredValue`, and the renderer default-zone fog-color shim now expressible as generated `Renderer.DefaultZone.FogColor`.
  - Updated handwritten `Variant` conversions and rbfx E2E surface assertions to consume generated properties such as `Variant.Int`, `UIElement.Name`, `EventHandler.EventType`, and `Spline.InterpolationMode`.
  - Verification: `dotnet test tests/SharpCpp.Tests.Model/SharpCpp.Tests.Model.csproj --configuration Debug --no-restore`, `dotnet test tests/SharpCpp.Tests.Rendering/SharpCpp.Tests.Rendering.csproj --configuration Debug --no-restore`, `cmake --build --preset Debug`, and `ctest --preset Debug --output-on-failure` all pass.

- **rbfx macro constants/events now generate through AST molding**:
  - Added neutral managed-only model nodes (`ManagedClassBinding`, `ManagedFieldBinding`) and renderer support so generated C# helper surfaces are ordinary `BindingCompilation` members instead of post-render fragments.
  - Added `RbfxNamespaceMacroSurfacePass` to scan SDK headers for `URHO3D_GLOBAL_CONSTANT`, `URHO3D_EVENT`, and `URHO3D_PARAM`, deriving managed classes from header paths such as `Urho3D/Engine/EngineDefs.h` -> `Urho3D.Engine.EngineDefs`.
  - Constants render as managed `const string` fields, preserving existing consumers such as `EngineDefs.FullScreen` without adding native thunks or type-specific core rules.
  - Events render as static event instances plus nested event wrapper classes deriving from `Urho3D.Detail.EventDefinition`, with parameter `StringHash` fields.
  - Macro classes now avoid collisions with real generated records/enums by suffixing colliding names with `Macros` (for example `SerializableMacros`, `DragDropPayloadMacros`).
  - Added renderer unit coverage for managed-only AST output and rbfx E2E generated-surface assertions for `EngineDefs` / `EngineEvents`.
  - Fixed the generated-binding follow-through after deleting old helpers: marshaller-exposed collection/string core methods are no longer degraded for external namespace roots, marshaller-configured wrappers get generated default constructors for allocation, `Ea.String(string)` chains through generated construction instead of a handwritten native export, and `Rbfx.Application` subscribes to `CoreEvents.Update` via generated `EventHandler` / `SubscribeToEventManual`.
  - Verification: `dotnet test tests/SharpCpp.Tests.Model/SharpCpp.Tests.Model.csproj`, `dotnet test tests/SharpCpp.Tests.Rendering/SharpCpp.Tests.Rendering.csproj`, `cmake --build --preset Debug`, and `ctest --preset Debug --output-on-failure` all pass.

- **rbfx handwritten helper layout now mirrors generated ownership instead of dumping domain helpers into `Interop`**:
  - Moved additive helpers from `Interop` into their owning domains: `Core` (`Context`, `Object`, `ObjectReflection`, `WorkQueue`), `Container` (`Ea.String` construction/formatting), `Engine` (`Application` update/engine-parameter helpers), and `Graphics` (`Renderer`/`Viewport` conveniences).
  - Kept `Interop` focused on true runtime bridge pieces: managed type-info registry, ref-counted instance cache/lifetime glue, and static type-info contract.
  - Ported useful old `Core` helpers that v3 can support today, including `Context.Instance`, `Context.SetRuntimeApi`, typed subsystem helpers, and `WorkQueue` managed task callbacks.
  - Deliberately did not port old `ScriptRuntimeApiImpl`: the generated v3 `ScriptRuntimeApi` surface is not constructible as a handwritten managed implementation yet. The `WorkQueue` helper remains necessary despite general callback support because the relevant SDK APIs are still excluded as `TaskFunction&&`/template shapes.
  - Added rbfx E2E surface coverage for the ported handwritten helper APIs.
  - Verification: `cmake --build --preset Debug` and `ctest --preset Debug --output-on-failure` pass.

- **`Rbfx.Application` launch path uses the generated `EngineParameters` map directly**:
  - Removed the associative-collection-specific recovery rule; core no longer needs to know about `EngineParameters`, `VariantMap`, or any concrete collection/type pair.
  - `MarshallerMetadataPhase2` now resolves recovered template arguments by normalized C++ record identity, preventing same-managed-name collisions such as `Rml::Variant` rebinding to `Urho3D::Variant`.
  - Recovered value ABI is gated by generic facts: copyable records or records with configured marshaller/support metadata may use value spelling; unconfigured records keep handle/pointer ABI so non-copyable values are not forced through collection thunks.
  - Generic record rendering deduplicates duplicate managed generic signatures and chooses the best native instantiation using generic signals, so `Ea.UnorderedMap<Ea.String, Urho3D.Core.Variant>` dispatches to `eastl::hash_map<eastl::basic_string<char>, Urho3D::Variant>`.
  - VS Code `Rbfx.Application (Debug)` launch and `Run: Rbfx.Application (Debug)` task now start the built DLL directly without triggering a build.
  - Verification: focused model/rendering tests pass, `cmake --build --preset Debug --target sharpcpp-rbfx-application` passes, and an 8-second preloaded app launch reaches engine/RmlUI/ImGui startup and exits only via timeout.

- **Generated string wrappers now expose borrowed UTF-8 spans**:
  - Extended the marshaller-exposed string wrapper contract with generated `GetDataCore()` overrides, so `StringBase.AsUtf8Span()` can return a span over native string storage instead of materializing a managed byte-array copy.
  - Added an interpolated-string formatting extension for `Ea.String`, allowing app code to format directly into native UTF-8 strings with ordinary C# format specifiers. Formatting defaults to invariant culture while still accepting an explicit provider.
  - Simplified `Rbfx.Application` ImGui labels to use reusable `Ea.String` instances and cultureless `Ea.String.Format(...)`; `Ea.String` now implicitly converts to `ReadOnlySpan<byte>` for generated UTF-8 APIs.
  - Verification: `cmake --build --preset Debug` and `ctest --preset Debug` pass.

- **Redundant public overloads for nullable unexpressible defaults are pruned**:
  - Renderer convenience-overload generation now skips public overloads that are equivalent to the full C# signature with nullable `= default` optional parameters, avoiding duplicate surfaces such as `ImGui.Button(label)` beside `ImGui.Button(label, size = default)`.
  - Useful overloads are still preserved when they restore an expressible default before a later unexpressible default, such as alternating-default shapes where the full signature must suppress that default.
  - Native `__ArgsN` dispatch variants are unchanged, so omitted nullable/unexpressible arguments still route to the correct C++ default path.
  - Verification: focused rendering regression tests, `cmake --build --preset Debug`, focused `sharpcpp.e2e` / `sharpcpp.rbfx.e2e`, and full `ctest --preset Debug` all pass.

- **rbfx managed-object RTTI source generator was ported into v3 bindings**:
  - Added `SharpCpp.Rbfx.SourceGenerator` with `ManagedObjectRttiGenerator`, updated for v3 namespaces (`Urho3D.Math.StringHash` and `SharpCpp.Rbfx.Bindings.Interop.ManagedTypeInfoRegistry`).
  - Wired the source generator as an analyzer for rbfx bindings, rbfx E2E tests, and `Rbfx.Application`, so managed subclasses such as `DemoApplication : Application` receive generated RTTI overrides.
  - Added the managed and native `ManagedTypeInfoRegistry` runtime bridge that owns process-local `Urho3D::TypeInfo` instances for generated managed object types.
  - Added rbfx E2E coverage for a managed `Application` subclass returning its generated type hash/type info and satisfying base-type checks.
  - Verification: `cmake --build --preset Debug --target sharpcpp-rbfx-managed`, `cmake --build --preset Debug --target sharpcpp-rbfx-application sharpcpp-rbfx-tests-e2e-managed`, focused rbfx E2E, full `cmake --build --preset Debug`, and `ctest --preset Debug` all pass.

- **rbfx manual helper port is broader and ImGui helpers now prefer generated APIs**:
  - Expanded the additive `Variant` partial to cover the v1-style implicit conversions and `GetVariantType(Type)` mappings that are supported by the current generated v3 surface, including primitives, math types, resource refs, variant vectors/maps, string vectors/maps, `VariantCurve`, and `IntPtr`/`ScriptObject`.
  - Left old v1 helper surfaces out when v3 cannot safely express them yet, such as `byte[] -> Variant` without public vector construction and old callback/native-shim helpers for `WorkQueue` / `ActionBuilder`.
  - Expanded `RbfxImGuiSurfacePass` so app-used ImGui functions are generated directly (`SetNextWindowPos`, text-wrap helpers, `Button`, tooltip helpers), removed the duplicate native ImGui helper exports, and deleted the remaining handwritten ImGui convenience partial.
  - Fixed parser default handling for C++ constructor/call expressions such as `ImVec2(0, 0)`, so these defaults use the existing unexpressible-default overload path instead of being treated as invalid C# default expressions.
  - Updated renderer public signatures so trailing unexpressible defaults render as nullable optional parameters (`T? value = default`) where C# permits it, while generated convenience overloads are created only for unexpressible-default positions. Defaults before an omitted unexpressible argument are suppressed; expressible/nullable defaults after a present unexpressible argument resume normally.
  - Nullable managed `null` defaults now participate in the same managed dispatch cascade and call the appropriate native `__ArgsN` variant without generating extra public overloads.
  - Added regression coverage for constructor-expression defaults, generated ImGui overloads, and the v1-style `Variant.GetVariantType` mapping.
  - Verification: `cmake --build --preset Debug --target sharpcpp-tests-model`, `git diff --check`, `cmake --build --preset Debug`, `cmake --build --preset Debug --target sharpcpp-rbfx-tests-e2e-managed`, and `ctest --preset Debug` all pass.

- **rbfx handwritten replacement suppression was removed; generated wrappers are canonical**:
  - Deleted `RbfxHandwrittenManagedTypeSuppressionPass` and removed it from the rbfx generator pipeline, so handwritten rbfx code can no longer hide generated types or members.
  - Removed handwritten managed replacements for generated rbfx members such as `Application(Context)`, `Node.CreateComponent(StringHash, ...)`, `Scene(Context)`, `Viewport(Context)`, `Renderer.SetViewport`, `StaticModel.SetModel`, RmlUI document/font/model-handle APIs, and the handwritten `Intersection` enum.
  - Kept handwritten rbfx code additive only, including typed convenience helpers for resources, components, nodes, UI elements, event definitions, `Variant` / `Ea.String` conversions, and reflection metadata; updated `Rbfx.Application` to use the generated protected `Application.EngineParameters` map directly from its subclass.
  - Fixed parser enum-value normalization so all-caps native enum constants such as `OUTSIDE` generate idiomatic managed names such as `Outside` instead of relying on a handwritten enum.
  - Verified `Urho3D::Object::GetContext() const` is retained as generated `Object.GetContext()` with the generated native entry point `scrbfx_Urho3D_Object_GetContext_const_*`; the former conflict was the handwritten `Context` property colliding with generated field/property projection, not const-overload pruning of `GetContext()`.
  - Updated `Rbfx.Application` to consume generated canonical APIs (`GetContext()`, `EngineParameters`, generated enum names, generated rbfx constructors/methods) plus additive helpers.
  - Verification: `cmake --build --preset Debug`, explicit `cmake --build --preset Debug --target sharpcpp-tests-model`, generated rbfx grep for `Object.GetContext()`, `git diff --check`, and `ctest --preset Debug` all pass.

- **rbfx `Context` default constructor now comes from generated bindings**:
  - Removed stale handwritten suppression for `Urho3D::Context::Context()` and deleted the handwritten managed constructor/import path, allowing the normal parser/render pipeline to emit `Core/SharpCpp.Rbfx.Context.g.cs` and `.g.cpp` constructor wrappers from `Context.h`.
  - Added generated-surface coverage that asserts `public Context()`, its P/Invoke call, and the native `MarshallerInvoker::construct<Urho3D::Context>()` thunk are emitted.
  - Verification: focused model regression test passes, `cmake --build --preset Debug` passes, generated rbfx grep confirms the constructor wrapper/thunk, and `ctest --preset Debug` passes.

- **rbfx third-party library bindings now generate under dedicated output roots**:
  - Added parser support for preserving caller-configured include prefixes for owned declaration roots, so rbfx can keep header locations such as `RmlUi/Core/ElementDocument.h` and `ImGui/imgui.h` without using namespaces to classify output ownership.
  - Configured the rbfx generator to preserve `RmlUi` and `ImGui` header roots and route those generated files under `generated/rbfx/ThirdParty/RmlUi/` and `generated/rbfx/ThirdParty/ImGui/`.
  - Updated rbfx surface tests and rendering path coverage for the new third-party output layout.
  - Verification: focused rendering path test passes, `dotnet build SharpCpp.slnx --no-restore -m:1` passes, `cmake --build --preset Debug` passes, and `ctest --preset Debug` passes.

- **Const/template overload pruning now matches exact callable shape, and rbfx post-processing no longer hides valid non-ImGui APIs**:
  - `SilentlyExcludeConstOverloadMethodsPass` now keys const/non-const duplicate candidates by native name, generic parameters, return type, and parameter ABI/type details, so a template overload no longer causes an unrelated `Object* GetSubsystem(StringHash) const` overload to be discarded.
  - `DegradeUnsupportedSurfacePass` includes generic-parameter shape in managed callable duplicate checks so template and non-template overloads do not collapse before the const-overload pass can reason about them.
  - `RbfxImGuiSurfacePass` now scopes unsupported ImGui type/function/field pruning to actual ImGui records and callables instead of treating every non-ImGui record type as outside the ImGui bootstrap surface.
  - Generated rbfx output now includes `Context.GetSubsystem(StringHash)` and `Object.GetSubsystem(StringHash)` while explicit handwritten rbfx conveniences are suppressed by `RbfxHandwrittenManagedTypeSuppressionPass`.
  - Renderer public signatures now suppress defaults on non-trailing optional parameters, avoiding invalid C# for APIs such as `Widgets.ImageButton(..., framePadding = -1, bgCol, tintCol)`.
  - Verification: focused model/rendering tests pass, `cmake --build --preset Debug` passes, generated rbfx grep checks for both `GetSubsystem(StringHash)` methods pass, and `ctest --preset Debug` passes.

- **rbfx typed object helpers now use a generated static type-info contract**:
  - Added self-typed `SharpCpp.Rbfx.Bindings.Interop.IStaticTypeInfo<TSelf>`.
  - Added `managed_static_interfaces` and `managed_static_interfaces_abstract` marshaller metadata; `Marshaller<Urho3D::Object*>` now owns the inherited `IStaticTypeInfo<{managed_type}>` contract without forcing generated `Object` itself to implement it, and the renderer expands `{managed_type}` for generated derived records that expose public static `GetTypeStatic()`.
  - Updated typed subsystem/component/resource helpers to constrain `T` to `IStaticTypeInfo<T>` and call `T.GetTypeStatic()` directly instead of reflecting through `ObjectReflection<T>`.
  - Verification: focused model/rendering tests pass, `cmake --build --preset Debug` passes, and `ctest --preset Debug` passes.

- **`Rbfx.Application` now starts from the no-rebuild VS Code path**:
  - Added `.Rbfx.Application.env` with `LD_LIBRARY_PATH`, `LD_PRELOAD`, and `RBFX_RESOURCE_PREFIX` so the app uses the already-built native libraries and the local rbfx `Data` / `CoreData` resource tree.
  - Kept `Node.CreateComponent<T>()` as v1-style typed sugar over the `StringHash` overload; it now resolves the hash through the generated static type-info contract.
  - Fixed generated native-object registry emission so polymorphic wrappers are registered only when their managed wrapper factory is available.
  - Adjusted renderer abstractness for non-public pure-virtual hooks: protected native hooks now render as protected throwing stubs instead of making every native-derived wrapper abstract, while public pure virtual methods remain managed abstract obligations.
  - Verification: focused rendering regression tests pass, `cmake --build --preset Debug --target sharpcpp-rbfx-application`, full `cmake --build --preset Debug`, and `ctest --preset Debug --output-on-failure` pass; an 8-second preloaded app run reaches engine/RmlUI/ImGui startup and resource loading without managed exceptions.

- **VS Code can build and run `Rbfx.Application` directly**:
  - Added a focused `CMake: Build Rbfx.Application (Debug)` task plus `Run: Rbfx.Application (Debug)` for running the already-built output without triggering CMake.
  - Added an `Rbfx.Application (Debug)` CoreCLR launch profile that runs the current CMake output DLL from `out/build/dev/bin/Debug` without a `preLaunchTask` rebuild.
  - Mirrored the v1 `.Rbfx.Application.env` workaround so `libUrho3D.so` is preloaded through `LD_PRELOAD` before the managed process starts, avoiding the known Linux TLS load-order issue.
  - Verification: `.vscode/tasks.json` / `.vscode/launch.json` pass `jq` validation.

- **Conservative rbfx RmlUI / ImGui submodule surfaces now build and test**:
  - Added rbfx submodule detection for `URHO3D_RMLUI=1` and `URHO3D_SYSTEMUI=1`, generated a parse umbrella, and tracked enabled SDK submodule headers as generator inputs.
  - Added conservative RmlUI and ImGui surface passes that keep sample-ready records/free functions while pruning unsupported templates, callbacks, raw arrays, pointer-to-pointer shapes, varargs, and stale silently excluded declarations.
  - Added routed free-function rendering so ImGui globals emit under `ImGuiNet.ImGui` without ImGui-specific renderer branching.
  - Added ImGui value marshaller configuration for `ImVec2` / `ImVec4`, fixed ignored array helper generation, enum/alias ABI casts, and polymorphic registry qualification for custom managed namespaces.
  - Ported the full v1-style `rbfx/Rbfx.Application` sample with scene setup, resource loading, RmlUI in-memory document/data-model callbacks, Dear ImGui overlay, and update-loop wiring through narrow rbfx handwritten conveniences.
  - Added sample-ready rbfx support shims for typed subsystem/resource/component lookups, application lifetime/update dispatch, viewport/node/resource conveniences, Rml data-model binding, and the ImGui calls used by the sample.
  - Replaced stale rbfx E2E assertions with compile-time surface checks for the current conservative output, including Rml `ElementDocument`, `ImGuiNet.ImGui`, `Ea.String`, `UIElement`, and `BiasParameters`.
  - Verification: focused model/rendering tests, focused `sharpcpp.e2e` rerun, `cmake --build --preset Debug --target sharpcpp-rbfx-application`, `cmake --build --preset Debug`, and `ctest --preset Debug --output-on-failure` all pass.

- **Wrapped record value returns now work without per-type marshaller configuration**:
  - Degradation no longer treats by-value returns of owned parsed records as unsupported just because the type lacks a custom `Marshaller<T>` specialization.
  - Native rendering now emits pointer-shaped entrypoints for these returns and uses generic `MarshallerInvoker::invoke_owned*` helpers to allocate an owned native copy for the generated managed wrapper.
  - Director callbacks still reject owned-handle returns during degradation, preserving the existing unsupported director-return boundary.
  - rbfx `Spline.GetPoint(float)` now generates as `Variant` instead of an excluded comment.
  - Verification: focused model/rendering tests, `cmake --build --preset Debug`, and `ctest --preset Debug` all pass.

- **rbfx `ea::shared_array<T>` / `eastl::shared_array<T>` wrappers now work end-to-end**:
  - Added dynamic-array model, parser, pipeline, and metadata facts driven by `dynamic_array = true` marshaller configuration.
  - Added managed `SharedArrayBase<T>` runtime support with optional known length, unsafe indexed get/set, span assignment, and explicit-length `ToArray` materialization.
  - Added rbfx EASTL shared-array marshaller specializations using `ea::shared_array<T>*` ABI, owned wrapper copies for value/reference returns, and borrowed pointer passthrough for pointer returns.
  - Renderer now emits `Ea.SharedArray<T>` wrappers with `nint` custom marshalling plus generated native create/delete/get/set helpers for supported concrete element types.
  - Previously excluded rbfx APIs such as `Sound.GetData()` and `BufferedSoundStream.AddData(...)` now generate as `Ea.SharedArray<T>` surfaces.
  - Verification: focused model/metadata/rendering tests, `cmake --build --preset Debug`, and `ctest --preset Debug` all pass.

- **Native-backed `std::array` / `eastl::array` wrappers now work end-to-end**:
  - Added fixed-array model and metadata facts, schema version 7 import/export, and marshaller config extraction for hidden non-type length parameters.
  - Added managed `NativeArrayAbi` / `ArrayBase<T>` runtime support plus native `std::array<T, N>` and rbfx `eastl::array<T, N>` marshaller support.
  - Renderer now emits `Std.Array<T>` / `Ea.Array<T>` wrappers with span/array/enumerable initialization, runtime length validation, zero-copy call ABI, and generated native create/delete/get/set helpers for supported concrete instantiations.
  - Fixed-array public imports use C# custom marshallers for `NativeArrayAbi` conversion instead of manual generated `ToNativeArrayAbi()` / `CreateManagedWrapper(...)` call-site marshalling.
  - Fixed helper entrypoint sanitization and filtered helper generation to retained renderer-ready primitive/enum/handle element types, avoiding invalid open-template or ignored/external rbfx helper branches.
  - Degradation now skips hidden fixed-array length arguments during public generic-argument validation.
  - Verification: `cmake --build --preset Debug`, focused `StandardLibraryTests`, metadata tests, and `ctest --preset Debug` all pass.

- **Alias-backed rbfx `VariantMap` / `hash_map` surfaces now generate coherent `Ea.UnorderedMap` wrappers**:
  - Marshaller extraction now preserves source template parameter names for configured generic records, keeping native-only EASTL hash/predicate/allocator/cache parameters distinct from the two public managed map arguments.
  - Alias-backed callback and generic-instantiation discovery now traverses callback signatures, preserves public alias names such as `VariantMap`, and avoids collecting open native template placeholders as concrete instantiations.
  - Resolved handle record IDs are rebased to the canonical parsed record identity before rendering, preventing alias/generic lookup splits between qualified and unqualified record IDs.
  - Generic validation now filters public instantiations with fatal ignored-record arguments or ref-struct managed arguments, while ignoring hidden native-only template parameters and preserving nonfatal ignored handle arguments.
  - Verification: focused alias/generic parser tests, full model/metadata/rendering tests, `cmake --build --preset Debug`, generated rbfx checks for `Ea.UnorderedMap`/`VariantMap`, and `ctest --preset Debug --output-on-failure` all pass.

- **rbfx event handler map callback surfaces now keep only visible generic arguments**:
  - Record handle specialization now parses concrete template arguments with opaque fallback and projects native-only template arguments out via configured managed placeholder formats.
  - Alias-backed `hash_map` / `unordered_map` surfaces now preserve two-argument managed map targets even when the canonical C++ type carries extra hash/predicate/allocator/cache parameters.
  - Generated callback helpers now accept configured value callback arguments only when the public managed type is already the generated ABI type, while conversion-required configured types still degrade before rendering.
  - Verification so far: focused parser/degradation model tests pass.

- **Callback parameters can opt into named managed delegate types**:
  - Added parameter-level `[[sharpcpp::delegate]]` support for callback parameters while preserving the default `Action<>` / `Func<>` projection.
  - Simple delegate names now generate nested delegates in the bindings class; fully qualified or dotted names reference external delegate types.
  - Metadata schema version 6 preserves callback delegate policy on parameters, and generated callback helpers are keyed by the effective managed callback type.
  - Explicit callback `managed_marshaller` configuration still takes precedence over generated callback helpers.
  - Projected secondary-base methods preserve callback delegate policy, and generated unnamed delegate names use deduplicated import method names to avoid overload collisions.
  - Parser callback signature token mapping now applies managed-type overrides before specialized record handles, so configured value aliases such as `IntVector2` keep their marshaller facts inside callback signatures.
  - Unsupported generated-helper callback argument/return shapes degrade before rendering invalid managed code.
  - Verification: focused parser/degradation/rendering tests, `cmake --build --preset Debug`, and `ctest --preset Debug --output-on-failure` all pass.

- **Managed-to-native callback parameters now work end-to-end**:
  - Added explicit callback facts to `BindingType`, metadata schema version 5, and parser support for `callback = true` function-object marshallers.
  - `std::function<>`, `ea::function<>`, and `eastl::function<>` now project to `Action<>` / `Func<>` delegate surfaces for supported in-value parameters.
  - Generated C# emits per-signature custom marshaller helpers that pin delegates with `GCHandle`, pass `{ state, callback }`, register a shared native release hook, use Cdecl callbacks, and fail fast if managed callbacks throw across unmanaged frames.
  - Native support wraps managed callback handles in copyable/movable shared-state function objects and releases the managed state through the registered hook when the last wrapper copy dies.
  - Degradation keeps only in-value callback parameters and deterministically rejects callback returns, callback fields, and out/ref callback parameters before rendering.
  - Optional callback defaults now use null dispatch instead of nullable-value `.HasValue` / `.Value`, and C++ `nullptr` callback defaults render nullable managed delegate parameters/imports.
  - Regression coverage includes marshaller extraction, parser mapping/defaults, metadata export/import, degradation guards, renderer helper/default output, and synthetic E2E callback invocation/lifetime tests.
  - Verification: `cmake --build --preset Debug` and `ctest --preset Debug` both pass.

- **P/Invoke imports now request aggressive inlining**:
  - Generated managed binding files now include `System.Runtime.CompilerServices` and emit `[MethodImpl(MethodImplOptions.AggressiveInlining)]` immediately after every generated `[LibraryImport]` declaration, including generic, field, metadata, callbase, director, and default-argument variant imports.
  - Handwritten rbfx `RefCountedRuntime` imports now carry the same aggressive-inlining attribute.
  - Regression coverage scans generated managed output and fails if any emitted `LibraryImport` is missing the inline attribute.
  - Verification: `dotnet test tests/SharpCpp.Tests.Rendering/SharpCpp.Tests.Rendering.csproj --configuration Debug`, `cmake --build --preset Debug`, source/generated `LibraryImport` scans, and `ctest --preset Debug` all pass.

- **Stale rbfx blocked-header tests now match the current header policy**:
  - `RbfxSilentExclusionPassTests` now use `Core/Assert.h` for synthetic blocked-header dependency fixtures instead of the intentionally unblocked `Core/Mutex.h`.
  - Verification: `dotnet test tests/SharpCpp.Tests.Model/SharpCpp.Tests.Model.csproj --configuration Debug --filter RbfxSilentExclusionPassTests`, full model tests, `cmake --build --preset Debug`, and `ctest --preset Debug` all pass.

- **Non-generic handle wrappers now share one generated marshaller implementation**:
  - Added `INativeHandleOwnership`, `BorrowedNativeHandle`, and `OwnedNativeHandle` support marker policies.
  - Non-generic generated handle wrappers now emit one `__TypeMarshaller<TOwnership>` class that handles both parameters and returns, with ownership selected by the marker type instead of generating separate borrowed/owned return marshaller classes.
  - Generic wrappers keep the existing arity-matching `InMarshaller` / `ReturnMarshaller` / `OwnedReturnMarshaller` shape because `LibraryImport` source generation requires generic custom marshaller arity to match the managed generic type arity.
  - Verification: `dotnet test tests/SharpCpp.Tests.Rendering/SharpCpp.Tests.Rendering.csproj --configuration Debug`, `cmake --build --preset Debug`, and generated rbfx checks for `ResourceRef` using `__ResourceRefMarshaller<OwnedNativeHandle>` / `<BorrowedNativeHandle>` passed; current `ctest --preset Debug` passes.

- **rbfx `ResourceRef` by-value returns now generate owned wrapper returns**:
  - Added rbfx `ResourceRef` value/reference/pointer/return marshaller specializations so C++ value returns use owned native handle ABI while existing reference returns remain borrowed.
  - Added explicit `owned_handle = true` marshaller metadata and ownership-only `return_ownership = "owning"` extraction, allowing the parser to mark by-value handle returns as owned without remapping their public type.
  - `DegradeUnsupportedSurfacePass` now accepts records explicitly marked as marshaller-configured owned handles, avoiding core type-specific special cases.
  - `rbfx/CMakeLists.txt` now tracks rbfx marshaller headers as generator inputs so these config edits invalidate generated bindings.
  - Verification: focused model tests for ownership-only extraction and marshaller-backed record-value returns, `cmake --build --preset Debug`, and generated rbfx grep checks for missing `ResourceRef` exclusions and owned return marshaller attributes passed; current `ctest --preset Debug` passes.

- **rbfx `FlagSet<T>` bitwise helper operators are silently excluded**:
  - `RbfxSilentExclusionPass` now applies rbfx-specific silent function rules before generic dependent-type degradation, so `operator&`, `operator^`, `operator|`, and `operator~` helpers returning `Urho3D::FlagSet<T>` do not render unsupported-operator comments.
  - Regression coverage models a blocked `Container/FlagSet.h` return type so the real ordering issue is covered.
  - Verification: `dotnet test tests/SharpCpp.Tests.Model/SharpCpp.Tests.Model.csproj --filter Apply_silently_excludes_flagset_only_operators`, `cmake --build --preset Debug`, and generated rbfx grep checks for the reported FlagSet operator comments passed; current `ctest --preset Debug` passes.

- **Const overload duplicate diagnostics are now suppressed consistently**:
  - `SilentlyExcludeConstOverloadMethodsPass` now considers non-const counterpart methods even when those counterparts were already degraded for a primary unsupported-surface reason.
  - Const methods already carrying duplicate managed-callable diagnostics are converted to silent exclusions instead of being emitted as generated `// excluded: managed callable signature ...` comments.
  - Verification: `dotnet test tests/SharpCpp.Tests.Model/SharpCpp.Tests.Model.csproj --filter SilentlyExcludeConstOverloadMethodsPassTests`, `cmake --build --preset Debug`, and generated rbfx grep checks for the reported `GetData|False|False|`/const duplicate patterns passed; current `ctest --preset Debug` passes.

- **Signed and unsigned byte pointer marshallers now expose pointer-token APIs as `IntPtr`**:
  - Added explicit `Marshaller<T>` specializations for `signed char*`, `const signed char*`, `volatile signed char*`, `const volatile signed char*`, `unsigned char*`, `const unsigned char*`, `volatile unsigned char*`, and `const volatile unsigned char*` that pass through native pointer ABI values and advertise `global::System.IntPtr` managed surfaces.
  - Parser parameter direction inference now treats full pointer types with managed-type overrides as by-value input tokens by default, while explicit `[[sharpcpp::out]]` / `[[sharpcpp::ref]]` still wins.
  - Synthetic E2E fixtures now distinguish byte scalar out/ref coverage from byte pointer-token APIs so mutable byte pointers are not accidentally tested as scalar out parameters.
  - Verification: `cmake --build --preset Debug`, `dotnet test tests/SharpCpp.Tests.Model/SharpCpp.Tests.Model.csproj --configuration Debug --filter Parse_defaults_managed_pointer_override_parameters_to_in_value_tokens`, `dotnet test tests/SharpCpp.Tests.E2E/SharpCpp.Tests.E2E.csproj --configuration Debug --filter BuiltinTests`, generated rbfx `rg -n "out global::System\.IntPtr" "out/build/dev/generated/rbfx"`, and `ctest --preset Debug --output-on-failure` with unrelated rbfx generator edits temporarily stashed all pass.

- **Direct `dotnet test` / IDE runs validate generated and native prerequisites before compiling tests**:
  - Central MSBuild properties carry deterministic build/output/generated/native/runsettings paths for direct consumers.
  - `SharpCpp.Build.targets` validates generated C# sources, generation stamps, native libraries, rbfx runsettings, and user-code runtime artifacts after the `SharpCpp.Native` dependency has produced them.
  - This earlier validation work is now subsumed by the C#-centric solution graph described above.

- **Handle-typed field setters now marshal wrappers by value instead of by-ref**:
  - Generated field setter imports for handle fields now use the same by-value `[MarshalUsing]` shape as ordinary handle parameters, avoiding a test-host crash from `in` by-ref custom marshalling.
  - The synthetic setter parameter remains pointer-shaped for non-handle fields, preserving existing field ABI behavior.
  - Regression coverage verifies handle field setter imports no longer emit `in Payload?` and the base-typed field E2E roundtrip preserves derived wrappers.
  - Verification: `dotnet test tests/SharpCpp.Tests.Rendering/SharpCpp.Tests.Rendering.csproj --configuration Debug`, `dotnet test tests/SharpCpp.Tests.E2E/SharpCpp.Tests.E2E.csproj --configuration Debug --filter Base_typed_fields_can_store_derived_wrappers`, and full `ctest --preset Debug --output-on-failure` all pass.

- **rbfx ignore-by-header filtering now removes selected implementation headers coherently**:
  - `RbfxSilentExclusionPass` now silently excludes declarations defined in the configured rbfx implementation headers (`Container/Ptr.h`, `Container/Str.h`, `Container/Hash.h`, `Container/FlagSet.h`, `Container/IndexAllocator.h`, `Container/Functors.h`, `Container/Allocator.h`, `Core/Assert.h`, `Core/AssertBase.h`) while intentionally leaving `Container/RefCounted.h` and `Core/Mutex.h` available.
  - Include matching canonicalizes grouped, `Urho3D/`-prefixed, and absolute SDK paths. Member functions and fields fall back to the nearest ancestor header because parser members do not carry their own include path.
  - APIs that reference header-blocked records/enums are non-silently ignored so generated diagnostics explain dependent removals, while retained records drop unavailable C++ bases and projected inherited members without changing virtual/override flags or promoting runtime bases.
  - Alias pruning is scoped to aliases targeting records/enums defined in blocked headers, preventing unrelated ignored aliases from being removed.
  - Verification: `dotnet test tests/SharpCpp.Tests.Model/SharpCpp.Tests.Model.csproj --filter RbfxSilentExclusionPassTests`, `cmake --build --preset Debug`, and `ctest --preset Debug -R sharpcpp.rbfx.e2e --output-on-failure` all pass.

- **Protected handle fields now render through the director protected-member path**:
  - Removed the bootstrap blanket degradation for protected handle fields; unsupported target records/enums and other invalid field shapes still degrade through the normal type checks.
  - Added a renderer `NativeFieldInteropPlan` with structural setter modes for primitive/value fields, non-borrowed handle fields, and borrowed-storage handle fields. Protected `SharedPtr<T>`-style fields now use `ReturnMarshaller<T>` / `ParameterMarshaller<T>` conversion, while protected map/vector/list-style fields return borrowed storage and preserve null-clear setter behavior.
  - rbfx `Application` protected fields `commandLineParameters_`, `engine_`, and `engineParameters_` are generated instead of emitted as `// excluded:` stubs.
  - Regression coverage verifies supported protected handles remain retained, ignored target records still degrade, primitive protected setters keep `field_t<T>`, projected handles use marshaller conversion, and borrowed containers use address-return/null-clear semantics.
  - Verification: `dotnet test tests/SharpCpp.Tests.Model/SharpCpp.Tests.Model.csproj`, `dotnet test tests/SharpCpp.Tests.Rendering/SharpCpp.Tests.Rendering.csproj`, `cmake --build --preset Debug`, and `ctest --preset Debug` all pass.

- **Director callback rendering now supports the rbfx inheritable SDK surface end-to-end**:
  - Managed director callback helpers now qualify callback owner types with `global::...`, preventing inherited callbacks from resolving against the wrong generated namespace in grouped rbfx output.
  - Protected inherited callbacks dispatch through the current director wrapper type and use string method names instead of inaccessible `nameof(Base.ProtectedMethod)` expressions, preserving C# protected-access rules.
  - Managed callback handle returns now use `NativeObject.GetHandle(...)` instead of assuming a public `.Handle` property, covering `StringBase`, object-reflection wrappers, and ordinary generated native objects uniformly.
  - Native director callbacks now marshal native override parameters to ABI values before invoking managed callbacks and marshal ABI returns back to native return values.
  - Protected native helper methods now perform explicit ABI-to-C++ parameter conversion and ABI return conversion while preserving direct protected member access. Callbase helper and entrypoint names now use deduplicated import method names, avoiding overloaded virtual callbase collisions.
  - Verification: `dotnet test tests/SharpCpp.Tests.Model/SharpCpp.Tests.Model.csproj`, `dotnet test tests/SharpCpp.Tests.Metadata/SharpCpp.Tests.Metadata.csproj`, `dotnet test tests/SharpCpp.Tests.Rendering/SharpCpp.Tests.Rendering.csproj`, `cmake --build --preset Debug`, and `ctest --preset Debug` all pass.

- **rbfx inheritable SDK classes are force-marked as director-capable before parser member population**:
  - Added a parse-request director override set so composition roots can opt specific records into director handling without editing third-party SDK headers or adding type-specific core special cases.
  - `SharpCpp.Rbfx.Generator` now marks the rbfx inheritable surface (`Object`, `Application`, `ApplicationState`, `Serializable`, `Animatable`, `Component`, `LogicComponent`, `PluginApplication`, `Drawable`, `MoveAndOrbitComponent`, `Resource`, `TriggerAnimator`, `Drawable2D`, `RmlUIComponent`) as director-capable during parsing.
  - The existing core gate that ignores abstract non-director constructors remains intact; forced director records pass that gate before constructors and protected members are populated.
  - Regression coverage verifies that an abstract constructor remains ignored by default but is preserved when the record is supplied in the forced director set.

- **Enum identity now drives ownership checks and rbfx `FlagSet<TEnum>` rescue stays narrow**:
  - `BindingType` enum references now carry `EnumId`, which is populated from parser-resolved `EnumBinding.SymbolId`, survives generic substitution/type-key comparisons, and round-trips through metadata schema version 4.
  - `DegradeUnsupportedSurfacePass`, alias pruning, and rbfx flag-set lowering now resolve enum ownership by symbol identity instead of formatted/native type-name strings. This fixes false exclusions for owned enums whose spelling differs across typedefs, aliases, or generated surfaces.
  - rbfx `FlagSet<TEnum>` lowering only clears stale ignore metadata for concrete constructors and fields whose lowered type tree no longer contains open generic parameters. Constructors on abstract records remain ignored, preventing invalid native thunks that instantiate abstract C++ classes such as `Drawable` or `OctreeQuery`.
  - Regression coverage verifies generic enum ownership by `EnumId` and that abstract-record constructors are not rescued after flag-set lowering.
  - Verification: `dotnet test tests/SharpCpp.Tests.Model/SharpCpp.Tests.Model.csproj`, `dotnet test tests/SharpCpp.Tests.Metadata/SharpCpp.Tests.Metadata.csproj`, `dotnet test tests/SharpCpp.Tests.Rendering/SharpCpp.Tests.Rendering.csproj`, `cmake --build --preset Debug`, and `ctest --preset Debug` all pass.

- **Silent exclusions now suppress generated `// excluded:` comment stubs**:
  - Added silent-exclusion metadata layered over `IgnoreReason`, preserving ignored-node dependency behavior while allowing renderers to omit selected declarations entirely.
  - Explicit `[[sharpcpp::ignore]]` attributes, deleted native callables, unconfigured external placeholder records, core iterator endpoint functions, core assignment operators, rbfx non-`Urho3D` declarations, rbfx `detail`/`Detail` implementation namespaces, rbfx smart-pointer wrapper declarations, and rbfx flagset-only bitwise operators are now silently excluded.
  - Ordinary non-silent degradation reasons still render diagnostic comment stubs, so unsupported-surface diagnostics remain visible unless a pass explicitly marks the node silent.
  - Grouped rendering still emits stable generated files for silent-only groups, avoiding stale CMake source-list failures while keeping the files free of excluded API comments. Functions whose parent record is silently excluded are now omitted as well, preventing stale member comment stubs under silent records.
  - Bootstrap pipeline pass start/completion lifecycle logs now use trace level; warnings and high-level generator progress remain visible at normal verbosity.
  - Verification: focused model/rendering tests, `cmake --build --preset Debug`, targeted generated-stub checks for rbfx smart-pointer and `PacketType` flagset comments, and `ctest --preset Debug` all pass.

- **rbfx managed namespace projection now uses a shadow AST over the parsed AST**:
  - Added `ManagedBindingProjection` with synthetic namespace nodes and declaration references that do not re-parent original `BindingNode` instances, so original AST traversal remains intact.
  - Added `RbfxManagedNamespaceProjectionPass` as a terminal rbfx shaping step after rbfx type lowering and before render/export. Actual `Urho3D::` declarations from owned rbfx headers such as `Urho3D/<Segment>/**` and grouped relative paths such as `<Segment>/**` now project into `Urho3D.<Segment>`, while configured external wrapper namespaces such as `Ea` remain unchanged even when declared from rbfx headers.
  - Rendering and metadata export resolve record/enum namespaces and fully-qualified type references through the projection when present. Aliases are intentionally excluded from the shadow namespace tree: generated C# aliases remain `global using` aliases because consuming projects compile the alias file directly, while alias metadata keeps its normalized namespace and points at projected target types.
  - rbfx generator now has a small manual handwritten-type suppression list for generated managed types provided by handwritten rbfx sources (currently `Urho3D.Math.Intersection`), avoiding fragile source scanning and keeping rbfx-specific exceptions out of core.
  - Native object registry initialization and rbfx E2E tests now use projected namespaces, so grouped output compiles after records move out of the flat `Urho3D` namespace.
  - Regression coverage verifies original/shadow traversal, projected namespace rendering and alias output, and metadata namespace/type-reference export.
- **String view parameters and returns now use an explicit UTF-8 ptr+len span ABI**:
  - Added core `std::string_view` marshaller configuration and rbfx `ea::string_view` / `eastl::string_view` configuration, all projecting to `global::System.ReadOnlySpan<byte>`.
  - Added `Utf8SpanAbi` plus separate input and return custom marshallers. Input spans are copied to owned unmanaged buffers and freed by the input marshaller; returned views are wrapped without freeing native memory.
  - Parser marshaller extraction now distinguishes `Marshaller<T>` from `ReturnMarshaller<T>` by the actual template declaration token, preventing return marshaller metadata from overwriting parameter marshaller metadata for the same C++ type.
  - Renderer emits `[MarshalUsing]` on configured span parameters, avoids invalid default-value branches for undefaultable ref-struct parameters, and only emits managed `override` when the managed return type remains compatible with the base method.
  - rbfx generator explicitly suppresses `Urho3D::Graph::LoadJSON` in `RbfxUnsupportedApisPass` because the SDK header declares it but the linked `libUrho3D.so` does not define/export the symbol; this keeps the generated native library loadable while documenting the unsupported API reason.
  - E2E coverage verifies `std::string_view` and synthetic `ea::string_view` parameters/returns, including embedded NUL bytes.
  - Verification: focused model/rendering tests, `cmake --build --preset Debug`, and `ctest --preset Debug` all pass.

- **Metadata-driven alias preservation and rbfx `FlagSet<TEnum>` lowering are now green end-to-end**:
  - Alias metadata now uses structural type matching (`BindingTypeKey`) so public aliases are restored from model facts instead of formatted type-name strings, while internal/import/P/Invoke signatures stay concrete and fully qualified.
  - Alias emission now treats `BindingCompilation.TypeAliases` as authoritative and recursively drops aliases whose complete target tree lacks a managed projection, preventing invalid aliases such as unresolved nested EASTL implementation types from leaking into `*.Aliases.g.cs`.
  - rbfx now runs `RbfxFlagSetLoweringPass` after SDK export filtering and before rendering/export. It rewrites `FlagSet<TEnum>` aliases, function types, parameters, and fields to the enum surface, marks the referenced enums as `[Flags]`, suppresses the `FlagSet` record itself, and drops remaining aliases targeting unavailable ignored records.
  - Regression coverage includes alias metadata export/import, hash-map alias parsing, alias pruning for unresolved generic targets, renderer alias emission, and rbfx `FlagSet<TEnum>` lowering.
  - Generated rbfx and user-code aliases include restored public aliases such as `VariantMap`, `StringVariantMap`, `AnimationChannelFlags`, and `RenderOutputFlags`; no generated `FlagSet` type remains.
  - Verification: `dotnet test tests/SharpCpp.Tests.Model/SharpCpp.Tests.Model.csproj`, `dotnet test tests/SharpCpp.Tests.Rendering/SharpCpp.Tests.Rendering.csproj`, `dotnet test tests/SharpCpp.Tests.Metadata/SharpCpp.Tests.Metadata.csproj`, `cmake --build --preset Debug`, and `ctest --preset Debug` all pass.

- **Public alias restoration is now isolated to the public surface, with concrete internal signatures preserved**:
  - `BindingOutputRenderer` now distinguishes public alias presentation from concrete/internal type emission using `BindingType` facts (`PrimitiveKind`, `RecordId`, `GenericArguments`, `ManagedTypeFormatOverride`) instead of aliasing `LibraryImport` signatures directly.
  - Public signatures can collapse exact typedef aliases such as `VariantMap`, `StringVariantMap`, `FileTime`, and `NetworkId`, while internal/import signatures remain concrete and fully qualified.
  - Generated `*.Aliases.g.cs` files now restore alias availability for alias-shaped types used on public record methods as well as free functions/fields, closing the `ThreadID` regression where the public API referenced an alias name without an emitted alias declaration.
  - Direct wrapper-typed `LibraryImport` parameters now use generated per-record `ManagedToUnmanagedIn` custom marshallers that route through `NativeObject.GetHandle(...)`, restoring null-to-zero behavior for nullable wrapper inputs without falling back to raw `nint` public signatures.
  - Verification: `dotnet test tests/SharpCpp.Tests.Rendering/SharpCpp.Tests.Rendering.csproj`, `dotnet test tests/SharpCpp.Tests.Metadata/SharpCpp.Tests.Metadata.csproj`, `dotnet test tests/SharpCpp.Tests.Model/SharpCpp.Tests.Model.csproj`, `cmake --build --preset Debug`, and `ctest --preset Debug` all pass.

- **Alias metadata now round-trips through `SharpCpp.Metadata`, including generic alias targets**:
  - `BindingMetadataDocument` schema version advanced to `3` and now carries `aliases` plus structural `generic_arguments` on `BindingMetadataTypeReference`.
  - `BindingMetadataExporter` now exports alias targets as fully-qualified concrete type references while preserving generic argument structure.
  - `BindingMetadataImportCatalog` now seeds imported aliases back into `BindingCompilation.TypeAliases`, restores imported generic record facts (`ManagedTypeFormat`, generic parameters), and reconstructs alias target types structurally for downstream rendering.
  - This fixes downstream imported alias cases such as `FloatRange` / `UintRange` and keeps user-code alias files coherent after importing upstream rbfx metadata.

- **Desugared EASTL associative-container matching now accepts `unordered_map` / `hash_map` equivalence**:
  - `MarshallerConfigExtractor` now prefers Clang's desugared template argument while preserving the source namespace qualification, so rbfx marshaller metadata authored against EASTL unordered-map surfaces resolves to the actual parsed `eastl::hash_map<...>` container type.
  - `ClangSharpHeaderParser` now desugars typedef targets through AST facts (`CanonicalType.Declaration`) and treats equivalent template heads as the same generic container surface during managed-type-override matching.
  - `DegradeUnsupportedSurfacePass` now treats ignored internal-detail generic arguments as fatal when evaluating container wrapper surfaces, preventing invalid `Ea.UnorderedMap<..., Detail::*...>` wrappers from leaking into generated API.
  - Added `Bootstrap_pipeline_supports_hash_map_alias_return_surface` plus a focused `HashMapAlias` fixture to keep the desugared hash-map projection path covered.
  - Verification: `dotnet test tests/SharpCpp.Tests.Model/SharpCpp.Tests.Model.csproj` passes. The rbfx generator now materializes previously excluded `VariantMap` / `StringVariantMap` surfaces, but the follow-up public-alias restoration work is still incomplete in this snapshot.

- **Incorrect generated bindings from alias/template-name leakage**:
  - `ClangSharpHeaderParser.ResolveQualifiedTypeName` now rewrites top-level template arguments from Clang's canonical spelling while preserving the spelled container surface. This stops native thunk signatures from leaking colliding short names such as `Rml::Vector3` into rbfx APIs that should resolve to fully qualified `Urho3D::*` types.
  - `ClangSharpHeaderParser.ParseTypeToken` now consults alias-origin names when parsing nested generic arguments, so alias-backed value types inside containers resolve to their configured managed types instead of canonical template spellings like `BaseVector3<float>` / `BaseVector2<float>`.
  - `MarshallerMetadataPhase2.ApplyAliasSurfaces` is no longer used to rewrite callable signatures to alias names. This avoids broad alias-surface pollution such as `FileTime`, `ThreadID`, `NetworkId`, and `VariantVector` leaking into generated P/Invoke and public signatures when the underlying concrete managed type is the correct surface.
  - `DegradeUnsupportedSurfacePass` now rejects container surfaces whose nested generic arguments resolve to fatally ignored records (for example `eastl::pair` from excluded namespace roots), while still allowing supported handle containers like `eastl::vector<Urho3D::Object*>`.
  - Handle-returning `LibraryImport` declarations now use the actual managed wrapper type instead of `nint` when the wrapper record is renderable. The renderer emits per-wrapper custom return marshallers (borrowed vs owned) so generated APIs return the public C# type directly without manual `CreateManagedWrapper(...)` code in the calling method.
  - Ordinary handle-like `LibraryImport` parameters now use the wrapper type directly instead of `nint` for non-`ref`/non-`out` inputs. Generated call sites pass the wrapper object directly, while `ref`/`out` handle parameters still use the explicit `nint` bridge path.
  - Hand-written `Type` references now use `global::System.Type` in `rbfx/SharpCpp.Rbfx.Bindings/Math/StringHash.cs` and `rbfx/SharpCpp.Rbfx.Tests.E2E/StringBindingTests.cs`, avoiding accidental resolution to generated `Urho3D.Type`.
  - Verification: `dotnet test tests/SharpCpp.Tests.Model/SharpCpp.Tests.Model.csproj`, `dotnet test tests/SharpCpp.Tests.Rendering/SharpCpp.Tests.Rendering.csproj`, `cmake --build --preset Debug`, and `ctest --preset Debug` all pass.

- **C++ namespace separators (`::`) leaking into managed type names**: Fixed at 6 sites in `ClangSharpHeaderParser.cs` where `::` could enter `ManagedName`/`PublicTypeName`:
  - Placeholder record creation (3 sites) — use `GetManagedTypeHead` to strip `::` before `ToPascalCase`
  - `RegisterRecord`/`RegisterTemplateRecord` (2 sites) — same fix for nested type spellings from ClangSharp
  - `MapType` enum fallback (1 site) — same fix for enums not found in `enumsByName`
  - `ParseTypeToken` record/enum lookups (4 sites) — use `StripGenericArguments` before lookups to prevent `NotSupportedException` fallback that created types with raw `::` tokens
  - `ParseConcreteTemplateArgumentsOrOpaque` fallback — NO normalization; unresolvable types keep raw `::` in `PublicTypeName` so that `HasUnresolvedNamespaceColons` catches and degrades them. The `::` → `.` string conversion was reverted as architectural violation (string guesswork instead of AST-based configuration).
- **`HasUnresolvedNamespaceColons`** restored to its original role as a valid degradation gate in `DegradeUnsupportedSurfacePass`: types lacking managed type configuration (e.g., external library types not in `NamespaceMappings`) are properly degraded rather than generating broken C# output.
- **Native field setters pass values by pointer (Issue #2)**: Added `field_t<T>` alias (`T*`) in `Marshaller.hpp`. Updated native setter template in `BindingOutputRenderer.cs` from `arg_t<T>` to `field_t<T>` for the value parameter. Managed P/Invoke declarations use `in` modifier for pointer-compatible ABI.
- **Degradation leak via placeholder records (Issue #1)**: `DegradeUnsupportedSurfacePass` now also checks `MetadataKeys.PlaceholderRecord` on Handle-type records — types referencing placeholder records (external library types with no real definition) are properly degraded instead of producing broken C++ output.
- **Post-instantiation unresolvable type check (Issue #1)**: `ValidateBootstrapSurfacePass` now validates generic instantiation type arguments for `HasUnresolvedNamespaceColons` and placeholder records, catching unresolvable types that slip through pre-instantiation degradation.
- **Managed type format override propagation (Issues #3 & #4)**: Added `BindingType.ManagedTypeFormatOverride` property (exposes `_managedTypeFormatOverride`). `SubstituteGenericType` and `SubstituteFunctionGenericType` now consult this override in their fallback chain, ensuring concrete type names use the marshaller's configured managed type name rather than the raw C++ name.
- **Test results**: 4/5 test suites pass (model, metadata, rendering, E2E). Rbfx E2E has 1 pre-existing failure (`BiasParametersTests` — unrelated to these fixes). Net reduction of 62 C# errors in rbfx SDK build (222→160).

## Doc Anchors

- `doc/01-architecture.md` — overall architecture
- `doc/02-marshaller-system.md` — marshaller contract
- `doc/03-two-phase-parsing.md` — two-phase parse
- `doc/06-core-minimal.md` — minimal core surface
- `doc/07-marshaller-contract.md` — marshaller ABI contract
- `doc/08-metadata-contract.md` — metadata schema
- `doc/11-project-shape-blueprint.md` — project layout
- `doc/12-generated-output-and-module-api.md` — generated output contract
- `doc/13-rewrite-strategy.md` — v1→v3 rewrite strategy
- `doc/14-diagnostics-and-failure-model.md` — diagnostics model

## Verification

```bash
dotnet restore SharpCpp.slnx
dotnet build SharpCpp.slnx --configuration Debug --no-restore
dotnet test SharpCpp.slnx --configuration Debug --no-build --no-restore
```
