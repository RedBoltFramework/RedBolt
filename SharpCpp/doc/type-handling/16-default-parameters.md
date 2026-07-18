# Default Parameters

C++ default argument values translate to C# default parameter values or nullable overloads, depending on the type.

## Rule: builtin literal → same literal in C#

When a C++ default argument is a literal of a builtin type (integer, float, bool, enum, nullptr), the generator emits the **same literal value** in the C# API.

### Integer literals

```cpp
void Resize(int width, int height = 0);
```

```csharp
public void Resize(int width, int height = 0) => __Resize(_handle, width, height);
```

### Float literals

```cpp
void SetScale(float x = 1.0f, float y = 1.0f, float z = 1.0f);
```

```csharp
public void SetScale(float x = 1.0f, float y = 1.0f, float z = 1.0f) => __SetScale(_handle, x, y, z);
```

### Bool literals

```cpp
void SetEnabled(bool visible = true);
```

```csharp
public void SetEnabled(bool visible = true) => __SetEnabled(_handle, visible);
```

### Enum literals

```cpp
void SetColor(Color color = Color::Red);
```

```csharp
public void SetColor(Color color = Color.Red) => __SetColor(_handle, (int)color);
```

The generator resolves the enum value name from the enum's definition.

### Null pointer literals

```cpp
void SetParent(Node* parent = nullptr);
```

```csharp
public void SetParent(Node? parent = null) => __SetParent(_handle, parent?.Handle ?? 0);
```

`nullptr` translates to `null` for handle types, `0` / `nint.Zero` for raw pointers.

## Rule: non-translatable → nullable with default null

When a C++ default argument cannot be cleanly represented as a C# literal (handle types, complex expressions, string literals, value-type constructors), the parameter becomes **nullable** with a **default value of `null`**. The generated C# code checks for null and substitutes the C++ default.

### Handle defaults

```cpp
void SetOwner(Object* owner = nullptr);
```

Already covered above: `owner = null` in C#. The P/Invoke passes `nint.Zero` when null.

### Non-null handle defaults (rare)

```cpp
void SetContext(Context* context = GetDefaultContext());
```

The default is a function call — not a C# literal. Two approaches:

**Approach A: nullable parameter, null = use C++ default**

```csharp
public void SetContext(Context? context = null)
{
    if (context is not null)
        __SetContext(_handle, context.Handle);
    else
        __SetContextDefault(_handle);
}
```

The generator emits a separate P/Invoke that calls the C++ function without the parameter (which uses the C++ default). The C# API hides this with a `null` default.

**Approach B: nullable parameter with explicit null handling**

```csharp
public void SetContext(Context? context = null)
    => __SetContext(_handle, context?.Handle ?? DefaultContextHandle);
```

When a static default handle is known. Rare.

### String defaults

```cpp
void SetName(const char* name = "default");
```

C# does not allow string defaults where the string needs marshalling. The parameter becomes nullable:

```csharp
public void SetName(string? name = null)
{
    if (name is not null)
        __SetName(_handle, Utf8StringMarshaller.ConvertToAbi(name));
    else
        __SetNameDefault(_handle);
}
```

Or using a null check with a default value inside the wrapper:

```csharp
public void SetName(string? name = null)
    => __SetName(_handle, Utf8StringMarshaller.ConvertToAbi(name ?? "default"));
```

The choice depends on whether the C++ default is a compile-time constant or a runtime value.

### Value-type defaults

```cpp
void SetTransform(const Matrix& transform = Matrix::Identity);
```

C# cannot use value-type defaults with complex construction. The parameter becomes nullable:

```csharp
public void SetTransform(Matrix? transform = null)
{
    if (transform is not null)
        __SetTransform(_handle, in MatrixMarshaller.ConvertToAbi(transform.Value));
    else
        __SetTransformIdentity(_handle);
}
```

The generator emits a separate P/Invoke for the null case that calls the C++ default-argument overload.

### Container defaults

```cpp
void SetItems(const std::vector<int>& items = {});
```

```csharp
public void SetItems(Std.Vector<int>? items = null)
{
    if (items is not null)
        __SetItems(_handle, items.Handle);
    else
        __SetItemsDefault(_handle);
}
```

Contaner types cannot have C# default values. Nullable + null default + separate P/Invoke.

## Multiple defaulted parameters

```cpp
void Configure(int mode = 0, float scale = 1.0f, std::string name = "default");
```

```csharp
// The generator produces overloads or nullable parameters:
public void Configure(int mode = 0, float scale = 1.0f, string? name = null)
{
    if (name is not null)
        __Configure(_handle, mode, scale, Utf8StringMarshaller.ConvertToAbi(name));
    else
        __ConfigureDefaultName(_handle, mode, scale);
}
```

Builtin parameters keep their defaults. Non-translatable parameters become nullable.

## Out parameters with defaults

Default values on `out` parameters are meaningless (C# forbids default values on `out`). The generator omits the default:

```cpp
void GetPosition([[sharpcpp::out]] float* x = nullptr, [[sharpcpp::out]] float* y = nullptr);
```

```csharp
public void GetPosition(out float x, out float y) => __GetPosition(_handle, out x, out y);
```

If the C++ function allows null out pointers (meaning "don't write this value"), the C# overloads provide nullable versions:

```csharp
public void GetPosition() => __GetPositionNone(_handle);
public void GetPosition(out float x) => __GetPositionX(_handle, out x);
public void GetPosition(out float x, out float y) => __GetPositionXY(_handle, out x, out y);
```

## Interaction with pass-kind attributes

Default values combine with `[[sharpcpp::in/out/ref]]`:

```cpp
void Update([[sharpcpp::ref]] Value& value = Value::Default);
```

```csharp
public void Update(ref Value? value = null) // C# ref cannot have defaults
```

C# `ref` parameters cannot have default values. The generator must produce overloads:

```csharp
public void Update() => UpdateDefault(); // Calls C++ with no parameter
public void Update(ref Value value) => __Update(_handle, in ValueMarshaller.ConvertToAbi(value));
```

---

← [[15-generics]] | [[index]] →