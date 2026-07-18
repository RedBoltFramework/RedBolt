using SharpCpp.Model.Ast;
using SharpCpp.Pipeline;

using Xunit;

namespace SharpCpp.Tests.Model;

public sealed class BindingCompilationGenericInstantiationPassTests
{
    [Fact]
    public void Apply_collects_record_instantiations_from_callback_parameter_types()
    {
        var compilation = new BindingCompilation("SharpCpp.Tests", "Test", "Bindings", "Native", ["Callbacks.h"]);
        var key = new RecordBinding("record:StringHash", "StringHash", "StringHash", "Test::StringHash");
        var value = new RecordBinding("record:Variant", "Variant", "Variant", "Test::Variant");
        var map = new RecordBinding("record:hash_map", "hash_map", "Map", "eastl::hash_map")
        {
            ManagedTypeFormat = "Map<{0}, {1}>",
            ManagedBaseTypeName = "global::SharpCpp.Support.UnorderedMapBase<{0}, {1}>"
        };
        map.GenericParameters.Add(new BindingGenericParameter("TKey", 0));
        map.GenericParameters.Add(new BindingGenericParameter("TValue", 1));
        compilation.Members.Add(key);
        compilation.Members.Add(value);
        compilation.Members.Add(map);

        var mapType = BindingType.CreateHandle(
            "eastl::hash_map<Test::StringHash, Test::Variant>*",
            map.ManagedName,
            map.SymbolId,
            genericArguments: [key.HandleType, value.HandleType]);
        var callbackType = BindingType.CreateCallback(
            "eastl::function<void(Test::StringHash, eastl::hash_map<Test::StringHash, Test::Variant>&)>",
            "global::System.Action<{0}, {1}>",
            managedMarshaller: null,
            BindingType.Void,
            [key.HandleType, mapType]);
        var subscribe = new FunctionBinding("function:Subscribe", "Subscribe", "Subscribe", BindingType.Void, "Subscribe");
        subscribe.Parameters.Add(new ParameterBinding("parameter:handler", "handler", "handler", callbackType));
        compilation.Members.Add(subscribe);

        BindingCompilationGenericInstantiationPass.Apply(compilation);

        var instantiation = Assert.Single(map.SupportedGenericInstantiations);
        Assert.Equal(new[] { key.SymbolId, value.SymbolId }, instantiation.TypeArguments.Select(static type => type.RecordId));
    }

    [Fact]
    public void Apply_does_not_collect_open_generic_record_member_types_as_concrete_instantiations()
    {
        var compilation = new BindingCompilation("SharpCpp.Tests", "Test", "Bindings", "Native", ["Vector.h"]);
        var vector = new RecordBinding("record:vector", "vector", "Vector", "eastl::vector")
        {
            ManagedTypeFormat = "Vector<{0}>",
            ManagedBaseTypeName = "global::SharpCpp.Support.VectorBase<{0}>"
        };
        vector.GenericParameters.Add(new BindingGenericParameter("T", 0));
        var openVectorType = BindingType.CreateHandle(
            "eastl::vector<type-parameter-0-0>*",
            vector.ManagedName,
            vector.SymbolId,
            genericArguments: [BindingType.CreateConfigured("type-parameter-0-0", "type-parameter-0-0")]);
        var add = new FunctionBinding("method:vector:Add", "Add", "Add", BindingType.Void, "eastl::vector::Add")
        {
            OwningRecordCppTypeName = vector.CppQualifiedName
        };
        add.Parameters.Add(new ParameterBinding("parameter:self", "self", "self", openVectorType));
        vector.Members.Add(add);
        var rootFunction = new FunctionBinding("function:UseOpenVector", "UseOpenVector", "UseOpenVector", BindingType.Void, "UseOpenVector");
        rootFunction.Parameters.Add(new ParameterBinding("parameter:value", "value", "value", openVectorType));
        compilation.Members.Add(vector);
        compilation.Members.Add(rootFunction);

        BindingCompilationGenericInstantiationPass.Apply(compilation);

        Assert.Empty(vector.SupportedGenericInstantiations);
    }

