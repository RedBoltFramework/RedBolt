# rbfx/Rbfx.Application/

## Responsibility

The application shell is the **executable entry point** that composes the generated rbfx SDK bindings with user-code bindings into a single runnable managed process. It is the final composition layer in the SharpCpp build graph, serving as both a validation target ("can we produce a working .NET executable that references all binding layers?") and a template for downstream rbfx projects that want to use SharpCpp-generated bindings.

This is deliberately a stub at present — it references both binding assemblies and exercises their type system (via `typeof` and delegate construction) without implementing a full game loop. Its architectural role is to prove that the full binding chain compiles and links end-to-end.

## Design Patterns

### Declaration-Only Consumption

`Program.Main` uses two C# idioms that force the JIT/compiler to materialize types from the referenced assemblies without executing substantive logic:

- `typeof(RotatingComponent)` — forces the runtime to resolve `RbfxUserCode.RotatingComponent` from the `Rbfx.UserCode.Bindings` assembly, proving the type was successfully generated and is accessible.
- `(Action<RotatingComponent>)TouchImportedComponent` — demonstrates that delegates over user-code types from the managed binding projection can be constructed without runtime errors, proving the interop chain (C# → p/invoke → native glue → C++) is coherent.

This pattern is **type-system smoke testing** — it catches resolution failures, missing assembly references, type-load exceptions, and marshaller configuration issues at the earliest possible point.

### Imported Type Reference Flow

The application demonstrates a two-hop type reference:

```
Rbfx.Application
  └── references Rbfx.UserCode.Bindings
        └── references SharpCpp.Rbfx.Bindings
              └── references SharpCpp.Support
```

The `RotatingComponent` class (defined in C++ as `RbfxUserCode::RotatingComponent`, which inherits from `Urho3D::LogicComponent`) is projected into C# as `RbfxUserCode.RotatingComponent` (from phase-2 generation) with `Urho3D.LogicComponent` as its base type (from phase-1 generation). The application imports only the user-code assembly and transitively gains access to both layers.

## Data & Control Flow

### Startup Sequence

```
Program.Main()
  ├── typeof(RotatingComponent)
  │     └── Triggers assembly load of Rbfx.UserCode.Bindings
  │           └── Triggers assembly load of SharpCpp.Rbfx.Bindings
  │                 └── Triggers assembly load of SharpCpp.Support
  │
  ├── (Action<RotatingComponent>)TouchImportedComponent
  │     └── Constructs delegate referencing RbfxUserCode.RotatingComponent
  │
  └── return 0
```

The `TouchImportedComponent` local function demonstrates calling methods on the projected type:

```
TouchImportedComponent(RotatingComponent component)
  ├── component.GetRotationSpeed()  →  returns float
  └── component.SetRotationSpeed(speed + 15.0f)
```

This exercises both Get and Set through the generated binding layer, confirming that property accessors and native-to-managed type marshalling function correctly.

## Integration Points

### Consumes

| Dependency | Role |
|---|---|
| `SharpCpp.Rbfx.Bindings` (csproj) | Generated managed bindings for the rbfx SDK engine API (`Urho3D.*` types) |
| `Rbfx.UserCode.Bindings` (csproj) | Generated managed bindings for user-authored C++ types (`RbfxUserCode.*` types) |

Both are consumed as standard .NET project references. Their generated/native
prerequisites flow transitively through the binding projects' build-only
reference to `SharpCpp.Native`.

### Build Integration

Defined entirely by ordinary `ProjectReference` items in
`Rbfx.Application.csproj`. The resulting build cascade is:

```
Rbfx.Application
  └── Rbfx.UserCode.Bindings
        ├── SharpCpp.Rbfx.Bindings
        └── SharpCpp.Native (build-only)
              └── sharpcpp-native (auxiliary CMake aggregate)
```

### Runtime Configuration

The `.csproj` disables `UseAppHost` on Linux (producing a DLL instead of a native executable), which is consistent with running via `dotnet` in the rbfx build environment. Nullable reference types are disabled to match the generated code style.

### Generated Output

None — the application is hand-authored source, not generated. It is the outermost consumer of generated artifacts.

### Future Evolution

When a full game loop is implemented, `Rbfx.Application` will:

1. Create an `Urho3D.Context` and `Urho3D.Engine` via generated bindings
2. Register `RotatingComponent` (and future user components) as factory types
3. Construct a scene, attach components, and enter the main loop
4. All type interactions will flow through the generated binding layer, serving as both a functional test and an SDK usage reference
