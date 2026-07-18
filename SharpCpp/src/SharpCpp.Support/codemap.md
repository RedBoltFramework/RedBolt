# src/SharpCpp.Support/ — Managed Runtime Support

## Responsibility

`SharpCpp.Support` is the managed-side runtime foundation that all generated C# bindings link against. It owns the infrastructure for:

- **Native instance tracking**: mapping native pointers (`nint` handles) to managed `SafeHandle`-derived wrappers, with cache lookups that prevent duplicate wrappers for the same native object.
- **Lifetime coordination**: deterministic disposal of native resources through `SafeHandle`'s `ReleaseHandle`, dual-path cleanup (managed `Dispose` vs. native-initiated destruction via `DisposeFromNativeRuntimeCore`), and a managed keep-alive mechanism to prevent GC reclamation of wrapper-visible resources.
- **Type resolution**: a static registry (`NativeObjectRegistry`) that maps runtime type IDs from the native side to the correct generated wrapper factories, enabling polymorphic wrapper creation (e.g., returning the most derived managed type for a native object whose exact type is only known at runtime).
- **String marshalling**: `StringBase` bridges managed `string` ↔ native UTF-8 byte buffers via virtual dispatch to generated override methods that call native `std::string` operations.

## Assembly Identity

- **Assembly name**: `SharpCpp.Support` (netstandard2.0-compatible, no external NuGet dependencies).
- **Marker type**: `AssemblyMarker` — an empty static class serving as a reflection-level assembly identity token (used by the toolchain to locate the support assembly at build time).

## Public Types

### Core Infrastructure

| Type | Kind | File | Description |
|---|---|---|---|
| `NativeObject` | `abstract class` | `NativeObject.cs:6` | Root of the binding model. Extends `SafeHandle` to provide deterministic native resource lifetime, instance caching, dual-path disposal (managed `Dispose` vs native `DisposeFromNativeRuntimeCore`), managed keep-alive for child objects (`SetManagedKeepAliveCore`), and static helpers (`GetObject`, `GetHandle`, `RequireHandle`, `RequireObject`, `HasManagedOverride`). |
| `INativeObjectRegistration<TSelf>` | `interface` (static abstract) | `INativeObjectRegistration.cs:3` | Contract implemented by generated wrapper types (`where TSelf : NativeObject`). Provides per-type metadata without reflection: `RegistrationDynamicTypeId` (`nuint`), `RegistrationExpectedType`, `RegistrationInstanceCache`, `RegistrationCreateFactory`, and `RegistrationGetMetadata(nint)`. |
| `NativeObjectMetadata` | `readonly struct` | `NativeObjectMetadata.cs:6` | Blittable value type (`[StructLayout(LayoutKind.Sequential)]`) flowing across P/Invoke. Carries native pointer (`Handle`) and C++ runtime type ID (`DynamicTypeId`). `Normalize(nint)` replaces a null handle with a fallback. |
| `NativeObjectRegistry` | `abstract class` (all static) | `NativeObjectRegistry.cs:5` | Static type registry mapping native dynamic type IDs → (expected CLR type, factory delegate, instance cache). `Create<T>(nint, bool)` resolves polymorphic wrappers: cache lookup → native metadata query → registry resolution → factory invocation. `RegisterType<T>()` reads from `INativeObjectRegistration<T>`; also exposes a direct `RegisterType(...)` overload. |
| `INativeInstanceCache` | `interface` | `INativeInstanceCache.cs:3` | Contract for per-type instance caches keyed by native handle. `TryGetExisting(nint, bool, out NativeObject?)` performs ownership-compatible lookup; `Register(NativeObject)` and `Unregister(NativeObject)` manage entries. |
| `DefaultNativeInstanceCache` | `sealed class` | `DefaultNativeInstanceCache.cs:6` | Thread-safe `ConcurrentDictionary<nint, WeakReference<NativeObject>>` implementation. Uses weak references so the cache never prevents GC. On registration, silently drops non-owning duplicates when an owning entry already exists. On lookup, purges stale entries. |

### String Bridge

| Type | Kind | File | Description |
|---|---|---|---|
| `StringBase` | `abstract class` | `StringBase.cs:8` | UTF-8 string bridge extending `NativeObject`, implementing `IReadOnlyList<char>` and `IEquatable<string>`. Protected core methods (`GetCountCore`, `GetDataCore`, `ClearCore`, `GetAtCore`, `AddCore`) are overridden by generated code to call native string operations. Public surface: `Assign(string?/ReadOnlySpan<byte>)`, `ToUtf8Bytes()`, borrowed `AsUtf8Span()`, `ToString()`, `Equals(string?)`. |

