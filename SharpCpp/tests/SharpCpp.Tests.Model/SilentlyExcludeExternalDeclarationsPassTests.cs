using SharpCpp.Model.Ast;
using SharpCpp.Pipeline;

using Xunit;

namespace SharpCpp.Tests.Model;

public sealed class SilentlyExcludeExternalDeclarationsPassTests
{
    [Fact]
    public void Apply_silently_excludes_unconfigured_placeholder_records()
    {
        var compilation = new BindingCompilation("TestProject", "Test", "Bindings", "Native", ["Sample.hpp"]);
        var placeholder = new RecordBinding("record:external::Widget", "Widget", "Widget", "external::Widget");
        placeholder.SetMetadata(MetadataKeys.PlaceholderRecord, true);
        compilation.Members.Add(placeholder);

        SilentlyExcludeExternalDeclarationsPass.Apply(compilation);

        Assert.True(placeholder.IsIgnored());
        Assert.True(placeholder.IsSilentlyExcluded());
    }

    [Fact]
    public void Apply_keeps_explicitly_configured_placeholder_records_visible()
    {
        var compilation = new BindingCompilation("TestProject", "Test", "Bindings", "Native", ["Sample.hpp"]);
        var placeholder = new RecordBinding("record:std::basic_string", "basic_string", "String", "std::basic_string")
        {
            ManagedBaseTypeName = "StringBase"
        };
        placeholder.SetMetadata(MetadataKeys.PlaceholderRecord, true);
        compilation.Members.Add(placeholder);

        SilentlyExcludeExternalDeclarationsPass.Apply(compilation);

        Assert.False(placeholder.IsIgnored());
        Assert.False(placeholder.IsSilentlyExcluded());
    }

    [Fact]
    public void Apply_silently_excludes_iterator_endpoint_functions()
    {
        var compilation = new BindingCompilation("TestProject", "Test", "Bindings", "Native", ["Sample.hpp"]);
        var record = new RecordBinding("record:Container", "Container", "Container", "Test::Container");
        var begin = new FunctionBinding("method:Container:begin", "begin", "Begin", BindingType.Int32, cppQualifiedName: "Test::Container::begin");
        var end = new FunctionBinding("method:Container:end", "end", "End", BindingType.Int32, cppQualifiedName: "Test::Container::end");
        var count = new FunctionBinding("method:Container:count", "count", "Count", BindingType.Int32, cppQualifiedName: "Test::Container::count");
        record.Members.Add(begin);
        record.Members.Add(end);
        record.Members.Add(count);
        compilation.Members.Add(record);

        SilentlyExcludeExternalDeclarationsPass.Apply(compilation);

        Assert.True(begin.IsSilentlyExcluded());
        Assert.True(end.IsSilentlyExcluded());
        Assert.False(count.IsIgnored());
    }

    [Fact]
    public void Apply_silently_excludes_assignment_operators()
    {
        var compilation = new BindingCompilation("TestProject", "Test", "Bindings", "Native", ["Sample.hpp"]);
        var record = new RecordBinding("record:Assignable", "Assignable", "Assignable", "Test::Assignable");
        var assignment = new FunctionBinding("method:Assignable:operator=", "operator=", "OperatorAssign", record.HandleType, cppQualifiedName: "Test::Assignable::operator=");
        var equality = new FunctionBinding("method:Assignable:operator==", "operator==", "OperatorEquals", BindingType.Boolean, cppQualifiedName: "Test::Assignable::operator==")
        {
            OperatorToken = "=="
        };
        record.Members.Add(assignment);
        record.Members.Add(equality);
        compilation.Members.Add(record);

        SilentlyExcludeExternalDeclarationsPass.Apply(compilation);

        Assert.True(assignment.IsSilentlyExcluded());
        Assert.False(equality.IsIgnored());
    }
}
