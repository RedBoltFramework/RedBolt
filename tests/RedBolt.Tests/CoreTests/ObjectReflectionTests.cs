using RedBolt;

namespace RedBolt.Tests;

public sealed class ObjectReflectionTests
{
    [Fact]
    public void Provides_fallback_metadata()
    {
        Assert.Equal("RedBolt.StaticModel", ObjectReflection<StaticModel>.ClassName);
        Assert.Equal(StringHash.Calculate("RedBolt.StaticModel"), ObjectReflection<StaticModel>.TypeId.Value);
    }

    [Fact]
    public void Formatted_type_name_is_csharp_style()
    {
        Assert.Equal("Dictionary<String, List<Int32>>",
            typeof(Dictionary<string, List<int>>).GetFormattedTypeName());
    }
}
