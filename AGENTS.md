# ==========================================================================
# RedBolt Port
# ==========================================================================
# Read this ENTIRE file before doing anything. Follow it literally.
# Do exactly what is written. Do not invent steps. Do not skip steps.
# ==========================================================================

## 1. WHAT YOU ARE DOING

You are porting the rbfx C++ game engine to C#, one class at a time.
The port must be 1:1 (as close as possible) to the original C++ code.

- Original C++ source lives in: /home/rk/Desktop/src/rbfx/rbfx
- Your C# target project is: /home/rk/Desktop/src/rbfx/RedBolt

"1:1" means: same classes, same methods, same fields, same order, same logic.
You may only change things that this document explicitly allows you to change.

If you see any existing C# code that deviates from the C++ original and is NOT
on the "ALLOWED DEVIATIONS" list in section 8, you MUST fix it to match C++.

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
    f. Implement copy-constructors as `ICloneable.Clone()`.
    g. Match member access (do not use `internal` for ported functions ever).

STEP 4 - Build and test (see section 6). Do not move on until it is GREEN.

STEP 5 - Update status of ported items in `out/PORTING_CHECKLIST.md`. If completed,
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

## 6. BUILD AND VERIFY (run these commands)

Build:
  cmake --preset dev
  cmake --build --preset Debug --target restore
  cmake --build --preset Debug --target RedBolt -- -j16

Use `-j16` for parallel builds.

Run the sample and capture a screenshot:
  rm -f /tmp/screenshot.png
  out/build/dev/bin/RedBolt.Sample.StaticScene --timeout 2 --screenshot-on-exit /tmp/screenshot.png

Run the tests (adjust the filter to your area):
  dotnet test tests/RedBolt.Tests/RedBolt.Tests.csproj --configuration Debug

(Optional) Check the screenshot at /tmp/screenshot.png shows a correct, non-black scene.

The build output binaries are in:
  /home/rk/Desktop/src/rbfx/RedBolt/out/build/dev/bin/Debug

## 7. THINGS YOU MUST ALWAYS DO

- Port EVERY function and method, even ones that look useless or redundant.
- Port EVERY code comment if such exists.
- Port EVERY doxygen documentation comment as C# xml documentation comment.
- Port C++ code as is, no changes to the core logic.
- PORT C++ destructors as `Dispose()`.
- Preserve C++ code layout.
- Keep the code building at all times. Small working slices, not big broken ones.
- Add or update focused unit tests when you change behavior.
- After ANY change: build, run tests, and run the StaticScene sample.
- After finishing a slice: commit a coherent checkpoint.
- If the current API cannot express a piece of C++ yet:
    - Add the C++ version as a C# COMMENT with a `// TODO: port` note,
      so it is ported later. Do not silently drop it.
- Use the global Context pattern:
    - Set it with Context.SetInstance(ctx).
    - Read it with Context.Instance.
    - Do NOT pass Context through constructors.
- Copy necessary dependencies into extern/thirdparty and wire them into build system.

## 8. THINGS YOU MUST NEVER DO

- NEVER invent your own code to plug gaps -> ALWAYS just port C++ code.
- NEVER invent your own defensive code only because you think it is better -> ALWAYS just port C++ code.
- NEVER introduce your own comments outside of allowed cases.
- NEVER rework code or adjust execution flow.
- NEVER write manual/hand-authored native bindings.
- NEVER add hand-written C/C++ facade headers, C ABI forwarding functions,
  P/Invoke declarations, or C# wrapper classes for native dependency APIs.
- NEVER take shortcuts that make C# diverge from C++ logic (except section 9).
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
- SubscribeToEvent / SendEvent are upgraded to the new struct-based event format.
- CommandLineParser is used instead of CLI11.
- WorkQueue is rebuilt using a thread-pool.
- Minor API tweaks are allowed ONLY to avoid GC churn.
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
- C++ `friend` has no C# equivalent. A member that C++ makes `protected`/`private` but reaches
  from a `friend` class is mapped to `internal` (or `protected internal` when it is also
  `virtual`/overridden). This is the required, faithful mapping — NOT an `internal`-visibility
  violation of section 3. A member that is `public` in C++ must still be `public` in C#.
- Built-in rbfx UI (`UI`, `Window`, `Text`, `Sprite`, and related controls/subsystems) is not ported.
- In samples, replace every use of the built-in rbfx UI with Dear ImGui equivalents.
- All ported samples are windowed; do not preserve fullscreen defaults from the C++ sample when configuring the managed sample.
- All ported samples use `MM_FREE` memory mode unless a sample has a documented sample-specific reason to require another mode.

Anything not on this list must match C++.

## 11. SUCCESS CRITERIA (all must be true)

- [ ] The project builds with no errors.
- [ ] All tests pass.
- [ ] The C# class outline matches the C++ class outline as closely as possible.
- [ ] The C# method bodies match the C++ method bodies as closely as possible.
- [ ] The RedBolt.Sample.StaticScene sample runs and shows a correct scene.
- [ ] Optional: The StaticScene sample does not allocate GC memory continuously
      (verify with dotnet-trace: steady-state allocations should be flat).
- [ ] All tasks from the goal section are completed.

## 12. IF YOU ARE UNSURE

- Re-read the C++ source with jcodemunch-mcp. The C++ code is the source of truth.
- Match the C++ code. When in doubt, copy the C++ structure and logic.
- Do not guess at runtime behavior. Use debug-mcp or apitrace to confirm.
- Prompt user to clarify deviations
  - Record agreed deviations in AGENTS.md file
