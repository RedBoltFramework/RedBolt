using SharpCpp.Parsing;

using Xunit;

namespace SharpCpp.Tests.Model;

public sealed class SharpCppAttributeScannerTests
{
    [Theory]
    [InlineData("[[ sharpcpp :: rename ( \"X\" ) ]]", "rename", " \"X\" ")]
    [InlineData("[ [ sharpcpp :: rename ( \"X\" ) ] ]", "rename", " \"X\" ")]
    [InlineData("[[sharpcpp::flags]]", "flags", "")]
    public void Scan_handles_token_spacing_and_bare_attributes(string text, string expectedName, string expectedBody)
    {
        var match = Assert.Single(SharpCppAttributeScanner.Scan(text));

        Assert.Equal(expectedName, match.Name);
        Assert.Equal(expectedBody, match.Body);
    }

    [Fact]
    public void Scan_handles_multiline_stacked_and_escaped_quotes()
    {
        var text = """
            [[sharpcpp::ignore("Line 1\\nLine 2")]]
            [[sharpcpp::rename(
                "A\\\"B"
            )]][[sharpcpp::instantiate(std::vector<Thing>, Foo)]]
            """;

        var matches = SharpCppAttributeScanner.Scan(text);

        Assert.Collection(matches,
            match =>
            {
                Assert.Equal("ignore", match.Name);
                Assert.Contains("Line 1", match.Body);
                Assert.Contains("Line 2", match.Body);
            },
            match =>
            {
                Assert.Equal("rename", match.Name);
                Assert.Contains("A", match.Body);
                Assert.Contains("B", match.Body);
            },
            match =>
            {
                Assert.Equal("instantiate", match.Name);
                Assert.Equal("std::vector<Thing>, Foo", match.Body.Trim());
            });
    }

    [Theory]
    [InlineData("[[sharpcpp::ignore]]", "")]
    [InlineData("[[sharpcpp::ignore(\"manual stub\")]]", "\"manual stub\"")]
    [InlineData("[ [ sharpcpp :: ignore ( \"manual stub\" ) ] ]", "\"manual stub\"")]
    public void Scan_keeps_raw_ignore_body_for_normalization(string text, string expectedBody)
    {
        var match = Assert.Single(SharpCppAttributeScanner.Scan(text));

        Assert.Equal("ignore", match.Name);
        Assert.Equal(expectedBody, match.Body.Trim());
    }
}
