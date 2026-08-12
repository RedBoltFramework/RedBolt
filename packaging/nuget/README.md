# RedBolt NuGet packages

Create packages for the platforms, architectures, and modules selected by the
normal RedBolt build configuration:

```sh
dotnet run --file packaging/nuget/package.cs -- create
```

Use `--no-build` to package existing Release outputs. Packages and generated
staging files are written below `build/package/nuget/`. The version is derived
from Git: an exact `vX.Y.Z` tag produces `X.Y.Z`; otherwise packaging produces
the next patch development version, or `0.1.0-dev.<height>+<sha>` before the
first tag.

Package dependencies and folded binding assemblies are derived from each
module's evaluated `ProjectReference` and `PackageReference` items. Run
`dotnet run --file packaging/nuget/package.cs -- validate` to build every
enabled desktop sample as an isolated package-only consumer. Android and Web
samples are validated by their platform builds.

## Consumption

Engine projects must set one `RuntimeIdentifier` and reference the runtime
facade for each top-level module they use. For example:

```xml
<PropertyGroup>
  <RuntimeIdentifier>linux-x64</RuntimeIdentifier>
</PropertyGroup>
<ItemGroup>
  <PackageReference Include="RedBolt.PhysicsJolt.Runtime.linux-x64" Version="VERSION" />
</ItemGroup>
```

The runtime facade brings in the matching managed module and its native and
module-runtime dependencies. Managed package build targets select the assembly
compiled for the requested RID.

`RedBolt.CoreData`, `RedBolt.Data`, and `RedBolt.EditorData` copy their resource
directories to build output. The Editor package depends on CoreData and
EditorData only.

Package-reference tools expose these MSBuild properties after build:

- `RedBoltEditorPath`
- `RedBoltPackageToolPath`

SharpCpp is provided as reusable library packages. `SharpCpp.Support` also
exports `SharpCppIncludeRoot`, which points to its native support headers.
