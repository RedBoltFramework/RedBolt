# 15 — Compatibility Policy

V2 has three independent but related compatibility axes:

1. generator/tool version
2. metadata schema version
3. support runtime contract version

The design must keep them separate so compatibility checks are explicit instead of guessed from package names.

## Compatibility objects

### Generator version

Human-facing implementation version of the tool.

Use for:

- provenance
- debugging
- release tracking

Do **not** use generator version alone as the compatibility gate.

### Metadata schema version

Defines how `.Bindings.json` is interpreted.

Use for:

- import/export compatibility
- deterministic serialization rules
- merge semantics

### Support runtime contract version

Defines the managed/native runtime assumptions generated code depends on.

Use for:

- callback ABI compatibility
- cache/runtime identity hooks
- native support header compatibility
- managed support assembly compatibility

## Required compatibility checks

### During metadata import

Check:

- imported metadata schema is supported
- imported metadata contract version is compatible with local consumer
- imported module identities do not collide with local exports

### During code generation

Check:

- generated output records the support runtime contract version it targets
- imported modules do not require a conflicting runtime contract

### During build/package composition

Check:

- native support headers and managed support assemblies come from the same runtime-contract line
- CMake package/config exports do not mix incompatible v1/v2 target families

## Compatibility policy shape

Recommended rule set:

### Metadata schema

- same major = potentially compatible
- newer minor = compatible only if consumer understands unknown optional fields
- major mismatch = hard failure

### Support runtime contract

- same major required
- minor mismatch allowed only if explicitly declared backward-compatible
- patch differences allowed by default

### Generator version

- informational unless paired with schema/runtime contract changes

## Compatibility matrix

| Axis | Compatible by default | Hard fail |
|---|---|---|
| Generator version | patch/minor differences with same contracts | none by itself |
| Metadata schema | same major, understood minor | major mismatch |
| Support runtime contract | same major, compatible minor | major mismatch or undeclared minor mismatch |

## What generated modules must declare

Every exported module must declare at least:

- generator version
- metadata schema version
- support runtime contract version
- imported module versions/identities it was built against

This data belongs in metadata, and package/install surfaces should expose it where useful.

## Compatibility failure behavior

When compatibility fails:

- generation/import fails before rendering when possible
- build/package composition fails before shipping mixed artifacts
- runtime load should fail early if a mismatched support runtime somehow slips through

No silent downgrade. No best-effort guessing.

## Relationship to rewrite strategy

Because v2 is an independent project line, v1 and v2 should be treated as incompatible by default unless a bridge is explicitly designed.

That means:

- v2 should not import v1 metadata as if it were native v2 metadata
- v2 should not share support-runtime contract identity with v1 accidentally
- parallel CMake/package names should stay distinct during the rewrite

## Rule of thumb

If two modules cannot prove they agree on metadata schema and support runtime contract, they are incompatible.

---

← [[14-diagnostics-and-failure-model]] | [[index]] →
