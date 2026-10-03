# RedBolt core

## Source of truth

- Resolve ambiguous work modes from the task and project history. Ask only when
  the ambiguity changes the allowed implementation.
- For direct ports and unmodified features, read the monorepo
  [C++ to C# porting skill](../.agent/skills/redbolt-porting/SKILL.md). Use
  upstream rbfx C++ behavior and `doc/ALLOWED_DEVIATIONS.md` as the
  specification.
- For intentional RedBolt development, use the requested behavior and existing
  RedBolt design. Treat C++ as compatibility and regression context; do not use
  it to undo intentional behavior. Change control flow, helpers, and comments
  within the task scope as needed.

## Engineering constraints

- Preserve ownership, deterministic disposal, initialization and teardown order,
  frame boundaries, and callback behavior. Use [porting semantics](doc/PORTING_SEMANTICS.md)
  for C++ mappings, copies, borrowed wrappers, and lifetime changes.
- Preserve render hot-path allocation behavior. Do not introduce per-frame
  allocations, boxing, LINQ, or other GC churn where the target has none.
- Use generated SharpCpp bindings. Do not hand-author native wrappers, P/Invoke,
  or C ABI forwarding facades. Fix the generator or generic SharpCpp support
  when bindings are insufficient. Add a supporting C++ helper only when neither
  generator can express the operation; document the reason and keep it narrow.
- Bind the complete dependency surface by default; do not prune it with symbol
  allowlists. Derive semantics from AST/model facts, not rendered type names.
  Put generic behavior in SharpCpp and dependency-specific policy, including
  Dear ImGui enum normalization, in its generator.
- Put generated binding artifacts in the configured build tree.
- Use `Context.SetInstance`/`Context.Instance` instead of constructor injection.
  Prefer `this.GetSubsystem<>()` where available.
- Keep dependencies under `extern/` and integrate them with the existing build.
  Preserve the monorepo's shared infrastructure ownership.

## References

- [C# conventions](doc/CSHARP_CONVENTIONS.md): C# declarations and headers.
- [Allowed deviations](doc/ALLOWED_DEVIATIONS.md): port mappings, generated
  reflection metadata, and accepted engine-specific behavior. Record new
  accepted deviations there instead of in this file.
- [Web runtime constraints](doc/WEB_RUNTIME_CONSTRAINTS.md): Web threading,
  linking, interop, and AOT value/lifetime bugs.
- [Build requirements](doc/BUILD_REQUIREMENTS.md): platform setup and build
  selection.
- [Workspace verification](../doc/VERIFICATION.md): builds, tests, samples,
  package checks, and coverage.
- Use ast-grep for structural queries, a debugger for crashes, apitrace for
  rendering bugs, and dotnet-trace for allocation investigations.
- Use equivalent tools when optional tools are unavailable.
- Do not pass the unsupported `--timeout` option to debug-mcp.

## Core build

- Use `RedBolt/RedBolt.slnx` for core-only iteration.
- Select a focused Linux/x64 build with:

  ```sh
  dotnet build RedBolt/RedBolt.slnx --configuration Debug --no-restore -maxcpucount \
    -p:RedBoltEnable=linux-x64 \
    -p:RedBoltTestsEnabled=false
  ```

## Completion

- Complete implementation and applicable verification; fix regressions caused by
  the change.
- Make tests verify intended behavior rather than bless a bug.
- Meet upstream fidelity and accepted mappings for port tasks. Meet requested
  RedBolt behavior for intentional features.
- Run only checks justified by the change, following workspace verification
  guidance.
- Create coherent local checkpoint commits after relevant checks pass. Do not
  commit incomplete ports, unrelated user changes, or unresolved merges.
- Delegate only within the active session's rules. Bound independent work and
  keep one build owner; specialist roles and repeated review stages are optional.
