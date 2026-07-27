using RedBolt;
using RedBolt.Graphics;
using RedBolt.Resource;
using ResourceType = global::RedBolt.Resource.Resource;

namespace RedBolt.Tests;

public sealed class TextureClassKindTests
{
    [Fact]
    public void Texture_base_chain_matches_cpp_concrete_noncopyable_reference_classes()
    {
        AssertConcreteNoncopyableReferenceClass<ResourceType>();
        AssertConcreteNoncopyableReferenceClass<ResourceWithMetadata>();
        AssertConcreteNoncopyableReferenceClass<Texture>();
        AssertConcreteNoncopyableReferenceClass<Texture2D>();
        AssertConcreteNoncopyableReferenceClass<Texture2DArray>();
        AssertConcreteNoncopyableReferenceClass<Texture3D>();
        AssertConcreteNoncopyableReferenceClass<TextureCube>();

        using var resource = new ResourceType { Name = string.Empty };
        using var resourceWithMetadata = new ResourceWithMetadata { Name = string.Empty };
        using var texture = new Texture { Name = string.Empty };
        using var texture2D = new Texture2D { Name = string.Empty };
        using var texture2DArray = new Texture2DArray { Name = string.Empty };
        using var texture3D = new Texture3D { Name = string.Empty };
        using var textureCube = new TextureCube { Name = string.Empty };
    }

    [Fact]
    public void Texture2D_dispose_releases_derived_load_state()
    {
        var texture = new Texture2D { Name = string.Empty };
        var image = new Image();
        using var parameters = new XMLFile { Name = string.Empty };
        var loadImage = typeof(Texture2D).GetField("_loadImage",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var loadParameters = typeof(Texture2D).GetField("_loadParameters",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        loadImage.SetValue(texture, image);
        loadParameters.SetValue(texture, parameters);

        texture.Dispose();

        Assert.Null(loadImage.GetValue(texture));
        Assert.Null(loadParameters.GetValue(texture));
    }

    [Fact]
    public void Texture2DArray_dispose_releases_derived_load_state()
    {
        var texture = new Texture2DArray { Name = string.Empty };
        var loadImages = typeof(Texture2DArray).GetField("_loadImages",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var loadParameters = typeof(Texture2DArray).GetField("_loadParameters",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var images = Assert.IsType<List<Image?>>(loadImages.GetValue(texture));
        images.Add(new Image());
        images.Add(new Image());
        loadParameters.SetValue(texture, new XMLFile { Name = string.Empty });

        texture.Dispose();

        Assert.Empty(images);
        Assert.Null(loadParameters.GetValue(texture));
    }

    [Fact]
    public void Texture3D_dispose_releases_derived_load_state()
    {
        var texture = new Texture3D { Name = string.Empty };
        var loadImage = typeof(Texture3D).GetField("_loadImage",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var loadParameters = typeof(Texture3D).GetField("_loadParameters",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        loadImage.SetValue(texture, new Image());
        loadParameters.SetValue(texture, new XMLFile { Name = string.Empty });

        texture.Dispose();

        Assert.Null(loadImage.GetValue(texture));
        Assert.Null(loadParameters.GetValue(texture));
    }

    [Fact]
    public void TextureCube_dispose_releases_derived_load_state()
    {
        var texture = new TextureCube { Name = string.Empty };
        var loadImageCube = typeof(TextureCube).GetField("_loadImageCube",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        loadImageCube.SetValue(texture, new ImageCube());

        texture.Dispose();

        Assert.Null(loadImageCube.GetValue(texture));
    }

    private static void AssertConcreteNoncopyableReferenceClass<T>()
    {
        var type = typeof(T);
        Assert.True(type.IsClass);
        Assert.False(type.IsAbstract);
        Assert.False(type.IsSealed);
        Assert.False(typeof(ICloneable).IsAssignableFrom(type));
        Assert.NotNull(type.GetConstructor(Type.EmptyTypes));
    }
}
