using SharpCpp.Model.Ast;
using SharpCpp.Rendering;
using Xunit;

namespace SharpCpp.Tests.Rendering;

public sealed class ImportedModuleRenderingTests
{
    [Fact]
    public void Imported_base_record_is_not_emitted_and_local_record_uses_qualified_base_type()
    {
        var compilation = new BindingCompilation(
            "Rbfx.UserCode.Bindings",
            "RbfxUserCode",
            "Bindings",
            "SharpCpp.Rbfx.UserCode.Native",
            ["RbfxUserCode/RotatingComponent.hpp"]);

        var importedBase = new RecordBinding("import:rbfx:LogicComponent", "LogicComponent", "global::Urho3D.LogicComponent", "Urho3D::LogicComponent")
        {
            ManagedNamespace = "Urho3D"
        };
        importedBase.SetMetadata(MetadataKeys.SuppressEmission, true);

        var localRecord = new RecordBinding("record:RotatingComponent", "RotatingComponent", "RotatingComponent", "RbfxUserCode::RotatingComponent")
        {
            ManagedNamespace = "RbfxUserCode",
            ManagedBaseTypeName = importedBase.ManagedName
        };
        localRecord.Bases.Add(new RecordBaseBinding(importedBase.SymbolId, importedBase.NativeName, importedBase.ManagedName, importedBase.CppQualifiedName, isVirtual: false));

        compilation.Members.Add(importedBase);
        compilation.Members.Add(localRecord);

        var managedOutput = BindingOutputRenderer.Render(compilation, "Rbfx.UserCode").Files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cs", StringComparison.Ordinal)).Content;

        Assert.DoesNotContain("class LogicComponent", managedOutput, StringComparison.Ordinal);
        Assert.Contains("public partial class RotatingComponent : global::Urho3D.LogicComponent", managedOutput, StringComparison.Ordinal);
    }
}
