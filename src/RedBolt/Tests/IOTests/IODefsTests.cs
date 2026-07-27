namespace RedBolt.Tests;

public sealed class IODefsTests
{
    [Fact]
    public void Vector_binary_encoding_matches_cpp_enum_values()
    {
        Assert.Equal(typeof(int), Enum.GetUnderlyingType(typeof(VectorBinaryEncoding)));
        Assert.Equal(0, (int)VectorBinaryEncoding.Float);
        Assert.Equal(1, (int)VectorBinaryEncoding.Double);
        Assert.Equal(2, (int)VectorBinaryEncoding.Int32);
        Assert.Equal(3, (int)VectorBinaryEncoding.Int16);
        Assert.Equal(4, (int)VectorBinaryEncoding.Count);
    }
}
