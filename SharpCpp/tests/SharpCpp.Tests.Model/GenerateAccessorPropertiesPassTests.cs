using SharpCpp.Model.Ast;
using SharpCpp.Pipeline;

using Xunit;

namespace SharpCpp.Tests.Model;

public sealed class GenerateAccessorPropertiesPassTests
{
    [Fact]
    public void Apply_promotes_matching_getter_and_setter_to_property_metadata()
    {
        var compilation = new BindingCompilation("TestProject", "Test", "Bindings", "Native", ["Sample.hpp"]);
        var record = new RecordBinding("record:Sample", "Sample", "Sample", "Test::Sample");
        var getter = new FunctionBinding("method:Sample:GetCount", "GetCount", "GetCount", BindingType.Int32)
        {
            IsStatic = false
        };
        var setter = new FunctionBinding("method:Sample:SetCount", "SetCount", "SetCount", BindingType.Void)
        {
            IsStatic = false
        };
        setter.Parameters.Add(new ParameterBinding("parameter:Sample:SetCount:value", "value", "value", BindingType.Int32));
        record.Members.Add(getter);
        record.Members.Add(setter);
        compilation.Members.Add(record);

        var promotedCount = GenerateAccessorPropertiesPass.Apply(compilation);

        Assert.Equal(1, promotedCount);
        var getterAnnotation = Assert.IsType<AccessorPropertyAnnotation>(getter.GetMetadata<AccessorPropertyAnnotation>(MetadataKeys.AccessorProperty));
        var setterAnnotation = Assert.IsType<AccessorPropertyAnnotation>(setter.GetMetadata<AccessorPropertyAnnotation>(MetadataKeys.AccessorProperty));
        Assert.Equal(new AccessorPropertyAnnotation("Count", AccessorPropertyKind.Getter), getterAnnotation);
        Assert.Equal(new AccessorPropertyAnnotation("Count", AccessorPropertyKind.Setter), setterAnnotation);
    }

    [Fact]
    public void Apply_promotes_is_prefixed_getter_only_property_when_pattern_allows_it()
    {
        var compilation = new BindingCompilation("TestProject", "Test", "Bindings", "Native", ["Sample.hpp"]);
        var record = new RecordBinding("record:Sample", "Sample", "Sample", "Test::Sample");
        var getter = new FunctionBinding("method:Sample:IsHovered", "IsHovered", "IsHovered", BindingType.Boolean)
        {
            IsStatic = false
        };
        record.Members.Add(getter);
        compilation.Members.Add(record);

        var promotedCount = GenerateAccessorPropertiesPass.Apply(
            compilation,
            [new AccessorPropertyPattern("Is", "Set", propertyNamePrefix: "Is", allowGetterOnly: true)]);

        Assert.Equal(1, promotedCount);
        Assert.Equal(
            new AccessorPropertyAnnotation("IsHovered", AccessorPropertyKind.Getter),
            getter.GetMetadata<AccessorPropertyAnnotation>(MetadataKeys.AccessorProperty));
    }

    [Fact]
    public void Apply_skips_property_when_setter_type_does_not_match_getter_type()
    {
        var compilation = new BindingCompilation("TestProject", "Test", "Bindings", "Native", ["Sample.hpp"]);
        var record = new RecordBinding("record:Sample", "Sample", "Sample", "Test::Sample");
        var getter = new FunctionBinding("method:Sample:GetCount", "GetCount", "GetCount", BindingType.Int32)
        {
            IsStatic = false
        };
        var setter = new FunctionBinding("method:Sample:SetCount", "SetCount", "SetCount", BindingType.Void)
        {
            IsStatic = false
        };
        setter.Parameters.Add(new ParameterBinding("parameter:Sample:SetCount:value", "value", "value", BindingType.Single));
        record.Members.Add(getter);
        record.Members.Add(setter);
        compilation.Members.Add(record);

        var promotedCount = GenerateAccessorPropertiesPass.Apply(compilation);

        Assert.Equal(0, promotedCount);
        Assert.False(getter.HasMetadata(MetadataKeys.AccessorProperty));
        Assert.False(setter.HasMetadata(MetadataKeys.AccessorProperty));
    }

    [Fact]
    public void Apply_skips_property_when_non_accessor_member_uses_property_name()
    {
        var compilation = new BindingCompilation("TestProject", "Test", "Bindings", "Native", ["Sample.hpp"]);
        var record = new RecordBinding("record:Sample", "Sample", "Sample", "Test::Sample");
        var getter = new FunctionBinding("method:Sample:GetCount", "GetCount", "GetCount", BindingType.Int32)
        {
            IsStatic = false
        };
        var existingMember = new FunctionBinding("method:Sample:Count", "Count", "Count", BindingType.Void)
        {
            IsStatic = false
        };
        record.Members.Add(getter);
        record.Members.Add(existingMember);
        compilation.Members.Add(record);

        var promotedCount = GenerateAccessorPropertiesPass.Apply(compilation);

        Assert.Equal(0, promotedCount);
        Assert.False(getter.HasMetadata(MetadataKeys.AccessorProperty));
    }

    [Fact]
    public void Apply_skips_property_when_field_uses_property_name()
    {
        var compilation = new BindingCompilation("TestProject", "Test", "Bindings", "Native", ["Sample.hpp"]);
        var record = new RecordBinding("record:Sample", "Sample", "Sample", "Test::Sample");
        var getter = new FunctionBinding("method:Sample:GetCount", "GetCount", "GetCount", BindingType.Int32)
        {
            IsStatic = false
        };
        var existingField = new FieldBinding("field:Sample:Count", "count_", "Count", BindingType.Int32, "int", "Test::Sample");
        record.Members.Add(getter);
        record.Members.Add(existingField);
        compilation.Members.Add(record);

        var promotedCount = GenerateAccessorPropertiesPass.Apply(compilation);

        Assert.Equal(0, promotedCount);
        Assert.False(getter.HasMetadata(MetadataKeys.AccessorProperty));
    }

    [Fact]
    public void Apply_skips_projected_secondary_base_methods()
    {
        var compilation = new BindingCompilation("TestProject", "Test", "Bindings", "Native", ["Sample.hpp"]);
        var record = new RecordBinding("record:Sample", "Sample", "Sample", "Test::Sample");
        var getter = new FunctionBinding("method:Sample:GetCount", "GetCount", "GetCount", BindingType.Int32)
        {
            IsStatic = false,
            NativeDispatchRecordCppTypeName = "Test::IValueSource"
        };
        var setter = new FunctionBinding("method:Sample:SetCount", "SetCount", "SetCount", BindingType.Void)
        {
            IsStatic = false,
            NativeDispatchRecordCppTypeName = "Test::IValueSource"
        };
        setter.Parameters.Add(new ParameterBinding("parameter:Sample:SetCount:value", "value", "value", BindingType.Int32));
        record.Members.Add(getter);
        record.Members.Add(setter);
        compilation.Members.Add(record);

        var promotedCount = GenerateAccessorPropertiesPass.Apply(compilation);

        Assert.Equal(0, promotedCount);
        Assert.False(getter.HasMetadata(MetadataKeys.AccessorProperty));
        Assert.False(setter.HasMetadata(MetadataKeys.AccessorProperty));
    }
}
