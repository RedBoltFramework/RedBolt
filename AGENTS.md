# ==========================================================================
# RedBolt Port
# ==========================================================================
# Read this ENTIRE file before doing anything. Follow it literally.
# Do exactly what is written. Do not invent steps. Do not skip steps.
# ==========================================================================

## 0. VOCABULARY

Reasoning and response format is defined by `caveman` skill.

## 1. WHAT YOU ARE DOING

You are porting the rbfx C++ game engine to C#, one class at a time.
The port must be 1:1 (as close as possible) to the original C++ code.

- Original C++ source lives in: /home/rk/Desktop/src/rbfx/rbfx
- Your C# target project is: /home/rk/Desktop/src/rbfx/RedBolt

"1:1" means: same classes, same methods, same fields, same order, same logic.
You may only change things that this document explicitly allows you to change.

### SOURCE-OF-TRUTH AND DIVERGENCE RULE

Before editing C# code, select exactly one of the following work modes. The
selected mode determines the source of truth and the changes that are allowed.

#### Initial direct port

The rbfx C++ implementation is the source of truth. Establish the corresponding
C++ declarations, complete method bodies, and relevant call paths; identify the
C# deviation; then copy the C++ structure, ordering, control flow, state changes,
and lifetime behavior using the required mappings and the allowed deviations in
`doc/ALLOWED_DEVIATIONS.md`. If no corresponding C++ implementation can be found,
stop and search the rbfx repository again before asking the user.

#### Unmodified feature investigation

The rbfx C++ implementation and call path are the source of truth. Diagnose the
concrete RedBolt deviation and port that behavior. An issue is not permission to
improve, redesign, simplify, harden, or work around the feature. If the C# code
already matches C++, continue tracing the C++ call path until the actual missing
or divergent port is found.

#### Intentional RedBolt development

The project requirements and existing RedBolt design are the source of truth.
Define and preserve the desired RedBolt behavior. Consult C++ for context,
compatibility, and regression analysis, but do not treat it as a replacement
specification or overwrite RedBolt behavior merely to restore parity.

Once RedBolt deliberately develops its own behavior in an area, that behavior
becomes part of the RedBolt target. It must not later be treated as a porting
bug or replaced with C++ code solely because the implementations differ.
Existing C# deviations therefore require correction only when the area remains
an initial direct port or is an unmodified feature being diagnosed.

## 2. TOOLS YOU MUST USE

- ast_search: use this to search BOTH rbfx and RedBolt C++ and C# code.
- dotnet-trace: (optional) use this to check for GC allocations in the running sample.
- debug-mcp: use this to debug crashes and wrong behavior at runtime.
  - Do NOT pass a `--timeout` argument to debug-mcp.
- apitrace: use this to debug rendering / visual bugs.

## 3. THE WORK LOOP (do this for EACH class)

Do these steps IN ORDER. Finish one step fully before starting the next.

STEP 0 - Preparation
  - Re-read @AGENTS.md to refresh your memory.

STEP 1 - Classify and investigate the work
  - Select the applicable work mode in the SOURCE-OF-TRUTH AND DIVERGENCE RULE.
  - Follow that mode's investigation requirements before editing C#.

STEP 2 - Match the class shape
  - Make sure the C# class has the SAME base classes / interfaces.
  - Make sure the C# class exists in a matching namespace/file location.

STEP 3 - Match every member, one by one (initial direct port)
  For EACH method and field in the C++ outline:
    a. Confirm the C# class has that same member.
       - If it is missing: ADD it.
       - If it is extra (not in C++ and not an allowed deviation): REMOVE it.
    b. Put members in the SAME ORDER as the C++ file. Reorder if needed.
    c. Rename to C# conventions (see section 4).
    d. Match the C# type to the C++ type as closely as possible (see section 5).
    e. Match the method body logic line-by-line to the C++ body.
    f. Audit every construction, copy, assignment, temporary, and destruction
       operation using the language-semantics rules in section 5A.
    g. Implement copy-constructors as `ICloneable.Clone()` and port every call
       site that invokes C++ copy construction with an actual independent clone.
    h. Match member access (do not use `internal` for ported functions ever).

