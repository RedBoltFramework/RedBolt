# RedBolt

RedBolt is a C# port of rbfx. The current bootstrap stage validates native dependency builds and automated SharpCpp binding generation described in `doc/`.

Contributor and coding-session rules now live in `AGENTS.md`.

## Build

```bash
cmake --preset dev
cmake --build --preset Debug --target redbolt-csharp
```

The managed build verifies the native dependency and binding-generation bootstrap, then builds the RedBolt managed projects and samples.

For architecture notes, porting strategy, and binding details, see `doc/`.
