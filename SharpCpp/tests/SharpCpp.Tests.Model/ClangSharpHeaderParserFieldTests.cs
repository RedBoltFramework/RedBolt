using SharpCpp.Model.Ast;
using SharpCpp.Parsing;

using Xunit;

namespace SharpCpp.Tests.Model;

public sealed class ClangSharpHeaderParserFieldTests
{
    [Fact]
    public void Parse_maps_typedef_char_pointer_fields_to_intptr_without_ignoring_them()
    {
        var compilation = ParseTempHeader(
            "Sample.hpp",
            """
            namespace Test
            {
            typedef char Char;

            struct ShaderCreateInfo
            {
                const Char* Source = nullptr;
                const Char* EntryPoint = nullptr;
            };
            }
            """,
            ["-x", "c++", "-std=c++20"]);

        var record = Assert.Single(compilation.Records, static record => record.CppQualifiedName == "Test::ShaderCreateInfo");
        var source = Assert.Single(record.Members.OfType<FieldBinding>(), static field => field.NativeName == "Source");
        var entryPoint = Assert.Single(record.Members.OfType<FieldBinding>(), static field => field.NativeName == "EntryPoint");

        AssertCharPointerField(source);
        AssertCharPointerField(entryPoint);
    }

    private static void AssertCharPointerField(FieldBinding field)
    {
        Assert.False(field.IsIgnored(), field.GetIgnoreReason());
        Assert.True(field.IsPointer);
        Assert.Equal(BindingPrimitiveKind.IntPtr, field.Type.PrimitiveKind);
        Assert.Equal("global::System.IntPtr", field.Type.ManagedTypeName);
        Assert.Contains("*", field.CppTypeName, StringComparison.Ordinal);
    }

    private static BindingCompilation ParseTempHeader(string fileName, string source, string[] clangArgs)
    {
        var fixtureDir = Path.Combine(Path.GetTempPath(), "sharpcpp-fields-" + Guid.NewGuid().ToString("N"));
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
