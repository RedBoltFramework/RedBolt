# Instance Caching

When a native pointer wraps into a managed object, the binding must ensure that the same native pointer always maps to the same managed instance. Without caching, two calls wrapping the same pointer would produce two managed objects pointing to the same native memory — leading to double-free, stale state, and identity violations.

Helper names in this document such as `CreateManagedWrapper`, `DefaultNativeInstanceCache`, `NativeObjectRegistry`, or `CreateManagedWrapperWithKeepAlive` are illustrative. The hard requirement is the cache/identity contract, not those exact API names.

## Cache invariants

1. cache keys use the normalized complete-object handle
2. one live managed identity exists per normalized handle
3. owning wrappers take precedence over non-owning wrappers for the same handle
4. runtime type IDs are dispatch-only values, never exported metadata identity
5. base-view pointers normalize before cache lookup

## Core mechanism

Every non-abstract generated record class declares a static instance cache:

```csharp
internal static readonly DefaultNativeInstanceCache s_instanceCache = new();
protected override INativeInstanceCache? InstanceCache => s_instanceCache;
```

`CreateManagedWrapper` checks the cache before creating a new instance:

```csharp
internal static MyType CreateManagedWrapper(nint handle, bool ownsHandle)
{
    if (handle == 0) return null;
    if (s_instanceCache.TryGetExisting(handle, ownsHandle, out var existing) && existing is MyType cached)
        return cached;
    var instance = new MyType(handle, ownsHandle);
    s_instanceCache.Register(instance);
    return instance;
}
```

Cache hits return the existing managed wrapper. Cache misses create, register, and return a new wrapper.

## INativeInstanceCache

The cache interface:

```csharp
public interface INativeInstanceCache
{
    bool TryGetExisting(nint handle, bool ownsHandle, out NativeObject? instance);
    void Register(NativeObject instance);
    void Unregister(NativeObject instance);
}
```

Three operations: lookup, register, remove. The `ownsHandle` parameter enforces ownership preference — an owning wrapper takes priority over a non-owning wrapper for the same native pointer.

## DefaultNativeInstanceCache

The default cache for non-ref-counted types. Uses `ConcurrentDictionary<nint, WeakReference<NativeObject>>`:

- **Key**: native pointer value (`nint`)
- **Storage**: weak references — managed wrappers can be GC'd when no strong references exist
- **Ownership preference**: if an owning wrapper already exists for a handle, a non-owning wrapper does not replace it
- **Stale entry cleanup**: dead weak references are removed on next access via `RemoveStaleEntry`

### Lifecycle

1. **Construction** (`new MyType(handle, ownsHandle)`): public constructors call `s_instanceCache.Register(this)` after base class initialization
2. **Wrapper creation** (`CreateManagedWrapper`): checks cache, creates if miss, registers
3. **Disposal** (`Dispose`): calls `s_instanceCache.Unregister(this)`, then `ReleaseHandle()`
4. **Polymorphic lookup** (`NativeObjectRegistry.Create<T>`): first checks static-type cache, then dynamic-type cache after type resolution

## Polymorphic caching

Polymorphic types use `NativeObjectRegistry.Create<T>` instead of direct cache lookup:

```csharp
internal static MyBaseType CreateManagedWrapper(nint handle, bool ownsHandle)
{
    if (handle == 0) return null;
    return (MyBaseType)__NativeObjectRegistry.Create<MyBaseType>(handle, ownsHandle);
}
```

`NativeObjectRegistry.Create<T>` does a two-stage cache check:

1. **Static-type cache**: checks `T.RegistrationInstanceCache` for an existing wrapper at the static type
2. **Dynamic type resolution**: calls `T.RegistrationGetMetadata(handle)` which invokes `__GetNativeObjectMetadata(handle, out resolvedHandle, out dynamicTypeId)`
3. **Dynamic-type cache**: if the dynamic type is a registered derived type, switches to that type's `InstanceCache` and `CreateFactory`
4. **Handle adjustment**: replaces `handle` with `resolvedHandle` (the complete-object pointer from `dynamic_cast<void*>(self)`)

This ensures that wrapping `MyBaseType*` that actually points to a `MyDerivedType` object returns `MyDerivedType` using `MyDerivedType`'s instance cache.

The static-type cache check is an optimization. The normalized handle and resolved dynamic cache are the authoritative identity path.

## INativeObjectRegistration<T>

Each polymorphic record implements this static abstract interface:

```csharp
public interface INativeObjectRegistration<T> where T : NativeObject
{
    static abstract nuint RegistrationDynamicTypeId { get; }          // typeid(T).hash_code()
    static abstract Type RegistrationExpectedType { get; }           // typeof(T)
    static abstract INativeInstanceCache RegistrationInstanceCache { get; }  // s_instanceCache
    static abstract Func<nint, bool, NativeObject>? RegistrationCreateFactory { get; }  // factory, null if abstract
    static abstract NativeObjectMetadata RegistrationGetMetadata(nint handle);
}
```

- `RegistrationDynamicTypeId`: C++ `typeid(T).hash_code()` — used for runtime type lookup
- `RegistrationExpectedType`: `typeof(T)` — used by the registry to verify type consistency
- `RegistrationInstanceCache`: the per-type `DefaultNativeInstanceCache` — used for wrapper lookup
- `RegistrationCreateFactory`: lambda that creates `new T(handle, ownsHandle)` — `null` for abstract types
- `RegistrationGetMetadata`: calls native `__GetNativeObjectMetadata` and returns `NativeObjectMetadata` with handle adjustment + type ID

Abstract types set `RegistrationCreateFactory` to `null`. They register in the type registry (so `dynamicTypeId` matches) but cannot be created directly.

## NativeObjectMetadata and handle adjustment

