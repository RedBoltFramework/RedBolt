# Parameters & Returns

General rules for how values cross the C++/C# boundary. Type-specific rules are in each type's file. This file covers the universal mechanics.

## Pass kind (in/out/ref)

The `[[sharpcpp::in]]`, `[[sharpcpp::out]]`, `[[sharpcpp::ref]]` attributes explicitly override the inferred pass kind. When no attribute is present, the generator infers the pass kind from the C++ declaration:

### Inference rules

| C++ declaration | Inferred PassKind | Rationale |
|---|---|---|
| `T value` | Value | Copy in, no copy out |
| `const T& value` | In | Read-only reference: `const` + `&` → `in` |
| `T& value` | Ref | Mutable reference: `&` without `const` → `ref` |
| `T* ptr` | Out | Mutable pointer: `*` without `const` → `out` |
| `const T* ptr` | In | Read-only pointer: `const` + `*` → `in` |
| `T** ptr` | Out (to pointer) | Pointer to mutable pointer |

The two key inference signals:
1. **`const` qualification** → `In` (the native side won't modify the value)
2. **Indirection kind** (`&` vs `*`) → `Ref` vs `Out` for mutable references

### Value (default)

```cpp
void Process(int value);
```

Copy in, no copy out. ABI: `int`. C#: `int value`.

### In (const reference)

```cpp
void Process(const Value& value);
```

Inferred as `In` because `const` + `&`. No attribute needed:

```cpp
// Equivalent explicit attribute:
void Process([[sharpcpp::in]] const Value& value);
```

ABI: pointer to ABI struct. C#: `in ValueAbi value` or `Value value` depending on size.

For blittable types, `in` is the same as value. For value types, `in` passes by readonly reference:

```csharp
[LibraryImport("libnative")]
internal static partial void __Process(in ValueAbi value);
```

### Out (mutable pointer)

```cpp
void GetResult(Value* result);
```

Inferred as `Out` because `*` without `const`. No attribute needed:

```cpp
// Equivalent explicit attribute:
void GetResult([[sharpcpp::out]] Value* result);
```

```csharp
[LibraryImport("libnative")]
internal static partial void __GetResult(out ValueAbi result);
```

The native side writes to the pointer. C# initializes the out parameter before return.

When the pointer is const-qualified (`const Value*`), inference yields `In` instead — the native side won't modify the pointee.

### Ref (mutable reference)

```cpp
void Update(Value& value);
```

Inferred as `Ref` because `&` without `const`. No attribute needed:

```cpp
// Equivalent explicit attribute:
void Update([[sharpcpp::ref]] Value& value);
```

```csharp
[LibraryImport("libnative")]
internal static partial void __Update(ref ValueAbi value);
```

Bidirectional: C# passes the current value in, native modifies it, C# reads the modified value back.

### When to use explicit attributes

Explicit `[[sharpcpp::in]]`, `[[sharpcpp::out]]`, or `[[sharpcpp::ref]]` attributes are needed when:

1. **Output-only pointer that the caller allocates**: `void GetBuffer([[sharpcpp::out]] uint8_t* buffer, int size)` — the pointer isn't `const`, but `out` semantics are more accurate than the default `out` inference
2. **Mutable reference that should be treated as input**: `void Consume([[sharpcpp::in]] Value& value)` — override `ref` inference to `in` because the native side only reads
3. **Mutable pointer to input**: `void Process([[sharpcpp::in]] Value* ptr)` — override `out` inference to `in` because the native side only reads through the pointer

## Indirection kind

| C++ declaration | IndirectionKind | ABI shape |
|---|---|---|
| `int value` | Value | `int` |
| `int* ptr` | Pointer | `nint` |
| `int& ref` | LValueReference | `int*` (pointer) |
| `const int& ref` | LValueReference | `int*` (pointer) |

The indirection interacts with pass kind:

| Indirection | PassKind | C# modifier | ABI shape |
|---|---|---|---|
| Value | Value | (none) | blittable |
| Value | In | `in` | `in ValueAbi` |
| Value | Out | `out` | `out ValueAbi` |
| Value | Ref | `ref` | `ref ValueAbi` |
| Pointer | Value | (none) | `nint` |
| Pointer | Out | (handle logic) | `out nint` or `nint` |
| LValueReference | In | `in` | pointer to ABI |
| LValueReference | Out | `out` | pointer to ABI |
| LValueReference | Ref | `ref` | pointer to ABI |

## Count attribute

`[[sharpcpp::count(...)]]` provides the element count for buffer/array parameters.

### Constant count

```cpp
void SetMatrix([[sharpcpp::count(16)]] float* matrix);
```

```csharp
[LibraryImport("libnative")]
internal static partial void __SetMatrix([MarshalUsing(typeof(SpanMarshaller<float>)), Count(16)] Span<float> matrix);
```

### Parameter-referenced count

```cpp
void GetData(int* buffer, [[sharpcpp::count("buffer")]] int size);
```

The `count` value references the adjacent parameter by name. The C# side reads `size` to determine buffer length:

```csharp
[LibraryImport("libnative")]
internal static partial void __GetData([MarshalUsing(typeof(SpanMarshaller<int>))] Span<int> buffer, int size);
```

### C++ expression count

```cpp
void GetItems(int* items, [[sharpcpp::count("self.size()")]] int count);
```

The count is a C++ expression evaluated at call time. The marshaller reads the count from the object state.

## String binding override

`[[sharpcpp::string_binding(...)]]` overrides the string surface for a specific parameter:

```cpp
void Log([[sharpcpp::string_binding("string")]] std::string_view message);
```

Values: `"span"`, `"string"`, `"wrapper"`, `"auto"`. See [[02-strings]] for full details.

## Callback user data

`[[sharpcpp::callback_user_data(...)]]` marks a parameter as the user-data companion to a callback:

```cpp
void RegisterCallback(
    void (*callback)(void*, int),
    [[sharpcpp::callback_user_data("callback")]] void* userData);
```

The named parameter (`"callback"`) identifies the callback parameter. The generator omits the user-data parameter from the managed signature. See [[07-callbacks]].

## Return value marshalling

### Value return

```cpp
int GetCount() const;
```
```csharp
[LibraryImport("libnative")]
internal static partial int __GetCount(nint self);
public int Count => __GetCount(_handle);
```

Blittable. No marshaller needed.

### Non-blittable return

```cpp
std::string GetName() const;
```
```csharp
[LibraryImport("libnative")]
[return: MarshalUsing(typeof(Utf8StringMarshaller))]
internal static partial StdString __GetName(nint self);
public StdString Name => __GetName(_handle);
```

`MarshalUsing` drives the return conversion.

### Pointer return (owned)

```cpp
MyClass* Create(int value);
```
```csharp
internal static partial nint __Create(int value);
public MyClass Create(int value) => new MyClass(__Create(value), ownsHandle: true);
```

The `ownsHandle` flag comes from the parser determining ownership semantics.

### Pointer return (borrowed)

```cpp
MyClass* GetParent() const;
```
```csharp
internal static partial nint __GetParent(nint self);
public MyClass? Parent
{
    get { var h = __GetParent(_handle); return h == 0 ? null : new MyClass(h, ownsHandle: false); }
}
```

### Void return

```cpp
void DoSomething();
```
```csharp
[LibraryImport("libnative")]
internal static partial void __DoSomething(nint self);
public void DoSomething() => __DoSomething(_handle);
```

### Reference return

```cpp
int& GetValue();
```
Reference-returning functions are rare in the binding surface. When supported, the ABI returns a pointer and the managed side provides a `ref` accessor through a separate property.

---

← [[10-projection]] | [[12-constructors-destructors]] →