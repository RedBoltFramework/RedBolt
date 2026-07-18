using Xunit;

namespace SharpCpp.Tests.E2E.Doc;

public sealed class DocumentationTests
{
    [Fact]
    public void Generated_public_api_includes_xml_docs_for_implemented_shapes()
    {
        var generatedDir = GetGeneratedDir();
        var files = Directory.GetFiles(generatedDir, "*.g.cs", SearchOption.TopDirectoryOnly);
        var generatedSource = string.Join("\n", files.Select(File.ReadAllText));

        Assert.Contains("/// <summary>Represents a documented record with a brief description.</summary>", generatedSource, StringComparison.Ordinal);
        Assert.Contains("/// <summary>Gets the stored value.</summary>", generatedSource, StringComparison.Ordinal);
        Assert.Contains("/// <summary>The current value stored in the record.</summary>", generatedSource, StringComparison.Ordinal);
        Assert.Contains("/// <summary>A free function with a block-style documentation comment.</summary>", generatedSource, StringComparison.Ordinal);
        Assert.Contains("/// <summary>Enumeration of status values for document processing.</summary>", generatedSource, StringComparison.Ordinal);
        Assert.Contains("/// <summary>Escapes XML-sensitive characters: &lt;tag&gt;value &amp; &quot;other&quot;</summary>", generatedSource, StringComparison.Ordinal);
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
