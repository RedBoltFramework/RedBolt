using Xunit;

namespace SharpCpp.Tests.E2E.Ignore;

public sealed class IgnoreTests
{
    [Fact]
    public void Ignored_record_is_omitted_from_generated_output()
    {
        var content = ReadAllGeneratedCSharpContent();

        Assert.DoesNotContain("excluded: marked [[sharpcpp::ignore]]", content, StringComparison.Ordinal);
        Assert.DoesNotContain("skipped_record", content, StringComparison.Ordinal);
        Assert.DoesNotContain("public partial class SkippedRecord", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Ignored_free_function_is_omitted_from_generated_output()
    {
        var content = ReadAllGeneratedCSharpContent();

        Assert.DoesNotContain("excluded: manual stub", content, StringComparison.Ordinal);
        Assert.DoesNotContain("skipped_free_function", content, StringComparison.Ordinal);
        Assert.DoesNotContain("\n    public static int SkippedFreeFunction(", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Ignored_enum_is_omitted_from_generated_output()
    {
        var content = ReadAllGeneratedCSharpContent();

        Assert.DoesNotContain("skipped_enum", content, StringComparison.Ordinal);
        Assert.DoesNotContain("public enum SkippedEnum", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Macro_backed_ignored_declarations_are_omitted()
    {
        var content = ReadAllGeneratedCSharpContent();

        Assert.DoesNotContain("macro_generated_macro_record", content, StringComparison.Ordinal);
        Assert.DoesNotContain("macro_generated_macro_enum", content, StringComparison.Ordinal);
        Assert.DoesNotContain("SHARPCPP_IGNORED_PAIR", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Ignored_record_with_reason_is_omitted()
    {
        var content = ReadAllGeneratedCSharpContent();

        Assert.DoesNotContain("skipped_record_with_reason", content, StringComparison.Ordinal);
        Assert.DoesNotContain("excluded: not yet supported", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Ignored_members_within_record_are_omitted()
    {
        var content = ReadAllGeneratedCSharpContent();

        Assert.DoesNotContain("internal_method", content, StringComparison.Ordinal);
        Assert.DoesNotContain("skipped_field", content, StringComparison.Ordinal);
        Assert.DoesNotContain("skipped_static", content, StringComparison.Ordinal);
        Assert.DoesNotContain("debug_value", content, StringComparison.Ordinal);
        Assert.DoesNotContain("operator+", content, StringComparison.Ordinal);
        Assert.Contains("VisibleField", content, StringComparison.Ordinal);
    }

    private static string ReadAllGeneratedCSharpContent()
    {
        var generatedDir = GetGeneratedDir();
        var files = Directory.GetFiles(generatedDir, "*.g.cs", SearchOption.TopDirectoryOnly);
        return string.Join("\n", files.Select(File.ReadAllText));
    }

    private static string GetGeneratedDir()
    {
        var dir = Environment.GetEnvironmentVariable("SharpCppGeneratedDir");
        if (string.IsNullOrWhiteSpace(dir))
        {
            dir = Path.Combine(
                Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? ".",
                "..", "..", "..", "..", "..",
                "build", "native", "dev", "generated", "sharpcpp-e2e");
        }

        return Path.GetFullPath(dir);
    }
}
