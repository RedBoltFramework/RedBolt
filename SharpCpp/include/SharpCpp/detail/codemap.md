# include/SharpCpp/detail/

## Responsibility

Implementation helpers for the `Marshaller<T>`, `ReturnMarshaller<T>`, and `MarshallerInvoker` public API. All symbols live in `namespace SharpCpp::detail` and are **not intended for direct consumption** by generated code or client headers; they are implementation details pulled in by `MarshallerInvoker`.

## Design

| File                  | Contents |
|-----------------------|----------|
| `MarshallerSupport.hpp` | Tuple-to-marshaller-tuple mapping (`MarshallerTupleForTuple`), ABI→C++→ABI invocation implementation (`InvokeWithMarshallersImpl`, `InvokeMemberWithMarshallersImpl`), function-type decomposition (`FunctionTraits`, `MemberFunctionTraits`), heap construction helper (`ConstructImpl`), and field read/write helpers. |
| `OptionalSupport.hpp`   | `OptionalValue<T>` — a simple `{bool HasValue; T Value;}` struct for ABI-safe optional value representation. |

### Key Patterns

- **Fold-expressions over index sequences**: the invocation impls use `std::index_sequence` + fold over `std::get<TIndices>` to process variadic argument tuples without runtime overhead.
- **`if constexpr` dispatch over traits**: `std::is_void_v`, `std::is_lvalue_reference_v`, and field-helper traits select the correct capture/conversion codepath at compile time.
- **CRTP-free static polymorphism**: The detail layer consists of standalone function templates that are instantiated per call signature by `MarshallerInvoker`, avoiding virtual dispatch entirely.

## Flow

```
MarshallerInvoker::invoke_as<FT>(callable, abiArgs...)
  → FunctionTraits<FT> yields return_type, argument_tuple
  → marshaller_tuple_t<arg_tuple> = tuple<ParameterMarshaller<Arg0>, ...>
  → InvokeWithMarshallersImpl<result_type, arg_tuple>(
       callable, marshallers, index_sequence, abiArgs...)
       → ConvertToCpp on each abiArg via marshaller
       → std::invoke(callable, cppArgs...)
       → if void: invoke, return void
       → if lref: addressof(invoke), return deref
       → if value: invoke, return value
       → ReturnMarshaller<result_type>.ConvertToAbi(result)
```

## Integration

- Included by `MarshallerInvoker` (defined at the bottom of `MarshallerSupport.hpp`), which is the sole public entrypoint.
- In turn pulled into generated `.g.cpp` files via `#include <SharpCpp/Marshaller.hpp>` (which does **not** include this file — the detail header must be explicitly included by the code that uses `MarshallerInvoker`).
