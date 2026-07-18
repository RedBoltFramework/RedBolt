using SharpCpp.Model.Ast;
using SharpCpp.Pipeline;

using Xunit;

namespace SharpCpp.Tests.Model;

public sealed class BindingCompilationInheritancePassTests
{
    [Fact]
    public void Apply_preserves_callback_delegate_policy_on_projected_secondary_base_methods()
    {
        var compilation = new BindingCompilation("SharpCpp.Tests", "Test", "Bindings", "Native", ["Sample.hpp"]);
        var primaryBase = new RecordBinding("record:Primary", "Primary", "Primary", "Test::Primary");
        var secondaryBase = new RecordBinding("record:Secondary", "Secondary", "Secondary", "Test::Secondary");
        var derived = new RecordBinding("record:Derived", "Derived", "Derived", "Test::Derived");
        derived.Bases.Add(new RecordBaseBinding(primaryBase.SymbolId, primaryBase.NativeName, primaryBase.ManagedName, primaryBase.CppQualifiedName, isVirtual: false));
        derived.Bases.Add(new RecordBaseBinding(secondaryBase.SymbolId, secondaryBase.NativeName, secondaryBase.ManagedName, secondaryBase.CppQualifiedName, isVirtual: false));

        var callbackType = BindingType.CreateCallback(
            "std::function<void(int)>",
            "global::System.Action<{0}>",
            managedMarshaller: null,
            BindingType.Void,
            [BindingType.Int32]);
        var method = new FunctionBinding("method:Secondary::OnProgress", "OnProgress", "OnProgress", BindingType.Void, "OnProgress")
        {
            IsStatic = false,
            OwningRecordCppTypeName = secondaryBase.CppQualifiedName
        };
        method.Parameters.Add(new ParameterBinding("parameter:callback", "callback", "callback", callbackType)
        {
            UseCallbackDelegate = true,
            CallbackDelegateTypeName = "ProgressCallback"
        });
        secondaryBase.Members.Add(method);
        compilation.Members.Add(primaryBase);
        compilation.Members.Add(secondaryBase);
        compilation.Members.Add(derived);

        BindingCompilationInheritancePass.Apply(compilation);

        var projectedMethod = Assert.Single(derived.Members.OfType<FunctionBinding>());
        var projectedParameter = Assert.Single(projectedMethod.Parameters);
        Assert.True(projectedParameter.UseCallbackDelegate);
        Assert.Equal("ProgressCallback", projectedParameter.CallbackDelegateTypeName);
    }
}
