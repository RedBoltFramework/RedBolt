# Strings

Strings follow the same marshaller path as every other type. Different surfaces are different `Marshaller<T>` specializations. No `string_kind` field.

## Marshalling convention

Where possible, `[MarshalUsing]` on `LibraryImport` parameters and returns drives conversion automatically — the public wrapper is a direct pass-through. Manual conversion (`AsReadOnlySpan()`, `ConvertToAbi()`, etc.) appears only when `MarshalUsing` cannot express the pattern:

| Pattern | Conversion | Why |
|---|---|---|
| In parameter | `MarshalUsing` | Runtime calls `ConvertToUnmanaged` |
| Return value | `MarshalUsing` | Runtime calls `ConvertToManaged` |
| `StdString` `out`/`ref` parameter | `MarshalUsing` | Runtime handles bidirectional conversion |
| `string` `out`/`ref` parameter | Manual | Public type differs from ABI type; manual `ConvertToAbi`/`AsString()` |
| Return span from `Utf8StringAbi` | Manual | Span lifetime tied to native memory; `AsReadOnlySpan()` + `FreeOwnerIfNeeded()` |
| Field getter/setter | `MarshalUsing` on P/Invoke | P/Invoke uses `MarshalUsing`; public property is a pass-through |

## Design principle: span first

**Span is the default string surface.** `string` (managed heap-allocated) causes GC pressure. Every string binding should prefer `ReadOnlySpan<byte>` or a wrapper unless the caller explicitly opts into `string`. The goal is zero GC allocations on the hot path.

- `std::string_view` → `ReadOnlySpan<byte>` (default)
- `std::string` → `StdString` wrapper (default for owned strings)
- `const char*` → `ReadOnlySpan<byte>` (default for parameters), `StdString` or `string` for returns depending on ownership
- Per-parameter `[[sharpcpp::string_binding(...)]]` can downgrade to `string` when needed

## Surface hierarchy (GC pressure, low → high)

| Surface | GC pressure | When to use |
|---|---|---|
| `ReadOnlySpan<byte>` | Zero | Default for parameters and most returns |
| `StdString` (wrapper) | Low (one native allocation) | Owned strings that must outlive the call |
| `string` | High (managed heap) | Only when the caller must store/interpolate/compare repeatedly |

## `std::string_view` → `ReadOnlySpan<byte>`

Zero-copy view over existing UTF-8 data. No managed heap allocation.

```cpp
template <typename TChar, typename TTraits, typename TAllocator>
struct [[sharpcpp::managed_type(
    public_type = "ReadOnlySpan<byte>",
    parameter_marshaller_type = "Utf8SpanMarshaller",
    return_marshaller_type = "Utf8SpanMarshaller",
    abi_type = "Utf8StringAbi"
)]]
Marshaller<std::basic_string_view<TChar, TTraits>> { ... };
```

Both parameter and return use `Utf8SpanMarshaller`. The span points into native memory — no copy, no GC.

### Parameter — in (zero-copy)

```cpp
void Print(std::string_view message);
```

```csharp
[LibraryImport("libnative")]
internal static partial void __Print([MarshalUsing(typeof(Utf8SpanMarshaller))] ReadOnlySpan<byte> message);
public void Print(ReadOnlySpan<byte> message) => __Print(message);
```

The caller passes a `ReadOnlySpan<byte>`. If the caller has a `string`, they must encode to UTF-8 first (or use a `string` overload if available). This is intentional — encoding is the caller's responsibility, not the binding's.

### Parameter — in with `string` override

When a caller needs the convenience of `string`, they opt in per-parameter:

```cpp
void Log([[sharpcpp::string_binding("string")]] std::string_view message);
```

```csharp
[LibraryImport("libnative")]
internal static partial void __Log([MarshalUsing(typeof(Utf8StringMarshaller))] string message);
public void Log(string message) => __Log(message);
```

This allocates — but only because the programmer explicitly asked for it.

### Return — zero-copy view

```cpp
std::string_view GetName() const;
```

```csharp
[LibraryImport("libnative")]
internal static partial Utf8StringAbi __GetName(nint self);
public ReadOnlySpan<byte> Name
{
    get
    {
        var abi = __GetName(_handle);
        return abi.AsReadOnlySpan();
    }
}
```

The returned span points into the native object's memory. Valid only while the native object is alive and unchanged. This is unsafe by design — the alternative is always copying to `string`, which allocates.

### Return — when span is insufficient

If the caller must store the returned value beyond the native object's lifetime, they can copy explicitly:

```csharp
ReadOnlySpan<byte> rawName = obj.Name;           // zero-copy, zero-GC
string storedName = rawName.AsString();            // explicit allocation, opt-in
```

