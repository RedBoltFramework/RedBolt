# 02 — Marshaller System

The marshaller system is the central design. Every type crossing the C++/C# boundary is configured by its `Marshaller<T>` specialization.

## Core idea

A `Marshaller<T>` specialization is the single source of truth for a type:

1. How the type appears in managed signatures (`public_type`)
2. What its ABI representation is (`abi_type`)
3. How to convert between managed and ABI representations (`marshaller_type`)
4. What extra members to inject into the wrapper (`[[sharpcpp::expose_member(...)]]`)

The generator reads these facts from C++ attributes and emits C# code. The generator core does not branch on type categories.

Example marshaller type names and helper APIs in this document are illustrative. V2 fixes the marshalling contract, not the final support-runtime naming/layout.

## Marshaller specialization shape

```cpp
template <>
struct [[sharpcpp::managed_type(
    public_type = "StdString",
    managed_base_type = "global::SharpCpp.Support.StringBase",
    abi_type = "Utf8StringAbi",
    marshaller_type = "Utf8StringMarshaller"
)]]
Marshaller<std::string>
{
    using cpp_type = std::string;
    using abi_type = detail::Utf8StringAbi;
};
```

The `[[sharpcpp::managed_type(...)]]` attribute declares everything the C# side needs. The C++ type aliases (`cpp_type`, `abi_type`) declare everything the C++ side needs. There is one truth.

No `string_kind` — strings follow the same marshaller path as every other type. Different string surfaces (span, wrapper, managed) are different marshaller specializations, not a separate category.

## Marshaller contexts

A type may marshal differently depending on context. `MarshallerSelector` provides context-dependent resolution:

```cpp
// Parameter context: pass by value-ABI-pointer
template <typename T>
struct MarshallerSelector<T, MarshallerContext::Parameter>
{
    using type = ValueParameterMarshallerBase<T, Marshaller<T>>;
};

// Return context: pass by value directly
// (default MarshallerSelector just uses Marshaller<T>)
```

On the C# side, the attribute can declare context-specific overrides:

```cpp
struct [[sharpcpp::managed_type(
    public_type = "T",
    abi_type = "OptionalValueAbi<T>",
    marshaller_type = "OptionalValueMarshaller<T>",
    parameter_marshaller_type = "OptionalValueMarshaller<T>",
    return_marshaller_type = "OptionalValueMarshaller<T>"
)]]
Marshaller<std::optional<T>> { ... };
```

When `parameter_marshaller_type` or `return_marshaller_type` differ from `marshaller_type`, the renderer uses the context-specific one.

## LibraryImport integration

The C# side uses `[LibraryImport]` with `[MarshalUsing]` attributes. The .NET runtime's source-generated marshalling calls the marshaller types automatically — no hand-rolled `ConvertToUnmanaged`/`ConvertToManaged` calls in generated C# wrapper methods.

### In parameters

The public type appears in both the LibraryImport and the public wrapper. `MarshalUsing` drives parameter marshalling:

```csharp
[LibraryImport("libnative")]
internal static partial void __Print(
    [MarshalUsing(typeof(Utf8SpanMarshaller))] ReadOnlySpan<byte> message);

public void Print(ReadOnlySpan<byte> message) => __Print(message);
```

The public method is a direct pass-through. `Utf8SpanMarshaller.ConvertToUnmanaged` is called by the runtime before the native call.

### Returns

For returns where the ABI type differs from the public type, `[return: MarshalUsing]` drives conversion:

```csharp
[LibraryImport("libnative")]
[return: MarshalUsing(typeof(Utf8StringMarshaller))]
internal static partial StdString __GetName(nint self);

public StdString Name => __GetName(_handle);
```

`Utf8StringMarshaller.ConvertToManaged` is called by the runtime after the native call returns.

### When manual conversion is needed

Some patterns require manual ABI handling in the public wrapper because `MarshalUsing` cannot express them:

| Pattern | Why manual | Example |
|---|---|---|
| `string` `out`/`ref` parameters | No `MarshalUsing` on `ref` for non-blittable ABI types | `out Utf8StringAbi` → manual `string` conversion |
| Return span from `Utf8StringAbi` | Span lifetime tied to native memory, can't be `MarshalUsing` return | `abi.AsReadOnlySpan()` with `FreeOwnerIfNeeded()` |
| Public `ref string` surface over wrapper ABI | Public type differs from wrapper ABI type | `ref Utf8StringAbi` + manual sync back to `string` |

