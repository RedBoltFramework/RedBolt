using RedBolt;

namespace RedBolt.BindingSmoke.Tests;

public sealed class ResourceRefTests
{
    [Fact]
    public void Parse_and_hash_work()
    {
        var materialRef = ResourceRef.Parse("Material;Materials/FirstCube");
        Assert.Equal(StringHash.Calculate("Material"), materialRef.Type);
        Assert.Equal("Materials/FirstCube", materialRef.Name);
        Assert.NotEqual(0u, materialRef.ToHash());
        Assert.False(ResourceRef.IsNullOrEmpty(materialRef));
    }

    [Fact]
    public void ResourceRefList_parse_equality_and_hash_work()
    {
        var list = ResourceRefList.Parse("Material;Materials/One,Materials/Two");
        var expected = new ResourceRefList(StringHash.Calculate("Material"), ["Materials/One", "Materials/Two"]);
        Assert.True(list.Equals(expected));
        Assert.NotEqual(0u, list.ToHash());
        Assert.False(ResourceRefList.IsNullOrEmpty(list));
        Assert.True(ResourceRefList.IsNullOrEmpty(ResourceRefList.Empty));
    }
}
