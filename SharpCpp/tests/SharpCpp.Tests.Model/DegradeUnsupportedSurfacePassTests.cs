using SharpCpp.Model.Ast;
using SharpCpp.Pipeline;

using Xunit;

namespace SharpCpp.Tests.Model;

public sealed class DegradeUnsupportedSurfacePassTests
{
    [Fact]
    public void Apply_keeps_generic_enum_arguments_resolved_by_enum_identity()
    {
        var compilation = new BindingCompilation("SharpCpp.Tests", "Urho3D", "Bindings", "Native", ["Attribute.h"]);

        var enumBinding = new EnumBinding(
            "enum:Urho3D::OwnedMode",
            "OwnedMode",
            "OwnedMode",
            cppQualifiedName: "Urho3D::OwnedMode");
        var flagSetRecord = new RecordBinding("record:Urho3D::FlagSet", "FlagSet", "FlagSet", "Urho3D::FlagSet")
        {
            ManagedTypeFormat = "FlagSet<{0}>"
        };
        flagSetRecord.GenericParameters.Add(new BindingGenericParameter("T", 0));

        var enumArgument = BindingType.CreateEnum("AttributeMode", enumBinding.ManagedName, enumId: enumBinding.SymbolId);
        var flagSetType = BindingType.CreateHandle(
            "Urho3D::FlagSet<AttributeMode>*",
            flagSetRecord.ManagedName,
            flagSetRecord.SymbolId,
            genericArguments: [enumArgument]);

        var attributeInfo = new RecordBinding("record:Urho3D::AttributeInfo", "AttributeInfo", "AttributeInfo", "Urho3D::AttributeInfo");
        var constructor = new FunctionBinding(
            "method:Urho3D::AttributeInfo::AttributeInfo",
            "AttributeInfo",
            "AttributeInfo",
            BindingType.Void,
            cppQualifiedName: "Urho3D::AttributeInfo::AttributeInfo")
        {
            IsConstructor = true,
            IsStatic = false
        };
        constructor.Parameters.Add(new ParameterBinding("parameter:mode", "mode", "mode", flagSetType, cppTypeName: "AttributeModeFlags"));
        attributeInfo.Members.Add(constructor);
        attributeInfo.Members.Add(new FieldBinding("field:mode", "mode_", "Mode", flagSetType, "AttributeModeFlags", attributeInfo.CppQualifiedName));

        compilation.Members.Add(enumBinding);
        compilation.Members.Add(flagSetRecord);
        compilation.Members.Add(attributeInfo);

        DegradeUnsupportedSurfacePass.Apply(compilation);

        Assert.False(attributeInfo.IsIgnored(), attributeInfo.GetIgnoreReason());
        Assert.False(constructor.IsIgnored(), constructor.GetIgnoreReason());
        Assert.False(attributeInfo.Members.OfType<FieldBinding>().Single().IsIgnored(), attributeInfo.Members.OfType<FieldBinding>().Single().GetIgnoreReason());
    }

    [Fact]
    public void Apply_keeps_supported_protected_handle_fields()
    {
        var compilation = new BindingCompilation("SharpCpp.Tests", "Urho3D", "Bindings", "Native", ["Object.h"]);
        var context = new RecordBinding("record:Urho3D::Context", "Context", "Context", "Urho3D::Context");
        context.Members.Add(new FunctionBinding("method:Urho3D::Context::GetValue", "GetValue", "GetValue", BindingType.Int32, "Urho3D::Context::GetValue")
        {
            IsStatic = false,
            OwningRecordCppTypeName = context.CppQualifiedName
        });
        var @object = new RecordBinding("record:Urho3D::Object", "Object", "Object", "Urho3D::Object")
        {
            IsDirector = true
        };
        var contextType = BindingType.CreateHandle("Urho3D::Context*", "Context", context.SymbolId);
        var field = new FieldBinding("field:Urho3D::Object::context_", "context_", "Context", contextType, "Urho3D::Context*", @object.CppQualifiedName)
        {
            AccessModifier = BindingAccessModifier.Protected
        };

        @object.Members.Add(field);
        compilation.Members.Add(context);
        compilation.Members.Add(@object);

        DegradeUnsupportedSurfacePass.Apply(compilation);

        Assert.False(@object.IsIgnored(), @object.GetIgnoreReason());
        Assert.False(field.IsIgnored(), field.GetIgnoreReason());
    }

