# 09 — Pass Boundaries

V2 is not "parser + renderers only". The pass pipeline stays. What changes is that each stage owns a tighter contract.

## Stage ownership

| Stage | Owns | Must not own |
|---|---|---|
| Phase 1 parse | native declarations, overloads, constness, indirection, inheritance, source references | managed policy |
| Phase 2 instantiation | resolved marshaller metadata, imported metadata merge, capability flags | surface reshaping |
| Structural passes | synthetic members, projected virtuals, field properties, naming, layout | ABI reinterpretation |
| Validation / degradation passes | diagnostics, unsupported-surface decisions, fail-fast gates | text emission |
| Renderers | C#, C++, metadata output | semantic inference |

## Required pass classes

### 1. Materialization passes

Create explicit AST surface from metadata:

- injected members
- field getters/setters
- projected interface / secondary-base members
- director helper members

### 2. Normalization passes

Make renderer assumptions true:

- override access normalization
- naming normalization
- namespace / container normalization
- constructor / property pairing

### 3. Discovery passes

- generic instantiation closure
- imported metadata reachability
- runtime registration set

### 4. Validation / degradation passes

- missing marshaller metadata
- unsupported callback / director combinations
- unsupported virtual inheritance
- invalid ownership / projection combinations

## Renderer assumptions

By render time, these must already be true:

1. every renderable parameter/return has resolved metadata
2. every projected synthetic member is already on the AST
3. unsupported members are already removed or marked diagnostic
4. naming collisions are already resolved
5. ownership / identity / cache hooks are already explicit

If a renderer needs to branch on discovery logic, a pass is missing.

## Diagnostics boundary

Diagnostics belong before rendering:

- parser diagnostics: malformed native declarations, missing includes, clang failures
- metadata diagnostics: invalid `managed_type`, bad reuse, bad marshaller names
- shaping diagnostics: ambiguous projected members, impossible overrides
- validation diagnostics: unsupported combinations

Renderers may assert invariants, but they should not be the first place a user learns a surface is unsupported.

## Why this matters

The current generator's hardest-to-maintain code exists where passes, validators, and renderers all partially own the same policy. V2 stays robust only if every stage has a narrow, explicit job.

---

← [[08-metadata-contract]] | [[10-ownership-lifetime]] →
