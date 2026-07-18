using System.Reflection;
using System.Runtime.CompilerServices;

using SharpCpp.Model.Ast;
using SharpCpp.Pipeline;
using SharpCpp.Parsing;

using Xunit;

namespace SharpCpp.Tests.Model;

public sealed class ClangSharpHeaderParserMarshallerTests
{
    [Fact]
    public void Parse_applies_marshaller_metadata_from_token_spaced_configuration_text()
    {
        var repoRoot = FindRepositoryRoot();
        var fixtureDir = Path.Combine(repoRoot, "tests", "SharpCpp.Tests.Model", "Fixtures", "TokenSpacedConfig");
        var includeDir = Path.Combine(repoRoot, "include");
        var marshallerHeader = Path.Combine(includeDir, "SharpCpp", "Marshaller.hpp");
        var headerPath = Path.Combine(fixtureDir, "Sample.hpp");

        var parser = new ClangSharpHeaderParser();
        var clangArgs = CreateStandardLibraryClangArgs(fixtureDir, includeDir);
        var compilation = parser.Parse(new ClangSharpParseRequest(
            ProjectName: "TestProject",
            ManagedNamespace: "Test.Namespace",
            ManagedClassName: "Bindings",
            NativeLibraryName: "Test.Native",
            HeaderFilePath: headerPath,
            HeaderIncludePath: fixtureDir,
            ClangArguments: clangArgs,
            MarshallerConfigurationFiles: [marshallerHeader],
            EntryPointPrefix: "Test_"));

        var pointerFunction = Assert.Single(compilation.Functions.Where(static function => function.NativeName == "GetName"));
        Assert.Equal("global::System.ReadOnlySpan<byte>", pointerFunction.ReturnType.ManagedTypeName);
        Assert.Equal("const char*", pointerFunction.ReturnType.CppTypeName);

        var viewReturnFunction = Assert.Single(compilation.Functions.Where(static function => function.NativeName == "GetNameView"));
        Assert.Equal("global::System.ReadOnlySpan<byte>", viewReturnFunction.ReturnType.ManagedTypeName);
        Assert.Equal("global::SharpCpp.Support.Utf8SpanAbiReturnMarshaller", viewReturnFunction.ReturnType.ManagedMarshaller);

        var viewParameterFunction = Assert.Single(compilation.Functions.Where(static function => function.NativeName == "MeasureNameView"));
        Assert.Equal("global::System.ReadOnlySpan<byte>", viewParameterFunction.Parameters[0].Type.ManagedTypeName);
        Assert.Equal("global::SharpCpp.Support.Utf8SpanAbiMarshaller", viewParameterFunction.Parameters[0].Type.ManagedMarshaller);
        Assert.Equal(BindingParameterAbiKind.Value, viewParameterFunction.Parameters[0].AbiKind);
    }

    [Fact]
    public void Parse_and_pipeline_materialize_wrapper_surface_from_custom_string_marshaller()
    {
        var repoRoot = FindRepositoryRoot();
        var fixtureDir = Path.Combine(repoRoot, "tests", "SharpCpp.Tests.Model", "Fixtures", "CustomStringMarshaller");
        var headerPath = Path.Combine(fixtureDir, "Sample.hpp");
        var marshallerHeader = Path.Combine(fixtureDir, "SampleMarshallers.h");

        var parser = new ClangSharpHeaderParser();
        var clangArgs = new[] { "-x", "c++", "-std=c++20", $"-I{fixtureDir}" };
        var marshallerFiles = new[] { marshallerHeader };

        var compilation = parser.Parse(new ClangSharpParseRequest(
            ProjectName: "TestProject",
            ManagedNamespace: "Test.Namespace",
            ManagedClassName: "Bindings",
            NativeLibraryName: "Test.Native",
            HeaderFilePath: headerPath,
            HeaderIncludePath: fixtureDir,
            ClangArguments: clangArgs,
            MarshallerConfigurationFiles: marshallerFiles,
            EntryPointPrefix: "Test_"));

        MarshallerMetadataPhase2.ApplyWithClang(compilation, marshallerFiles, clangArgs);

        var function = Assert.Single(compilation.Functions.Where(static function => function.NativeName == "measure_ea_string"));
        Assert.Equal(BindingTypeKind.Handle, function.Parameters[0].Type.Kind);
        Assert.Equal("Ea.String", function.Parameters[0].Type.ManagedTypeName);

        var record = Assert.Single(compilation.Records.Where(static record => record.NativeName == "ea_compat_string"));
        Assert.Equal("String", record.ManagedName);
        Assert.Equal("Ea", record.ManagedNamespace);
        Assert.Equal("global::SharpCpp.Support.StringBase", record.ManagedBaseTypeName);
        Assert.Contains(record.Members.OfType<FunctionBinding>(), static member => member.IsConstructor && member.Parameters.Count == 0);
        Assert.Contains(record.Members.OfType<FunctionBinding>(), static member => member.ManagedName == "GetCountCore");
        Assert.Contains(record.Members.OfType<FunctionBinding>(), static member => member.ManagedName == "GetDataCore");
        Assert.Contains(record.Members.OfType<FunctionBinding>(), static member => member.ManagedName == "AddCore");
        Assert.Contains(record.Members.OfType<FunctionBinding>(), static member => member.ManagedName == "ClearCore");
    }

    [Fact]
    public void Parse_recognizes_alias_spelled_string_view_marshaller()
    {
        var repoRoot = FindRepositoryRoot();
        var fixtureDir = Path.Combine(repoRoot, "tests", "SharpCpp.Tests.Model", "Fixtures", "AliasStringViewMarshaller");
        var includeDir = Path.Combine(repoRoot, "include");
        var headerPath = Path.Combine(fixtureDir, "Sample.hpp");
        var marshallerHeader = Path.Combine(fixtureDir, "SampleMarshallers.h");

        var parser = new ClangSharpHeaderParser();
        var clangArgs = CreateStandardLibraryClangArgs(fixtureDir, includeDir);
        var marshallerFiles = new[] { marshallerHeader };

        var compilation = parser.Parse(new ClangSharpParseRequest(
            ProjectName: "TestProject",
            ManagedNamespace: "Test.Namespace",
            ManagedClassName: "Bindings",
            NativeLibraryName: "Test.Native",
            HeaderFilePath: headerPath,
            HeaderIncludePath: fixtureDir,
            ClangArguments: clangArgs,
            MarshallerConfigurationFiles: marshallerFiles,
            EntryPointPrefix: "Test_"));

        var parameterFunction = Assert.Single(compilation.Functions.Where(static function => function.NativeName == "measure_ea_string_view"));
        Assert.Equal("global::System.ReadOnlySpan<byte>", parameterFunction.Parameters[0].Type.ManagedTypeName);
        Assert.Equal("global::SharpCpp.Support.Utf8SpanAbiMarshaller", parameterFunction.Parameters[0].Type.ManagedMarshaller);
        Assert.Equal(BindingParameterAbiKind.Value, parameterFunction.Parameters[0].AbiKind);

        var defaultedFunction = Assert.Single(compilation.Functions.Where(static function => function.NativeName == "measure_default_ea_string_view"));
        Assert.True(defaultedFunction.Parameters[0].HasMetadata(MetadataKeys.UndefaultableParam));
        Assert.False(defaultedFunction.Parameters[0].HasMetadata(MetadataKeys.UnexpressibleDefault));

        var returnFunction = Assert.Single(compilation.Functions.Where(static function => function.NativeName == "get_ea_string_view"));
        Assert.Equal("global::System.ReadOnlySpan<byte>", returnFunction.ReturnType.ManagedTypeName);
        Assert.Equal("global::SharpCpp.Support.Utf8SpanAbiReturnMarshaller", returnFunction.ReturnType.ManagedMarshaller);
    }

