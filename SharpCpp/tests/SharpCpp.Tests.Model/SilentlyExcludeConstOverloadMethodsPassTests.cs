using SharpCpp.Model.Ast;
using SharpCpp.Pipeline;

using Xunit;

namespace SharpCpp.Tests.Model;

public sealed class SilentlyExcludeConstOverloadMethodsPassTests
{
    [Fact]
    public void Apply_silently_excludes_const_method_when_non_const_method_has_same_managed_signature()
    {
        var compilation = new BindingCompilation("TestProject", "Test", "Bindings", "Native", ["Sample.hpp"]);
        var record = new RecordBinding("record:Test::Sample", "Sample", "Sample", "Test::Sample");
        var mutable = new FunctionBinding("method:Test::Sample::GetData", "GetData", "GetData", BindingType.Int32, cppQualifiedName: "Test::Sample::GetData")
        {
            IsStatic = false,
            IsConstMethod = false
        };
        var immutable = new FunctionBinding("method:Test::Sample::GetData:const", "GetData", "GetData", BindingType.Int32, cppQualifiedName: "Test::Sample::GetData")
        {
            IsStatic = false,
            IsConstMethod = true
        };
        record.Members.Add(mutable);
        record.Members.Add(immutable);
        compilation.Members.Add(record);

        SilentlyExcludeConstOverloadMethodsPass.Apply(compilation);

        Assert.False(mutable.IsIgnored());
        Assert.True(immutable.IsIgnored());
        Assert.True(immutable.IsSilentlyExcluded());
        Assert.Contains("duplicates a non-const managed callable signature", immutable.GetIgnoreReason(), StringComparison.Ordinal);
    }

    [Fact]
    public void Apply_keeps_const_method_when_no_non_const_duplicate_exists()
    {
        var compilation = new BindingCompilation("TestProject", "Test", "Bindings", "Native", ["Sample.hpp"]);
        var record = new RecordBinding("record:Test::Sample", "Sample", "Sample", "Test::Sample");
        var immutable = new FunctionBinding("method:Test::Sample::GetData:const", "GetData", "GetData", BindingType.Int32, cppQualifiedName: "Test::Sample::GetData")
        {
            IsStatic = false,
            IsConstMethod = true
        };
        record.Members.Add(immutable);
        compilation.Members.Add(record);

        SilentlyExcludeConstOverloadMethodsPass.Apply(compilation);

        Assert.False(immutable.IsIgnored());
        Assert.False(immutable.IsSilentlyExcluded());
    }

    [Fact]
    public void Apply_keeps_const_method_when_non_const_duplicate_is_already_degraded()
    {
        var compilation = new BindingCompilation("TestProject", "Test", "Bindings", "Native", ["Sample.hpp"]);
        var record = new RecordBinding("record:Test::Sample", "Sample", "Sample", "Test::Sample");
        var mutable = new FunctionBinding("method:Test::Sample::GetData", "GetData", "GetData", BindingType.Int32, cppQualifiedName: "Test::Sample::GetData")
        {
            IsStatic = false,
            IsConstMethod = false
        };
        var immutable = new FunctionBinding("method:Test::Sample::GetData:const", "GetData", "GetData", BindingType.Int32, cppQualifiedName: "Test::Sample::GetData")
        {
            IsStatic = false,
            IsConstMethod = true
        };
        mutable.SetIgnoreReason("non-const overload is not renderer-ready");
        record.Members.Add(mutable);
        record.Members.Add(immutable);
        compilation.Members.Add(record);

        SilentlyExcludeConstOverloadMethodsPass.Apply(compilation);

        Assert.True(mutable.IsIgnored());
        Assert.False(immutable.IsIgnored());
        Assert.False(immutable.IsSilentlyExcluded());
    }

    [Fact]
    public void Apply_silently_excludes_const_method_when_duplicate_degradation_already_marked_it()
    {
        var compilation = new BindingCompilation("TestProject", "Test", "Bindings", "Native", ["Sample.hpp"]);
        var record = new RecordBinding("record:Test::Sample", "Sample", "Sample", "Test::Sample");
        var mutable = new FunctionBinding("method:Test::Sample::GetData", "GetData", "GetData", BindingType.Int32, cppQualifiedName: "Test::Sample::GetData")
        {
            IsStatic = false,
            IsConstMethod = false
        };
        var immutable = new FunctionBinding("method:Test::Sample::GetData:const", "GetData", "GetData", BindingType.Int32, cppQualifiedName: "Test::Sample::GetData")
        {
            IsStatic = false,
            IsConstMethod = true
        };
        record.Members.Add(mutable);
        record.Members.Add(immutable);
        compilation.Members.Add(record);
        DegradeUnsupportedSurfacePass.Apply(compilation);

        SilentlyExcludeConstOverloadMethodsPass.Apply(compilation);

        Assert.False(mutable.IsIgnored());
        Assert.True(immutable.IsIgnored());
        Assert.True(immutable.IsSilentlyExcluded());
        Assert.Contains("duplicates a non-const managed callable signature", immutable.GetIgnoreReason(), StringComparison.Ordinal);
    }