```csharp
public readonly struct NativeObjectMetadata
{
    public readonly nint Handle;          // adjusted complete-object pointer
    public readonly nuint DynamicTypeId; // typeid(*self).hash_code()

    public NativeObjectMetadata Normalize(nint fallbackHandle)
        => Handle != 0 ? this : new NativeObjectMetadata(fallbackHandle, DynamicTypeId);
}
```

The C++ side provides:

```cpp
template <class TObject>
inline __SharpCppNativeObjectMetadata __SharpCppGetNativeObjectMetadata(TObject* self)
{
    return self == nullptr
        ? __SharpCppNativeObjectMetadata{nullptr, 0}
        : __SharpCppNativeObjectMetadata{dynamic_cast<void*>(self), typeid(*self).hash_code()};
}
```

`dynamic_cast<void*>(self)` converts a secondary-base pointer to the complete-object pointer — critical for multiple inheritance where a `Serializable*` pointing into a `MyObject` needs to map back to the `MyObject` start address.

Per-record, the generator emits `__GetNativeObjectMetadata` which additionally checks concrete director descendants via `dynamic_cast<DerivedType*>(self)` to resolve the exact runtime type.

Neither the handle-adjusted pointer nor the runtime `DynamicTypeId` is exported as binding metadata. Metadata stores build-time type identity; caches store runtime object identity.

## Cache customization

### ManagedInstanceCacheFactoryExpression

The `RecordBinding.ManagedInstanceCacheFactoryExpression` property overrides the default cache:

```csharp
// Default:
internal static readonly DefaultNativeInstanceCache s_instanceCache = new();

// Custom (e.g., rbfx RefCountedInstanceCache):
internal static readonly RefCountedInstanceCache s_instanceCache
    = new(getScriptObject: static handle => __GetCachedScriptObjectRaw(handle), ...);
```

Token substitution: `{interop_accessor}` and `{managed_type}` are replaced with the generated accessor and type name.

### ManagedInstanceCacheTypeName

The `RecordBinding.ManagedInstanceCacheTypeName` property overrides the cache field type:

```csharp
// Default: DefaultNativeInstanceCache (concrete, for inlining)
// Custom: RefCountedInstanceCache or INativeInstanceCache (interface)
```

## rbfx: RefCountedInstanceCache

For `Urho3D::RefCounted`-derived types, `RbfxRefCountedSupportPass` replaces the default cache with `RefCountedInstanceCache`. Instead of maintaining a separate `ConcurrentDictionary`, `RefCountedInstanceCache` stores the managed `GCHandle` directly inside the C++ object's private `scriptObject_` member:

- **Storage**: The C++ object's `scriptObject_` field holds a `GCHandle` — no separate hash table
- **Lookup**: P/Invoke bridge functions (`__GetCachedScriptObjectRaw`, `__SetCachedScriptObjectRaw`, `__ResetCachedScriptObjectRaw`) read/write `scriptObject_` via `RbfxRefCountedAccess.hpp` private member access
- **Reference strength**: When native ref count > 0, the `GCHandle` is `Normal` (strong, prevents GC). When ref count drops to 0, it's `Weak` (allows GC but still cacheable if native side hasn't destroyed yet)
- **Lifecycle**: `RefCountedNativeObject.ManagesInstanceCacheLifecycle` returns `true` — the cache manages registration/unregistration, not `NativeObject.Dispose`

`RefCountedNativeObject` overrides:
- `ReleaseNativeHandle(handle)`: calls `InstanceCache.Unregister(this)` then `ReleaseReferenceCore()` (bumps C++ ref count down)
- `NormalizeManagedOwnership()`: when `OwnsNativeHandle`, calls `AcquireReferenceCore()` (bumps C++ ref count up)
- `ShouldRegisterStrongScriptObjectHandle()`: returns `GetReferenceCountCore() > 0`

SharedPtr returns with ownership transfer are handled by `CreateManagedWrapperWithKeepAlive` which stores a `GCHandle` to the `SharedPtr`'s control block, preventing premature native destruction.

## Native-triggered disposal

When C++ destroys an object, the managed side must be notified to unregister from the cache:

```csharp
// NativeObject.DisposeFromNativeRuntimeCore():
// 1. Unregister from instance cache
// 2. Invalidate handle
// 3. GC.SuppressFinalize(this)
```

For rbfx: `ScriptRuntimeApiImpl.Dispose(instance)` checks if the instance is a `RefCountedNativeObject` and calls `RefCountedRuntimeHelpers.DisposeFromNativeRuntime(refCounted)` which triggers `DisposeFromNativeRuntimeCore()`.

## Type registration (ModuleInitializer)

All concrete polymorphic records register in `NativeObjectRegistry` via a `ModuleInitializer`:

```csharp
[ModuleInitializer]
internal static void Initialize()
{
    NativeObjectRegistry.RegisterType<Widget>();
    NativeObjectRegistry.RegisterType<Button>();
    NativeObjectRegistry.RegisterType<Label>();
}
```

This ensures the type registry is populated before any `CreateManagedWrapper` call. Abstract types register with `RegistrationCreateFactory = null` — they match on `dynamicTypeId` but delegate creation to the concrete derived type.

## Handle constructor visibility

Non-polymorphic records use `internal` handle constructors (only the cache and `CreateManagedWrapper` create instances). Polymorphic records use `protected internal` — derived managed types need access for `INativeObjectRegistration<T>` implementation:

```csharp
// Non-polymorphic
internal MyType(nint handle, bool ownsHandle) : base(handle, ownsHandle) { }

// Polymorphic
protected internal MyType(nint handle, bool ownsHandle) : base(handle, ownsHandle) { }
```

---

← [[08-inheritance]] | [[10-projection]] →
