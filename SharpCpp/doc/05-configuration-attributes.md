# 05 — Configuration Attributes

C++ attributes are the single source of truth for generator configuration. No separate C# configuration objects.

## `[[sharpcpp::rename(...)]]`

Applied to C++ declarations to override the managed name. The single argument is a format string.

| Field | Type | Description |
|---|---|---|
| (value) | string | The desired managed name. Supports `{Name}` placeholder for the PascalCase native name and `{0}`, `{1}` placeholders for generic type arguments |

### Supported element types

- **Struct / class** — renames the managed class name. For generic records, the format string also sets `ManagedTypeFormat` for instantiation naming.
- **Enum** — renames the managed enum name.
- **Free function** — renames the managed method name.
- **Instance method** — renames the managed method name.
- **Static method** — renames the managed method name.
- **Field** — renames the managed property name.

Constructors follow the class name and are not independently renamable. Parameters are not renamable in this slice.

### `{Name}` placeholder

Resolves to the PascalCase version of the element's native C++ name. Useful for suffix/prefix transformations:

```cpp
// value_box → ValueBoxWrapper
struct [[sharpcpp::rename("{Name}Wrapper")]] value_box { ... };

// do_calc → DoCalcAsync
[[sharpcpp::rename("{Name}Async")]]
int do_calc(int a, int b);
```

### Generic placeholder

Placeholders `{0}`, `{1}`, etc. refer to generic type arguments — same semantics as `public_type` in `managed_type`. `{Name}` is resolved at parse time; `{0}`/`{1}` at format time.

```cpp
// value_box<T> → BoxOf<T>
template <typename T>
struct [[sharpcpp::rename("BoxOf<{0}>")]] value_box { ... };

// item_holder<T> → ItemHolderCollection<T>
template <typename T>
struct [[sharpcpp::rename("{Name}Collection<{0}>")]] item_holder { ... };
```

### Precedence

When `[[sharpcpp::rename(...)]]` and a `[[sharpcpp::managed_type(public_type = ...)]]` on a `Marshaller<T>` both exist for the same record and disagree, rename takes precedence with a warning emitted to stderr.

## `[[sharpcpp::managed_type(...)]]`

Applied to `Marshaller<T>` specializations. Declares how the C++ type maps to managed code. For simple type renames, prefer `[[sharpcpp::rename(...)]]` instead.

### Attribute fields

| Field | Type | Default | Description |
|---|---|---|---|
| `public_type` | string | required | Managed public-facing type name. Supports `{0}`, `{1}` placeholders for generic arguments |
| `abi_type` | string | same as `public_type` | Managed ABI type name. The type that appears in `LibraryImport` signatures |
| `marshaller_type` | string | null | C# marshaller type for `MarshalUsing`. Null means blittable |
| `parameter_marshaller_type` | string | `marshaller_type` | Override for parameter context |
| `return_marshaller_type` | string | `marshaller_type` | Override for return context |
| `managed_base_type` | string | null | C# base class override |
| `managed_base_type_constraints` | string | null | C# generic constraints for the managed base class. Uses `{0}`, `{1}` placeholders referencing generic type arguments. Example: `"where {0} : notnull"` |
| `projection` | bool | false | The managed type is a generic argument, not the wrapper type itself |
| `polymorphic` | bool | false | Enable runtime wrapper selection for this surface when the native type is structurally polymorphic |
| `suppress_emission` | bool | false | Don't emit the managed type itself (imported from support library or upstream) |
| `reuse_builtin_marshaller` | string | null | Reuse the marshaller from another type |
| `callback` | bool | false | This type is a callable type |

### Format string placeholders

Placeholders `{0}`, `{1}`, etc. refer to generic type arguments. The generator substitutes the managed type names of the generic arguments.

Example: `Marshaller<std::optional<T>>` with `public_type = "{0}?"` — when T = `int`, the generator substitutes to get `public_type = "int?"`.

### Examples

```cpp
// Owned string → StdString wrapper
struct [[sharpcpp::managed_type(
    public_type = "StdString",
    managed_base_type = "global::SharpCpp.Support.StringBase",
    abi_type = "Utf8StringAbi",
    marshaller_type = "Utf8StringMarshaller"
)]]
Marshaller<std::string> {};

// Optional → nullable
struct [[sharpcpp::managed_type(
    public_type = "{0}?",
    abi_type = "OptionalValueAbi<{0}>",
    marshaller_type = "OptionalValueMarshaller<{0}>",
    suppress_emission = true
)]]
Marshaller<std::optional<T>> {};

// Vector → Std.Vector
struct [[sharpcpp::managed_type(
    public_type = "Std.Vector<{0}>",
    managed_base_type = "global::SharpCpp.Support.VectorBase<{0}>",
    abi_type = "void*"
)]]
Marshaller<std::vector<T, TAllocator>> {};

// Record handle → wrapper with polymorphic dispatch
template <>
struct [[sharpcpp::managed_type(
    public_type = "MyClass",
    abi_type = "nint",
    polymorphic = true
)]]
Marshaller<MyClass*> {};

// Secondary base → interface (detected automatically, no attribute needed)
class Serializable { ... };
```