    [Fact]
    public void Apply_ignores_reference_fields_from_ast_fact()
    {
        var compilation = new BindingCompilation("SharpCpp.Tests", "Test", "Bindings", "Native", ["ReferenceFields.h"]);
        var record = new RecordBinding("record:Test::Owner", "Owner", "Owner", "Test::Owner");
        var field = new FieldBinding(
            "field:Test::Owner::value_",
            "value_",
            "Value",
            BindingType.Int32,
            "int",
            record.CppQualifiedName,
            isReference: true);
        record.Members.Add(field);
        compilation.Members.Add(record);

        DegradeUnsupportedSurfacePass.Apply(compilation);

        Assert.True(field.IsIgnored());
        Assert.Contains("reference fields", field.GetIgnoreReason(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Urho3D::ResourceRef", "ResourceRef", "Material", "GetMaterialAttr")]
    [InlineData("Urho3D::Variant", "Variant", "Spline", "GetPoint")]
    public void Apply_keeps_wrapped_record_value_returns(
        string valueCppTypeName,
        string valueManagedName,
        string ownerName,
        string methodName)
    {
        var compilation = new BindingCompilation("SharpCpp.Tests", "Urho3D", "Bindings", "Native", ["Variant.h"]);
        var valueRecord = new RecordBinding($"record:{valueCppTypeName}", valueManagedName, valueManagedName, valueCppTypeName);
        valueRecord.Members.Add(new FieldBinding($"field:{valueCppTypeName}::type_", "type_", "Type", BindingType.UInt32, "unsigned int", valueRecord.CppQualifiedName));
        var owner = new RecordBinding($"record:Urho3D::{ownerName}", ownerName, ownerName, $"Urho3D::{ownerName}");
        var method = new FunctionBinding(
            $"method:Urho3D::{ownerName}::{methodName}",
            methodName,
            methodName,
            BindingType.CreateHandle(valueCppTypeName, valueRecord.ManagedName, valueRecord.SymbolId, cppIsRecordValue: true),
            cppQualifiedName: $"Urho3D::{ownerName}::{methodName}")
        {
            IsStatic = false,
            OwningRecordCppTypeName = owner.CppQualifiedName
        };
        owner.Members.Add(method);
        owner.Members.Add(new FieldBinding($"field:Urho3D::{ownerName}::value_", "value_", "Value", BindingType.Int32, "int", owner.CppQualifiedName));
        compilation.Members.Add(valueRecord);
        compilation.Members.Add(owner);

        DegradeUnsupportedSurfacePass.Apply(compilation);

        Assert.False(owner.IsIgnored(), owner.GetIgnoreReason());
        Assert.False(method.IsIgnored(), method.GetIgnoreReason());
    }

    [Fact]
    public void Apply_ignores_protected_handle_fields_when_target_record_is_ignored()
    {
        var compilation = new BindingCompilation("SharpCpp.Tests", "Urho3D", "Bindings", "Native", ["Object.h"]);
        var context = new RecordBinding("record:Urho3D::Context", "Context", "Context", "Urho3D::Context");
        context.SetIgnoreReason("record is unavailable");
        var @object = new RecordBinding("record:Urho3D::Object", "Object", "Object", "Urho3D::Object")
        {
            IsDirector = true
        };
        var contextType = BindingType.CreateHandle("Urho3D::Context*", "Context", context.SymbolId);
        var field = new FieldBinding("field:Urho3D::Object::context_", "context_", "Context", contextType, "Urho3D::Context*", @object.CppQualifiedName)
        {
            AccessModifier = BindingAccessModifier.Protected
        };

        @object.Members.Add(field);
        compilation.Members.Add(context);
        compilation.Members.Add(@object);

        DegradeUnsupportedSurfacePass.Apply(compilation);

        Assert.False(@object.IsIgnored(), @object.GetIgnoreReason());
        Assert.True(field.IsIgnored());
        Assert.Contains("record 'Urho3D::Context' is ignored", field.GetIgnoreReason(), StringComparison.Ordinal);
    }

    [Fact]
    public void Apply_ignores_non_pure_director_virtuals_with_unsupported_callback_parameters()
    {
        var compilation = new BindingCompilation("SharpCpp.Tests", "Urho3D", "Bindings", "Native", ["Drawable.h"]);
        var drawable = new RecordBinding("record:Urho3D::Drawable", "Drawable", "Drawable", "Urho3D::Drawable")
        {
            IsDirector = true
        };
        var update = new FunctionBinding("method:Urho3D::Drawable::UpdateWorldOrigin", "UpdateWorldOrigin", "UpdateWorldOrigin", BindingType.Void, cppQualifiedName: "Urho3D::Drawable::UpdateWorldOrigin")
        {
            IsVirtual = true,
            OwningRecordCppTypeName = drawable.CppQualifiedName
        };
        update.Parameters.Add(new ParameterBinding("parameter:oldOrigin", "oldOrigin", "oldOrigin", BindingType.Int32, BindingParameterDirection.In, BindingParameterAbiKind.Reference, "const int&"));
        drawable.Members.Add(update);
        drawable.Members.Add(new FieldBinding("field:Urho3D::Drawable::value_", "value_", "Value", BindingType.Int32, "int", drawable.CppQualifiedName));
        compilation.Members.Add(drawable);

        DegradeUnsupportedSurfacePass.Apply(compilation);

        Assert.False(drawable.IsIgnored(), drawable.GetIgnoreReason());
        Assert.True(update.IsIgnored());
        Assert.Contains("director callback", update.GetIgnoreReason(), StringComparison.Ordinal);
    }

    [Fact]
    public void Apply_ignores_director_records_with_unsupported_pure_virtual_callbacks()
    {
        var compilation = new BindingCompilation("SharpCpp.Tests", "Urho3D", "Bindings", "Native", ["Abstract.h"]);
        var record = new RecordBinding("record:Urho3D::AbstractThing", "AbstractThing", "AbstractThing", "Urho3D::AbstractThing")
        {
            IsDirector = true,
            IsAbstract = true
        };
        var compute = new FunctionBinding("method:Urho3D::AbstractThing::Compute", "Compute", "Compute", BindingType.Int32, cppQualifiedName: "Urho3D::AbstractThing::Compute")
        {
            IsVirtual = true,
            IsPureVirtual = true,
            OwningRecordCppTypeName = record.CppQualifiedName
        };
        compute.Parameters.Add(new ParameterBinding("parameter:value", "value", "value", BindingType.Int32, BindingParameterDirection.Ref, BindingParameterAbiKind.Reference, "int&"));
        record.Members.Add(compute);
        compilation.Members.Add(record);

        DegradeUnsupportedSurfacePass.Apply(compilation);

        Assert.True(record.IsIgnored());
        Assert.Contains("pure virtual director callback", record.GetIgnoreReason(), StringComparison.Ordinal);
    }

    [Fact]
    public void Apply_keeps_in_value_callback_parameters()
    {
        var compilation = new BindingCompilation("SharpCpp.Tests", "Test", "Bindings", "Native", ["Callbacks.h"]);
        var callbackType = CreateVoidCallbackType();
        var function = new FunctionBinding("function:UseCallback", "UseCallback", "UseCallback", BindingType.Void, "UseCallback");
        function.Parameters.Add(new ParameterBinding("parameter:callback", "callback", "callback", callbackType));
        compilation.Members.Add(function);

        DegradeUnsupportedSurfacePass.Apply(compilation);

        Assert.False(function.IsIgnored(), function.GetIgnoreReason());
    }

    [Fact]
    public void Apply_keeps_marshaller_exposed_members_outside_namespace_mappings()
    {
        var compilation = new BindingCompilation("SharpCpp.Tests", "Urho3D", "Bindings", "Native", ["Vector.h"])
        {
            NamespaceMappings = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Urho3D"] = "Urho3D"
            }
        };
        var vector = new RecordBinding("record:eastl::vector", "vector", "Vector", "eastl::vector")
        {
            ManagedBaseTypeName = "global::SharpCpp.Support.VectorBase<{0}>"
        };
        vector.GenericParameters.Add(new BindingGenericParameter("T", 0));
        var method = new FunctionBinding(
            "method:eastl::vector:GetCountCore",
            "GetCountCore",
            "GetCountCore",
            BindingType.UInt64,
            cppQualifiedName: "eastl::vector::GetCountCore")
        {
            AccessModifier = BindingAccessModifier.Protected,
            IsOverride = true,
            OwningRecordCppTypeName = vector.CppQualifiedName
        };
        method.SetMetadata(MetadataKeys.MarshallerExposedMethod, "eastl::vector<T>");
        vector.Members.Add(method);
        compilation.Members.Add(vector);

        DegradeUnsupportedSurfacePass.Apply(compilation);

        Assert.False(vector.IsIgnored(), vector.GetIgnoreReason());
        Assert.False(method.IsIgnored(), method.GetIgnoreReason());
    }

    [Fact]
    public void Apply_ignores_callback_return_types()
    {
        var compilation = new BindingCompilation("SharpCpp.Tests", "Test", "Bindings", "Native", ["Callbacks.h"]);
        var function = new FunctionBinding("function:GetCallback", "GetCallback", "GetCallback", CreateVoidCallbackType(), "GetCallback");
        compilation.Members.Add(function);

        DegradeUnsupportedSurfacePass.Apply(compilation);

        Assert.True(function.IsIgnored());
        Assert.Contains("callback return types", function.GetIgnoreReason(), StringComparison.Ordinal);
    }

    [Fact]
    public void Apply_ignores_callback_fields()
    {
        var compilation = new BindingCompilation("SharpCpp.Tests", "Test", "Bindings", "Native", ["Callbacks.h"]);
        var record = new RecordBinding("record:CallbackOwner", "CallbackOwner", "CallbackOwner", "CallbackOwner");
        var field = new FieldBinding("field:CallbackOwner::callback", "callback", "Callback", CreateVoidCallbackType(), "std::function<void(int)>", record.CppQualifiedName);
        record.Members.Add(field);
        compilation.Members.Add(record);

        DegradeUnsupportedSurfacePass.Apply(compilation);

        Assert.True(field.IsIgnored());
        Assert.Contains("callback fields", field.GetIgnoreReason(), StringComparison.Ordinal);
    }

    [Fact]
    public void Apply_ignores_out_or_ref_callback_parameters()
    {
        var compilation = new BindingCompilation("SharpCpp.Tests", "Test", "Bindings", "Native", ["Callbacks.h"]);
        var callbackType = CreateVoidCallbackType();
        var function = new FunctionBinding("function:ReplaceCallback", "ReplaceCallback", "ReplaceCallback", BindingType.Void, "ReplaceCallback");
        function.Parameters.Add(new ParameterBinding(
            "parameter:callback",
            "callback",
            "callback",
            callbackType,
            BindingParameterDirection.Ref,
            BindingParameterAbiKind.Reference,
            "std::function<void(int)>&"));
        compilation.Members.Add(function);

        DegradeUnsupportedSurfacePass.Apply(compilation);

        Assert.True(function.IsIgnored());
        Assert.Contains("callback parameters must be passed as in-value parameters", function.GetIgnoreReason(), StringComparison.Ordinal);
    }

    [Fact]
    public void Apply_ignores_member_overloads_that_collapse_through_aliases()
    {
        var compilation = new BindingCompilation("SharpCpp.Tests", "Test", "Bindings", "Native", ["Aliases.h"]);
        var record = new RecordBinding("record:Emitter", "Emitter", "Emitter", "Test::Emitter");
        var aliasType = BindingType.CreateConfigured("Test::AliasMap", "global::Test.Map<int, int>")
            .WithManagedTypeFormatOverride("AliasMap");
        var targetType = BindingType.CreateConfigured("Test::Map<int, int>", "global::Test.Map<int, int>");
        var aliasOverload = new FunctionBinding("method:Emitter::SendAlias", "Send", "Send", BindingType.Void, "Test::Emitter::Send")
        {
            OwningRecordCppTypeName = record.CppQualifiedName
        };
        var targetOverload = new FunctionBinding("method:Emitter::SendTarget", "Send", "Send", BindingType.Void, "Test::Emitter::Send")
        {
            OwningRecordCppTypeName = record.CppQualifiedName
        };
        aliasOverload.Parameters.Add(new ParameterBinding("parameter:alias", "value", "value", aliasType, cppTypeName: "Test::AliasMap"));
        targetOverload.Parameters.Add(new ParameterBinding("parameter:target", "value", "value", targetType, cppTypeName: "Test::Map<int, int>"));
        record.Members.Add(aliasOverload);
        record.Members.Add(targetOverload);
        compilation.Members.Add(record);

        DegradeUnsupportedSurfacePass.Apply(compilation);

        Assert.False(aliasOverload.IsIgnored(), aliasOverload.GetIgnoreReason());
        Assert.True(targetOverload.IsIgnored());
        Assert.Contains("managed callable signature", targetOverload.GetIgnoreReason(), StringComparison.Ordinal);
    }

    [Fact]
    public void Apply_ignores_duplicate_delegate_names_with_different_callback_signatures()
    {
        var compilation = new BindingCompilation("SharpCpp.Tests", "Test", "Bindings", "Native", ["Callbacks.h"]);
        var first = new FunctionBinding("function:first", "First", "First", BindingType.Void, "First");
        first.Parameters.Add(new ParameterBinding("parameter:first:callback", "callback", "callback", CreateVoidCallbackType())
        {
            UseCallbackDelegate = true,
            CallbackDelegateTypeName = "SharedCallback"
        });
        var second = new FunctionBinding("function:second", "Second", "Second", BindingType.Void, "Second");
        second.Parameters.Add(new ParameterBinding(
            "parameter:second:callback",
            "callback",
            "callback",
            BindingType.CreateCallback(
                "std::function<void(float)>",
                "global::System.Action<{0}>",
                managedMarshaller: null,
                BindingType.Void,
                [BindingType.Single]))
        {
            UseCallbackDelegate = true,
            CallbackDelegateTypeName = "SharedCallback"
        });
        compilation.Members.Add(first);
        compilation.Members.Add(second);

        DegradeUnsupportedSurfacePass.Apply(compilation);

        Assert.True(first.IsIgnored());
        Assert.True(second.IsIgnored());
        Assert.Contains("incompatible callback signatures", first.GetIgnoreReason(), StringComparison.Ordinal);
    }

    [Fact]
    public void Apply_keeps_unnamed_delegate_overloads_with_different_callback_signatures()
    {
        var compilation = new BindingCompilation("SharpCpp.Tests", "Test", "Bindings", "Native", ["Callbacks.h"]);
        var first = new FunctionBinding("function:first", "Invoke", "Invoke", BindingType.Void, "Invoke")
        {
            ImportMethodName = "Invoke"
        };
        first.Parameters.Add(new ParameterBinding("parameter:first:callback", "callback", "callback", CreateVoidCallbackType())
        {
            UseCallbackDelegate = true
        });
        var second = new FunctionBinding("function:second", "Invoke", "Invoke", BindingType.Void, "Invoke")
        {
            ImportMethodName = "Invoke_2"
        };
        second.Parameters.Add(new ParameterBinding(
            "parameter:second:callback",
            "callback",
            "callback",
            BindingType.CreateCallback(
                "std::function<void(float)>",
                "global::System.Action<{0}>",
                managedMarshaller: null,
                BindingType.Void,
                [BindingType.Single]))
        {
            UseCallbackDelegate = true
        });
        compilation.Members.Add(first);
        compilation.Members.Add(second);

        DegradeUnsupportedSurfacePass.Apply(compilation);

        Assert.False(first.IsIgnored(), first.GetIgnoreReason());
        Assert.False(second.IsIgnored(), second.GetIgnoreReason());
    }

    [Fact]
    public void Apply_ignores_invalid_external_callback_delegate_type_name()
    {
        var compilation = new BindingCompilation("SharpCpp.Tests", "Test", "Bindings", "Native", ["Callbacks.h"]);
        var function = new FunctionBinding("function:callback", "UseCallback", "UseCallback", BindingType.Void, "UseCallback");
        function.Parameters.Add(new ParameterBinding("parameter:callback", "callback", "callback", CreateVoidCallbackType())
        {
            UseCallbackDelegate = true,
            CallbackDelegateTypeName = "global::"
        });
        compilation.Members.Add(function);

        DegradeUnsupportedSurfacePass.Apply(compilation);

        Assert.True(function.IsIgnored());
        Assert.Contains("external callback delegate type name 'global::' is not a valid managed type name", function.GetIgnoreReason(), StringComparison.Ordinal);
    }

    [Fact]
    public void Apply_keeps_generated_callback_helpers_with_configured_value_callback_argument_when_public_type_matches_marshaller_type()
    {
        var compilation = new BindingCompilation("SharpCpp.Tests", "Test", "Bindings", "Native", ["Callbacks.h"]);
        var configuredType = BindingType.CreateConfigured("Test::Vector2", "global::Test.Vector2", "global::Test.Vector2");
        var callbackType = BindingType.CreateCallback(
            "std::function<void(Vector2)>",
            "global::System.Action<{0}>",
            managedMarshaller: null,
            BindingType.Void,
            [configuredType]);
        var function = new FunctionBinding("function:UseCallback", "UseCallback", "UseCallback", BindingType.Void, "UseCallback");
        function.Parameters.Add(new ParameterBinding("parameter:callback", "callback", "callback", callbackType));
        compilation.Members.Add(function);

        DegradeUnsupportedSurfacePass.Apply(compilation);

        Assert.False(function.IsIgnored(), function.GetIgnoreReason());
    }

    [Fact]
    public void Apply_ignores_generated_callback_helpers_with_conversion_required_configured_callback_argument_types()
    {
        var compilation = new BindingCompilation("SharpCpp.Tests", "Test", "Bindings", "Native", ["Callbacks.h"]);
        var configuredType = BindingType.CreateConfigured("std::string_view", "global::System.ReadOnlySpan<byte>", "global::SharpCpp.Support.Utf8SpanAbiMarshaller");
        var callbackType = BindingType.CreateCallback(
            "std::function<void(std::string_view)>",
            "global::System.Action<{0}>",
            managedMarshaller: null,
            BindingType.Void,
            [configuredType]);
        var function = new FunctionBinding("function:UseCallback", "UseCallback", "UseCallback", BindingType.Void, "UseCallback");
        function.Parameters.Add(new ParameterBinding("parameter:callback", "callback", "callback", callbackType));
        compilation.Members.Add(function);

        DegradeUnsupportedSurfacePass.Apply(compilation);

        Assert.True(function.IsIgnored());
        Assert.Contains("generated callback marshaller cannot convert callback parameter type", function.GetIgnoreReason(), StringComparison.Ordinal);
    }

    [Fact]
    public void Apply_ignores_generated_callback_helpers_with_value_record_callback_argument_types()
    {
        var compilation = new BindingCompilation("SharpCpp.Tests", "Test", "Bindings", "Native", ["Callbacks.h"]);
        var valueRecord = new RecordBinding("record:Vector2", "Vector2", "global::Test.Vector2", "Test::Vector2", isHandleType: false);
        var callbackType = BindingType.CreateCallback(
            "std::function<void(Vector2)>",
            "global::System.Action<{0}>",
            managedMarshaller: null,
            BindingType.Void,
            [BindingType.CreateHandle("Test::Vector2", valueRecord.ManagedName, valueRecord.SymbolId)]);
        var function = new FunctionBinding("function:UseCallback", "UseCallback", "UseCallback", BindingType.Void, "UseCallback");
        function.Parameters.Add(new ParameterBinding("parameter:callback", "callback", "callback", callbackType));
        compilation.Members.Add(valueRecord);
        compilation.Members.Add(function);

        DegradeUnsupportedSurfacePass.Apply(compilation);

        Assert.True(function.IsIgnored());
        Assert.Contains("generated callback marshaller cannot convert callback parameter type", function.GetIgnoreReason(), StringComparison.Ordinal);
    }

    private static BindingType CreateVoidCallbackType()
    {
        return BindingType.CreateCallback(
            "std::function<void(int)>",
            "global::System.Action<{0}>",
            managedMarshaller: null,
            BindingType.Void,
            [BindingType.Int32]);
    }
}