### Collection Hierarchy

All collection bases extend `NativeObject`. Protected abstract `*Core` methods are overridden by generated code to call native container operations. Public surface implements standard .NET collection interfaces.

| Type | Kind | File | Implements | Description |
|---|---|---|---|---|
| `IndexableCollectionBase<T>` | `abstract class` | `CollectionBases.cs:41` | `IList<T>` | Sequence container base for `std::vector`/`std::list`-family wrappers. Core methods: `GetCountCore`, `ClearCore`, `GetAtCore`, `SetAtCore`, `AddCore`, `InsertAtCore`, `RemoveAtCore`. Indexed access with bounds validation. |
| `VectorBase<T>` | `abstract class` | `CollectionBases.cs:7` | (inherits `IList<T>`) | Marker for native `std::vector<T>` wrappers. Constructor only. |
| `ListBase<T>` | `abstract class` | `CollectionBases.cs:15` | (inherits `IList<T>`) | Marker for native `std::list<T>` wrappers. Constructor only. |
| `DictionaryBase<TKey, TValue>` | `abstract class` | `CollectionBases.cs:177` | `IDictionary<TKey,TValue>`, `IReadOnlyDictionary<TKey,TValue>` | Associative container base for map-family wrappers (`where TKey : notnull`). Core methods: `GetCountCore`, `ClearCore`, `ContainsKeyCore`, `GetValueCore`, `SetValueCore`, `AddCore`, `RemoveCore`, `GetKeyAtCore`, `GetValueAtCore`. Indexer throws `KeyNotFoundException` on missing key. |
| `MapBase<TKey, TValue>` | `abstract class` | `CollectionBases.cs:23` | (inherits dictionary interfaces) | Marker for native `std::map<TKey, TValue>` ordered map wrappers. |
| `UnorderedMapBase<TKey, TValue>` | `abstract class` | `CollectionBases.cs:32` | (inherits dictionary interfaces) | Marker for native `std::unordered_map<TKey, TValue>` wrappers. |

### LibraryImport Marshalling

| Type | Kind | File | Description |
|---|---|---|---|
| `Utf8SpanReturnMarshaller` | `static unsafe class` | `Utf8SpanReturnMarshaller.cs:15` | `[CustomMarshaller]` for `ReadOnlySpan<byte>` in `ManagedToUnmanagedOut` mode. `ConvertToManaged(IntPtr)` creates a null-terminated span from a native `const char*` using `MemoryMarshal.CreateReadOnlySpanFromNullTerminated` — zero-copy. |

### Assembly Identity

| Type | Kind | File | Description |
|---|---|---|---|
| `AssemblyMarker` | `static class` | `AssemblyMarker.cs:3` | Empty marker for reflection-level assembly identity lookups by the toolchain. |

### Type Dependency Graph

```
NativeObject (SafeHandle)
 ├── INativeInstanceCache (interface)
 │    └── DefaultNativeInstanceCache (sealed)
 │         └── uses ConcurrentDictionary<nint, WeakReference<NativeObject>>
 ├── INativeObjectRegistration<TSelf> (static abstract interface)
 │    └── implemented by generated wrapper types
 ├── NativeObjectRegistry (static)
 │    ├── uses NativeObjectMetadata (blittable struct)
 │    ├── uses INativeInstanceCache
 │    └── uses INativeObjectRegistration<T>
 ├── StringBase (implements IReadOnlyList<char>, IEquatable<string>)
 │    └── uses Encoding.UTF8 for encode/decode
 ├── IndexableCollectionBase<T> (implements IList<T>)
 │    ├── VectorBase<T>
 │    └── ListBase<T>
 └── DictionaryBase<TKey,TValue> (implements IDictionary<TKey,TValue>, IReadOnlyDictionary<TKey,TValue>)
      ├── MapBase<TKey,TValue>
      └── UnorderedMapBase<TKey,TValue>

Utf8SpanReturnMarshaller (standalone — CustomMarshaller for [LibraryImport])
AssemblyMarker (standalone — reflection target only)
```

## Design Patterns

### 1. Instance Cache via Weak References

