# 14 — Diagnostics & Failure Model

V2 needs a clear answer to two questions:

1. when do we fail the pipeline?
2. when do we degrade one symbol/member and continue?

Without that contract, failures move around unpredictably between parser, passes, renderers, and runtime.

## Diagnostic record shape

Every diagnostic should carry:

- stable diagnostic code
- severity
- stage
- human-readable message
- primary source location when available
- related locations when useful
- affected symbol/type identity when known
- optional remediation hint

## Stages

Diagnostics are emitted from these stages only:

1. parse
2. phase 2 marshaller instantiation
3. metadata import/merge
4. shaping/normalization passes
5. validation/degradation passes
6. runtime support boundary checks

Renderers may assert invariants, but renderers should almost never be the first place a user learns a surface is unsupported.

## Severity model

### Error

The current declaration/module cannot be trusted to produce correct output.

Examples:

- malformed `managed_type` metadata
- incompatible imported metadata schema
- missing required marshaller type
- duplicate exported managed identity
- invalid callback/director combination

### Warning

Generation continues, but output shape changed or a declaration/member was omitted.

Examples:

- unsupported member omitted from an otherwise valid type
- fallback naming/disambiguation applied
- optional enhancement skipped

### Info

Trace/debug-level guidance. Not a correctness signal.

Examples:

- imported metadata source chosen by precedence rule
- cache hit/miss explanation in verbose mode

## Fatal vs degradable failures

### Fatal to the whole pipeline

Fail the module generation when any of these occur:

- parse cannot build a trustworthy AST
- phase 2 cannot produce trustworthy marshaller metadata
- metadata import/version mismatch prevents safe composition
- exported type identity would be ambiguous or inconsistent
- support-runtime compatibility check fails

### Degradable at member level

Allow generation to continue, usually with an error or warning attached to the affected declaration, when:

- one member uses an unsupported combination
- one property pairing fails
- one callback signature is unsupported
- one virtual edge case cannot be projected safely

But only if the containing module can still export a coherent API.

### Degradable at type level

An entire type may be omitted if:

- it cannot be safely represented
- its omission does not invalidate identities exported by other types
- downstream imports cannot mistake omission for success

This still emits a strong diagnostic.

## Stage-specific rules

### Parse stage

Fatal:

- clang invocation failure
- missing required headers
- structurally invalid declaration extraction

Degradable:

- isolated declaration skipped because source is malformed but rest of translation unit remains usable

### Phase 2 instantiation stage

Fatal:

- explicit instantiation file cannot parse
- required marshaller alias/attribute missing
- ambiguous marshaller resolution for a required API type

Degradable:

- optional helper metadata absent for a non-essential feature

### Metadata import stage

Fatal:

- schema incompatibility
- imported identity collision
- required referenced module artifact missing
- generator/runtime compatibility mismatch

Degradable:

- optional imported enhancement not understood but ignorable by schema rules

### Pass/validation stage

Fatal:

- pass invariant broken such that renderer assumptions would be false
- ownership/identity model contradiction

Degradable:

- isolated unsupported symbol/member omitted

## Diagnostic code families

Suggested families:

- `PARSE####`
- `INST####`
- `META####`
- `PASS####`
- `VALID####`
- `RUNTIME####`

Exact numbering can be assigned during implementation, but the family prefix should encode the stage.

## Reporting rules

- diagnostics are deterministic in ordering
- same root cause should not spam duplicate messages across layers
- degraded symbols should clearly say whether they were omitted, rewritten, or downgraded
- import/version errors should name both producer and consumer identities

## Runtime-boundary failures

Some failures appear only at runtime boundaries and still need a design contract:

- callback exception crosses native boundary
- runtime support contract mismatch detected during module load
- native callback release contract violated

These are not compile-time diagnostics, but they should still map to named failure classes with clear behavior.

## Principle

If the generator cannot produce a coherent, importable, identity-safe module, it must fail loudly.

If it can still produce a coherent module after dropping or rewriting an isolated unsupported edge, it may degrade — but only with explicit diagnostics.

---

← [[13-rewrite-strategy]] | [[15-compatibility-policy]] →