STEP 3A - Fixes discovered while building or testing
  If a build, test, sample, debugger, or trace reveals a problem:
    a. STOP editing the C# implementation and apply the rules of the selected
       work mode.
    b. Do not use a symptom as permission to change the source of truth or to
       create an alternate execution path.

STEP 4 - Build and test (see section 6). Do not move on until it is GREEN.

STEP 5 - Update status of ported items in `PORTING_CHECKLIST.md`. If completed,
mark them as such. If incomplete, mark them as such and clarify what is missing.
Make a note of any accepted deviations.

STEP 6 - Commit the completed slice as a coherent checkpoint.

## 4. NAMING RULES (follow exactly)

- Classes: PascalCase          (e.g. RenderPipeline)
- Methods: PascalCase          (e.g. GetRenderPath)
- Properties: PascalCase       (e.g. IsEnabled)
- Public fields: PascalCase    (e.g. FrameCount)
- Private fields: _camelCase   with leading underscore (e.g. _frameCount)
- Local variables: camelCase   (e.g. frameCount)
- Method parameters: camelCase (e.g. renderPath)
- Constants: PascalCase

## 4A. C# FILE HEADERS (follow exactly)

Keep leading copyright and license comments before the namespace declaration.
Order namespace-bearing C# file headers as follows:

1. File-scoped namespace declaration.
2. `System.*` and `Microsoft.*` using directives.
3. Package using directives, including NuGet and `extern/` dependencies such as SharpCpp.
4. `RedBolt.*` using directives.

All non-global using directives must be namespace-local. Alphabetize each group
and separate adjacent groups with one empty line. Classify aliases by their
target namespace. Separate the namespace declaration, using directives, and code
with one empty line. Files containing only global using directives remain
namespace-less because C# forbids global using directives inside a namespace.

## 5. TYPE MAPPING RULES

- Match the C++ type as closely as C# allows.
- C++ getter/setter PAIRS may become a C# property, BUT the getter/setter
  body logic must still match the C++ code exactly.
- DO use nullable reference types (annotate with `?`); they are explicitly allowed and
  are the required mapping for C++ pointers / optional references.
- Prefer plain C# constructs over clever ones.
- A single value that C++ exposes as a getter should be a C# property,
  not a `GetX()` method, UNLESS matching C++ shape is clearer.

## 5A. C++ / C# LANGUAGE SEMANTICS AND LIFETIME (NON-NEGOTIABLE)

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

## 6. BUILD AND VERIFY (run these commands)

Build:
  git submodule update --init --recursive
  dotnet build RedBolt.slnx --configuration Debug

CMake is auxiliary and is invoked by C# build targets. Do not call CMake directly
for normal build or verification; build through `dotnet`, `.slnx`, or `.csproj`.

Run the most relevant rendering sample for the changed area and capture a screenshot.
Use MultipleViewports when no more relevant sample exists:
  rm -f /tmp/screenshot.png
  build/bin/Debug/samples/RedBolt.Sample.MultipleViewports --timeout 2 --screenshot-on-exit /tmp/screenshot.png

Run the tests (adjust the filter to your area):
  dotnet test src/RedBolt/Tests/RedBolt.Tests.csproj --configuration Debug

Review the captured screenshot as a success gate. The relevant scene must render correctly and must not be black or otherwise visibly broken.

Shared desktop managed libraries are in:
  /home/rk/Desktop/src/rbfx/RedBolt/build/bin/Debug

Desktop native libraries are in:
  /home/rk/Desktop/src/rbfx/RedBolt/build/bin/Debug/runtimes/linux-x64/native

