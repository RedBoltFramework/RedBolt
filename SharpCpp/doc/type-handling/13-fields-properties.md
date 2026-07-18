# Fields & Properties

## Public field access

### Blittable field

```cpp
struct Vector3
{
    float X;
    float Y;
    float Z;
};
```

```csharp
public float X { get => __GetX(_handle); set => __SetX(_handle, value); }
// Or direct access if the struct is value-type and blittable:
public float X;
```

Blittable value-type fields in structs may be accessed directly. Record (class) fields always go through P/Invoke get/set functions.

### Non-blittable field (handle)

```cpp
class Node
{
    Node* Parent;
};
```

```csharp
public Node? Parent
{
    get { var h = __GetParent(_handle); return h == 0 ? null : new Node(h, ownsHandle: false); }
    set => __SetParent(_handle, value?.Handle ?? 0);
}
```

### Non-blittable field (string)

String fields use dedicated P/Invoke getter/setter functions. `MarshalUsing` applies to the P/Invoke:

```cpp
std::string name;
```

```csharp
[LibraryImport("libnative")]
[return: MarshalUsing(typeof(Utf8StringMarshaller))]
internal static partial StdString __GetName(nint self);

[LibraryImport("libnative")]
internal static partial void __SetName(nint self, [MarshalUsing(typeof(Utf8StringMarshaller))] StdString value);

public StdString Name
{
    get => __GetName(_handle);
    set => __SetName(_handle, value);
}
```

### Non-blittable field (value type)

```cpp
struct Color
{
    float R, G, B, A;
};
```

```csharp
[LibraryImport("libnative")]
[return: MarshalUsing(typeof(ColorMarshaller))]
internal static partial Color __GetColor(nint self);

[LibraryImport("libnative")]
internal static partial void __SetColor(nint self, [MarshalUsing(typeof(ColorMarshaller))] in Color value);

public Color Color
{
    get => __GetColor(_handle);
    set => __SetColor(_handle, in value);
}
```

Value-type fields use `MarshalUsing` on their P/Invoke getter/setter.

## Property generation

A `GenerateFieldPropertiesPass` creates getter and setter `FunctionBinding`s for each public field. The pass runs on the AST before rendering.

### Read-only property (const field)

```cpp
const int MaxSize = 100;
```

```csharp
public int MaxSize => __GetMaxSize(_handle);
```

No setter. The P/Invoke reads the value from native memory.

### Getter-only property (no setter)

```cpp
int Count() const;
```

This is a method, not a field. The generator creates a read-only property:

```csharp
public int Count => __GetCount(_handle);
```

### Getter + setter

```cpp
int GetWidth() const;
void SetWidth(int width);
```

The generator pairs these into a property:

```csharp
public int Width
{
    get => __GetWidth(_handle);
    set => __SetWidth(_handle, value);
}
```

## Static fields and properties

```cpp
class Config
{
    static int MaxConnections;
    static int GetMaxConnections();
    static void SetMaxConnections(int value);
};
```

```csharp
public static int MaxConnections
{
    get => __GetMaxConnections();
    set => __SetMaxConnections(value);
}
```

Static members don't need a `self` handle parameter. The P/Invoke signature omits the instance parameter.

## Protected fields

Fields marked `protected` in C++ bind as `protected` properties in C#, enabling derived class access:

```cpp
class Widget
{
protected:
    int _internalState;
};
```

```csharp
protected int InternalState
{
    get => __GetInternalState(_handle);
    set => __SetInternalState(_handle, value);
}
```

## Array fields with count

### Fixed-size array field

```cpp
struct Matrix4x4
{
    float Elements[16];
};
```

```csharp
public Span<float> Elements => GetElementsSpan();
```

The `[[sharpcpp::count(16)]]` attribute provides the count. See [[03-sequential-containers]].

### Paired pointer + count field

```cpp
struct Buffer
{
    int* Data;
    int Count;
};
```

If `Data` has `[[sharpcpp::count("Count")]]` on its type metadata, the getter returns a span:

```csharp
public Span<int> Data => GetSpan(__GetData(_handle), __GetCount(_handle));
```

## Access shape via expose_member

Fields on marshaller specializations can be injected into wrapper types:

```cpp
struct Marshaller<std::vector<T>>
{
    [[sharpcpp::expose_member(shape = "property_getter")]]
    static unsigned long long GetCount(const std::vector<T>& self) { return self.size(); }
};
```

The `shape` attribute controls how the member appears in C#:

| shape | C# member |
|---|---|
| `"method"` | Public method |
| `"property_getter"` | Property with getter |
| `"property_setter"` | Property with setter |
| `"protected_override"` | Protected override (support-base pattern) |

---

← [[12-constructors-destructors]] | [[14-operators]] →