`INativeInstanceCache` / `DefaultNativeInstanceCache`:
- Keys are native handles (`nint`). Values are `WeakReference<NativeObject>` — the cache does **not** keep wrappers alive; it merely prevents duplicate wrappers for the same handle while the wrapper is still reachable.
- **Registration policy** (`Register`): if a wrapper already exists in the cache and already claims ownership (`OwnsNativeHandle = true`), a new non-owning wrapper for the same handle is silently dropped. This prevents ownership conflicts.
- **Lookup policy** (`TryGetExisting`): caller provides `ownsHandle` — the lookup verifies the cached wrapper's ownership is compatible (if `ownsHandle` is true, the cache entry must itself own the handle). Stale entries (collected target, closed wrapper) are purged on access.
- **Unregistration**: removes the entry only if the reference matches the registering instance or the reference is stale. This avoids races between concurrent dispose paths.

### 2. SafeHandle as the Root Abstraction

`NativeObject` extends `System.Runtime.InteropServices.SafeHandle`:
- The `handle` field is the raw native pointer. `IsInvalid` returns true when `handle == IntPtr.Zero`.
- `OwnsNativeHandle` records whether this wrapper owns the native object's lifetime. Non-owning wrappers (created from raw pointers where ownership stays on the native side) skip native deletion.
- `ReleaseHandle()` calls `ReleaseNativeHandle(handle)` (virtual, overridden by generated code to call the native destructor) then unregisters from the instance cache.
- `Dispose(bool)` also unregisters before calling `base.Dispose` — double-coverage for the managed disposal path.

### 3. Dual Dispose Path

Native objects can be destroyed from either side:
- **Managed-initiated**: user calls `Dispose()` → `Dispose(bool)` → `Unregister` → `ReleaseNativeHandle` → finalization suppressed.
- **Native-initiated**: the C++ runtime calls `DisposeFromNativeRuntimeCore()` → `Unregister` → `SetHandleAsInvalid` → `GC.SuppressFinalize`. The native side handles actual memory reclamation; the managed side just severs the wrapper connection.

### 4. Managed Keep-Alive

`NativeObject` stores a `GCHandle` (`_managedKeepAlive`) to an `IDisposable` object that must outlive the wrapper. This is used by generated code to keep a child collection or transient object alive while the parent is alive. `ClearManagedKeepAlive` disposes the held object and frees the `GCHandle`. It is called in every teardown path.

### 5. Override Detection

`HasManagedOverride(Type runtimeType, Type declaringType, string methodName, Type[] parameterTypes)` is a static utility that checks whether a C# subclass overrides a virtual method from its declaring type. Used by generated code to determine whether to emit a `callvirt` to the override or skip calling the native default. Works by comparing `GetBaseDefinition()` chains.

### 6. Registered Type Resolution

`NativeObjectRegistry`:
- Static `ConcurrentDictionary<nuint, RegisteredType>` mapping native dynamic type IDs → (expected CLR type, factory delegate, instance cache).
- `Create<T>(nint handle, bool ownsHandle)` is the **primary entry point** for generated `P/Invoke` wrappers returning objects:
  1. First cache lookup from the static-site type `T`'s own cache.
  2. Query native metadata via `T.RegistrationGetMetadata(handle)` to get the true dynamic type ID.
  3. Resolve to the most derived registered type via `s_types[metadata.DynamicTypeId]`.
  4. Second cache lookup using the resolved type's cache.
  5. Factory invocation to create the wrapper (the factory ctor registers the new wrapper into the cache).