In these cases, the LibraryImport signature uses the ABI type, and the public wrapper performs the conversion.

### Callbacks

Callback delegate types use `MarshalUsing` on their parameters and returns:

```csharp
[UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
internal delegate void OnMessageDelegate(
    [MarshalUsing(typeof(Utf8SpanMarshaller))] ReadOnlySpan<byte> message);
```

The same marshaller types serve both P/Invoke and callback marshalling.

## Marshaller invocation pipeline

The marshaller system has two invocation paths: C++ (native thunks) and C# (LibraryImport source generation). Both use the same type information from `Marshaller<T>`.

### C++ invocation path

Generated C++ thunks call through `MarshallerInvoker`, which selects `ParameterMarshaller<T>` or `ReturnMarshaller<T>` based on context (`MarshallerContext::Parameter` vs `MarshallerContext::Return`):

```text
P/Invoke call (ABI args)
    ↓
MarshallerInvoker::invoke / invoke_as / invoke_member
    ↓
Constructs MarshallerTuple<CppArgumentTuple>  (parameter_marshaller_t per arg)
    ↓
Each parameter_marshaller_t<T>.ConvertToCpp(abiValue)  →  C++ value
    ↓
Invokes the C++ method
    ↓
return_marshaller_t<TResult>.ConvertToC(cppResult)  →  ABI return
    ↓
SyncMarshaller on each parameter marshaller  (for ref/out sync-back)
```

For callbacks (managed → native):

```text
Managed delegate invoke (C# values)
    ↓
MarshallerInvoker::invoke_callback_as<Signature>
    ↓
InvokeCallbackWithSignature  constructs  parameter_marshaller_t per arg
    ↓
Each parameter_marshaller_t<T>.ConvertToC(cppValue)  →  ABI args
    ↓
Calls the native function pointer with ABI args
    ↓
return_marshaller_t<TResult>.ConvertToCpp(abiReturn)  →  C# value
```

For field access:

```text
MarshallerInvoker::GetFieldValue / SetFieldValue
    ↓
return_marshaller_t<TResult>.ConvertToC(value)  (getter)
parameter_marshaller_t<T>.ConvertToCpp(abiValue)  (setter)
```

### C# invocation path

`[LibraryImport]` source generation reads `[MarshalUsing]` attributes and calls the corresponding `CustomMarshaller` methods:

```text
Caller passes public_type value
    ↓
[MarshalUsing(typeof(XMarshaller))] → XMarshaller.ConvertToUnmanaged(value)
    ↓
Native call receives ABI representation
    ↓
XMarshaller.ConvertToManaged(abiValue)  (for out/ref/return)
    ↓
Caller receives public_type value
```

The C# `CustomMarshaller` types implement the `ICustomMarshaller` pattern (`ConvertToUnmanaged`, `ConvertToManaged`, `Free`). These live in the support library (for built-in types) or are generated per-instantiation (for generic types like `OptionalValueMarshaller<T>`).

## Extractable marshaller contract

V2 does **not** inspect arbitrary C++ marshaller implementation bodies. The generator may read only a small, explicit contract from `Marshaller<T>`:

### Generator-readable surface

1. `[[sharpcpp::managed_type(...)]]` fields
2. Named type aliases such as `cpp_type`, `abi_type`, and other fixed-schema aliases
3. `[[sharpcpp::expose_member(...)]]` on static helper methods
4. A small set of named capability flags / traits with fixed semantics

### Generator-opaque surface

The generator must treat these as opaque implementation details:

- Arbitrary C++ method bodies inside marshallers
- Template metaprogramming internals beyond named aliases / traits
- Ad-hoc helper functions without SharpCpp attributes
- Control flow inside support-library marshaller implementations

If the generator needs a new fact, the plan is to add a new explicit attribute field or named trait — not to parse more C++.

### Who supplies the C# marshaller implementation

Every `marshaller_type` named in metadata must come from one of two places:

| Source | When used |
|---|---|
| Support library | Built-in surfaces such as strings, callbacks, handles, common container bases |
| Generator-owned template emission | Structural families like optional / pair / tuple / per-instantiation helper marshallers |

The generator does not translate arbitrary C++ `Marshaller<T>` code into C#. It emits from fixed templates or references support-library types by name.

## Marshaller type emission

Each custom surface names a C# `CustomMarshaller` type. Some are supplied by the support library, others by generator-owned templates:

