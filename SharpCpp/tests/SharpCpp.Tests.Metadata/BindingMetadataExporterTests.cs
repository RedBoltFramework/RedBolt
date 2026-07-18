using SharpCpp.Metadata.Export;
using SharpCpp.Model.Ast;

using Xunit;

namespace SharpCpp.Tests.Metadata;

public sealed class BindingMetadataExporterTests
{
    [Fact]
    public void Export_sorts_types_functions_imports_and_headers_deterministically()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "SharpCpp.Sample",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "z.hpp", "a.hpp" });

        var zoo = new RecordBinding("record:Zoo", "Zoo", "Zoo");
        var alpha = new RecordBinding("record:Alpha", "Alpha", "Alpha");
        var zebra = new FunctionBinding("function:zebra", "zebra", "Zebra", alpha.HandleType, "entry_zebra");
        zebra.Parameters.Add(new ParameterBinding("parameter:zebra:value", "value", "value", BindingType.Int32));
        var add = new FunctionBinding("function:add", "add", "Add", BindingType.Int32, "entry_add");

        compilation.Members.Add(zoo);
        compilation.Members.Add(alpha);
        compilation.Members.Add(zebra);
        compilation.Members.Add(add);

        var document = BindingMetadataExporter.Export(
            compilation,
            new BindingMetadataExportOptions(
                PackageId: "SharpCpp.Sample",
                Imports: new[]
                {
                    new BindingMetadataImport("Zoo.Package", "z"),
                    new BindingMetadataImport("Alpha.Package", "a")
                }));

        Assert.Equal(new[] { "Alpha", "Zoo" }, document.Types.Select(static type => type.ManagedName).Take(2));
        Assert.Equal(new[] { "Add", "Zebra" }, document.Functions.Select(static function => function.ManagedName));
        Assert.Equal(new[] { "Alpha.Package", "Zoo.Package" }, document.Imports.Select(static importItem => importItem.Package));
        Assert.Equal(new[] { "a.hpp", "z.hpp" }, document.Fingerprint.HeaderFiles);
    }

    [Fact]
    public void Serialize_round_trips_metadata_document()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "SharpCpp.Sample",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "sample.hpp" });
        compilation.Members.Add(new RecordBinding("record:Widget", "Widget", "Widget", marshallerTypeName: "WidgetMarshaller"));

        var original = BindingMetadataExporter.Export(compilation);
        var json = BindingMetadataSerializer.Serialize(original);
        var roundTripped = BindingMetadataSerializer.Deserialize(json);

        Assert.Contains("\"schema_version\": 8", json, StringComparison.Ordinal);
        Assert.EndsWith(Environment.NewLine, json, StringComparison.Ordinal);
        Assert.Equal(original.SchemaVersion, roundTripped.SchemaVersion);
        Assert.Equal(original.GeneratorVersion, roundTripped.GeneratorVersion);
        Assert.Equal(original.SupportRuntimeVersion, roundTripped.SupportRuntimeVersion);
        Assert.Equal(original.Source, roundTripped.Source);
        Assert.Equal(original.Imports, roundTripped.Imports);
        Assert.Equal(original.Fingerprint.Phase2ClosureHash, roundTripped.Fingerprint.Phase2ClosureHash);
        Assert.Equal(original.Fingerprint.ClangArgsHash, roundTripped.Fingerprint.ClangArgsHash);
        Assert.Equal(original.Fingerprint.AttributeVocabularyHash, roundTripped.Fingerprint.AttributeVocabularyHash);
        Assert.Equal(original.Fingerprint.HeaderFiles, roundTripped.Fingerprint.HeaderFiles);
        Assert.Equal(original.Types.Select(static type => type.SymbolId), roundTripped.Types.Select(static type => type.SymbolId));
        Assert.Equal(original.Types.Select(static type => type.IsFlags), roundTripped.Types.Select(static type => type.IsFlags));
        Assert.Equal(
            original.Types.Select(static type => type.Values?.Count ?? 0),
            roundTripped.Types.Select(static type => type.Values?.Count ?? 0));
        Assert.Equal(
            original.Types.Select(static type => type.Members?.Count ?? 0),
            roundTripped.Types.Select(static type => type.Members?.Count ?? 0));
        Assert.Equal(original.Functions, roundTripped.Functions);
        Assert.Equal(original.Aliases, roundTripped.Aliases);
    }

    [Fact]
    public void Export_round_trips_dynamic_array_metadata()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "SharpCpp.Sample",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "arrays.hpp" });
        var record = new RecordBinding("record:shared_array", "shared_array", "SharedArray", "ea::shared_array")
        {
            ManagedTypeFormat = "Ea.SharedArray<{0}>",
            ManagedNamespace = "Ea",
            ManagedBaseTypeName = "global::SharpCpp.Support.SharedArrayBase<{0}>"
        };
        record.GenericParameters.Add(new BindingGenericParameter("T", 0));
        record.SetMetadata(MetadataKeys.DynamicArrayRecord, true);
        compilation.Members.Add(record);

        var arrayType = BindingType.CreateHandle(
            "ea::shared_array<int>*",
            "SharedArray",
            record.SymbolId,
            genericArguments: [BindingType.Int32])
            .WithDynamicArray(BindingType.Int32);
        var function = new FunctionBinding("function:GetArray", "GetArray", "GetArray", arrayType, "entry_get_array");
        compilation.Members.Add(function);

        var original = BindingMetadataExporter.Export(compilation);
        var json = BindingMetadataSerializer.Serialize(original);
        var roundTripped = BindingMetadataSerializer.Deserialize(json);

        Assert.True(Assert.Single(roundTripped.Types).IsDynamicArray);
        Assert.Equal("int", Assert.Single(roundTripped.Functions).ReturnType.DynamicArrayElementType!.PublicType);
    }

    [Fact]
    public void Export_round_trips_callback_type_metadata()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "SharpCpp.Sample",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "callbacks.hpp" });
        var callbackType = BindingType.CreateCallback(
            "std::function<int(float)>",
            "global::System.Func<{0}, {1}>",
            "global::SharpCpp.Sample.CallbackMarshaller",
            BindingType.Int32,
            [BindingType.Single]);
        var function = new FunctionBinding("function:UseCallback", "UseCallback", "UseCallback", BindingType.Void, "entry_use_callback");
        function.Parameters.Add(new ParameterBinding("parameter:callback", "callback", "callback", callbackType));
        compilation.Members.Add(function);

        var original = BindingMetadataExporter.Export(compilation);
        var json = BindingMetadataSerializer.Serialize(original);
        var roundTripped = BindingMetadataSerializer.Deserialize(json);
        var parameterType = Assert.Single(roundTripped.Functions).Parameters.Single().Type;

        Assert.True(parameterType.IsCallback);
        Assert.Equal("std::function<int(float)>", parameterType.CppType);
        Assert.Equal("global::System.Func<float, int>", parameterType.PublicType);
        Assert.Equal("global::SharpCpp.Sample.CallbackMarshaller", parameterType.ManagedMarshaller);
        Assert.Equal("int", parameterType.CallbackReturnType!.CppType);
        Assert.Equal("float", Assert.Single(parameterType.CallbackParameterTypes!).CppType);
    }

    [Fact]
    public void Export_round_trips_callback_delegate_parameter_policy()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "SharpCpp.Sample",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "callbacks.hpp" });
        var callbackType = BindingType.CreateCallback(
            "std::function<void(int)>",
            "global::System.Action<{0}>",
            managedMarshaller: null,
            BindingType.Void,
            [BindingType.Int32]);
        var function = new FunctionBinding("function:UseCallback", "UseCallback", "UseCallback", BindingType.Void, "entry_use_callback");
        function.Parameters.Add(new ParameterBinding("parameter:callback", "callback", "callback", callbackType)
        {
            UseCallbackDelegate = true,
            CallbackDelegateTypeName = "ProgressCallback"
        });
        compilation.Members.Add(function);

        var original = BindingMetadataExporter.Export(compilation);
        var json = BindingMetadataSerializer.Serialize(original);
        var roundTripped = BindingMetadataSerializer.Deserialize(json);
        var parameter = Assert.Single(roundTripped.Functions).Parameters.Single();

        Assert.True(parameter.UseCallbackDelegate);
        Assert.Equal("ProgressCallback", parameter.CallbackDelegateTypeName);
    }

    [Fact]
    public void Export_includes_aliases_with_fully_qualified_concrete_targets()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "Urho3D",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "aliases.hpp" });
        var vectorRecord = new RecordBinding("record:ea_vector", "vector", "Vector", "eastl::vector")
        {
            ManagedNamespace = "Ea",
            ManagedTypeFormat = "Vector<{0}>"
        };
        vectorRecord.GenericParameters.Add(new BindingGenericParameter("T", 0));
        compilation.Members.Add(vectorRecord);

        var vector3Type = BindingType.CreateConfigured(
            "Urho3D::Vector3",
            "global::Urho3D.Math.Vector3",
            "global::Urho3D.Math.Vector3");
        var pathPointsType = BindingType.CreateHandle(
            "eastl::vector<Urho3D::Vector3>",
            "Vector",
            vectorRecord.SymbolId,
            genericArguments: new[] { vector3Type });
        var alias = new BindingTypeAlias("Urho3D::PathPoints", "PathPoints", "PathPoints", pathPointsType)
        {
            ManagedNamespace = "Urho3D"
        };
        compilation.TypeAliases.Add(alias);

        var document = BindingMetadataExporter.Export(compilation);
        var exportedAlias = Assert.Single(document.Aliases!);

        Assert.Equal("PathPoints", exportedAlias.ManagedName);
        Assert.Equal("Urho3D", exportedAlias.ManagedNamespace);
        Assert.Equal("global::Ea.Vector<global::Urho3D.Math.Vector3>", exportedAlias.TargetType.PublicType);
        Assert.Single(exportedAlias.TargetType.GenericArguments!);
        Assert.Equal("global::Urho3D.Math.Vector3", exportedAlias.TargetType.GenericArguments![0].PublicType);
    }

    [Fact]
    public void Export_uses_shadow_projection_for_type_and_alias_namespaces()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Rbfx.Bindings",
            "Urho3D",
            "Bindings",
            "SharpCpp.Rbfx.Native",
            new[] { "Urho3D/Urho3DAll.h" });
        var context = new RecordBinding("record:Context", "Context", "Context", "Urho3D::Context")
        {
            ManagedNamespace = "Urho3D"
        };
        var alias = new BindingTypeAlias("Urho3D::ContextPtr", "ContextPtr", "ContextPtr", context.HandleType)
        {
            ManagedNamespace = "Urho3D"
        };
        var factory = new FunctionBinding("function:CreateContext", "CreateContext", "CreateContext", context.HandleType, "scrbfx_CreateContext");
        compilation.Members.Add(context);
        compilation.Members.Add(factory);
        compilation.TypeAliases.Add(alias);

        var projection = new ManagedBindingProjection(compilation.ManagedNamespace);
        projection.AddDeclaration("Urho3D.Core", context);
        compilation.SetMetadata(MetadataKeys.ManagedBindingProjection, projection);

        var document = BindingMetadataExporter.Export(compilation);
        var exportedType = Assert.Single(document.Types);
        var exportedFunction = Assert.Single(document.Functions);
        var exportedAlias = Assert.Single(document.Aliases!);

        Assert.Equal("Urho3D.Core", exportedType.ManagedNamespace);
        Assert.Equal("global::Urho3D.Core.Context", exportedFunction.ReturnType.PublicType);
        Assert.Equal("Urho3D", exportedAlias.ManagedNamespace);
        Assert.Equal("global::Urho3D.Core.Context", exportedAlias.TargetType.PublicType);
    }

    [Fact]
    public void Export_includes_enum_flag_status_and_values()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "SharpCpp.Sample",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "enum.hpp" });
        var permissions = new EnumBinding("enum:permissions", "permissions", "Permissions", "unsigned int", underlyingPrimitiveKind: BindingPrimitiveKind.UInt32)
        {
            IsFlags = true
        };
        permissions.Values.Add(new EnumValueBinding("read", "Read", "1u"));
        permissions.Values.Add(new EnumValueBinding("write", "Write", "2u"));
        compilation.Members.Add(permissions);

        var document = BindingMetadataExporter.Export(compilation);
        var exportedEnum = Assert.Single(document.Types);

        Assert.True(exportedEnum.IsFlags);
        Assert.Collection(
            exportedEnum.Values!,
            value =>
            {
                Assert.Equal("read", value.NativeName);
                Assert.Equal("Read", value.ManagedName);
                Assert.Equal("1u", value.ValueExpression);
            },
            value =>
            {
                Assert.Equal("write", value.NativeName);
                Assert.Equal("Write", value.ManagedName);
                Assert.Equal("2u", value.ValueExpression);
            });
    }

    [Fact]
    public void Export_includes_record_members()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "SharpCpp.Sample",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "record.hpp" });
        var record = new RecordBinding("record:Counter", "counter", "Counter");
        record.Members.Add(new FieldBinding("field:counter:value", "value", "Value", BindingType.Int32, "int", "counter", isReadOnly: true));
        record.Members.Add(new FunctionBinding("ctor:counter", "counter", "Counter", record.HandleType, "entry_ctor") { IsConstructor = true });
        record.Members.Add(new FunctionBinding("method:counter:get", "get_value", "GetValue", BindingType.Int32, "entry_get") { OwningRecordCppTypeName = "counter" });
        compilation.Members.Add(record);

        var document = BindingMetadataExporter.Export(compilation);
        var exportedRecord = Assert.Single(document.Types);

        Assert.Collection(
            exportedRecord.Members!,
            member =>
            {
                Assert.Equal("constructor", member.Kind);
                Assert.Equal("Counter", member.ManagedName);
                Assert.True(member.IsConstructor);
            },
            member =>
            {
                Assert.Equal("method", member.Kind);
                Assert.Equal("GetValue", member.ManagedName);
                Assert.Equal("int", member.Type!.PublicType);
            },
            member =>
            {
                Assert.Equal("field", member.Kind);
                Assert.Equal("Value", member.ManagedName);
                Assert.True(member.IsReadOnly);
            });
    }

    [Fact]
    public void Export_includes_parameter_defaults_and_operator_metadata()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "SharpCpp.Sample",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "operators.hpp" });
        var record = new RecordBinding("record:OperatorBox", "operator_box", "OperatorBox");
        var plus = new FunctionBinding("method:operator_box:operator+", "operator+", "OperatorPlus", BindingType.Int32, "entry_plus")
        {
            OperatorToken = "+"
        };
        plus.Parameters.Add(new ParameterBinding("parameter:plus:value", "value", "value", BindingType.Int32) { ManagedDefaultValueExpression = "23" });
        var convert = new FunctionBinding("method:operator_box:operator bool", "operator bool", "OperatorToBool", BindingType.Boolean, "entry_bool")
        {
            IsConversionOperator = true,
            IsExplicitConversionOperator = true
        };
        record.Members.Add(plus);
        record.Members.Add(convert);
        compilation.Members.Add(record);

        var document = BindingMetadataExporter.Export(compilation);
        var exportedRecord = Assert.Single(document.Types);
        var exportedMembers = exportedRecord.Members!;
        var plusMember = Assert.Single(exportedMembers.Where(static member => member.OperatorToken == "+"));
        var conversionMember = Assert.Single(exportedMembers.Where(static member => member.IsConversionOperator));

        Assert.Equal("23", Assert.Single(plusMember.Parameters!).DefaultValueExpression);
        Assert.True(conversionMember.IsExplicitConversionOperator);
    }

    [Fact]
    public void Export_includes_owned_handle_return_flag()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "SharpCpp.Sample",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "factory.hpp" });
        var record = new RecordBinding("record:Widget", "widget", "Widget");
        var factory = new FunctionBinding("function:create_widget", "create_widget", "CreateWidget", record.HandleType, "entry_create")
        {
            ReturnsOwnedHandle = true
        };
        compilation.Members.Add(record);
        compilation.Members.Add(factory);

        var document = BindingMetadataExporter.Export(compilation);
        var exportedFunction = Assert.Single(document.Functions);

        Assert.True(exportedFunction.ReturnsOwnedHandle);
    }

    [Fact]
    public void Export_includes_record_runtime_policy_and_field_owned_handle_flag()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "SharpCpp.Sample",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "runtime.hpp" });
        var record = new RecordBinding("record:Widget", "widget", "Widget")
        {
            ManagedBaseTypeName = "global::Urho3D.Interop.RefCountedNativeObject",
            ManagedInstanceCacheFactoryExpression = "global::Urho3D.Interop.RefCountedInstanceCache.Shared",
            ManagedInstanceCacheTypeName = "global::SharpCpp.Support.INativeInstanceCache",
            EmitDefaultNativeReleaseOverride = false
        };
        var field = new FieldBinding("field:widget:payload", "payload", "Payload", record.HandleType, "widget*", "widget")
        {
            ReturnsOwnedHandle = true
        };
        record.Members.Add(field);
        compilation.Members.Add(record);

        var document = BindingMetadataExporter.Export(compilation);
        var exportedRecord = Assert.Single(document.Types);
        var exportedField = Assert.Single(exportedRecord.Members!);

        Assert.Equal("global::Urho3D.Interop.RefCountedInstanceCache.Shared", exportedRecord.ManagedInstanceCacheFactoryExpression);
        Assert.Equal("global::SharpCpp.Support.INativeInstanceCache", exportedRecord.ManagedInstanceCacheTypeName);
        Assert.False(exportedRecord.EmitDefaultNativeReleaseOverride);
        Assert.True(exportedField.GetterReturnsOwnedHandle);
    }

    [Fact]
    public void Export_skips_ignored_surface()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "SharpCpp.Sample",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "ignored.hpp" });

        var retainedRecord = new RecordBinding("record:Retained", "retained", "Retained");
        var ignoredRecord = new RecordBinding("record:Ignored", "ignored", "Ignored");
        ignoredRecord.SetIgnoreReason("not yet supported");

        var retainedMethod = new FunctionBinding("method:retained:get", "get_value", "GetValue", BindingType.Int32, "entry_get")
        {
            OwningRecordCppTypeName = "retained"
        };
        var ignoredMethod = new FunctionBinding("method:retained:skip", "skip", "Skip", BindingType.Int32, "entry_skip")
        {
            OwningRecordCppTypeName = "retained"
        };
        ignoredMethod.SetIgnoreReason("unsupported signature");
        retainedRecord.Members.Add(retainedMethod);
        retainedRecord.Members.Add(ignoredMethod);

        var retainedFunction = new FunctionBinding("function:keep", "keep", "Keep", BindingType.Int32, "entry_keep");
        var ignoredFunction = new FunctionBinding("function:skip", "skip", "Skip", BindingType.Int32, "entry_skip");
        ignoredFunction.SetIgnoreReason("manual stub");

        compilation.Members.Add(retainedRecord);
        compilation.Members.Add(ignoredRecord);
        compilation.Members.Add(retainedFunction);
        compilation.Members.Add(ignoredFunction);

        var document = BindingMetadataExporter.Export(compilation);

        var exportedRecord = Assert.Single(document.Types);
        Assert.Equal("Retained", exportedRecord.ManagedName);
        Assert.Single(exportedRecord.Members!);
        Assert.Equal("GetValue", exportedRecord.Members![0].ManagedName);
        Assert.Single(document.Functions);
        Assert.Equal("Keep", document.Functions[0].ManagedName);
    }
}
