# 10 — Ownership & Lifetime

Ownership is a cross-cutting contract. Strings, handles, projections, callbacks, caches, and keep-alives all participate in the same state machine.

## Ownership states

| State | Meaning |
|---|---|
| Borrowed | Managed wrapper references native object but must not destroy it |
| Owned | Managed wrapper is responsible for native release |
| Shared | Native lifetime is controlled by shared ownership (`shared_ptr`, ref count, keep-alive) |
| Pinned | Managed memory is exposed to native code for a bounded lifetime |
| Cached | A managed wrapper is registered as the identity representative for a native handle |
| Callback-owned | Callback state is held behind `ManagedCallbackHandleAbi` until release fires |

## Invariants

1. **Complete-object handle is identity.** Base-view pointers normalize before cache lookup.
2. **One live managed identity per normalized native handle.** Instance caching enforces this.
3. **Runtime type IDs are dispatch tokens only.** They are never exported or persisted.
4. **Borrowed does not destroy.** Borrowed wrappers may cache, but disposal must not free native storage.
5. **Pinned memory is scoped.** Any native pointer into managed memory must have an explicit invalidation boundary.
6. **Callback state release is explicit.** `ManagedCallbackHandleAbi.release` owns callback-state destruction.

## Common transitions

| Transition | Enforced by |
|---|---|
| borrowed handle → cached wrapper | `CreateManagedWrapper` + instance cache |
| owned handle → released native object | `ReleaseNativeHandle` / support runtime |
| shared owner → keep-alive wrapper | projection helpers / keep-alive API |
| managed span → pinned native-visible bytes | string / buffer support helpers |
| callback registration → callback-owned state | callback marshaller / ABI handle |
| native destroy → cache invalidation | native runtime disposal hook |

## Strings

- `ReadOnlySpan<byte>` return over native memory is borrowed view state
- `StdString` is owned wrapper state
- `string` is copied managed state
- `ref/out string` may require ABI intermediate + copy back

## Handles and projections

- raw pointer returns choose borrowed vs owned based on native policy
- `shared_ptr` / ref-counted projections must carry keep-alive or reference-count transitions explicitly
- projected managed surface must not hide who really owns the native object

## Callbacks and directors

- callback state is owned by `ManagedCallbackHandleAbi` until `release` fires
- managed exceptions must not cross the native ABI boundary unchecked
- director callbacks follow the same ownership rule as ordinary callbacks

## Caches

- caches hold identity, not ownership policy
- owning wrappers take precedence over non-owning wrappers for the same handle
- cache invalidation happens on managed disposal or native runtime disposal notification

## Why this matters

Most lifetime bugs happen at feature intersections: cached borrowed handle, ref-counted projection, callback-captured owner, span over temporary storage. V2 documents one ownership model so those intersections are designed, not improvised.

---

← [[09-pass-boundaries]] | [[11-project-shape-blueprint]] →