```csharp
[CustomMarshaller(typeof(string), MarshalMode.Default, typeof(Utf8StringMarshaller))]
internal static partial class Utf8StringMarshaller
{
    public static Utf8StringAbi ConvertToUnmanaged(string? value) { ... }
    public static string? ConvertToManaged(Utf8StringAbi value) { ... }
    public static void Free(Utf8StringAbi value) { ... }
}
```

The C# marshaller implementation mirrors the declared contract of the C++ `Marshaller<T>`, not its arbitrary implementation details. The marshaller type itself lives in the support library (for built-in types) or is emitted from a generator-owned template (for structural families).

### Built-in vs generated marshallers

| Category | Where the C# marshaller lives | Who emits it |
|---|---|---|
| Primitives (int, bool, etc.) | No marshaller needed (blittable) | N/A |
| Handles (nint) | No marshaller needed (blittable) | N/A |
| Strings (span, owned, managed) | `SharpCpp.Support` library | N/A (pre-built) |
| `std::optional<T>` | Generated per-T | Generator |
| `std::pair<T1, T2>` | Generated per-T1,T2 | Generator |
| `std::vector<T>` | `SharpCpp.Support` base + per-T generated accessors | Generator |
| Callbacks | Generated per-signature | Generator |
| User value types | Generated per-type or referenced from support lib | Generator |

## Member injection

The `[[sharpcpp::expose_member(...)]]` attribute on static marshaller methods declares members to inject into the wrapper type:

```cpp
template <typename T>
struct Marshaller<std::vector<T>>
{
    [[sharpcpp::expose_member(shape = "protected_override")]]
    static unsigned long long GetCountCore(const std::vector<T>& self)
    {
        return self.size();
    }

    [[sharpcpp::expose_member(shape = "protected_override")]]
    static T GetAtCore(const std::vector<T>& self, unsigned long long index)
    {
        return self[static_cast<std::size_t>(index)];
    }
};
```

The generator reads these and creates `FunctionBinding` nodes with native bodies that call through the marshaller.

## Builtin marshaller reuse

Users with custom stdlib implementations (e.g., EASTL instead of STL) can reuse builtin marshallers by inheriting from them:

```cpp
// Reuse the vector marshaller shape for an EASTL vector
template <typename T>
struct Marshaller<ea::vector<T>>
    : Marshaller<std::vector<T>>
{
    using Marshaller<std::vector<T>>::GetCountCore;
    using Marshaller<std::vector<T>>::GetAtCore;
};
```

Or by forwarding with the `reuse_builtin_marshaller` attribute:

```cpp
template <typename T>
struct [[sharpcpp::managed_type(
    reuse_builtin_marshaller = "std::vector<{0}>"
)]]
Marshaller<ea::vector<T>> : Marshaller<std::vector<T>> {};
```

The `reuse_builtin_marshaller` attribute tells the generator to use the same managed type, ABI type, and marshaller type as the referenced builtin.

## C++ side: generated thunks use marshallers

Generated C++ thunks use `InvokeWithSignature` which constructs `MarshallerTuple` and converts each argument through its marshaller:

```cpp
extern "C" void* __sc_MyClass_MyMethod(void* self, Utf8StringAbi name_abi)
{
    auto* instance = static_cast<MyClass*>(self);
    parameter_marshaller_t<std::string> marshaller;
    auto name = marshaller.ConvertToCpp(name_abi);
    auto result = instance->MyMethod(name);
    SyncMarshaller(marshaller);
    return_marshaller_t<MyClass*> return_marshaller;
    return return_marshaller.ConvertToC(result);
}
```

This is generated uniformly from marshaller metadata and the fixed `ParameterMarshaller<T>` / `ReturnMarshaller<T>` contract — no type-category branching.

## Callbacks and marshallers

See [[04-type-handling]] for the full callback design. Key point: callbacks use the same `ParameterMarshaller<T>` / `ReturnMarshaller<T>` path as non-callback code. The C# side matches by using `MarshalUsing` on callback delegate parameters.

## Reading guide

- [[01-architecture]] — overall architecture
- [[03-two-phase-parsing]] — how marshaller attributes are read
- [[04-type-handling]] — per-type-category marshaller designs
- [[05-configuration-attributes]] — full attribute vocabulary
- [[07-marshaller-contract]] — exact contract the generator may extract
- [[10-ownership-lifetime]] — when manual conversion is still required

---

← [[01-architecture]] | [[03-two-phase-parsing]] →
