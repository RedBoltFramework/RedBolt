using SharpCpp.Model.Ast;
using SharpCpp.Parsing;

using Xunit;

namespace SharpCpp.Tests.Model;

public sealed class ClangSharpHeaderParserFlattenedFieldTests
{
    [Fact]
    public void Parse_flattens_anonymous_record_fields_with_native_access_paths()
    {
        var compilation = ParseTempHeader(
            "Sample.h",
            """
            typedef struct SdlSysWMinfo
            {
                int version;
                union
                {
                    struct
                    {
                        void* display;
                        unsigned long window;
                    } x11;
                } info;
            } SdlSysWMinfo;
            """,
            ["-xc", "-std=c17"]);

        var record = Assert.Single(compilation.Records, static record => record.CppQualifiedName == "SdlSysWMinfo");
        var display = Assert.Single(record.Members.OfType<FieldBinding>(), static field => field.NativeName == "info_x11_display");
        var window = Assert.Single(record.Members.OfType<FieldBinding>(), static field => field.NativeName == "info_x11_window");

        Assert.Equal("InfoX11Display", display.ManagedName);
        Assert.Equal("info.x11.display", display.GetMetadata<string>(MetadataKeys.NativeFieldAccessExpression));
        Assert.Equal("void*", display.CppTypeName);
        Assert.Equal(BindingPrimitiveKind.IntPtr, display.Type.PrimitiveKind);
        Assert.Equal("InfoX11Window", window.ManagedName);
        Assert.Equal("info.x11.window", window.GetMetadata<string>(MetadataKeys.NativeFieldAccessExpression));
    }

    [Fact]
    public void Parse_composes_nested_flattened_access_paths_from_named_record_values()
    {
        var compilation = ParseTempHeader(
            "Sample.hpp",
            """
            namespace Test
            {
            struct StencilDesc
            {
                int StencilFailOp;
            };

            struct DepthStencilDesc
            {
                StencilDesc BackFace;
            };

            struct GraphicsPipelineDesc
            {
                DepthStencilDesc DepthStencilDesc;
            };

            struct PipelineStateDesc
            {
                GraphicsPipelineDesc GraphicsPipeline;
            };
            }
            """,
            ["-x", "c++", "-std=c++20"]);

        var graphicsPipeline = Assert.Single(compilation.Records, static record => record.CppQualifiedName == "Test::GraphicsPipelineDesc");
        var depthStencil = Assert.Single(graphicsPipeline.Members.OfType<FieldBinding>(), static field => field.NativeName == "DepthStencilDesc_BackFace_StencilFailOp");
        Assert.Equal("DepthStencilDesc.BackFace.StencilFailOp", depthStencil.GetMetadata<string>(MetadataKeys.NativeFieldAccessExpression));

        var pipelineState = Assert.Single(compilation.Records, static record => record.CppQualifiedName == "Test::PipelineStateDesc");
        var field = Assert.Single(pipelineState.Members.OfType<FieldBinding>(), static field => field.NativeName == "GraphicsPipeline_DepthStencilDesc_BackFace_StencilFailOp");

        Assert.Equal("GraphicsPipelineDepthStencilDescBackFaceStencilFailOp", field.ManagedName);
        Assert.Equal("GraphicsPipeline.DepthStencilDesc.BackFace.StencilFailOp", field.GetMetadata<string>(MetadataKeys.NativeFieldAccessExpression));
    }

    [Fact]
    public void Parse_preserves_protected_access_from_flattened_root_field()
    {
        var compilation = ParseTempHeader(
            "ProtectedFlattened.hpp",
            """
            class [[sharpcpp::director]] protected_container
            {
            public:
                virtual int get_value() const { return data.value; }

            protected:
                struct nested_value
                {
                    int value;
                } data;
            };
            """,
            ["-x", "c++", "-std=c++20"]);

        var record = Assert.Single(compilation.Records, static record => record.CppQualifiedName == "protected_container");
        var field = Assert.Single(record.Members.OfType<FieldBinding>(), static field => field.NativeName == "data_value");

        Assert.Equal(BindingAccessModifier.Protected, field.AccessModifier);
        Assert.Equal("data.value", field.GetMetadata<string>(MetadataKeys.NativeFieldAccessExpression));
    }

    [Fact]
    public void Parse_does_not_synthesize_public_constructor_over_explicit_protected_constructor()
    {
        var compilation = ParseTempHeader(
            "ProtectedConstructor.hpp",
            """
            struct protected_constructor
            {
                int value;

            protected:
                protected_constructor() = default;
            };
            """,
            ["-x", "c++", "-std=c++20"]);

        var record = Assert.Single(compilation.Records, static record => record.CppQualifiedName == "protected_constructor");

        Assert.DoesNotContain(record.Members.OfType<FunctionBinding>(), static function => function.IsConstructor);
    }

    [Fact]
    public void Parse_preserves_token_boundaries_in_lambda_native_defaults()
    {
        var compilation = ParseTempHeader(
            "LambdaDefault.hpp",
            """
            struct Time
            {
                static unsigned GetSystemTime();
            };

            using callback = unsigned (*)();
            void use_callback(callback value = [] { return Time::GetSystemTime(); });
            """,
            ["-x", "c++", "-std=c++20"]);

        var function = Assert.Single(compilation.Members.OfType<FunctionBinding>(), static function => function.NativeName == "use_callback");
        var parameter = Assert.Single(function.Parameters);
        var nativeDefault = parameter.GetMetadata<string>(MetadataKeys.NativeDefaultValueExpression);

        Assert.Contains("return Time :: GetSystemTime", nativeDefault, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_normalizes_spaced_enum_default_expressions_to_managed_names()
    {
        var compilation = ParseTempHeader(
            "EnumDefault.hpp",
            """
            enum class default_mode
            {
                active
            };

            void use_mode(default_mode mode = default_mode::active);
            """,
            ["-x", "c++", "-std=c++20"]);

        var function = Assert.Single(compilation.Members.OfType<FunctionBinding>(), static function => function.NativeName == "use_mode");
        var parameter = Assert.Single(function.Parameters);

        Assert.Equal("DefaultMode.Active", parameter.ManagedDefaultValueExpression);
    }

    private static BindingCompilation ParseTempHeader(string fileName, string source, string[] clangArgs)
    {
        var fixtureDir = Path.Combine(Path.GetTempPath(), "sharpcpp-flattened-fields-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixtureDir);
        try
        {
            var headerPath = Path.Combine(fixtureDir, fileName);
            File.WriteAllText(headerPath, source);

            var parser = new ClangSharpHeaderParser();
            return parser.Parse(new ClangSharpParseRequest(
                ProjectName: "TestProject",
                ManagedNamespace: "Test.Namespace",
                ManagedClassName: "Bindings",
                NativeLibraryName: "Test.Native",
                HeaderFilePath: headerPath,
                HeaderIncludePath: fileName,
                ClangArguments: [.. clangArgs, $"-I{fixtureDir}"],
                MarshallerConfigurationFiles: Array.Empty<string>(),
                EntryPointPrefix: "Test_"));
        }
        finally
        {
            Directory.Delete(fixtureDir, recursive: true);
        }
    }
}