    [Fact]
    public void Apply_silently_excludes_const_method_when_non_const_duplicate_has_other_degradation_reason()
    {
        var compilation = new BindingCompilation("TestProject", "Test", "Bindings", "Native", ["Sample.hpp"]);
        var record = new RecordBinding("record:Test::Sample", "Sample", "Sample", "Test::Sample");
        var mutable = new FunctionBinding("method:Test::Sample::GetData", "GetData", "GetData", BindingType.Int32, cppQualifiedName: "Test::Sample::GetData")
        {
            IsStatic = false,
            IsConstMethod = false
        };
        var immutable = new FunctionBinding("method:Test::Sample::GetData:const", "GetData", "GetData", BindingType.Int32, cppQualifiedName: "Test::Sample::GetData")
        {
            IsStatic = false,
            IsConstMethod = true
        };
        mutable.SetIgnoreReason("return type is unsupported in this bootstrap surface");
        immutable.SetIgnoreReason("managed callable signature 'GetData|False|False|' is declared multiple times in this bootstrap surface");
        record.Members.Add(mutable);
        record.Members.Add(immutable);
        compilation.Members.Add(record);

        SilentlyExcludeConstOverloadMethodsPass.Apply(compilation);

        Assert.True(mutable.IsIgnored());
        Assert.False(mutable.IsSilentlyExcluded());
        Assert.True(immutable.IsIgnored());
        Assert.True(immutable.IsSilentlyExcluded());
        Assert.Contains("duplicates a non-const managed callable signature", immutable.GetIgnoreReason(), StringComparison.Ordinal);
    }

    [Fact]
    public void Apply_keeps_const_method_when_parameters_keep_managed_signature_distinct()
    {
        var compilation = new BindingCompilation("TestProject", "Test", "Bindings", "Native", ["Sample.hpp"]);
        var record = new RecordBinding("record:Test::Sample", "Sample", "Sample", "Test::Sample");
        var getByIndex = new FunctionBinding("method:Test::Sample::GetData:int", "GetData", "GetData", BindingType.Int32, cppQualifiedName: "Test::Sample::GetData")
        {
            IsStatic = false
        };
        getByIndex.Parameters.Add(new ParameterBinding("parameter:index", "index", "index", BindingType.Int32, cppTypeName: "int"));
        var getAll = new FunctionBinding("method:Test::Sample::GetData:const", "GetData", "GetData", BindingType.Int32, cppQualifiedName: "Test::Sample::GetData")
        {
            IsStatic = false,
            IsConstMethod = true
        };
        record.Members.Add(getByIndex);
        record.Members.Add(getAll);
        compilation.Members.Add(record);

        SilentlyExcludeConstOverloadMethodsPass.Apply(compilation);

        Assert.False(getByIndex.IsIgnored());
        Assert.False(getAll.IsIgnored());
    }

    [Fact]
    public void Apply_keeps_const_method_when_non_const_template_overload_has_same_managed_parameters()
    {
        var compilation = new BindingCompilation("TestProject", "Test", "Bindings", "Native", ["Sample.hpp"]);
        var record = new RecordBinding("record:Test::Sample", "Sample", "Sample", "Test::Sample");
        var mutableTemplate = new FunctionBinding("method:Test::Sample::GetSubsystem:template", "GetSubsystem", "GetSubsystem", BindingType.CreateGenericParameter("T"), cppQualifiedName: "Test::Sample::GetSubsystem")
        {
            IsStatic = false,
            IsConstMethod = false,
            CppReturnTypeName = "T*"
        };
        mutableTemplate.GenericParameters.Add(new BindingGenericParameter("T", 0));
        mutableTemplate.Parameters.Add(new ParameterBinding("parameter:type:template", "type", "type", BindingType.Int32, cppTypeName: "StringHash"));
        var immutable = new FunctionBinding("method:Test::Sample::GetSubsystem:const", "GetSubsystem", "GetSubsystem", BindingType.Int32, cppQualifiedName: "Test::Sample::GetSubsystem")
        {
            IsStatic = false,
            IsConstMethod = true,
            CppReturnTypeName = "Object*"
        };
        immutable.Parameters.Add(new ParameterBinding("parameter:type", "type", "type", BindingType.Int32, cppTypeName: "StringHash"));
        record.Members.Add(mutableTemplate);
        record.Members.Add(immutable);
        compilation.Members.Add(record);

        DegradeUnsupportedSurfacePass.Apply(compilation);
        SilentlyExcludeConstOverloadMethodsPass.Apply(compilation);

        Assert.False(mutableTemplate.IsIgnored(), mutableTemplate.GetIgnoreReason());
        Assert.False(immutable.IsIgnored(), immutable.GetIgnoreReason());
        Assert.False(immutable.IsSilentlyExcluded());
    }
}
