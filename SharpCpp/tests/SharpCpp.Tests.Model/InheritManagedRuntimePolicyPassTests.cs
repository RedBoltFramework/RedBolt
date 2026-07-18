using SharpCpp.Model.Ast;
using SharpCpp.Pipeline;

using Xunit;

namespace SharpCpp.Tests.Model;

public sealed class InheritManagedRuntimePolicyPassTests
{
    [Fact]
    public void Apply_handles_primary_base_cycle_without_hanging()
    {
        var compilation = new BindingCompilation(
            "TestProject",
            "Test.Namespace",
            "Bindings",
            "Test.Native",
            ["Sample.hpp"]);

        var baseRecord = new RecordBinding("record:Base", "Base", "Base")
        {
            ManagedInstanceCacheFactoryExpression = "Cache.Shared",
            ManagedInstanceCacheTypeName = "CacheType",
            EmitDefaultNativeReleaseOverride = false
        };

        var derivedRecord = new RecordBinding("record:Derived", "Derived", "Derived");

        baseRecord.Bases.Add(new RecordBaseBinding(derivedRecord.SymbolId, derivedRecord.NativeName, derivedRecord.ManagedName, derivedRecord.CppQualifiedName, isVirtual: false));
        derivedRecord.Bases.Add(new RecordBaseBinding(baseRecord.SymbolId, baseRecord.NativeName, baseRecord.ManagedName, baseRecord.CppQualifiedName, isVirtual: false));

        compilation.Members.Add(baseRecord);
        compilation.Members.Add(derivedRecord);

        InheritManagedRuntimePolicyPass.Apply(compilation);

        Assert.Equal("Cache.Shared", derivedRecord.ManagedInstanceCacheFactoryExpression);
        Assert.Equal("CacheType", derivedRecord.ManagedInstanceCacheTypeName);
        Assert.False(derivedRecord.EmitDefaultNativeReleaseOverride);
    }
}
