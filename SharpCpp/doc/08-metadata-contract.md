# 08 — Metadata Contract

Binding metadata is a first-class product of the generator. It is not a debug dump.

Downstream generators rely on it for:

- imported wrapper identities
- suppress-emission decisions
- reused marshaller surfaces
- project-to-project binding composition

## Exported metadata must include

Every metadata file must carry:

- `schema_version`
- `generator_version`
- `support_runtime_version`
- source / package identity
- canonical type identities
- imported metadata dependencies
- build fingerprint inputs

## Build fingerprint inputs

Metadata validity depends on more than source files. The fingerprint must include:

- Phase 2 instantiation closure
- project headers and marshaller headers
- clang args / target triple / defines / include roots
- support runtime version
- imported metadata content hashes
- SharpCpp attribute vocabulary version

If any of those change, the metadata is stale.

## Merge precedence

When metadata comes from multiple places, precedence is explicit:

1. local project metadata
2. imported upstream metadata
3. built-in fallback metadata

Local wins only when the project explicitly binds the type itself. Silent shadowing of imported metadata is not allowed.

## Identity rules

Metadata stores **build-time identity**, not runtime dispatch identity.

- export canonical C++ type identity
- export managed type identity
- do **not** export `typeid(...).hash_code()`
- do **not** export process-local cache keys

Runtime type IDs are valid only inside one support runtime process.

`canonical C++ type identity` means an AST-resolved identity with a stable serialized representation. The exported text is the serialization of that identity, not the semantic key the generator should compare internally during parsing.

## Compatibility policy

| Change | Compatibility |
|---|---|
| add optional field | backward-compatible |
| remove field | breaking |
| change field meaning | breaking |
| rename exported type identity | breaking |
| change support runtime contract without version bump | invalid |

Consumers reject newer breaking schema versions instead of guessing.

## Determinism

Given identical inputs, metadata output must be byte-stable:

- deterministic ordering of records / members
- stable formatting
- stable hashing inputs
- no process-local values in exported payload

Deterministic metadata is required for incremental caching and reviewable diffs.

## Relationship to rendering

The metadata renderer exports facts already materialized in the binding model. It does not recompute policy. If a consumer needs a fact, an earlier stage must produce it explicitly.

## Minimal payload sketch

```json
{
  "schema_version": 1,
  "generator_version": "v2-preview",
  "support_runtime_version": "v2-preview",
  "source": { "package": "MyBindings" },
  "imports": [
    { "package": "SharpCpp.CoreBindings", "hash": "..." }
  ],
  "fingerprint": {
    "phase2_closure_hash": "...",
    "clang_args_hash": "...",
    "attribute_vocab_hash": "..."
  },
  "types": [ ... ]
}
```

Exact payload shape may evolve, but versioning / precedence / determinism are mandatory.

## Relationship to generated outputs

Metadata is one artifact in a larger module output set. The full source/output contract and cross-module consumption rules are defined in [[12-generated-output-and-module-api]].

---

← [[07-marshaller-contract]] | [[09-pass-boundaries]] →
