using System.Reflection;

namespace RedBolt.Tests;

public sealed class ShaderConstsTests
{
    [Fact]
    public void Built_in_shader_constants_match_native_names()
    {
        var fields = typeof(ShaderConsts).GetFields(BindingFlags.Public | BindingFlags.Static);

        Assert.Equal(67, fields.Length);
        foreach (var field in fields)
        {
            var value = Assert.IsType<ConstString>(field.GetValue(null));
            Assert.Equal(field.Name[(field.Name.IndexOf('_') + 1)..], value.ToString());
        }
    }

    [Fact]
    public void Built_in_shader_resources_match_native_names()
    {
        var fields = typeof(ShaderResources).GetFields(BindingFlags.Public | BindingFlags.Static);

        Assert.Equal(10, fields.Length);
        foreach (var field in fields)
        {
            var value = Assert.IsType<ConstString>(field.GetValue(null));
            Assert.Equal(field.Name, value.ToString());
        }
    }
}
