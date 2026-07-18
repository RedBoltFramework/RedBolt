# Handles (Record Pointers)

Handle types represent C++ object pointers. The managed type is a wrapper class holding a `nint`. The ABI is blittable (`nint`).

## Owned handle (constructor return)

The most common pattern: a C++ function returns a pointer to a newly-constructed object. Ownership transfers to the managed wrapper.

```cpp
MyClass* CreateMyClass(int value);
```

```csharp
// P/Invoke
[LibraryImport("libnative")]
internal static partial nint __CreateMyClass(int value);

// Wrapper
public MyClass(int value) : this(__CreateMyClass(value), ownsHandle: true) { }
```

The `Marshaller<MyClass*>` specialization converts to/from raw pointer. No `MarshalUsing` — `nint` is blittable.

### Managed type declaration

```cpp
template <>
struct [[sharpcpp::managed_type(
    public_type = "MyClass"
)]]
Marshaller<MyClass*> {};
```

Default: `public_type` = class name. No `marshaller_type` needed — `nint` is the ABI, blittable.

### Static factory

Some classes have named constructors rather than public constructors:

```cpp
class MyClass
{
    [[sharpcpp::expose_member(shape = "method")]]
    static MyClass* Create(int value);
};
```

```csharp
public static MyClass Create(int value) => new MyClass(__Create(value), ownsHandle: true);
```

## Borrowed handle (parameter, return reference)

A function takes or returns a pointer without transferring ownership. The managed wrapper uses `ownsHandle: false`.

```cpp
MyClass* GetParent() const;
void Process([[sharpcpp::in]] const MyClass* obj);
```

```csharp
internal static partial nint __GetParent(nint self);

public MyClass? Parent => __GetParent(_handle) == 0 ? null : new MyClass(__GetParent(_handle), ownsHandle: false);

internal static partial void __Process(nint obj);
public static void Process(MyClass obj) => __Process(obj.Handle);
```

No lifetime management on borrowed handles. The wrapper does not free the native pointer.

### Null handle

When a function returns `nullptr`, the managed side receives `nint.Zero`. The wrapper maps this to `null`:

```csharp
public MyClass? FindItem(string name)
{
    var handle = __FindItem(name);
    return handle == 0 ? null : new MyClass(handle, ownsHandle: false);
}
```

## shared_ptr handle (two-param ABI)

`std::shared_ptr<T>` needs two ABI parameters: the pointer value and the owner (control block). The marshaller declares a two-param ABI struct.

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

The C# ABI struct:

```csharp
[StructLayout(LayoutKind.Sequential)]
internal struct SharedPtrTwoParam<T>
{
    public nint Pointer;
    public nint Owner;
}
```

P/Invoke emits two `nint` parameters. The `SharedPtrMarshaller<T>` converts between `std::shared_ptr<T>` and `SharedPtrTwoParam<T>`.

### Pass direction variants

**Return (owned):**
```cpp
std::shared_ptr<MyClass> CreateShared();
```
```csharp
internal static partial SharedPtrTwoParam<MyClass> __CreateShared();
public MyClass CreateShared()
{
    var abi = __CreateShared();
    return new MyClass(abi.Pointer, ownsHandle: false, owner: abi.Owner);
}
```

**Parameter (borrowed):**
```cpp
void Process([[sharpcpp::in]] const std::shared_ptr<MyClass>& obj);
```
```csharp
internal static partial void __Process(SharedPtrTwoParam<MyClass> obj);
public static void Process(MyClass obj)
{
    var abi = SharedPtrTwoParam<MyClass>.FromManaged(obj);
    __Process(abi);
}
```

**Out parameter:**
```cpp
void GetItem([[sharpcpp::out]] std::shared_ptr<MyClass>* outItem);
```
```csharp
internal static partial void __GetItem(out SharedPtrTwoParam<MyClass> outItem);
public static MyClass? GetItem()
{
    __GetItem(out var abi);
    return abi.Pointer == 0 ? null : new MyClass(abi.Pointer, ownsHandle: false, owner: abi.Owner);
}
```

## WeakPtr handle

`std::weak_ptr<T>` produces a two-param ABI on lock, similar to shared_ptr but with additional null-safety. The marshaller's `ConvertToCpp` calls `lock()` to get a `shared_ptr<T>`, then wraps that.

```cpp
template <typename T>
struct [[sharpcpp::managed_type(
    public_type = "WeakReference<T>",
    abi_type = "WeakPtrTwoParam",
    marshaller_type = "WeakPtrMarshaller<T>"
)]]
Marshaller<std::weak_ptr<T>> { ... };
```

## Handle indirection variants

### Pointer to pointer

```cpp
MyClass** GetStorage();
```

Rare. The marshaller treats `T**` as `void**` at ABI level. The managed surface uses `ref nint` or `out nint`. Not commonly bound — usually wrapped in a function that dereferences once.

### Reference to pointer

```cpp
void GetItem([[sharpcpp::out]] MyClass*& item);
```

The reference-to-pointer pattern. The `[[sharpcpp::out]]` attribute sets `PassKind = Out`. The ABI is `nint*`. Managed side: `out nint item`.

### Const pointer

```cpp
const MyClass* GetData() const;
```

Same ABI as `MyClass*`. The managed side cannot distinguish const from non-const — the wrapper always exposes the same public API. Const correctness is a C++ concern.

## Polymorphic handle

When a function returns a base-type pointer but the actual runtime type may be a derived type, `polymorphic = true` configures runtime-type-based wrapping.

```cpp
template <>
struct [[sharpcpp::managed_type(
    public_type = "Widget",
    abi_type = "nint",
    polymorphic = true
)]]
Marshaller<Widget*> {};
```

The generator emits type-registry infrastructure and a factory method that inspects the runtime type and constructs the appropriate derived wrapper. See [[08-inheritance]] for full details.

---

← [[00-primitives-enums]] | [[02-strings]] →