    [Fact]
    public void ValidateBootstrapSurface_removes_instantiations_with_ignored_record_arguments()
    {
        var compilation = new BindingCompilation("SharpCpp.Tests", "Test", "Bindings", "Native", ["Map.h"]);
        var value = new RecordBinding("record:Detail::ActionMapping", "ActionMapping", "ActionMapping", "Test::Detail::ActionMapping");
        value.SetIgnoreReason("declaration is in an internal detail namespace: 'Test::Detail::ActionMapping'");
        var map = new RecordBinding("record:hash_map", "hash_map", "UnorderedMap", "eastl::hash_map")
        {
            ManagedTypeFormat = "UnorderedMap<{0}, {1}>",
            ManagedBaseTypeName = "global::SharpCpp.Support.UnorderedMapBase<{0}, {1}>"
        };
        map.GenericParameters.Add(new BindingGenericParameter("TKey", 0));
        map.GenericParameters.Add(new BindingGenericParameter("TValue", 1));
        map.SupportedGenericInstantiations.Add(new RecordGenericInstantiationBinding([
            BindingType.Int32,
            value.HandleType
        ]));
        compilation.Members.Add(value);
        compilation.Members.Add(map);

        ValidateBootstrapSurfacePass.Apply(compilation);

        Assert.Empty(map.SupportedGenericInstantiations);
        Assert.Equal("generic record 'UnorderedMap' has no supported concrete instantiations", map.GetIgnoreReason());
    }

    [Fact]
    public void ValidateBootstrapSurface_removes_instantiations_with_ref_struct_arguments()
    {
        var compilation = new BindingCompilation("SharpCpp.Tests", "Test", "Bindings", "Native", ["Vector.h"]);
        var vector = new RecordBinding("record:vector", "vector", "Vector", "eastl::vector")
        {
            ManagedTypeFormat = "Vector<{0}>",
            ManagedBaseTypeName = "global::SharpCpp.Support.VectorBase<{0}>"
        };
        vector.GenericParameters.Add(new BindingGenericParameter("T", 0));
        vector.SupportedGenericInstantiations.Add(new RecordGenericInstantiationBinding([
            BindingType.CreateConfigured(
                "const char*",
                "global::System.ReadOnlySpan<byte>",
                primitiveKind: BindingPrimitiveKind.Unsupported)
        ]));
        compilation.Members.Add(vector);

        ValidateBootstrapSurfacePass.Apply(compilation);

        Assert.Empty(vector.SupportedGenericInstantiations);
        Assert.Equal("generic record 'Vector' has no supported concrete instantiations", vector.GetIgnoreReason());
    }

    [Fact]
    public void ValidateBootstrapSurface_keeps_instantiations_with_hidden_ref_struct_arguments()
    {
        var compilation = new BindingCompilation("SharpCpp.Tests", "Test", "Bindings", "Native", ["Vector.h"]);
        var element = new RecordBinding("record:Object", "Object", "Object", "Test::Object");
        var vector = new RecordBinding("record:vector", "vector", "Vector", "eastl::vector")
        {
            ManagedTypeFormat = "Vector<{0}>",
            ManagedBaseTypeName = "global::SharpCpp.Support.VectorBase<{0}>"
        };
        vector.GenericParameters.Add(new BindingGenericParameter("T", 0));
        vector.GenericParameters.Add(new BindingGenericParameter("TAllocator", 1));
        vector.SupportedGenericInstantiations.Add(new RecordGenericInstantiationBinding([
            element.HandleType,
            BindingType.CreateConfigured(
                "const char*",
                "global::System.ReadOnlySpan<byte>",
                primitiveKind: BindingPrimitiveKind.Unsupported)
        ]));
        compilation.Members.Add(element);
        compilation.Members.Add(vector);

        ValidateBootstrapSurfacePass.Apply(compilation);

        Assert.False(vector.IsIgnored(), vector.GetIgnoreReason());
        Assert.Single(vector.SupportedGenericInstantiations);
    }

    [Fact]
    public void ValidateBootstrapSurface_keeps_instantiations_with_nonfatal_ignored_record_arguments()
    {
        var compilation = new BindingCompilation("SharpCpp.Tests", "Test", "Bindings", "Native", ["Vector.h"]);
        var element = new RecordBinding("record:Object", "Object", "Object", "Test::Object");
        element.SetIgnoreReason("record 'Test::Object' has no user-defined API surface (it is likely a template utility type)");
        var vector = new RecordBinding("record:vector", "vector", "Vector", "eastl::vector")
        {
            ManagedTypeFormat = "Vector<{0}>",
            ManagedBaseTypeName = "global::SharpCpp.Support.VectorBase<{0}>"
        };
        vector.GenericParameters.Add(new BindingGenericParameter("T", 0));
        vector.SupportedGenericInstantiations.Add(new RecordGenericInstantiationBinding([element.HandleType]));
        compilation.Members.Add(element);
        compilation.Members.Add(vector);

        ValidateBootstrapSurfacePass.Apply(compilation);

        Assert.False(vector.IsIgnored(), vector.GetIgnoreReason());
        Assert.Single(vector.SupportedGenericInstantiations);
    }