    [Fact]
    public void Parse_defaults_managed_pointer_override_parameters_to_in_value_tokens()
    {
        var repoRoot = FindRepositoryRoot();
        var fixtureDir = Path.Combine(repoRoot, "tests", "SharpCpp.Tests.Model", "Fixtures", "BytePointerMarshaller");
        var includeDir = Path.Combine(repoRoot, "include");
        var headerPath = Path.Combine(fixtureDir, "Sample.hpp");
        var marshallerHeader = Path.Combine(includeDir, "SharpCpp", "Marshaller.hpp");

        var parser = new ClangSharpHeaderParser();
        var clangArgs = CreateStandardLibraryClangArgs(fixtureDir, includeDir);

        var compilation = parser.Parse(new ClangSharpParseRequest(
            ProjectName: "TestProject",
            ManagedNamespace: "Test.Namespace",
            ManagedClassName: "Bindings",
            NativeLibraryName: "Test.Native",
            HeaderFilePath: headerPath,
            HeaderIncludePath: fixtureDir,
            ClangArguments: clangArgs,
            MarshallerConfigurationFiles: [marshallerHeader],
            EntryPointPrefix: "Test_"));

        var unsignedBuffer = Assert.Single(compilation.Functions.Where(static function => function.NativeName == "consume_unsigned_buffer"));
        Assert.Equal("global::System.IntPtr", unsignedBuffer.Parameters[0].Type.ManagedTypeName);
        Assert.Equal(BindingParameterDirection.In, unsignedBuffer.Parameters[0].Direction);
        Assert.Equal(BindingParameterAbiKind.Value, unsignedBuffer.Parameters[0].AbiKind);

        var volatileUnsignedBuffer = Assert.Single(compilation.Functions.Where(static function => function.NativeName == "consume_volatile_unsigned_buffer"));
        Assert.Equal("global::System.IntPtr", volatileUnsignedBuffer.Parameters[0].Type.ManagedTypeName);
        Assert.Equal(BindingParameterDirection.In, volatileUnsignedBuffer.Parameters[0].Direction);
        Assert.Equal(BindingParameterAbiKind.Value, volatileUnsignedBuffer.Parameters[0].AbiKind);

        var constVolatileUnsignedBuffer = Assert.Single(compilation.Functions.Where(static function => function.NativeName == "consume_const_volatile_unsigned_buffer"));
        Assert.Equal("global::System.IntPtr", constVolatileUnsignedBuffer.Parameters[0].Type.ManagedTypeName);
        Assert.Equal(BindingParameterDirection.In, constVolatileUnsignedBuffer.Parameters[0].Direction);
        Assert.Equal(BindingParameterAbiKind.Value, constVolatileUnsignedBuffer.Parameters[0].AbiKind);

        var signedBuffer = Assert.Single(compilation.Functions.Where(static function => function.NativeName == "consume_signed_buffer"));
        Assert.Equal("global::System.IntPtr", signedBuffer.Parameters[0].Type.ManagedTypeName);
        Assert.Equal(BindingParameterDirection.In, signedBuffer.Parameters[0].Direction);
        Assert.Equal(BindingParameterAbiKind.Value, signedBuffer.Parameters[0].AbiKind);

        var volatileSignedBuffer = Assert.Single(compilation.Functions.Where(static function => function.NativeName == "consume_volatile_signed_buffer"));
        Assert.Equal("global::System.IntPtr", volatileSignedBuffer.Parameters[0].Type.ManagedTypeName);
        Assert.Equal(BindingParameterDirection.In, volatileSignedBuffer.Parameters[0].Direction);
        Assert.Equal(BindingParameterAbiKind.Value, volatileSignedBuffer.Parameters[0].AbiKind);

        var constVolatileSignedBuffer = Assert.Single(compilation.Functions.Where(static function => function.NativeName == "consume_const_volatile_signed_buffer"));
        Assert.Equal("global::System.IntPtr", constVolatileSignedBuffer.Parameters[0].Type.ManagedTypeName);
        Assert.Equal(BindingParameterDirection.In, constVolatileSignedBuffer.Parameters[0].Direction);
        Assert.Equal(BindingParameterAbiKind.Value, constVolatileSignedBuffer.Parameters[0].AbiKind);

        var explicitUnsignedOut = Assert.Single(compilation.Functions.Where(static function => function.NativeName == "write_unsigned_pointer"));
        Assert.Equal("global::System.IntPtr", explicitUnsignedOut.Parameters[0].Type.ManagedTypeName);
        Assert.Equal(BindingParameterDirection.Out, explicitUnsignedOut.Parameters[0].Direction);
        Assert.Equal(BindingParameterAbiKind.Value, explicitUnsignedOut.Parameters[0].AbiKind);

        var explicitSignedOut = Assert.Single(compilation.Functions.Where(static function => function.NativeName == "write_signed_pointer"));
        Assert.Equal("global::System.IntPtr", explicitSignedOut.Parameters[0].Type.ManagedTypeName);
        Assert.Equal(BindingParameterDirection.Out, explicitSignedOut.Parameters[0].Direction);
        Assert.Equal(BindingParameterAbiKind.Value, explicitSignedOut.Parameters[0].AbiKind);
    }

    [Fact]
    public void Parse_treats_null_macro_pointer_defaults_as_managed_defaults()
    {
        var repoRoot = FindRepositoryRoot();
        var fixtureDir = Path.Combine(repoRoot, "tests", "SharpCpp.Tests.Model", "Fixtures", "PointerNullDefaults");
        var includeDir = Path.Combine(repoRoot, "include");
        var headerPath = Path.Combine(fixtureDir, "Sample.hpp");
        var marshallerHeader = Path.Combine(fixtureDir, "SampleMarshallers.h");

        var parser = new ClangSharpHeaderParser();
        var clangArgs = CreateStandardLibraryClangArgs(fixtureDir, includeDir);

        var compilation = parser.Parse(new ClangSharpParseRequest(
            ProjectName: "TestProject",
            ManagedNamespace: "Test.Namespace",
            ManagedClassName: "Bindings",
            NativeLibraryName: "Test.Native",
            HeaderFilePath: headerPath,
            HeaderIncludePath: fixtureDir,
            ClangArguments: clangArgs,
            MarshallerConfigurationFiles: [marshallerHeader],
            EntryPointPrefix: "Test_"));

        var function = Assert.Single(compilation.Functions.Where(static candidate => candidate.NativeName == "accept_pointer"));
        var parameter = Assert.Single(function.Parameters);
        Assert.Equal("global::System.IntPtr", parameter.Type.ManagedTypeName);
        Assert.Equal("default", parameter.ManagedDefaultValueExpression);
        Assert.False(parameter.HasMetadata(MetadataKeys.UnexpressibleDefault));
    }

    [Fact]
    public void Parse_marks_empty_macro_expansions_as_unexpressible_defaults()
    {
        var repoRoot = FindRepositoryRoot();
        var fixtureDir = Path.Combine(repoRoot, "tests", "SharpCpp.Tests.Model", "Fixtures", "EmptyMacroDefaults");
        var includeDir = Path.Combine(repoRoot, "include");
        var headerPath = Path.Combine(fixtureDir, "Sample.hpp");

        var parser = new ClangSharpHeaderParser();
        var clangArgs = CreateStandardLibraryClangArgs(fixtureDir, includeDir);

        var compilation = parser.Parse(new ClangSharpParseRequest(
            ProjectName: "TestProject",
            ManagedNamespace: "Test.Namespace",
            ManagedClassName: "Bindings",
            NativeLibraryName: "Test.Native",
            HeaderFilePath: headerPath,
            HeaderIncludePath: fixtureDir,
            ClangArguments: clangArgs,
            MarshallerConfigurationFiles: Array.Empty<string>(),
            EntryPointPrefix: "Test_"));

        var function = Assert.Single(compilation.Functions.Where(static candidate => candidate.NativeName == "plot_value"));
        Assert.All(function.Parameters, static parameter =>
        {
            Assert.Null(parameter.ManagedDefaultValueExpression);
            Assert.True(parameter.HasMetadata(MetadataKeys.UnexpressibleDefault));
        });
    }

    [Fact]
    public void Parse_maps_callback_function_objects_to_managed_delegate_types()
    {
        var repoRoot = FindRepositoryRoot();
        var fixtureDir = Path.Combine(repoRoot, "tests", "SharpCpp.Tests.Model", "Fixtures", "CallbackMarshaller");
        var headerPath = Path.Combine(fixtureDir, "Sample.hpp");
        var marshallerHeader = Path.Combine(fixtureDir, "SampleMarshallers.h");

        var parser = new ClangSharpHeaderParser();
        var clangArgs = new[] { "-x", "c++", "-std=c++20", $"-I{fixtureDir}" };

        var compilation = parser.Parse(new ClangSharpParseRequest(
            ProjectName: "TestProject",
            ManagedNamespace: "Test.Namespace",
            ManagedClassName: "Bindings",
            NativeLibraryName: "Test.Native",
            HeaderFilePath: headerPath,
            HeaderIncludePath: fixtureDir,
            ClangArguments: clangArgs,
            MarshallerConfigurationFiles: [marshallerHeader],
            EntryPointPrefix: "Test_"));

        var forEach = Assert.Single(compilation.Functions.Where(static function => function.NativeName == "ForEach"));
        var forEachCallback = forEach.Parameters[0].Type;
        Assert.True(forEachCallback.IsCallback);
        Assert.Equal("global::System.Action<int, float>", forEachCallback.ManagedTypeName);
        Assert.Equal("global::Test.CallbackMarshaller", forEachCallback.ManagedMarshaller);
        Assert.Equal("void", forEachCallback.CallbackReturnType!.ManagedTypeName);
        Assert.Equal(new[] { "int", "float" }, forEachCallback.CallbackParameterTypes.Select(static type => type.ManagedTypeName));
        Assert.Equal(BindingParameterDirection.In, forEach.Parameters[0].Direction);
        Assert.Equal(BindingParameterAbiKind.Value, forEach.Parameters[0].AbiKind);

        var transform = Assert.Single(compilation.Functions.Where(static function => function.NativeName == "Transform"));
        var transformCallback = transform.Parameters[0].Type;
        Assert.True(transformCallback.IsCallback);
        Assert.Equal("global::System.Func<int, int>", transformCallback.ManagedTypeName);
        Assert.Equal("int", transformCallback.CallbackReturnType!.ManagedTypeName);
        Assert.Equal(new[] { "int" }, transformCallback.CallbackParameterTypes.Select(static type => type.ManagedTypeName));

        var setCallback = Assert.Single(compilation.Functions.Where(static function => function.NativeName == "SetCallback"));
        var parameterlessCallback = setCallback.Parameters[0].Type;
        Assert.True(parameterlessCallback.IsCallback);
        Assert.Equal("global::System.Action", parameterlessCallback.ManagedTypeName);
        Assert.Empty(parameterlessCallback.CallbackParameterTypes);

        var setDefaultCallback = Assert.Single(compilation.Functions.Where(static function => function.NativeName == "SetDefaultCallback"));
        var defaultedCallbackParameter = setDefaultCallback.Parameters[0];
        Assert.True(defaultedCallbackParameter.Type.IsCallback);
        Assert.Equal("null", defaultedCallbackParameter.ManagedDefaultValueExpression);

        var vectorCallback = Assert.Single(compilation.Functions.Where(static function => function.NativeName == "UseVectorCallback"));
        var vectorCallbackArgument = Assert.Single(vectorCallback.Parameters[0].Type.CallbackParameterTypes);
        Assert.Equal("global::Test.Vector2", vectorCallbackArgument.ManagedTypeName);
        Assert.Equal("global::Test.Vector2", vectorCallbackArgument.MarshallerTypeName);

        var callbackOwner = Assert.Single(compilation.Records.Where(static record => record.NativeName == "CallbackOwner"));
        var callbackOwnerConstructor = Assert.Single(callbackOwner.Members.OfType<FunctionBinding>().Where(static function => function.IsConstructor));
        Assert.False(callbackOwnerConstructor.IsIgnored(), callbackOwnerConstructor.GetIgnoreReason());
        var handlerParameter = Assert.Single(callbackOwnerConstructor.Parameters.Where(static parameter => parameter.NativeName == "handler"));
        Assert.True(handlerParameter.Type.IsCallback);
        Assert.False(handlerParameter.Type.ContainsGenericParameters);
        Assert.Equal("global::System.Action<Object, StringHash, Map<StringHash, Variant>>", handlerParameter.Type.ManagedTypeName);
        Assert.Equal(new[] { "Object", "StringHash", "Map<StringHash, Variant>" }, handlerParameter.Type.CallbackParameterTypes.Select(static type => type.ManagedTypeName));
        Assert.DoesNotContain(handlerParameter.Type.CallbackParameterTypes, static type => type.ContainsGenericParameters);

        var generatedDelegate = Assert.Single(compilation.Functions.Where(static function => function.NativeName == "UseGeneratedDelegate"));
        Assert.True(generatedDelegate.Parameters[0].UseCallbackDelegate);
        Assert.Null(generatedDelegate.Parameters[0].CallbackDelegateTypeName);
        Assert.False(generatedDelegate.IsIgnored(), generatedDelegate.GetIgnoreReason());

        var namedDelegate = Assert.Single(compilation.Functions.Where(static function => function.NativeName == "UseNamedDelegate"));
        Assert.True(namedDelegate.Parameters[0].UseCallbackDelegate);
        Assert.Equal("ProgressCallback", namedDelegate.Parameters[0].CallbackDelegateTypeName);
        Assert.False(namedDelegate.IsIgnored(), namedDelegate.GetIgnoreReason());

        var invalidDelegate = Assert.Single(compilation.Functions.Where(static function => function.NativeName == "UseInvalidDelegate"));
        Assert.True(invalidDelegate.IsIgnored());
        Assert.Contains("invalid [[sharpcpp::delegate]]", invalidDelegate.GetIgnoreReason(), StringComparison.Ordinal);

        var invalidExternalDelegate = Assert.Single(compilation.Functions.Where(static function => function.NativeName == "UseInvalidExternalDelegate"));
        Assert.True(invalidExternalDelegate.IsIgnored());
        Assert.Contains("external delegate type name 'global::' is not a valid managed type name", invalidExternalDelegate.GetIgnoreReason(), StringComparison.Ordinal);

        var nonCallbackDelegate = Assert.Single(compilation.Functions.Where(static function => function.NativeName == "UseDelegateOnNonCallback"));
        Assert.True(nonCallbackDelegate.IsIgnored());
        Assert.Contains("not a callback parameter", nonCallbackDelegate.GetIgnoreReason(), StringComparison.Ordinal);
    }

