# Operators

C++ operator overloads bind to C# operator overloads or named methods, depending on the operator.

## Arithmetic operators

### Binary operators

```cpp
Vector3 operator+(const Vector3& other) const;
Vector3 operator-(const Vector3& other) const;
Vector3 operator*(float scalar) const;
```

```csharp
public static Vector3 operator +(Vector3 left, Vector3 right) => new Vector3(__Add(left.Handle, right.Handle));
public static Vector3 operator -(Vector3 left, Vector3 right) => new Vector3(__Subtract(left.Handle, right.Handle));
public static Vector3 operator *(Vector3 self, float scalar) => new Vector3(__Multiply(self.Handle, scalar));
```

The parser maps `operator+` → `op_Addition`, `operator-` → `op_Subtraction`, etc.

### Unary operators

```cpp
Vector3 operator-() const;
```

```csharp
public static Vector3 operator -(Vector3 self) => new Vector3(__UnaryNegation(self.Handle));
```

### Compound assignment

C++ `operator+=` binds to a named method (C# doesn't allow overloading `+=`):

```cpp
Vector3& operator+=(const Vector3& other);
```

```csharp
public void Add(Vector3 other) => __AddAssign(_handle, other.Handle);
```

The generator emits `Add` rather than `operator +=`.

## Comparison operators

```cpp
bool operator==(const Vector3& other) const;
bool operator!=(const Vector3& other) const;
bool operator<(const Vector3& other) const;
```

```csharp
public static bool operator ==(Vector3 left, Vector3 right) => __Equality(left.Handle, right.Handle);
public static bool operator !=(Vector3 left, Vector3 right) => __Inequality(left.Handle, right.Handle);
public static bool operator <(Vector3 left, Vector3 right) => __LessThan(left.Handle, right.Handle);
```

Equality operators also get `Equals` and `GetHashCode` overrides when the class implements equality semantics.

## Subscript operator

```cpp
float& operator[](int index);
const float& operator[](int index) const;
```

```csharp
public float this[int index]
{
    get => __GetAt(_handle, index);
    set => __SetAt(_handle, index, value);
}
```

Mapped to C# indexer. The getter and setter are separate P/Invoke calls. The `operator[]` return type's marshaller handles the `ref` semantics for non-blittable element types.

## Function call operator

```cpp
float operator()(float x, float y) const;
```

```csharp
public float Invoke(float x, float y) => __Invoke(_handle, x, y);
```

Mapped to a named `Invoke` method. C# doesn't allow overloading `()`.

## Conversion operators

### Implicit conversion

```cpp
operator int() const;
```

```csharp
public static implicit operator int(MyType self) => __ToInt(self.Handle);
```

### Explicit conversion

```cpp
explicit operator std::string() const;
```

```csharp
public static explicit operator StdString(MyType self) => new StdString(__ToString(self.Handle));
```

The `explicit` keyword in C++ maps to `explicit` in C# (requiring cast). Without `explicit`, the conversion is implicit.

## Dereference and member access

```cpp
T& operator*();
T* operator->();
```

Rarely bound directly. Instead, the generator typically exposes the dereferenced value as a property:

```csharp
public T Value => new T(__GetValue(_handle), ownsHandle: false);
```

## Operator pass direction

Most operator parameters follow the same pass-direction rules as regular functions:

- `const T&` → `in` semantics
- `T` → value semantics
- Return by value → owned handle or value-type conversion
- Return by reference → borrowed handle

See [[11-parameters-returns]] for the full pass-direction reference.

---

← [[13-fields-properties]] | [[15-generics]] →
