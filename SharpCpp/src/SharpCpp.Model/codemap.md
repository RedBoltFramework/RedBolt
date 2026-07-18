# src/SharpCpp.Model/

## Responsibility

`SharpCpp.Model` is the **foundation layer** of the SharpCpp binding generator. It defines the shared vocabulary that every other layer depends on — the binding AST, type system, diagnostics infrastructure, and code-emission utility — without knowing about ClangSharp, pass scheduling, code rendering, or support runtime internals.

Per the architecture (doc/01-architecture.md), this is the lowest managed layer. All upstream projects (`Parsing`, `Pipeline`, `Rendering.CSharp`, `Rendering.Cpp`, `Metadata`, `Tool`) depend on it. It must not reference those projects.

Owns:
- **Binding AST** (`Ast/`): The tree of `BindingNode` types that represent the entire binding model after parsing and before rendering.
- **Type system** (`BindingType`, `MarshallerDescriptor`, `BindingPrimitiveKind`, `BindingTypeKind`): Unified representation of C++ types with their managed-side mappings.
- **Diagnostics** (`Diagnostics/`): Stamped diagnostic records, thread-safe collector, severity/stage enums, source references.
- **Code emission utility** (`CodeWriter`): Indentation-aware `StringBuilder` wrapper for emitting generated C#/C++.
- **Metadata key constants** (`MetadataKeys`): Well-known string keys for the per-node metadata dictionary.
- **Assembly identity** (`AssemblyMarker`): Empty marker class for project-level assembly referencing.

## Design Patterns

### Composite + Visitor-amenable tree (`BindingNode` hierarchy)

The AST is a classic composite pattern. `BindingNode` is the abstract root with:
- **Parent pointer** and **auto-parenting child collections** (`BindingNodeCollection<TNode>` overrides `InsertItem`/`RemoveItem`/`SetItem`/`ClearItems` to keep parent references consistent).
- **`Children`** (virtual) and **`Descendants<T>()`** / **`DescendantsAndSelf<T>()`** for tree traversal — renderers and passes use these to walk the tree without a separate visitor.
- **`Ancestors()`** for upward traversal (e.g. `FunctionBinding.Ancestors().OfType<RecordBinding>()` to find an owning record).
- **`BindingNodeKind`** discriminated enum for switch dispatch when pattern matching is insufficient.

Four abstract intermediate classes partition concerns:
| Class | Adds |
|---|---|
| `BindingNode` | parent, metadata dictionary, source reference, detach |
| `BindingContainerNode` | `Members` collection of `BindingNode` |
| `BindingNamedNode` | `SymbolId`, `NativeName`, `ManagedName` |
| `BindingNamedContainerNode` | both naming and children |

Concrete leaf/composite nodes: `BindingCompilation` (root), `RecordBinding`, `EnumBinding`, `FunctionBinding` (container via parameters), `ParameterBinding`, `FieldBinding`.

### Per-node metadata dictionary

Every `BindingNode` carries a lazily-initialized `Dictionary<string, object?>` accessed via `SetMetadata<T>()`, `GetMetadata<T>()`, `TryGetMetadata<T>()`, `HasMetadata()`, and `CopyMetadataFrom()`. This is the **universal extensibility slot** — passes attach resolved marshaller info, ignore reasons, accessor annotations, warnings, etc. without changing the node class hierarchy. Key names are centralized in `MetadataKeys` for consistency.

### Flyweight type singletons (`BindingType`)

`BindingType` is an immutable (after construction) type descriptor. Primitive types (`Void`, `Int32`, `Double`, etc.) are `static readonly` singletons. Handle, enum, configured, and generic-parameter types are created through named factory methods (`CreateHandle`, `CreateEnum`, `CreateConfigured`, `CreateGenericParameter`). A `MarshallerDescriptor` record binds C++ type name, public managed type name, and optional marshaller type name together.

`BindingType` supports recursive generic arguments (`GenericArguments`), generic parameter placeholders, and managed-type-name formatting with `{0}` format string overrides (used by generic records like `Std.Vector<{0}>`).

### Thread-safe diagnostic collector (`DiagnosticCollector`)

`DiagnosticCollector` uses a `ConcurrentQueue<StampedDiagnostic>` with sequence-number ordering to provide lock-free addition and sequenced retrieval. Convenience methods (`Error`, `Warning`, `Info`) wrap `Diagnostic` construction. This is shared across parsing, pipeline passes, and validation — each stage adds diagnostics while the collector preserves insertion order.

### Indentation-aware code writer (`CodeWriter`)

`CodeWriter` wraps `StringBuilder` with automatic indentation tracking based on brace deltas (`{`/`}`). It parses input lines, counting leading closing braces to dedent before writing, then tracking brace delta after content to determine the next indent level. It handles string/character literals and single-line comments to avoid false brace detection. Input normalization strips incoming whitespace and relies on the writer's own indent logic — callers never manage indent state.

