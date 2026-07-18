# Primitives & Enums

Blittable types cross the boundary with no marshalling. The default `Marshaller<T>` for numeric and enum types is a passthrough.

## Numeric primitives

### Built-in numeric types

`int`, `float`, `double`, `bool`, `byte`, `sbyte`, `short`, `ushort`, `uint`, `ulong`, `nint` — all blittable between C++ and C#.

```cpp
int Add(int a, int b);
```

```csharp
[LibraryImport("libnative")]
internal static partial int Add(int a, int b);
```

No marshaller needed. No `MarshalUsing`. No attribute.

### CLong / CULong

`long` and `unsigned long` in C++ vary by platform (4 bytes on 32-bit, 8 bytes on 64-bit). The marshaller maps to `CLong`/`CULong`:

```cpp
long GetSize();
unsigned long GetCount();
```

```csharp
[LibraryImport("libnative")]
internal static partial CLong GetSize();

[LibraryImport("libnative")]
internal static partial CULong GetCount();
```

The C# side uses `CLong`/`CULong` from `System.Runtime.InteropServices`. The marshaller specialization:

```cpp
template <>
struct Marshaller<long>
{
    using cpp_type = long;
    using abi_type = CLong;  // maps to CLong in managed
    // blittable on both sides — no ConvertToC/ConvertToCpp needed
};
```

### Size_t / IntPtr

`size_t` maps to `nuint`. `intptr_t` maps to `nint`. Both are blittable.

```cpp
size_t GetBufferSize();
intptr_t GetHandle();
```

```csharp
[LibraryImport("libnative")]
internal static partial nuint GetBufferSize();

[LibraryImport("libnative")]
internal static partial nint GetHandle();
```

### bool

C++ `bool` (1 byte) maps to C# `bool` (marshaller: blittable in LibraryImport).

## Pass direction variants

### in (default for value types)

```cpp
void Process(int value);
```

```csharp
[LibraryImport("libnative")]
internal static partial void Process(int value);
```

No modifier. Value passed by copy.

### out

```cpp
void GetValue([[sharpcpp::out]] int* value);
```

```csharp
[LibraryImport("libnative")]
internal static partial void GetValue(out int value);
```

The `[[sharpcpp::out]]` attribute sets `PassKind = Out`. The C# side uses `out`.

### ref

```cpp
void UpdateValue([[sharpcpp::ref]] int& value);
```

```csharp
[LibraryImport("libnative")]
internal static partial void UpdateValue(ref int value);
```

The `[[sharpcpp::ref]]` attribute sets `PassKind = Ref`. The C# side uses `ref`.

### Pointer to primitive

```cpp
int* FindItem(int* data, int count);
```

For `T*` where T is numeric, the marshaller uses pointer-passthrough. C# side: `int*` (unsafe) or `nint`.

```csharp
[LibraryImport("libnative")]
internal static partial nint FindItem(nint data, int count);
```

The ABI is blittable for numeric pointers. No `MarshalUsing`.

### Reference to primitive

```cpp
void Increment(int& count);
```

The `Marshaller<int&>` specialization converts `int&` → `int*` for ABI. In the parameter direction, it passes a pointer. On return, it syncs the value back.

```csharp
[LibraryImport("libnative")]
internal static partial void Increment(ref int count);
```

### Fixed-size array parameter

```cpp
void SetMatrix([[sharpcpp::count(16)]] float* values);
```

```csharp
[LibraryImport("libnative")]
internal static partial void SetMatrix([MarshalUsing(typeof(ContiguousBufferMarshaller<float>)), Count(16)] ReadOnlySpan<float> values);
```

The `[[sharpcpp::count(...)]]` attribute provides the element count. See [[11-parameters-returns]] for full details.

## Enums

### Regular enum

C++ enum maps to a C# enum with the same underlying type:

```cpp
enum class Color : int { Red, Green, Blue };
```

```csharp
public enum Color { Red, Green, Blue }
```

The marshaller is a blittable passthrough. The C# underlying type matches the C++ underlying type.

### Flagset (bit flags)

A C++ flagset wrapper class maps to a C# enum with `[Flags]`:

```cpp
// C++ flagset
class MyFlags
{
public:
    using Enum = unsigned int;
    // ... operators ...
};

template <>
struct Marshaller<MyFlags>
{
    using cpp_type = MyFlags;
    using abi_type = MyFlags::Enum;
    // ... converts through underlying enum ...
};
```

```csharp
[Flags]
public enum MyFlags : uint { None = 0, FlagA = 1, FlagB = 2, FlagC = 4 }
```

The `[[sharpcpp::managed_type(...)]]` attribute on the flagset Marshaller declares `public_type` as the flags enum name. The generator detects the `flagset_like` concept in C++ and generates `[Flags]`.

### Enum as underlying type

When a function parameter or return uses the enum's underlying type directly:

```cpp
int GetValue(Color color);
```

```csharp
[LibraryImport("libnative")]
internal static partial int GetValue(Color color);
```

The enum type itself is blittable. No marshaller needed.

### Enum pointer / reference

```cpp
void GetColor([[sharpcpp::out]] Color* outColor);
void SetColor([[sharpcpp::ref]] Color& color);
```

```csharp
[LibraryImport("libnative")]
internal static partial void GetColor(out Color outColor);

[LibraryImport("libnative")]
internal static partial void SetColor(ref Color color);
```

Pointer and reference follow the same `out`/`ref` pattern as primitives. The marshaller for `Color*` (enum pointer) returns the enum value by copy rather than leaving a dangling pointer.

### Enum in container

When an enum is the element type of a container, the element accessor uses the enum's blittable marshalling:

```cpp
std::vector<Color> GetColors();
```

The vector marshaller handles the container; each element access goes through `Marshaller<Color>` which is blittable.

---

← [[index]] | [[01-handles]] →
