# RedBolt

RedBolt is a C# port of rbfx. The current bootstrap stage validates native dependency builds and automated SharpCpp binding generation described in `doc/`.

Contributor and coding-session rules now live in `AGENTS.md`.

## Build

```bash
cmake --preset linux-gcc-x64-dll
cmake --build --preset linux-gcc-x64-dll-debug --target restore
cmake --build --preset linux-gcc-x64-dll-debug --target RedBolt
```

The managed build verifies the native dependency and binding-generation bootstrap, then builds `RedBolt` and dependent managed projects you select by target (for example, `RedBolt.Sample.StaticScene`).

For architecture notes, porting strategy, and binding details, see `doc/`.
