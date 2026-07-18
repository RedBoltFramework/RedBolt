using SharpCpp.Tests.E2E.Generated;

using Xunit;

namespace SharpCpp.Tests.E2E.Records;

public sealed class ClassDeclTests
{
    [Fact]
    public void Plain_cpp_class_declaration_parses_and_wraps_like_struct_records()
    {
        using var box = new ClassDeclBox(42);

        Assert.Equal(42, box.GetValue());
    }
}
