# Constructors & Destructors

## Default constructor

```cpp
MyClass() = default;
```

```csharp
[LibraryImport("libnative")]
internal static partial nint __CreateMyClass(int x, float y, [MarshalUsing(typeof(Utf8SpanMarshaller))] ReadOnlySpan<byte> name);

public MyClass(int x, float y, ReadOnlySpan<byte> name) : this(__CreateMyClass(x, y, name), ownsHandle: true) { }
```

The P/Invoke creates the native object. The managed wrapper takes ownership.

## Parameterized constructor

```cpp
MyClass(int value, std::string_view name);
```

```csharp
[LibraryImport("libnative")]
internal static partial nint __CreateMyClass(int value, [MarshalUsing(typeof(Utf8SpanMarshaller))] ReadOnlySpan<byte> name);

public MyClass(int value, ReadOnlySpan<byte> name) : this(__CreateMyClass(value, name), ownsHandle: true) { }
```

`MarshalUsing` drives the span parameter conversion. The return handle is blittable — no marshaller needed.

## Static factory methods

Some types don't have public constructors. Instead, they expose static creation methods:

```cpp
class Texture
{
    [[sharpcpp::expose_member(shape = "method")]]
    static Texture* Create(std::string_view path);
};
```

```csharp
[LibraryImport("libnative")]
internal static partial nint __Create([MarshalUsing(typeof(Utf8SpanMarshaller))] ReadOnlySpan<byte> path);

public static Texture Create(ReadOnlySpan<byte> path) => new Texture(__Create(path), ownsHandle: true);
```

The `expose_member` with `shape = "method"` creates a regular `FunctionBinding` on the record. The generator emits it as a static method.

## Copy and move constructors

The parser detects copy/move constructors. They become regular constructor bindings:

```cpp
MyClass(const MyClass& other);  // copy
MyClass(MyClass&& other);        // move
```

```csharp
// Copy constructor
public MyClass(MyClass other) : this(__CopyConstruct(other.Handle), ownsHandle: true) { }

// Move constructor (rare in managed API — usually just copies the handle)
public MyClass(MyClass other) : this(other.Handle, ownsHandle: false) { other.Handle = 0; }
```

Move semantics are complex across the boundary. The typical binding treats move as handle transfer.

## Destructor / Dispose

C++ destructors become `Dispose` on the managed side:

```cpp
~MyClass();
```

```csharp
protected override void Dispose(bool disposing)
{
    if (disposing)
    {
        __Destroy(_handle);
    }
    base.Dispose(disposing);
}
```

The `ownsHandle` flag from the constructor determines whether `Dispose` calls the native destructor. Borrowed handles (`ownsHandle: false`) do not destroy.

## RAII types

Value types that manage their own lifetime (e.g., `std::string`, `std::vector`) use the marshaller's `ConvertToC` / `ConvertToCpp` for construction:

```cpp
// Constructed implicitly via marshaller
std::vector<int> items;
items.push_back(1);
items.push_back(2);
return items;
```

The C++ side constructs via normal C++ mechanisms. The C# side constructs via the container's managed wrapper and `expose_member` accessors.

## Constructor with default parameters

```cpp
MyClass(int x, float y = 1.0f, std::string name = "default");
```

```csharp
public MyClass(int x) : this(x, 1.0f, "default") { }
public MyClass(int x, float y) : this(x, y, "default") { }
public MyClass(int x, float y, string name) : this(__CreateMyClass(x, y, Utf8SpanMarshaller.ConvertToAbi(name)), ownsHandle: true) { }
```

Default values from C++ default arguments become C# method overloads with default parameter expressions. The parser records `DefaultValue` on `FunctionParameterBinding`.

## Private / deleted constructors

Constructors that are `= delete` or `private` / `protected` are not bound. The parser filters them out based on accessibility.

## Explicit constructor

```cpp
explicit MyClass(int value);
```

`explicit` is a C++ keyword that prevents implicit conversion. In the binding, all constructors are explicit by default (managed constructors are always explicit). No special handling needed.

---

← [[11-parameters-returns]] | [[13-fields-properties]] →