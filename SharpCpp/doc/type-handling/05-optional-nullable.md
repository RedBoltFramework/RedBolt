# Optional & Nullable

`std::optional<T>` and nullable handles use the same marshaller path. Nullable value types use `OptionalValueAbi<T>`; nullable handles are `nint` with null check.

## std::optional → nullable value type

```cpp
template <typename T>
struct [[sharpcpp::managed_type(
    public_type = "{0}?",
    abi_type = "OptionalValueAbi<{0}>",
    marshaller_type = "OptionalValueMarshaller<{0}>",
    suppress_emission = true
)]]
Marshaller<std::optional<T>> { ... };
```

`{0}` substitutes the inner type's managed name. When T = `int`, the generator emits `int?`. `suppress_emission = true` because `int?` is a .NET builtin — no wrapper class to emit.

### Pass direction variants

**Return:**
```cpp
std::optional<int> FindId(std::string_view name);
```
```csharp
[LibraryImport("libnative")]
internal static partial OptionalValueAbi<int> __FindId(Utf8StringAbi name);
public int? FindId(string name) => OptionalValueMarshaller<int>.ConvertToManaged(__FindId(Utf8StringMarshaller.ConvertToAbi(name)));
```

**Parameter — in:**
```cpp
void SetId([[sharpcpp::in]] const std::optional<int>& id);
```
```csharp
[LibraryImport("libnative")]
internal static partial void __SetId(in OptionalValueAbi<int> id);
public void SetId(int? id) => __SetId(in OptionalValueMarshaller<int>.ConvertToAbi(id));
```

**Parameter — out:**
```cpp
void GetId([[sharpcpp::out]] std::optional<int>* outId);
```
```csharp
[LibraryImport("libnative")]
internal static partial void __GetId(out OptionalValueAbi<int> outId);
```

**Parameter — ref:**
```cpp
void UpdateId([[sharpcpp::ref]] std::optional<int>& id);
```
```csharp
[LibraryImport("libnative")]
internal static partial void __UpdateId(ref OptionalValueAbi<int> id);
```

The `OptionalValueAbi<T>` struct has `HasValue` and `Value` fields. The `OptionalValueMarshaller<T>` converts between `T?` and `OptionalValueAbi<T>` on both sides.

### Optional with non-blittable inner type

```cpp
std::optional<std::string> GetName();
```

The inner type goes through its own marshaller. The `OptionalValueMarshaller<std::string>` reads the inner type's `abi_type` (Utf8StringAbi), producing `OptionalValueAbi<Utf8StringAbi>`.

## Nullable handle

A pointer type that can be null. Same ABI as a handle (`nint`), but the managed side checks for zero before constructing:

```cpp
MyClass* GetParent() const;  // may return nullptr
```

```csharp
public MyClass? Parent
{
    get
    {
        var handle = __GetParent(_handle);
        return handle == 0 ? null : new MyClass(handle, ownsHandle: false);
    }
}
```

No special marshaller — `nint` is blittable, null check is in the wrapper logic. The `?` on the managed return type comes from the parser detecting that the C++ return type is a pointer (not a reference).

### Nullable shared_ptr

```cpp
std::shared_ptr<MyClass> FindItem(std::string_view name);
```

```csharp
public MyClass? FindItem(string name)
{
    var abi = __FindItem(Utf8SpanMarshaller.ConvertToAbi(name));
    return abi.Pointer == 0 ? null : new MyClass(abi.Pointer, ownsHandle: false, owner: abi.Owner);
}
```

The `SharedPtrTwoParam` ABI struct's `Pointer == 0` check replaces the single `nint == 0` check.

## Nullable optional handle

```cpp
std::optional<MyClass*> FindItem(int id);
```

Two layers of nullability: `optional` (has value?) and pointer (not null?). The `OptionalValueAbi<nint>` handles both:

```csharp
public MyClass? FindItem(int id)
{
    var abi = __FindItem(id);
    if (!abi.HasValue) return null;
    return abi.Value == 0 ? null : new MyClass(abi.Value, ownsHandle: false);
}
```

---

← [[04-associative-containers]] | [[06-pair-tuple]] →