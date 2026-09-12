# Porting semantics

Use for initial direct ports and investigations of unmodified features. The
source-of-truth boundary in `../AGENTS.md` governs intentional RedBolt development.
Locate upstream rbfx through the checkout's remotes and workspace discovery;
do not depend on an old absolute source path.

Preserve class shape, member access, declaration order, method logic, and source
comments, translating Doxygen to C# XML documentation. Port all members unless
`ALLOWED_DEVIATIONS.md` permits replacement by C# facilities. Add no speculative
fallbacks or helpers to bypass missing upstream behavior. Complete integration
and relevant verification before treating a port as finished; no fixed number
of conversion passes is required. Record completed and missing port items in
`PORTING_CHECKLIST.md` when that checklist is part of the port.

If an API cannot express upstream behavior yet, retain the C++ fragment with a
`// TODO: port` explanation and report the missing implementation; this does not
make the requested port complete. Preserve `WeakPtr` weak ownership with
`WeakReference` where required without recreating intrusive reference counting.
For C++ friend access, use the narrow bridges described in
`ALLOWED_DEVIATIONS.md`; preserve the original member access and virtual dispatch.

## Type mappings


- Match the C++ type as closely as C# allows.
- C++ getter/setter PAIRS may become a C# property, BUT the getter/setter
  body logic must still match the C++ code exactly.
- DO use nullable reference types (annotate with `?`); they are explicitly allowed and
  are the required mapping for C++ pointers / optional references.
- Prefer plain C# constructs over clever ones.
- A single value that C++ exposes as a getter should be a C# property,
  not a `GetX()` method, UNLESS matching C++ shape is clearer.


## Lifetime and ownership


C++ syntax that looks like an ordinary assignment or return may invoke a
constructor, copy constructor, move constructor, assignment operator, or
destructor. C# reference assignment does not. These operations are part of the
implementation and MUST be ported explicitly.

- Before porting a constructor, copy/move operation, assignment, or destructor,
  read its complete C++ declaration and definition, including base classes,
  field declarations, default member initializers, delegated constructors, and
  the relevant call sites.
- Port every C++ constructor overload. Preserve base-constructor calls,
  delegated-constructor behavior, default values, side effects, and observable
  initialization ordering. C++ initializes bases first and fields in declaration
  order, regardless of initializer-list order; the C# port must preserve the
  resulting behavior.
- A C++ value copy such as `T copy = source`, pass-by-value, return-by-value, or
  copy construction MUST produce the same independent value in C#. Never replace
  it with a C# reference alias.
- Implement C++ copy constructors as `ICloneable.Clone()` and call `Clone()` at
  every C# site corresponding to C++ copy construction. Port copy-assignment
  operator behavior explicitly when C++ uses it.
- Do NOT clone C++ pointers or references that intentionally alias the same
  object. Preserve C++ aliasing, ownership, and nullability exactly.
- Generated native binding wrappers require special care: a getter may return a
  borrowed wrapper around native storage even when C++ returns a value or the C++
  call site immediately copies it. If C++ creates a value copy, use the generated
  copy constructor / `Clone()` before mutating, retaining, or disposing it.
  Mutating a borrowed wrapper is not equivalent to mutating a C++ copy.
- C# `struct` assignment is not automatically equivalent to C++ value semantics
  when the struct contains managed references or native handles. Verify whether
  the C++ operation is deep, shallow, borrowed, owned, copied, or moved and map
  that behavior explicitly.
- Port every C++ destructor as `Dispose()`. Preserve destructor-body actions,
  teardown ordering, callbacks, unregistering, resource release, and ownership
  effects. Do not rely on garbage collection or a finalizer as a replacement for
  deterministic C++ destruction.
- Reproduce C++ RAII local-variable lifetime with `using`, `try/finally`, or an
  equivalent deterministic scope. A temporary that dies at the end of a C++
  statement or scope must not remain alive until a later C# garbage collection,
  and an owned object must not be disposed while C++ would still keep it alive.
- Distinguish borrowed wrappers from owned wrappers. Dispose the managed wrapper
  at the matching C++ lifetime boundary without releasing a native object that
  C++ does not own, and never mutate or dispose an alias when C++ operates on an
  independent copy.
