# C# conventions

Apply when editing C# source.


- Classes: PascalCase          (e.g. RenderPipeline)
- Methods: PascalCase          (e.g. GetRenderPath)
- Properties: PascalCase       (e.g. IsEnabled)
- Public fields: PascalCase    (e.g. FrameCount)
- Private fields: _camelCase   with leading underscore (e.g. _frameCount)
- Local variables: camelCase   (e.g. frameCount)
- Method parameters: camelCase (e.g. renderPath)
- Constants: PascalCase

## File headers

Keep leading copyright and license comments before the namespace declaration.
Order namespace-bearing C# file headers as follows:

1. File-scoped namespace declaration.
2. `System.*` and `Microsoft.*` using directives.
3. Package using directives, including NuGet and `extern/` dependencies such as SharpCpp.
4. `RedBolt.*` using directives.

All non-global using directives must be namespace-local. Alphabetize each group
and separate adjacent groups with one empty line. Classify aliases by their
target namespace. Separate the namespace declaration, using directives, and code
with one empty line. Files containing only global using directives remain
namespace-less because C# forbids global using directives inside a namespace.