    [Fact]
    public void Bootstrap_pipeline_preserves_alias_backed_map_callback_parameters()
    {
        var repoRoot = FindRepositoryRoot();
        var fixtureDir = Path.Combine(repoRoot, "tests", "SharpCpp.Tests.Model", "Fixtures", "CallbackMarshaller");
        var headerPath = Path.Combine(fixtureDir, "Sample.hpp");
        var marshallerHeader = Path.Combine(fixtureDir, "SampleMarshallers.h");

        var parser = new ClangSharpHeaderParser();
        var clangArgs = new[] { "-x", "c++", "-std=c++20", $"-I{fixtureDir}" };
        var marshallerFiles = new[] { marshallerHeader };

        var compilation = parser.Parse(new ClangSharpParseRequest(
            ProjectName: "TestProject",
            ManagedNamespace: "Test.Namespace",
            ManagedClassName: "Bindings",
            NativeLibraryName: "Test.Native",
            HeaderFilePath: headerPath,
            HeaderIncludePath: fixtureDir,
            ClangArguments: clangArgs,
            MarshallerConfigurationFiles: marshallerFiles,
            EntryPointPrefix: "Test_"));

        BindingCompilationBootstrapPipeline.Apply(compilation, marshallerFiles, clangArgs);

        var callbackOwner = Assert.Single(compilation.Records.Where(static record => record.NativeName == "CallbackOwner"));
        var constructor = Assert.Single(callbackOwner.Members.OfType<FunctionBinding>().Where(static function => function.IsConstructor));
        Assert.False(constructor.IsIgnored(), constructor.GetIgnoreReason());

        var handler = Assert.Single(constructor.Parameters.Where(static parameter => parameter.NativeName == "handler"));
        Assert.True(handler.Type.IsCallback);
        Assert.Equal("global::System.Action<Object, StringHash, VariantMap>", handler.Type.ManagedTypeName);
        Assert.Equal(new[] { "Object", "StringHash", "VariantMap" }, handler.Type.CallbackParameterTypes.Select(static type => type.ManagedTypeName));

        var mapRecord = Assert.Single(compilation.Records.Where(static record => record.ManagedName == "Map"));
        Assert.False(mapRecord.IsIgnored(), mapRecord.GetIgnoreReason());
        Assert.Contains(mapRecord.SupportedGenericInstantiations, instantiation =>
            instantiation.TypeArguments.Select(static type => type.ManagedTypeName)
                .SequenceEqual(new[] { "StringHash", "Variant" }, StringComparer.Ordinal));
    }

    [Fact]
    public void Parse_projects_shared_ptr_and_marks_returns_as_owning_handles()
    {
        var repoRoot = FindRepositoryRoot();
        var fixtureDir = Path.Combine(repoRoot, "tests", "SharpCpp.Tests.Model", "Fixtures", "SharedPtr");
        var headerPath = Path.Combine(fixtureDir, "Sample.hpp");
        var marshallerHeader = Path.Combine(fixtureDir, "SampleMarshallers.h");

        var parser = new ClangSharpHeaderParser();
        var clangArgs = new[] { "-x", "c++", "-std=c++20", $"-I{fixtureDir}" };
        var marshallerFiles = new[] { marshallerHeader };

        var compilation = parser.Parse(new ClangSharpParseRequest(
            ProjectName: "TestProject",
            ManagedNamespace: "Test.Namespace",
            ManagedClassName: "Bindings",
            NativeLibraryName: "Test.Native",
            HeaderFilePath: headerPath,
            HeaderIncludePath: fixtureDir,
            ClangArguments: clangArgs,
            MarshallerConfigurationFiles: marshallerFiles,
            EntryPointPrefix: "Test_"));

        MarshallerMetadataPhase2.ApplyWithClang(compilation, marshallerFiles, clangArgs);
        SharpCpp.Pipeline.InheritManagedRuntimePolicyPass.Apply(compilation);

        var refCountedRecord = Assert.Single(compilation.Records.Where(static record => record.NativeName == "RefCounted"));
        var payloadRecord = Assert.Single(compilation.Records.Where(static record => record.NativeName == "sample_payload"));
        Assert.Equal("global::Urho3D.Interop.RefCountedNativeObject", refCountedRecord.ManagedBaseTypeName);
        Assert.Equal("RefCounted", payloadRecord.ManagedBaseTypeName);
        Assert.Equal("global::Urho3D.Interop.RefCountedInstanceCache.Shared", payloadRecord.ManagedInstanceCacheFactoryExpression);
        Assert.False(payloadRecord.EmitDefaultNativeReleaseOverride);

        var sharedReturn = Assert.Single(compilation.Functions.Where(static function => function.NativeName == "get_payload_shared"));
        Assert.Equal(BindingTypeKind.Handle, sharedReturn.ReturnType.Kind);
        Assert.Equal(payloadRecord.SymbolId, sharedReturn.ReturnType.RecordId);
        Assert.Equal("Urho3D::SharedPtr<sample_payload>", sharedReturn.ReturnType.CppTypeName);
        Assert.True(sharedReturn.ReturnsOwnedHandle);

        var weakReturn = Assert.Single(compilation.Functions.Where(static function => function.NativeName == "get_payload_weak"));
        Assert.Equal(BindingTypeKind.Handle, weakReturn.ReturnType.Kind);
        Assert.Equal(payloadRecord.SymbolId, weakReturn.ReturnType.RecordId);
        Assert.Equal("Urho3D::WeakPtr<sample_payload>", weakReturn.ReturnType.CppTypeName);
        Assert.True(weakReturn.ReturnsOwnedHandle);

        var sharedParameter = Assert.Single(compilation.Functions.Where(static function => function.NativeName == "use_payload_shared"));
        Assert.Equal(BindingTypeKind.Handle, sharedParameter.Parameters[0].Type.Kind);
        Assert.Equal(payloadRecord.SymbolId, sharedParameter.Parameters[0].Type.RecordId);
    }

    [Fact]
    public void Bootstrap_pipeline_supports_vector_of_record_handle_parameters()
    {
        var repoRoot = FindRepositoryRoot();
        var fixtureDir = Path.Combine(repoRoot, "tests", "SharpCpp.Tests.Model", "Fixtures", "VectorOfRecordHandleParameters");
        var headerPath = Path.Combine(fixtureDir, "Sample.hpp");
        var marshallerHeader = Path.Combine(fixtureDir, "SampleMarshallers.h");

        var parser = new ClangSharpHeaderParser();
        var clangArgs = new[] { "-x", "c++", "-std=c++20", $"-I{fixtureDir}" };
        var marshallerFiles = new[] { marshallerHeader };

        var compilation = parser.Parse(new ClangSharpParseRequest(
            ProjectName: "TestProject",
            ManagedNamespace: "Test.Namespace",
            ManagedClassName: "Bindings",
            NativeLibraryName: "Test.Native",
            HeaderFilePath: headerPath,
            HeaderIncludePath: fixtureDir,
            ClangArguments: clangArgs,
            MarshallerConfigurationFiles: marshallerFiles,
            EntryPointPrefix: "Test_"));

        BindingCompilationBootstrapPipeline.Apply(compilation, marshallerFiles, clangArgs);
        var eventHub = Assert.Single(compilation.Records.Where(static record => record.NativeName == "EventHub"));
        var method = Assert.Single(eventHub.Members.OfType<FunctionBinding>().Where(static function => function.NativeName == "UnsubscribeFromAllEventsExcept"));

        Assert.False(
            method.IsIgnored(),
            $"Ignored because '{method.GetIgnoreReason()}' with managed type '{method.Parameters[0].Type.ManagedTypeName}'.");
        Assert.Equal("Ea.Vector<Object>", method.Parameters[0].Type.ManagedTypeName);
        Assert.Equal("const eastl::vector<Urho3D::Object*>&", method.Parameters[0].CppTypeName);
    }