The `AsString()` extension method is provided by the support library for explicit opt-in.

## `std::string` → `StdString` wrapper

Owned, heap-allocated string. The `StdString` wrapper holds a native `std::string` via handle. No managed `string` on the heap.

```cpp
struct [[sharpcpp::managed_type(
    public_type = "StdString",
    managed_base_type = "global::SharpCpp.Support.StringBase",
    abi_type = "Utf8StringAbi",
    marshaller_type = "Utf8StringMarshaller"
)]]
Marshaller<std::string> { ... };
```

### Return (owned)

```cpp
std::string GetName() const;
```

```csharp
[LibraryImport("libnative")]
[return: MarshalUsing(typeof(Utf8StringMarshaller))]
internal static partial StdString __GetName(nint self);

public StdString Name => __GetName(_handle);
```

`MarshalUsing` drives the return conversion. `Utf8StringMarshaller.ConvertToManaged` is called by the runtime after the native call.

### Parameter — value (ownership transfer)

```cpp
void SetName(std::string name);
```

```csharp
[LibraryImport("libnative")]
internal static partial void __SetName(nint self, [MarshalUsing(typeof(Utf8StringMarshaller))] StdString name);
public void SetName(StdString name) => __SetName(_handle, name);
```

`MarshalUsing` drives the conversion. `Utf8StringMarshaller.ConvertToUnmanaged` is called by the runtime before the native call.

### Parameter — from span

When the caller has a `ReadOnlySpan<byte>`, they can construct the `StdString` from it:

```csharp
public void SetName(ReadOnlySpan<byte> name) => SetName(new StdString(name));
```

This pattern (span → wrapper → native) keeps `string` completely out of the hot path.

### Converting to `string` (opt-in)

```csharp
StdString nativeName = obj.Name;                    // StdString — zero GC
string managedName = nativeName.AsString();           // explicit opt-in
```

`AsString()` is available on `StdString` but never called automatically.

## `const char*` → `ReadOnlySpan<byte>` (parameter), varies (return)

### Parameter — `const char*` → span (default)

```cpp
void SetLabel(const char* label);
```

```csharp
[LibraryImport("libnative")]
internal static partial void __SetLabel([MarshalUsing(typeof(Utf8SpanMarshaller))] ReadOnlySpan<byte> label);
public void SetLabel(ReadOnlySpan<byte> label) => __SetLabel(label);
```

`const char*` as parameter defaults to span. Zero-GC path.

### Parameter — with explicit `string` override

```cpp
void SetLabel([[sharpcpp::string_binding("string")]] const char* label);
```

```csharp
[LibraryImport("libnative")]
internal static partial void __SetLabel([MarshalUsing(typeof(Utf8StringMarshaller))] string label);
```

### Return — static C string → span (default for static strings)

```cpp
const char* GetStaticLabel();
```

For static storage strings, the span can safely outlive the call:

```csharp
[LibraryImport("libnative")]
internal static partial nint __GetStaticLabel();
public ReadOnlySpan<byte> StaticLabel
{
    get
    {
        var ptr = __GetStaticLabel();
        return ptr == 0 ? ReadOnlySpan<byte>.Empty : MarshalUtf8.GetSpanFromNullTerminated(ptr);
    }
}
```

### Return — owned `const char*` → span with lifetime management

```cpp
const char* GetOwnedLabel();
```

The native side owns the string. The returned span is valid until the native object is mutated or destroyed. Manual conversion is required because the span's lifetime is tied to the native object — `MarshalUsing` cannot express this:

```csharp
[LibraryImport("libnative")]
internal static partial Utf8StringAbi __GetOwnedLabel(nint self);
public ReadOnlySpan<byte> OwnedLabel
{
    get
    {
        var abi = __GetOwnedLabel(_handle);
        try { return abi.AsReadOnlySpan(); }
        finally { abi.FreeOwnerIfNeeded(); }
    }
}
```

### Return — `char*` (ownership transfer)

```cpp
char* AllocateMessage();
```

Ownership transfers to the caller. The ABI `owner` field carries the freeable pointer. Manual conversion because `StdString` construction from `Utf8StringAbi` is not expressible via `MarshalUsing`:

```csharp
[LibraryImport("libnative")]
internal static partial Utf8StringAbi __AllocateMessage();
public StdString AllocateMessage()
{
    var abi = __AllocateMessage();
    return new StdString(abi);
}
```

`StdString` takes ownership and frees native memory on `Dispose`.

## `[[sharpcpp::string_binding(...)]]` per-parameter override

Overrides the default string surface for a specific parameter:

```cpp
void Log([[sharpcpp::string_binding("string")]] std::string_view message);
```

