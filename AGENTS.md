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

If you see any existing C# code that deviates from the C++ original and is NOT
on the "ALLOWED DEVIATIONS" list in section 10, you MUST fix it to match C++.

### NON-NEGOTIABLE SOURCE-OF-TRUTH RULE

The rbfx C++ implementation is the ONLY source of truth for implementation
decisions. C# code, tests, runtime symptoms, compiler errors, debugger output,
and personal judgment may reveal that something is wrong, but they MUST NEVER
be used to design the fix.

For EVERY implementation, bug fix, crash fix, rendering fix, test fix, API fix,
refactor, or cleanup, you MUST do all of the following BEFORE editing C# code:

1. Locate the corresponding rbfx C++ class, function, field, test, and call path.
2. Read the complete relevant C++ declarations and method bodies.
3. Identify exactly how the C# code deviates from the C++ code.
4. Copy the C++ structure, ordering, control flow, state changes, and lifetime
   behavior into C#, applying only the mappings and deviations allowed below.

If you cannot find a corresponding C++ implementation, STOP. Do not invent a
solution. Search the C++ repository again, inspect callers and related classes,
and ask the user only if the C++ source still cannot determine the behavior.

An issue is NEVER permission to improve, redesign, simplify, harden, or work
around the C# implementation. The only valid fix is a faithful port of what the
C++ implementation already does. A green test that deviates from C++ is wrong.

## 2. TOOLS YOU MUST USE

- jcodemunch-mcp: use this to read/search BOTH rbfx and RedBolt.
  - ALWAYS use jcodemunch-mcp to explore code.
  - DO NOT use plain grep, glob, find, cat, or Read for code exploration.
- dotnet-trace: (optional) use this to check for GC allocations in the running sample.
- debug-mcp: use this to debug crashes and wrong behavior at runtime.
  - Do NOT pass a `--timeout` argument to debug-mcp.
- apitrace: use this to debug rendering / visual bugs.

## 3. THE WORK LOOP (do this for EACH class)

Do these steps IN ORDER. Finish one step fully before starting the next.

STEP 0 - Preparation
  - Re-read @AGENTS.md to refresh your memory.
  - Update jcodemunch-mcp index of rbfx and RedBolt.

STEP 1 - Find the C++ class
  - Use jcodemunch-mcp to open the C++ class outline (all methods, fields, bases).
  - Write down: base classes, interfaces, every method, every field.
  - Open and read the COMPLETE C++ body of every method that may be added,
    changed, removed, debugged, or tested in this slice.
  - Trace the relevant C++ callers and callees whenever behavior depends on
    frame ordering, ownership, teardown, events, platform gates, or side effects.
  - DO NOT inspect the C# implementation and decide how it should work without
    first completing this C++ review.

STEP 2 - Match the class shape
  - Make sure the C# class has the SAME base classes / interfaces.
  - Make sure the C# class exists in a matching namespace/file location.

STEP 3 - Match every member, one by one
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
    a. STOP editing the C# implementation.
    b. Find and re-read the corresponding C++ implementation and call path.
    c. Compare C# against C++ and identify the concrete deviation.
    d. Fix ONLY that deviation by porting the C++ code.
    e. If C# already matches C++, continue tracing the C++ call path until the
       actual missing or divergent port is found.
    f. NEVER create a C#-only workaround, fallback, helper, state flag, special
       case, or alternate execution path to make the symptom disappear.

STEP 4 - Build and test (see section 6). Do not move on until it is GREEN.

STEP 5 - Update status of ported items in `PORTING_CHECKLIST.md`. If completed,
mark them as such. If incomplete, mark them as such and clarify what is missing.
Make a note of any accepted deviations.

## 4. NAMING RULES (follow exactly)

- Classes: PascalCase          (e.g. RenderPipeline)
- Methods: PascalCase          (e.g. GetRenderPath)
- Properties: PascalCase       (e.g. IsEnabled)
- Public fields: PascalCase    (e.g. FrameCount)
- Private fields: _camelCase   with leading underscore (e.g. _frameCount)
- Local variables: camelCase   (e.g. frameCount)
- Method parameters: camelCase (e.g. renderPath)
- Constants: PascalCase

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

