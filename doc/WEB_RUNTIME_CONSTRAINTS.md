# Web runtime constraints

Read when changing Web interop, threading, native linking, or managed value
storage. These are known failure modes; apply a workaround only to affected
code and verify against the toolchain in use. Preserve copy and allocation
semantics. Read `ALLOWED_DEVIATIONS.md` for accepted port mappings.

- Web Release AOT can corrupt large managed structs that contain reference
  fields when they cross struct return buffers, `Span.CopyTo`, or generic
  by-value collection APIs. Desktop and Web Debug builds may not reproduce the
  fault. For a C++ reference or in-place construction, use a managed `ref` and
  initialize the reserved destination storage field by field. Do not add an
  allocation. Verify the Release Web build.
- Web Release AOT can lower an interior `ref` into a managed list or array to a
  raw pointer. Do not keep this `ref` across a call that can allocate or trigger
  garbage collection. Keep the index and reacquire the `ref` after the call.
- Do not compile native Emscripten code with `-pthread` when the final .NET Web
  module has WebAssembly threads disabled. The mismatched thread-local storage
  model can corrupt native `thread_local` state without a crash. Use the same
  thread model for all native objects and the final module.
- Threaded Web builds require cross-origin isolation for `SharedArrayBuffer`.
  Serve them with `dotnet run --file scripts/serve_webassembly.cs -- --directory <output>`,
  or configure the deployment server to return
  `Cross-Origin-Opener-Policy: same-origin` and
  `Cross-Origin-Embedder-Policy: require-corp`.
- Emscripten enables its internal stack-first layout at `-O0` when the user
  does not set `GLOBAL_BASE`. This layout gives the threaded .NET runtime
  incorrect main-thread stack bounds and triggers a Mono assertion before the
  managed entry point. Keep an explicit nonzero `GLOBAL_BASE` when Web linking
  uses `-O0`.
- The threaded .NET Web runtime runs the managed entry point in a worker. SDL's
  Emscripten EGL path does not initialize the thread-local state used by
  `OFFSCREEN_FRAMEBUFFER` GL proxies. Create and bind the context through
  `emscripten_webgl_*` so Emscripten records the context owner and proxies GL
  calls to it.
- Browser APIs and JavaScript module state such as Web Audio belong to the
  browser thread. Access them from native worker code through
  `MAIN_THREAD_EM_ASM`, not plain `EM_ASM`.
- Threaded .NET rejects synchronous JavaScript-to-C# calls from the browser
  thread. Browser-driven frame and event callbacks must call `[JSExport]`
  methods that return `Task`, and JavaScript must await the resulting Promise.
  Return `Task.CompletedTask` when the managed operation is synchronous.
