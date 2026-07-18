# Inheritance & Polymorphism

C++ inheritance and polymorphism map to C# through four mechanisms: single inheritance with directors, multiple inheritance via interfaces, polymorphic runtime-type wrapping, and handle adjustment. Each is driven by marshaller metadata and passes — no renderer-level type branching.

## 1. Single inheritance with directors

A `[[sharpcpp::director]]` attribute on a C++ class marks it as director-capable. Directors enable managed code to override C++ virtual methods.

### 1.1 Director qualification

A record qualifies for director instantiation when:
- It has at least one effective virtual method slot (`RequiresDirector()`)
- No missing declared pure virtual methods exist in the record or any of its abstract base classes (`CanInstantiateDirector()`)

Abstract classes with unimplemented pure virtuals cannot instantiate directors. Concrete derived classes that implement all pure virtuals can.

### 1.2 Director class emission (C++ renderer)

For each director-qualified record, a `__<Name>Director` class is emitted that inherits from the record:

```cpp
class __WidgetDirector : public Widget
{
    void* _managed;
    void (*_onUpdate)(void*, float);
    int (*_getPriority)(void*);

    void OnUpdate(float dt) override
    {
        if (_onUpdate)
            _onUpdate(_managed, dt);
        else
            Widget::OnUpdate(dt);  // fallback to base
    }

    int GetPriority() const override
    {
        if (_getPriority)
            return _getPriority(_managed);
        throw std::runtime_error("pure virtual: no managed override");
    }
};
```

- Each virtual slot gets a function pointer field
- If the callback is set (`!nullptr`), invoke it through `MarshallerInvoker::invoke_callback_as<FT>`
- If not set: call base implementation, or throw for pure virtuals
- Value-type reference returns store the result in a mutable field for lifetime safety

### 1.3 Director connection (C++ renderer)

A C-linkage `__ConnectDirector` function takes the object handle + per-slot callback pointers:

```cpp
extern "C" void __Widget_ConnectDirector(void* self, void* slot0, void* slot1)
{
    auto* director = dynamic_cast<__WidgetDirector*>(self);
    if (director)
        director->connect_director(slot0, slot1);
}
```

### 1.4 Director connection (C# renderer)

```csharp
protected override void ConnectDirector()
{
    var runtimeType = ((object)this).GetType();
    _director = new __Widget_Director();
    __ConnectDirector(
        _handle,
        OnUpdate_VirtualCallbackPointer(runtimeType),
        GetPriority_VirtualCallbackPointer(runtimeType));
}
```

Each `VirtualCallbackPointer` accessor uses `NativeObject.HasManagedOverride(runtimeType, declaringType, methodName, parameterTypes)` to check at runtime whether the managed subclass actually overrides the method. If not, the pointer is `0` — no callback wired, no overhead.

### 1.5 Director-aware construction

When a record `RequiresDirector()`, the C++ constructor invocation uses `new __DirectorTypeName(...)` instead of `new RecordTypeName(...)`. This ensures native objects allocated from managed code always have the director vtable.

### 1.6 Director-aware destruction

```cpp
extern "C" void __Widget_Delete(void* self)
{
    auto* director = dynamic_cast<__WidgetDirector*>(self);
    delete director ? director : self;
}
```

If the object is a director, `delete` triggers the full C++ destructor chain through the director subclass.

### 1.7 Generic type arguments with directors

When a generic type argument is a record that requires a director (`UsesDirectorRecordType = true`), the C++ type name resolves to the director variant. For example, `Vector<PolymorphicBase>` where `PolymorphicBase` requires a director emits `Vector<__PolymorphicBaseDirector*>` on the C++ side. The `ExpandDirectorAwareGenericInstantiations` pass produces a second instantiation variant with the director type.

## 2. Multiple inheritance via interfaces

When a C++ class inherits from multiple base classes, the primary base maps to C# class inheritance. Secondary bases map to C# interfaces — **automatically, with no configuration**.

### 2.1 Secondary base detection

```cpp
class MyObject : public RefCounted, public Serializable { ... };
```

The parser sees `RefCounted` as the primary base (first) and `Serializable` as secondary. A pass generates:

```csharp
public interface ISerializable
{
    void Serialize(ReadOnlySpan<byte> path);
    void Deserialize(ReadOnlySpan<byte> path);
}

public class MyObject : RefCounted, ISerializable
{
    // Interface members satisfied by exposed members from the primary base
}
```

