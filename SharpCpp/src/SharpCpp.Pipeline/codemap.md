# src/SharpCpp.Pipeline/

## Responsibility

Owns the **binding compilation pipeline** — the ordered sequence of transformation passes that convert a parsed `BindingCompilation` AST into a renderer-ready intermediate representation. This is the middle layer of the three-stage architecture (Parsing → Pipeline → Rendering).

Each pass is a cohesive, self-contained transformation that mutates the AST in place. The pipeline orchestrator (`BindingCompilationBootstrapPipeline`) selects, orders, and applies these passes to produce a validated, denormalized, and fully-resolved `BindingCompilation` ready for code generation.

## Design

### Pass Model (Implicit Interface)

There is **no formal `IPipelinePass` interface or base class**. Every pass is a `public static class` exposing a single entry point:

```csharp
public static void Apply(BindingCompilation compilation)
```

This keeps pass definitions zero-overhead and avoids a fixed abstraction boundary, at the cost of requiring manual ordering in the orchestrator. Passes communicate exclusively through the shared `BindingCompilation` AST — there is no per-pass context object or bus.

### Pass Taxonomy

| Category | Passes | Pattern |
|---|---|---|
| **Normalization** | `NormalizeManagedNamespacesPass` | Map C++ namespaces → managed namespaces via configured mappings or PascalCase heuristics |
| **Decoration** | `MarshallerMetadataPhase2` (from Parsing layer) | Apply marshaller annotations from metadata files onto AST nodes |
| **Filtering** | `SilentlyExcludeExternalDeclarationsPass`, `SilentlyExcludeConstOverloadMethodsPass`, `DegradeUnsupportedSurfacePass` | Silently prune external/implementation-only declarations and const methods whose non-const overloads collapse to the same managed callable signature, then remove constructs that cannot be expressed in the target managed language surface; degradation uses **fixed-point iteration** (`while (changed)`) since ignoring one node may cascade to dependents |
| **Instantiation Discovery** | `BindingCompilationGenericInstantiationPass` | Walk all function return types, parameter types, field types, and aliases to discover concrete generic instantiations referenced by usage before validation/degradation can reject generic records |
| **Inheritance Projection** | `BindingCompilationInheritancePass` | Clone virtual methods from secondary base records into derived records so the managed hierarchy has a flat method list per type |
| **Analysis** | `InheritManagedRuntimePolicyPass`, `BindingCompilationPolymorphismPass` | Inherit managed runtime policy from primary bases and mark records as polymorphic (`IsPolymorphic = true`) if they declare virtual methods or inherit from a polymorphic base; polymorphism uses **fixed-point iteration** for transitive propagation |
| **Validation** | `ValidateBootstrapSurfacePass` | Reject remaining unsupported shapes: virtual base inheritance, generic records without instantiations, non-primitive/enum/handle generic arguments, generics with operators or fields |

### Key Architectural Decisions

- **All mutation, all in-place**: Passes modify the AST directly. There is no immutable snapshot, no diff, and no rollback. The pipeline is a linear sequence of destructive transformations.
- **No inter-pass context**: Every pass re-reads the current state of the AST; there is no shared state beyond what lives on the `BindingCompilation` itself.
- **Signature-keyed deduplication**: Multiple passes construct composite string keys (e.g., `"managedName|isStatic|isConstructor|paramType1;paramType2"`) to detect duplicates across callable signatures, enabling deterministic overload resolution.
- **Metadata-driven decisions**: Passes inspect `MetadataKeys` annotations (set by earlier parsing or metadata phases) to decide suppression, behavior, or diagnostics — avoiding type-specific special cases in core pass logic.

### Fixed-Point Iteration

Two passes (`DegradeUnsupportedSurfacePass`, `BindingCompilationPolymorphismPass`) use a `while (changed)` loop because their decisions are **non-local and cascading**:
- Ignoring a record may cause dependent functions/fields to also become unsupported in the next iteration.
- Polymorphism propagates upward through the inheritance chain, so a newly-marked-polymorphic base requires re-evaluating all its descendents.

This guarantees convergence without requiring explicit topological constraint propagation.

## Data & Control Flow

### Pass Ordering (Bootstrap Pipeline)

