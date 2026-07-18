# SharpCpp — Agent Guide

## 1. Scope

This repository is the clean restart line described in `doc/`.

- `doc/` is authoritative for architecture and sequencing.
- Reuse from `../SharpCpp` is allowed by copy/adapt/re-own only.

## 2. Working Rules

- Think before editing. State assumptions when they matter.
- Prefer the smallest change that moves the current phase forward.
- Keep layering clean: `Model -> Metadata -> Parsing/Pipeline/Rendering -> Tool`; `Support` stays runtime-side.
- Do not broaden feature surface before the current slice is coherent.
- **DO NOT** add type-specific special cases to the core ever. All type-specific behaviors are configured through C++ and C# marshallers only.
- **Tooling preference (hierarchy for codebase exploration/editing — applies to ALL agents with tool access)**:
  1. **jcodemunch** (first resort) — `search_symbols`, `get_symbol_source`, `get_call_hierarchy`, `get_blast_radius`, `get_file_outline`, `get_dependency_graph`, `find_references`, `check_references`, `get_ranked_context`. Use these for symbol lookup, call chain tracing, reference checking, and dependency analysis BEFORE falling back to text tools.
  2. **ast_grep** (structural search/replace) — `ast_grep_search` / `ast_grep_replace` for AST-aware pattern matching and rewriting. Use when jcodemunch is too heavy (single simple pattern) or when you need to rewrite matching code blocks.
  3. **glob + grep + read** (fallback) — use only when jcodemunch can't answer the question (e.g. searching string literals, comments, config values) or when you already know the exact file and line.

## 3. Current Implementation Phase

This repo is in bootstrap mode.

Current priorities:

1. establish independent project/build identities
2. land model + metadata foundations
3. prove narrow vertical slices before expanding breadth

Follow `PROGRESS.md` for active tasks, next steps, and local decisions.

## 4. Required Verification

Until parser/renderers/E2E exist, completion means:

1. relevant unit tests added or updated
2. `dotnet build SharpCpp.slnx --configuration Debug`
3. `dotnet test SharpCpp.slnx --configuration Debug --no-build`
4. `PROGRESS.md` updated
5. git commit created (user request is not required)

The solution build owns generator ordering and invokes the auxiliary native CMake aggregate through `SharpCpp.Native`.

## 5. Project Shape

Target layout follows `doc/11-project-shape-blueprint.md`.

- `src/SharpCpp.Model/` - binding model, diagnostics, utilities
- `src/SharpCpp.Metadata/` - metadata schema/import/export
- `src/SharpCpp.Parsing/` - parser + marshaller instantiation
- `src/SharpCpp.Pipeline/` - passes and scheduling
- `src/SharpCpp.Rendering/` - renderers
- `src/SharpCpp.Tool/` - CLI/composition entrypoint
- `src/SharpCpp.Support/` - managed runtime support
- `include/SharpCpp/` - native support headers
- `tests/` - unit/golden/E2E coverage by layer

## 6. Naming Rules

- Managed assemblies use `SharpCpp.*` identities.
- CMake targets use `sharpcpp-*` targets.
- Generated outputs belong under the build tree, not the source tree.

## 7. Reuse Policy

Good copy candidates:

- deterministic utilities such as `CodeWriter`
- diagnostics record shapes
- stable metadata serialization helpers
- small bounded support/runtime helpers

## 8. Subagent Delegation Rules

Dispatch aggressively. The cost of explaining to a subagent is almost always less than the cost of doing the work yourself and losing parallel throughput.

### 8.1 Exploration (`@explorer`)

**Delegate when:**
- You need to find all call sites of a function or all usages of a pattern
- You need the structure/layout of a subsystem before planning
- You need to trace a call chain across multiple files
- You need several independent queries in parallel

**Batch queries:** Send 2-4 focused queries in parallel rather than one broad sweep. E.g. "find all helpers in class X" + "find all call sites of Y" + "find all files matching pattern Z" as three parallel tasks, not one serial read.

**Tool preference for exploration:**
- Use `search_symbols` (jcodemunch) for finding classes, methods, types — it understands symbol identity, not text
- Use `get_call_hierarchy` / `get_dependency_graph` / `get_blast_radius` before falling back to text greps
- Use `get_file_outline` to understand a file's structure without reading its full contents
- Use `ast_grep_search` for AST-aware pattern matching (e.g. "all methods taking a RecordBinding parameter")

**Don't delegate when:**
- You already know the exact file and line number — just read it directly
- A single grep/glob will answer the question faster than the dispatch overhead

### 8.2 Research (`@librarian`)

**Delegate when:**
- API documentation for an external library is needed
- Version-specific behavior or changelog info
- Fixing a bug where latest docs/patterns might help
- Complex configuration or usage patterns

### 8.3 Code Review / Architecture (`@oracle`)

**Delegate when:**
- Before starting implementation: review the plan for gaps and risks
- After implementation: review correctness before the final verification pass
- A bug survives two fix attempts
- The approach has architectural trade-offs (performance vs complexity, etc.)
- Ask explicitly for simplify/readability feedback in every review

**Don't skip:** Every non-trivial feature should have an oracle review of the plan and an oracle review of the implementation before commit.

### 8.4 Bounded Implementation (`@fixer`)

**Delegate when:**
- The plan is clear and reviewed (by oracle or by you)
- The change spans multiple files with mechanical edits
- The work can be scoped to a single layer (e.g. "implement the rendering for feature X following this spec")

**Don't delegate when:**
- The task requires design decisions better made by you
- The work is trivially small (one file, <20 lines)
- The spec is still in flux

**Hard rule:** If the implementation would take you more than 50 lines of code across >1 file, dispatch `@fixer` instead of writing it yourself. This is the most common mistake — context looks cheap when you're already deep in it, but it blocks parallel work and slows feature throughput.

### 8.5 UI/UX (`@designer`)

Delegate all user-facing visual/interaction work. Never implement UI yourself.

### 8.6 Parallel Dispatch Pattern

For a typical feature:
```
Phase 1: @explorer (parallel: explore model + explore parser + explore renderer)
Phase 2: plan + @oracle review
Phase 3: @fixer (parallel: model changes + parsing changes + test fixture)
Phase 4: @oracle review of implementation
Phase 5: @fixer (fixes from review)
Phase 6: verify + commit (you)
```

Overlap independent work aggressively. If Phase 3 model changes don't depend on parsing insights, they can run in parallel.

### 8.7 When NOT to delegate

- Trivial answers you can give directly
- Single-file edits under 20 lines
- Final synthesis and commit
- Coordination between specialist outputs (reconcile results, resolve conflicts)
- The dispatch overhead would exceed the work

## 9. Communication

- Be direct.
- Surface blockers early.
- Before asking the user for input, send a notification with `./make.sh notify "Input needed: <short reason>"`; do this immediately before the question so the notification corresponds to a real waiting point.
- Before wrapping up the task, send a notification with `./make.sh notify "Task done: <short description>"`; do this immediately before finishing the task.
- Record decisions and unfinished work in `PROGRESS.md` before stopping.

## 10. Repository Map

A full codemap is available at `codemap.md` in the project root.

Before working on any task, read `codemap.md` to understand:
- Project architecture and entry points
- Directory responsibilities and design patterns
- Data flow and integration points between modules

For deep work on a specific folder, also read that folder's `codemap.md`.
