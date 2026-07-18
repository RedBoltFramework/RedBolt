# 04 — Type Handling

Every type category uses the same marshaller path. The renderer reads marshaller metadata and emits — it does not branch on type category.

## Detailed files

| # | File | Description |
|---|---|---|
| [[00-primitives-enums]] | Primitives & Enums | Numeric, bool, CLong, size_t, enum, flagset; in/out/ref/ptr variants |
| [[01-handles]] | Handles | Owned/borrowed/shared_ptr/weak_ptr/polymorphic handles; null; pointer-to-pointer |
| [[02-strings]] | Strings | **Span-first**: `ReadOnlySpan<byte>` default, `StdString` for owned, `string` opt-in only; `string_binding` override; all pass directions |
| [[03-sequential-containers]] | Sequential Containers | vector, span, array, list; owned/borrowed/span surfaces; exposed members |
| [[04-associative-containers]] | Associative Containers | map, unordered_map; key/value access; custom containers |
| [[05-optional-nullable]] | Optional & Nullable | optional→T?, nullable handles, optional handles |
| [[06-pair-tuple]] | Pair & Tuple | value-type composite; blittable/non-blittable; Value/ConstRef/Ref marshaller variants |
| [[07-callbacks]] | Callbacks | function→delegate; ManagedCallbackHandleAbi; raw function pointers; user-data; all pass directions |
| [[08-inheritance]] | Inheritance | director pattern, MI via interfaces, handle adjustment, polymorphic wrapping |
| [[09-instance-caching]] | Instance Caching | DefaultNativeInstanceCache, polymorphic type dispatch, NativeObjectRegistry, RefCountedInstanceCache, handle adjustment |
| [[10-projection]] | Type Projection | shared_ptr→T, unique_ptr→T, custom projection; non-projected smart pointers |
| [[11-parameters-returns]] | Parameters & Returns | Pass kind inference (in/out/ref), indirection, count, string_binding, callback_user_data |
| [[12-constructors-destructors]] | Constructors & Destructors | Default, parameterized, static factory, copy/move, RAII, default params |
| [[13-fields-properties]] | Fields & Properties | Blittable/non-blittable fields, property generation, expose_member shapes |
| [[14-operators]] | Operators | Arithmetic, comparison, subscript, call, conversion, compound assignment |
| [[15-generics]] | C++ Templates as Generics | Template→generic mapping, instantiation discovery, non-type params, constraints |
| [[16-default-parameters]] | Default Parameters | Builtin literals → C# defaults; non-translatable → nullable; overloads for null-default |

## Key principle

No type category has special-case code paths in the renderer. Each type category declares its behavior through `[[sharpcpp::managed_type(...)]]` attributes on its `Marshaller<T>` specialization. The renderer reads `public_type`, `abi_type`, `marshaller_type`, and `parameter_marshaller_type`/`return_marshaller_type` to emit the correct C# and C++ code.

---

← [[03-two-phase-parsing]] | [[05-configuration-attributes]] →