    [Fact]
    public void Bootstrap_pipeline_supports_hash_map_alias_return_surface()
    {
        var repoRoot = FindRepositoryRoot();
        var fixtureDir = Path.Combine(repoRoot, "tests", "SharpCpp.Tests.Model", "Fixtures", "HashMapAlias");
        var headerPath = Path.Combine(fixtureDir, "Sample.hpp");
        var marshallerHeader = Path.Combine(fixtureDir, "SampleMarshallers.h");

        var parser = new ClangSharpHeaderParser();
        var clangArgs = new[] { "-x", "c++", "-std=c++20", $"-I{fixtureDir}" };
        var marshallerFiles = new[] { marshallerHeader };

        var compilation = parser.Parse(new ClangSharpParseRequest(
            ProjectName: "TestProject",
            ManagedNamespace: "Test",
            ManagedClassName: "Bindings",
            NativeLibraryName: "Test.Native",
            HeaderFilePath: headerPath,
            HeaderIncludePath: fixtureDir,
            ClangArguments: clangArgs,
            MarshallerConfigurationFiles: marshallerFiles,
            EntryPointPrefix: "Test_"));

        BindingCompilationBootstrapPipeline.Apply(compilation, marshallerFiles, clangArgs);

        var eventHub = Assert.Single(compilation.Records.Where(static record => record.NativeName == "EventHub"));
        var getEventDataMap = Assert.Single(eventHub.Members.OfType<FunctionBinding>().Where(static function => function.NativeName == "GetEventDataMap"));
        var getStringEventDataMap = Assert.Single(eventHub.Members.OfType<FunctionBinding>().Where(static function => function.NativeName == "GetStringEventDataMap"));

        Assert.False(getEventDataMap.IsIgnored(), $"Ignored because '{getEventDataMap.GetIgnoreReason()}'. Type: {DescribeType(getEventDataMap.ReturnType)}");
        Assert.False(getStringEventDataMap.IsIgnored(), $"Ignored because '{getStringEventDataMap.GetIgnoreReason()}'. Type: {DescribeType(getStringEventDataMap.ReturnType)}");
        Assert.NotEmpty(compilation.TypeAliases);
        var variantMapAlias = Assert.Single(compilation.TypeAliases.Where(static alias => alias.NativeName == "VariantMap"));
        var stringVariantMapAlias = Assert.Single(compilation.TypeAliases.Where(static alias => alias.NativeName == "StringVariantMap"));
        var unorderedMapRecord = Assert.Single(compilation.Records.Where(static record => record.ManagedName == "UnorderedMap"));
        Assert.Equal("Ea", unorderedMapRecord.ManagedNamespace);
        Assert.Equal("Ea.UnorderedMap<{0}, {1}>", unorderedMapRecord.ManagedTypeFormat);
        Assert.False(unorderedMapRecord.GetMetadata<bool>(MetadataKeys.SuppressEmission));
        Assert.Equal("VariantMap", variantMapAlias.ManagedName);
        Assert.Equal("StringVariantMap", stringVariantMapAlias.ManagedName);
        Assert.Equal(unorderedMapRecord.SymbolId, variantMapAlias.TargetType.RecordId);
        Assert.Equal("Ea.UnorderedMap<global::Urho3D.Math.StringHash, Variant>", variantMapAlias.TargetType.ManagedTypeName);
        Assert.Equal("Ea.UnorderedMap<Ea.String, Variant>", stringVariantMapAlias.TargetType.ManagedTypeName);
        Assert.Equal(2, variantMapAlias.TargetType.GenericArguments.Count);
        Assert.Equal(2, stringVariantMapAlias.TargetType.GenericArguments.Count);

        Assert.True(
            string.Equals("VariantMap", getEventDataMap.ReturnType.ManagedTypeName, StringComparison.Ordinal),
            $"Expected VariantMap but got {DescribeType(getEventDataMap.ReturnType)}; alias target {DescribeType(variantMapAlias.TargetType)}");
        Assert.True(
            string.Equals("StringVariantMap", getStringEventDataMap.ReturnType.ManagedTypeName, StringComparison.Ordinal),
            $"Expected StringVariantMap but got {DescribeType(getStringEventDataMap.ReturnType)}; alias target {DescribeType(stringVariantMapAlias.TargetType)}");
        Assert.Equal("const Urho3D::VariantMap&", getEventDataMap.CppReturnTypeName);
        Assert.Equal("const Urho3D::StringVariantMap&", getStringEventDataMap.CppReturnTypeName);
    }

    [Fact]
    public void Bootstrap_pipeline_keeps_hash_map_wrapper_after_multi_header_merge()
    {
        var repoRoot = FindRepositoryRoot();
        var fixtureDir = Path.Combine(repoRoot, "tests", "SharpCpp.Tests.Model", "Fixtures", "HashMapAlias");
        var headerPath = Path.Combine(fixtureDir, "Sample.hpp");
        var marshallerHeader = Path.Combine(fixtureDir, "SampleMarshallers.h");

        var parser = new ClangSharpHeaderParser();
        var clangArgs = new[] { "-x", "c++", "-std=c++20", $"-I{fixtureDir}" };
        var marshallerFiles = new[] { marshallerHeader };
        var compilation = new BindingCompilation(
            "TestProject",
            "Test",
            "Bindings",
            "Test.Native",
            Array.Empty<string>());

        foreach (var relativeHeaderPath in new[] { "First/Sample.hpp", "Second/Sample.hpp" })
        {
            var parsedHeaderCompilation = parser.Parse(new ClangSharpParseRequest(
                ProjectName: compilation.ProjectName,
                ManagedNamespace: compilation.ManagedNamespace,
                ManagedClassName: compilation.ManagedClassName,
                NativeLibraryName: compilation.NativeLibraryName,
                HeaderFilePath: headerPath,
                HeaderIncludePath: relativeHeaderPath,
                ClangArguments: clangArgs,
                MarshallerConfigurationFiles: marshallerFiles,
                EntryPointPrefix: "Test_"));

            compilation.HeaderFiles.Add(relativeHeaderPath);
            foreach (var member in parsedHeaderCompilation.Members.ToArray())
            {
                compilation.Members.Add(member);
            }

            foreach (var alias in parsedHeaderCompilation.TypeAliases)
            {
                compilation.TypeAliases.Add(alias);
            }
        }

        BindingCompilationBootstrapPipeline.Apply(compilation, marshallerFiles, clangArgs);

        var unorderedMapRecord = Assert.Single(compilation.Records.Where(static record => record.ManagedName == "UnorderedMap"));
        Assert.Equal("Ea", unorderedMapRecord.ManagedNamespace);
        Assert.Equal("Ea.UnorderedMap<{0}, {1}>", unorderedMapRecord.ManagedTypeFormat);
        Assert.False(unorderedMapRecord.GetMetadata<bool>(MetadataKeys.SuppressEmission));
        Assert.False(unorderedMapRecord.GetMetadata<bool>(MetadataKeys.PlaceholderRecord));
        Assert.False(unorderedMapRecord.IsIgnored(), unorderedMapRecord.GetIgnoreReason());

        var stringVariantMapAliases = compilation.TypeAliases
            .Where(static alias => alias.NativeName == "StringVariantMap")
            .ToArray();
        Assert.True(
            stringVariantMapAliases.Length > 0,
            $"Expected at least one StringVariantMap alias. Aliases: {string.Join("; ", compilation.TypeAliases.Select(static alias => $"{alias.CppQualifiedName}|{alias.NativeName}|{alias.ManagedName}|{alias.TargetType.RecordId}|{alias.TargetType.ManagedTypeName}"))}");
        Assert.All(stringVariantMapAliases, alias => Assert.Equal(unorderedMapRecord.SymbolId, alias.TargetType.RecordId));
        Assert.All(stringVariantMapAliases, static alias => Assert.Equal("Ea.UnorderedMap<Ea.String, Variant>", alias.TargetType.ManagedTypeName));
    }

    [Fact]
    public void Bootstrap_pipeline_keeps_eastl_hash_map_wrapper_when_std_unordered_map_config_is_present()
    {
        var repoRoot = FindRepositoryRoot();
        var fixtureDir = Path.Combine(repoRoot, "tests", "SharpCpp.Tests.Model", "Fixtures", "HashMapAlias");
        var includeDir = Path.Combine(repoRoot, "include");
        var headerPath = Path.Combine(fixtureDir, "Sample.hpp");
        var coreMarshallerHeader = Path.Combine(includeDir, "SharpCpp", "Marshaller.hpp");
        var eastlMarshallerHeader = Path.Combine(fixtureDir, "SampleMarshallers.h");

        var parser = new ClangSharpHeaderParser();
        var clangArgs = CreateStandardLibraryClangArgs(fixtureDir, includeDir);
        var marshallerFiles = new[] { coreMarshallerHeader, eastlMarshallerHeader };

        var compilation = parser.Parse(new ClangSharpParseRequest(
            ProjectName: "TestProject",
            ManagedNamespace: "Test",
            ManagedClassName: "Bindings",
            NativeLibraryName: "Test.Native",
            HeaderFilePath: headerPath,
            HeaderIncludePath: fixtureDir,
            ClangArguments: clangArgs,
            MarshallerConfigurationFiles: marshallerFiles,
            EntryPointPrefix: "Test_"));

        BindingCompilationBootstrapPipeline.Apply(compilation, marshallerFiles, clangArgs);

        var unorderedMapRecords = compilation.Records
            .Where(static record => record.ManagedName == "UnorderedMap")
            .ToArray();
        var candidates = string.Join("; ", unorderedMapRecords.Select(static record =>
            $"{record.SymbolId}|{record.CppQualifiedName}|{record.ManagedNamespace}|ignored={record.IsIgnored()}|{record.GetIgnoreReason()}"));
        var eastlMapRecords = unorderedMapRecords.Where(static record => record.ManagedNamespace == "Ea").ToArray();
        var stdMapRecords = unorderedMapRecords.Where(static record => record.ManagedNamespace == "Std").ToArray();
        Assert.True(eastlMapRecords.Length == 1, $"Expected one Ea UnorderedMap. Candidates: {candidates}");
        Assert.True(stdMapRecords.Length == 1, $"Expected one Std UnorderedMap. Candidates: {candidates}");
        var eastlMapRecord = eastlMapRecords[0];
        var stdMapRecord = stdMapRecords[0];
        var variantMapAlias = Assert.Single(compilation.TypeAliases.Where(static alias => alias.NativeName == "VariantMap"));

        Assert.NotEqual(stdMapRecord.SymbolId, eastlMapRecord.SymbolId);
        Assert.Equal("eastl::hash_map", eastlMapRecord.CppQualifiedName);
        Assert.Equal(eastlMapRecord.SymbolId, variantMapAlias.TargetType.RecordId);
        Assert.False(eastlMapRecord.IsIgnored(), eastlMapRecord.GetIgnoreReason());
        Assert.NotEmpty(eastlMapRecord.SupportedGenericInstantiations);
    }

