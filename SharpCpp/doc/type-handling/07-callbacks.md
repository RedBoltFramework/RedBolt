# Callbacks

Callbacks reuse the same `ParameterMarshaller<T>` / `ReturnMarshaller<T>` primitives as non-callback code, but they still have a dedicated ABI and lifetime contract.

## Managed → Native (delegates)

C# calls C++ through P/Invoke with a delegate parameter. The delegate type carries `MarshalUsing` annotations on its parameters.

```cpp
void RegisterCallback(void (*callback)(int, float));
```

```csharp
[UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
internal delegate void CallbackDelegate(int value, float scale);

[LibraryImport("libnative")]
internal static partial void __RegisterCallback(CallbackDelegate callback);
```

When parameters are non-blittable:

```cpp
void RegisterStringCallback(void (*callback)(const char*));
```

```csharp
[UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
internal delegate void StringCallbackDelegate(
    [MarshalUsing(typeof(Utf8SpanMarshaller))] ReadOnlySpan<byte> name);

[LibraryImport("libnative")]
internal static partial void __RegisterStringCallback(StringCallbackDelegate callback);
```

The `.NET` source generator handles the conversion. The generator still owns callback ABI selection, delegate emission, and lifecycle validation.

## Native → Managed (managed overrides of C++ virtual methods)

C++ class marked with `[[sharpcpp::director]]` allows C# to override virtual methods:

```cpp
class [[sharpcpp::director]] MyWidget : public Widget
{
    void OnUpdate(float dt) override;
    int GetPriority() const override;
};
```

The generator emits:
1. A C++ director class that overrides virtual methods to call managed callbacks
2. A C# `ConnectDirector()` method that creates the director and wires callbacks
3. Override infrastructure on the C# side

See [[08-inheritance]] for full details.

## Callback ABI: ManagedCallbackHandleAbi

For callbacks that capture managed state, the ABI is `ManagedCallbackHandleAbi`:

```cpp
struct ManagedCallbackHandleAbi
{
    void* state;
    void* callback;
    void (*release)(void*);
};
```

Three fields:
- **state** — opaque pointer to managed delegate holder
- **callback** — function pointer to a static invoke stub
- **release** — function pointer to destroy the holder

## Lifetime contract

`ManagedCallbackHandleAbi` owns callback state until `release(state)` fires.

- registration allocates holder state
- native storage may copy the ABI handle
- final native release must call `release`
- release must fire exactly once for a live holder

The generator assumes callback storage obeys that contract. If native code stores callback handles without calling `release`, the leak is native-side undefined behavior, not a managed cache problem.

## Exception policy

Managed exceptions must not cross the native ABI boundary unchecked.

- callback adapters catch managed exceptions
- report them through a central SharpCpp callback/director exception sink
- treat them as fatal by default unless a support-runtime policy explicitly handles the boundary

V2 makes this policy explicit so callback failure behavior is not ad-hoc per feature.

### C++ side: CreateManagedCallbackHandle

```cpp
template <typename TSignature, typename TCallback>
ManagedCallbackHandleAbi CreateManagedCallbackHandle(TCallback&& callback);
```

Wraps a C++ lambda or `std::function` into the three-field ABI. Managed code calls this to produce the handle that crosses the boundary.

### C# side: __FunctionObjectMarshaller<T>

Unwraps the handle. The `.NET` source generator uses `MarshalUsing` on delegate parameters to produce conversion code.

## Raw function pointers

For simple C function pointers (no managed state), the ABI is just the raw pointer. No `ManagedCallbackHandleAbi` overhead:

```cpp
void SetComparator(int (*cmp)(int, int));
```

```csharp
[UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
internal delegate int ComparatorDelegate(int a, int b);

[LibraryImport("libnative")]
internal static partial void __SetComparator(ComparatorDelegate cmp);
```

The `Marshaller<T*>` for function-pointer types declares `abi_type = function_pointer_type` directly.

## std::function

```cpp
template <typename T>
struct [[sharpcpp::managed_type(
    callback = true,
    public_type = "Action<{0}>",
    abi_type = "ManagedCallbackHandleAbi",
    marshaller_type = "ManagedCallbackMarshaller<{0}>"
)]]
Marshaller<std::function<T>> { ... };
```

`callback = true` tells the generator to emit a delegate type. The `public_type` uses `{0}` for the return/parameter type substitution.

### Callback with non-blittable parameters

```cpp
using StringCallback = std::function<void(std::string_view)>;
void SetCallback(StringCallback cb);
```

```csharp
[UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
internal delegate void StringCallbackDelegate(
    [MarshalUsing(typeof(Utf8SpanMarshaller))] ReadOnlySpan<byte> name);

[LibraryImport("libnative")]
internal static partial void __SetCallback(ManagedCallbackHandleAbi cb);
public void SetCallback(Action<string> callback) => __SetCallback(ManagedCallbackMarshaller.CreateHandle(callback));
```

Each parameter in the delegate goes through its own `ParameterMarshaller<T>`.

### Callback with return value

```cpp
using Predicate = std::function<bool(int)>;
void SetPredicate(Predicate pred);
```

```csharp
[UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
internal delegate bool PredicateDelegate(int value);

[LibraryImport("libnative")]
internal static partial void __SetPredicate(ManagedCallbackHandleAbi pred);
```

The return value goes through `ReturnMarshaller<T>`. This is why `MarshallerSelector<T, Return>` exists — callback returns may need different ABI shapes than parameter passes.

## User data parameter

The `[[sharpcpp::callback_user_data(...)]]` attribute marks a parameter as the user-data companion to a callback:

```cpp
void RegisterCallback(
    void (*callback)(void*, int),
    [[sharpcpp::callback_user_data("callback")]] void* userData);
```

The generator omits the user-data parameter from the managed signature. The `callback_user_data("callback")` links it to the `callback` parameter by name.

## Threading and reentrancy

The generator assumes callbacks may arrive on arbitrary native threads unless the support runtime narrows the contract.

- callback state storage must be thread-safe
- instance caches / registries touched by callbacks must be concurrency-safe
- callback handlers must tolerate reentrancy when the native library allows it

## Callback pass direction

Callbacks are always passed as value (function pointer / handle). `out` and `ref` do not apply. The only variant is nullable callback (`std::function` that may be empty → managed as `Action<T>?` with null check).

---

← [[06-pair-tuple]] | [[08-inheritance]] →
