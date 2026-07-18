# C++ Templates as Generic Classes

C++ class templates become C# generic classes. Each concrete instantiation of a C++ template produces a separate C# generic type instantiation. The generator discovers instantiations from configuration and generates each one.

## Discovery of instantiations

The generator does **not** automatically instantiate templates. Instantiations come from:

1. **Explicit generic instantiation requests** in the binding project configuration
2. **Implicit instantiations** discovered from function parameters, return types, and field types that reference template specializations

```csharp
// BindingProjectConfig or attribute-driven:
GenericInstantiations = new[]
{
    new GenericInstantiation(typeof(Vector<>), new[] { typeof(int) }),
    new GenericInstantiation(typeof(Vector<>), new[] { typeof(MyClass) }),
    new GenericInstantiation(typeof(Map<,>), new[] { typeof(string), typeof(int) }),
}
```

## Template → generic mapping

### Simple template

```cpp
template <typename T>
class Container
{
    T GetAt(int index) const;
    void SetAt(int index, const T& value);
};
```

```csharp
public class Container<T>
{
    public T GetAt(int index) => Marshaller.ConvertToManaged(__GetAt(_handle, index));
    public void SetAt(int index, T value) => __SetAt(_handle, index, in Marshaller.ConvertToAbi(value));
}
```

Concrete instantiations:

```csharp
// Container<int> — blittable element, no marshaller needed for T
public class ContainerInt : Container<int> { /* specialized P/Invoke signatures */ }

// Container<MyClass> — handle element, goes through Marshaller<MyClass*>
public class ContainerMyClass : Container<MyClass> { /* handle-aware P/Invoke signatures */ }
```

The P/Invoke signatures use the **ABI type** for each instantiation. `Container<int>` passes `int` directly. `Container<MyClass>` passes `nint` for the element.

### Template with multiple parameters

```cpp
template <typename TKey, typename TValue>
class Dictionary { ... };
```

```csharp
public class Dictionary<TKey, TValue> { ... }

// Concrete: Dictionary<string, int>
// TKey → Utf8StringAbi, TValue → int (blittable)
```

The `{0}`, `{1}` placeholders in `public_type` and `abi_type` substitute the managed type names of the generic arguments.

### Template with non-type parameters

```cpp
template <typename T, size_t N>
class FixedArray { ... };
```

```csharp
public class FixedArray<T, int N> { ... }

// Concrete: FixedArray<float, 16>
// N is a compile-time constant — emitted as a literal in the P/Invoke signature
```

Non-type template parameters become C# generic parameters with compile-time values. The generator dispatches on the concrete values.

## Generic instantiation and the marshaller

Each concrete instantiation resolves the marshaller's generic arguments:

```cpp
// std::vector<int> → Marshaller<std::vector<int>>
// {0} in public_type = "Std.Vector<int>" resolves "int"
// abi_type = "void*"
// marshaller_type = "StdVectorMarshaller<int>"
```

The generator emits a concrete marshaller type for `StdVectorMarshaller<int>` that knows how to convert `int` elements. For `StdVectorMarshaller<MyClass>`, the element accessors use `ReturnMarshaller<MyClass*>` and `ParameterMarshaller<MyClass*>`.

## Static members on template specializations

```cpp
template <typename T>
class Container
{
    static Container<T> Create(int size);
};
```

```csharp
public static Container<T> Create<T>(int size) where T : new()
{
    // Each T gets its own P/Invoke entry point
    return new Container<T>(__Create(size), ownsHandle: true);
}
```

Static members on template classes generate per-instantiation P/Invoke entries, since the native symbol is `Container<int>::Create`, not `Container<T>::Create`.

## Template member functions

When only some member functions of a template class are instantiated:

```cpp
template <typename T>
class Maybe
{
    bool HasValue() const;    // always bound
    T GetValue() const;       // only bound for instantiations where T is default-constructible
};
```

The generator only emits P/Invoke entries for member functions that exist in the concrete instantiation. Functions that would be ill-formed for a given T are omitted.

## Template constraints

C++20 concepts constrain template parameters. The generator maps these to C# generic constraints where possible:

```cpp
template <std::derived_from<Node> T>
class NodeList { ... };
```

```csharp
public class NodeList<T> where T : Node { ... }
```

Not all C++ concepts map to C# constraints. When no C# constraint exists, the constraint is documented in a comment.

## Concrete generic type naming

Each concrete instantiation gets a distinct managed name. The naming convention:

| C++ | C# |
|---|---|
| `Container<int>` | `ContainerInt` or `Container<int>` |
| `Container<MyClass>` | `ContainerMyClass` or `Container<MyClass>` |
| `Map<string, int>` | `MapStringInt` or `Map<string, int>` |

The `managed_type` attribute controls naming. Without explicit `public_type`, the generator uses the `GenericInstantiation` configuration to determine names.

---

← [[14-operators]] | [[16-default-parameters]] →