Run the sample and capture a screenshot:
  rm -f /tmp/screenshot.png
  build/bin/Debug/samples/RedBolt.Sample.MultipleViewports --timeout 2 --screenshot-on-exit /tmp/screenshot.png

Run the tests (adjust the filter to your area):
  dotnet test tests/RedBolt.Tests/RedBolt.Tests.csproj --configuration Debug

(Optional) Check the screenshot at /tmp/screenshot.png shows a correct, non-black scene.

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
- After ANY change: build, run tests, and run the MultipleViewports sample.
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

- NEVER diagnose a symptom and design a C# fix from first principles. Diagnose
  by comparing against C++, then port the C++ behavior.
- NEVER use the existing C# implementation as the specification.
- NEVER add a helper, workaround, fallback, state flag, special case, or new
  execution path unless the same thing exists in C++ or is an allowed deviation.
- NEVER change implementation code before locating and reading its C++
  counterpart. Build errors and test failures do not waive this requirement.
- NEVER update a test to bless behavior that differs from C++. Tests must verify
  the faithfully ported C++ behavior, not preserve an invented C# API.
- NEVER invent your own code to plug gaps -> ALWAYS just port C++ code.
- NEVER invent your own defensive code only because you think it is better -> ALWAYS just port C++ code.
- NEVER introduce your own comments outside of allowed cases.
- NEVER rework code or adjust execution flow.
- NEVER write manual/hand-authored native bindings.
- NEVER add hand-written C/C++ facade headers, C ABI forwarding functions,
  P/Invoke declarations, or C# wrapper classes for native dependency APIs.
- NEVER take shortcuts that make C# diverge from C++ logic (except section 10).
- NEVER port things C# already provides (see section 10).
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

## 10. ALLOWED DEVIATIONS (only these)

These are the ONLY places C# is allowed to differ from C++:

- RefCounted / SharedPtr / WeakPtr are dropped ON PURPOSE. They must NOT exist.
- ResourceCache cannot observe external CLR references after the removal of intrusive reference counting. Its release methods therefore treat cached resources as releasable when `force` is false; managed references remain valid after cache eviction. `force` is retained for API parity and skips the native cross-type retry passes when true.
- SubscribeToEvent / SendEvent are upgraded to the new struct-based event format.
- CommandLineParser is used instead of CLI11.
- WorkQueue is rebuilt using a thread-pool.
- Managed platform/graphics composition uses one selected platform assembly and any supported graphics-backend assemblies. Shared desktop managed libraries use `build/bin/<configuration>`, native libraries use `build/bin/<configuration>/runtimes/<platform>-<architecture>/native`, desktop sample executables and sample-specific files use `build/bin/<configuration>/samples`, tests use `build/bin/<configuration>/tests`, Web output uses `build/bin/<configuration>/web`, and platform-specific behavior is delegated through runtime contracts.
- Native shared-library filenames use conventional platform-neutral names.
- Minor API tweaks are allowed ONLY to avoid GC churn.
- C# indices and sizes use `int`, including when the corresponding C++ type is unsigned.
- Prefer property syntax alone when it can faithfully represent a C++ member and its public accessors; do not add a private backing field only to wrap it in a public property. Private fields remain allowed when required by behavior or storage semantics.
- `IApproximateEquatable<T>` is a managed addition used to expose strongly typed epsilon-based equality across math value types and is to be kept.
- Do NOT re-port things C# already has, for example:
    - Threading primitives (locks, tasks, thread pools)
    - Math functions and constants
    - Standard containers / collections
    - String utilities
- Math type aliases are different, but used
  - `Vector[234]` -> `Vector[234]f`
  - `IntVector[234]` -> `Vector[234]i`
