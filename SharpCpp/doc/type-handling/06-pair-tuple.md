# Pair & Tuple

Composite value types that aggregate multiple fields. Both use `PairAbi` / tuple-ABI structs at the boundary.

## std::pair

```cpp
template <typename TFirst, typename TSecond>
    requires detail::pair_tuple_eligible<TFirst> && detail::pair_tuple_eligible<TSecond>
struct [[sharpcpp::managed_type(
    public_type = "(TFirst, TSecond)",
    abi_type = "PairAbi<TFirst, TSecond>",
    marshaller_type = "PairTupleMarshaller<std::pair<TFirst, TSecond>>",
    suppress_emission = true
)]]
Marshaller<std::pair<TFirst, TSecond>> { ... };
```

### Blittable pair (both elements blittable)

```cpp
std::pair<int, float> GetPosition() const;
```

```csharp
[LibraryImport("libnative")]
internal static partial PairAbi<int, float> __GetPosition(nint self);
public (int, float) Position => PairTupleMarshaller.ConvertToManaged(__GetPosition(_handle));
```

The `PairAbi<TFirst, TSecond>` struct is blittable when both TFirst and TSecond are blittable. `LibraryImport` passes it by value.

### Non-blittable pair

When elements are non-blittable (handles, strings, other value types), `PairAbi` contains the ABI types of the elements:

```cpp
std::pair<std::string, int> GetEntry() const;
```

```csharp
// PairAbi<Utf8StringAbi, int> — element type goes through its own marshaller
[LibraryImport("libnative")]
internal static partial PairAbi<Utf8StringAbi, int> __GetEntry(nint self);
public (StdString, int) GetEntry()
{
    var abi = __GetEntry(_handle);
    return (Utf8StringMarshaller.ConvertToManaged(abi.First), abi.Second);
}
```

### Pass direction variants

**Value parameter:**
```cpp
void SetPosition(std::pair<int, float> pos);
```
```csharp
[LibraryImport("libnative")]
internal static partial void __SetPosition(PairAbi<int, float> pos);
public void SetPosition(int x, float y) => __SetPosition(new PairAbi<int, float>(x, y));
```

**Const reference parameter:**
```cpp
void ProcessEntry([[sharpcpp::in]] const std::pair<std::string, int>& entry);
```
```csharp
[LibraryImport("libnative")]
internal static partial void __ProcessEntry(in PairAbi<Utf8StringAbi, int> entry);
```

The `ValueParameterMarshaller` or `ConstReferenceParameterMarshaller` is selected by `MarshallerSelector` based on context.

**Out parameter:**
```cpp
void GetRange([[sharpcpp::out]] std::pair<int, int>* range);
```
```csharp
[LibraryImport("libnative")]
internal static partial void __GetRange(out PairAbi<int, int> range);
```

**Ref parameter:**
```cpp
void UpdateRange([[sharpcpp::ref]] std::pair<int, int>& range);
```
```csharp
[LibraryImport("libnative")]
internal static partial void __UpdateRange(ref PairAbi<int, int> range);
```

The `ReferenceParameterMarshaller` handles bidirectional sync — converts to ABI before call, syncs back after call.

## Pair with non-eligible elements

When elements are non-eligible (complex types like records, containers), the pair uses `PairPointerMarshaller`:

```cpp
// Non-eligible pair — uses pointer-based ABI
template <typename TFirst, typename TSecond>
struct Marshaller<std::pair<TFirst, TSecond>>
    : PairPointerMarshaller<std::pair<TFirst, TSecond>>
{
};
```

The pointer-based ABI allocates a heap copy for the pair, passes the pointer, and transfers ownership. The managed side wraps the pointer.

## Custom value types (user-defined structs)

Users declare `Marshaller<T>` specializations for their own value types with the same attribute schema:

```cpp
struct [[sharpcpp::managed_type(
    public_type = "Color",
    abi_type = "ColorAbi",
    marshaller_type = "ColorMarshaller"
)]]
Marshaller<MyColor> { ... };
```

The generator emits the `ColorAbi` struct in C# and the `ColorMarshaller` conversion logic. Each field of `MyColor` goes through its own marshaller.

---

← [[05-optional-nullable]] | [[07-callbacks]] →