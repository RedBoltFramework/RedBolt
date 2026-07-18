# include/SharpCpp/

## Responsibility

The **Native Support Headers** layer provides the C++ side of the managed/unmanaged interop boundary. It owns the **Marshaller type system**, **P/Invoke export macros**, and **runtime invocation machinery** that generated `.g.cpp` files depend on to bridge between C++ native code and the C#/CLR managed heap.

This layer is header-only, lives entirely in `namespace SharpCpp`, and has **zero runtime dependencies** beyond the C++ standard library. It is consumed at compile time by generated code and by the native test harness (`SharpCppTestsE2E_Native`).

## Design Patterns

### 1. Marshaller Template — `Marshaller<T>`

Defines a **two-type mapping** per C++ type:

| Alias       | Meaning                        |
|-------------|--------------------------------|
| `cpp_type`  | The original C++ type          |
| `abi_type`  | The ABI-safe representation passed across P/Invoke |

Every marshaller provides a pair of conversion functions:

- `ConvertToAbi(cpp_type) → abi_type` — C++ → ABI (before call to managed)
- `ConvertToCpp(abi_type) → cpp_type` — ABI → C++ (after call from managed)

**Primary template** (identity): when `cpp_type == abi_type`, conversion is a no-op. This covers primitives, enums, and opaque pointers.

**Specializations** handle type families that differ between C++ and ABI representations:
- **Reference marshalling** (`T&`, `const T&`): references become pointers across ABI boundary (calling convention safety).
- **Pointer marshalling** (`T*`, `const T*`): passthrough with recursive ABI type resolution via `Marshaller<T>::abi_type*`.
- **`const char*` and `char**`/`const char**`**: tagged with `[[sharpcpp::managed_type(...)]]` for the C# generator to map to `System.ReadOnlySpan<byte>` or `string`.
- **`std::string`**: ABI type becomes `std::string*` (heap-allocated, pass-by-pointer). Exposes `GetCountCore`, `AddCore`, `ClearCore` hooks annotated as marshaller members that the generator emits as managed overrides.

**`ParameterMarshaller<T>`** → alias for `Marshaller<T>` (parameters always pass through the standard mapping).

**`ReturnMarshaller<T>`** → inherits from `Marshaller<T>` but can override return-specific behavior. The key example: `ReturnMarshaller<const char*>` is tagged with `marshaller_type = "global::System.IntPtr"`, which tells the generator to marshal return strings via `IntPtr` rather than `ReadOnlySpan<byte>`.

**`arg_t<T>`** / **`ret_t<T>`** — convenience aliases for `Marshaller<T>::abi_type` / `ReturnMarshaller<T>::abi_type`, used by generated code to declare P/Invoke function signatures.

### 2. Metadata Attributes — `[[sharpcpp::managed_type(...)]]`

Non-standard C++ attributes consumed by the **SharpCpp parser** (not the compiler). These annotate marshaller specializations with metadata that drives C# code generation:

- `public_type` — the C# type on the managed side
- `managed_base_type` — the C# base class for type hierarchy mapping
- `undefaultable` — prevents the C# generator from emitting a default value
- `marshaller_type` — overrides the intermediate type used in P/Invoke signatures

`[[sharpcpp::expose_member(...)]]` annotates marshaller-provided helper methods (e.g. `shape = "protected_override"`) that the generator emits as virtual members on the managed wrapper.

### 3. MarshallerInvoker — `MarshallerInvoker`

A static-polymorphic invocation hub that **generated `.g.cpp` wrapper functions** use to translate ABI-level calls back into C++:

- **`invoke_as<FT>(callable, abiArgs...)`** — free function: converts ABI args → C++ args, invokes, converts result back to ABI.
- **`invoke<FT>(method, selfAbi, abiArgs...)`** — member function via ABI self pointer: marshals the `this` pointer, marshal arguments, invoke, marshal return.
- **`invoke_instance<FT>(method, instance*, abiArgs...)`** — member function via known C++ pointer (skips self marshalling).
- **`construct<T, Args...>(abiArgs...)`** — heap-allocate a new object, return an ABI pointer.
- **`destroy<T>(self)`** — `delete` an object from an ABI pointer.
- **`get_field<TResult TClass::*, self>` / `set_field<...>`** — field read/write through marshaller conversions, including borrowed-storage and fixed-array helper shapes.

### 4. Sync Pattern — Destructor-Based `ParameterMarshaller<T>`

`ParameterMarshaller<T>` wraps `Marshaller<T>` in an RAII adapter. If the configured marshaller exposes `Sync()`, the adapter calls it from its destructor. This keeps two-way marshalling scenarios scoped to the marshaller lifetime and avoids generated per-call `SyncMarshaller(...)` statements.

### 5. Optional Support — `OptionalValue<T>`

A trivial `{bool HasValue; T Value;}` struct. Used when the ABI boundary cannot represent `std::optional<T>` directly (e.g. because the CLR struct-layout rules differ). The C# side maps this to `T?` / `Nullable<T>`.

## Flow

```
  [Managed Code]
       |
       | P/Invoke call (ABI types)
       v
  [Generated .g.cpp Export Function]
       |
       | SHARPCPP_EXPORT extern "C" ret_t Func(arg_t arg)
       | {
       |     return MarshallerInvoker::invoke_as<FT>(&Impl::Func, arg);
       | }
       |
       v
  [MarshallerInvoker]
       |
       | 1. Build tuple<ParameterMarshaller<Args>...> (one per ABI arg)
       | 2. For each arg: marshaller.ConvertToCpp(abiValue) → cpp_type
       | 3. Invoke the actual C++ function/method
       | 4. Sync marshallers (write-back)
       | 5. Convert result: ReturnMarshaller<R>.ConvertToAbi(cppResult) → abi_type
       | 6. Return ABI result to managed caller
       |
       v
  [Actual C++ Implementation]
```

## Integration Points

- **Generated `.g.cpp` files** — Every P/Invoke-exported wrapper function:
  1. Declares its signature with `SHARPCPP_EXPORT` and `arg_t`/`ret_t` types.
  2. Delegates to `MarshallerInvoker::invoke`, `invoke_as`, or `invoke_instance`.
  3. The generator reads `[[sharpcpp::managed_type(...)]]` attributes from marshaller specializations to emit matching C# P/Invoke declarations.

- **`SharpCppTestsE2E_Native`** — The native test DLL links against these headers directly. Its test functions use `Marshaller<T>` explicitly (without the generator) to validate correctness of marshalling round-trips, sync semantics, and edge cases.

- **`SharpCpp.Parsing`** — The C++ parser (`src/SharpCpp.Parsing/`) reads these headers to extract `[[sharpcpp::managed_type(...)]]` and `[[sharpcpp::expose_member(...)]]` attributes. This metadata populates the binding model that drives C# code generation.

- **`SharpCpp.Metadata`** — The marshaller attribute annotations are a form of metadata embedding; the parser/projection layer reads them and converts them into `AssemblyAttribute` records in the binding model.
