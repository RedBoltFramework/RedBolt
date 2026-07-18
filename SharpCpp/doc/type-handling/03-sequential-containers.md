# Sequential Containers

Each container type declares its own `Marshaller<T>` specialization with its own surface. No universal container abstraction. No `SourceKind` dispatch.

## std::vector → Std.Vector

Owned, growable, indexable container.

```cpp
template <typename T, typename TAllocator>
struct [[sharpcpp::managed_type(
    public_type = "Std.Vector<{0}>",
    managed_base_type = "global::SharpCpp.Support.VectorBase<{0}>",
    abi_type = "void*",
    marshaller_type = "StdVectorMarshaller<{0}>"
)]]
Marshaller<std::vector<T, TAllocator>>
{
    // ... expose_member declarations for Count, GetAt, SetAt, Add, InsertAt, RemoveAt, Clear ...
};
```

### Pass direction variants

**Return (owned):**
```cpp
std::vector<int> GetItems() const;
```
```csharp
public Std.Vector<int> Items => new Std.Vector<int>(__GetItems(_handle));
```
Native returns `new vector(...)`, C# takes ownership via `void*` handle.

**Parameter — value:**
```cpp
void SetItems(std::vector<int> items);
```
```csharp
[LibraryImport("libnative")]
internal static partial void __SetItems(nint self, nint items);
```
Wrapper passes underlying handle. Ownership semantics determined by the wrapper's `ownsHandle` flag.

**Parameter — const reference (in):**
```cpp
void ProcessItems([[sharpcpp::in]] const std::vector<int>& items);
```
```csharp
[LibraryImport("libnative")]
internal static partial void __ProcessItems(nint self, nint items);
```
Borrowed handle — no ownership transfer.

**Parameter — out:**
```cpp
void GetItems([[sharpcpp::out]] std::vector<int>* outItems);
```
```csharp
[LibraryImport("libnative")]
internal static partial void __GetItems(nint self, out nint outItems);
```

**Parameter — ref:**
```cpp
void AppendItems([[sharpcpp::ref]] std::vector<int>& items);
```
```csharp
[LibraryImport("libnative")]
internal static partial void __AppendItems(nint self, ref nint items);
```

### Element access

For non-blittable element types (handles, strings), container accessors use per-element marshalling:

```cpp
[[sharpcpp::expose_member(shape = "protected_override", exposed = false)]]
static T GetAtCore(const std::vector<T>& self, unsigned long long index)
{
    return self[static_cast<std::size_t>(index)];
}
```

The `T` return goes through `ReturnMarshaller<T>`. Reading: `GetAt` calls P/Invoke → native `GetAtCore` → returns `ReturnMarshaller<T>::abi_type` → C# converts to managed `T`. Writing: `SetAt` takes `ParameterMarshaller<T>::abi_type`.

## std::span → ReadOnlySpan or ArrayView

Borrowed, non-owning view over contiguous memory.

```cpp
// Span surface (default for std::span)
template <typename T>
struct [[sharpcpp::managed_type(
    public_type = "ReadOnlySpan<{0}>",
    abi_type = "ContiguousBufferAbi",
    marshaller_type = "SpanMarshaller<{0}>"
)]]
Marshaller<std::span<T>> { ... };
```

**Parameter — in:**
```cpp
void ProcessData(std::span<const int> data);
```
```csharp
[LibraryImport("libnative")]
internal static partial void __ProcessData(nint self, [MarshalUsing(typeof(SpanMarshaller<int>))] ReadOnlySpan<int> data);
```

**Parameter — out (writable span):**
```cpp
void FillBuffer([[sharpcpp::out]] std::span<int> buffer);
```
```csharp
[LibraryImport("libnative")]
internal static partial void __FillBuffer(nint self, [MarshalUsing(typeof(SpanMarshaller<int>))] Span<int> buffer);
```

### ArrayView surface

An alternative surface for `std::span` — managed wrapper with index access instead of `ReadOnlySpan<T>`:

```cpp
template <typename T>
struct [[sharpcpp::managed_type(
    public_type = "ArrayView<{0}>",
    abi_type = "ContiguousBufferAbi",
    marshaller_type = "ArrayViewMarshaller<{0}>"
)]]
Marshaller<std::span<T>> { ... };
```

The choice between `ReadOnlySpan<T>` and `ArrayView<T>` is in the marshaller attribute. Both use the same `ContiguousBufferAbi` at the ABI level.

**Return:**
```cpp
std::span<const int> GetData() const;
```
```csharp
public ReadOnlySpan<int> Data => SpanMarshaller<int>.ConvertToManaged(__GetData(_handle));
```
The span is valid only until the next native call that might invalidate the underlying memory.

## std::array → T[] or ArrayView

Fixed-size, stack-allocated array.

```cpp
template <typename T, size_t N>
struct [[sharpcpp::managed_type(
    public_type = "{0}[]",
    abi_type = "ContiguousBufferAbi",
    marshaller_type = "StdArrayMarshaller<{0}, {1}>"
)]]
Marshaller<std::array<T, N>> { ... };
```

**Return:**
```cpp
std::array<float, 4> GetMatrix() const;
```
```csharp
[LibraryImport("libnative")]
internal static partial ContiguousBufferAbi __GetMatrix(nint self);
public float[] Matrix => StdArrayMarshaller<float, 4>.ConvertToManaged(__GetMatrix(_handle));
```

For blittable element types, `T[]` is the simplest surface. For non-blittable types, `ArrayView<T>` with per-element accessors.

**Parameter — in:**
```cpp
void SetTransform([[sharpcpp::in]] const std::array<float, 16>& matrix);
```
```csharp
[LibraryImport("libnative")]
internal static partial void __SetTransform(nint self, [MarshalUsing(typeof(StdArrayMarshaller<float, 16>)), Count(16)] ReadOnlySpan<float> matrix);
```

### Fixed-size via count attribute

When a raw pointer carries a known count:

```cpp
void GetVertices([[sharpcpp::count(16)]] float* vertices);
```
```csharp
[LibraryImport("libnative")]
internal static partial void __GetVertices(nint self, [MarshalUsing(typeof(SpanMarshaller<float>)), Count(16)] Span<float> vertices);
```

### Count from another parameter

```cpp
void GetItems(int* items, [[sharpcpp::count("items")]] int count);
```

The `[[sharpcpp::count(...)]]` attribute can reference another parameter by name. The C# side reads `Count` from the `count` parameter.

## std::list → Std.List

Same pattern as `std::vector` — owned container with sequential access.

```cpp
template <typename T, typename TAllocator>
struct [[sharpcpp::managed_type(
    public_type = "Std.List<{0}>",
    managed_base_type = "global::SharpCpp.Support.ListBase<{0}>",
    abi_type = "void*"
)]]
Marshaller<std::list<T, TAllocator>> { ... };
```

No random access. Element access uses `std::next` internally, O(n) per access. All pass-direction variants are identical to vector.

## Source kind dispatch eliminated

Each container type has its own `Marshaller<T>` specialization. The renderer reads `public_type`, `abi_type`, and `marshaller_type` from the attribute. No `switch` on source kind. No `BindingContiguousBufferSourceKind` enum in core.

---

← [[02-strings]] | [[04-associative-containers]] →