No `render_interface` attribute needed. The core detects secondary bases automatically.

### 2.2 Interface inheritance chains

If `Serializable` itself inherits from `IStreamable`, then `ISerializable : IStreamable`. The `GetManagedInterfaceBaseRecords()` extension walks the managed base bindings to collect which interfaces the current interface extends.

### 2.3 Interface method collection

`GetRenderableInterfaceMethods()` walks up non-interface-rendering base records, collecting public non-static methods while deduplicating by signature. This ensures secondary base interface surfaces are properly projected even when the methods are inherited through multiple levels.

### 2.4 Virtual inheritance

C++ virtual base classes (the `virtual` keyword in inheritance) are **excluded** from managed inheritance. Only non-virtual primary and secondary bases contribute to the C# type hierarchy. Virtual inheritance creates ambiguous base layouts that cannot be mapped to C#.

## 3. Polymorphic wrapping (runtime type resolution)

When a function returns a base-type pointer but the runtime type may be derived, `polymorphic = true` on the handle marshaller enables runtime-type-based wrapper creation.

### 3.1 IsPolymorphic

A record is marked polymorphic if it has virtual methods or any of its bases is polymorphic. This is a structural property derived from the C++ AST — not configured.

### 3.2 Type registration

Only **concrete** (non-abstract) polymorphic records register in the type registry. The generated static constructor calls:

```csharp
NativeObjectRegistry.RegisterType<Widget>();
NativeObjectRegistry.RegisterType<Button>();
NativeObjectRegistry.RegisterType<Label>();
```

A `ModuleInitializer` ensures the class constructor runs before any type is used.

### 3.3 Runtime type resolution

Each polymorphic record emits a native `GetNativeObjectMetadata` function:

```cpp
extern "C" void __Widget_GetNativeObjectMetadata(
    void* self, void** outHandle, std::size_t* outDynamicTypeId)
{
    auto metadata = __SharpCppGetNativeObjectMetadata(self);
    *outHandle = metadata.handle;

    // Check concrete director descendants first
    if (auto* btn = dynamic_cast<Button*>(self)) {
        *outDynamicTypeId = typeid(Button).hash_code();
        return;
    }
    if (auto* lbl = dynamic_cast<Label*>(self)) {
        *outDynamicTypeId = typeid(Label).hash_code();
        return;
    }
    *outDynamicTypeId = metadata.dynamicTypeId;
}
```

The template `__SharpCppGetNativeObjectMetadata` does two things:
1. `dynamic_cast<void*>(self)` — adjusts from a possible secondary-base pointer to the complete-object pointer (handle adjustment)
2. `typeid(*self).hash_code()` — gets the runtime type ID

Then the per-record function checks director concrete descendants with `dynamic_cast<ConcreteType*>(self)` to resolve the exact derived type. This two-level check handles both the base type's own ID and any derived type registered in the system.

`typeid(...).hash_code()` is a runtime dispatch token only. It is valid only inside the current generated binary / support runtime process. It is never exported to binding metadata and never treated as stable build-time identity.

### 3.4 Wrapper creation

```csharp
public static Widget CreateManagedWrapper(nint handle, bool ownsHandle)
{
    return NativeObjectRegistry.Create<Widget>(handle, ownsHandle);
}
```

`NativeObjectRegistry.Create<T>`:
1. Check `T.RegistrationInstanceCache` for existing instance
2. Call `T.RegistrationGetMetadata(handle).Normalize(handle)` — gets complete-object handle + dynamic type ID
3. Look up `dynamicTypeId` in the type registry
4. If found, switch to the registered type's `CreateFactory` and `InstanceCache`
5. Create the appropriate derived wrapper
6. Register the new instance in the cache

### 3.5 Handle adjustment

When a C++ object's secondary-base pointer differs from its primary-base pointer, `dynamic_cast<void*>(self)` adjusts to the complete object. This is critical for multiple inheritance:

```cpp
// self is a Serializable* pointing into a MyObject
// dynamic_cast<void*>(self) returns the MyObject* start
```

The complete-object handle is what gets stored and used for all subsequent native calls. The interface wrapper converts between the two using the same `dynamic_cast` mechanism. Cache keys, runtime wrapper selection, and native identity all use this normalized handle — never the incoming base-view pointer.

### 3.6 Handle constructor visibility

Polymorphic records use `protected internal` constructor visibility (instead of `internal`) so derived managed types can call the handle constructor:

```csharp
// Non-polymorphic
internal MyType(nint handle, bool ownsHandle) : base(handle, ownsHandle) { }

// Polymorphic
protected internal MyType(nint handle, bool ownsHandle) : base(handle, ownsHandle) { }
```

## 4. Secondary base dispatch (virtual methods through interfaces)

Virtual methods inherited from secondary bases need special handling because the C++ vtable layout differs from the primary base.

### 4.1 ProjectInheritedHiddenVirtualsPass

This pass creates projected method bindings for:

1. **Own hidden override candidates** — virtual methods declared in the record that override inherited virtual methods but have `private` access. These are projected as `ProtectedOverride` (if director-required) or `Public`.

2. **Secondary branch candidates** — virtual methods from secondary base records that are not already visible in the primary inheritance chain. These are projected with `NativeDispatchRecordOverride` set to the secondary base, generating calls like `self->Serializable::Method(...)`.

Projected methods get `NativeBody` strings that call through the correct base using qualified dispatch.

### 4.2 NormalizeOverrideAccessModifiersPass

When a virtual override's access modifier differs from the base declaration:

- If the base is `public` but the override is not → force `public`, set `NativeDispatchRecordOverride` to the base record
- If the base is `protected` but the override is `public` → demote to `protected`, clear `NativeDispatchRecordOverride`

`NativeDispatchRecordOverride` on `FunctionBinding` tells the C++ renderer to use qualified dispatch: `self->BaseRecord::method(...)`.

### 4.3 Protected access directors

Protected methods are only surfaced when `RequiresDirector()` is true. `PruneNonDirectorProtectedSurfacePass` removes all protected properties, functions, and fields from records that don't support directors.

For director records, the C++ renderer generates protected-access thunks in the director class using `dynamic_cast<DirectorType*>(self)` chains:

```cpp
static auto __Widget_GetValue_thunk(Widget* self) -> int
{
    if (auto* director = dynamic_cast<__WidgetDirector*>(self))
        return director->_getValue();
    throw std::runtime_error("protected access requires director");
}
```

## 5. Degradation rules

Certain polymorphism combinations are unsupported and cause the record or member to be excluded:

| Scenario | Reason |
|---|---|
| Polymorphic generic record | Polymorphic + generic cannot be combined |
| Virtual base class | Virtual inheritance cannot map to C# |
| Callback parameter on virtual method | Callback + virtual dispatch conflict |
| Callback return on virtual method | Callback + virtual dispatch conflict |
| Contiguous-buffer return on virtual method | Buffer lifetime incompatible with virtual dispatch |
| Protected field/constructor on non-director | No access path without director thunk |

## 6. Pipeline ordering

The relevant passes execute in this order:

1. `AssignManagedInterfaceUsagePass` — determines which records render as interfaces
2. `ProjectInheritedHiddenVirtualsPass` — projects hidden virtuals from secondary bases
3. `NormalizeOverrideAccessModifiersPass` — normalizes access modifiers, sets `NativeDispatchRecordOverride`
4. `PruneNonDirectorProtectedSurfacePass` — removes protected surface on non-director records
5. `CollectGenericInstantiationsPass` — expands director-aware generic instantiation variants
6. `DegradeUnsupportedSurfacePass` — removes polymorphic generics, virtual bases, unsupported combinations

Each pass reshapes the AST in place. Later passes see the results of earlier ones. Renderers consume the final AST state — no reconstruction from side metadata.

## 7. rbfx-specific overrides

### 7.1 RefCounted integration

`RbfxRefCountedSupportPass` configures all `RefCounted`-derived records:
- Overrides `ManagedBaseTypeNameOverride` to `RefCountedNativeObject`
- Replaces `DefaultNativeInstanceCache` with `RefCountedInstanceCache` (bridges to rbfx's script object caching)
- Injects cache helper methods (`GetCachedScriptObjectRaw`, `AcquireReferenceCore`, `ReleaseReferenceCore`)
- Rewrites `SharedPtr<T>` returns to detach the raw pointer with ownership transfer
- Sets `EmitDefaultNativeReleaseOverride = false` (rbfx uses ref counting, not naive delete)

### 7.2 ScriptRuntime director injection

`RbfxScriptRuntimePass` injects a synthetic default constructor on `Urho3D::ScriptRuntimeApi` so SharpCpp can emit a director-backed managed implementation.

---

← [[07-callbacks]] | [[09-instance-caching]] →