Desktop sample executables and sample-specific files are in:
  /home/rk/Desktop/src/rbfx/RedBolt/build/bin/Debug/samples

Test output binaries are in:
  /home/rk/Desktop/src/rbfx/RedBolt/build/bin/Debug/tests

Web output is in:
  /home/rk/Desktop/src/rbfx/RedBolt/build/bin/Debug/web

C# build targets symlink runtime resource directories into the shared desktop
output directory. Tests consume these directories from their parent output.

The engine sets the process working directory to the managed executable directory.
Samples and tests that consume the staged resource directories use `CoreData;Data`
with resource prefix paths `.;..`.

## 7. THINGS YOU MUST ALWAYS DO

- Before changing implementation code for ANY reason, read the corresponding
  C++ implementation and use it to determine the change.
- For bug fixes, write down the exact C++-to-C# deviation being corrected. If
  there is no identified deviation, you are not ready to edit the C# code.
- Treat C++ lifecycle and ordering as required behavior, including frame
  boundaries, exit gates, teardown order, ownership, callbacks, and platform
  compile-time conditions.
- Port EVERY function and method, even ones that look useless or redundant.
- Port EVERY code comment if such exists.
- Port EVERY doxygen documentation comment as C# xml documentation comment.
- Port C++ code as is, no changes to the core logic.
- Port C++ destructors as `IDisposable.Dispose()`.
- Port C++ copy-constructors as `ICloneable.Clone()`.
- Port C++ `WeakPtr<>` ownership as `WeakReference<>`.
- Preserve C++ code layout.
- Commit only code that builds and works. In case this is a larger rework - it must be completed before committing.
- Add or update focused unit tests when you change behavior.
- After ANY change: build, run tests, and run the most relevant rendering sample with screenshot capture and review as a success gate. Use MultipleViewports when no more relevant sample exists.
- After finishing a slice: commit a coherent checkpoint.
- If the current API cannot express a piece of C++ yet:
    - Add the C++ version as a C# COMMENT with a `// TODO: port` note,
      so it is ported later. Do not silently drop it.
- Use the global Context pattern:
    - Set it with Context.SetInstance(ctx).
    - Read it with Context.Instance.
    - Do NOT pass Context through constructors.
- Add necessary dependencies under `extern/` and wire them into the build system.

## 8. THINGS YOU MUST NEVER DO

### In initial-port and unmodified-feature modes

- NEVER diagnose a symptom or design a fix from first principles. Diagnose by
  comparing against C++, then port the C++ behavior.
- NEVER use the existing C# implementation as the specification.
- NEVER add a helper, workaround, fallback, state flag, special case, or new
  execution path unless the same thing exists in C++ or is an allowed deviation.
- NEVER change implementation code before locating and reading its C++
  counterpart. Build errors and test failures do not waive this requirement.
- NEVER invent code to plug gaps or add defensive behavior; ALWAYS port the
  corresponding C++ behavior.

### In intentional RedBolt development mode

- NEVER replace intentional RedBolt development with C++ code solely to restore
  parity.

### In all modes

- NEVER update a test to bless behavior that differs from the intended target.
  Tests for intentional RedBolt development verify RedBolt behavior; tests for
  direct ports verify the faithfully ported C++ behavior.
- NEVER introduce your own comments outside of allowed cases.
- NEVER rework code or adjust execution flow.
- NEVER write manual/hand-authored native bindings.
- NEVER add hand-written C/C++ facade headers, C ABI forwarding functions,
  P/Invoke declarations, or C# wrapper classes for native dependency APIs.
- NEVER take shortcuts that make an initial direct port or unmodified feature diverge from C++ logic (except the allowed deviations in `doc/ALLOWED_DEVIATIONS.md`).
- NEVER port things C# already provides (see `doc/ALLOWED_DEVIATIONS.md`).
- NEVER allocate on the render hot path if the C++ code does not (avoid GC churn:
  no per-frame `new`, no boxing, no LINQ, no `params` arrays, no `Enum.HasFlag`).
