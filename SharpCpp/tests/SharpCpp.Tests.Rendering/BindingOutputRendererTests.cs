using System.Collections.Generic;
using System.IO;

using SharpCpp.Model.Ast;
using SharpCpp.Pipeline;
using SharpCpp.Rendering;

using Xunit;

namespace SharpCpp.Tests.Rendering;

public sealed class BindingOutputRendererTests
{
    [Fact]
    public void Managed_record_declarations_include_configured_static_interfaces()
    {
        var compilation = new BindingCompilation("SharpCpp.Sample", "SharpCpp.Sample", "SampleBindings", "SharpCpp.Sample.Native", new[] { "Sample.hpp" });
        var objectRecord = new RecordBinding("record:object", "Object", "Object")
        {
            IsAbstract = true
        };
        objectRecord.SetMetadata(MetadataKeys.ManagedStaticInterfaces, new[] { "global::Example.IStaticTypeInfo<{managed_type}>" });
        objectRecord.SetMetadata(MetadataKeys.ManagedStaticInterfacesAbstract, true);
        var nodeRecord = new RecordBinding("record:node", "Node", "Node")
        {
            ManagedBaseTypeName = "Object"
        };
        nodeRecord.Bases.Add(new RecordBaseBinding(
            objectRecord.SymbolId,
            objectRecord.NativeName,
            objectRecord.ManagedName,
            objectRecord.CppQualifiedName,
            isVirtual: false));
        nodeRecord.Members.Add(new FunctionBinding(
            "method:node:GetTypeStatic",
            "GetTypeStatic",
            "GetTypeStatic",
            BindingType.Int32,
            "node_get_type_static")
        {
            IsStatic = true
        });
        compilation.Members.Add(objectRecord);
        compilation.Members.Add(nodeRecord);

        var managedOutput = BindingOutputRenderer.Render(compilation, "SharpCppSample")
            .Files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cs", StringComparison.Ordinal)).Content;

        Assert.Contains(
            "public abstract partial class Object : global::SharpCpp.Support.NativeObject",
            managedOutput,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "public abstract partial class Object : global::SharpCpp.Support.NativeObject, global::Example.IStaticTypeInfo<global::SharpCpp.Sample.Object>",
            managedOutput,
            StringComparison.Ordinal);
        Assert.Contains(
            "public partial class Node : global::SharpCpp.Sample.Object, global::Example.IStaticTypeInfo<global::SharpCpp.Sample.Node>",
            managedOutput,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Managed_only_classes_render_through_normal_grouped_output()
    {
        var compilation = new BindingCompilation("SharpCpp.Sample", "SharpCpp.Sample", "SampleBindings", "SharpCpp.Sample.Native", new[] { "Sample.hpp" });
        var engineDefs = new ManagedClassBinding("managed:engine-defs", "EngineDefs", "EngineDefs")
        {
            HeaderIncludePath = "Urho3D/Engine/EngineDefs.h",
            ManagedNamespace = "SharpCpp.Sample.Engine",
            IsStatic = true,
            IsPartial = true
        };
        engineDefs.Members.Add(new ManagedFieldBinding("managed:engine-defs:FullScreen", "FullScreen", "FullScreen", "string", "\"FullScreen\"")
        {
            HeaderIncludePath = engineDefs.HeaderIncludePath,
            IsConst = true
        });

        var detail = new ManagedClassBinding("managed:engine-events:Detail", "Detail", "Detail")
        {
            HeaderIncludePath = "Urho3D/Engine/EngineEvents.h",
            IsStatic = true,
            IsPartial = true
        };
        var updateEvent = new ManagedClassBinding("managed:engine-events:Detail:UpdateEvent", "UpdateEvent", "UpdateEvent")
        {
            HeaderIncludePath = detail.HeaderIncludePath,
            IsSealed = true,
            IsPartial = false,
            BaseTypeName = "global::SharpCpp.Sample.EventDefinition"
        };
        updateEvent.BodyCodeBlocks.Add(
            """
            public UpdateEvent()
                : base("Update")
            {
            }
            """);
        updateEvent.Members.Add(new ManagedFieldBinding("managed:engine-events:Detail:UpdateEvent:TimeStep", "TimeStep", "TimeStep", "global::SharpCpp.Sample.StringHash", "\"TimeStep\"")
        {
            IsStatic = false
        });
        detail.Members.Add(updateEvent);
        engineDefs.Members.Add(detail);
        compilation.Members.Add(engineDefs);

        var managedOutput = BindingOutputRenderer.Render(
                compilation,
                "SharpCppSample",
                header => header == "Urho3D/Engine/EngineDefs.h" ? "Engine/SharpCppSample.EngineDefs" : "SharpCppSample.Bindings")
            .Files.Single(static file => file.RelativePath == "Engine/SharpCppSample.EngineDefs.g.cs")
            .Content;

        Assert.Contains("namespace SharpCpp.Sample.Engine", managedOutput, StringComparison.Ordinal);
        Assert.Contains("public static partial class EngineDefs", managedOutput, StringComparison.Ordinal);
        Assert.Contains("public const string FullScreen = \"FullScreen\";", managedOutput, StringComparison.Ordinal);
        Assert.Contains("public static partial class Detail", managedOutput, StringComparison.Ordinal);
        Assert.Contains("public sealed class UpdateEvent : global::SharpCpp.Sample.EventDefinition", managedOutput, StringComparison.Ordinal);
        Assert.Contains("public readonly global::SharpCpp.Sample.StringHash TimeStep = \"TimeStep\";", managedOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Ignored_stubs_prefer_native_declaration_metadata()
    {
        var compilation = new BindingCompilation("SharpCpp.Sample", "SharpCpp.Sample", "SampleBindings", "SharpCpp.Sample.Native", new[] { "Sample.hpp" });
        var function = new FunctionBinding("function:test", "test", "Test", BindingType.Void, "sharpcpp_sample_test", "void")
        {
            IsStatic = true
        };
        function.SetIgnoreReason("manual stub");
        function.SetMetadata(MetadataKeys.DeclarationDisplayText, "static int native_test(const Foo& value);");
        compilation.Members.Add(function);

        var managedOutput = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files
            .Single(static file => file.RelativePath.EndsWith(".Bindings.g.cs", StringComparison.Ordinal)).Content;

        Assert.Contains("// static int native_test(const Foo& value); // excluded: manual stub", managedOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Ignored_stubs_fall_back_to_native_shapes_without_metadata()
    {
        var compilation = new BindingCompilation("SharpCpp.Sample", "SharpCpp.Sample", "SampleBindings", "SharpCpp.Sample.Native", new[] { "Sample.hpp" });
        var record = new RecordBinding("record:test", "test", "Test");
        var field = new FieldBinding("field:test:value", "value", "Value", BindingType.Int32, "int", "test", isReadOnly: false);
        field.SetIgnoreReason("manual stub");
        record.Members.Add(field);
        compilation.Members.Add(record);

        var managedOutput = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files
            .Single(static file => file.RelativePath.EndsWith(".Bindings.g.cs", StringComparison.Ordinal)).Content;

        Assert.Contains("// int value; // excluded: manual stub", managedOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Native_field_access_metadata_renders_direct_nested_field_access()
    {
        var compilation = new BindingCompilation("SharpCpp.Sample", "SharpCpp.Sample", "SampleBindings", "SharpCpp.Sample.Native", new[] { "Sample.hpp" });
        var record = new RecordBinding("record:sample", "sample", "Sample");
        var field = new FieldBinding("field:sample:nested_value", "nested_value", "NestedValue", BindingType.Int32, "int", "sample", isReadOnly: false);
        field.SetMetadata(MetadataKeys.NativeFieldAccessExpression, "outer.inner.value");
        record.Members.Add(field);
        compilation.Members.Add(record);

        var nativeOutput = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files
            .Single(static file => file.RelativePath.EndsWith(".Bindings.g.cpp", StringComparison.Ordinal)).Content;

        Assert.Contains("ReturnMarshaller<int> marshaller;", nativeOutput, StringComparison.Ordinal);
        Assert.Contains("return marshaller.ConvertToAbi(self->outer.inner.value);", nativeOutput, StringComparison.Ordinal);
        Assert.Contains("self->outer.inner.value = *value;", nativeOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("&sample::nested_value", nativeOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Native_field_access_metadata_casts_opaque_pointer_setters_to_declared_field_type()
    {
        var compilation = new BindingCompilation("SharpCpp.Sample", "SharpCpp.Sample", "SampleBindings", "SharpCpp.Sample.Native", new[] { "Sample.hpp" });
        var record = new RecordBinding("record:sample", "sample", "Sample");
        var field = new FieldBinding("field:sample:info_wl_display", "info_wl_display", "InfoWlDisplay", BindingType.IntPtr, "void*", "sample", isReadOnly: false, isPointer: true);
        field.SetMetadata(MetadataKeys.NativeFieldAccessExpression, "info.wl.display");
        record.Members.Add(field);
        compilation.Members.Add(record);

        var nativeOutput = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files
            .Single(static file => file.RelativePath.EndsWith(".Bindings.g.cpp", StringComparison.Ordinal)).Content;

        Assert.Contains("ReturnMarshaller<void*> marshaller;", nativeOutput, StringComparison.Ordinal);
        Assert.Contains("return marshaller.ConvertToAbi(self->info.wl.display);", nativeOutput, StringComparison.Ordinal);
        Assert.Contains("self->info.wl.display = static_cast<decltype(self->info.wl.display)>(*value);", nativeOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Native_field_accessors_preserve_const_char_pointer_storage_types()
    {
        var compilation = new BindingCompilation("SharpCpp.Sample", "SharpCpp.Sample", "SampleBindings", "SharpCpp.Sample.Native", new[] { "Sample.hpp" });
        var record = new RecordBinding("record:sample", "Sample", "Sample");
        var field = new FieldBinding("field:sample:Source", "Source", "Source", BindingType.IntPtr, "const Sample::Char*", "Sample", isReadOnly: false, isPointer: true);
        record.Members.Add(field);
        compilation.Members.Add(record);

        var output = BindingOutputRenderer.Render(compilation, "SharpCppSample");
        var managedOutput = output.Files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cs", StringComparison.Ordinal)).Content;
        var nativeOutput = output.Files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cpp", StringComparison.Ordinal)).Content;

        Assert.Contains("public global::System.IntPtr Source", managedOutput, StringComparison.Ordinal);
        Assert.Contains("ret_t<const Sample::Char*> sharpcpp_e2e_Sample_Source_get", nativeOutput, StringComparison.Ordinal);
        Assert.Contains("field_t<const Sample::Char*> value", nativeOutput, StringComparison.Ordinal);
        Assert.Contains("MarshallerInvoker::set_field<const Sample::Char*>(&Sample::Source, self, value);", nativeOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Native_fixed_array_fields_render_view_and_copy_accessors_with_qualified_element_type()
    {
        var compilation = new BindingCompilation("SharpCpp.Sample", "SharpCpp.Sample", "SampleBindings", "SharpCpp.Sample.Native", new[] { "Sample.hpp", "SharpCpp/Marshaller.hpp" });
        var arrayRecord = new RecordBinding("record:SharpCpp::NativeArray", "NativeArray", "NativeArray", "SharpCpp::NativeArray")
        {
            ManagedTypeFormat = "global::SharpCpp.NativeArray<{0}>"
        };
        arrayRecord.GenericParameters.Add(new BindingGenericParameter("T", 0));
        arrayRecord.GenericParameters.Add(new BindingGenericParameter("N", 1));
        arrayRecord.SetMetadata(MetadataKeys.FixedArrayRecord, true);
        arrayRecord.SetMetadata(MetadataKeys.NativeArrayRecord, true);
        compilation.Members.Add(arrayRecord);

        var formatEnum = new EnumBinding("enum:Sample::FORMAT", "FORMAT", "Format", cppQualifiedName: "Sample::FORMAT");
        compilation.Members.Add(formatEnum);

        var formatType = BindingType.CreateEnum("FORMAT", "Format", enumId: formatEnum.SymbolId);
        var lengthType = BindingType.CreateConfigured("4", "4");
        arrayRecord.SupportedGenericInstantiations.Add(new RecordGenericInstantiationBinding([formatType, lengthType]));
        arrayRecord.SupportedGenericInstantiations.Add(new RecordGenericInstantiationBinding([BindingType.Boolean, BindingType.CreateConfigured("7", "7")]));
        var fixedArrayType = BindingType.CreateHandle(
            "SharpCpp::NativeArray<FORMAT,4>",
            "global::SharpCpp.NativeArray<{0}>",
            arrayRecord.SymbolId,
            genericArguments: [formatType, lengthType])
            .WithFixedArray(formatType, 4);
        var record = new RecordBinding("record:Sample::Pipeline", "Pipeline", "Pipeline", "Sample::Pipeline");
        record.Members.Add(new FieldBinding("field:Sample::Pipeline:Formats", "Formats", "Formats", fixedArrayType, "Sample::FORMAT[4]", "Sample::Pipeline", isReadOnly: false));
        compilation.Members.Add(record);

        var output = BindingOutputRenderer.Render(compilation, "SharpCppSample");
        var managedOutput = output.Files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cs", StringComparison.Ordinal)).Content;
        var nativeOutput = output.Files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cpp", StringComparison.Ordinal)).Content;

        Assert.Contains("[return: MarshalAs(UnmanagedType.I1)]", managedOutput, StringComparison.Ordinal);
        Assert.Contains("internal static partial void __Array_Set_NativeArray_bool_7(global::SharpCpp.Support.NativeArrayAbi self, nuint index, [MarshalAs(UnmanagedType.I1)] bool value);", managedOutput, StringComparison.Ordinal);
        Assert.Contains("auto* value = new Sample::FORMAT[4]{};", nativeOutput, StringComparison.Ordinal);
        Assert.Contains("static_cast<Sample::FORMAT*>(self.handle)[index]", nativeOutput, StringComparison.Ordinal);
        Assert.Contains("return { const_cast<void*>(static_cast<const void*>(self->Formats)), 4 };", nativeOutput, StringComparison.Ordinal);
        Assert.Contains("auto* source = static_cast<Sample::FORMAT*>(value.handle);", nativeOutput, StringComparison.Ordinal);
        Assert.Contains("ParameterMarshaller<Sample::FORMAT> marshaller;", nativeOutput, StringComparison.Ordinal);
        Assert.Contains("self->Formats[index] = marshaller.ConvertToCpp(source[index]);", nativeOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("new FORMAT[4]", nativeOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Native_handle_backed_fixed_array_fields_render_view_and_copy_accessors()
    {
        var compilation = new BindingCompilation("SharpCpp.Sample", "SharpCpp.Sample", "SampleBindings", "SharpCpp.Sample.Native", new[] { "Sample.hpp", "SharpCpp/Marshaller.hpp" });
        var arrayRecord = new RecordBinding("record:SharpCpp::NativeArray", "NativeArray", "NativeArray", "SharpCpp::NativeArray")
        {
            ManagedTypeFormat = "global::SharpCpp.NativeArray<{0}>"
        };
        arrayRecord.GenericParameters.Add(new BindingGenericParameter("T", 0));
        arrayRecord.GenericParameters.Add(new BindingGenericParameter("N", 1));
        arrayRecord.SetMetadata(MetadataKeys.FixedArrayRecord, true);
        arrayRecord.SetMetadata(MetadataKeys.NativeArrayRecord, true);
        compilation.Members.Add(arrayRecord);

        var attachment = new RecordBinding("record:Sample::Attachment", "Attachment", "Attachment", "Sample::Attachment");
        compilation.Members.Add(attachment);

        var attachmentType = BindingType.CreateHandle("Sample::Attachment*", "global::SharpCpp.Sample.Attachment", attachment.SymbolId, cppIsRecordValue: true);
        var lengthType = BindingType.CreateConfigured("2", "2");
        arrayRecord.SupportedGenericInstantiations.Add(new RecordGenericInstantiationBinding([attachmentType, lengthType]));
        var fixedArrayType = BindingType.CreateHandle(
            "SharpCpp::NativeArray<Sample::Attachment,2>",
            "global::SharpCpp.NativeArray<{0}>",
            arrayRecord.SymbolId,
            genericArguments: [attachmentType, lengthType])
            .WithFixedArray(attachmentType, 2);

        var record = new RecordBinding("record:Sample::Pipeline", "Pipeline", "Pipeline", "Sample::Pipeline");
        record.Members.Add(new FieldBinding("field:Sample::Pipeline:Attachments", "Attachments", "Attachments", fixedArrayType, "Sample::Attachment[2]", "Sample::Pipeline", isReadOnly: false));
        compilation.Members.Add(record);

        var output = BindingOutputRenderer.Render(compilation, "SharpCppSample");
        var managedOutput = output.Files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cs", StringComparison.Ordinal)).Content;
        var nativeOutput = output.Files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cpp", StringComparison.Ordinal)).Content;

        Assert.Contains("public global::SharpCpp.Support.IFixedArray<global::SharpCpp.Sample.Attachment> Attachments", managedOutput, StringComparison.Ordinal);
        Assert.Contains("private __AttachmentsFixedArrayProxy? __attachmentsFixedArrayProxy;", managedOutput, StringComparison.Ordinal);
        Assert.Contains("internal static partial global::SharpCpp.Sample.Attachment Get_Attachments_Item(Pipeline self, nuint index);", managedOutput, StringComparison.Ordinal);
        Assert.Contains("internal static partial void Set_Attachments_Item(Pipeline self, nuint index, [MarshalUsing(typeof(global::SharpCpp.Sample.__AttachmentMarshaller<global::SharpCpp.Support.BorrowedNativeHandle>))] global::SharpCpp.Sample.Attachment? value);", managedOutput, StringComparison.Ordinal);
        Assert.Contains("ReturnMarshaller<Sample::Attachment*> marshaller;", nativeOutput, StringComparison.Ordinal);
        Assert.Contains("return marshaller.ConvertToAbi(&(self->Attachments[index]));", nativeOutput, StringComparison.Ordinal);
        Assert.Contains("auto* source = marshaller.ConvertToCpp(value);", nativeOutput, StringComparison.Ordinal);
        Assert.Contains("if (source == nullptr)", nativeOutput, StringComparison.Ordinal);
        Assert.Contains("self->Attachments[index] = *source;", nativeOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("Get_Attachments(Pipeline self)", managedOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("Set_Attachments(Pipeline self", managedOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Pointer_count_record_fields_register_runtime_length_native_arrays()
    {
        var compilation = new BindingCompilation("SharpCpp.Sample", "SharpCpp.Sample", "SampleBindings", "SharpCpp.Sample.Native",
            new[] { "Sample.hpp", "SharpCpp/Marshaller.hpp" });
        var arrayRecord = new RecordBinding("record:SharpCpp::NativeArray", "NativeArray", "NativeArray", "SharpCpp::NativeArray")
        {
            ManagedTypeFormat = "global::SharpCpp.NativeArray<{0}>"
        };
        arrayRecord.GenericParameters.Add(new BindingGenericParameter("T", 0));
        arrayRecord.GenericParameters.Add(new BindingGenericParameter("N", 1));
        arrayRecord.SetMetadata(MetadataKeys.FixedArrayRecord, true);
        arrayRecord.SetMetadata(MetadataKeys.NativeArrayRecord, true);
        compilation.Members.Add(arrayRecord);

        var attachment = new RecordBinding("record:Sample::Attachment", "Attachment", "Attachment", "Sample::Attachment");
        compilation.Members.Add(attachment);
        var attachmentType = BindingType.CreateHandle("const Sample::Attachment*", "global::SharpCpp.Sample.Attachment",
            attachment.SymbolId, cppIsRecordValue: true);
        var pipeline = new RecordBinding("record:Sample::Pipeline", "Pipeline", "Pipeline", "Sample::Pipeline");
        pipeline.Members.Add(new FieldBinding("field:Sample::Pipeline:Attachments", "Attachments", "Attachments",
            attachmentType, "const Sample::Attachment*", "Sample::Pipeline", isReadOnly: false, isPointer: true));
        pipeline.Members.Add(new FieldBinding("field:Sample::Pipeline:NumAttachments", "NumAttachments", "NumAttachments",
            BindingType.UInt32, "unsigned int", "Sample::Pipeline", isReadOnly: false));
        compilation.Members.Add(pipeline);

        var output = BindingOutputRenderer.Render(compilation, "SharpCppSample");
        var managedOutput = output.Files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cs", StringComparison.Ordinal)).Content;
        var nativeOutput = output.Files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cpp", StringComparison.Ordinal)).Content;

        Assert.Contains("NativeArray<global::SharpCpp.Sample.Attachment>.__RegisterDynamic", managedOutput, StringComparison.Ordinal);
        Assert.Contains("__DynamicNativeArray_Create_record_Sample__Attachment", managedOutput, StringComparison.Ordinal);
        Assert.Contains("dynamic_native_array_record_Sample__Attachment_create", nativeOutput, StringComparison.Ordinal);
        Assert.Contains("new Sample::Attachment[length]{}", nativeOutput, StringComparison.Ordinal);
        Assert.Contains("delete[] static_cast<Sample::Attachment*>(self.handle)", nativeOutput, StringComparison.Ordinal);
        Assert.Contains("index >= self.length", nativeOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Native_nested_fixed_array_fields_render_proxy_item_accessors()
    {
        var compilation = new BindingCompilation("SharpCpp.Sample", "SharpCpp.Sample", "SampleBindings", "SharpCpp.Sample.Native", new[] { "Sample.hpp", "SharpCpp/Marshaller.hpp" });
        var arrayRecord = new RecordBinding("record:SharpCpp::NativeArray", "NativeArray", "NativeArray", "SharpCpp::NativeArray")
        {
            ManagedTypeFormat = "global::SharpCpp.NativeArray<{0}>"
        };
        arrayRecord.GenericParameters.Add(new BindingGenericParameter("T", 0));
        arrayRecord.GenericParameters.Add(new BindingGenericParameter("N", 1));
        arrayRecord.SetMetadata(MetadataKeys.FixedArrayRecord, true);
        arrayRecord.SetMetadata(MetadataKeys.NativeArrayRecord, true);
        compilation.Members.Add(arrayRecord);

        var innerLengthType = BindingType.CreateConfigured("4", "4");
        var outerLengthType = BindingType.CreateConfigured("3", "3");
        arrayRecord.SupportedGenericInstantiations.Add(new RecordGenericInstantiationBinding([BindingType.Single, innerLengthType]));
        var innerArrayType = BindingType.CreateHandle(
            "SharpCpp::NativeArray<float,4>",
            "global::SharpCpp.NativeArray<{0}>",
            arrayRecord.SymbolId,
            genericArguments: [BindingType.Single, innerLengthType])
            .WithFixedArray(BindingType.Single, 4);
        arrayRecord.SupportedGenericInstantiations.Add(new RecordGenericInstantiationBinding([innerArrayType, outerLengthType]));
        var alternateInnerLengthType = BindingType.CreateConfigured("8", "8");
        var alternateInnerArrayType = BindingType.CreateHandle(
            "SharpCpp::NativeArray<float,8>",
            "global::SharpCpp.NativeArray<{0}>",
            arrayRecord.SymbolId,
            genericArguments: [BindingType.Single, alternateInnerLengthType])
            .WithFixedArray(BindingType.Single, 8);
        arrayRecord.SupportedGenericInstantiations.Add(
            new RecordGenericInstantiationBinding([alternateInnerArrayType, outerLengthType]));
        var outerArrayType = BindingType.CreateHandle(
            "SharpCpp::NativeArray<SharpCpp::NativeArray<float,4>,3>",
            "global::SharpCpp.NativeArray<{0}>",
            arrayRecord.SymbolId,
            genericArguments: [innerArrayType, outerLengthType])
            .WithFixedArray(innerArrayType, 3);

        var record = new RecordBinding("record:Sample::Matrix", "Matrix", "Matrix", "Sample::Matrix");
        record.Members.Add(new FieldBinding("field:Sample::Matrix:data", "data", "Data", outerArrayType, "float[3][4]", "Sample::Matrix", isReadOnly: false));
        compilation.Members.Add(record);

        var output = BindingOutputRenderer.Render(compilation, "SharpCppSample");
        var managedOutput = output.Files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cs", StringComparison.Ordinal)).Content;
        var nativeOutput = output.Files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cpp", StringComparison.Ordinal)).Content;

        Assert.Contains("public global::SharpCpp.Support.IFixedArray<global::SharpCpp.NativeArray<float>> Data", managedOutput, StringComparison.Ordinal);
        Assert.Contains("internal static partial global::SharpCpp.NativeArray<float> Get_Data_Item(Matrix self, nuint index);", managedOutput, StringComparison.Ordinal);
        Assert.Contains("internal static partial void Set_Data_Item(Matrix self, nuint index, [MarshalUsing(typeof(global::SharpCpp.Sample.__NativeArrayInMarshaller<>))] global::SharpCpp.NativeArray<float> value);", managedOutput, StringComparison.Ordinal);
        Assert.Contains("return { const_cast<void*>(static_cast<const void*>(self->data[index])), 4 };", nativeOutput, StringComparison.Ordinal);
        Assert.Contains("auto* source = reinterpret_cast<float (*)[4]>(value.handle);", nativeOutput, StringComparison.Ordinal);
        Assert.Contains("target[nestedIndex] = (*source)[nestedIndex];", nativeOutput, StringComparison.Ordinal);
        Assert.Contains("_float_4_3", nativeOutput, StringComparison.Ordinal);
        Assert.Contains("_float_8_3", nativeOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("return { const_cast<void*>(static_cast<const void*>(self->data)), 3 };", nativeOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Free_functions_can_render_into_custom_managed_containers()
    {
        var compilation = new BindingCompilation("SharpCpp.Sample", "SharpCpp.Sample", "SampleBindings", "SharpCpp.Sample.Native", new[] { "Sample.hpp" });
        compilation.Members.Add(new FunctionBinding("function:default", "default_call", "Call", BindingType.Void, "entry_default"));
        compilation.Members.Add(new FunctionBinding("function:custom", "custom_call", "Call", BindingType.Void, "entry_custom")
        {
            ManagedNamespace = "Custom.Api",
            ManagedClassName = "CustomBindings"
        });

        var managedOutput = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files
            .Single(static file => file.RelativePath.EndsWith(".Bindings.g.cs", StringComparison.Ordinal)).Content;

        Assert.Contains("namespace SharpCpp.Sample", managedOutput, StringComparison.Ordinal);
        Assert.Contains("public static partial class SampleBindings", managedOutput, StringComparison.Ordinal);
        Assert.Contains("__PInvoke.Call();", managedOutput, StringComparison.Ordinal);
        Assert.Contains("EntryPoint = \"entry_default\"", managedOutput, StringComparison.Ordinal);
        Assert.Contains("namespace Custom.Api", managedOutput, StringComparison.Ordinal);
        Assert.Contains("public static partial class CustomBindings", managedOutput, StringComparison.Ordinal);
        Assert.Contains("EntryPoint = \"entry_custom\"", managedOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Silent_exclusions_do_not_render_comment_stubs()
    {
        var compilation = new BindingCompilation("SharpCpp.Sample", "SharpCpp.Sample", "SampleBindings", "SharpCpp.Sample.Native", new[] { "Sample.hpp" });
        var record = new RecordBinding("record:silent", "silent", "Silent");
        record.SetSilentExclusionReason("internal detail");
        var ignoredMethodOnSilentRecord = new FunctionBinding("method:silent:ignored", "ignored", "Ignored", BindingType.Int32, "entry_ignored")
        {
            OwningRecordCppTypeName = "silent"
        };
        ignoredMethodOnSilentRecord.SetIgnoreReason("unsupported member");
        record.Members.Add(ignoredMethodOnSilentRecord);
        var function = new FunctionBinding("function:silent_fn", "silent_fn", "SilentFn", BindingType.Int32, "entry_silent");
        function.SetSilentExclusionReason("internal detail");
        var visibleRecord = new RecordBinding("record:visible", "visible", "Visible");
        var silentMethod = new FunctionBinding("method:visible:silent_method", "silent_method", "SilentMethod", BindingType.Int32, "entry_silent_method")
        {
            OwningRecordCppTypeName = "visible"
        };
        silentMethod.SetSilentExclusionReason("internal detail");
        visibleRecord.Members.Add(silentMethod);
        compilation.Members.Add(record);
        compilation.Members.Add(function);
        compilation.Members.Add(visibleRecord);

        var managedOutput = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files
            .Single(static file => file.RelativePath.EndsWith(".Bindings.g.cs", StringComparison.Ordinal)).Content;

        Assert.DoesNotContain("excluded: internal detail", managedOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("public partial class Silent", managedOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("SilentFn", managedOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("SilentMethod", managedOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("unsupported member", managedOutput, StringComparison.Ordinal);
        Assert.Contains("public partial class Visible", managedOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Native_output_uses_export_macro_with_extern_c()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "SharpCpp.Sample",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "Builtin/Builtin.hpp" });
        var function = new FunctionBinding(
            "function:add_numbers",
            "add_numbers",
            "AddNumbers",
            BindingType.Int32,
            "sharpcpp_sample_add_numbers");
        function.Parameters.Add(new ParameterBinding("parameter:add_numbers:lhs", "lhs", "lhs", BindingType.Int32));
        function.Parameters.Add(new ParameterBinding("parameter:add_numbers:rhs", "rhs", "rhs", BindingType.Int32));
        compilation.Members.Add(function);

        var files = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files;
        var nativeOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cpp", StringComparison.Ordinal)).Content;

        Assert.Contains("using namespace SharpCpp;", nativeOutput, StringComparison.Ordinal);
        Assert.Contains("#include \"SharpCpp/Export.hpp\"", nativeOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("extern \"C\"\n{", nativeOutput, StringComparison.Ordinal);
        Assert.Contains("#include \"SharpCpp/Marshaller.hpp\"", nativeOutput, StringComparison.Ordinal);
        Assert.Contains("SHARPCPP_EXPORT ret_t<int> sharpcpp_sample_add_numbers(arg_t<int> lhs, arg_t<int> rhs)", nativeOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Managed_output_uses_ReadOnlySpan_for_const_char_pointer_parameter()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "SharpCpp.Sample",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "String/String.hpp" });
        var constCharType = BindingType.CreateConfigured("const char*", "global::System.ReadOnlySpan<byte>");
        var function = new FunctionBinding(
            "function:set_name",
            "set_name",
            "SetName",
            BindingType.Void,
            "sharpcpp_sample_set_name");
        function.Parameters.Add(new ParameterBinding(
            "parameter:set_name:name",
            "name",
            "name",
            constCharType,
            BindingParameterDirection.In,
            BindingParameterAbiKind.Value,
            "const char*"));
        compilation.Members.Add(function);

        var files = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files;
        var managedOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cs", StringComparison.Ordinal)).Content;

        Assert.Contains("ReadOnlySpan<byte> name", managedOutput, StringComparison.Ordinal);
        Assert.Contains("public static void SetName(global::System.ReadOnlySpan<byte> name)", managedOutput, StringComparison.Ordinal);
        Assert.Contains("internal static partial void SetName(global::System.ReadOnlySpan<byte> name);", managedOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Managed_output_uses_configured_marshaller_for_string_view_parameter()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "SharpCpp.Sample",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "String/String.hpp" });
        var stringViewType = BindingType.CreateConfigured(
            "std::string_view",
            "global::System.ReadOnlySpan<byte>",
            null,
            "global::SharpCpp.Support.Utf8SpanAbiMarshaller");
        var function = new FunctionBinding(
            "function:measure_view",
            "measure_view",
            "MeasureView",
            BindingType.Int32,
            "sharpcpp_sample_measure_view");
        function.Parameters.Add(new ParameterBinding(
            "parameter:measure_view:value",
            "value",
            "value",
            stringViewType,
            BindingParameterDirection.In,
            BindingParameterAbiKind.Value,
            "std::string_view"));
        compilation.Members.Add(function);

        var files = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files;
        var managedOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cs", StringComparison.Ordinal)).Content;

        Assert.Contains("public static int MeasureView(global::System.ReadOnlySpan<byte> value)", managedOutput, StringComparison.Ordinal);
        Assert.Contains("[MarshalUsing(typeof(global::SharpCpp.Support.Utf8SpanAbiMarshaller))] global::System.ReadOnlySpan<byte> value", managedOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Managed_output_emits_callback_marshaller_helper_for_callback_parameters()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "SharpCpp.Sample",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "Callbacks/Callbacks.hpp" });
        var callbackType = BindingType.CreateCallback(
            "std::function<void(int)>",
            "global::System.Action<{0}>",
            managedMarshaller: null,
            BindingType.Void,
            [BindingType.Int32]);
        var function = new FunctionBinding(
            "function:invoke_callback",
            "invoke_callback",
            "InvokeCallback",
            BindingType.Void,
            "sharpcpp_sample_invoke_callback");
        function.Parameters.Add(new ParameterBinding(
            "parameter:invoke_callback:callback",
            "callback",
            "callback",
            callbackType,
            BindingParameterDirection.In,
            BindingParameterAbiKind.Value,
            "std::function<void(int)>"));
        compilation.Members.Add(function);

        var files = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files;
        var managedOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cs", StringComparison.Ordinal)).Content;
        var aliasOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.Aliases.g.cs", StringComparison.Ordinal)).Content;
        var nativeOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cpp", StringComparison.Ordinal)).Content;

        Assert.Contains("public static void InvokeCallback(global::System.Action<int> callback)", managedOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("internal readonly unsafe struct __SharpCppManagedCallbackHandleAbi", managedOutput, StringComparison.Ordinal);
        Assert.Contains("internal readonly unsafe struct __SharpCppManagedCallbackHandleAbi", aliasOutput, StringComparison.Ordinal);
        Assert.Contains("public __SharpCppManagedCallbackHandleAbi(nint state, nint callback)", aliasOutput, StringComparison.Ordinal);
        Assert.Equal(1, aliasOutput.Split("public nint State { get; }", StringSplitOptions.None).Length - 1);
        Assert.Equal(1, aliasOutput.Split("public nint Callback { get; }", StringSplitOptions.None).Length - 1);
        Assert.Contains("__PInvoke.__SetManagedCallbackRelease(&__ReleaseManagedCallbackState);", aliasOutput, StringComparison.Ordinal);
        Assert.Contains("internal static unsafe partial void __SetManagedCallbackRelease(delegate* unmanaged[Cdecl]<nint, void> release);", aliasOutput, StringComparison.Ordinal);
        Assert.Contains("[CustomMarshaller(typeof(global::System.Action<int>), MarshalMode.ManagedToUnmanagedIn", aliasOutput, StringComparison.Ordinal);
        Assert.Contains("global::System.Runtime.InteropServices.GCHandle.Alloc(managed)", aliasOutput, StringComparison.Ordinal);
        Assert.Contains("private static void __ReleaseManagedCallbackState(nint state)", aliasOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("public delegate* unmanaged[Cdecl]<nint, void> Release", aliasOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("private static void Release(nint state)", aliasOutput, StringComparison.Ordinal);
        Assert.Contains("[MarshalUsing(typeof(global::SharpCpp.Sample.SampleBindings.__CallbackMarshaller_void_int))] global::System.Action<int>? callback", managedOutput, StringComparison.Ordinal);
        Assert.Contains("SHARPCPP_EXPORT void sharpcpp_set_managed_callback_release(void (*release)(void*))", nativeOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Optional_callback_defaults_use_null_dispatch_instead_of_nullable_value_dispatch()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "SharpCpp.Sample",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "Callbacks/Callbacks.hpp" });
        var callbackType = BindingType.CreateCallback(
            "std::function<void(int)>",
            "global::System.Action<{0}>",
            managedMarshaller: null,
            BindingType.Void,
            [BindingType.Int32]);
        var function = new FunctionBinding(
            "function:invoke_optional_callback",
            "invoke_optional_callback",
            "InvokeOptionalCallback",
            BindingType.Void,
            "sharpcpp_sample_invoke_optional_callback");
        var callback = new ParameterBinding(
            "parameter:invoke_optional_callback:callback",
            "callback",
            "callback",
            callbackType,
            BindingParameterDirection.In,
            BindingParameterAbiKind.Value,
            "std::function<void(int)>");
        callback.SetMetadata(MetadataKeys.UnexpressibleDefault, true);
        function.Parameters.Add(callback);
        compilation.Members.Add(function);

        var files = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files;
        var managedOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cs", StringComparison.Ordinal)).Content;

        Assert.Contains("public static void InvokeOptionalCallback(global::System.Action<int>? callback = default)", managedOutput, StringComparison.Ordinal);
        Assert.Contains("if (callback is not null)", managedOutput, StringComparison.Ordinal);
        Assert.Contains("__PInvoke.InvokeOptionalCallback(callback);", managedOutput, StringComparison.Ordinal);
        Assert.Contains("if (callback is null)", managedOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("public static void InvokeOptionalCallback()", managedOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("callback.HasValue", managedOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("callback.Value", managedOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Trailing_unexpressible_defaults_do_not_emit_redundant_public_overloads()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "SharpCpp.Sample",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "ImGui/imgui.h" });
        var function = new FunctionBinding(
            "function:button",
            "Button",
            "Button",
            BindingType.Boolean,
            "sharpcpp_sample_button");
        function.Parameters.Add(new ParameterBinding(
            "parameter:button:label",
            "label",
            "label",
            BindingType.CreateConfigured("const char*", "global::System.ReadOnlySpan<byte>"),
            BindingParameterDirection.In,
            BindingParameterAbiKind.Value,
            "const char*"));
        var size = new ParameterBinding(
            "parameter:button:size",
            "size",
            "size",
            BindingType.CreateConfigured("ImVec2", "global::Urho3D.Math.Vector2"),
            BindingParameterDirection.In,
            BindingParameterAbiKind.Reference,
            "const ImVec2&");
        size.SetMetadata(MetadataKeys.UnexpressibleDefault, true);
        function.Parameters.Add(size);
        compilation.Members.Add(function);

        var managedOutput = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files
            .Single(static file => file.RelativePath.EndsWith(".Bindings.g.cs", StringComparison.Ordinal)).Content;

        Assert.Contains("public static bool Button(global::System.ReadOnlySpan<byte> label, in global::Urho3D.Math.Vector2? size = default)", managedOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("public static bool Button(global::System.ReadOnlySpan<byte> label)\n", managedOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Callback_null_defaults_render_nullable_public_parameters()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "SharpCpp.Sample",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "Callbacks/Callbacks.hpp" });
        var callbackType = BindingType.CreateCallback(
            "std::function<unsigned()>",
            "global::System.Func<{0}>",
            managedMarshaller: null,
            BindingType.UInt32,
            []);
        var function = new FunctionBinding(
            "function:register_timestamp",
            "register_timestamp",
            "RegisterTimestamp",
            BindingType.Void,
            "sharpcpp_sample_register_timestamp");
        function.Parameters.Add(new ParameterBinding(
            "parameter:register_timestamp:get_timestamp",
            "getTimestamp",
            "getTimestamp",
            callbackType,
            BindingParameterDirection.In,
            BindingParameterAbiKind.Value,
            "std::function<unsigned()>")
        {
            ManagedDefaultValueExpression = "null"
        });
        compilation.Members.Add(function);

        var files = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files;
        var managedOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cs", StringComparison.Ordinal)).Content;

        Assert.Contains("public static void RegisterTimestamp(global::System.Func<uint>? getTimestamp = default)", managedOutput, StringComparison.Ordinal);
        Assert.Contains("[MarshalUsing(typeof(global::SharpCpp.Sample.SampleBindings.__CallbackMarshaller_uint_none))] global::System.Func<uint>? getTimestamp", managedOutput, StringComparison.Ordinal);
        Assert.Contains("if (getTimestamp is not null)", managedOutput, StringComparison.Ordinal);
        Assert.Contains("__PInvoke.RegisterTimestamp(getTimestamp);", managedOutput, StringComparison.Ordinal);
        Assert.Contains("if (getTimestamp is null)", managedOutput, StringComparison.Ordinal);
        Assert.Contains("__PInvoke.RegisterTimestamp__Args0();", managedOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("public static void RegisterTimestamp(global::System.Func<uint> getTimestamp = default)", managedOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void String_default_values_render_nullable_public_parameters()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "SharpCpp.Sample",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "String/String.hpp" });
        var function = new FunctionBinding(
            "function:measure",
            "Measure",
            "Measure",
            BindingType.Void,
            "sharpcpp_sample_measure");
        function.Parameters.Add(new ParameterBinding(
            "parameter:measure:out_remaining",
            "out_remaining",
            "outRemaining",
            BindingType.CreateConfigured("const char**", "string"),
            BindingParameterDirection.In,
            BindingParameterAbiKind.Value,
            "const char**")
        {
            ManagedDefaultValueExpression = "default"
        });
        compilation.Members.Add(function);

        var managedOutput = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files
            .Single(static file => file.RelativePath.EndsWith(".Bindings.g.cs", StringComparison.Ordinal)).Content;

        Assert.Contains("public static void Measure(string? outRemaining = default)", managedOutput, StringComparison.Ordinal);
        Assert.Contains("internal static partial void Measure(string? outRemaining);", managedOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("public static void Measure(string outRemaining = default)", managedOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("internal static partial void Measure(string outRemaining);", managedOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Unexpressible_default_overloads_preserve_prefix_optional_parameters()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "SharpCpp.Sample",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "ImGui/imgui.h" });
        var function = new FunctionBinding(
            "function:drag_scalar",
            "DragScalar",
            "DragScalar",
            BindingType.Boolean,
            "sharpcpp_sample_drag_scalar");
        function.Parameters.Add(new ParameterBinding(
            "parameter:drag_scalar:label",
            "label",
            "label",
            BindingType.CreateConfigured("const char*", "global::System.ReadOnlySpan<byte>"),
            BindingParameterDirection.In,
            BindingParameterAbiKind.Value,
            "const char*"));
        function.Parameters.Add(new ParameterBinding(
            "parameter:drag_scalar:data_type",
            "data_type",
            "dataType",
            BindingType.Int32,
            BindingParameterDirection.In,
            BindingParameterAbiKind.Value,
            "int"));
        function.Parameters.Add(new ParameterBinding(
            "parameter:drag_scalar:p_data",
            "p_data",
            "pData",
            BindingType.IntPtr,
            BindingParameterDirection.In,
            BindingParameterAbiKind.Value,
            "void*"));
        function.Parameters.Add(new ParameterBinding(
            "parameter:drag_scalar:v_speed",
            "v_speed",
            "vSpeed",
            BindingType.Single,
            BindingParameterDirection.In,
            BindingParameterAbiKind.Value,
            "float")
        {
            ManagedDefaultValueExpression = "1.0f"
        });
        function.Parameters.Add(new ParameterBinding(
            "parameter:drag_scalar:p_min",
            "p_min",
            "pMin",
            BindingType.IntPtr,
            BindingParameterDirection.In,
            BindingParameterAbiKind.Value,
            "const void*")
        {
            ManagedDefaultValueExpression = "default"
        });
        function.Parameters.Add(new ParameterBinding(
            "parameter:drag_scalar:p_max",
            "p_max",
            "pMax",
            BindingType.IntPtr,
            BindingParameterDirection.In,
            BindingParameterAbiKind.Value,
            "const void*")
        {
            ManagedDefaultValueExpression = "default"
        });
        var format = new ParameterBinding(
            "parameter:drag_scalar:format",
            "format",
            "format",
            BindingType.CreateConfigured("const char*", "global::System.ReadOnlySpan<byte>"),
            BindingParameterDirection.In,
            BindingParameterAbiKind.Value,
            "const char*");
        format.SetMetadata(MetadataKeys.UnexpressibleDefault, true);
        function.Parameters.Add(format);
        function.Parameters.Add(new ParameterBinding(
            "parameter:drag_scalar:flags",
            "flags",
            "flags",
            BindingType.Int32,
            BindingParameterDirection.In,
            BindingParameterAbiKind.Value,
            "int")
        {
            ManagedDefaultValueExpression = "0"
        });
        compilation.Members.Add(function);

        var managedOutput = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files
            .Single(static file => file.RelativePath.EndsWith(".Bindings.g.cs", StringComparison.Ordinal)).Content;

        Assert.Contains(
            "public static bool DragScalar(global::System.ReadOnlySpan<byte> label, int dataType, global::System.IntPtr pData, float vSpeed = 1.0f, global::System.IntPtr pMin = default, global::System.IntPtr pMax = default)",
            managedOutput,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "float vSpeed = 1.0f, global::System.IntPtr pMin, global::System.IntPtr pMax",
            managedOutput,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Unexpressible_default_convenience_overloads_forward_out_parameters_with_modifiers()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "SharpCpp.Sample",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "ImGui/imgui.h" });
        var function = new FunctionBinding(
            "function:selectable",
            "Selectable",
            "Selectable",
            BindingType.Boolean,
            "sharpcpp_sample_selectable");
        function.Parameters.Add(new ParameterBinding(
            "parameter:selectable:label",
            "label",
            "label",
            BindingType.CreateConfigured("const char*", "global::System.ReadOnlySpan<byte>"),
            BindingParameterDirection.In,
            BindingParameterAbiKind.Value,
            "const char*"));
        function.Parameters.Add(new ParameterBinding(
            "parameter:selectable:p_selected",
            "p_selected",
            "p_selected",
            BindingType.Boolean,
            BindingParameterDirection.Out,
            BindingParameterAbiKind.Value,
            "bool"));
        function.Parameters.Add(new ParameterBinding(
            "parameter:selectable:flags",
            "flags",
            "flags",
            BindingType.Int32,
            BindingParameterDirection.In,
            BindingParameterAbiKind.Value,
            "int")
        {
            ManagedDefaultValueExpression = "0"
        });
        var size = new ParameterBinding(
            "parameter:selectable:size",
            "size",
            "size",
            BindingType.CreateConfigured("ImVec2", "global::RedBolt.Vector2"),
            BindingParameterDirection.In,
            BindingParameterAbiKind.Reference,
            "const ImVec2&");
        size.SetMetadata(MetadataKeys.UnexpressibleDefault, true);
        function.Parameters.Add(size);
        compilation.Members.Add(function);

        var managedOutput = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files
            .Single(static file => file.RelativePath.EndsWith(".Bindings.g.cs", StringComparison.Ordinal)).Content;

        Assert.Contains(
            "return Selectable(label, out p_selected, flags, default(",
            managedOutput,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "return Selectable(label, p_selected, flags, default(",
            managedOutput,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Undefaultable_default_cascade_dispatches_to_matching_argument_variants()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "SharpCpp.Sample",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "ImGui/imgui.h" });
        var function = new FunctionBinding(
            "function:plot_like",
            "PlotLike",
            "PlotLike",
            BindingType.Void,
            "sharpcpp_sample_plot_like");
        function.Parameters.Add(new ParameterBinding(
            "parameter:plot_like:label",
            "label",
            "label",
            BindingType.CreateConfigured("const char*", "global::System.ReadOnlySpan<byte>"),
            BindingParameterDirection.In,
            BindingParameterAbiKind.Value,
            "const char*"));
        function.Parameters.Add(new ParameterBinding(
            "parameter:plot_like:value",
            "value",
            "value",
            BindingType.Single,
            BindingParameterDirection.In,
            BindingParameterAbiKind.Reference,
            "const float&"));
        function.Parameters.Add(new ParameterBinding(
            "parameter:plot_like:count",
            "count",
            "count",
            BindingType.Int32,
            BindingParameterDirection.In,
            BindingParameterAbiKind.Value,
            "int"));
        function.Parameters.Add(new ParameterBinding(
            "parameter:plot_like:offset",
            "offset",
            "offset",
            BindingType.Int32,
            BindingParameterDirection.In,
            BindingParameterAbiKind.Value,
            "int")
        {
            ManagedDefaultValueExpression = "0"
        });
        var overlayText = new ParameterBinding(
            "parameter:plot_like:overlay_text",
            "overlay_text",
            "overlay_text",
            BindingType.CreateConfigured("const char*", "global::System.ReadOnlySpan<byte>"),
            BindingParameterDirection.In,
            BindingParameterAbiKind.Value,
            "const char*")
        {
            ManagedDefaultValueExpression = "null"
        };
        overlayText.SetMetadata(MetadataKeys.UndefaultableParam, true);
        function.Parameters.Add(overlayText);
        var scaleMin = new ParameterBinding(
            "parameter:plot_like:scale_min",
            "scale_min",
            "scale_min",
            BindingType.Single,
            BindingParameterDirection.In,
            BindingParameterAbiKind.Value,
            "float");
        scaleMin.SetMetadata(MetadataKeys.UnexpressibleDefault, true);
        function.Parameters.Add(scaleMin);
        var scaleMax = new ParameterBinding(
            "parameter:plot_like:scale_max",
            "scale_max",
            "scale_max",
            BindingType.Single,
            BindingParameterDirection.In,
            BindingParameterAbiKind.Value,
            "float");
        scaleMax.SetMetadata(MetadataKeys.UnexpressibleDefault, true);
        function.Parameters.Add(scaleMax);
        var graphSize = new ParameterBinding(
            "parameter:plot_like:graph_size",
            "graph_size",
            "graph_size",
            BindingType.CreateConfigured("ImVec2", "global::RedBolt.Vector2"),
            BindingParameterDirection.In,
            BindingParameterAbiKind.Reference,
            "const ImVec2&");
        graphSize.SetMetadata(MetadataKeys.UnexpressibleDefault, true);
        function.Parameters.Add(graphSize);
        compilation.Members.Add(function);

        var managedOutput = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files
            .Single(static file => file.RelativePath.EndsWith(".Bindings.g.cs", StringComparison.Ordinal)).Content;

        Assert.Contains(
            "__PInvoke.PlotLike__Args7(label, value, count, offset, overlay_text, scale_min.Value, scale_max.Value);",
            managedOutput,
            StringComparison.Ordinal);
        Assert.Contains(
            "__PInvoke.PlotLike__Args6(label, value, count, offset, overlay_text, scale_min.Value);",
            managedOutput,
            StringComparison.Ordinal);
        Assert.Contains(
            "__PInvoke.PlotLike__Args5(label, value, count, offset, overlay_text);",
            managedOutput,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "__PInvoke.PlotLike(label, value, count, offset, overlay_text, scale_min.Value, scale_max.Value);",
            managedOutput,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "__PInvoke.PlotLike(label, value, count, offset, overlay_text, scale_min.Value);",
            managedOutput,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "__PInvoke.PlotLike(label, value, count, offset, overlay_text);",
            managedOutput,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Undefaultable_prefix_cascade_uses_prefix_variant_as_full_dispatch()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "SharpCpp.Sample",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "ImGui/imgui.h" });
        var function = new FunctionBinding(
            "function:progress_like",
            "ProgressLike",
            "ProgressLike",
            BindingType.Void,
            "sharpcpp_sample_progress_like");
        function.Parameters.Add(new ParameterBinding(
            "parameter:progress_like:fraction",
            "fraction",
            "fraction",
            BindingType.Single,
            BindingParameterDirection.In,
            BindingParameterAbiKind.Value,
            "float"));
        var size = new ParameterBinding(
            "parameter:progress_like:size",
            "size",
            "size",
            BindingType.CreateConfigured("ImVec2", "global::RedBolt.Vector2"),
            BindingParameterDirection.In,
            BindingParameterAbiKind.Reference,
            "const ImVec2&");
        size.SetMetadata(MetadataKeys.UnexpressibleDefault, true);
        function.Parameters.Add(size);
        var overlay = new ParameterBinding(
            "parameter:progress_like:overlay",
            "overlay",
            "overlay",
            BindingType.CreateConfigured("const char*", "global::System.ReadOnlySpan<byte>"),
            BindingParameterDirection.In,
            BindingParameterAbiKind.Value,
            "const char*")
        {
            ManagedDefaultValueExpression = "default"
        };
        overlay.SetMetadata(MetadataKeys.UndefaultableParam, true);
        function.Parameters.Add(overlay);
        compilation.Members.Add(function);

        var managedOutput = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files
            .Single(static file => file.RelativePath.EndsWith(".Bindings.g.cs", StringComparison.Ordinal)).Content;

        Assert.Contains(
            "__PInvoke.ProgressLike__Args2(fraction, size.Value);",
            managedOutput,
            StringComparison.Ordinal);
        Assert.Contains(
            "__PInvoke.ProgressLike__Args1(fraction);",
            managedOutput,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "__PInvoke.ProgressLike(fraction, size.Value, overlay);",
            managedOutput,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Grouped_managed_output_emits_callback_marshaller_helpers_once_in_alias_file()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "SharpCpp.Sample",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "A/Callbacks.hpp", "B/Callbacks.hpp" });
        var callbackType = BindingType.CreateCallback(
            "std::function<void(int)>",
            "global::System.Action<{0}>",
            managedMarshaller: null,
            BindingType.Void,
            [BindingType.Int32]);
        var first = new FunctionBinding("function:a", "a_callback", "ACallback", BindingType.Void, "sharpcpp_sample_a_callback")
        {
            HeaderIncludePath = "A/Callbacks.hpp"
        };
        var second = new FunctionBinding("function:b", "b_callback", "BCallback", BindingType.Void, "sharpcpp_sample_b_callback")
        {
            HeaderIncludePath = "B/Callbacks.hpp"
        };
        first.Parameters.Add(new ParameterBinding("parameter:a:callback", "callback", "callback", callbackType, cppTypeName: "std::function<void(int)>"));
        second.Parameters.Add(new ParameterBinding("parameter:b:callback", "callback", "callback", callbackType, cppTypeName: "std::function<void(int)>"));
        compilation.Members.Add(first);
        compilation.Members.Add(second);

        var files = BindingOutputRenderer.Render(compilation, "SharpCppSample", path => path?.Split('/')[0] ?? "Root").Files;
        var aliasOutput = files.Single(static file => file.RelativePath == "SharpCppSample.Bindings.Aliases.g.cs").Content;
        var groupOutputs = files.Where(static file => file.RelativePath.EndsWith(".g.cs", StringComparison.Ordinal) &&
            file.RelativePath != "SharpCppSample.Bindings.Aliases.g.cs");
        var nativeGroupOutputs = files.Where(static file => file.RelativePath.EndsWith(".g.cpp", StringComparison.Ordinal)).ToArray();

        Assert.Equal(1, aliasOutput.Split("internal readonly unsafe struct __SharpCppManagedCallbackHandleAbi", StringSplitOptions.None).Length - 1);
        Assert.Equal(1, aliasOutput.Split("private static void __ReleaseManagedCallbackState(nint state)", StringSplitOptions.None).Length - 1);
        Assert.Equal(1, aliasOutput.Split("internal static unsafe partial void __SetManagedCallbackRelease", StringSplitOptions.None).Length - 1);
        Assert.Equal(1, aliasOutput.Split("internal static unsafe class __CallbackMarshaller_void_int", StringSplitOptions.None).Length - 1);
        Assert.All(groupOutputs, file => Assert.DoesNotContain("__SharpCppManagedCallbackHandleAbi", file.Content, StringComparison.Ordinal));
        Assert.Equal(1, nativeGroupOutputs.Count(static file => file.Content.Contains("sharpcpp_set_managed_callback_release", StringComparison.Ordinal)));
    }

    [Fact]
    public void Delegate_callback_parameter_emits_named_delegate_and_parameter_specific_marshaller()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "SharpCpp.Sample",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "Callbacks/Callbacks.hpp" });
        var callbackType = BindingType.CreateCallback(
            "std::function<void(int)>",
            "global::System.Action<{0}>",
            managedMarshaller: null,
            BindingType.Void,
            [BindingType.Int32]);
        var function = new FunctionBinding(
            "function:invoke_callback",
            "invoke_callback",
            "InvokeCallback",
            BindingType.Void,
            "sharpcpp_sample_invoke_callback");
        function.Parameters.Add(new ParameterBinding(
            "parameter:invoke_callback:callback",
            "callback",
            "callback",
            callbackType,
            BindingParameterDirection.In,
            BindingParameterAbiKind.Value,
            "std::function<void(int)>")
        {
            UseCallbackDelegate = true,
            CallbackDelegateTypeName = "ProgressCallback"
        });
        compilation.Members.Add(function);

        var files = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files;
        var managedOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cs", StringComparison.Ordinal)).Content;
        var aliasOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.Aliases.g.cs", StringComparison.Ordinal)).Content;

        Assert.Contains("public static void InvokeCallback(global::SharpCpp.Sample.SampleBindings.ProgressCallback callback)", managedOutput, StringComparison.Ordinal);
        Assert.Contains("public delegate void ProgressCallback(int arg0);", aliasOutput, StringComparison.Ordinal);
        Assert.Contains("[CustomMarshaller(typeof(global::SharpCpp.Sample.SampleBindings.ProgressCallback), MarshalMode.ManagedToUnmanagedIn", aliasOutput, StringComparison.Ordinal);
        Assert.Contains("[MarshalUsing(typeof(global::SharpCpp.Sample.SampleBindings.__CallbackMarshaller_void_int_global__SharpCpp_Sample_SampleBindings_ProgressCallback))] global::SharpCpp.Sample.SampleBindings.ProgressCallback? callback", managedOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Unnamed_delegate_callback_overloads_emit_distinct_generated_delegate_types()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "SharpCpp.Sample",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "Callbacks/Callbacks.hpp" });
        var intCallbackType = BindingType.CreateCallback(
            "std::function<void(int)>",
            "global::System.Action<{0}>",
            managedMarshaller: null,
            BindingType.Void,
            [BindingType.Int32]);
        var floatCallbackType = BindingType.CreateCallback(
            "std::function<void(float)>",
            "global::System.Action<{0}>",
            managedMarshaller: null,
            BindingType.Void,
            [BindingType.Single]);
        var intOverload = new FunctionBinding("function:invoke_callback:int", "invoke_callback", "InvokeCallback", BindingType.Void, "sharpcpp_sample_invoke_callback_int")
        {
            ImportMethodName = "InvokeCallback"
        };
        intOverload.Parameters.Add(new ParameterBinding("parameter:int:callback", "callback", "callback", intCallbackType)
        {
            UseCallbackDelegate = true
        });
        var floatOverload = new FunctionBinding("function:invoke_callback:float", "invoke_callback", "InvokeCallback", BindingType.Void, "sharpcpp_sample_invoke_callback_float")
        {
            ImportMethodName = "InvokeCallback_2"
        };
        floatOverload.Parameters.Add(new ParameterBinding("parameter:float:callback", "callback", "callback", floatCallbackType)
        {
            UseCallbackDelegate = true
        });
        compilation.Members.Add(intOverload);
        compilation.Members.Add(floatOverload);

        var files = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files;
        var aliasOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.Aliases.g.cs", StringComparison.Ordinal)).Content;

        Assert.Contains("public delegate void Global_InvokeCallback_callbackCallback(float arg0);", aliasOutput, StringComparison.Ordinal);
        Assert.Contains("public delegate void Global_InvokeCallback2_callbackCallback(int arg0);", aliasOutput, StringComparison.Ordinal);
        Assert.Contains("[CustomMarshaller(typeof(global::SharpCpp.Sample.SampleBindings.Global_InvokeCallback_callbackCallback), MarshalMode.ManagedToUnmanagedIn", aliasOutput, StringComparison.Ordinal);
        Assert.Contains("[CustomMarshaller(typeof(global::SharpCpp.Sample.SampleBindings.Global_InvokeCallback2_callbackCallback), MarshalMode.ManagedToUnmanagedIn", aliasOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Explicit_callback_managed_marshaller_takes_precedence_over_generated_helper()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "SharpCpp.Sample",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "Callbacks/Callbacks.hpp" });
        var callbackType = BindingType.CreateCallback(
            "std::function<void(int)>",
            "global::System.Action<{0}>",
            "global::SharpCpp.Sample.CustomCallbackMarshaller",
            BindingType.Void,
            [BindingType.Int32]);
        var function = new FunctionBinding(
            "function:invoke_callback",
            "invoke_callback",
            "InvokeCallback",
            BindingType.Void,
            "sharpcpp_sample_invoke_callback");
        function.Parameters.Add(new ParameterBinding(
            "parameter:invoke_callback:callback",
            "callback",
            "callback",
            callbackType,
            BindingParameterDirection.In,
            BindingParameterAbiKind.Value,
            "std::function<void(int)>"));
        compilation.Members.Add(function);

        var files = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files;
        var managedOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cs", StringComparison.Ordinal)).Content;
        var aliasOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.Aliases.g.cs", StringComparison.Ordinal)).Content;

        Assert.Contains("[MarshalUsing(typeof(global::SharpCpp.Sample.CustomCallbackMarshaller))] global::System.Action<int>? callback", managedOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("__CallbackMarshaller_void_int", aliasOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Managed_output_preserves_alias_shaped_public_signatures_and_emits_alias_file()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "Urho3D",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "Math/Vector3.hpp" });
        var vector3TargetType = BindingType.CreateConfigured(
            "Urho3D::Vector3",
            "global::Urho3D.Math.Vector3",
            "global::Urho3D.Math.Vector3");
        var vector3AliasType = vector3TargetType.WithManagedTypeFormatOverride("Vector3");
        compilation.TypeAliases.Add(new BindingTypeAlias("Urho3D::Vector3", "Vector3", "Vector3", vector3TargetType));

        var function = new FunctionBinding(
            "function:jump_by",
            "jump_by",
            "JumpBy",
            BindingType.Void,
            "sharpcpp_sample_jump_by");
        function.Parameters.Add(new ParameterBinding(
            "parameter:jump_by:offset",
            "offset",
            "offset",
            vector3AliasType,
            BindingParameterDirection.In,
            BindingParameterAbiKind.Reference,
            "const Urho3D::Vector3&"));
        compilation.Members.Add(function);

        var files = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files;
        var managedOutput = files.Single(static file => file.RelativePath == "SharpCppSample.Bindings.g.cs").Content;
        var aliasOutput = files.Single(static file => file.RelativePath == "SharpCppSample.Bindings.Aliases.g.cs").Content;

        Assert.Contains("public static void JumpBy(in Vector3 offset)", managedOutput, StringComparison.Ordinal);
        Assert.Contains("internal static partial void JumpBy(in global::Urho3D.Math.Vector3 offset);", managedOutput, StringComparison.Ordinal);
        Assert.Contains("global using Vector3 = global::Urho3D.Math.Vector3;", aliasOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Managed_output_restores_exact_aliases_only_on_public_surface_for_generic_containers()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "Urho3D",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "Container/VariantMap.hpp" });
        var stringHashRecord = new RecordBinding("record:string_hash", "StringHash", "StringHash", "Urho3D::StringHash")
        {
            ManagedNamespace = "Urho3D"
        };
        var variantRecord = new RecordBinding("record:variant", "Variant", "Variant", "Urho3D::Variant")
        {
            ManagedNamespace = "Urho3D"
        };
        var unorderedMapRecord = new RecordBinding("record:ea_unordered_map", "hash_map", "UnorderedMap", "eastl::hash_map")
        {
            ManagedNamespace = "Ea",
            ManagedTypeFormat = "UnorderedMap<{0}, {1}>"
        };
        unorderedMapRecord.GenericParameters.Add(new BindingGenericParameter("TKey", 0));
        unorderedMapRecord.GenericParameters.Add(new BindingGenericParameter("TValue", 1));
        compilation.Members.Add(stringHashRecord);
        compilation.Members.Add(variantRecord);
        compilation.Members.Add(unorderedMapRecord);

        var variantMapType = BindingType.CreateHandle(
            "eastl::hash_map<Urho3D::StringHash, Urho3D::Variant>",
            "UnorderedMap",
            unorderedMapRecord.SymbolId,
            genericArguments: new[] { stringHashRecord.HandleType, variantRecord.HandleType });
        compilation.TypeAliases.Add(new BindingTypeAlias("Urho3D::VariantMap", "VariantMap", "VariantMap", variantMapType));

        var function = new FunctionBinding(
            "function:get_event_data_map",
            "get_event_data_map",
            "GetEventDataMap",
            variantMapType,
            "sharpcpp_sample_get_event_data_map");
        compilation.Members.Add(function);

        var files = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files;
        var managedOutput = files.Single(static file => file.RelativePath == "SharpCppSample.Bindings.g.cs").Content;
        var aliasOutput = files.Single(static file => file.RelativePath == "SharpCppSample.Bindings.Aliases.g.cs").Content;

        Assert.Contains("public static VariantMap GetEventDataMap()", managedOutput, StringComparison.Ordinal);
        Assert.Contains("internal static partial global::Ea.UnorderedMap<global::Urho3D.StringHash, global::Urho3D.Variant> GetEventDataMap();", managedOutput, StringComparison.Ordinal);
        Assert.Contains("global using VariantMap = global::Ea.UnorderedMap<global::Urho3D.StringHash, global::Urho3D.Variant>;", aliasOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Managed_output_restores_aliases_inside_callback_public_signatures()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "Urho3D",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "Core/Object.hpp" });
        var objectRecord = new RecordBinding("record:object", "Object", "Object", "Urho3D::Object")
        {
            ManagedNamespace = "Urho3D"
        };
        var stringHashRecord = new RecordBinding("record:string_hash", "StringHash", "StringHash", "Urho3D::StringHash")
        {
            ManagedNamespace = "Urho3D"
        };
        var variantRecord = new RecordBinding("record:variant", "Variant", "Variant", "Urho3D::Variant")
        {
            ManagedNamespace = "Urho3D"
        };
        var unorderedMapRecord = new RecordBinding("record:ea_unordered_map", "hash_map", "UnorderedMap", "eastl::hash_map")
        {
            ManagedNamespace = "Ea",
            ManagedTypeFormat = "UnorderedMap<{0}, {1}>"
        };
        unorderedMapRecord.GenericParameters.Add(new BindingGenericParameter("TKey", 0));
        unorderedMapRecord.GenericParameters.Add(new BindingGenericParameter("TValue", 1));
        compilation.Members.Add(objectRecord);
        compilation.Members.Add(stringHashRecord);
        compilation.Members.Add(variantRecord);
        compilation.Members.Add(unorderedMapRecord);

        var variantMapType = BindingType.CreateHandle(
            "Urho3D::VariantMap*",
            "UnorderedMap",
            unorderedMapRecord.SymbolId,
            genericArguments: new[] { stringHashRecord.HandleType, variantRecord.HandleType });
        compilation.TypeAliases.Add(new BindingTypeAlias("Urho3D::VariantMap", "VariantMap", "VariantMap", variantMapType));

        var callbackType = BindingType.CreateCallback(
            "ea::function<void(Urho3D::Object*, Urho3D::StringHash, Urho3D::VariantMap&)>",
            "global::System.Action<{0}, {1}, {2}>",
            managedMarshaller: null,
            BindingType.Void,
            [objectRecord.HandleType, stringHashRecord.HandleType, variantMapType]);
        var function = new FunctionBinding(
            "function:subscribe",
            "Subscribe",
            "Subscribe",
            BindingType.Void,
            "sharpcpp_sample_subscribe");
        function.Parameters.Add(new ParameterBinding(
            "parameter:subscribe:handler",
            "handler",
            "handler",
            callbackType,
            BindingParameterDirection.In,
            BindingParameterAbiKind.Value,
            "HandlerFunction"));
        compilation.Members.Add(function);

        var files = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files;
        var managedOutput = files.Single(static file => file.RelativePath == "SharpCppSample.Bindings.g.cs").Content;

        Assert.Contains("public static void Subscribe(global::System.Action<global::Urho3D.Object, global::Urho3D.StringHash, VariantMap> handler)", managedOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("public static void Subscribe(global::System.Action<global::Urho3D.Object, global::Urho3D.StringHash, global::Ea.UnorderedMap", managedOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Generic_method_callback_imports_use_alias_shaped_callback_marshaller_names()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "Urho3D",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "Core/Object.hpp" });
        var objectRecord = new RecordBinding("record:object", "Object", "Object", "Urho3D::Object")
        {
            ManagedNamespace = "Urho3D"
        };
        var stringHashRecord = new RecordBinding("record:string_hash", "StringHash", "StringHash", "Urho3D::StringHash")
        {
            ManagedNamespace = "Urho3D"
        };
        var variantRecord = new RecordBinding("record:variant", "Variant", "Variant", "Urho3D::Variant")
        {
            ManagedNamespace = "Urho3D"
        };
        var unorderedMapRecord = new RecordBinding("record:ea_unordered_map", "hash_map", "UnorderedMap", "eastl::hash_map")
        {
            ManagedNamespace = "Ea",
            ManagedTypeFormat = "UnorderedMap<{0}, {1}>"
        };
        unorderedMapRecord.GenericParameters.Add(new BindingGenericParameter("TKey", 0));
        unorderedMapRecord.GenericParameters.Add(new BindingGenericParameter("TValue", 1));
        compilation.Members.Add(objectRecord);
        compilation.Members.Add(stringHashRecord);
        compilation.Members.Add(variantRecord);
        compilation.Members.Add(unorderedMapRecord);

        var variantMapType = BindingType.CreateHandle(
            "Urho3D::VariantMap*",
            "UnorderedMap",
            unorderedMapRecord.SymbolId,
            genericArguments: new[] { stringHashRecord.HandleType, variantRecord.HandleType });
        compilation.TypeAliases.Add(new BindingTypeAlias("Urho3D::VariantMap", "VariantMap", "VariantMap", variantMapType));

        var callbackType = BindingType.CreateCallback(
            "ea::function<void(Urho3D::Object*, Urho3D::StringHash, Urho3D::VariantMap&)>",
            "global::System.Action<{0}, {1}, {2}>",
            managedMarshaller: null,
            BindingType.Void,
            [objectRecord.HandleType, stringHashRecord.HandleType, variantMapType]);
        var directFunction = new FunctionBinding(
            "function:subscribe_direct",
            "SubscribeDirect",
            "SubscribeDirect",
            BindingType.Void,
            "sharpcpp_sample_subscribe_direct");
        directFunction.Parameters.Add(new ParameterBinding(
            "parameter:subscribe_direct:handler",
            "handler",
            "handler",
            callbackType,
            cppTypeName: "HandlerFunction"));
        compilation.Members.Add(directFunction);

        var subscribe = new FunctionBinding(
            "method:object:subscribe",
            "Subscribe",
            "Subscribe",
            BindingType.Void,
            "sharpcpp_sample_object_subscribe",
            cppQualifiedName: "Urho3D::Object::Subscribe")
        {
            IsStatic = false,
            OwningRecordCppTypeName = "Urho3D::Object"
        };
        subscribe.GenericParameters.Add(new BindingGenericParameter("T", 0));
        subscribe.Parameters.Add(new ParameterBinding(
            "parameter:object:subscribe:handler",
            "handler",
            "handler",
            BindingType.CreateGenericParameter("T"),
            cppTypeName: "T"));
        subscribe.SupportedGenericInstantiations.Add(new RecordGenericInstantiationBinding([callbackType]));
        objectRecord.Members.Add(subscribe);

        var files = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files;
        var managedOutput = files.Single(static file => file.RelativePath == "SharpCppSample.Bindings.g.cs").Content;
        var aliasOutput = files.Single(static file => file.RelativePath == "SharpCppSample.Bindings.Aliases.g.cs").Content;

        const string AliasShapedHelper = "__CallbackMarshaller_void_global__Urho3D_Object_global__Urho3D_StringHash_VariantMap_global__System_Action_global__Urho3D_Object__global__Urho3D_StringHash__VariantMap";
        const string UnaliasedHelperPrefix = "__CallbackMarshaller_void_global__Urho3D_Object_global__Urho3D_StringHash_UnorderedMap";

        Assert.Contains($"[MarshalUsing(typeof(global::Urho3D.SampleBindings.{AliasShapedHelper}))]", managedOutput, StringComparison.Ordinal);
        Assert.DoesNotContain($"[MarshalUsing(typeof(global::Urho3D.SampleBindings.{UnaliasedHelperPrefix}", managedOutput, StringComparison.Ordinal);
        Assert.Contains($"internal static unsafe class {AliasShapedHelper}", aliasOutput, StringComparison.Ordinal);
        Assert.DoesNotContain($"internal static unsafe class {UnaliasedHelperPrefix}", aliasOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Managed_output_uses_shadow_projection_for_namespaces_and_type_qualification()
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
        var scene = new RecordBinding("record:Scene", "Scene", "Scene", "Urho3D::Scene")
        {
            ManagedNamespace = "Urho3D"
        };
        var vectorBuffer = new RecordBinding("record:VectorBuffer", "VectorBuffer", "VectorBuffer", "Urho3D::VectorBuffer")
        {
            ManagedNamespace = "Urho3D"
        };
        var cubeMapFace = new EnumBinding("enum:CubeMapFace", "CubeMapFace", "CubeMapFace", "unsigned int", "Urho3D::CubeMapFace", BindingPrimitiveKind.UInt32)
        {
            ManagedNamespace = "Urho3D"
        };
        cubeMapFace.Values.Add(new EnumValueBinding("FACE_POSITIVE_X", "FACEPOSITIVEX", "0u"));
        var getContext = new FunctionBinding(
            "method:Scene:GetContext",
            "GetContext",
            "GetContext",
            context.HandleType,
            "scrbfx_Scene_GetContext")
        {
            OwningRecordCppTypeName = scene.CppQualifiedName
        };
        scene.Members.Add(getContext);
        var useBuffer = new FunctionBinding(
            "method:Scene:UseBuffer",
            "UseBuffer",
            "UseBuffer",
            BindingType.Void,
            "scrbfx_Scene_UseBuffer")
        {
            OwningRecordCppTypeName = scene.CppQualifiedName
        };
        useBuffer.Parameters.Add(new ParameterBinding(
            "parameter:Scene:UseBuffer:buffer",
            "buffer",
            "buffer",
            BindingType.CreateConfigured("Urho3D::VectorBuffer", "VectorBuffer")));
        scene.Members.Add(useBuffer);
        var clearFace = new FunctionBinding(
            "method:Scene:ClearFace",
            "ClearFace",
            "ClearFace",
            BindingType.Void,
            "scrbfx_Scene_ClearFace")
        {
            OwningRecordCppTypeName = scene.CppQualifiedName
        };
        clearFace.Parameters.Add(new ParameterBinding(
            "parameter:Scene:ClearFace:face",
            "face",
            "face",
            BindingType.CreateEnum("Urho3D::CubeMapFace", "CubeMapFace"))
        {
            ManagedDefaultValueExpression = "CubeMapFace.FACEPOSITIVEX"
        });
        scene.Members.Add(clearFace);
        compilation.Members.Add(context);
        compilation.Members.Add(scene);
        compilation.Members.Add(vectorBuffer);
        compilation.Members.Add(cubeMapFace);
        var contextAlias = new BindingTypeAlias("Urho3D::ContextPtr", "ContextPtr", "ContextPtr", context.HandleType);
        compilation.TypeAliases.Add(contextAlias);

        var projection = new ManagedBindingProjection(compilation.ManagedNamespace);
        projection.AddDeclaration("Urho3D.Core", context);
        projection.AddDeclaration("Urho3D.Scene", scene);
        projection.AddDeclaration("Urho3D.IO", vectorBuffer);
        projection.AddDeclaration("Urho3D.Graphics", cubeMapFace);
        compilation.SetMetadata(MetadataKeys.ManagedBindingProjection, projection);

        var files = BindingOutputRenderer.Render(compilation, "SharpCppRbfx").Files;
        var managedOutput = files.Single(static file => file.RelativePath == "SharpCppRbfx.Bindings.g.cs").Content;
        var aliasOutput = files.Single(static file => file.RelativePath == "SharpCppRbfx.Bindings.Aliases.g.cs").Content;

        Assert.Contains("namespace Urho3D.Core", managedOutput, StringComparison.Ordinal);
        Assert.Contains("namespace Urho3D.Scene", managedOutput, StringComparison.Ordinal);
        Assert.Contains("global::Urho3D.Core.Context", managedOutput, StringComparison.Ordinal);
        Assert.Contains("global::Urho3D.IO.VectorBuffer buffer", managedOutput, StringComparison.Ordinal);
        Assert.Contains("global::Urho3D.Graphics.CubeMapFace face = global::Urho3D.Graphics.CubeMapFace.FACEPOSITIVEX", managedOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("global::Urho3D.Context GetContext()", managedOutput, StringComparison.Ordinal);
        Assert.Contains("global using ContextPtr = global::Urho3D.Core.Context;", aliasOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("namespace Urho3D.Core", aliasOutput, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(aliasOutput, "ContextPtr = global::Urho3D.Core.Context"));
    }

    [Fact]
    public void Managed_output_suppresses_non_trailing_default_values()
    {
        var compilation = new BindingCompilation("TestProject", "Test", "Bindings", "Native", ["Sample.hpp"]);
        var widgets = new RecordBinding("record:Widgets", "Widgets", "Widgets", "Test::Widgets");
        var imageButton = new FunctionBinding(
            "method:Widgets:ImageButton",
            "ImageButton",
            "ImageButton",
            BindingType.Boolean,
            "scrbfx_Widgets_ImageButton")
        {
            IsStatic = true,
            OwningRecordCppTypeName = widgets.CppQualifiedName
        };
        imageButton.Parameters.Add(new ParameterBinding("parameter:framePadding", "framePadding", "framePadding", BindingType.Int32, cppTypeName: "int")
        {
            ManagedDefaultValueExpression = "-1"
        });
        imageButton.Parameters.Add(new ParameterBinding("parameter:bgCol", "bgCol", "bgCol", BindingType.Int32, cppTypeName: "int"));
        widgets.Members.Add(imageButton);
        compilation.Members.Add(widgets);

        var output = BindingOutputRenderer.Render(compilation, "TestProject").Files.Single(static file => file.RelativePath == "TestProject.Bindings.g.cs").Content;

        Assert.Contains("public static bool ImageButton(int framePadding, int bgCol)", output, StringComparison.Ordinal);
        Assert.DoesNotContain("int framePadding = -1, int bgCol", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Grouped_alias_output_uses_projected_namespaces_for_registry_types()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Rbfx.Bindings",
            "Urho3D",
            "Bindings",
            "SharpCpp.Rbfx.Native",
            new[] { "Urho3D/Urho3DAll.h" });
        var rigidBody = new RecordBinding("record:RigidBody", "RigidBody", "RigidBody", "Urho3D::RigidBody")
        {
            HeaderIncludePath = "Physics/RigidBody.h",
            ManagedNamespace = "Urho3D",
            IsPolymorphic = true
        };
        compilation.Members.Add(rigidBody);

        var projection = new ManagedBindingProjection(compilation.ManagedNamespace);
        projection.AddDeclaration("Urho3D.Physics", rigidBody);
        compilation.SetMetadata(MetadataKeys.ManagedBindingProjection, projection);

        var aliasOutput = BindingOutputRenderer.Render(compilation, "SharpCppRbfx", _ => "SharpCppRbfx.Physics").Files
            .Single(static file => file.RelativePath == "SharpCppRbfx.Bindings.Aliases.g.cs").Content;

        Assert.Contains("global::SharpCpp.Support.NativeObjectRegistry.RegisterType<global::Urho3D.Physics.RigidBody>();", aliasOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("RegisterType<RigidBody>()", aliasOutput, StringComparison.Ordinal);
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var offset = 0;
        while (true)
        {
            var index = text.IndexOf(value, offset, StringComparison.Ordinal);
            if (index < 0)
            {
                return count;
            }

            count++;
            offset = index + value.Length;
        }
    }

    [Fact]
    public void Alias_output_emits_catalog_aliases_even_when_unused_on_surviving_surface()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "Urho3D",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "Container/VariantMap.hpp" });
        var stringRecord = new RecordBinding("record:ea_string", "basic_string", "String", "eastl::basic_string")
        {
            ManagedNamespace = "Ea"
        };
        var variantRecord = new RecordBinding("record:variant", "Variant", "Variant", "Urho3D::Variant")
        {
            ManagedNamespace = "Urho3D"
        };
        var unorderedMapRecord = new RecordBinding("record:ea_unordered_map", "hash_map", "UnorderedMap", "eastl::hash_map")
        {
            ManagedNamespace = "Ea",
            ManagedTypeFormat = "UnorderedMap<{0}, {1}>"
        };
        unorderedMapRecord.GenericParameters.Add(new BindingGenericParameter("TKey", 0));
        unorderedMapRecord.GenericParameters.Add(new BindingGenericParameter("TValue", 1));
        compilation.Members.Add(stringRecord);
        compilation.Members.Add(variantRecord);
        compilation.Members.Add(unorderedMapRecord);

        var stringVariantMapType = BindingType.CreateHandle(
            "eastl::hash_map<eastl::basic_string<char>, Urho3D::Variant>",
            "UnorderedMap",
            unorderedMapRecord.SymbolId,
            genericArguments: new[] { stringRecord.HandleType, variantRecord.HandleType });
        compilation.TypeAliases.Add(new BindingTypeAlias("Urho3D::StringVariantMap", "StringVariantMap", "StringVariantMap", stringVariantMapType));

        var files = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files;
        var aliasOutput = files.Single(static file => file.RelativePath == "SharpCppSample.Bindings.Aliases.g.cs").Content;

        Assert.Contains("global using StringVariantMap = global::Ea.UnorderedMap<global::Ea.String, global::Urho3D.Variant>;", aliasOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Managed_output_keeps_aliases_public_but_internal_generic_signatures_fully_qualified()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "Urho3D",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "Container/VectorOfVector3.hpp" });
        var vector3TargetType = BindingType.CreateConfigured(
            "Urho3D::Vector3",
            "global::Urho3D.Math.Vector3",
            "global::Urho3D.Math.Vector3");
        var vectorRecord = new RecordBinding("record:ea_vector", "vector", "Vector", "eastl::vector")
        {
            ManagedNamespace = "Ea",
            ManagedTypeFormat = "Vector<{0}>"
        };
        vectorRecord.GenericParameters.Add(new BindingGenericParameter("T", 0));
        compilation.Members.Add(vectorRecord);
        compilation.TypeAliases.Add(new BindingTypeAlias("Urho3D::Vector3", "Vector3", "Vector3", vector3TargetType));

        var vectorOfVector3 = BindingType.CreateHandle(
            "eastl::vector<Urho3D::Vector3>",
            "Vector",
            vectorRecord.SymbolId,
            genericArguments: new[] { vector3TargetType });
        var function = new FunctionBinding(
            "function:get_path",
            "get_path",
            "GetPath",
            vectorOfVector3,
            "sharpcpp_sample_get_path");
        compilation.Members.Add(function);

        var files = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files;
        var managedOutput = files.Single(static file => file.RelativePath == "SharpCppSample.Bindings.g.cs").Content;

        Assert.Contains("public static global::Ea.Vector<Vector3> GetPath()", managedOutput, StringComparison.Ordinal);
        Assert.Contains("internal static partial global::Ea.Vector<global::Urho3D.Math.Vector3> GetPath();", managedOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("internal static partial global::Ea.Vector<Vector3> GetPath();", managedOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Managed_output_uses_concrete_primitive_types_when_no_alias_metadata_exists()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "Urho3D",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "Core/Thread.hpp" });
        var function = new FunctionBinding(
            "function:get_current_thread_id",
            "get_current_thread_id",
            "GetCurrentThreadID",
            BindingType.CreateConfigured("unsigned long", "ThreadID", primitiveKind: BindingPrimitiveKind.NativeUInt),
            "sharpcpp_sample_get_current_thread_id");
        compilation.Members.Add(function);

        var files = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files;
        var managedOutput = files.Single(static file => file.RelativePath == "SharpCppSample.Bindings.g.cs").Content;

        Assert.Contains("public static nuint GetCurrentThreadID()", managedOutput, StringComparison.Ordinal);
        Assert.Contains("internal static partial nuint GetCurrentThreadID();", managedOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("ThreadID GetCurrentThreadID", managedOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Native_output_uses_arg_t_for_const_char_pointer_parameter()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "SharpCpp.Sample",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "String/String.hpp" });
        var constCharType = BindingType.CreateConfigured("const char*", "global::System.ReadOnlySpan<byte>");
        var function = new FunctionBinding(
            "function:set_name",
            "set_name",
            "SetName",
            BindingType.Void,
            "sharpcpp_sample_set_name");
        function.Parameters.Add(new ParameterBinding(
            "parameter:set_name:name",
            "name",
            "name",
            constCharType,
            BindingParameterDirection.In,
            BindingParameterAbiKind.Value,
            "const char*"));
        compilation.Members.Add(function);

        var files = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files;
        var nativeOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cpp", StringComparison.Ordinal)).Content;

        Assert.Contains("SHARPCPP_EXPORT ret_t<void> sharpcpp_sample_set_name(arg_t<const char*> name)", nativeOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Instance_methods_render_handle_pointer_array_parameters_as_spans()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "SharpCpp.Sample",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "Graphics/DeviceContext.hpp" });
        var textureView = new RecordBinding("record:Sample::TextureView", "TextureView", "TextureView", "Sample::TextureView");
        var deviceContext = new RecordBinding("record:Sample::DeviceContext", "DeviceContext", "DeviceContext", "Sample::DeviceContext");
        var method = new FunctionBinding(
            "method:Sample::DeviceContext:SetTargets",
            "SetTargets",
            "SetTargets",
            BindingType.Void,
            "sharpcpp_sample_set_targets",
            "void",
            "Sample::DeviceContext::SetTargets")
        {
            IsStatic = false,
            IsVirtual = true,
            OwningRecordCppTypeName = "Sample::DeviceContext"
        };
        method.Parameters.Add(new ParameterBinding(
            "parameter:Sample::DeviceContext:SetTargets:count",
            "count",
            "count",
            BindingType.UInt32,
            BindingParameterDirection.In,
            BindingParameterAbiKind.Value,
            "unsigned int"));
        method.Parameters.Add(new ParameterBinding(
            "parameter:Sample::DeviceContext:SetTargets:views",
            "views",
            "views",
            textureView.HandleType,
            BindingParameterDirection.In,
            BindingParameterAbiKind.Pointer,
            "Sample::TextureView**"));
        deviceContext.Members.Add(method);
        compilation.Members.Add(textureView);
        compilation.Members.Add(deviceContext);

        var files = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files;
        var managedOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cs", StringComparison.Ordinal)).Content;
        var nativeOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cpp", StringComparison.Ordinal)).Content;

        Assert.Contains("public virtual void SetTargets(uint count, global::System.ReadOnlySpan<global::SharpCpp.Sample.TextureView?> views)", managedOutput, StringComparison.Ordinal);
        Assert.Contains("global::System.Span<nint> __abi_views", managedOutput, StringComparison.Ordinal);
        Assert.Contains("fixed (nint* __abi_views_ptr = __abi_views)", managedOutput, StringComparison.Ordinal);
        Assert.Contains("internal static unsafe partial void SetTargets(DeviceContext self, uint count, nint* views);", managedOutput, StringComparison.Ordinal);
        Assert.Contains("SHARPCPP_EXPORT ret_t<void> sharpcpp_sample_set_targets(arg_t<Sample::DeviceContext*> self, arg_t<unsigned int> count, arg_t<Sample::TextureView**> views)", nativeOutput, StringComparison.Ordinal);
        Assert.Contains("using FT = void(Sample::DeviceContext::*)(unsigned int, Sample::TextureView**);", nativeOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Instance_methods_render_annotated_primitive_pointer_array_parameters_as_spans()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "SharpCpp.Sample",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "Graphics/DeviceContext.hpp" });
        var deviceContext = new RecordBinding("record:Sample::DeviceContext", "DeviceContext", "DeviceContext", "Sample::DeviceContext");
        var method = new FunctionBinding(
            "method:Sample::DeviceContext:SetOffsets",
            "SetOffsets",
            "SetOffsets",
            BindingType.Void,
            "sharpcpp_sample_set_offsets",
            "void",
            "Sample::DeviceContext::SetOffsets")
        {
            IsStatic = false,
            IsVirtual = true,
            OwningRecordCppTypeName = "Sample::DeviceContext"
        };
        method.Parameters.Add(new ParameterBinding(
            "parameter:Sample::DeviceContext:SetOffsets:count",
            "count",
            "count",
            BindingType.UInt32,
            BindingParameterDirection.In,
            BindingParameterAbiKind.Value,
            "unsigned int"));
        var offsets = new ParameterBinding(
            "parameter:Sample::DeviceContext:SetOffsets:offsets",
            "offsets",
            "offsets",
            BindingType.UInt64,
            BindingParameterDirection.In,
            BindingParameterAbiKind.Pointer,
            "const unsigned long long*");
        offsets.SetMetadata(MetadataKeys.InputPointerArray, true);
        method.Parameters.Add(offsets);
        deviceContext.Members.Add(method);
        compilation.Members.Add(deviceContext);

        var files = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files;
        var managedOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cs", StringComparison.Ordinal)).Content;
        var nativeOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cpp", StringComparison.Ordinal)).Content;

        Assert.Contains("public virtual void SetOffsets(uint count, global::System.ReadOnlySpan<ulong> offsets)", managedOutput, StringComparison.Ordinal);
        Assert.Contains("fixed (ulong* __abi_offsets_ptr = offsets)", managedOutput, StringComparison.Ordinal);
        Assert.Contains("internal static unsafe partial void SetOffsets(DeviceContext self, uint count, ulong* offsets);", managedOutput, StringComparison.Ordinal);
        Assert.Contains("arg_t<const unsigned long long*> offsets", nativeOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Managed_output_uses_string_for_const_char_return()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "SharpCpp.Sample",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "String/String.hpp" });
        var function = new FunctionBinding(
            "function:get_name",
            "get_name",
            "GetName",
            BindingType.CreateConfigured(
                "const char*",
                "global::System.ReadOnlySpan<byte>",
                null,
                "global::SharpCpp.Support.Utf8SpanReturnMarshaller"),
            "sharpcpp_sample_get_name",
            "const char*");
        compilation.Members.Add(function);

        var files = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files;
        var managedOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cs", StringComparison.Ordinal)).Content;

        Assert.Contains("public static global::System.ReadOnlySpan<byte> GetName()", managedOutput, StringComparison.Ordinal);
        Assert.Contains("internal static partial global::System.ReadOnlySpan<byte> GetName();", managedOutput, StringComparison.Ordinal);
        Assert.Contains("MarshalUsing", managedOutput, StringComparison.Ordinal);
        Assert.Contains("Utf8SpanReturnMarshaller", managedOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Native_output_uses_ret_t_of_const_char_pointer_for_const_char_return()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "SharpCpp.Sample",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "String/String.hpp" });
        var function = new FunctionBinding(
            "function:get_name",
            "get_name",
            "GetName",
            BindingType.CreateConfigured("const char*", "string"),
            "sharpcpp_sample_get_name",
            "const char*");
        compilation.Members.Add(function);

        var files = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files;
        var nativeOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cpp", StringComparison.Ordinal)).Content;

        Assert.Contains("ret_t<const char*> sharpcpp_sample_get_name(", nativeOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Managed_output_keeps_legacy_utf8_pointer_return_conversion_local_to_renderer()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "SharpCpp.Sample",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "String/String.hpp" });
        var function = new FunctionBinding(
            "function:get_name",
            "get_name",
            "GetName",
            BindingType.CreateConfigured("const char*", "string", BindingType.IntPtr.ManagedTypeName),
            "sharpcpp_sample_get_name",
            "const char*");
        compilation.Members.Add(function);

        var files = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files;
        var managedOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cs", StringComparison.Ordinal)).Content;

        Assert.Contains("var __result_raw = __PInvoke.GetName();", managedOutput, StringComparison.Ordinal);
        Assert.Contains("return global::System.Runtime.InteropServices.Marshal.PtrToStringUTF8(__result_raw)!;", managedOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Managed_output_uses_ast_direction_and_abi_kind_for_handle_parameters()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "SharpCpp.Sample",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "Records/Composition.hpp" });
        var payloadRecord = new RecordBinding("record:payload_record", "payload_record", "PayloadRecord");
        var function = new FunctionBinding(
            "function:write_payload",
            "write_payload",
            "WritePayload",
            BindingType.Void,
            "sharpcpp_sample_write_payload");
        function.Parameters.Add(new ParameterBinding(
            "parameter:write_payload:payload",
            "payload",
            "payload",
            payloadRecord.HandleType,
            BindingParameterDirection.Out,
            BindingParameterAbiKind.Pointer,
            "payload_record**"));
        compilation.Members.Add(payloadRecord);
        compilation.Members.Add(function);

        var files = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files;
        var managedOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cs", StringComparison.Ordinal)).Content;
        var nativeOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cpp", StringComparison.Ordinal)).Content;

        Assert.Contains("public static void WritePayload(out global::SharpCpp.Sample.PayloadRecord payload)", managedOutput, StringComparison.Ordinal);
        Assert.Contains("internal static partial void WritePayload(out nint payload);", managedOutput, StringComparison.Ordinal);
        Assert.Contains("nint __abi_payload;", managedOutput, StringComparison.Ordinal);
        Assert.Contains("payload = global::SharpCpp.Sample.PayloadRecord.CreateManagedWrapper(__abi_payload, ownsHandle: false);", managedOutput, StringComparison.Ordinal);
        Assert.Contains("public PayloadRecord GetArrayElement(nuint index)", managedOutput, StringComparison.Ordinal);
        Assert.Contains("internal static partial nint GetArrayElement(PayloadRecord self, nuint index);", managedOutput, StringComparison.Ordinal);
        Assert.Contains("sharpcpp_e2e_payload_record_array_element", nativeOutput, StringComparison.Ordinal);
        Assert.Contains("return self != nullptr ? self + index : nullptr;", nativeOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Managed_output_uses_wrapper_types_for_non_ref_handle_import_parameters()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "SharpCpp.Sample",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "Records/Composition.hpp" });
        var ownerRecord = new RecordBinding("record:chain", "chain", "Chain");
        var payloadRecord = new RecordBinding("record:payload_record", "payload_record", "PayloadRecord");
        var method = new FunctionBinding(
            "method:chain:set_payload",
            "set_payload",
            "SetPayload",
            BindingType.Void,
            "entry_set_payload")
        {
            OwningRecordCppTypeName = "chain"
        };
        method.Parameters.Add(new ParameterBinding(
            "parameter:set_payload:value",
            "value",
            "value",
            payloadRecord.HandleType,
            BindingParameterDirection.In,
            BindingParameterAbiKind.Value,
            "payload_record*"));
        ownerRecord.Members.Add(method);
        compilation.Members.Add(ownerRecord);
        compilation.Members.Add(payloadRecord);

        var files = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files;
        var managedOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cs", StringComparison.Ordinal)).Content;

        Assert.Contains("public static void SetPayload(global::SharpCpp.Sample.PayloadRecord value)", managedOutput, StringComparison.Ordinal);
        Assert.Contains("[MarshalUsing(typeof(global::SharpCpp.Sample.__PayloadRecordMarshaller<global::SharpCpp.Support.BorrowedNativeHandle>))] global::SharpCpp.Sample.PayloadRecord? value", managedOutput, StringComparison.Ordinal);
        Assert.Contains("internal static partial void SetPayload(", managedOutput, StringComparison.Ordinal);
        Assert.Contains("__PInvoke.SetPayload(value);", managedOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("GetHandle(value)", managedOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Managed_output_renders_flags_enum_from_ast()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "SharpCpp.Sample",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "Enum/Enum.hpp" });
        var @enum = new EnumBinding("enum:permissions", "permissions", "Permissions", "unsigned int", underlyingPrimitiveKind: BindingPrimitiveKind.UInt32)
        {
            IsFlags = true
        };
        @enum.Values.Add(new EnumValueBinding("none", "None", "0u"));
        @enum.Values.Add(new EnumValueBinding("read", "Read", "1u"));
        compilation.Members.Add(@enum);

        var files = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files;
        var managedOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cs", StringComparison.Ordinal)).Content;

        Assert.Contains("[Flags]", managedOutput, StringComparison.Ordinal);
        Assert.Contains("public enum Permissions : uint", managedOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Managed_output_casts_enum_arguments_to_import_abi_type()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "SharpCpp.Sample",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "Enum/Enum.hpp" });
        var modeAlias = new BindingTypeAlias("mode", "Mode", "Mode", BindingType.Int32);
        var function = new FunctionBinding(
            "function:set_mode",
            "set_mode",
            "SetMode",
            BindingType.Void,
            cppQualifiedName: "set_mode");
        function.Parameters.Add(new ParameterBinding(
            "parameter:set_mode:mode",
            "mode",
            "mode",
            BindingType.Int32));
        compilation.TypeAliases.Add(modeAlias);
        compilation.Members.Add(function);

        var files = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files;
        var managedOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cs", StringComparison.Ordinal)).Content;

        Assert.Contains("public static void SetMode(Mode mode)", managedOutput, StringComparison.Ordinal);
        Assert.Contains("__PInvoke.SetMode((int)mode);", managedOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Managed_output_uses_ast_base_type_and_readonly_field_shape()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "SharpCpp.Sample",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "Records/Inheritance.hpp" });
        var baseRecord = new RecordBinding("record:base_box", "base_box", "BaseBox");
        var derivedRecord = new RecordBinding("record:derived_box", "derived_box", "DerivedBox")
        {
            ManagedBaseTypeName = "BaseBox"
        };
        derivedRecord.Members.Add(new FieldBinding(
            "field:derived_box:count",
            "count",
            "Count",
            BindingType.Int32,
            "int",
            "derived_box",
            isReadOnly: true));
        compilation.Members.Add(baseRecord);
        compilation.Members.Add(derivedRecord);

        var files = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files;
        var managedOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cs", StringComparison.Ordinal)).Content;

        Assert.Contains("public partial class DerivedBox : global::SharpCpp.Sample.BaseBox", managedOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("Set_Count", managedOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Managed_output_can_synthesize_properties_from_ast_annotations()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "SharpCpp.Sample",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "Records/AccessorProperty.hpp" });
        var record = new RecordBinding("record:accessor_box", "accessor_box", "AccessorBox");
        var getter = new FunctionBinding("method:accessor_box:get_count", "get_count", "GetCount", BindingType.Int32, "entry_get") { OwningRecordCppTypeName = "accessor_box" };
        getter.SetMetadata(MetadataKeys.AccessorProperty, new AccessorPropertyAnnotation("Count", AccessorPropertyKind.Getter));
        var setter = new FunctionBinding("method:accessor_box:set_count", "set_count", "SetCount", BindingType.Void, "entry_set") { OwningRecordCppTypeName = "accessor_box" };
        setter.Parameters.Add(new ParameterBinding("parameter:set_count:value", "value", "value", BindingType.Int32));
        setter.SetMetadata(MetadataKeys.AccessorProperty, new AccessorPropertyAnnotation("Count", AccessorPropertyKind.Setter));
        record.Members.Add(getter);
        record.Members.Add(setter);
        compilation.Members.Add(record);

        var files = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files;
        var managedOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cs", StringComparison.Ordinal)).Content;

        Assert.Contains("public int Count", managedOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("public int GetCount()", managedOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("public void SetCount(int value)", managedOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Managed_output_uses_distinct_import_names_for_overloads()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "SharpCpp.Sample",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "Records/Overload.hpp" });
        var freeOverloadA = new FunctionBinding("function:overload:int", "overload_value", "OverloadValue", BindingType.Int32, "entry_overload_int");
        freeOverloadA.Parameters.Add(new ParameterBinding("parameter:overload:value", "value", "value", BindingType.Int32, cppTypeName: "int"));
        var freeOverloadB = new FunctionBinding("function:overload:int,int", "overload_value", "OverloadValue", BindingType.Int32, "entry_overload_int_int");
        freeOverloadB.Parameters.Add(new ParameterBinding("parameter:overload:left", "left", "left", BindingType.Int32, cppTypeName: "int"));
        freeOverloadB.Parameters.Add(new ParameterBinding("parameter:overload:right", "right", "right", BindingType.Int32, cppTypeName: "int"));

        var record = new RecordBinding("record:overload_box", "overload_box", "OverloadBox");
        var ctorA = new FunctionBinding("ctor:overload_box:int", "overload_box", "OverloadBox", record.HandleType, "entry_ctor_int")
        {
            IsConstructor = true,
            OwningRecordCppTypeName = "overload_box"
        };
        ctorA.Parameters.Add(new ParameterBinding("parameter:ctor:value", "value", "value", BindingType.Int32, cppTypeName: "int"));
        var ctorB = new FunctionBinding("ctor:overload_box:int,int", "overload_box", "OverloadBox", record.HandleType, "entry_ctor_int_int")
        {
            IsConstructor = true,
            OwningRecordCppTypeName = "overload_box"
        };
        ctorB.Parameters.Add(new ParameterBinding("parameter:ctor:left", "left", "left", BindingType.Int32, cppTypeName: "int"));
        ctorB.Parameters.Add(new ParameterBinding("parameter:ctor:right", "right", "right", BindingType.Int32, cppTypeName: "int"));
        var methodA = new FunctionBinding("method:mix:int", "mix", "Mix", BindingType.Int32, "entry_mix_int") { OwningRecordCppTypeName = "overload_box" };
        methodA.Parameters.Add(new ParameterBinding("parameter:mix:value", "value", "value", BindingType.Int32, cppTypeName: "int"));
        var methodB = new FunctionBinding("method:mix:int,int", "mix", "Mix", BindingType.Int32, "entry_mix_int_int") { OwningRecordCppTypeName = "overload_box" };
        methodB.Parameters.Add(new ParameterBinding("parameter:mix:left", "left", "left", BindingType.Int32, cppTypeName: "int"));
        methodB.Parameters.Add(new ParameterBinding("parameter:mix:right", "right", "right", BindingType.Int32, cppTypeName: "int"));
        var staticA = new FunctionBinding("static:combine:int", "combine", "Combine", BindingType.Int32, "entry_combine_int") { IsStatic = true, OwningRecordCppTypeName = "overload_box" };
        staticA.Parameters.Add(new ParameterBinding("parameter:combine:value", "value", "value", BindingType.Int32, cppTypeName: "int"));
        var staticB = new FunctionBinding("static:combine:int,int", "combine", "Combine", BindingType.Int32, "entry_combine_int_int") { IsStatic = true, OwningRecordCppTypeName = "overload_box" };
        staticB.Parameters.Add(new ParameterBinding("parameter:combine:left", "left", "left", BindingType.Int32, cppTypeName: "int"));
        staticB.Parameters.Add(new ParameterBinding("parameter:combine:right", "right", "right", BindingType.Int32, cppTypeName: "int"));

        record.Members.Add(ctorA);
        record.Members.Add(ctorB);
        record.Members.Add(methodA);
        record.Members.Add(methodB);
        record.Members.Add(staticA);
        record.Members.Add(staticB);
        compilation.Members.Add(record);
        compilation.Members.Add(freeOverloadA);
        compilation.Members.Add(freeOverloadB);

        var files = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files;
        var managedOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cs", StringComparison.Ordinal)).Content;

        Assert.Contains("using System.Runtime.CompilerServices;", managedOutput, StringComparison.Ordinal);
        AssertAllLibraryImportsAreAggressivelyInlined(managedOutput);
        Assert.Contains("OverloadValue", managedOutput, StringComparison.Ordinal);
        Assert.Contains("OverloadValue2", managedOutput, StringComparison.Ordinal);
        Assert.Contains("OverloadBox", managedOutput, StringComparison.Ordinal);
        Assert.Contains("OverloadBox2", managedOutput, StringComparison.Ordinal);
        Assert.Contains("Mix", managedOutput, StringComparison.Ordinal);
        Assert.Contains("Mix2", managedOutput, StringComparison.Ordinal);
        Assert.Contains("Combine", managedOutput, StringComparison.Ordinal);
        Assert.Contains("Combine2", managedOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Managed_output_renders_owned_wrapper_surface_for_root_records()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "SharpCpp.Sample",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "Records/Constructor.hpp" });
        var record = new RecordBinding("record:widget", "widget", "Widget");
        compilation.Members.Add(record);

        var files = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files;
        var managedOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cs", StringComparison.Ordinal)).Content;

        Assert.Contains("public partial class Widget : global::SharpCpp.Support.NativeObject", managedOutput, StringComparison.Ordinal);
        Assert.Contains("public bool OwnsHandle => OwnsNativeHandle;", managedOutput, StringComparison.Ordinal);
        Assert.Contains("protected override bool ReleaseNativeHandle(nint handle)", managedOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Output_does_not_emit_generic_delete_for_opaque_native_handles()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "SharpCpp.Sample",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "SDL.h" });
        var record = new RecordBinding("record:SDL_Window", "SDL_Window", "SdlWindow", "SDL_Window");
        record.SetMetadata(MetadataKeys.ForwardDeclaration, true);
        record.SetMetadata(MetadataKeys.OpaqueNativeHandle, true);
        record.SetMetadata(MetadataKeys.NonPublicDestructor, true);
        compilation.Members.Add(record);

        var files = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files;
        var managedOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cs", StringComparison.Ordinal)).Content;
        var nativeOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cpp", StringComparison.Ordinal)).Content;

        Assert.Contains("public partial class SdlWindow : global::SharpCpp.Support.NativeObject", managedOutput, StringComparison.Ordinal);
        Assert.Contains("protected override bool ReleaseNativeHandle(nint handle)", managedOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("__PInvoke.Delete(handle);", managedOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("internal static partial void Delete(nint self);", managedOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("sharpcpp_e2e_SDL_Window_delete", nativeOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("MarshallerInvoker::destroy<SDL_Window>", nativeOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Native_interface_wrappers_emit_concrete_pure_virtual_calls_without_lifetime_metadata()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "SharpCpp.Sample",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "Diligent/Interface.hpp" });
        var record = new RecordBinding("record:IValue", "IValue", "IValue", "IValue")
        {
            IsAbstract = true,
            IsPolymorphic = true
        };
        record.SetMetadata(MetadataKeys.NativeInterfaceWrapper, true);
        var getValue = new FunctionBinding(
            "method:IValue:GetValue",
            "GetValue",
            "GetValue",
            BindingType.Int32,
            "entry_get_value",
            cppQualifiedName: "IValue::GetValue")
        {
            IsStatic = false,
            IsVirtual = true,
            IsPureVirtual = true,
            OwningRecordCppTypeName = "IValue"
        };
        record.Members.Add(getValue);
        compilation.Members.Add(record);

        var files = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files;
        var managedOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cs", StringComparison.Ordinal)).Content;
        var nativeOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cpp", StringComparison.Ordinal)).Content;

        Assert.Contains("public partial class IValue : global::SharpCpp.Support.NativeObject", managedOutput, StringComparison.Ordinal);
        Assert.Contains("public virtual int GetValue()", managedOutput, StringComparison.Ordinal);
        Assert.Contains("__PInvoke.GetValue(this)", managedOutput, StringComparison.Ordinal);
        Assert.Contains("INativeObjectRegistration<IValue>.RegistrationCreateFactory => static (handle, ownsHandle) => new IValue(handle, ownsHandle)", managedOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("public abstract partial class IValue", managedOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("Native pure virtual method 'GetValue' is not callable", managedOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("internal static partial void Delete(nint self);", managedOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("GetNativeObjectMetadata", managedOutput, StringComparison.Ordinal);

        Assert.Contains("&IValue::GetValue", nativeOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("sharpcpp_e2e_IValue_delete", nativeOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("sharpcpp_e2e_IValue_type_id", nativeOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("sharpcpp_e2e_IValue_metadata", nativeOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("dynamic_cast<void*>", nativeOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Native_interface_query_emits_owned_managed_wrapper_and_query_interface_thunk()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "SharpCpp.Sample",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "Diligent/Interface.hpp" });
        var objectRecord = new RecordBinding("record:IObject", "IObject", "IObject", "Diligent::IObject");
        objectRecord.SetMetadata(MetadataKeys.NativeInterfaceWrapper, true);
        var deviceRecord = new RecordBinding("record:IRenderDeviceVk", "IRenderDeviceVk", "IRenderDeviceVk", "Diligent::IRenderDeviceVk");
        deviceRecord.SetMetadata(MetadataKeys.NativeInterfaceWrapper, true);
        deviceRecord.SetMetadata(MetadataKeys.NativeInterfaceReleaseMethod, "Release");
        deviceRecord.SetMetadata(
            MetadataKeys.NativeInterfaceQuery,
            new NativeInterfaceQueryAnnotation("Diligent::IObject", "Diligent::IID_RenderDeviceVk"));
        compilation.Members.Add(objectRecord);
        compilation.Members.Add(deviceRecord);

        var files = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files;
        var managedOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cs", StringComparison.Ordinal)).Content;
        var nativeOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cpp", StringComparison.Ordinal)).Content;

        Assert.Contains("public static IRenderDeviceVk? QueryManagedWrapper(global::SharpCpp.Sample.IObject source)", managedOutput, StringComparison.Ordinal);
        Assert.Contains("return handle == 0 ? null : CreateManagedWrapper(handle, ownsHandle: true);", managedOutput, StringComparison.Ordinal);
        Assert.Contains("internal static partial nint QueryInterface(IObject source);", managedOutput, StringComparison.Ordinal);
        Assert.Contains("__PInvoke.ReleaseOwnedHandle(handle);", managedOutput, StringComparison.Ordinal);
        Assert.Contains("Diligent::IRenderDeviceVk* result = nullptr;", nativeOutput, StringComparison.Ordinal);
        Assert.Contains("self->QueryInterface(Diligent::IID_RenderDeviceVk, reinterpret_cast<Diligent::IObject**>(&result));", nativeOutput, StringComparison.Ordinal);
        Assert.Contains("self->Release();", nativeOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Managed_output_renders_dispose_override_for_derived_records()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "SharpCpp.Sample",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "Records/Inheritance.hpp" });
        var baseRecord = new RecordBinding("record:base_widget", "base_widget", "BaseWidget");
        var derivedRecord = new RecordBinding("record:derived_widget", "derived_widget", "DerivedWidget")
        {
            ManagedBaseTypeName = "BaseWidget"
        };
        compilation.Members.Add(baseRecord);
        compilation.Members.Add(derivedRecord);

        var files = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files;
        var managedOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cs", StringComparison.Ordinal)).Content;

        Assert.Contains("public partial class DerivedWidget : global::SharpCpp.Sample.BaseWidget", managedOutput, StringComparison.Ordinal);
        Assert.Contains("protected override bool ReleaseNativeHandle(nint handle)", managedOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("public bool OwnsHandle => OwnsNativeHandle;", managedOutput[(managedOutput.IndexOf("public partial class DerivedWidget", StringComparison.Ordinal))..], StringComparison.Ordinal);
    }

    [Fact]
    public void Managed_output_can_mark_handle_returns_as_owned_from_ast()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "SharpCpp.Sample",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "Records/Factory.hpp" });
        var record = new RecordBinding("record:widget", "widget", "Widget");
        var factory = new FunctionBinding("function:create_widget", "create_widget", "CreateWidget", record.HandleType, "entry_create_widget")
        {
            ReturnsOwnedHandle = true
        };
        compilation.Members.Add(record);
        compilation.Members.Add(factory);

        var files = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files;
        var managedOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cs", StringComparison.Ordinal)).Content;

        Assert.Contains("internal static class __WidgetMarshaller<TOwnership>", managedOutput, StringComparison.Ordinal);
        Assert.Contains("[return: MarshalUsing(typeof(global::SharpCpp.Sample.__WidgetMarshaller<global::SharpCpp.Support.OwnedNativeHandle>))]", managedOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("__WidgetOwnedReturnMarshaller", managedOutput, StringComparison.Ordinal);
        Assert.Contains("internal static partial global::SharpCpp.Sample.Widget CreateWidget();", managedOutput, StringComparison.Ordinal);
        Assert.Contains("return __result;", managedOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Output_treats_wrapped_record_value_returns_as_owned_handles()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "SharpCpp.Sample",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "Records/Variant.hpp" });
        var variant = new RecordBinding("record:variant", "Variant", "Variant", "Sample::Variant");
        var spline = new RecordBinding("record:spline", "Spline", "Spline", "Sample::Spline");
        var getPoint = new FunctionBinding(
            "method:spline:get_point",
            "GetPoint",
            "GetPoint",
            BindingType.CreateHandle("Sample::Variant", "Variant", variant.SymbolId, cppIsRecordValue: true),
            cppQualifiedName: "Sample::Spline::GetPoint")
        {
            IsStatic = false,
            OwningRecordCppTypeName = spline.CppQualifiedName,
            CppReturnTypeName = "Sample::Variant"
        };
        spline.Members.Add(getPoint);
        compilation.Members.Add(variant);
        compilation.Members.Add(spline);

        var files = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files;
        var managedOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cs", StringComparison.Ordinal)).Content;
        var nativeOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cpp", StringComparison.Ordinal)).Content;

        Assert.Contains("[return: MarshalUsing(typeof(global::SharpCpp.Sample.__VariantMarshaller<global::SharpCpp.Support.OwnedNativeHandle>))]", managedOutput, StringComparison.Ordinal);
        Assert.Contains("internal static partial global::SharpCpp.Sample.Variant GetPoint(Spline self);", managedOutput, StringComparison.Ordinal);
        Assert.Contains("SHARPCPP_EXPORT arg_t<Sample::Variant*>", nativeOutput, StringComparison.Ordinal);
        Assert.Contains("return MarshallerInvoker::invoke_owned<FT, std::tuple<>>(&Sample::Spline::GetPoint, self);", nativeOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Output_treats_pointer_qualified_record_value_returns_as_borrowed_handles()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "SharpCpp.Sample",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "Records/Vector.hpp" });
        var variant = new RecordBinding("record:variant", "Variant", "Variant", "Sample::Variant")
        {
            ManagedNamespace = "SharpCpp.Sample"
        };
        var vector = new RecordBinding("record:vector", "vector", "Vector", "eastl::vector")
        {
            ManagedNamespace = "Ea",
            ManagedTypeFormat = "global::Ea.Vector<{0}>"
        };
        vector.GenericParameters.Add(new BindingGenericParameter("T", 0));

        var pointerQualifiedVariant = BindingType.CreateHandle(
            "Sample::Variant*",
            "Variant",
            variant.SymbolId,
            cppIsRecordValue: true);
        vector.SupportedGenericInstantiations.Add(new RecordGenericInstantiationBinding([pointerQualifiedVariant]));

        var getAtCore = new FunctionBinding(
            "method:vector:get_at_core",
            "GetAtCore",
            "GetAtCore",
            BindingType.CreateGenericParameter("T"),
            "entry_get_at_core")
        {
            IsStatic = false,
            OwningRecordCppTypeName = "eastl::vector"
        };
        vector.Members.Add(getAtCore);
        compilation.Members.Add(variant);
        compilation.Members.Add(vector);

        var nativeOutput = BindingOutputRenderer.Render(compilation, "SharpCppSample")
            .Files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cpp", StringComparison.Ordinal)).Content;

        Assert.Contains("SHARPCPP_EXPORT ret_t<Sample::Variant*>", nativeOutput, StringComparison.Ordinal);
        Assert.Contains("return MarshallerInvoker::invoke<FT, std::tuple<>>(&eastl::vector<Sample::Variant*>::GetAtCore, self);", nativeOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("arg_t<Sample::Variant**>", nativeOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("invoke_owned<FT, std::tuple<>>(&eastl::vector<Sample::Variant*>::GetAtCore", nativeOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Output_renders_generic_methods_on_non_generic_records()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "SharpCpp.Sample",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "Records/Variant.hpp" });
        var variant = new RecordBinding("record:variant", "Variant", "Variant", "Rml::Variant");
        var stringType = BindingType.CreateConfigured(
            "Rml::String",
            "global::Ea.String",
            marshallerTypeName: null,
            managedMarshaller: "global::Ea.__StringMarshaller<global::SharpCpp.Support.BorrowedNativeHandle>");
        var constStringRefType = BindingType.CreateConfigured(
            "const Rml::String&",
            "global::Ea.String",
            marshallerTypeName: null,
            managedMarshaller: "global::Ea.__StringMarshaller<global::SharpCpp.Support.BorrowedNativeHandle>",
            cppIsReference: true);
        var get = new FunctionBinding(
            "method:variant:get",
            "Get",
            "Get",
            BindingType.CreateGenericParameter("T"),
            entryPointName: "sample_variant_get",
            cppReturnTypeName: "T",
            cppQualifiedName: "Rml::Variant::Get")
        {
            IsStatic = false,
            IsConstMethod = true,
            OwningRecordCppTypeName = "Rml::Variant"
        };
        get.GenericParameters.Add(new BindingGenericParameter("T", 0));
        var defaultValue = new ParameterBinding(
            "parameter:variant:get:default_value",
            "default_value",
            "defaultValue",
            BindingType.CreateGenericParameter("T"),
            cppTypeName: "T");
        defaultValue.SetMetadata(MetadataKeys.UnexpressibleDefault, true);
        get.Parameters.Add(defaultValue);
        get.SupportedGenericInstantiations.Add(new RecordGenericInstantiationBinding([stringType]));
        var assign = new FunctionBinding(
            "method:variant:assign",
            "operator=",
            "Assign",
            BindingType.CreateHandle("Rml::Variant&", "Variant", variant.SymbolId),
            entryPointName: "sample_variant_assign",
            cppReturnTypeName: "Rml::Variant&",
            cppQualifiedName: "Rml::Variant::operator=")
        {
            IsStatic = false,
            OwningRecordCppTypeName = "Rml::Variant"
        };
        assign.GenericParameters.Add(new BindingGenericParameter("T", 0));
        assign.Parameters.Add(new ParameterBinding(
            "parameter:variant:assign:value",
            "value",
            "value",
            BindingType.CreateGenericParameter("T"),
            cppTypeName: "T&&"));
        assign.SupportedGenericInstantiations.Add(new RecordGenericInstantiationBinding([constStringRefType]));
        variant.Members.Add(get);
        variant.Members.Add(assign);
        compilation.Members.Add(variant);

        var files = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files;
        var managedOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cs", StringComparison.Ordinal)).Content;
        var nativeOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cpp", StringComparison.Ordinal)).Content;

        Assert.Contains("public global::SharpCpp.Sample.Variant Assign<T>(T value)", managedOutput, StringComparison.Ordinal);
        Assert.Contains("public global::SharpCpp.Sample.Variant Assign(global::Ea.String value)", managedOutput, StringComparison.Ordinal);
        Assert.Contains("public T Get<T>(T? defaultValue = default)", managedOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("public T Get<T>()", managedOutput, StringComparison.Ordinal);
        Assert.Contains("if (defaultValue is not null)", managedOutput, StringComparison.Ordinal);
        Assert.Contains("if (defaultValue is null)", managedOutput, StringComparison.Ordinal);
        Assert.Contains("public global::Ea.String Get(global::Ea.String defaultValue)", managedOutput, StringComparison.Ordinal);
        Assert.Contains("__Args0", managedOutput, StringComparison.Ordinal);
        Assert.Contains("typeof(T) == typeof(global::Ea.String)", managedOutput, StringComparison.Ordinal);
        Assert.Contains("using FT = Rml::String(Rml::Variant::*)(Rml::String) const;", nativeOutput, StringComparison.Ordinal);
        Assert.Contains("->template Get<Rml::String>()", nativeOutput, StringComparison.Ordinal);
        Assert.Contains("using FT = Rml::Variant&(Rml::Variant::*)(const Rml::String&);", nativeOutput, StringComparison.Ordinal);
        Assert.Contains("&Rml::Variant::operator=<const Rml::String&>", nativeOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Managed_output_honors_custom_cache_policy_and_base_release_strategy()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "SharpCpp.Sample",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "Records/RefCounted.hpp" });
        var record = new RecordBinding("record:payload", "payload", "Payload")
        {
            ManagedBaseTypeName = "global::Urho3D.Interop.RefCountedNativeObject",
            ManagedInstanceCacheFactoryExpression = "global::Urho3D.Interop.RefCountedInstanceCache.Shared",
            ManagedInstanceCacheTypeName = "global::SharpCpp.Support.INativeInstanceCache",
            EmitDefaultNativeReleaseOverride = false
        };
        var field = new FieldBinding("field:payload:child", "child", "Child", record.HandleType, "payload*", "payload")
        {
            ReturnsOwnedHandle = true
        };
        record.Members.Add(field);
        compilation.Members.Add(record);

        var files = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files;
        var managedOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cs", StringComparison.Ordinal)).Content;

        Assert.Contains("internal static readonly global::SharpCpp.Support.INativeInstanceCache s_instanceCache = global::Urho3D.Interop.RefCountedInstanceCache.Shared;", managedOutput, StringComparison.Ordinal);
        Assert.Contains("RegistrationInstanceCache => s_instanceCache;", managedOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("protected override bool ReleaseNativeHandle(nint handle)", managedOutput, StringComparison.Ordinal);
        Assert.Contains("[return: MarshalUsing(typeof(global::SharpCpp.Sample.__PayloadMarshaller<global::SharpCpp.Support.OwnedNativeHandle>))]", managedOutput, StringComparison.Ordinal);
        Assert.Contains("internal static partial global::SharpCpp.Sample.Payload Get_Child(Payload self);", managedOutput, StringComparison.Ordinal);
        Assert.Contains("return __PInvoke.Get_Child(this);", managedOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Managed_output_emits_clone_surface_for_records_with_copy_constructors()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "SharpCpp.Sample",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "Records/Clone.hpp" });
        var record = new RecordBinding("record:clone_counter", "clone_counter", "CloneCounter")
        {
            HasCopyConstructor = true
        };
        compilation.Members.Add(record);

        var files = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files;
        var managedOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cs", StringComparison.Ordinal)).Content;
        var nativeOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cpp", StringComparison.Ordinal)).Content;

        Assert.Contains("global::System.ICloneable", managedOutput, StringComparison.Ordinal);
        Assert.Contains("public CloneCounter Clone()", managedOutput, StringComparison.Ordinal);
        Assert.Contains("object global::System.ICloneable.Clone()", managedOutput, StringComparison.Ordinal);
        Assert.Contains("[return: MarshalUsing(typeof(global::SharpCpp.Sample.__CloneCounterMarshaller<global::SharpCpp.Support.OwnedNativeHandle>))]", managedOutput, StringComparison.Ordinal);
        Assert.Contains("internal static partial global::SharpCpp.Sample.CloneCounter CloneCopyCtor(CloneCounter self);", managedOutput, StringComparison.Ordinal);
        Assert.Contains("sharpcpp_e2e_clone_counter_clone", nativeOutput, StringComparison.Ordinal);
        Assert.Contains("return detail::TryClone<clone_counter>(instance, 0);", nativeOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Managed_output_emits_polymorphic_wrapper_factory_and_registry_hooks()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "SharpCpp.Sample",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "Records/Inheritance.hpp" });
        var baseRecord = new RecordBinding("record:base_counter", "base_counter", "BaseCounter")
        {
            IsPolymorphic = true
        };
        var derivedRecord = new RecordBinding("record:derived_counter", "derived_counter", "DerivedCounter")
        {
            ManagedBaseTypeName = "BaseCounter",
            IsPolymorphic = true
        };
        compilation.Members.Add(baseRecord);
        compilation.Members.Add(derivedRecord);

        var files = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files;
        var managedOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cs", StringComparison.Ordinal)).Content;
        var nativeOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cpp", StringComparison.Ordinal)).Content;

        Assert.Contains("internal static readonly global::SharpCpp.Support.DefaultNativeInstanceCache s_instanceCache = new global::SharpCpp.Support.DefaultNativeInstanceCache();", managedOutput, StringComparison.Ordinal);
        Assert.Contains("global::SharpCpp.Support.NativeObjectRegistry.Create<BaseCounter>(handle, ownsHandle)", managedOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("if (s_instanceCache.TryGetExisting(handle, ownsHandle, out var existing)", managedOutput, StringComparison.Ordinal);
        Assert.Contains("[global::System.Runtime.CompilerServices.ModuleInitializer]", managedOutput, StringComparison.Ordinal);
        Assert.Contains("global::SharpCpp.Support.NativeObjectRegistry.RegisterType<global::SharpCpp.Sample.BaseCounter>();", managedOutput, StringComparison.Ordinal);
        Assert.Contains("global::SharpCpp.Support.NativeObjectRegistry.RegisterType<global::SharpCpp.Sample.DerivedCounter>();", managedOutput, StringComparison.Ordinal);
        Assert.Contains("INativeObjectRegistration<BaseCounter>", managedOutput, StringComparison.Ordinal);
        Assert.Contains("sharpcpp_e2e_base_counter_type_id", nativeOutput, StringComparison.Ordinal);
        Assert.Contains("sharpcpp_e2e_base_counter_metadata", nativeOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Managed_registry_skips_polymorphic_records_without_wrapper_factory()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "SharpCpp.Sample",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "Records/Inheritance.hpp" });
        var abstractBase = new RecordBinding("record:abstract_base", "abstract_base", "AbstractBase")
        {
            IsPolymorphic = true
        };
        var pureVirtual = new FunctionBinding("method:abstract_base:tick", "tick", "Tick", BindingType.Void, "entry_tick")
        {
            IsVirtual = true,
            IsPureVirtual = true,
            OwningRecordCppTypeName = "abstract_base"
        };
        abstractBase.Members.Add(pureVirtual);
        var inheritedAbstract = new RecordBinding("record:inherited_abstract", "inherited_abstract", "InheritedAbstract")
        {
            ManagedBaseTypeName = "AbstractBase",
            IsPolymorphic = true
        };
        inheritedAbstract.Bases.Add(new RecordBaseBinding(
            abstractBase.SymbolId,
            abstractBase.NativeName,
            abstractBase.ManagedName,
            abstractBase.CppQualifiedName,
            isVirtual: false));
        var concreteRecord = new RecordBinding("record:concrete_counter", "concrete_counter", "ConcreteCounter")
        {
            IsPolymorphic = true
        };
        compilation.Members.Add(abstractBase);
        compilation.Members.Add(inheritedAbstract);
        compilation.Members.Add(concreteRecord);

        var files = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files;
        var managedOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cs", StringComparison.Ordinal)).Content;

        Assert.Contains("public abstract partial class InheritedAbstract", managedOutput, StringComparison.Ordinal);
        Assert.Contains("INativeObjectRegistration<InheritedAbstract>.RegistrationCreateFactory => null", managedOutput, StringComparison.Ordinal);
        Assert.Contains("global::SharpCpp.Support.NativeObjectRegistry.RegisterType<global::SharpCpp.Sample.ConcreteCounter>();", managedOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("global::SharpCpp.Support.NativeObjectRegistry.RegisterType<global::SharpCpp.Sample.InheritedAbstract>();", managedOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Protected_pure_virtual_hooks_do_not_make_native_derived_wrappers_abstract()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "SharpCpp.Sample",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "Records/Inheritance.hpp" });
        var nativeBase = new RecordBinding("record:native_base", "native_base", "NativeBase")
        {
            IsPolymorphic = true,
            IsAbstract = true
        };
        var protectedHook = new FunctionBinding("method:native_base:on_update", "on_update", "OnUpdate", BindingType.Void, "entry_on_update")
        {
            IsStatic = false,
            AccessModifier = BindingAccessModifier.Protected,
            IsVirtual = true,
            IsPureVirtual = true,
            OwningRecordCppTypeName = "native_base"
        };
        nativeBase.Members.Add(protectedHook);
        var nativeDerived = new RecordBinding("record:native_derived", "native_derived", "NativeDerived")
        {
            ManagedBaseTypeName = "NativeBase",
            IsPolymorphic = true
        };
        nativeDerived.Bases.Add(new RecordBaseBinding(
            nativeBase.SymbolId,
            nativeBase.NativeName,
            nativeBase.ManagedName,
            nativeBase.CppQualifiedName,
            isVirtual: false));
        compilation.Members.Add(nativeBase);
        compilation.Members.Add(nativeDerived);

        var files = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files;
        var managedOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cs", StringComparison.Ordinal)).Content;

        Assert.Contains("protected virtual void OnUpdate()", managedOutput, StringComparison.Ordinal);
        Assert.Contains("Native pure virtual method 'OnUpdate' is not callable from managed code.", managedOutput, StringComparison.Ordinal);
        Assert.Contains("public partial class NativeDerived", managedOutput, StringComparison.Ordinal);
        Assert.Contains("INativeObjectRegistration<NativeDerived>.RegistrationCreateFactory => static (handle, ownsHandle) => new NativeDerived(handle, ownsHandle)", managedOutput, StringComparison.Ordinal);
        Assert.Contains("global::SharpCpp.Support.NativeObjectRegistry.RegisterType<global::SharpCpp.Sample.NativeDerived>();", managedOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Director_records_emit_virtual_managed_methods_and_native_director_hooks()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "SharpCpp.Sample",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "Records/Director.hpp" });
        var record = new RecordBinding("record:callback_counter", "callback_counter", "CallbackCounter")
        {
            IsDirector = true
        };
        var childRecord = new RecordBinding("record:callback_child", "callback_child", "CallbackChild");
        var ctor = new FunctionBinding("ctor:callback_counter:int", "callback_counter", "CallbackCounter", record.HandleType, "entry_ctor")
        {
            IsConstructor = true,
            OwningRecordCppTypeName = "callback_counter"
        };
        ctor.Parameters.Add(new ParameterBinding("parameter:seed", "seed", "seed", BindingType.Int32, cppTypeName: "int"));
        var getValue = new FunctionBinding("method:callback_counter:get_value", "get_value", "GetValue", BindingType.Int32, "entry_get")
        {
            IsStatic = false,
            IsVirtual = true,
            IsConstMethod = true,
            OwningRecordCppTypeName = "callback_counter"
        };
        getValue.Parameters.Add(new ParameterBinding("parameter:callback_counter:get_value:amount", "amount", "amount", BindingType.Int32, cppTypeName: "int"));
        var callTwice = new FunctionBinding("method:callback_counter:call_virtual_twice", "call_virtual_twice", "CallVirtualTwice", BindingType.Int32, "entry_call")
        {
            IsStatic = false,
            IsConstMethod = true,
            OwningRecordCppTypeName = "callback_counter"
        };
        var getChild = new FunctionBinding("method:callback_counter:get_child", "get_child", "GetChild", childRecord.HandleType, "entry_get_child")
        {
            IsStatic = false,
            IsVirtual = true,
            IsConstMethod = true,
            OwningRecordCppTypeName = "callback_counter"
        };
        record.Members.Add(ctor);
        record.Members.Add(getValue);
        record.Members.Add(callTwice);
        record.Members.Add(getChild);
        compilation.Members.Add(childRecord);
        compilation.Members.Add(record);

        BindingCompilationPolymorphismPass.Apply(compilation);

        var files = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files;
        var managedOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cs", StringComparison.Ordinal)).Content;
        var nativeOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cpp", StringComparison.Ordinal)).Content;

        Assert.Contains("public virtual int GetValue(int amount)", managedOutput, StringComparison.Ordinal);
        Assert.Contains("private void ConnectDirector()", managedOutput, StringComparison.Ordinal);
        Assert.Contains("Marshal.GetFunctionPointerForDelegate", managedOutput, StringComparison.Ordinal);
        Assert.Contains("ParameterMarshaller<int> __arg0_marshaller;", nativeOutput, StringComparison.Ordinal);
        Assert.Contains("auto __abi_arg0 = __arg0_marshaller.ConvertToAbi(amount);", nativeOutput, StringComparison.Ordinal);
        Assert.Contains("ReturnMarshaller<int> __return_marshaller;", nativeOutput, StringComparison.Ordinal);
        Assert.Contains("return __return_marshaller.ConvertToCpp(__abi_result);", nativeOutput, StringComparison.Ordinal);
        Assert.Contains("return global::SharpCpp.Support.NativeObject.GetHandle(__result);", managedOutput, StringComparison.Ordinal);
        Assert.Contains("class __callback_counter_director : public callback_counter", nativeOutput, StringComparison.Ordinal);
        Assert.Contains("sharpcpp_e2e_callback_counter_connect_director", nativeOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Director_callbase_entrypoints_are_unique_for_overloaded_virtual_methods()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "SharpCpp.Sample",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "Records/Director.hpp" });
        var record = new RecordBinding("record:overloaded_director", "overloaded_director", "OverloadedDirector")
        {
            IsDirector = true
        };
        var loadFromId = new FunctionBinding("method:overloaded_director:load:int", "load", "Load", BindingType.Boolean, "entry_load_int")
        {
            IsStatic = false,
            IsVirtual = true,
            OwningRecordCppTypeName = "overloaded_director"
        };
        loadFromId.Parameters.Add(new ParameterBinding("parameter:overloaded_director:load:id", "id", "id", BindingType.Int32, cppTypeName: "int"));
        var loadFromName = new FunctionBinding("method:overloaded_director:load:name", "load", "Load", BindingType.Boolean, "entry_load_name")
        {
            IsStatic = false,
            IsVirtual = true,
            OwningRecordCppTypeName = "overloaded_director"
        };
        loadFromName.Parameters.Add(new ParameterBinding("parameter:overloaded_director:load:name", "name", "name", BindingType.CreateConfigured("const char*", "global::System.IntPtr"), cppTypeName: "const char*"));
        record.Members.Add(loadFromId);
        record.Members.Add(loadFromName);
        compilation.Members.Add(record);

        BindingCompilationPolymorphismPass.Apply(compilation);

        var nativeOutput = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files
            .Single(static file => file.RelativePath.EndsWith(".Bindings.g.cpp", StringComparison.Ordinal)).Content;

        Assert.Contains("sharpcpp_e2e_overloaded_director_callbase_Load(", nativeOutput, StringComparison.Ordinal);
        Assert.Contains("sharpcpp_e2e_overloaded_director_callbase_Load2(", nativeOutput, StringComparison.Ordinal);
        Assert.Contains("__callbase_Load2", nativeOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Director_records_qualify_inherited_callback_owner_references()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "SharpCpp.Sample",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "Records/Director.hpp" });
        var baseRecord = new RecordBinding("record:base_callback", "base_callback", "BaseCallback", "Sample::BaseCallback")
        {
            IsDirector = true,
            ManagedNamespace = "SharpCpp.Sample.Core"
        };
        var onEvent = new FunctionBinding("method:base_callback:on_event", "on_event", "OnEvent", BindingType.Void, "entry_on_event")
        {
            IsStatic = false,
            IsVirtual = true,
            AccessModifier = BindingAccessModifier.Protected,
            OwningRecordCppTypeName = "Sample::BaseCallback"
        };
        baseRecord.Members.Add(onEvent);

        var derivedRecord = new RecordBinding("record:derived_callback", "derived_callback", "DerivedCallback", "Sample::DerivedCallback")
        {
            IsDirector = true,
            ManagedBaseTypeName = "global::SharpCpp.Sample.Core.BaseCallback",
            ManagedNamespace = "SharpCpp.Sample.Graphics"
        };
        derivedRecord.Bases.Add(new RecordBaseBinding(baseRecord.SymbolId, baseRecord.NativeName, baseRecord.ManagedName, baseRecord.CppQualifiedName, isVirtual: false));
        compilation.Members.Add(baseRecord);
        compilation.Members.Add(derivedRecord);

        BindingCompilationPolymorphismPass.Apply(compilation);

        var managedOutput = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files
            .Single(static file => file.RelativePath.EndsWith(".Bindings.g.cs", StringComparison.Ordinal)).Content;

        Assert.Contains("global::SharpCpp.Sample.Graphics.DerivedCallback.__GetBaseCallback_OnEventPointer(runtimeType)", managedOutput, StringComparison.Ordinal);
        Assert.Contains("typeof(global::SharpCpp.Sample.Core.BaseCallback)", managedOutput, StringComparison.Ordinal);
        Assert.Contains("\"OnEvent\"", managedOutput, StringComparison.Ordinal);
        Assert.Contains("global::SharpCpp.Sample.Graphics.DerivedCallback.CreateManagedWrapper(self, ownsHandle: false)", managedOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("return HasManagedOverride(runtimeType, typeof(BaseCallback)", managedOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Director_records_render_protected_members_with_director_helpers()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "SharpCpp.Sample",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "Records/Protected.hpp" });
        var record = new RecordBinding("record:protected_counter", "protected_counter", "ProtectedCounter")
        {
            IsDirector = true
        };
        record.Members.Add(new FunctionBinding("ctor:protected_counter:int", "protected_counter", "ProtectedCounter", record.HandleType, "entry_ctor")
        {
            IsConstructor = true,
            AccessModifier = BindingAccessModifier.Protected,
            OwningRecordCppTypeName = "protected_counter"
        });
        ((FunctionBinding)record.Members[^1]).Parameters.Add(new ParameterBinding("parameter:seed", "seed", "seed", BindingType.Int32, cppTypeName: "int"));
        record.Members.Add(new FunctionBinding("method:protected_counter:increment", "increment", "Increment", BindingType.Int32, "entry_increment")
        {
            IsStatic = false,
            AccessModifier = BindingAccessModifier.Protected,
            OwningRecordCppTypeName = "protected_counter"
        });
        ((FunctionBinding)record.Members[^1]).Parameters.Add(new ParameterBinding("parameter:amount", "amount", "amount", BindingType.Int32, cppTypeName: "int"));
        record.Members.Add(new FunctionBinding("method:protected_counter:scale", "scale", "Scale", BindingType.Int32, "entry_scale")
        {
            IsStatic = false,
            IsVirtual = true,
            AccessModifier = BindingAccessModifier.Protected,
            OwningRecordCppTypeName = "protected_counter"
        });
        ((FunctionBinding)record.Members[^1]).Parameters.Add(new ParameterBinding("parameter:value", "value", "value", BindingType.Int32, cppTypeName: "int"));
        record.Members.Add(new FunctionBinding("method:protected_counter:call_scale", "call_scale", "CallScale", BindingType.Int32, "entry_call_scale")
        {
            IsStatic = false,
            OwningRecordCppTypeName = "protected_counter"
        });
        ((FunctionBinding)record.Members[^1]).Parameters.Add(new ParameterBinding("parameter:value", "value", "value", BindingType.Int32, cppTypeName: "int"));
        record.Members.Add(new FieldBinding("field:protected_counter:factor_", "factor_", "Factor", BindingType.Int32, "int", "protected_counter")
        {
            AccessModifier = BindingAccessModifier.Protected
        });
        compilation.Members.Add(record);

        BindingCompilationPolymorphismPass.Apply(compilation);

        var files = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files;
        var managedOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cs", StringComparison.Ordinal)).Content;
        var nativeOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cpp", StringComparison.Ordinal)).Content;

        Assert.Contains("protected ProtectedCounter(int", managedOutput, StringComparison.Ordinal);
        Assert.Contains("protected int Increment(int amount)", managedOutput, StringComparison.Ordinal);
        Assert.Contains("protected virtual int Scale(int value)", managedOutput, StringComparison.Ordinal);
        Assert.Contains("protected int Factor", managedOutput, StringComparison.Ordinal);
        Assert.Contains("__call_Increment", nativeOutput, StringComparison.Ordinal);
        Assert.Contains("__call_Scale", nativeOutput, StringComparison.Ordinal);
        Assert.Contains("return MarshallerInvoker::invoke_instance<FT, std::tuple<int>>(static_cast<FT>(&__protected_counter_director::increment), this, amount);", nativeOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("auto&& __arg0 = __arg0_marshaller.ConvertToCpp(amount);", nativeOutput, StringComparison.Ordinal);
        Assert.Contains("__get_Factor", nativeOutput, StringComparison.Ordinal);
        Assert.Contains("void __set_Factor(field_t<int> value)", nativeOutput, StringComparison.Ordinal);
        Assert.Contains("return MarshallerInvoker::get_field<int>(&__protected_counter_director::factor_, this);", nativeOutput, StringComparison.Ordinal);
        Assert.Contains("MarshallerInvoker::set_field<int>(&__protected_counter_director::factor_, this, value);", nativeOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("void __set_Factor(arg_t<int> value)", nativeOutput, StringComparison.Ordinal);
        Assert.Contains("protected member access requires a director-backed instance", nativeOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Director_records_skip_explicitly_ignored_protected_field_helpers()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "SharpCpp.Sample",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "Records/Protected.hpp" });
        var context = new RecordBinding("record:context", "context", "Context");
        var record = new RecordBinding("record:protected_counter", "protected_counter", "ProtectedCounter")
        {
            IsDirector = true
        };
        var getValue = new FunctionBinding("method:protected_counter:get_value", "get_value", "GetValue", BindingType.Int32, "entry_get")
        {
            IsStatic = false,
            IsVirtual = true,
            IsConstMethod = true,
            OwningRecordCppTypeName = "protected_counter"
        };
        var contextField = new FieldBinding(
            "field:protected_counter:context_",
            "context_",
            "Context",
            BindingType.CreateHandle("context*", "Context", context.SymbolId),
            "context*",
            "protected_counter")
        {
            AccessModifier = BindingAccessModifier.Protected
        };
        contextField.SetIgnoreReason("field explicitly ignored by pipeline");

        record.Members.Add(getValue);
        record.Members.Add(contextField);
        compilation.Members.Add(context);
        compilation.Members.Add(record);

        BindingCompilationPolymorphismPass.Apply(compilation);

        var files = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files;
        var nativeOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cpp", StringComparison.Ordinal)).Content;

        Assert.DoesNotContain("__get_Context", nativeOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("__set_Context", nativeOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Director_records_render_pointer_handle_protected_field_helpers()
    {
        var compilation = CreateProtectedHandleFieldCompilation(
            BindingType.CreateHandle("payload*", "Payload", "record:payload"),
            "payload*");

        var files = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files;
        var managedOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cs", StringComparison.Ordinal)).Content;
        var nativeOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cpp", StringComparison.Ordinal)).Content;

        Assert.Contains("protected global::SharpCpp.Sample.Payload Payload", managedOutput, StringComparison.Ordinal);
        Assert.Contains("internal static partial global::SharpCpp.Sample.Payload Get_Payload(ProtectedCounter self);", managedOutput, StringComparison.Ordinal);
        Assert.Contains("internal static partial void Set_Payload(ProtectedCounter self, [MarshalUsing(typeof(global::SharpCpp.Sample.__PayloadMarshaller<global::SharpCpp.Support.BorrowedNativeHandle>))] global::SharpCpp.Sample.Payload? value);", managedOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("Set_Payload(ProtectedCounter self, [MarshalUsing(typeof(global::SharpCpp.Sample.__PayloadMarshaller<global::SharpCpp.Support.BorrowedNativeHandle>))] in global::SharpCpp.Sample.Payload? value);", managedOutput, StringComparison.Ordinal);
        Assert.Contains("__PInvoke.Set_Payload(this, value);", managedOutput, StringComparison.Ordinal);
        Assert.Contains("ret_t<payload*> __get_Payload()", nativeOutput, StringComparison.Ordinal);
        Assert.Contains("return MarshallerInvoker::get_field<payload*>(&__protected_counter_director::payload_, this);", nativeOutput, StringComparison.Ordinal);
        Assert.Contains("void __set_Payload(arg_t<payload*> value)", nativeOutput, StringComparison.Ordinal);
        Assert.Contains("MarshallerInvoker::set_field<payload*>(&__protected_counter_director::payload_, this, value);", nativeOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Director_records_render_projected_handle_protected_field_with_marshaller_conversion()
    {
        var compilation = CreateProtectedHandleFieldCompilation(
            BindingType.CreateHandle("SharedPtr<payload>", "Payload", "record:payload", marshallerTypeName: "Marshaller<SharedPtr<payload>>"),
            "SharedPtr<payload>");

        var nativeOutput = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files
            .Single(static file => file.RelativePath.EndsWith(".Bindings.g.cpp", StringComparison.Ordinal)).Content;

        Assert.Contains("ret_t<SharedPtr<payload>> __get_Payload()", nativeOutput, StringComparison.Ordinal);
        Assert.Contains("return MarshallerInvoker::get_field<SharedPtr<payload>>(&__protected_counter_director::payload_, this);", nativeOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("return this->payload_;", nativeOutput, StringComparison.Ordinal);
        Assert.Contains("SHARPCPP_EXPORT ret_t<void> sharpcpp_e2e_protected_counter_payload__set(arg_t<protected_counter*> self, arg_t<SharedPtr<payload>> value)", nativeOutput, StringComparison.Ordinal);
        Assert.Contains("void __set_Payload(arg_t<SharedPtr<payload>> value)", nativeOutput, StringComparison.Ordinal);
        Assert.Contains("MarshallerInvoker::set_field<SharedPtr<payload>>(&__protected_counter_director::payload_, this, value);", nativeOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("void __set_Payload(field_t<SharedPtr<payload>> value)", nativeOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("this->payload_ = *value;", nativeOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Director_records_render_borrowed_container_protected_field_with_address_return_and_null_clear()
    {
        var compilation = CreateProtectedHandleFieldCompilation(
            BindingType.CreateHandle("std::unordered_map<int, int>*", "VariantMap", "record:payload", marshallerTypeName: "Marshaller<std::unordered_map<int, int>>"),
            "std::unordered_map<int, int>",
            payloadHasMarshallerExposedMethod: true);

        var nativeOutput = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files
            .Single(static file => file.RelativePath.EndsWith(".Bindings.g.cpp", StringComparison.Ordinal)).Content;

        Assert.Contains("ret_t<std::unordered_map<int, int>> __get_Payload()", nativeOutput, StringComparison.Ordinal);
        Assert.Contains("return MarshallerInvoker::get_field<std::unordered_map<int, int>>(&__protected_counter_director::payload_, this, std::true_type{});", nativeOutput, StringComparison.Ordinal);
        Assert.Contains("SHARPCPP_EXPORT ret_t<void> sharpcpp_e2e_protected_counter_payload__set(arg_t<protected_counter*> self, field_t<std::unordered_map<int, int>> value)", nativeOutput, StringComparison.Ordinal);
        Assert.Contains("void __set_Payload(field_t<std::unordered_map<int, int>> value)", nativeOutput, StringComparison.Ordinal);
        Assert.Contains("MarshallerInvoker::set_field<std::unordered_map<int, int>>(&__protected_counter_director::payload_, this, value, std::true_type{});", nativeOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Nested_borrowed_container_fields_preserve_null_clear_and_move_assignment()
    {
        var compilation = CreateProtectedHandleFieldCompilation(
            BindingType.CreateHandle("std::unordered_map<int, int>*", "VariantMap", "record:payload", marshallerTypeName: "Marshaller<std::unordered_map<int, int>>"),
            "std::unordered_map<int, int>",
            payloadHasMarshallerExposedMethod: true);
        var record = compilation.Records.Single(static record => record.CppQualifiedName == "protected_counter");
        var field = record.Members.OfType<FieldBinding>().Single();
        field.SetMetadata(MetadataKeys.NativeFieldAccessExpression, "nested.payload_");

        var nativeOutput = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files
            .Single(static file => file.RelativePath.EndsWith(".Bindings.g.cpp", StringComparison.Ordinal)).Content;

        Assert.Contains("if (value == nullptr)", nativeOutput, StringComparison.Ordinal);
        Assert.Contains("this->nested.payload_.clear();", nativeOutput, StringComparison.Ordinal);
        Assert.Contains("this->nested.payload_ = std::move(*value);", nativeOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Protected_base_field_access_accepts_derived_director_instances()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "SharpCpp.Sample",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "Records/Application.hpp" });
        var contextRecord = new RecordBinding("record:context", "context", "Context");
        var objectRecord = new RecordBinding("record:object", "object", "Object");
        objectRecord.Members.Add(new FieldBinding(
            "field:object:context_",
            "context_",
            "Context",
            BindingType.CreateHandle("context*", "Context", contextRecord.SymbolId),
            "context*",
            objectRecord.CppQualifiedName)
        {
            AccessModifier = BindingAccessModifier.Protected
        });

        var applicationRecord = new RecordBinding("record:application", "application", "Application")
        {
            ManagedBaseTypeName = objectRecord.ManagedName,
            IsDirector = true
        };
        applicationRecord.Bases.Add(new RecordBaseBinding(objectRecord.SymbolId, objectRecord.NativeName, objectRecord.ManagedName, objectRecord.CppQualifiedName, isVirtual: false));
        applicationRecord.Members.Add(new FunctionBinding("method:application:start", "start", "Start", BindingType.Void, "entry_start")
        {
            IsStatic = false,
            IsVirtual = true,
            OwningRecordCppTypeName = applicationRecord.CppQualifiedName
        });

        compilation.Members.Add(contextRecord);
        compilation.Members.Add(objectRecord);
        compilation.Members.Add(applicationRecord);

        BindingCompilationPolymorphismPass.Apply(compilation);

        var files = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files;
        var managedOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cs", StringComparison.Ordinal)).Content;
        var nativeOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cpp", StringComparison.Ordinal)).Content;

        Assert.Contains("protected new global::SharpCpp.Sample.Context Context", managedOutput, StringComparison.Ordinal);
        Assert.Contains("EntryPoint = \"sharpcpp_e2e_application_context__get\"", managedOutput, StringComparison.Ordinal);
        Assert.Contains("ret_t<context*> __get_Context()", nativeOutput, StringComparison.Ordinal);
        Assert.Contains("SHARPCPP_EXPORT ret_t<context*> sharpcpp_e2e_application_context__get(arg_t<application*> self)", nativeOutput, StringComparison.Ordinal);
        Assert.Contains("if (auto* director_application = dynamic_cast<__application_director*>(self))", nativeOutput, StringComparison.Ordinal);
        Assert.Contains("return director_application->__get_Context();", nativeOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Director_records_do_not_borrow_pointer_fields_even_for_marshaller_backed_wrappers()
    {
        var compilation = CreateProtectedHandleFieldCompilation(
            BindingType.CreateHandle("std::unordered_map<int, int>*", "VariantMap", "record:payload", marshallerTypeName: "Marshaller<std::unordered_map<int, int>>"),
            "std::unordered_map<int, int>*",
            fieldIsPointer: true,
            payloadHasMarshallerExposedMethod: true);

        var nativeOutput = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files
            .Single(static file => file.RelativePath.EndsWith(".Bindings.g.cpp", StringComparison.Ordinal)).Content;

        Assert.Contains("ret_t<std::unordered_map<int, int>*> __get_Payload()", nativeOutput, StringComparison.Ordinal);
        Assert.Contains("return MarshallerInvoker::get_field<std::unordered_map<int, int>*>(&__protected_counter_director::payload_, this);", nativeOutput, StringComparison.Ordinal);
        Assert.Contains("void __set_Payload(arg_t<std::unordered_map<int, int>*> value)", nativeOutput, StringComparison.Ordinal);
        Assert.Contains("MarshallerInvoker::set_field<std::unordered_map<int, int>*>(&__protected_counter_director::payload_, this, value);", nativeOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("return &this->payload_;", nativeOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("field_t<std::unordered_map<int, int>*>", nativeOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Director_records_do_not_borrow_protected_field_storage_from_cpp_type_name_substrings()
    {
        var compilation = CreateProtectedHandleFieldCompilation(
            BindingType.CreateHandle("custom_map_like<int>*", "Payload", "record:payload", marshallerTypeName: "Marshaller<custom_map_like<int>>"),
            "custom_map_like<int>");

        var nativeOutput = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files
            .Single(static file => file.RelativePath.EndsWith(".Bindings.g.cpp", StringComparison.Ordinal)).Content;

        Assert.Contains("ret_t<custom_map_like<int>> __get_Payload()", nativeOutput, StringComparison.Ordinal);
        Assert.Contains("return MarshallerInvoker::get_field<custom_map_like<int>>(&__protected_counter_director::payload_, this);", nativeOutput, StringComparison.Ordinal);
        Assert.Contains("SHARPCPP_EXPORT ret_t<void> sharpcpp_e2e_protected_counter_payload__set(arg_t<protected_counter*> self, arg_t<custom_map_like<int>> value)", nativeOutput, StringComparison.Ordinal);
        Assert.Contains("void __set_Payload(arg_t<custom_map_like<int>> value)", nativeOutput, StringComparison.Ordinal);
        Assert.Contains("MarshallerInvoker::set_field<custom_map_like<int>>(&__protected_counter_director::payload_, this, value);", nativeOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("return &this->payload_;", nativeOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("field_t<custom_map_like<int>>", nativeOutput, StringComparison.Ordinal);
    }

    private static BindingCompilation CreateProtectedHandleFieldCompilation(
        BindingType fieldType,
        string fieldCppTypeName,
        bool payloadHasMarshallerExposedMethod = false,
        bool fieldIsPointer = false)
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "SharpCpp.Sample",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "Records/Protected.hpp" });
        var payloadRecord = new RecordBinding("record:payload", "payload", "Payload");
        if (payloadHasMarshallerExposedMethod)
        {
            var getCountCore = new FunctionBinding("method:payload:get_count_core", "GetCountCore", "GetCountCore", BindingType.UInt64, "entry_get_count_core")
            {
                OwningRecordCppTypeName = "payload"
            };
            getCountCore.SetMetadata(MetadataKeys.MarshallerExposedMethod, true);
            payloadRecord.Members.Add(getCountCore);
        }
        var record = new RecordBinding("record:protected_counter", "protected_counter", "ProtectedCounter")
        {
            IsDirector = true
        };
        var virtualMethod = new FunctionBinding("method:protected_counter:get_value", "get_value", "GetValue", BindingType.Int32, "entry_get")
        {
            IsStatic = false,
            IsVirtual = true,
            IsConstMethod = true,
            OwningRecordCppTypeName = "protected_counter"
        };
        record.Members.Add(virtualMethod);
        record.Members.Add(new FieldBinding("field:protected_counter:payload_", "payload_", "Payload", fieldType, fieldCppTypeName, "protected_counter")
        {
            AccessModifier = BindingAccessModifier.Protected,
            IsPointer = fieldIsPointer
        });
        compilation.Members.Add(payloadRecord);
        compilation.Members.Add(record);

        BindingCompilationPolymorphismPass.Apply(compilation);
        return compilation;
    }

    [Fact]
    public void Secondary_base_methods_are_projected_with_adjusted_native_dispatch()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "SharpCpp.Sample",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "Records/MultipleInheritance.hpp" });
        var left = new RecordBinding("record:mi_left", "mi_left", "MiLeft");
        var leftValue = new FunctionBinding("method:mi_left:left_value", "left_value", "LeftValue", BindingType.Int32, "entry_left")
        {
            IsStatic = false,
            IsVirtual = true,
            IsConstMethod = true,
            OwningRecordCppTypeName = "mi_left"
        };
        left.Members.Add(leftValue);

        var right = new RecordBinding("record:mi_right", "mi_right", "MiRight");
        var rightValue = new FunctionBinding("method:mi_right:right_value", "right_value", "RightValue", BindingType.Int32, "entry_right")
        {
            IsStatic = false,
            IsVirtual = true,
            IsConstMethod = true,
            OwningRecordCppTypeName = "mi_right"
        };
        var rightOnly = new FunctionBinding("method:mi_right:right_only", "right_only", "RightOnly", BindingType.Int32, "entry_right_only")
        {
            IsStatic = false,
            IsConstMethod = true,
            OwningRecordCppTypeName = "mi_right"
        };
        right.Members.Add(rightValue);
        right.Members.Add(rightOnly);

        var counter = new RecordBinding("record:mi_counter", "mi_counter", "MiCounter")
        {
            ManagedBaseTypeName = "MiLeft",
            IsDirector = true
        };
        counter.Bases.Add(new RecordBaseBinding(left.SymbolId, left.NativeName, left.ManagedName, left.CppQualifiedName, isVirtual: false));
        counter.Bases.Add(new RecordBaseBinding(right.SymbolId, right.NativeName, right.ManagedName, right.CppQualifiedName, isVirtual: false));
        var ctor = new FunctionBinding("ctor:mi_counter", "mi_counter", "MiCounter", counter.HandleType, "entry_ctor")
        {
            IsConstructor = true,
            OwningRecordCppTypeName = "mi_counter"
        };
        counter.Members.Add(ctor);

        compilation.Members.Add(left);
        compilation.Members.Add(right);
        compilation.Members.Add(counter);

        BindingCompilationInheritancePass.Apply(compilation);
        BindingCompilationPolymorphismPass.Apply(compilation);

        var files = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files;
        var managedOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cs", StringComparison.Ordinal)).Content;
        var nativeOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cpp", StringComparison.Ordinal)).Content;

        Assert.Contains("public virtual int RightValue()", managedOutput, StringComparison.Ordinal);
        Assert.Contains("public int RightOnly()", managedOutput, StringComparison.Ordinal);
        Assert.Contains("dynamic_cast<mi_right*>(instance)", nativeOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Generic_records_render_as_managed_generic_classes_with_concrete_instantiation_dispatch()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "SharpCpp.Sample",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "Records/Generic.hpp" });
        var record = new RecordBinding("record:value_box", "value_box", "BoxOf", "value_box")
        {
            ManagedTypeFormat = "BoxOf<{0}>"
        };
        record.GenericParameters.Add(new BindingGenericParameter("T", 0));
        var intInstantiation = new RecordGenericInstantiationBinding(new[] { BindingType.Int32 });
        var floatInstantiation = new RecordGenericInstantiationBinding(new[] { BindingType.Single });
        record.SupportedGenericInstantiations.Add(intInstantiation);
        record.SupportedGenericInstantiations.Add(floatInstantiation);

        var ctor = new FunctionBinding("ctor:value_box:T", "value_box", "BoxOf", record.HandleType, "entry_ctor")
        {
            IsConstructor = true,
            OwningRecordCppTypeName = "value_box"
        };
        ctor.Parameters.Add(new ParameterBinding("parameter:value", "value", "value", BindingType.CreateGenericParameter("T"), cppTypeName: "T"));

        var getValue = new FunctionBinding("method:value_box:get_value", "get_value", "GetValue", BindingType.CreateGenericParameter("T"), "entry_get")
        {
            IsStatic = false,
            OwningRecordCppTypeName = "value_box",
            IsConstMethod = true
        };

        var setValue = new FunctionBinding("method:value_box:set_value", "set_value", "SetValue", BindingType.Void, "entry_set")
        {
            IsStatic = false,
            OwningRecordCppTypeName = "value_box"
        };
        setValue.Parameters.Add(new ParameterBinding("parameter:value", "value", "value", BindingType.CreateGenericParameter("T"), cppTypeName: "T"));

        var create = new FunctionBinding("static:value_box:create", "create", "Create", BindingType.CreateHandle("value_box<T>*", "BoxOf", record.SymbolId, genericArguments: new[] { BindingType.CreateGenericParameter("T") }), "entry_create")
        {
            IsStatic = true,
            OwningRecordCppTypeName = "value_box"
        };
        create.Parameters.Add(new ParameterBinding("parameter:value", "value", "value", BindingType.CreateGenericParameter("T"), cppTypeName: "T"));

        record.Members.Add(ctor);
        record.Members.Add(getValue);
        record.Members.Add(setValue);
        record.Members.Add(create);
        compilation.Members.Add(record);

        var files = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files;
        var managedOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cs", StringComparison.Ordinal)).Content;
        var nativeOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cpp", StringComparison.Ordinal)).Content;

        Assert.Contains("public partial class BoxOf<T>", managedOutput, StringComparison.Ordinal);
        Assert.Contains("typeof(T) == typeof(int)", managedOutput, StringComparison.Ordinal);
        Assert.Contains("typeof(T) == typeof(float)", managedOutput, StringComparison.Ordinal);
        Assert.Contains("public T GetValue()", managedOutput, StringComparison.Ordinal);
        Assert.Contains("internal static partial int BoxOf_GetValue", managedOutput, StringComparison.Ordinal);
        Assert.Contains("(nint self);", managedOutput, StringComparison.Ordinal);
        Assert.Contains("__PInvoke.BoxOf_GetValue", managedOutput, StringComparison.Ordinal);
        Assert.Contains("global::SharpCpp.Support.NativeObject.GetHandle(this)", managedOutput, StringComparison.Ordinal);
        Assert.Contains("(global::SharpCpp.Sample.BoxOf<T>)(object)__result!;", managedOutput, StringComparison.Ordinal);
        AssertAllLibraryImportsAreAggressivelyInlined(managedOutput);
        Assert.Contains($"entry_get{intInstantiation.Suffix}", nativeOutput, StringComparison.Ordinal);
        Assert.Contains($"entry_get{floatInstantiation.Suffix}", nativeOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Generic_record_dispatch_deduplicates_managed_signatures_and_prefers_native_value_arguments()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "SharpCpp.Sample",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "Records/UnorderedMap.hpp" });
        var stringRecord = new RecordBinding("record:string", "basic_string", "String", "eastl::basic_string<char>")
        {
            ManagedNamespace = "Ea",
            ManagedTypeFormat = "global::Ea.String"
        };
        var urhoVariantRecord = new RecordBinding("record:urho_variant", "Variant", "Variant", "Urho3D::Variant")
        {
            ManagedNamespace = "Urho3D.Core",
            ManagedTypeFormat = "global::Urho3D.Core.Variant"
        };
        var rmlVariantRecord = new RecordBinding("record:rml_variant", "Variant", "Variant", "Rml::Variant")
        {
            ManagedNamespace = "Rml",
            ManagedTypeFormat = "global::Urho3D.Core.Variant"
        };
        var mapRecord = new RecordBinding("record:hash_map", "hash_map", "UnorderedMap", "eastl::hash_map")
        {
            ManagedNamespace = "Ea",
            ManagedTypeFormat = "global::Ea.UnorderedMap<{0}, {1}>"
        };
        mapRecord.GenericParameters.Add(new BindingGenericParameter("TKey", 0));
        mapRecord.GenericParameters.Add(new BindingGenericParameter("TValue", 1));

        var stringPointerType = BindingType.CreateHandle("eastl::basic_string<char>*", "String", stringRecord.SymbolId)
            .WithManagedTypeFormatOverride("global::Ea.String");
        var stringValueType = BindingType.CreateHandle("eastl::basic_string<char>", "String", stringRecord.SymbolId, cppIsRecordValue: true)
            .WithManagedTypeFormatOverride("global::Ea.String");
        var urhoVariantValueType = BindingType.CreateHandle("Urho3D::Variant", "Variant", urhoVariantRecord.SymbolId, cppIsRecordValue: true)
            .WithManagedTypeFormatOverride("global::Urho3D.Core.Variant");
        var rmlVariantValueType = BindingType.CreateHandle("Rml::Variant", "Variant", rmlVariantRecord.SymbolId, cppIsRecordValue: true)
            .WithManagedTypeFormatOverride("global::Urho3D.Core.Variant");
        var pointerKeyInstantiation = new RecordGenericInstantiationBinding([stringPointerType, urhoVariantValueType]);
        var rmlVariantInstantiation = new RecordGenericInstantiationBinding([stringValueType, rmlVariantValueType]);
        var valueInstantiation = new RecordGenericInstantiationBinding([stringValueType, urhoVariantValueType]);
        mapRecord.SupportedGenericInstantiations.Add(pointerKeyInstantiation);
        mapRecord.SupportedGenericInstantiations.Add(rmlVariantInstantiation);
        mapRecord.SupportedGenericInstantiations.Add(valueInstantiation);

        var setValue = new FunctionBinding("method:hash_map:set_value", "SetValueCore", "SetValueCore", BindingType.Void, "entry_set")
        {
            IsStatic = false,
            OwningRecordCppTypeName = "eastl::hash_map"
        };
        setValue.Parameters.Add(new ParameterBinding("parameter:key", "key", "key", BindingType.CreateGenericParameter("TKey"), cppTypeName: "TKey"));
        setValue.Parameters.Add(new ParameterBinding("parameter:value", "value", "value", BindingType.CreateGenericParameter("TValue"), cppTypeName: "TValue"));
        mapRecord.Members.Add(setValue);

        var getValue = new FunctionBinding("method:hash_map:get_value", "GetValueCore", "GetValueCore", BindingType.CreateGenericParameter("TValue"), "entry_get")
        {
            IsStatic = false,
            OwningRecordCppTypeName = "eastl::hash_map"
        };
        getValue.Parameters.Add(new ParameterBinding("parameter:key", "key", "key", BindingType.CreateGenericParameter("TKey"), cppTypeName: "TKey"));
        mapRecord.Members.Add(getValue);

        compilation.Members.Add(stringRecord);
        compilation.Members.Add(urhoVariantRecord);
        compilation.Members.Add(rmlVariantRecord);
        compilation.Members.Add(mapRecord);

        var files = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files;
        var managedOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cs", StringComparison.Ordinal)).Content;

        Assert.Equal(1, CountOccurrences(managedOutput, "EntryPoint = \"entry_set"));
        Assert.Contains("internal static partial void UnorderedMap_SetValueCore", managedOutput, StringComparison.Ordinal);
        Assert.Contains("(nint self, [MarshalUsing", managedOutput, StringComparison.Ordinal);
        Assert.Contains("__PInvoke.UnorderedMap_SetValueCore", managedOutput, StringComparison.Ordinal);
        Assert.Contains("global::SharpCpp.Support.NativeObject.GetHandle(this)", managedOutput, StringComparison.Ordinal);
        Assert.Contains($"EntryPoint = \"entry_set{valueInstantiation.Suffix}\"", managedOutput, StringComparison.Ordinal);
        Assert.DoesNotContain($"EntryPoint = \"entry_set{pointerKeyInstantiation.Suffix}\"", managedOutput, StringComparison.Ordinal);
        Assert.DoesNotContain($"EntryPoint = \"entry_set{rmlVariantInstantiation.Suffix}\"", managedOutput, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(managedOutput, "EntryPoint = \"entry_get"));
        Assert.Contains("[return: MarshalUsing(typeof(global::Urho3D.Core.__VariantMarshaller<global::SharpCpp.Support.OwnedNativeHandle>))]", managedOutput, StringComparison.Ordinal);
        Assert.Contains("internal static partial global::Urho3D.Core.Variant UnorderedMap_GetValueCore", managedOutput, StringComparison.Ordinal);
        Assert.Contains($"EntryPoint = \"entry_get{valueInstantiation.Suffix}\"", managedOutput, StringComparison.Ordinal);
        Assert.DoesNotContain($"EntryPoint = \"entry_get{pointerKeyInstantiation.Suffix}\"", managedOutput, StringComparison.Ordinal);
        Assert.DoesNotContain($"EntryPoint = \"entry_get{rmlVariantInstantiation.Suffix}\"", managedOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Managed_output_renders_comment_stub_for_non_silent_ignored_record()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "SharpCpp.Sample",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "Ignore/IgnoreTests.hpp" });
        var record = new RecordBinding("record:skipped", "skipped", "Skipped");
        record.SetMetadata(MetadataKeys.IgnoreReason, "not yet supported");
        compilation.Members.Add(record);

        var files = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files;
        var managedOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cs", StringComparison.Ordinal)).Content;

        var lines = managedOutput.Replace("\r\n", "\n").Split('\n');

        Assert.Contains("    // skipped; // excluded: not yet supported", lines);
        Assert.DoesNotContain("public partial class Skipped", managedOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Managed_output_renders_comment_stub_for_non_silent_ignored_free_function()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "SharpCpp.Sample",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "Ignore/IgnoreTests.hpp" });
        var function = new FunctionBinding("function:skipped_fn", "skipped_fn", "SkippedFn", BindingType.Int32, "entry_skipped");
        function.SetMetadata(MetadataKeys.IgnoreReason, "manual stub");
        compilation.Members.Add(function);

        var files = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files;
        var managedOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cs", StringComparison.Ordinal)).Content;

        var lines = managedOutput.Replace("\r\n", "\n").Split('\n');

        Assert.Contains("        // static int skipped_fn(); // excluded: manual stub", lines);
        Assert.DoesNotContain("\n    public static int SkippedFn(", managedOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Managed_output_renders_comment_stub_for_non_silent_ignored_enum()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "SharpCpp.Sample",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "Ignore/IgnoreTests.hpp" });
        var @enum = new EnumBinding("enum:skipped", "skipped", "Skipped", "unsigned int", underlyingPrimitiveKind: BindingPrimitiveKind.UInt32);
        @enum.SetMetadata(MetadataKeys.IgnoreReason, "marked [[sharpcpp::ignore]]");
        compilation.Members.Add(@enum);

        var files = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files;
        var managedOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cs", StringComparison.Ordinal)).Content;

        var lines = managedOutput.Replace("\r\n", "\n").Split('\n');

        Assert.Contains("    // enum skipped; // excluded: marked [[sharpcpp::ignore]]", lines);
    }

    [Fact]
    public void Managed_output_renders_comment_stub_for_non_silent_ignored_members_within_record()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "SharpCpp.Sample",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "Ignore/IgnoreTests.hpp" });
        var record = new RecordBinding("record:box", "box", "Box");
        var ignoredMethod = new FunctionBinding("method:box:skip", "skip", "Skip", BindingType.Int32, "entry_skip") { OwningRecordCppTypeName = "box", IsStatic = false };
        ignoredMethod.SetMetadata(MetadataKeys.IgnoreReason, "internal only");
        record.Members.Add(ignoredMethod);
        compilation.Members.Add(record);

        var files = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files;
        var managedOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cs", StringComparison.Ordinal)).Content;

        Assert.Contains("// excluded: internal only", managedOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Managed_output_renders_comment_stub_for_non_silent_ignored_field()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "SharpCpp.Sample",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "Ignore/IgnoreTests.hpp" });
        var record = new RecordBinding("record:box", "box", "Box");
        var ignoredField = new FieldBinding("field:box:skip", "skip", "Skip", BindingType.Int32, "int", "box");
        ignoredField.SetMetadata(MetadataKeys.IgnoreReason, "marked [[sharpcpp::ignore]]");
        record.Members.Add(ignoredField);
        compilation.Members.Add(record);

        var files = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files;
        var managedOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cs", StringComparison.Ordinal)).Content;

        var lines = managedOutput.Replace("\r\n", "\n").Split('\n');

        Assert.Contains("        // int skip; // excluded: marked [[sharpcpp::ignore]]", lines);
    }

    [Fact]
    public void Managed_output_renders_documentation_comment_for_record()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample", "SharpCpp.Sample", "SampleBindings",
            "SharpCpp.Sample.Native", new[] { "Doc/Documentation.hpp" });
        var record = new RecordBinding("record:doc", "doc", "Doc");
        record.SetMetadata(MetadataKeys.DocumentationComment, "Represents a documented record.");
        compilation.Members.Add(record);

        var files = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files;
        var managedOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cs", StringComparison.Ordinal)).Content;

        Assert.Contains("/// <summary>Represents a documented record.</summary>", managedOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Managed_output_escapes_xml_in_documentation_comments()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample", "SharpCpp.Sample", "SampleBindings",
            "SharpCpp.Sample.Native", new[] { "Doc/Documentation.hpp" });
        var function = new FunctionBinding("function:escape", "escape", "Escape", BindingType.Int32, "entry_escape");
        function.SetMetadata(MetadataKeys.DocumentationComment, "Handles <tag> escaping & special chars.");
        compilation.Members.Add(function);

        var files = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files;
        var managedOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cs", StringComparison.Ordinal)).Content;

        Assert.Contains("/// <summary>Handles &lt;tag&gt; escaping &amp; special chars.</summary>", managedOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Managed_output_renders_record_in_assigned_namespace()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample", "SharpCpp.Sample", "SampleBindings",
            "SharpCpp.Sample.Native", new[] { "Namespace/Namespace.hpp" });
        var record = new RecordBinding("record:ns_type", "ns_type", "NsType", "outer::inner::ns_type");
        record.ManagedNamespace = "Outer.Inner";
        compilation.Members.Add(record);

        var files = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files;
        var managedOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cs", StringComparison.Ordinal)).Content;

        Assert.Contains("namespace Outer.Inner", managedOutput, StringComparison.Ordinal);
        Assert.Contains("public partial class NsType", managedOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Managed_output_renders_global_scope_record_in_default_namespace()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample", "SharpCpp.Sample", "SampleBindings",
            "SharpCpp.Sample.Native", new[] { "Records/Field.hpp" });
        var record = new RecordBinding("record:global_type", "global_type", "GlobalType");
        record.ManagedNamespace = "SharpCpp.Sample";
        compilation.Members.Add(record);

        var files = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files;
        var managedOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cs", StringComparison.Ordinal)).Content;

        Assert.Contains("namespace SharpCpp.Sample", managedOutput, StringComparison.Ordinal);
        Assert.Contains("public partial class GlobalType", managedOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Managed_output_groups_multiple_records_by_namespace()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample", "SharpCpp.Sample", "SampleBindings",
            "SharpCpp.Sample.Native", new[] { "Namespace/Namespace.hpp" });
        var recordA = new RecordBinding("record:a", "a", "A");
        var recordB = new RecordBinding("record:b", "b", "B", "ns1::b");
        var recordC = new RecordBinding("record:c", "c", "C", "ns1::c");
        compilation.Members.Add(recordA);
        compilation.Members.Add(recordB);
        compilation.Members.Add(recordC);

        NormalizeManagedNamespacesPass.Apply(compilation);

        var files = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files;
        var managedOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cs", StringComparison.Ordinal)).Content;

        Assert.Contains("namespace Ns1", managedOutput, StringComparison.Ordinal);
        Assert.Contains("public partial class B", managedOutput, StringComparison.Ordinal);
        Assert.Contains("public partial class C", managedOutput, StringComparison.Ordinal);

        var defaultNsIndex = managedOutput.IndexOf("namespace SharpCpp.Sample", StringComparison.Ordinal);
        var ns1Index = managedOutput.IndexOf("namespace Ns1", StringComparison.Ordinal);
        Assert.True(ns1Index < defaultNsIndex, "Non-default namespaces should appear before the default namespace.");
    }

    [Fact]
    public void Managed_output_renders_enum_in_assigned_namespace()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample", "SharpCpp.Sample", "SampleBindings",
            "SharpCpp.Sample.Native", new[] { "Enum/Enum.hpp" });
        var @enum = new EnumBinding("enum:color", "color", "Color", "int", "palette::color");
        @enum.ManagedNamespace = "Palette";
        @enum.Values.Add(new EnumValueBinding("red", "Red", "0"));
        @enum.Values.Add(new EnumValueBinding("green", "Green", "1"));
        compilation.Members.Add(@enum);

        var files = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files;
        var managedOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cs", StringComparison.Ordinal)).Content;

        Assert.Contains("namespace Palette", managedOutput, StringComparison.Ordinal);
        Assert.Contains("public enum Color", managedOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Managed_output_uses_default_namespace_for_static_free_function_class()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample", "SharpCpp.Sample", "SampleBindings",
            "SharpCpp.Sample.Native", new[] { "Builtin/Builtin.hpp" });
        var function = new FunctionBinding("function:free_func", "free_func", "FreeFunc", BindingType.Void, "entry_free_func");
        compilation.Members.Add(function);

        var files = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files;
        var managedOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cs", StringComparison.Ordinal)).Content;

        Assert.Contains("namespace SharpCpp.Sample", managedOutput, StringComparison.Ordinal);
        Assert.Contains("public static partial class SampleBindings", managedOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void NormalizeManagedNamespacesPass_assigns_identity_namespace()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample", "SharpCpp.Sample", "SampleBindings",
            "SharpCpp.Sample.Native", new[] { "Namespace/Namespace.hpp" });
        var record = new RecordBinding("record:ns_rec", "ns_rec", "NsRec", "myapp::utils::ns_rec");
        compilation.Members.Add(record);

        NormalizeManagedNamespacesPass.Apply(compilation);

        Assert.Equal("Myapp.Utils", record.ManagedNamespace);
    }

    [Fact]
    public void NormalizeManagedNamespacesPass_uses_default_namespace_for_global_scope()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample", "SharpCpp.Sample", "SampleBindings",
            "SharpCpp.Sample.Native", new[] { "Records/Field.hpp" });
        var record = new RecordBinding("record:global_rec", "global_rec", "GlobalRec", "global_rec");
        compilation.Members.Add(record);

        NormalizeManagedNamespacesPass.Apply(compilation);

        Assert.Equal("SharpCpp.Sample", record.ManagedNamespace);
    }

    [Fact]
    public void NormalizeManagedNamespacesPass_respects_namespace_mappings()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample", "SharpCpp.Sample", "SampleBindings",
            "SharpCpp.Sample.Native", new[] { "Namespace/Namespace.hpp" })
        {
            NamespaceMappings = new Dictionary<string, string>
            {
                { "myapp::utils", "SharpCpp.Generated.Utils" }
            }
        };
        var record = new RecordBinding("record:ns_rec", "ns_rec", "NsRec", "myapp::utils::ns_rec");
        compilation.Members.Add(record);

        NormalizeManagedNamespacesPass.Apply(compilation);

        Assert.Equal("SharpCpp.Generated.Utils", record.ManagedNamespace);
    }

    [Fact]
    public void Render_uses_first_rbfx_folder_as_output_subdirectory()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Rbfx.Bindings",
            "Urho3D",
            "Bindings",
            "SharpCpp.Rbfx.Native",
            new[] { "Urho3D/Urho3DAll.h" });

        var graphicsModel = new RecordBinding("record:graphics_model", "graphics_model", "GraphicsModel")
        {
            HeaderIncludePath = "Graphics/Model.h"
        };
        var graphicsMaterial = new RecordBinding("record:graphics_material", "graphics_material", "GraphicsMaterial")
        {
            HeaderIncludePath = "Graphics/Material.h"
        };
        var deepNode = new RecordBinding("record:deep_node", "deep_node", "DeepNode")
        {
            HeaderIncludePath = "Foo/Bar/Baz.h"
        };
        var rmlDocument = new RecordBinding("record:rml_document", "rml_document", "ElementDocument")
        {
            HeaderIncludePath = "RmlUi/Core/ElementDocument.h"
        };
        var imguiIo = new RecordBinding("record:imgui_io", "imgui_io", "ImGuiIO")
        {
            HeaderIncludePath = "ImGui/imgui.h"
        };

        compilation.Members.Add(graphicsModel);
        compilation.Members.Add(graphicsMaterial);
        compilation.Members.Add(deepNode);
        compilation.Members.Add(rmlDocument);
        compilation.Members.Add(imguiIo);

        var files = BindingOutputRenderer.Render(compilation, "SharpCpp.Rbfx", DeriveRbfxGroupStem).Files;

        Assert.Contains(files, static file => file.RelativePath == "Graphics/SharpCpp.Rbfx.Model.g.cs");
        Assert.Contains(files, static file => file.RelativePath == "Graphics/SharpCpp.Rbfx.Model.g.cpp");
        Assert.Contains(files, static file => file.RelativePath == "Graphics/SharpCpp.Rbfx.Material.g.cs");
        Assert.Contains(files, static file => file.RelativePath == "Graphics/SharpCpp.Rbfx.Material.g.cpp");
        Assert.Contains(files, static file => file.RelativePath == "Foo/SharpCpp.Rbfx.Bar.Baz.g.cs");
        Assert.Contains(files, static file => file.RelativePath == "Foo/SharpCpp.Rbfx.Bar.Baz.g.cpp");
        Assert.Contains(files, static file => file.RelativePath == "ThirdParty/RmlUi/SharpCpp.Rbfx.Core.ElementDocument.g.cs");
        Assert.Contains(files, static file => file.RelativePath == "ThirdParty/RmlUi/SharpCpp.Rbfx.Core.ElementDocument.g.cpp");
        Assert.Contains(files, static file => file.RelativePath == "ThirdParty/ImGui/SharpCpp.Rbfx.imgui.g.cs");
        Assert.Contains(files, static file => file.RelativePath == "ThirdParty/ImGui/SharpCpp.Rbfx.imgui.g.cpp");
        Assert.Contains(files, static file => file.RelativePath == "SharpCpp.Rbfx.Bindings.Aliases.g.cs");

        var modelManagedOutput = files.Single(static file => file.RelativePath == "Graphics/SharpCpp.Rbfx.Model.g.cs").Content;
        var materialManagedOutput = files.Single(static file => file.RelativePath == "Graphics/SharpCpp.Rbfx.Material.g.cs").Content;
        var deepManagedOutput = files.Single(static file => file.RelativePath == "Foo/SharpCpp.Rbfx.Bar.Baz.g.cs").Content;

        Assert.Contains("public partial class GraphicsModel", modelManagedOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("public partial class GraphicsMaterial", modelManagedOutput, StringComparison.Ordinal);

        Assert.Contains("public partial class GraphicsMaterial", materialManagedOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("public partial class GraphicsModel", materialManagedOutput, StringComparison.Ordinal);

        Assert.Contains("public partial class DeepNode", deepManagedOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_routes_null_header_ignored_records_into_main_bindings_file()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Rbfx.Bindings",
            "Urho3D",
            "Bindings",
            "SharpCpp.Rbfx.Native",
            new[] { "Urho3D/Urho3DAll.h" });

        var foreignRecord = new RecordBinding(
            "record:std::basic_string<char>",
            "std::basic_string<char>",
            "BasicString",
            "std::basic_string<char>");
        foreignRecord.SetIgnoreReason("outside bindings api");
        compilation.Members.Add(foreignRecord);

        var files = BindingOutputRenderer.Render(compilation, "SharpCpp.Rbfx", DeriveRbfxGroupStem).Files;

        Assert.Contains(files, static file => file.RelativePath == "SharpCpp.Rbfx.Bindings.g.cs");
        Assert.DoesNotContain(files, static file => file.RelativePath.Contains("basic_string", StringComparison.Ordinal));

        var managedOutput = files.Single(static file => file.RelativePath == "SharpCpp.Rbfx.Bindings.g.cs").Content;
        Assert.Contains("// std::basic_string<char>; // excluded: outside bindings api", managedOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Grouped_render_keeps_files_for_silent_only_groups()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Rbfx.Bindings",
            "Urho3D",
            "Bindings",
            "SharpCpp.Rbfx.Native",
            new[] { "Urho3D/Urho3DAll.h" });
        var silentRecord = new RecordBinding("record:Urho3D::Detail::Hidden", "Hidden", "Hidden", "Urho3D::Detail::Hidden")
        {
            HeaderIncludePath = "Urho3D/RmlUI/Hidden.h"
        };
        silentRecord.SetSilentExclusionReason("internal detail");
        compilation.Members.Add(silentRecord);

        var files = BindingOutputRenderer.Render(compilation, "SharpCpp.Rbfx", DeriveRbfxGroupStem).Files;

        var managedOutput = Assert.Single(files, static file => file.RelativePath == "RmlUI/SharpCpp.Rbfx.Hidden.g.cs").Content;
        var nativeOutput = Assert.Single(files, static file => file.RelativePath == "RmlUI/SharpCpp.Rbfx.Hidden.g.cpp").Content;
        Assert.DoesNotContain("excluded: internal detail", managedOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("public partial class Hidden", managedOutput, StringComparison.Ordinal);
        Assert.Contains("// <auto-generated />", nativeOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Generated_binding_set_removes_stale_generated_files_when_rewriting_directory()
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), $"SharpCpp.Tests.Rendering.{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDirectory);

        try
        {
            Directory.CreateDirectory(Path.Combine(outputDirectory, "Graphics"));
            File.WriteAllText(Path.Combine(outputDirectory, "SharpCpp.Rbfx.Bindings.g.cpp"), "stale native");
            File.WriteAllText(Path.Combine(outputDirectory, "Graphics", "SharpCpp.Rbfx.Model.g.cs"), "stale managed");
            File.WriteAllText(Path.Combine(outputDirectory, "keep.txt"), "keep");

            var generatedSet = new GeneratedBindingSet(
            [
                new GeneratedBindingFile("Graphics/SharpCpp.Rbfx.Material.g.cs", "material managed"),
                new GeneratedBindingFile("Graphics/SharpCpp.Rbfx.Material.g.cpp", "material native"),
                new GeneratedBindingFile("SharpCpp.Rbfx.Bindings.Aliases.g.cs", "aliases")
            ]);

            generatedSet.WriteToDirectory(outputDirectory);

            Assert.False(File.Exists(Path.Combine(outputDirectory, "SharpCpp.Rbfx.Bindings.g.cpp")));
            Assert.False(File.Exists(Path.Combine(outputDirectory, "Graphics", "SharpCpp.Rbfx.Model.g.cs")));
            Assert.True(File.Exists(Path.Combine(outputDirectory, "Graphics", "SharpCpp.Rbfx.Material.g.cs")));
            Assert.True(File.Exists(Path.Combine(outputDirectory, "Graphics", "SharpCpp.Rbfx.Material.g.cpp")));
            Assert.True(File.Exists(Path.Combine(outputDirectory, "SharpCpp.Rbfx.Bindings.Aliases.g.cs")));
            Assert.True(File.Exists(Path.Combine(outputDirectory, "keep.txt")));
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }

    private static string DeriveRbfxGroupStem(string? headerPath)
    {
        if (string.IsNullOrWhiteSpace(headerPath))
        {
            return "SharpCpp.Rbfx.Bindings";
        }

        var normalizedHeaderPath = headerPath.Replace('\\', '/');
        if (normalizedHeaderPath.StartsWith("Urho3D/", StringComparison.Ordinal))
        {
            normalizedHeaderPath = normalizedHeaderPath["Urho3D/".Length..];
        }

        if (normalizedHeaderPath.StartsWith("RmlUi/", StringComparison.Ordinal) ||
            normalizedHeaderPath.StartsWith("ImGui/", StringComparison.Ordinal))
        {
            normalizedHeaderPath = $"ThirdParty/{normalizedHeaderPath}";
        }

        var pathWithoutExtension = Path.ChangeExtension(normalizedHeaderPath, null)!.Replace('\\', '/');
        var pathParts = pathWithoutExtension.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (pathParts.Length == 0)
        {
            return "SharpCpp.Rbfx.Bindings";
        }

        if (pathParts.Length >= 3 && string.Equals(pathParts[0], "ThirdParty", StringComparison.Ordinal))
        {
            return $"ThirdParty/{pathParts[1]}/SharpCpp.Rbfx.{string.Join('.', pathParts.Skip(2))}";
        }

        if (pathParts.Length == 1)
        {
            return $"SharpCpp.Rbfx.{pathParts[0]}";
        }

        return $"{pathParts[0]}/SharpCpp.Rbfx.{string.Join('.', pathParts.Skip(1))}";
    }

    private static void AssertAllLibraryImportsAreAggressivelyInlined(string managedOutput)
    {
        var lines = managedOutput.Replace("\r\n", "\n").Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            if (!lines[index].Contains("[LibraryImport(", StringComparison.Ordinal))
            {
                continue;
            }

            Assert.True(
                index + 1 < lines.Length && lines[index + 1].Trim() == "[MethodImpl(MethodImplOptions.AggressiveInlining)]",
                $"LibraryImport at generated line {index + 1} is missing [MethodImpl(MethodImplOptions.AggressiveInlining)].");
        }
    }

    [Fact]
    public void Managed_output_uses_StringBase_for_string_record_wrapper()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "SharpCpp.Sample",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "String/String.hpp" });
        var stringRecord = new RecordBinding("record:std::basic_string", "std::basic_string", "String")
        {
            ManagedBaseTypeName = "global::SharpCpp.Support.StringBase",
            ManagedTypeFormat = "String"
        };
        compilation.Members.Add(stringRecord);

        var files = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files;
        var managedOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cs", StringComparison.Ordinal)).Content;

        Assert.Contains("public partial class String : global::SharpCpp.Support.StringBase", managedOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(": global::SharpCpp.Support.NativeObject", managedOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Managed_output_renders_protected_core_methods_for_string_record()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "SharpCpp.Sample",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "String/String.hpp" });
        var stringRecord = new RecordBinding("record:std::basic_string", "std::basic_string", "String")
        {
            ManagedBaseTypeName = "global::SharpCpp.Support.StringBase",
            ManagedTypeFormat = "String",
            IsDirector = true
        };

        var getCountCore = new FunctionBinding("method:std::basic_string:size", "size", "GetCountCore",
            BindingType.UInt64, "sharpcpp_e2e_std_basic_string_size", "std::size_t", "std::basic_string::size")
        {
            IsStatic = false,
            AccessModifier = BindingAccessModifier.Protected,
            IsConstMethod = true,
            OwningRecordCppTypeName = "std::basic_string"
        };
        stringRecord.Members.Add(getCountCore);

        var getDataCore = new FunctionBinding("method:std::basic_string:data", "data", "GetDataCore",
            BindingType.IntPtr, "sharpcpp_e2e_std_basic_string_data", "const char*", "std::basic_string::data")
        {
            IsStatic = false,
            AccessModifier = BindingAccessModifier.Protected,
            IsConstMethod = true,
            IsOverride = true,
            OwningRecordCppTypeName = "std::basic_string"
        };
        stringRecord.Members.Add(getDataCore);

        var addCore = new FunctionBinding("method:std::basic_string:push_back", "push_back", "AddCore",
            BindingType.Void, "sharpcpp_e2e_std_basic_string_push_back", "void", "std::basic_string::push_back")
        {
            IsStatic = false,
            AccessModifier = BindingAccessModifier.Protected,
            OwningRecordCppTypeName = "std::basic_string"
        };
        addCore.Parameters.Add(new ParameterBinding(
            "parameter:std::basic_string:push_back:value", "value", "value",
            BindingType.UInt8, BindingParameterDirection.In, BindingParameterAbiKind.Value, "unsigned char"));
        stringRecord.Members.Add(addCore);

        compilation.Members.Add(stringRecord);

        var files = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files;
        var managedOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cs", StringComparison.Ordinal)).Content;
        var nativeOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cpp", StringComparison.Ordinal)).Content;

        Assert.Contains("GetCountCore()", managedOutput, StringComparison.Ordinal);
        Assert.Contains("GetDataCore()", managedOutput, StringComparison.Ordinal);
        Assert.Contains("AddCore", managedOutput, StringComparison.Ordinal);
        Assert.Contains("SHARPCPP_EXPORT ret_t<std::size_t> sharpcpp_e2e_std_basic_string_size(arg_t<std::basic_string*> self)", nativeOutput, StringComparison.Ordinal);
        Assert.Contains("SHARPCPP_EXPORT ret_t<const char*> sharpcpp_e2e_std_basic_string_data(arg_t<std::basic_string*> self)", nativeOutput, StringComparison.Ordinal);
        Assert.Contains("SHARPCPP_EXPORT ret_t<void> sharpcpp_e2e_std_basic_string_push_back(arg_t<std::basic_string*> self", nativeOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Generic_dynamic_array_records_use_pointer_abi_helpers()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "SharpCpp.Sample",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "Array/SharedArray.hpp" });
        var sharedArrayRecord = new RecordBinding("record:ea::shared_array", "shared_array", "SharedArray", "ea::shared_array")
        {
            ManagedNamespace = "Ea",
            ManagedTypeFormat = "SharedArray<{0}>",
            ManagedBaseTypeName = "global::SharpCpp.Support.SharedArrayBase<{0}>"
        };
        sharedArrayRecord.GenericParameters.Add(new BindingGenericParameter("T", 0));
        sharedArrayRecord.SetMetadata(MetadataKeys.DynamicArrayRecord, true);
        compilation.Members.Add(sharedArrayRecord);

        var sharedIntArrayType = BindingType.CreateHandle(
            "ea::shared_array<int>*",
            "SharedArray",
            sharedArrayRecord.SymbolId,
            genericArguments: [BindingType.Int32])
            .WithDynamicArray(BindingType.Int32);
        var factory = new FunctionBinding(
            "function:make_shared_array",
            "make_shared_array",
            "MakeSharedArray",
            sharedIntArrayType,
            "sharpcpp_sample_make_shared_array",
            "ea::shared_array<int>",
            "make_shared_array")
        {
            ReturnsOwnedHandle = true
        };
        compilation.Members.Add(factory);

        var files = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files;
        var managedOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cs", StringComparison.Ordinal)).Content;
        var nativeOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cpp", StringComparison.Ordinal)).Content;

        Assert.Contains("public partial class SharedArray<T> : global::SharpCpp.Support.SharedArrayBase<T>", managedOutput, StringComparison.Ordinal);
        Assert.Contains("internal static partial nint __Array_Create_int(nuint length);", managedOutput, StringComparison.Ordinal);
        Assert.Contains("public static nint ConvertToUnmanaged(global::Ea.SharedArray<T> managed)", managedOutput, StringComparison.Ordinal);
        Assert.Contains("public static global::Ea.SharedArray<T> ConvertToManaged(nint unmanaged)", managedOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("NativeArrayAbi", managedOutput, StringComparison.Ordinal);
        Assert.Contains("SHARPCPP_EXPORT ret_t<ea::shared_array<int>*> sharpcpp_e2e_ea_shared_array_dynamic_array_create_int(arg_t<std::size_t> length)", nativeOutput, StringComparison.Ordinal);
        Assert.Contains("return new ea::shared_array<int>(new int[length]{});", nativeOutput, StringComparison.Ordinal);
        Assert.Contains("(*self)[index]", nativeOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Ignored_fixed_array_surfaces_do_not_generate_helper_instantiations()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "SharpCpp.Sample",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "Array/NativeArray.hpp" });
        var arrayRecord = new RecordBinding("record:NativeArray", "NativeArray", "NativeArray", "NativeArray")
        {
            ManagedTypeFormat = "global::SharpCpp.NativeArray<{0}>"
        };
        arrayRecord.GenericParameters.Add(new BindingGenericParameter("T", 0));
        arrayRecord.GenericParameters.Add(new BindingGenericParameter("N", 1));
        arrayRecord.SetMetadata(MetadataKeys.NativeArrayRecord, true);
        compilation.Members.Add(arrayRecord);

        var vectorType = BindingType.CreateHandle("Vector2", "global::SharpCpp.Sample.Vector2");
        var fixedArrayType = BindingType.CreateHandle(
            "NativeArray<Vector2, 2>",
            "global::SharpCpp.NativeArray<{0}>",
            arrayRecord.SymbolId,
            genericArguments: [vectorType, BindingType.CreateConfigured("2", "2")])
            .WithFixedArray(vectorType, 2);
        var ignoredFunction = new FunctionBinding("function:ignored", "ignored", "Ignored", BindingType.Void);
        ignoredFunction.Parameters.Add(new ParameterBinding("parameter:ignored:values", "values", "values", fixedArrayType));
        ignoredFunction.SetSilentExclusionReason("unsupported fixed array surface");
        compilation.Members.Add(ignoredFunction);

        var files = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files;
        var nativeOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cpp", StringComparison.Ordinal)).Content;

        Assert.DoesNotContain("NativeArray_array_create", nativeOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("NativeArray_array_get", nativeOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Fixed_array_free_functions_use_declared_marshaller_tuple()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "SharpCpp.Sample",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "Array/NativeArray.hpp" });
        var arrayRecord = new RecordBinding("record:SharpCpp::NativeArray", "NativeArray", "NativeArray", "SharpCpp::NativeArray")
        {
            ManagedTypeFormat = "global::SharpCpp.NativeArray<{0}>"
        };
        arrayRecord.GenericParameters.Add(new BindingGenericParameter("T", 0));
        arrayRecord.GenericParameters.Add(new BindingGenericParameter("N", 1));
        arrayRecord.SetMetadata(MetadataKeys.FixedArrayRecord, true);
        arrayRecord.SetMetadata(MetadataKeys.NativeArrayRecord, true);
        compilation.Members.Add(arrayRecord);

        var fixedArrayType = BindingType.CreateHandle(
            "SharpCpp::NativeArray<int,2>",
            "global::SharpCpp.NativeArray<{0}>",
            arrayRecord.SymbolId,
            genericArguments: [BindingType.Int32, BindingType.CreateConfigured("2", "2")])
            .WithFixedArray(BindingType.Int32, 2);
        var function = new FunctionBinding(
            "function:consume_pair",
            "consume_pair",
            "ConsumePair",
            BindingType.Void,
            "sharpcpp_sample_consume_pair",
            "void",
            "Sample::ConsumePair");
        function.Parameters.Add(new ParameterBinding(
            "parameter:consume_pair:values",
            "values",
            "values",
            fixedArrayType,
            BindingParameterDirection.In,
            BindingParameterAbiKind.Value,
            "int [2]"));
        compilation.Members.Add(function);

        var files = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files;
        var nativeOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cpp", StringComparison.Ordinal)).Content;

        Assert.Contains("SHARPCPP_EXPORT ret_t<void> sharpcpp_sample_consume_pair(arg_t<int [2]> values)", nativeOutput, StringComparison.Ordinal);
        Assert.Contains("MarshallerInvoker::invoke_as<FT, std::tuple<int [2]>>(static_cast<std::add_pointer_t<FT>>(&Sample::ConsumePair), values);", nativeOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Generic_array_helpers_honor_entrypoint_prefix()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "SharpCpp.Sample",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "Array/NativeArray.hpp" })
        {
            EntryPointPrefix = "sample_prefix"
        };
        var arrayRecord = new RecordBinding("record:NativeArray", "NativeArray", "NativeArray", "NativeArray")
        {
            ManagedTypeFormat = "global::SharpCpp.NativeArray<{0}>"
        };
        arrayRecord.GenericParameters.Add(new BindingGenericParameter("T", 0));
        arrayRecord.GenericParameters.Add(new BindingGenericParameter("N", 1));
        arrayRecord.SetMetadata(MetadataKeys.NativeArrayRecord, true);
        arrayRecord.SetMetadata(MetadataKeys.FixedArrayRecord, true);
        arrayRecord.SupportedGenericInstantiations.Add(new RecordGenericInstantiationBinding([BindingType.Single, BindingType.CreateConfigured("3", "3")]));
        compilation.Members.Add(arrayRecord);

        var files = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files;
        var managedOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cs", StringComparison.Ordinal)).Content;
        var nativeOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cpp", StringComparison.Ordinal)).Content;

        Assert.Contains("EntryPoint = \"sample_prefix_NativeArray_array_create_float_3\"", managedOutput, StringComparison.Ordinal);
        Assert.Contains("SHARPCPP_EXPORT NativeArrayAbi sample_prefix_NativeArray_array_create_float_3()", nativeOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("sharpcpp_e2e_NativeArray_array_create_float_3", nativeOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Polymorphic_records_in_custom_namespace_qualify_registry_initializer()
    {
        var compilation = new BindingCompilation(
            "SharpCpp.Sample",
            "SharpCpp.Sample",
            "SampleBindings",
            "SharpCpp.Sample.Native",
            new[] { "Custom/Widget.hpp" });
        var record = new RecordBinding("record:Custom::Widget", "Widget", "Widget", "Custom::Widget")
        {
            ManagedNamespace = "Custom",
            IsPolymorphic = true
        };
        compilation.Members.Add(record);

        var files = BindingOutputRenderer.Render(compilation, "SharpCppSample").Files;
        var managedOutput = files.Single(static file => file.RelativePath.EndsWith(".Bindings.g.cs", StringComparison.Ordinal)).Content;

        Assert.Contains("[global::System.Runtime.CompilerServices.ModuleInitializer]", managedOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("EnsureRegistered", managedOutput, StringComparison.Ordinal);
        Assert.Contains("global::SharpCpp.Support.NativeObjectRegistry.Create<Widget>(handle, ownsHandle)", managedOutput, StringComparison.Ordinal);
    }
}
