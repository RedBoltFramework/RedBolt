# RedBolt

RedBolt is a C# port of rbfx. Its C#-centric build drives managed compilation, native dependencies, generated SharpCpp bindings, desktop samples, tests, and the WebAssembly sample.

Contributor and coding-session rules now live in `AGENTS.md`.

## Build

```bash
git submodule update --init --recursive
dotnet build RedBolt.slnx --configuration Debug
```

All generated files and binaries are written below `build/`. CMake remains available for native-only diagnosis, but the solution is the supported full-build entry point and builds Linux and WebAssembly components in dependency order.

The test suite is maintained in the sibling `RedBolt.Tests` repository. When
that repository is cloned next to `RedBolt`, `RedBolt.slnx` includes its test
project automatically; without the sibling repository, the framework solution
still builds without tests.

For architecture notes, porting strategy, and binding details, see `doc/`.
