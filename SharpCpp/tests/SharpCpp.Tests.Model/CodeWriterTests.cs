using SharpCpp.Model.Utilities;

using Xunit;

namespace SharpCpp.Tests.Model;

public sealed class CodeWriterTests
{
    [Fact]
    public void Write_normalizes_brace_driven_indentation()
    {
        var writer = new CodeWriter();

        writer.Write(
            """
            namespace Sample
            {
            public static class MathHelpers
            {
            public static int Add(int lhs, int rhs)
            {
            return lhs + rhs;
            }
            }
            }
            """);

        Assert.Equal(
            """
            namespace Sample
            {
                public static class MathHelpers
                {
                    public static int Add(int lhs, int rhs)
                    {
                        return lhs + rhs;
                    }
                }
            }

            """,
            writer.ToString().Replace("\r\n", "\n", StringComparison.Ordinal));
    }
}