Values:
- `"span"` → `ReadOnlySpan<byte>` — zero-GC, zero-copy
- `"string"` → `string` — managed heap allocation, GC pressure
- `"wrapper"` → `StdString` — native heap allocation, no GC pressure
- `"auto"` → type's default (no override)

The default string binding for the project can be set in configuration. With the span-first principle, the default should be `"span"`.

## Pass direction variants

### in (default for const reference)

```cpp
void Consume([[sharpcpp::in]] const std::string& text);
```

```csharp
[LibraryImport("libnative")]
internal static partial void __Consume(nint self, [MarshalUsing(typeof(Utf8StringMarshaller))] StdString text);
public void Consume(StdString text) => __Consume(_handle, text);
```

`MarshalUsing` drives the conversion. The handle is borrowed — no ownership transfer.

### out

```cpp
void GetName([[sharpcpp::out]] std::string* name);
```

```csharp
[LibraryImport("libnative")]
internal static partial void __GetName(nint self, [MarshalUsing(typeof(Utf8StringMarshaller))] out StdString name);
public StdString Name => __GetName(_handle);
```

`MarshalUsing` on `out` parameters drives bidirectional conversion. The `StdString` is constructed by the runtime from the native `Utf8StringAbi`.

### ref

```cpp
void Append([[sharpcpp::ref]] std::string& text);
```

```csharp
[LibraryImport("libnative")]
internal static partial void __Append(nint self, [MarshalUsing(typeof(Utf8StringMarshaller))] ref StdString text);
public void Append(ref StdString text) => __Append(_handle, ref text);
```

`MarshalUsing` on `ref` drives `ConvertToUnmanaged` on entry, `ConvertToManaged` on exit. Bidirectional sync is handled by the marshaller.

### ref/out with `string` surface

When the public surface must be `string` (not `StdString`), manual conversion is needed because `ref string` with `out`/`ref` semantics requires an ABI intermediate:

```csharp
// out string — manual because the ABI type differs from the public type
internal static partial void __GetName(nint self, out Utf8StringAbi name);
public string GetName()
{
    __GetName(_handle, out var abi);
    return abi.AsString();
}

// ref string — manual bidirectional conversion
internal static partial void __Append(nint self, ref Utf8StringAbi text);
public void Append(ref string text)
{
    var abi = Utf8StringMarshaller.ConvertToAbi(text);
    __Append(_handle, ref abi);
    text = abi.AsString();
}
```

This allocation is inherent to `ref`/`out` with `string` — not a design choice. Use `StdString` with `MarshalUsing` instead when possible.

## String field access

Field access uses dedicated P/Invoke getters/setters with ABI types. `MarshalUsing` cannot apply here because fields aren't P/Invoke parameters — the getter/setter P/Invokes use ABI types directly, and the public property wraps them.

### `std::string` field → `StdString` property

```cpp
std::string name;
```

```csharp
public StdString Name
{
    [return: MarshalUsing(typeof(Utf8StringMarshaller))]
    get => __GetName(_handle);
    set => __SetName(_handle, value);
}
```

The getter P/Invoke returns `StdString` via `MarshalUsing`; the setter P/Invoke takes `StdString` via `MarshalUsing`.

### `std::string_view` field → `ReadOnlySpan<byte>` property

```cpp
std::string_view label;
```

```csharp
public ReadOnlySpan<byte> Label
{
    get
    {
        var abi = __GetLabel(_handle);
        return abi.AsReadOnlySpan();
    }
    set => __SetLabel(_handle, value);
}
```

Span returns require manual `AsReadOnlySpan()` because the span points into native memory — `MarshalUsing` cannot express this lifetime dependency. The setter can use `MarshalUsing` for the parameter since it's a forward-only conversion.

### `const char*` field → pinned span with `AssignManagedUtf8FieldStorage`

When a C++ struct stores `const char*` that points to managed-pinned memory:

```cpp
const char* label;  // points into AssignManagedUtf8FieldStorage-managed memory
```

```csharp
public ReadOnlySpan<byte> Label
{
    get => __GetLabel(_handle).AsReadOnlySpan();
    set => __SetLabel(_handle, AssignManagedUtf8FieldStorage(_handle, "label", value));
}
```

Both getter and setter use dedicated P/Invokes with ABI types. The setter pins the span content in native-accessible storage. The getter returns a span into native memory.

## Callback string parameters

String parameters in callback signatures follow the same span-first rule:

```csharp
[UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
internal delegate void OnMessageDelegate(
    [MarshalUsing(typeof(Utf8SpanMarshaller))] ReadOnlySpan<byte> message);
```

No `string` allocation in the callback path. If callback consumers need `string`, they convert explicitly.

---

← [[01-handles]] | [[03-sequential-containers]] →