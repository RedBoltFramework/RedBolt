using SharpCpp.Model.Ast;
using SharpCpp.Parsing;
using System.Runtime.CompilerServices;

using Xunit;

namespace SharpCpp.Tests.Model;

public sealed class ClangSharpHeaderParserIgnoreTests
{
    [Fact]
    public void Parse_normalizes_ignore_reasons_from_attributes_and_prescan()
    {
        var repoRoot = FindRepositoryRoot();
        var fixtureDir = Path.Combine(repoRoot, "tests", "SharpCpp.Tests.Model", "Fixtures", "IgnoreReasons");
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

        var functions = compilation.Functions.OrderBy(static function => function.NativeName).ToArray();
        Assert.Equal(3, functions.Length);
        Assert.Equal("marked [[sharpcpp::ignore]]", functions[0].GetMetadata<string>(MetadataKeys.IgnoreReason));
        Assert.Equal("manual stub", functions[1].GetMetadata<string>(MetadataKeys.IgnoreReason));
        Assert.Equal("prescan only", functions[2].GetMetadata<string>(MetadataKeys.IgnoreReason));
        Assert.All(functions, static function => Assert.True(function.IsSilentlyExcluded()));
    }

    [Fact]
    public void Parse_marks_deleted_callables_as_silent_exclusions()
    {
        var repoRoot = FindRepositoryRoot();
        var fixtureDir = Path.Combine(repoRoot, "tests", "SharpCpp.Tests.Model", "Fixtures", "DeletedCallables");
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

        var record = Assert.Single(compilation.Records, static record => record.NativeName == "deleted_box");
        var constructors = record.Members.OfType<FunctionBinding>().Where(static function => function.IsConstructor).ToArray();
        var defaultConstructor = Assert.Single(constructors, static function => function.Parameters.Count == 0);
        var deletedConstructor = Assert.Single(constructors, static function => function.Parameters.Count == 1);
        var deletedMethod = Assert.Single(record.Members.OfType<FunctionBinding>(), static function => function.NativeName == "deleted_method");
        var deletedOperator = Assert.Single(record.Members.OfType<FunctionBinding>(), static function => function.NativeName == "operator=");

        Assert.False(defaultConstructor.IsIgnored());
        Assert.False(defaultConstructor.IsSilentlyExcluded());
        Assert.True(deletedConstructor.IsSilentlyExcluded());
        Assert.True(deletedMethod.IsSilentlyExcluded());
        Assert.True(deletedOperator.IsSilentlyExcluded());
    }

    [Fact]
    public void Parse_forced_director_records_preserves_abstract_constructors()
    {
        var repoRoot = FindRepositoryRoot();
        var fixtureDir = Path.Combine(repoRoot, "tests", "SharpCpp.Tests.Model", "Fixtures", "ForcedDirectors");
        var headerPath = Path.Combine(fixtureDir, "Sample.hpp");

        var parser = new ClangSharpHeaderParser();
        var defaultCompilation = parser.Parse(new ClangSharpParseRequest(
            ProjectName: "TestProject",
            ManagedNamespace: "Test.Namespace",
            ManagedClassName: "Bindings",
            NativeLibraryName: "Test.Native",
            HeaderFilePath: headerPath,
            HeaderIncludePath: fixtureDir,
            ClangArguments: ["-x", "c++", "-std=c++20", $"-I{fixtureDir}"],
            MarshallerConfigurationFiles: Array.Empty<string>(),
            EntryPointPrefix: "Test_"));
        var defaultRecord = Assert.Single(defaultCompilation.Records, static record => record.CppQualifiedName == "Test::ForcedAbstract");
        var defaultConstructor = Assert.Single(defaultRecord.Members.OfType<FunctionBinding>(), static function => function.IsConstructor);

        var forcedCompilation = parser.Parse(new ClangSharpParseRequest(
            ProjectName: "TestProject",
            ManagedNamespace: "Test.Namespace",
            ManagedClassName: "Bindings",
            NativeLibraryName: "Test.Native",
            HeaderFilePath: headerPath,
            HeaderIncludePath: fixtureDir,
            ClangArguments: ["-x", "c++", "-std=c++20", $"-I{fixtureDir}"],
            MarshallerConfigurationFiles: Array.Empty<string>(),
            EntryPointPrefix: "Test_",
            DirectorRecordCppTypeNames: new HashSet<string>(StringComparer.Ordinal) { "Test::ForcedAbstract" }));
        var forcedRecord = Assert.Single(forcedCompilation.Records, static record => record.CppQualifiedName == "Test::ForcedAbstract");
        var forcedConstructor = Assert.Single(forcedRecord.Members.OfType<FunctionBinding>(), static function => function.IsConstructor);

        Assert.True(defaultRecord.IsAbstract);
        Assert.False(defaultRecord.IsDirector);
        Assert.True(defaultConstructor.IsIgnored());
        Assert.True(forcedRecord.IsAbstract);
        Assert.True(forcedRecord.IsDirector);
        Assert.False(forcedConstructor.IsIgnored(), forcedConstructor.GetIgnoreReason());
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
}
