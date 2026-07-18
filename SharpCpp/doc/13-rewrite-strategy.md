# 13 — Rewrite Strategy

V2 should be implemented as a **new, independent project line**, not as an in-place refactor of the existing generator.

That does not mean starting from zero. Reuse from v1 is encouraged where it helps, but reuse must happen by **copy/adapt/re-own**, not by keeping v2 structurally dependent on v1 code.

## Core decision

During the rewrite, v1 and v2 coexist.

- v1 remains buildable and shippable
- v2 evolves behind separate projects/targets/packages/output roots
- moving a piece from v1 into v2 does not create a long-lived source dependency from v2 back to v1

The rule is simple:

> v2 may learn from v1 and copy from v1, but it must not be architecturally trapped behind v1.

## What independence means

Independence requires all of the following:

1. distinct managed project identities during the rewrite
2. distinct CMake target families during the rewrite
3. distinct generated output roots
4. distinct support-runtime compatibility contract
5. no source/project reference from v2 core layers to v1 assemblies/projects

If v2 cannot build after deleting a local checkout of v1 source projects, it is not truly independent.

## Reuse policy

### Allowed reuse

Reuse by copy/adapt is good when the existing piece already matches the v2 direction or is mechanically useful.

Typical good candidates:

- small utility primitives
- deterministic text emitters like `CodeWriter`
- diagnostics record shapes
- stable metadata serialization helpers
- native/runtime helper algorithms with clear contracts
- targeted pass logic whose semantics survive into v2 after simplification
- well-tested support-runtime behaviors such as instance-cache algorithms

### Disallowed reuse shapes

Do **not** reuse these by wiring v2 directly to them:

- monolithic v1 core project references
- renderer feature-branching logic as a dependency surface
- v1 configuration object model (`ConfiguredTypeBinding`-style semantics)
- v1 metadata schema as an unversioned implicit contract
- shared mutable generated output directories
- shared assembly/target/package identities during parallel development

### Copy/re-own rule

When reusing a piece from v1:

1. copy it into the v2 project boundary
2. simplify/adapt it to v2 contracts
3. make v2 the owner of future changes there
4. do not keep a hidden requirement that fixes must still land in v1 first

Optional but recommended: leave a short provenance note in code comments or commit message when a large chunk was copied from v1.

## Parallel naming strategy

While both lines coexist, v2 names must stay distinct.

Examples:

- managed projects: `SharpCpp2.*` or `SharpCpp.V2.*`
- CMake targets: `sharpcpp2-*`
- generated roots: `${CMAKE_BINARY_DIR}/generated-v2/...`
- package/export namespaces: `SharpCpp2::...`

Exact spelling may change, but collision with v1 names is not allowed during migration.

## Recommended implementation order

### Phase 0: Skeleton

- create independent v2 projects/targets
- establish output roots, package identities, and support-runtime version line
- add empty metadata and rendering smoke tests

### Phase 1: Model + metadata

- implement binding model
- implement metadata schema/versioning
- implement deterministic export/import core

### Phase 2: Parsing

- implement phase 1 API parse
- implement phase 2 marshaller instantiation
- implement closure hashing / incremental inputs

### Phase 3: First vertical slice

Support a narrow but complete slice end-to-end:

- primitive/value parameters
- simple handle returns
- metadata export
- managed/native compile
- one E2E case

Do not broaden feature surface before this slice works.

### Phase 4: Cross-module composition

- import upstream metadata
- suppress duplicate emission
- reference upstream managed API
- validate downstream native support requirements

### Phase 5: Cross-cutting hard parts

- ownership/lifetime model
- instance caching
- polymorphic wrapping
- callbacks/directors

### Phase 6: Breadth expansion

- strings
- containers
- optionals/pairs/tuples
- fields/properties/operators
- generic instantiation families

### Phase 7: Consumer adoption

- rbfx-specific composition
- package/install surface
- migration of selected real consumers

## Migration rule: vertical slices over blanket porting

Do not port the whole old generator surface first and clean it up later.

Preferred approach:

- choose one feature slice
- implement it against v2 contracts
- prove it with parsing + rendering + metadata + E2E
- then widen outward

That keeps old accidental complexity from being copied wholesale.

## When to copy versus redesign

### Copy first

Use copy-first when the old implementation is:

- local and well-bounded
- already contract-compatible
- test-backed
- not entangled with deprecated v1 configuration models

### Redesign first

Redesign first when the old implementation is:

- coupled to renderer feature detection
- coupled to deprecated config objects
- dependent on v1-specific metadata/state assumptions
- hard to separate from unrelated policy

## Success criteria for the rewrite approach

The rewrite approach is working if:

1. v2 build graph can stand alone
2. v2 can copy useful code without inheriting v1 layering problems
3. feature work lands mostly inside the correct v2 layer
4. v1 can continue shipping while v2 grows
5. cross-module import works before the full feature matrix is complete

## Anti-goals

- do not make v2 a thin façade over v1
- do not dual-write the same semantic feature into both architectures unless required for shipping
- do not share project identities or generated output roots between v1 and v2
- do not let reuse pressure reintroduce the architectural seams v2 is trying to remove

---

← [[12-generated-output-and-module-api]] | [[14-diagnostics-and-failure-model]] →
