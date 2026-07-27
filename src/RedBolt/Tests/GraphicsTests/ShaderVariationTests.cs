using RedBolt.Graphics;
using RedBolt.IO;
using RedBolt.RenderAPI;
namespace RedBolt.Tests;
using ShaderType = global::RedBolt.RenderAPI.ShaderType;


public sealed class ShaderVariationTests
{
    [Fact]
    public void Class_kind_and_borrowed_parameters_match_cpp_types()
    {
        var type = typeof(ShaderVariation);
        Assert.False(type.IsValueType);
        Assert.True(type.IsSealed);
        Assert.False(typeof(ICloneable).IsAssignableFrom(type));
        Assert.Equal(typeof(RawShader), typeof(RawShader).GetMethod(nameof(IDisposable.Dispose))!.DeclaringType);

        Assert.Equal(typeof(GraphicsSystem), type.GetField("_graphics", System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance)!.FieldType);
        Assert.Equal(typeof(Shader), type.GetField("_owner", System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance)!.FieldType);
        Assert.Equal(typeof(string), type.GetField("_defines", System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance)!.FieldType);
        Assert.Null(type.GetProperty("Shader"));
        Assert.Null(type.GetProperty("ShaderType"));
        Assert.Null(type.GetMethod("PrepareSource"));

        var constructor = type.GetConstructors().Single();
        Assert.Equal(3, constructor.GetParameters().Length);
        Assert.DoesNotContain(constructor.GetParameters(), static parameter => parameter.IsOptional);

        var load = type.GetMethod("LoadByteCode", System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance);
        var save = type.GetMethod("SaveByteCode", System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance);
        Assert.True(load!.GetParameters()[0].IsIn);
        Assert.True(save!.GetParameters()[0].IsIn);
        Assert.True(typeof(FileIdentifier).IsValueType);
    }
}