- `RegisterType<T>()` reads from the `INativeObjectRegistration<T>` generic interface — implemented by generated wrapper types as a static abstract interface (C# 11 `static abstract`). Each wrapper type provides its own cache, factory, dynamic type ID, and metadata query function.

### 7. StringBase — UTF-8 String Bridge

`StringBase` extends `NativeObject` and implements `IReadOnlyList<char>` and `IEquatable<string>`:
- Virtual methods `GetCountCore()`, `GetDataCore()`, `ClearCore()`, `GetAtCore(ulong)`, `AddCore(byte)` — generated overrides that delegate to the native string's `size()`, `data()`, `clear()`, indexed byte access, and `push_back()`.
- `Assign(string?)` / `Assign(ReadOnlySpan<byte>)` — replace contents with UTF-8 encoded text.
- `ToUtf8Bytes()` — copy native string into a managed `byte[]`.
- `AsUtf8Span()` — return a borrowed span over native UTF-8 storage when `GetDataCore()` is available; falls back to a managed copy otherwise.
- `ToString()` — decode from UTF-8 bytes.
- The native string marshaller specializations in `Marshaller.hpp` declare these methods as `protected_override` shapes, completing the bridge.

### 8. INativeObjectRegistration<T> — Static Abstract Interface

Generated wrapper structs implement this interface:
```csharp
public interface INativeObjectRegistration<TSelf> where TSelf : NativeObject
{
    static abstract nuint RegistrationDynamicTypeId { get; }
    static abstract Type RegistrationExpectedType { get; }
    static abstract INativeInstanceCache RegistrationInstanceCache { get; }
    static abstract Func<nint, bool, NativeObject>? RegistrationCreateFactory { get; }
    static abstract NativeObjectMetadata RegistrationGetMetadata(nint handle);
}
```
This enables the registry to work with any generated type without reflection-heavy patterns. The `static abstract` interface members are resolved at compile time by the JIT.

### 9. NativeObjectMetadata — Blittable Metadata Struct

`[StructLayout(LayoutKind.Sequential)]` — a blittable value type that flows across the P/Invoke boundary:
- `Handle` (`nint`): the native object's pointer.
- `DynamicTypeId` (`nuint`): a type identifier from the C++ runtime (e.g., rbfx's `GetType()`-based hash).
- `Normalize(fallbackHandle)`: returns `this` if `Handle` is non-zero, otherwise creates a new instance with the fallback handle. Used when the native metadata query returns a null self-handle (the handle is known from the P/Invoke call site).

## Data & Control Flow

```
                     P/Invoke call returns native handle
                                │
                                ▼
                    NativeObjectRegistry.Create<T>()
                                │
                    ┌───────────┴───────────┐
                    ▼                       ▼
          Cache lookup (handle)    Query native metadata
          via T's InstanceCache    via T.RegistrationGetMetadata
                    │                       │
                    │              ┌────────┴────────┐
                    │              ▼                 ▼
                    │       Resolve dynamic     Normalize handle
                    │       type → registered    (fill null handle)
                    │       type (Registry)
                    │              │
                    └──────┬──────┘
                           ▼
                 Cache lookup (resolved handle)
                           │
                ┌──────────┴──────────┐
                ▼                     ▼
           Found, alive,        Not found →
           compatible            factory(handle,
           → return               ownsHandle)
                                  │
                                  ▼
                           NativeObject ctor
                           (sets handle, registers
                            in InstanceCache)
                                  │
                                  ▼
                            Return wrapper
```

**Disposal flow** (managed initiated):
```
Dispose() → Dispose(bool) → Unregister from cache
                           → base.Dispose (SafeHandle)
                             → ReleaseHandle()
                               → ReleaseNativeHandle(native_ptr)
                               → SetHandleAsInvalid()
```

**Disposal flow** (native initiated):
```
[unmanaged →] DisposeFromNativeRuntimeCore()
                → Unregister from cache
                → SetHandleAsInvalid()
                → GC.SuppressFinalize(this)
```

## Integration Points

### Dependencies (of this layer)

| Dependency | Purpose |
|---|---|
| `System.Runtime.InteropServices` | `SafeHandle` base class, `GCHandle`, `StructLayout` |
| `System.Collections.Concurrent` | `ConcurrentDictionary` for thread-safe cache/registry |
| `System.Reflection` | `HasManagedOverride` — `GetMethod`, `GetBaseDefinition` |
| `System.Text.Encoding` | `StringBase` — UTF-8 encode/decode |
| Native side (`Marshaller.hpp`) | `StringBase` virtual overrides correspond to `std::string` operations exposed by the `Marshaller<std::string>` specialization |

### Consumers (depend on this layer)

| Consumer | Relationship |
|---|---|
| **Generated wrapper types** | Extend `NativeObject`, implement `INativeObjectRegistration<T>`, register in `NativeObjectRegistry`. Call `Create<T>()` to materialize wrappers from handles. |
| **Generated P/Invoke marshallers** | Use `NativeObject.GetHandle()` / `RequireHandle()` to extract `nint` from managed wrappers before calling into native code. |
| **Generated string wrappers** | Extend `StringBase` and override the four core methods. Registered with the `Marshaller<std::string>` ABI pair. |
| **Toolchain (SharpCpp.Pipeline + SharpCpp.Rendering)** | References this assembly to emit `: base(handle, ownsHandle)` calls, `INativeObjectRegistration<T>` implementations, and `NativeObjectRegistry.RegisterType<T>()` invocations. |

### Thread Safety

- `DefaultNativeInstanceCache` is fully thread-safe (`ConcurrentDictionary`).
- `NativeObjectRegistry` is thread-safe after registration (registrations happen in a static initializer / module initializer phase; lookups are concurrent-safe).
- `NativeObject` per-instance state (`_managedKeepAlive`, `handle` via `SafeHandle`) follows `SafeHandle`'s own thread-safety contract (critical finalizer, no concurrent `Dispose`).