- `CString` is our addition and is to be kept, as well as related customizations of other APIs
- No URHO3D_OBJECT-related metadata code -> replaced by C# type system/reflection with `ObjectReflection` as a facade
- Object reflection and serializable attribute registration may be declared with C# `[Reflected]` and
  `[Serialized]` attributes and generated at compile time, including for RedBolt user assemblies.
  Field and property initializers are the primary source of serialization defaults; generated registration
  may use an explicitly named default source for method-based and custom accessors.
- C++ `friend` has no C# equivalent. A member that C++ makes `protected`/`private` but reaches
  from a `friend` class is mapped to `internal` (or `protected internal` when it is also
  `virtual`/overridden). This is the required, faithful mapping — NOT an `internal`-visibility
  violation of section 3. A member that is `public` in C++ must still be `public` in C#.
- Built-in rbfx UI (`UI`, `Window`, `Text`, `Sprite`, and related controls/subsystems) is not ported.
- In samples, replace every use of the built-in rbfx UI with Dear ImGui equivalents.
- All ported samples are windowed; do not preserve fullscreen defaults from the C++ sample when configuring the managed sample.
- The MultipleViewports sample may explicitly disable window maximization and configure default window dimensions so `--width` and `--height` control the actual window size.
- C#-only diagnostic flags may trigger a RenderDoc capture after a specified delay.
- The MultipleViewports sample may temporarily animate its camera forward when `REDBOLT_MULTIPLE_VIEWPORTS_ANIMATE_CAMERA=1` for automated rendering diagnostics.
- Engine construction sets the process working directory to the managed executable directory so CMake-staged resource-directory symlinks resolve consistently.
- All ported samples use `MouseMode.Free` with a visible cursor while idle. Free-fly camera controls follow rbfx: while the right mouse button is held, temporarily hide and lock the cursor in relative mode, then restore free mode and cursor visibility on release.
- All ported samples use `MM_FREE` memory mode unless a sample has a documented sample-specific reason to require another mode.
- WebAssembly frame pacing is controlled by the JavaScript `requestAnimationFrame` loop. Do not call `SDL_GL_SetSwapInterval` on WebAssembly because SDL's Emscripten implementation requires an Emscripten-owned main loop that the .NET host does not use.
- On WebAssembly, application window width and height default to the browser canvas dimensions in physical pixels before `Setup()`. `Setup()` may override either value. The Web shell follows rbfx: it rounds CSS canvas dimensions multiplied by the device pixel ratio, creates the SDL window without `SDL_WINDOW_ALLOW_HIGHDPI`, and resynchronizes canvas resolution on resize, fullscreen, and visibility changes.
- `SystemUI` is renamed to `DearImGui`.
- Calls to `GetSubsystem<T>()` in ported subsystem consumers assume that the requested subsystem is registered and return a non-null object. C# code may use a non-null field/reference and omit C++ null checks that only guard a missing subsystem.
- The optional Actions subsystem and compile-gated Actions reflection/integration members are excluded from the current completion scope.

Anything not on this list must match C++.

## 11. SUCCESS CRITERIA (all must be true)

- [ ] The project builds with no errors.
- [ ] All tests pass.
- [ ] The C# class outline matches the C++ class outline as closely as possible.
- [ ] The C# method bodies match the C++ method bodies as closely as possible.
- [ ] The RedBolt.Sample.MultipleViewports sample runs and shows a correct scene.
- [ ] Optional: The MultipleViewports sample does not allocate GC memory continuously
      (verify with dotnet-trace: steady-state allocations should be flat).
- [ ] All tasks from the goal section are completed.

## 12. IF YOU ARE UNSURE

- Re-read the C++ source with jcodemunch-mcp. The C++ code is the source of truth.
- Match the C++ code. When in doubt, copy the C++ structure and logic.
- If a proposed fix cannot be justified by pointing to corresponding C++ code,
  do not implement it.
- Do not guess at runtime behavior. Use debug-mcp or apitrace to confirm.
- Prompt user to clarify deviations
  - Record agreed deviations in AGENTS.md file