```
BindingCompilation
    │
    ▼
1. NormalizeManagedNamespacesPass
    │  Assigns ManagedNamespace to each record/enum
    │  from C++ qualified name via namespace mappings
    ▼
2. MarshallerMetadataPhase2  (from SharpCpp.Parsing)
    │  Applies marshaller annotations, type mappings, config
    ▼
3. SilentlyExcludeExternalDeclarationsPass
    │  Silently ignores external placeholders and implementation-only APIs
    ▼
4. BindingCompilationGenericInstantiationPass
   │  Discovers concrete instantiations for generic records/functions
   │  by walking all typed references in the compilation
   ▼
5. DegradeUnsupportedSurfacePass
   │  Removes duplicates, namespace-filtered, externally-owned,
   │  unsupported-type, and unsupported-operator declarations
   │  [fixed-point: cascading ignores]
   ▼
6. BindingCompilationInheritancePass
    │  Projects virtual methods from secondary bases
    │  into derived records (flattened method lists)
    ▼
7. InheritManagedRuntimePolicyPass
    │  Propagates managed runtime policy along retained primary bases
    ▼
8. BindingCompilationPolymorphismPass
    │  Marks records as polymorphic transitively
    │  [fixed-point: propagates up inheritance chain]
    ▼
9. ValidateBootstrapSurfacePass
    │  Final validation: generic shape constraints,
    │  virtual base inheritance, field/operator restrictions
    ▼
10. SilentlyExcludeConstOverloadMethodsPass
    │  Silently ignores const methods when a non-const overload has
    │  the same managed callable signature
    ▼
11. DegradeUnsupportedSurfacePass
    │  Re-runs degradation after validation and const-overload pruning
    ▼
BindingCompilation (renderer-ready)
```

### Lifecycle of a Single Pass

1. **Guard**: `ArgumentNullException.ThrowIfNull(compilation)`
2. **Iterate**: Walk `compilation.Records`, `compilation.Functions`, `compilation.Enums`, or `compilation.Descendants<T>()`
3. **Decide**: Inspect `IsIgnored()`, `MetadataKeys`, structural properties
4. **Mutate**: Set `IgnoreReason`, add/remove members, toggle boolean flags, populate instantiation lists
5. **Repeat** (if fixed-point): loop while any mutation occurred

### Side Effects

Passes may emit **warnings** via `record.SetWarning(...)` for recoverable degradation (e.g., dropping an unavailable base class) and set **diagnostic ignore reasons** via `record.SetIgnoreReason(...)` to exclude a node from code generation.

## Integration

### Dependencies (incoming arrows)

| Layer | Dependency | How used |
|---|---|---|
| **SharpCpp.Model** (`../SharpCpp.Model/`) | `BindingCompilation`, `RecordBinding`, `FunctionBinding`, `FieldBinding`, `EnumBinding`, `BindingType`, metadata annotations (`MetadataKeys`) | AST types that all passes read and mutate |
| **SharpCpp.Metadata** (`../SharpCpp.Metadata/`) | Metadata keys (`MetadataKeys.ExplicitGenericInstantiations`, `MetadataKeys.SuppressEmission`, `MetadataKeys.DocumentationComment`, etc.), `GenericInstantiationMetadata` | Passes consult metadata to decide behavior |
| **SharpCpp.Parsing** (`../SharpCpp.Parsing/`) | `MarshallerMetadataPhase2.Apply()` | Called as pass #2 to apply marshaller configurations to the AST |

### Consumers (outgoing arrows)

| Layer | Consumption | How consumed |
|---|---|---|
| **SharpCpp.Rendering** (`../SharpCpp.Rendering/`) | The processed `BindingCompilation` | Renderers traverse the validated AST to emit C# source code |
| **SharpCpp.Tool** (`../SharpCpp.Tool/`) | `BindingCompilationBootstrapPipeline.Apply()` | The CLI tool composes the pipeline after parsing and before rendering |

### Assembly Dependencies (csproj)

The project references three sibling assemblies:
- `SharpCpp.Model` — AST node types
- `SharpCpp.Metadata` — metadata key constants and value types
- `SharpCpp.Parsing` — the `MarshallerMetadataPhase2` static entry point (pass #2)

No downstream layers (`Rendering`, `Tool`) appear in the csproj — the pipeline is a pure transformation stage with no knowledge of code generation or CLI orchestration.