    [Fact]
    public void ValidateBootstrapSurface_keeps_instantiated_generic_methods_on_non_generic_records()
    {
        var compilation = new BindingCompilation("SharpCpp.Tests", "Test", "Bindings", "Native", ["Object.h"]);
        var objectRecord = new RecordBinding("record:Object", "Object", "Object", "Test::Object");
        var subscribe = new FunctionBinding(
            "method:Object:Subscribe:T",
            "Subscribe",
            "Subscribe",
            BindingType.Void,
            cppQualifiedName: "Test::Object::Subscribe")
        {
            OwningRecordCppTypeName = objectRecord.CppQualifiedName
        };
        subscribe.GenericParameters.Add(new BindingGenericParameter("T", 0));
        subscribe.Parameters.Add(new ParameterBinding(
            "parameter:Object:Subscribe:handler",
            "handler",
            "handler",
            BindingType.CreateGenericParameter("T"),
            cppTypeName: "T"));
        subscribe.SupportedGenericInstantiations.Add(new RecordGenericInstantiationBinding([BindingType.Int32]));
        objectRecord.Members.Add(subscribe);
        compilation.Members.Add(objectRecord);

        ValidateBootstrapSurfacePass.Apply(compilation);

        Assert.False(subscribe.IsIgnored(), subscribe.GetIgnoreReason());
        Assert.Single(subscribe.SupportedGenericInstantiations);
    }

    [Fact]
    public void ValidateBootstrapSurface_keeps_suppressed_placeholder_instantiations_under_configured_namespace()
    {
        var compilation = new BindingCompilation("SharpCpp.Tests", "Test", "Bindings", "Native", ["Vector.h"])
        {
            NamespaceMappings = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Test::External"] = "Test.External"
            }
        };
        var element = new RecordBinding("record:Test::External::Payload", "Payload", "Payload", "Test::External::Payload");
        element.SetMetadata(MetadataKeys.PlaceholderRecord, true);
        element.SetMetadata(MetadataKeys.SuppressEmission, true);
        var vector = CreateGenericVector(element.HandleType);
        compilation.Members.Add(element);
        compilation.Members.Add(vector);

        ValidateBootstrapSurfacePass.Apply(compilation);

        Assert.False(vector.IsIgnored(), vector.GetIgnoreReason());
        Assert.Single(vector.SupportedGenericInstantiations);
    }

    [Fact]
    public void ValidateBootstrapSurface_removes_suppressed_placeholder_instantiations_outside_configured_namespaces()
    {
        var compilation = new BindingCompilation("SharpCpp.Tests", "Test", "Bindings", "Native", ["Vector.h"])
        {
            NamespaceMappings = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Test"] = "Test"
            }
        };
        var element = new RecordBinding("record:External::Payload", "Payload", "Payload", "External::Payload");
        element.SetMetadata(MetadataKeys.PlaceholderRecord, true);
        element.SetMetadata(MetadataKeys.SuppressEmission, true);
        var vector = CreateGenericVector(element.HandleType);
        compilation.Members.Add(element);
        compilation.Members.Add(vector);

        ValidateBootstrapSurfacePass.Apply(compilation);

        Assert.Empty(vector.SupportedGenericInstantiations);
        Assert.Equal("generic record 'Vector' has no supported concrete instantiations", vector.GetIgnoreReason());
    }

    [Fact]
    public void ValidateBootstrapSurface_removes_non_suppressed_placeholder_instantiations_even_under_configured_namespace()
    {
        var compilation = new BindingCompilation("SharpCpp.Tests", "Test", "Bindings", "Native", ["Vector.h"])
        {
            NamespaceMappings = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Test"] = "Test"
            }
        };
        var element = new RecordBinding("record:Test::Payload", "Payload", "Payload", "Test::Payload");
        element.SetMetadata(MetadataKeys.PlaceholderRecord, true);
        var vector = CreateGenericVector(element.HandleType);
        compilation.Members.Add(element);
        compilation.Members.Add(vector);

        ValidateBootstrapSurfacePass.Apply(compilation);

        Assert.Empty(vector.SupportedGenericInstantiations);
        Assert.Equal("generic record 'Vector' has no supported concrete instantiations", vector.GetIgnoreReason());
    }

    private static RecordBinding CreateGenericVector(BindingType elementType)
    {
        var vector = new RecordBinding("record:vector", "vector", "Vector", "eastl::vector")
        {
            ManagedTypeFormat = "Vector<{0}>",
            ManagedBaseTypeName = "global::SharpCpp.Support.VectorBase<{0}>"
        };
        vector.GenericParameters.Add(new BindingGenericParameter("T", 0));
        vector.SupportedGenericInstantiations.Add(new RecordGenericInstantiationBinding([elementType]));
        return vector;
    }
}
