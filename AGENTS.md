# RedBolt engine

## Source of truth

Infer the work mode from the task and project history. Ask only when unresolved
ambiguity materially changes the allowed implementation.

- Initial direct ports and investigations of unmodified features use upstream
  rbfx C++ behavior plus `doc/ALLOWED_DEVIATIONS.md` as the specification. Trace
  relevant declarations, bodies, and call paths before correcting a divergence.
  Matching local code does not prove a port is complete: investigate callers,
  lifetime, and integration when the observed behavior still differs.
- Intentional RedBolt development uses the requested behavior and existing
  RedBolt design as the specification. C++ is context for compatibility and
  regressions, not a reason to undo intentional behavior. Necessary changes to
  control flow, helpers, and explanatory comments are permitted within scope.

## Engineering constraints

- Preserve ownership, deterministic disposal, initialization and teardown order,
  frame boundaries, and callback behavior. Use [porting semantics](doc/PORTING_SEMANTICS.md)
  for C++ mappings, copies, borrowed wrappers, and lifetime changes.
- Preserve render hot-path allocation behavior: do not introduce per-frame
  allocations, boxing, LINQ, or other GC churn where the target has none.
- Use generated SharpCpp bindings. Do not hand-author native API wrappers,
  P/Invoke, or C ABI forwarding facades. Fix the dependency generator or generic
  SharpCpp support when bindings are insufficient. A supporting C++ helper is
  a last resort only when neither generator can express the required operation;
  document why and keep it narrowly scoped to that operation.
- Bind the complete dependency surface by default. Do not prune it with symbol
  allowlists. Derive binding semantics from AST/model facts, not rendered type
  names. Generic binding behavior belongs in SharpCpp; dependency-specific
  policy, including Dear ImGui enum normalization, belongs in its generator.
- Generated binding artifacts belong in the configured build tree.
- Use `Context.SetInstance`/`Context.Instance` rather than injecting Context via
  constructors. Prefer `this.GetSubsystem<>()` where available.
- Keep dependencies under `extern/` and integrated with the existing build.
  Preserve the shared infrastructure ownership specified by the monorepo.

## References by task

- [C# conventions](doc/CSHARP_CONVENTIONS.md): editing C# declarations and headers.
- [Allowed deviations](doc/ALLOWED_DEVIATIONS.md): port mappings, generated
  reflection metadata, and accepted engine-specific behavior. Record new accepted
  deviations there rather than appending them to this file.
- [Web runtime constraints](doc/WEB_RUNTIME_CONSTRAINTS.md): Web threading,
  linking, interop, or AOT value/lifetime bugs.
- [Build requirements](doc/BUILD_REQUIREMENTS.md): platform setup and build selection.
- [Workspace verification](../doc/VERIFICATION.md): selecting builds, tests,
  sample inspection, package checks, and coverage.

Use available search and debugging tools according to the question. Structural
queries may benefit from ast-grep; crashes may need a debugger; rendering bugs
may need apitrace; allocation investigations may need dotnet-trace. Missing
optional tools do not block an equivalent investigation. If using debug-mcp,
do not pass its unsupported `--timeout` option.

## Completion

Continue through implementation and applicable verification, fixing regressions
caused by the change. Tests must verify intended behavior, not bless a bug.
Port tasks must meet upstream fidelity and accepted mappings; intentional
features must meet the requested RedBolt behavior. Build and inspect only the
checks justified by the change, following workspace verification guidance.

Coherent local checkpoint commits remain authorized after relevant checks pass.
Do not commit an incomplete port, unrelated user changes, or an unresolved merge.
Delegate only when allowed by the active session, with bounded independent work
and one build owner; no mandatory specialist roles or repeated review stages.