    [Fact]
    public void Bootstrap_pipeline_materializes_std_string_wrapper_from_core_marshaller()
    {
        var repoRoot = FindRepositoryRoot();
        var fixtureDir = Path.Combine(repoRoot, "tests", "SharpCpp.Tests.E2E", "StandardLibrary");
        var includeDir = Path.Combine(repoRoot, "include");
        var headerPath = Path.Combine(fixtureDir, "StandardLibrary.hpp");
        var marshallerHeader = Path.Combine(includeDir, "SharpCpp", "Marshaller.hpp");

        var parser = new ClangSharpHeaderParser();
        var clangArgs = CreateStandardLibraryClangArgs(fixtureDir, includeDir);
        var marshallerFiles = new[] { marshallerHeader };

        var compilation = parser.Parse(new ClangSharpParseRequest(
            ProjectName: "TestProject",
            ManagedNamespace: "Test",
            ManagedClassName: "Bindings",
            NativeLibraryName: "Test.Native",
            HeaderFilePath: headerPath,
            HeaderIncludePath: fixtureDir,
            ClangArguments: clangArgs,
            MarshallerConfigurationFiles: marshallerFiles,
            EntryPointPrefix: "Test_"));

        BindingCompilationBootstrapPipeline.Apply(compilation, marshallerFiles, clangArgs);

        var makeText = Assert.Single(compilation.Functions.Where(static function => function.NativeName == "e2e_make_std_text"));
        Assert.False(makeText.IsIgnored(), makeText.GetIgnoreReason());
        Assert.Equal("Std.String", makeText.ReturnType.ManagedTypeName);

        var stringRecord = Assert.Single(compilation.Records.Where(static record => record.ManagedNamespace == "Std" && record.ManagedName == "String"));
        Assert.Equal("std::basic_string", stringRecord.NativeName);
        Assert.Equal("global::SharpCpp.Support.StringBase", stringRecord.ManagedBaseTypeName);
        Assert.False(stringRecord.GetMetadata<bool>(MetadataKeys.SuppressEmission));
        Assert.False(stringRecord.GetMetadata<bool>(MetadataKeys.PlaceholderRecord));
        Assert.False(stringRecord.IsIgnored(), stringRecord.GetIgnoreReason());
    }

    [Fact]
    public void Bootstrap_pipeline_keeps_std_string_wrapper_after_multi_header_merge()
    {
        var repoRoot = FindRepositoryRoot();
        var fixtureRoot = Path.Combine(repoRoot, "tests", "SharpCpp.Tests.E2E");
        var includeDir = Path.Combine(repoRoot, "include");
        var marshallerHeader = Path.Combine(includeDir, "SharpCpp", "Marshaller.hpp");
        var marshallerFiles = new[] { marshallerHeader };
        var clangArgs = CreateStandardLibraryClangArgs(fixtureRoot, includeDir);
        var parser = new ClangSharpHeaderParser();
        var compilation = new BindingCompilation(
            "TestProject",
            "Test",
            "Bindings",
            "Test.Native",
            Array.Empty<string>());

        foreach (var relativeHeaderPath in new[] { "String/String.hpp", "StandardLibrary/StandardLibrary.hpp", "Callback/Callback.hpp" })
        {
            var headerPath = Path.Combine(fixtureRoot, relativeHeaderPath);
            var parsedHeaderCompilation = parser.Parse(new ClangSharpParseRequest(
                ProjectName: compilation.ProjectName,
                ManagedNamespace: compilation.ManagedNamespace,
                ManagedClassName: compilation.ManagedClassName,
                NativeLibraryName: compilation.NativeLibraryName,
                HeaderFilePath: headerPath,
                HeaderIncludePath: relativeHeaderPath,
                ClangArguments: clangArgs,
                MarshallerConfigurationFiles: marshallerFiles,
                EntryPointPrefix: "Test_"));

            compilation.HeaderFiles.Add(relativeHeaderPath);
            foreach (var member in parsedHeaderCompilation.Members.ToArray())
            {
                compilation.Members.Add(member);
            }
        }

        BindingCompilationBootstrapPipeline.Apply(compilation, marshallerFiles, clangArgs);

        var stringRecord = Assert.Single(compilation.Records.Where(static record => record.ManagedNamespace == "Std" && record.ManagedName == "String"));
        Assert.Equal("std::basic_string", stringRecord.NativeName);
        Assert.Equal("global::SharpCpp.Support.StringBase", stringRecord.ManagedBaseTypeName);
        Assert.False(stringRecord.GetMetadata<bool>(MetadataKeys.SuppressEmission));
        Assert.False(stringRecord.GetMetadata<bool>(MetadataKeys.PlaceholderRecord));
        Assert.False(stringRecord.IsIgnored(), stringRecord.GetIgnoreReason());

        var stringCallback = Assert.Single(compilation.Functions.Where(static function => function.NativeName == "invoke_string_reference_callback"));
        Assert.Equal(stringRecord.SymbolId, Assert.Single(stringCallback.Parameters[0].Type.CallbackParameterTypes).RecordId);
    }