Note: secondary base classes that appear as non-primary bases in inheritance lists are automatically detected by the core. No `render_interface` attribute is needed — the generator produces C# interfaces for secondary bases on its own.

`polymorphic = true` does not create polymorphism by itself. Native polymorphism is structural and comes from the C++ AST (virtual methods / polymorphic bases). The attribute enables runtime wrapper selection for the managed surface of that type.

## `[[sharpcpp::expose_member(...)]]`

Applied to static methods within `Marshaller<T>` specializations. Declares members to inject into the managed wrapper type.

### Attribute fields

| Field | Type | Default | Description |
|---|---|---|---|
| `shape` | string | `"method"` | Member shape: `"method"`, `"protected_override"`, `"property_getter"`, `"property_setter"` |
| `exposed` | bool | true | Whether the member appears in the public managed API |
| `disambiguate` | bool | true | Whether to disambiguate overloads by parameter count |

### Example

```cpp
template <typename T>
struct Marshaller<std::vector<T>>
{
    [[sharpcpp::expose_member(shape = "protected_override", exposed = false)]]
    static unsigned long long GetCountCore(const std::vector<T>& self)
    {
        return self.size();
    }
};
```

Generates a `FunctionBinding` on the record with:
- Managed name derived from the method name (stripped `Core` suffix)
- Parameters derived from the static method signature (minus the `self` parameter)
- Native body that calls through the marshaller

## `[[sharpcpp::director]]`

Applied to C++ class declarations. Marks the class as supporting C++ director pattern for managed overrides.

### Effect

- Parser records `IsDirector` on the record
- A pass generates virtual-method-slot metadata
- C++ renderer emits a director class that overrides virtual methods to call managed callbacks
- C# renderer emits `ConnectDirector()` and override infrastructure

### Example

```cpp
class [[sharpcpp::director]] Widget
{
    virtual void OnUpdate(float dt);
};
```

## Callback declaration

V2 does not use a separate `[[sharpcpp::callback]]` record attribute. Callback-ness is declared on the marshaller surface itself:

```cpp
template <typename T>
struct [[sharpcpp::managed_type(
    callback = true,
    public_type = "Action<{0}>",
    abi_type = "ManagedCallbackHandleAbi",
    marshaller_type = "ManagedCallbackMarshaller<{0}>"
)]]
Marshaller<std::function<T>> {};
```

`callback = true` tells the generator to emit delegate shape, callback ABI plumbing, and callback-specific validation.

## `[[sharpcpp::count(...)]]`

Applied to function parameters and fields. Declares the element count for array/buffer types.

### Attribute value

A string that is either:
- An integer literal: `[[sharpcpp::count(16)]]`
- A reference to another parameter: `[[sharpcpp::count(length)]]`
- A C++ expression: `[[sharpcpp::count(self.size())]]`

### Example

```cpp
void CopyData(const int* data, [[sharpcpp::count(length)]] int length);
```

## `[[sharpcpp::string_binding(...)]]`

Applied to function parameters. Overrides the default string surface for this parameter.

### Attribute value

One of: `"span"`, `"string"`, `"wrapper"`, `"auto"`.

### Example

```cpp
void SetName([[sharpcpp::string_binding("string")]] const char* name);
```

## `[[sharpcpp::callback_user_data(...)]]`

Applied to function parameters. Declares this parameter as the user-data parameter in a callback signature.

### Example

```cpp
void RegisterCallback(
    void (*callback)(void*, int),
    [[sharpcpp::callback_user_data("callback")]] void* userData);
```

## `[[sharpcpp::in]]`, `[[sharpcpp::out]]`, `[[sharpcpp::ref]]`

Applied to function parameters. Override the inferred parameter pass kind.

Without these attributes, the generator infers the most reasonable shape from the native signature:

- `const T&` → `in`
- `T&` → `ref`
- `const T*` → `in`
- `T*` → `out`

Use the attributes only when the native signature's mutability does not match the intended managed semantics.

### Example

```cpp
void GetValue([[sharpcpp::out]] int* value);
void UpdateRef([[sharpcpp::ref]] int& value);
```

---

← [[04-type-handling]] | [[06-core-minimal]] →