### Static extension-method facades

- `BindingNodeIgnoreExtensions` — query/mutate the `IgnoreReason` metadata key as a pseudo-property (`IsIgnored`, `GetIgnoreReason`, `SetIgnoreReason`), plus warning accumulation.
- `RecordBindingExtensions` — inheritance analysis utilities: base-record chain traversal (primary, secondary, transitive closure), virtual-method slot resolution (including override replacement by matching signatures), director requirements, dispatch-record resolution, interface-name generation, managed-member naming conflict detection.

## Data & Control Flow

### Construction (Parsing → Model)

```
ClangSharp C++ AST    Phase 1/2 Parse
       ↓
BindingCompilation (root)
  ├── HeaderFiles, NamespaceMappings, ProjectName, ManagedNamespace/ClassName
  ├── RecordBinding[]
  │     ├── Bases (RecordBaseBinding[] — recordId, names, virtual flag)
  │     ├── GenericParameters (BindingGenericParameter[])
  │     ├── SupportedGenericInstantiations (RecordGenericInstantiationBinding[])
  │     ├── Members: FunctionBinding[], FieldBinding[]
  │     └── IsHandleType, IsDirector, IsAbstract, IsPolymorphic, CppQualifiedName, etc.
  ├── EnumBinding[]
  │     └── Values (EnumValueBinding[])
  └── FunctionBinding[] (free functions)
        └── Parameters (ParameterBinding[])
```

Each `BindingNode` carries:
- `Source` (`BindingSourceReference?`) — file/line/column provenance from the parsed C++ header.
- `HeaderIncludePath` — the include path for the owning header.
- `Metadata` — set during phase 2 instantiation (marshaller resolution) and passes.

### Consumption (Model → Renderers)

Renders traverse the tree via:
- `BindingCompilation.Records` / `Enums` / `Functions` — convenience properties that call `Descendants<T>()`.
- `RecordBinding.Members` — polymorphic list of `BindingNode`; renderers filter with `OfType<FunctionBinding>()` / `OfType<FieldBinding>()`.
- Per-node properties (`ReturnType`, `Parameters`, `IsStatic`, `AccessModifier`, etc.) — used directly by renderers.
- `BindingNode.Metadata` — renderers consume facts materialized by earlier stages (marshaller annotations, accessor properties, ignore reasons).

The `RecordBindingExtensions` methods are consumed by pipeline passes (virtual-method slot resolution, director detection, base-chain flattening) to reshape the tree before rendering.

### Mutation (Passes → Model)

Pipeline passes mutate the tree in place:
- Set metadata via `SetMetadata<T>()` / extension methods.
- Add/remove members via `Members` collection (auto-parenting triggers `NotifyStructureChanged()`).
- Set properties (`ManagedName`, `IsAbstract`, `IsPolymorphic`, etc.).
- Detach nodes via `Detach()` (clears parent and removes from owning collection).
- Add diagnostics via the shared `DiagnosticCollector`.

## Integration Points

### Upstream dependents (consume SharpCpp.Model)

| Layer | What it consumes |
|---|---|
| `SharpCpp.Parsing` | Constructs `BindingCompilation` tree; sets `Source`, `HeaderIncludePath`, node properties; uses `BindingType` factories; attaches initial metadata. |
| `SharpCpp.Pipeline` | Reads/mutates the tree via `Members` collections, metadata dictionary, and `RecordBindingExtensions`; adds diagnostics. |
| `SharpCpp.Rendering.CSharp` | Walks the tree to emit C# source; consumes `MarshallerDescriptor`, `ManagedName`, `BindingType`, `CodeWriter`. |
| `SharpCpp.Rendering.Cpp` | Walks the tree to emit C++ thunks; consumes `CppTypeName`, `CppQualifiedName`, `NativeName`, `CodeWriter`. |
| `SharpCpp.Metadata` | Serializes `BindingCompilation` and node properties to/from JSON; consumes `SymbolId` for identity. |
| `SharpCpp.Tool` | Wires all of the above; owns the `DiagnosticCollector` instance and presents diagnostics. |

### Downstream dependencies (SharpCpp.Model depends on)

- **Nothing** in the SharpCpp project tree. This is intentional per `doc/11-project-shape-blueprint.md`.
- Standard library: `System.Collections.Concurrent` (for `DiagnosticCollector`), `System.Collections.ObjectModel` (for `Collection<T>` base), `System.Text` (for `CodeWriter`).

### External contract surface

- `AssemblyMarker` class provides a stable target for project references (`ProjectReference` in `.csproj` files).
- `BindingNodeKind` enables switch-on-kind patterns without reflection.
- `MetadataKeys` strings form an implicit contract: any stage can write, any downstream can read. Wrong key spelling is a silent null — no compile-time safety.
- `BindingType` singletons and factory methods are the only way to create type descriptors — no direct `BindingType(...)` construction (constructor is private).