    [Fact]
    public void Bootstrap_pipeline_prunes_aliases_with_unresolved_configured_generic_arguments()
    {
        var compilation = new BindingCompilation("TestProject", "Urho3D", "Bindings", "Test.Native", ["Sample.hpp"]);
        var vectorRecord = new RecordBinding("record:eastl::vector", "eastl::vector", "Vector", "eastl::vector")
        {
            ManagedTypeFormat = "global::Ea.Vector<{0}>"
        };
        compilation.Members.Add(vectorRecord);

        var unresolvedPair = BindingType.CreateConfigured("ea::pair<ea::string, bool>", "ea::pair<ea::string, bool>");
        var aliasTarget = BindingType.CreateHandle(
            "ea::vector<ea::pair<ea::string, bool>>*",
            "global::Ea.Vector<{0}>",
            vectorRecord.SymbolId,
            genericArguments: [unresolvedPair]);
        compilation.TypeAliases.Add(new BindingTypeAlias("Urho3D::EnabledRenderPasses", "EnabledRenderPasses", "EnabledRenderPasses", aliasTarget));

        var prune = typeof(BindingCompilationBootstrapPipeline).GetMethod(
            "PruneAliasesTargetingDegradedRecords",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(prune);

        prune.Invoke(null, [compilation]);

        Assert.Empty(compilation.TypeAliases);
    }

    private static string DescribeType(BindingType type)
    {
        var args = type.GenericArguments.Count == 0
            ? string.Empty
            : $"<{string.Join(", ", type.GenericArguments.Select(DescribeType))}>";
        return $"{type.Kind}:{type.ManagedTypeName}:{type.RecordId}:{type.CppTypeName}{args}";
    }

    [Fact]
    public void Bootstrap_pipeline_preserves_alias_backed_value_type_parameters_and_emits_alias_metadata()
    {
        var repoRoot = FindRepositoryRoot();
        var fixtureDir = Path.Combine(repoRoot, "tests", "SharpCpp.Tests.Model", "Fixtures", "Vector3Alias");
        var headerPath = Path.Combine(fixtureDir, "Sample.hpp");
        var marshallerHeader = Path.Combine(fixtureDir, "SampleMarshallers.h");

        var parser = new ClangSharpHeaderParser();
        var clangArgs = new[] { "-x", "c++", "-std=c++20", $"-I{fixtureDir}" };
        var marshallerFiles = new[] { marshallerHeader };

        var compilation = parser.Parse(new ClangSharpParseRequest(
            ProjectName: "TestProject",
            ManagedNamespace: "Urho3D",
            ManagedClassName: "Bindings",
            NativeLibraryName: "Test.Native",
            HeaderFilePath: headerPath,
            HeaderIncludePath: fixtureDir,
            ClangArguments: clangArgs,
            MarshallerConfigurationFiles: marshallerFiles,
            EntryPointPrefix: "Test_"));

        BindingCompilationBootstrapPipeline.Apply(compilation, marshallerFiles, clangArgs);

        var actionBuilder = Assert.Single(compilation.Records.Where(static record => record.NativeName == "ActionBuilder"));
        var jumpBy = Assert.Single(actionBuilder.Members.OfType<FunctionBinding>().Where(static function => function.NativeName == "JumpBy"));
        Assert.False(jumpBy.IsIgnored(), $"Ignored because '{jumpBy.GetIgnoreReason()}'.");
        Assert.Equal("Vector3", jumpBy.Parameters[0].Type.ManagedTypeName);
        Assert.Equal("global::Urho3D.Math.Vector3", jumpBy.Parameters[0].Type.UnaliasedManagedTypeName);

        var alias = Assert.Single(compilation.TypeAliases.Where(static alias => alias.CppQualifiedName == "Urho3D::Vector3"));
        Assert.Equal("Vector3", alias.ManagedName);
        Assert.Equal("Urho3D", alias.ManagedNamespace);
        Assert.Equal("global::Urho3D.Math.Vector3", alias.TargetType.UnaliasedManagedTypeName);
    }

    [Fact]
    public void Parse_marks_constructor_expression_defaults_as_unexpressible()
    {
        var repoRoot = FindRepositoryRoot();
        var fixtureDir = Path.Combine(repoRoot, "tests", "SharpCpp.Tests.Model", "Fixtures", "Vector3Alias");
        var headerPath = Path.Combine(fixtureDir, "Sample.hpp");
        var marshallerHeader = Path.Combine(fixtureDir, "SampleMarshallers.h");

        var parser = new ClangSharpHeaderParser();
        var clangArgs = new[] { "-x", "c++", "-std=c++20", $"-I{fixtureDir}" };
        var compilation = parser.Parse(new ClangSharpParseRequest(
            ProjectName: "TestProject",
            ManagedNamespace: "Urho3D",
            ManagedClassName: "Bindings",
            NativeLibraryName: "Test.Native",
            HeaderFilePath: headerPath,
            HeaderIncludePath: fixtureDir,
            ClangArguments: clangArgs,
            MarshallerConfigurationFiles: [marshallerHeader],
            EntryPointPrefix: "Test_"));

        var actionBuilder = Assert.Single(compilation.Records.Where(static record => record.NativeName == "ActionBuilder"));
        var moveBy = Assert.Single(actionBuilder.Members.OfType<FunctionBinding>().Where(static function => function.NativeName == "MoveBy"));
        var offset = Assert.Single(moveBy.Parameters);
        Assert.Null(offset.ManagedDefaultValueExpression);
        Assert.True(offset.HasMetadata(MetadataKeys.UnexpressibleDefault));
    }

    [Fact]
    public void Bootstrap_pipeline_preserves_string_vector_alias_and_emits_alias_metadata()
    {
        var repoRoot = FindRepositoryRoot();
        var fixtureDir = Path.Combine(repoRoot, "tests", "SharpCpp.Tests.Model", "Fixtures", "IntVectorAlias");
        var headerPath = Path.Combine(fixtureDir, "Sample.hpp");
        var marshallerHeader = Path.Combine(fixtureDir, "SampleMarshallers.h");

        var parser = new ClangSharpHeaderParser();
        var clangArgs = new[] { "-x", "c++", "-std=c++20", $"-I{fixtureDir}" };
        var marshallerFiles = new[] { marshallerHeader };

        var compilation = parser.Parse(new ClangSharpParseRequest(
            ProjectName: "TestProject",
            ManagedNamespace: "Test",
            ManagedClassName: "Bindings",
            NativeLibraryName: "Test.Native",
            HeaderFilePath: headerPath,
            HeaderIncludePath: fixtureDir,
            ClangArguments: clangArgs,
            MarshallerConfigurationFiles: marshallerFiles,
            EntryPointPrefix: "Test_"));

        BindingCompilationBootstrapPipeline.Apply(compilation, marshallerFiles, clangArgs);

        // Verify the alias record exists with correct metadata.
        var alias = Assert.Single(compilation.TypeAliases.Where(static a => a.CppQualifiedName == "IntVector"));
        Assert.Equal("IntVector", alias.ManagedName);
        Assert.Equal("Test", alias.ManagedNamespace);

        // Target type should reference the record-backed vector type
        Assert.Contains("Vector", alias.TargetType.ManagedTypeName, StringComparison.Ordinal);

        // Target type must not contain C++ :: separators (would be pruned otherwise)
        Assert.DoesNotContain("::", alias.TargetType.ManagedTypeName.Replace("global::", "", StringComparison.Ordinal), StringComparison.Ordinal);

        // Verify the method is not ignored (container types with Marshaller metadata survive degradation)
        var consumer = Assert.Single(compilation.Records.Where(static record => record.NativeName == "IntVectorConsumer"));
        var accept = Assert.Single(consumer.Members.OfType<FunctionBinding>().Where(static function => function.NativeName == "Accept"));
        Assert.False(accept.IsIgnored(), $"Ignored because '{accept.GetIgnoreReason()}'.");
    }

    [Fact]
    public void Bootstrap_pipeline_fully_qualifies_value_aliases_inside_vector_cpp_signatures()
    {
        var repoRoot = FindRepositoryRoot();
        var fixtureDir = Path.Combine(repoRoot, "tests", "SharpCpp.Tests.Model", "Fixtures", "VectorOfValueAliasCollision");
        var headerPath = Path.Combine(fixtureDir, "Sample.hpp");
        var marshallerHeader = Path.Combine(fixtureDir, "SampleMarshallers.h");

        var parser = new ClangSharpHeaderParser();
        var clangArgs = new[] { "-x", "c++", "-std=c++20", $"-I{fixtureDir}" };
        var marshallerFiles = new[] { marshallerHeader };

        var compilation = parser.Parse(new ClangSharpParseRequest(
            ProjectName: "TestProject",
            ManagedNamespace: "Test.Namespace",
            ManagedClassName: "Bindings",
            NativeLibraryName: "Test.Native",
            HeaderFilePath: headerPath,
            HeaderIncludePath: fixtureDir,
            ClangArguments: clangArgs,
            MarshallerConfigurationFiles: marshallerFiles,
            EntryPointPrefix: "Test_"));

        BindingCompilationBootstrapPipeline.Apply(compilation, marshallerFiles, clangArgs);

        var hullBuilder = Assert.Single(compilation.Records.Where(static record => record.NativeName == "HullBuilder"));
        var getVertices = Assert.Single(hullBuilder.Members.OfType<FunctionBinding>().Where(static function => function.NativeName == "GetVertices"));
        var setVertices = Assert.Single(hullBuilder.Members.OfType<FunctionBinding>().Where(static function => function.NativeName == "SetVertices"));
        var stored = Assert.Single(hullBuilder.Members.OfType<FieldBinding>().Where(static field => field.NativeName == "stored_"));

        Assert.Equal("Ea.Vector<global::Urho3D.Math.Vector3>", getVertices.ReturnType.ManagedTypeName);
        Assert.Equal("Ea.Vector<global::Urho3D.Math.Vector3>", setVertices.Parameters[0].Type.ManagedTypeName);
        Assert.Equal("Ea.Vector<global::Urho3D.Math.Vector3>", stored.Type.ManagedTypeName);

        Assert.Equal("const eastl::vector<Urho3D::BaseVector3<float>>&", getVertices.CppReturnTypeName);
        Assert.Equal("const eastl::vector<Urho3D::BaseVector3<float>>&", setVertices.Parameters[0].CppTypeName);
        Assert.Equal("eastl::vector<Urho3D::BaseVector3<float>>", stored.CppTypeName);

        Assert.DoesNotContain("Rml::Vector3", getVertices.CppReturnTypeName, StringComparison.Ordinal);
        Assert.DoesNotContain("Rml::Vector3", setVertices.Parameters[0].CppTypeName, StringComparison.Ordinal);
        Assert.DoesNotContain("Rml::Vector3", stored.CppTypeName, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_maps_native_fixed_array_fields_to_native_array_wrapper()
    {
        var repoRoot = FindRepositoryRoot();
        var fixtureDir = Path.Combine(repoRoot, "tests", "SharpCpp.Tests.E2E", "Records");
        var includeDir = Path.Combine(repoRoot, "include");
        var headerPath = Path.Combine(fixtureDir, "Field.hpp");
        var marshallerHeader = Path.Combine(includeDir, "SharpCpp", "Marshaller.hpp");

        var parser = new ClangSharpHeaderParser();
        var clangArgs = CreateStandardLibraryClangArgs(fixtureDir, includeDir);

        var compilation = parser.Parse(new ClangSharpParseRequest(
            ProjectName: "TestProject",
            ManagedNamespace: "Test.Namespace",
            ManagedClassName: "Bindings",
            NativeLibraryName: "Test.Native",
            HeaderFilePath: headerPath,
            HeaderIncludePath: fixtureDir,
            ClangArguments: clangArgs,
            MarshallerConfigurationFiles: [marshallerHeader],
            EntryPointPrefix: "Test_"));

        BindingCompilationBootstrapPipeline.Apply(compilation, [marshallerHeader], clangArgs);

        var box = Assert.Single(compilation.Records.Where(static record => record.NativeName == "field_box"));
        var samples = Assert.Single(box.Members.OfType<FieldBinding>().Where(static field => field.NativeName == "samples"));

        Assert.False(samples.IsIgnored(), samples.GetIgnoreReason());
        Assert.True(samples.Type.IsFixedArray);
        Assert.Equal(3, samples.Type.FixedArrayLength);
        Assert.Equal("SharpCpp.NativeArray<int>", samples.Type.ManagedTypeName);
        Assert.Equal("int", samples.Type.FixedArrayElementType!.ManagedTypeName);
    }

    [Fact]
    public void Parse_maps_handle_backed_native_fixed_array_fields_to_native_array_wrapper()
    {
        var repoRoot = FindRepositoryRoot();
        var fixtureDir = Path.Combine(repoRoot, "tests", "SharpCpp.Tests.E2E", "Records");
        var includeDir = Path.Combine(repoRoot, "include");
        var headerPath = Path.Combine(fixtureDir, "Field.hpp");
        var marshallerHeader = Path.Combine(includeDir, "SharpCpp", "Marshaller.hpp");

        var parser = new ClangSharpHeaderParser();
        var clangArgs = CreateStandardLibraryClangArgs(fixtureDir, includeDir);

        var compilation = parser.Parse(new ClangSharpParseRequest(
            ProjectName: "TestProject",
            ManagedNamespace: "Test.Namespace",
            ManagedClassName: "Bindings",
            NativeLibraryName: "Test.Native",
            HeaderFilePath: headerPath,
            HeaderIncludePath: fixtureDir,
            ClangArguments: clangArgs,
            MarshallerConfigurationFiles: [marshallerHeader],
            EntryPointPrefix: "Test_"));

        BindingCompilationBootstrapPipeline.Apply(compilation, [marshallerHeader], clangArgs);

        var box = Assert.Single(compilation.Records.Where(static record => record.NativeName == "field_box"));
        var payloadSamples = Assert.Single(box.Members.OfType<FieldBinding>().Where(static field => field.NativeName == "payload_samples"));

        Assert.False(payloadSamples.IsIgnored(), payloadSamples.GetIgnoreReason());
        Assert.True(payloadSamples.Type.IsFixedArray);
        Assert.Equal(2, payloadSamples.Type.FixedArrayLength);
        Assert.Equal("SharpCpp.NativeArray<FieldPayload>", payloadSamples.Type.ManagedTypeName);
        Assert.Equal("FieldPayload", payloadSamples.Type.FixedArrayElementType!.ManagedTypeName);
    }

    [Fact]
    public void Parse_uses_qualified_record_symbol_ids_for_same_named_records()
    {
        var repoRoot = FindRepositoryRoot();
        var fixtureDir = Path.Combine(repoRoot, "tests", "SharpCpp.Tests.Model", "Fixtures", "QualifiedRecordIds");
        var headerPath = Path.Combine(fixtureDir, "Sample.hpp");

        var parser = new ClangSharpHeaderParser();
        var compilation = parser.Parse(new ClangSharpParseRequest(
            ProjectName: "TestProject",
            ManagedNamespace: "Test.Namespace",
            ManagedClassName: "Bindings",
            NativeLibraryName: "Test.Native",
            HeaderFilePath: headerPath,
            HeaderIncludePath: fixtureDir,
            ClangArguments: ["-x", "c++", "-std=c++20", $"-I{fixtureDir}"],
            MarshallerConfigurationFiles: Array.Empty<string>(),
            EntryPointPrefix: "Test_"));

        var recordDump = string.Join(", ", compilation.Records.Select(static record => $"{record.NativeName}|{record.CppQualifiedName}|{record.SymbolId}"));
        var alphaRecords = compilation.Records.Where(static record => record.CppQualifiedName == "Alpha::Context").ToArray();
        var betaRecords = compilation.Records.Where(static record => record.CppQualifiedName == "Beta::Context").ToArray();
        Assert.True(alphaRecords.Length == 1, recordDump);
        Assert.True(betaRecords.Length == 1, recordDump);
        var alpha = alphaRecords[0];
        var beta = betaRecords[0];

        Assert.Equal("record:Alpha::Context", alpha.SymbolId);
        Assert.Equal("record:Beta::Context", beta.SymbolId);
        Assert.NotEqual(alpha.SymbolId, beta.SymbolId);
        Assert.Same(alpha, compilation.FindRecordBySymbolId(alpha.SymbolId));
        Assert.Same(beta, compilation.FindRecordBySymbolId(beta.SymbolId));
    }

    [Fact]
    public void Parse_normalizes_all_caps_enum_values_to_managed_names()
    {
        var repoRoot = FindRepositoryRoot();
        var fixtureDir = Path.Combine(repoRoot, "tests", "SharpCpp.Tests.Model", "Fixtures", "EnumNames");
        var headerPath = Path.Combine(fixtureDir, "Sample.hpp");

        var parser = new ClangSharpHeaderParser();
        var compilation = parser.Parse(new ClangSharpParseRequest(
            ProjectName: "TestProject",
            ManagedNamespace: "Test.Namespace",
            ManagedClassName: "Bindings",
            NativeLibraryName: "Test.Native",
            HeaderFilePath: headerPath,
            HeaderIncludePath: fixtureDir,
            ClangArguments: ["-x", "c++", "-std=c++20", $"-I{fixtureDir}"],
            MarshallerConfigurationFiles: Array.Empty<string>(),
            EntryPointPrefix: "Test_"));

        var enumBinding = Assert.Single(compilation.Enums, static @enum => @enum.NativeName == "Intersection");
        Assert.Equal(["Outside", "Intersects", "Inside"], enumBinding.Values.Select(static value => value.ManagedName));
    }

    [Fact]
    public void Parser_collects_function_template_instantiations_from_ast_specializations()
    {
        var fixtureDir = Path.Combine(Path.GetTempPath(), "sharpcpp-template-instantiations-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixtureDir);
        try
        {
            var headerPath = Path.Combine(fixtureDir, "Sample.hpp");
            File.WriteAllText(headerPath,
                """
                namespace Test
                {
                struct Sample
                {
                    template <class T> void Set(T value) {}
                };

                template void Sample::Set<int>(int);
                }
                """);

            var parser = new ClangSharpHeaderParser();
            var compilation = parser.Parse(new ClangSharpParseRequest(
                ProjectName: "TestProject",
                ManagedNamespace: "Test.Namespace",
                ManagedClassName: "Bindings",
                NativeLibraryName: "Test.Native",
                HeaderFilePath: headerPath,
                HeaderIncludePath: "Sample.hpp",
                ClangArguments: ["-x", "c++", "-std=c++20", $"-I{fixtureDir}"],
                MarshallerConfigurationFiles: Array.Empty<string>(),
                EntryPointPrefix: "Test_"));

            BindingCompilationGenericInstantiationPass.Apply(compilation);

            var sample = Assert.Single(compilation.Records, static record => record.CppQualifiedName == "Test::Sample");
            var set = Assert.Single(sample.Members.OfType<FunctionBinding>(), static function => function.ManagedName == "Set");
            var instantiation = Assert.Single(set.SupportedGenericInstantiations);
            Assert.Equal(BindingPrimitiveKind.Int32, instantiation.TypeArguments[0].PrimitiveKind);
        }
        finally
        {
            Directory.Delete(fixtureDir, recursive: true);
        }
    }

    [Fact]
    public void Parser_maps_function_template_instantiation_arguments_from_decl_qualified_ast_type()
    {
        var fixtureDir = Path.Combine(Path.GetTempPath(), "sharpcpp-template-instantiation-qualified-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixtureDir);
        try
        {
            var headerPath = Path.Combine(fixtureDir, "Sample.hpp");
            File.WriteAllText(headerPath,
                """
                namespace Alpha
                {
                struct Payload {};
                }

                namespace Beta
                {
                struct Payload {};
                }

                namespace Alpha
                {
                struct Sample
                {
                    template <class T> void Set(T value) {}
                };

                template void Sample::Set<Payload>(Payload);
                }
                """);

            var parser = new ClangSharpHeaderParser();
            var compilation = parser.Parse(new ClangSharpParseRequest(
                ProjectName: "TestProject",
                ManagedNamespace: "Test.Namespace",
                ManagedClassName: "Bindings",
                NativeLibraryName: "Test.Native",
                HeaderFilePath: headerPath,
                HeaderIncludePath: "Sample.hpp",
                ClangArguments: ["-x", "c++", "-std=c++20", $"-I{fixtureDir}"],
                MarshallerConfigurationFiles: Array.Empty<string>(),
                EntryPointPrefix: "Test_"));

            BindingCompilationGenericInstantiationPass.Apply(compilation);

            var sample = Assert.Single(compilation.Records, static record => record.CppQualifiedName == "Alpha::Sample");
            var set = Assert.Single(sample.Members.OfType<FunctionBinding>(), static function => function.ManagedName == "Set");
            var instantiation = Assert.Single(set.SupportedGenericInstantiations);
            var argument = Assert.Single(instantiation.TypeArguments);

            Assert.Equal("record:Alpha::Payload", argument.RecordId);
            Assert.Equal("Alpha::Payload", argument.CppTypeName);
        }
        finally
        {
            Directory.Delete(fixtureDir, recursive: true);
        }
    }

    [Fact]
    public void Parse_maps_instance_method_handle_pointer_arrays_to_input_pointer_abi()
    {
        var fixtureDir = Path.Combine(Path.GetTempPath(), "sharpcpp-pointer-array-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixtureDir);
        try
        {
            var headerPath = Path.Combine(fixtureDir, "Sample.hpp");
            File.WriteAllText(headerPath,
                """
                namespace Sample
                {
                struct TextureView {};

                struct DeviceContext
                {
                    virtual void SetTargets(unsigned int count, TextureView* views[]) = 0;
                };
                }
                """);

            var parser = new ClangSharpHeaderParser();
            var compilation = parser.Parse(new ClangSharpParseRequest(
                ProjectName: "TestProject",
                ManagedNamespace: "Test.Namespace",
                ManagedClassName: "Bindings",
                NativeLibraryName: "Test.Native",
                HeaderFilePath: headerPath,
                HeaderIncludePath: "Sample.hpp",
                ClangArguments: ["-x", "c++", "-std=c++20", $"-I{fixtureDir}"],
                MarshallerConfigurationFiles: Array.Empty<string>(),
                EntryPointPrefix: "Test_"));

            var textureView = Assert.Single(compilation.Records, static record => record.CppQualifiedName == "Sample::TextureView");
            var deviceContext = Assert.Single(compilation.Records, static record => record.CppQualifiedName == "Sample::DeviceContext");
            var setTargets = Assert.Single(deviceContext.Members.OfType<FunctionBinding>(), static function => function.NativeName == "SetTargets");
            var views = Assert.Single(setTargets.Parameters, static parameter => parameter.NativeName == "views");

            Assert.False(setTargets.IsIgnored(), setTargets.GetIgnoreReason());
            Assert.Equal(textureView.SymbolId, views.Type.RecordId);
            Assert.Equal(BindingParameterDirection.In, views.Direction);
            Assert.Equal(BindingParameterAbiKind.Pointer, views.AbiKind);
            Assert.Equal("Sample::TextureView**", views.CppTypeName);
        }
        finally
        {
            Directory.Delete(fixtureDir, recursive: true);
        }
    }

    [Fact]
    public void Parse_keeps_c_opaque_handles_void_pointers_and_event_records()
    {
        var fixtureDir = Path.Combine(Path.GetTempPath(), "sharpcpp-c-opaque-handles-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixtureDir);
        try
        {
            var headerPath = Path.Combine(fixtureDir, "Sample.h");
            File.WriteAllText(headerPath,
                """
                typedef struct SDL_Window SDL_Window;
                typedef void* SDL_GLContext;

                typedef union SDL_Event
                {
                    unsigned int type;
                    unsigned char padding[56];
                } SDL_Event;

                SDL_Window* SDL_CreateWindow(int flags);
                void SDL_DestroyWindow(SDL_Window* window);
                SDL_GLContext SDL_GL_CreateContext(SDL_Window* window);
                void SDL_GL_DeleteContext(SDL_GLContext context);
                void* SDL_GL_GetCurrentContext(void);
                int SDL_PollEvent(SDL_Event* event);
                """);

            var parser = new ClangSharpHeaderParser();
            var compilation = parser.Parse(new ClangSharpParseRequest(
                ProjectName: "TestProject",
                ManagedNamespace: "Test.Namespace",
                ManagedClassName: "Bindings",
                NativeLibraryName: "Test.Native",
                HeaderFilePath: headerPath,
                HeaderIncludePath: "Sample.h",
                ClangArguments: ["-xc", "-std=c17", $"-I{fixtureDir}"],
                MarshallerConfigurationFiles: Array.Empty<string>(),
                EntryPointPrefix: "Test_"));

            DegradeUnsupportedSurfacePass.Apply(compilation);

            var window = Assert.Single(compilation.Records, static record => record.CppQualifiedName == "SDL_Window");
            Assert.True(window.GetMetadata<bool>(MetadataKeys.ForwardDeclaration));
            Assert.True(window.GetMetadata<bool>(MetadataKeys.OpaqueNativeHandle));
            Assert.True(window.GetMetadata<bool>(MetadataKeys.NonPublicDestructor));
            Assert.False(window.IsIgnored(), window.GetIgnoreReason());

            var createWindow = Assert.Single(compilation.Functions, static function => function.NativeName == "SDL_CreateWindow");
            Assert.Equal(window.SymbolId, createWindow.ReturnType.RecordId);
            Assert.False(createWindow.IsIgnored(), createWindow.GetIgnoreReason());

            var destroyWindow = Assert.Single(compilation.Functions, static function => function.NativeName == "SDL_DestroyWindow");
            Assert.Equal(BindingParameterDirection.In, destroyWindow.Parameters[0].Direction);
            Assert.Equal(BindingParameterAbiKind.Value, destroyWindow.Parameters[0].AbiKind);
            Assert.Equal(window.SymbolId, destroyWindow.Parameters[0].Type.RecordId);
            Assert.False(destroyWindow.IsIgnored(), destroyWindow.GetIgnoreReason());

            var createContext = Assert.Single(compilation.Functions, static function => function.NativeName == "SDL_GL_CreateContext");
            Assert.Equal("SDL_GLContext", createContext.CppReturnTypeName);
            Assert.Equal(BindingPrimitiveKind.IntPtr, createContext.ReturnType.PrimitiveKind);
            Assert.False(createContext.IsIgnored(), createContext.GetIgnoreReason());

            var deleteContext = Assert.Single(compilation.Functions, static function => function.NativeName == "SDL_GL_DeleteContext");
            Assert.Equal("SDL_GLContext", deleteContext.Parameters[0].CppTypeName);
            Assert.Equal(BindingPrimitiveKind.IntPtr, deleteContext.Parameters[0].Type.PrimitiveKind);
            Assert.False(deleteContext.IsIgnored(), deleteContext.GetIgnoreReason());

            var currentContext = Assert.Single(compilation.Functions, static function => function.NativeName == "SDL_GL_GetCurrentContext");
            Assert.Equal("void*", currentContext.CppReturnTypeName);
            Assert.Equal(BindingPrimitiveKind.IntPtr, currentContext.ReturnType.PrimitiveKind);
            Assert.False(currentContext.IsIgnored(), currentContext.GetIgnoreReason());

            var sdlEvent = Assert.Single(compilation.Records, static record => record.CppQualifiedName == "SDL_Event");
            Assert.False(sdlEvent.IsIgnored(), sdlEvent.GetIgnoreReason());
            Assert.Contains(sdlEvent.Members.OfType<FunctionBinding>(), static member => member.IsConstructor && member.Parameters.Count == 0);

            var pollEvent = Assert.Single(compilation.Functions, static function => function.NativeName == "SDL_PollEvent");
            Assert.Equal(BindingParameterDirection.In, pollEvent.Parameters[0].Direction);
            Assert.Equal(BindingParameterAbiKind.Value, pollEvent.Parameters[0].AbiKind);
            Assert.Equal(sdlEvent.SymbolId, pollEvent.Parameters[0].Type.RecordId);
            Assert.False(pollEvent.IsIgnored(), pollEvent.GetIgnoreReason());
        }
        finally
        {
            Directory.Delete(fixtureDir, recursive: true);
        }
    }

    [Fact]
    public void Parse_treats_macro_generated_native_interfaces_as_definitions()
    {
        var fixtureDir = Path.Combine(Path.GetTempPath(), "sharpcpp-native-interface-macro-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixtureDir);
        try
        {
            var headerPath = Path.Combine(fixtureDir, "Sample.hpp");
            File.WriteAllText(headerPath,
                """
                #define BEGIN_INTERFACE(NAME) struct NAME {
                #define END_INTERFACE };

                BEGIN_INTERFACE(IMacroCounter)
                    virtual int GetValue() = 0;
                END_INTERFACE

                struct [[sharpcpp::native_interface]] IDirectCounter
                {
                    virtual int GetValue() = 0;
                };
                """);

            var parser = new ClangSharpHeaderParser();
            var compilation = parser.Parse(new ClangSharpParseRequest(
                ProjectName: "TestProject",
                ManagedNamespace: "Test.Namespace",
                ManagedClassName: "Bindings",
                NativeLibraryName: "Test.Native",
                HeaderFilePath: headerPath,
                HeaderIncludePath: "Sample.hpp",
                ClangArguments: ["-x", "c++", "-std=c++20", "-Wno-unknown-attributes", $"-I{fixtureDir}"],
                MarshallerConfigurationFiles: Array.Empty<string>(),
                EntryPointPrefix: "Test_"));

            var counter = Assert.Single(compilation.Records, static record => record.CppQualifiedName == "IMacroCounter");
            Assert.False(counter.GetMetadata<bool>(MetadataKeys.ForwardDeclaration));

            var method = Assert.Single(counter.Members.OfType<FunctionBinding>(), static member => member.NativeName == "GetValue");
            Assert.True(method.IsVirtual);
            Assert.True(method.IsPureVirtual);
            Assert.False(method.IsIgnored(), method.GetIgnoreReason());

            var directCounter = Assert.Single(compilation.Records, static record => record.CppQualifiedName == "IDirectCounter");
            Assert.True(directCounter.GetMetadata<bool>(MetadataKeys.NativeInterfaceWrapper));
        }
        finally
        {
            Directory.Delete(fixtureDir, recursive: true);
        }
    }

    private static string FindRepositoryRoot([CallerFilePath] string sourceFilePath = "")
    {
        var startPath = string.IsNullOrWhiteSpace(sourceFilePath)
            ? AppContext.BaseDirectory
            : Path.GetDirectoryName(sourceFilePath)!;
        var directory = new DirectoryInfo(startPath);
        while (directory is not null)
        {
            var hasIncludeDir = Directory.Exists(Path.Combine(directory.FullName, "include"));
            var hasModelTestsDir = Directory.Exists(Path.Combine(directory.FullName, "tests", "SharpCpp.Tests.Model"));
            if (hasIncludeDir &&
                (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")) || hasModelTestsDir))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Unable to locate repository root.");
    }

    private static string[] CreateStandardLibraryClangArgs(params string[] includeDirs)
    {
        var args = new List<string> { "-x", "c++", "-std=c++20" };
        args.AddRange(includeDirs.Select(static includeDir => $"-I{includeDir}"));

        args.AddRange(EnumerateSystemIncludeDirs().Select(static includeDir => $"-I{includeDir}"));
        return args.ToArray();
    }

    private static IEnumerable<string> EnumerateSystemIncludeDirs()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        if (Directory.Exists("/usr/include/c++"))
        {
            foreach (var versionDir in Directory.GetDirectories("/usr/include/c++").OrderByDescending(static path => path, StringComparer.Ordinal))
            {
                foreach (var candidate in new[]
                {
                    versionDir,
                    Path.Combine(versionDir, "x86_64-pc-linux-gnu"),
                    Path.Combine(versionDir, "backward")
                })
                {
                    if (Directory.Exists(candidate) && seen.Add(candidate))
                    {
                        yield return candidate;
                    }
                }
            }
        }

        if (Directory.Exists("/usr/lib/gcc/x86_64-pc-linux-gnu"))
        {
            foreach (var versionDir in Directory.GetDirectories("/usr/lib/gcc/x86_64-pc-linux-gnu").OrderByDescending(static path => path, StringComparer.Ordinal))
            {
                var includeDir = Path.Combine(versionDir, "include");
                if (Directory.Exists(includeDir) && seen.Add(includeDir))
                {
                    yield return includeDir;
                }
            }
        }

        if (Directory.Exists("/usr/lib/clang"))
        {
            foreach (var versionDir in Directory.GetDirectories("/usr/lib/clang").OrderByDescending(static path => path, StringComparer.Ordinal))
            {
                var includeDir = Path.Combine(versionDir, "include");
                if (Directory.Exists(includeDir) && seen.Add(includeDir))
                {
                    yield return includeDir;
                }
            }
        }

        if (Directory.Exists("/usr/include") && seen.Add("/usr/include"))
        {
            yield return "/usr/include";
        }
    }
}
