# RedBolt Agent Notes

## Main goal

1:1 (or as close as possible) port of /home/rk/Desktop/src/rbfx/rbfx game engine to C#. We still use idiomatic C# and avoid non-portable things, but logic and structure should be as close to original as possible. No shortcuts!

**IMPORTANT**: If you observe deviations from the original engine - they must be fixed immediately!

## Project Snapshot

RedBolt is a C# port of rbfx. Native dependency access is generated through SharpCpp from clang-parsed SDL and Diligent headers; hand-authored engine code lives above that generated interop layer.

The current rendering effort is an incremental port of:

- `/home/rk/Desktop/src/rbfx/rbfx/Source/Urho3D/RenderAPI`
- `/home/rk/Desktop/src/rbfx/rbfx/Source/Urho3D/RenderPipeline`

The target is a full port of the graphics API and pipeline: all classes, behavior, and hookups should be ported unless the omitted piece is only C++ utility infrastructure that has no meaningful C# equivalent.

Do not try to transplant the full render stack at once. Land small coherent slices and keep them building, but each slice should move toward full rbfx parity rather than introducing long-lived custom shortcuts.

## Binding And Interop Rules

- Prefer idiomatic C# constructs.
- Avoid superfluous getters when a property is the natural API.
- No manual bindings.
- If bindings are not enough:
  - fix our generator
  - fix SharpCpp
- Do not add hand-authored C/C++ facade headers, C ABI forwarding functions, P/Invoke declarations, or C# binding-wrapper classes for dependency APIs.
- Generated C# and C++ binding artifacts belong in the CMake build tree, not in the source tree.

## Context And Event Model

- Use the managed global `Context` pattern rather than constructor-injecting context through the port.
- Set the active context with `Context.SetInstance(ctx)` and access it with `Context.Instance`.
- `Engine` owns the active `Context`, registers core subsystems during initialization, and clears the global instance on shutdown.
- Subsystem registration and lookup may be used concurrently.
- Named events are `StringHash`-based and are main-thread-only.
- When changing event behavior, add or update focused tests.

## Managed Type Metadata

- Do not port `URHO3D_OBJECT`-related static metadata code. Use the C# type system and reflection instead.

## Render Port Guidance

- Match rbfx resource formats and default behaviors whenever possible. Avoid RedBolt-only side formats if upstream rbfx already defines the asset/runtime contract.
- Prefer incremental managed ports that compile and are tested over broad partial translations.
- Native libraries are wrapped using our custom generators based on SharpCpp. Sometimes generated bindings are insufficient.
  - You can:
    - Fix SharpCpp to produce correct bindings (preferred way)
    - Add a custom supporting C++ library that links to C++ dependencies to offer helper code (best avoided, unless preferred way is impossible)


## Workflow Expectations

- Trim stale guesses, superseded notes, and completed low-value items from that file.
- Commit coherent checkpoints so progress is not stranded between sessions.
- Keep the sample build healthy while porting rendering.
- Always use jcodemunch to explore rbfx and RedBolt repositories, avoid using standard search/grep/glob tools.
- Use `apitrace` to triage rendering issues.
- Use `debug-mcp` (but without `--timeout`) to debug issues.

## Build And Verification

- Bootstrap / full managed build:

```bash
cmake --preset dev
cmake --build --preset Debug --target restore
cmake --build --preset Debug --target RedBolt
out/build/dev/bin/RedBolt.Sample.StaticScene --timeout 2 --screenshot-on-exit /tmp/screenshot.png
```

- The managed build should keep producing sample outputs in:
  - `/home/rk/Desktop/src/rbfx/RedBolt/out/build/dev/bin/Debug`
- Verify screenshots for valid visual output.

## Current Constraint

- Assume named-event dispatch is single-threaded on the main thread.
- Do not reintroduce locking around the event bus unless the threading model changes deliberately.