- NEVER commit unless the build is green and tests pass.
- NEVER delegate tasks to subagents.
- NEVER insert absolute paths to code.
- NEVER get subsystems through context in classes that have `this.GetSubsystem<>()` available.

## 9. BINDINGS: HOW TO GET NATIVE ACCESS

Native dependencies (SDL, Diligent, Dear ImGui) are accessed through generated bindings.

- Use SharpCpp to AUTOMATICALLY generate the bindings.
- If the generated bindings are not enough, in this priority order:
    1. Fix our generator so it produces correct bindings. (PREFERRED)
    2. Fix SharpCpp so it produces correct bindings.
    3. Only as a LAST RESORT, add a small supporting C++ helper library that
       links to the C++ dependency. Avoid this unless 1 and 2 are impossible.
- Generated C# and C++ binding artifacts go in the CMake BUILD TREE,
  never in the source tree.

## 10. ALLOWED DEVIATIONS

The exhaustive list of allowed deviations is maintained in
`doc/ALLOWED_DEVIATIONS.md`. Anything not on that list must match C++.

Agreed project-specific naming deviation: C# engine types use folder-matching namespaces; the `Audio`, `Engine`, `Graphics`, and `Input` subsystem classes are named `AudioSystem`, `EngineSystem`, `GraphicsSystem`, and `InputSystem`; and the render pipeline component class is named `RenderPipelineComponent`.

Agreed project-specific namespace resolution deviation: namespace/type collisions
are resolved with namespace-local using directives.

Agreed project-specific reflection deviation: reflected C# objects and serializable attributes must declare generated `[Reflected]`, `[Serialized]`, and `[RemoveSerialized]` metadata instead of hand-written `RegisterObject`, `RegisterAttribute`, or `AddFactoryReflection` registration. Apply `[Serialized]` only to the instance field, property, getter, or setter that it serializes, never to a class. Infer the value type and available accessors from that member: a getter declaration names only a separate setter when required, and a setter declaration names only a separate getter when required. Field, auto-property, and simple-property backing-field initializers are their serialization defaults without `DefaultSource`; use a private named default source only when the target member cannot declare the required default. Inherited serialized members are copied from their reflected attribute source without forwarding declarations; use a private forwarding property only for a new subtype-specific serialized property whose storage/accessors are otherwise inherited. Preserve factory categories, non-factory types (`Factory = false`), exact attribute order inferred from serialized member declaration order, explicit inheritance source and insertion point, removals, defaults, modes, scope hints, enum names, custom accessors, and metadata configuration in those declarations. Named source members and forwarding properties used only by generated reflection must remain private implementation details. Library registration entry points register the generated assembly metadata; only the generator and its runtime registration infrastructure may use the low-level reflection APIs that materialize those declarations.

## 11. SUCCESS CRITERIA (all must be true)

- [ ] The project builds with no errors.
- [ ] All tests pass.
- [ ] The C# class outline matches the C++ class outline as closely as possible.
- [ ] The C# method bodies match the C++ method bodies as closely as possible.
- [ ] The most relevant rendering sample for the changed area runs and its reviewed screenshot shows a correct scene; use MultipleViewports when no more relevant sample exists.
- [ ] Optional: The relevant sample does not allocate GC memory continuously
      (verify with dotnet-trace: steady-state allocations should be flat).
- [ ] All tasks from the goal section are completed.

## 12. IF YOU ARE UNSURE

- Re-read the C++ source. The C++ code is the source of truth.
- Match the C++ code. When in doubt, copy the C++ structure and logic.
- If a proposed fix cannot be justified by pointing to corresponding C++ code,
  do not implement it.
- Do not guess at runtime behavior. Use debug-mcp or apitrace to confirm.
- Prompt user to clarify deviations
  - Record agreed deviations in AGENTS.md file
