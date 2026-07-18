# src/SharpCpp.Tool/

## Responsibility

The **composition root** and **CLI entry point** of the SharpCpp binding generator. This layer owns:

- **Startup and dispatch**: Parses CLI arguments and routes to the appropriate command handler (`sample-metadata`, `generate-sample`).
- **Compilation construction**: Assembles a `BindingCompilation` (from the Model layer) by either hand-rolling sample data (`SampleCompilationFactory`) or driving real parsing via `ClangSharpHeaderParser` (Parsing layer).
- **Pipeline orchestration**: Invokes the bootstrap pipeline passes (`BindingCompilationBootstrapPipeline.Apply`) to normalize and decorate the model before rendering.
- **Output materialization**: Writes rendered C# source files and serialized metadata JSON to the filesystem.

This layer performs no domain logic of its own — it wires, invokes, and writes.

## Design

### Two command modes (both thin vertical slices for bootstrap validation)

1. **`sample-metadata`** — fully synthetic path:
   - `SampleCompilationFactory.CreateMetadataSample()` produces a `BindingCompilation` with hardcoded members (`SampleContext` record, `add_numbers` / `create_context` functions).
   - `BindingMetadataExporter.Export()` converts it to a `BindingMetadataDocument`.
   - `BindingMetadataSerializer.Serialize()` writes the metadata as JSON to stdout or a file.

2. **`generate-sample`** — semi-realistic path:
   - Discovers `.hpp` header files under `tests/SharpCpp.Tests.E2E/`.
   - Constructs Clang arguments (C++20, include paths for system headers, project `include/`, and fixture root).
   - Instantiates `ClangSharpHeaderParser` to parse each header into model members, accumulating them into a single `BindingCompilation`.
   - Runs `BindingCompilationBootstrapPipeline.Apply()` for required post-processing passes.
   - Invokes `BindingOutputRenderer.Render()` to produce a `RenderedFileCollection` of C# source.
   - Exports and serializes metadata alongside the rendered output.
   - Writes all artifacts into `--generated-root`.

### Key patterns

- **Static pipeline wiring**: All layer entry points are `static` classes/methods called directly — no DI container, no service locator. The tool explicitly sequences every step.
- **Option parsing via ad-hoc helpers**: `GetOptionValue` / `GetPathOption` provide minimal key-value extraction from `string[]`. No CLI framework dependency.
- **Filesystem path convention**: The `generate-sample` command derives most paths from `--project-root` (defaults to `Environment.CurrentDirectory`), avoiding hardcoded absolutes.

## Data & Control Flow

```
┌─ args[] ──► Program.Main()
│
├── "sample-metadata" ──► EmitSampleMetadata()
│   ├── SampleCompilationFactory.CreateMetadataSample()
│   │     └── returns BindingCompilation (Model)
│   ├── BindingMetadataExporter.Export(compilation, options)
│   │     └── returns BindingMetadataDocument (Metadata)
│   ├── BindingMetadataSerializer.Serialize(document)
│   │     └── returns JSON string
│   └── Console.Write / File.WriteAllText
│
├── "generate-sample" ──► GenerateSampleBindings()
│   ├── Resolve paths (--generated-root, --project-root, --file-stem)
│   ├── Discover *.hpp files under tests/SharpCpp.Tests.E2E/
│   ├── new ClangSharpHeaderParser()                      (Parsing)
│   ├── For each header:
│   │   └── parser.Parse(request) → members added to BindingCompilation
│   ├── BindingCompilationBootstrapPipeline.Apply(compilation, fileSet)
│   │                                                    (Pipeline)
│   ├── BindingOutputRenderer.Render(compilation, stem, nameResolver)
│   │     └── returns RenderedFileCollection              (Rendering)
│   ├── BindingMetadataExporter.Export + Serialize        (Metadata)
│   └── WriteToDirectory + File.WriteAllText              (filesystem)
│
└── unknown ──► PrintUsage() → exit code 1
```

**Sequence invariant**: `Parsing → Pipeline → Rendering → Metadata → Output`. The pipeline pass operates on the populated model before any rendering or export occurs.

## Integration Points

| Dependency | Layer | Usage in Tool |
|---|---|---|
| `SharpCpp.Model` | Model | `BindingCompilation`, `RecordBinding`, `FunctionBinding`, `BindingType`, `ParameterBinding` — the AST that every operation reads/writes |
| `SharpCpp.Parsing` | Parsing | `ClangSharpHeaderParser`, `ClangSharpParseRequest` — translates C++ headers into model members |
| `SharpCpp.Pipeline` | Pipeline | `BindingCompilationBootstrapPipeline.Apply()` — runs inheritance, polymorphism, and generic instantiation passes |
| `SharpCpp.Rendering` | Rendering | `BindingOutputRenderer.Render()` — produces `RenderedFileCollection` (C# source artifacts) |
| `SharpCpp.Metadata` | Metadata | `BindingMetadataExporter.Export()`, `BindingMetadataSerializer.Serialize()` — emits JSON metadata describing the compilation |
| `SharpCpp.Support` | Support | Referenced in `.csproj`; expected to provide managed runtime support types (not directly called from Tool in current bootstrap slice) |

All six `SharpCpp.*` assemblies are project-referenced in `SharpCpp.Tool.csproj`.
