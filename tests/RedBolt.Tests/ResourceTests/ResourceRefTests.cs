using RedBolt;

namespace RedBolt.Tests;

public sealed class ResourceRefTests
{
    [Fact]
    public void Parse_and_hash_work()
    {
        var materialRef = ResourceRef.Parse("Material;Materials/FirstCube");
        Assert.Equal(StringHash.Calculate("Material"), materialRef.Type);
        Assert.Equal("Materials/FirstCube", materialRef.Name);
        Assert.Equal("Material;Materials/FirstCube", materialRef.ToString());
        Assert.NotEqual(0u, materialRef.ToHash());
        Assert.False(ResourceRef.IsNullOrEmpty(materialRef));
    }

    [Fact]
    public void ResourceRefList_parse_equality_and_hash_work()
    {
        var list = ResourceRefList.Parse("Material;Materials/One;Materials/Two");
        var expected = new ResourceRefList(StringHash.Calculate("Material"), ["Materials/One", "Materials/Two"]);
        Assert.True(list.Equals(expected));
        Assert.Equal("Material;Materials/One;Materials/Two", list.ToString());
        Assert.Equal("Material;", new ResourceRefList("Material").ToString());
        Assert.NotEqual(0u, list.ToHash());
        Assert.False(ResourceRefList.IsNullOrEmpty(list));
        Assert.True(ResourceRefList.IsNullOrEmpty(ResourceRefList.Empty));
    }

    [Fact]
    public void Type_only_resource_refs_follow_rbfx_string_contract()
    {
        var resourceRef = ResourceRef.Parse("Material");
        Assert.Equal(StringHash.Calculate("Material"), resourceRef.Type);
        Assert.Equal(string.Empty, resourceRef.Name);
        Assert.Equal("Material;", resourceRef.ToString());

        var resourceRefList = ResourceRefList.Parse("Texture2D;A.png;B.png");
        Assert.Equal(StringHash.Calculate("Texture2D"), resourceRefList.Type);
        Assert.Equal(["A.png", "B.png"], resourceRefList.Names);
        Assert.Equal("Texture2D;A.png;B.png", resourceRefList.ToString());
    }
}
