# Type Projection

Type projection makes a wrapper type transparent. When `SharedPtr<T>` returns T rather than `SharedPtr<T>`, projection is active.

## SharedPtr projection

The most common projection: `std::shared_ptr<T>` projects to `T` (not `SharedPtr<T>`):

```cpp
template <typename T>
struct [[sharpcpp::managed_type(
    public_type = "T",
    abi_type = "SharedPtrTwoParam<T>",
    marshaller_type = "SharedPtrMarshaller<T>",
    projection = true
)]]
Marshaller<std::shared_ptr<T>> { ... };
```

`projection = true` tells the generator: when you encounter `std::shared_ptr<MyClass>`, the managed public type is `MyClass`, not `SharedPtr<MyClass>`. The ABI is two-param (pointer + owner).

### Pass direction variants

**Return (polymorphic):**
```cpp
std::shared_ptr<Widget> CreateWidget(std::string_view type);
```
```csharp
// The return type is Widget, not SharedPtr<Widget>
public Widget CreateWidget(string type)
{
    var abi = __CreateWidget(Utf8SpanMarshaller.ConvertToAbi(type));
    // Polymorphic wrapping handles runtime type
    return Widget.Wrap(abi.Pointer, owner: abi.Owner);
}
```

**Return (non-polymorphic):**
```cpp
std::shared_ptr<MyClass> Create();
```
```csharp
public MyClass Create()
{
    var abi = __Create();
    return new MyClass(abi.Pointer, ownsHandle: false, owner: abi.Owner);
}
```

**Parameter:**
```cpp
void Process(const std::shared_ptr<MyClass>& obj);
```
```csharp
[LibraryImport("libnative")]
internal static partial void __Process(SharedPtrTwoParam<MyClass> obj);
public static void Process(MyClass obj) => __Process(SharedPtrTwoParam<MyClass>.FromManaged(obj));
```

## WeakPtr projection

`std::weak_ptr<T>` is not projected — it has its own surface type `WeakReference<T>`:

```cpp
template <typename T>
struct [[sharpcpp::managed_type(
    public_type = "WeakReference<T>",
    abi_type = "WeakPtrTwoParam",
    marshaller_type = "WeakPtrMarshaller<T>"
)]]
Marshaller<std::weak_ptr<T>> { ... };
```

No `projection = true`. The managed type is `WeakReference<T>`.

## Custom projection

Any generic type can project its type argument:

```cpp
// UniquePtr<T> projects to T
template <typename T>
struct [[sharpcpp::managed_type(
    public_type = "T",
    abi_type = "nint",
    projection = true
)]]
Marshaller<std::unique_ptr<T>> { ... };
```

When `projection = true`:
- The managed type name is the projected type name (substituted `{0}`)
- Construction goes through the projected type's constructor
- Handle management uses the projected type's ownership semantics

## Non-projected smart pointers

If a smart pointer should have its own managed wrapper (not project):

```cpp
// Custom smart pointer with its own managed type
template <typename T>
struct [[sharpcpp::managed_type(
    public_type = "IntrusivePtr<{0}>",
    managed_base_type = "global::SharpCpp.Support.IntrusivePtrBase<{0}>",
    abi_type = "void*"
)]]
Marshaller<IntrusivePtr<T>> { ... };
```

No projection. The wrapper type `IntrusivePtr<T>` has its own reference counting.

---

← [[09-instance-caching]] | [[11-parameters-returns]] →